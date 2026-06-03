using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Application.Services.Implementations;

/// <summary>
/// ESignService — orchestrates the full e-sign creation flow.
///
/// Auto-generation rules (NEW):
///   reference_id      → generated here if caller sends null/empty
///   reference_doc_id  → generated here if caller sends null/empty
///   signer_ref_id     → always generated here (pattern: SGN{tick}{guid}_s{n})
///                       caller's signer_ref_id in the request is IGNORED —
///                       we own this ID because we need it to match webhook updates
///
/// Why we own signer_ref_id:
///   SignDesk echoes signer_ref_id back in both the create response AND the webhook.
///   Our WebhookService uses it to look up the correct row in esign_signers.
///   If the caller sets an arbitrary ID and later sends a webhook with a different
///   format, the lookup would break. Generating it here guarantees consistency.
/// </summary>
public class ESignService : IESignService
{
	private readonly IESignFallbackService _fallbackService;
	private readonly IESignRepository _transactionRepo;
	private readonly IESignSignerRepository _signerRepo;

	public ESignService(
		IESignFallbackService fallbackService,
		IESignRepository transactionRepo,
		IESignSignerRepository signerRepo)
	{
		_fallbackService = fallbackService;
		_transactionRepo = transactionRepo;
		_signerRepo = signerRepo;
	}

	public async Task<(bool isSuccess, ESignCommonResponseDto? result, long transactionId)> CreateESignAsync(
		ESignRequest request,
		string correlationId)
	{
		SafeLogger.App($"[SERVICE] ESignService.CreateESignAsync START | CorrelationId: {correlationId}");

		// ── Step 1: Auto-generate IDs if caller didn't provide them ──────────
		// reference_id: top-level transaction reference
		if (string.IsNullOrWhiteSpace(request.ReferenceId))
		{
			request.ReferenceId = ReferenceIdGenerator.NewReferenceId();
			SafeLogger.App($"[SERVICE] Auto-generated ReferenceId: {request.ReferenceId}");
		}

		// reference_doc_id: document reference passed to provider
		if (string.IsNullOrWhiteSpace(request.ReferenceDocId))
		{
			request.ReferenceDocId = ReferenceIdGenerator.NewReferenceDocId();
			SafeLogger.App($"[SERVICE] Auto-generated ReferenceDocId: {request.ReferenceDocId}");
		}

		// signer_ref_id: always regenerated here — we own this ID
		// Pattern: SGN{8-digit-tick}{8-char-hex}_s{n}
		// The caller's signer_ref_id (if any) is discarded — we generate a canonical one
		for (int i = 0; i < request.Signers.Count; i++)
		{
			var generated = ReferenceIdGenerator.NewSignerRefId(i + 1);
			request.Signers[i].SignerRefId = generated;
			SafeLogger.App($"[SERVICE] Auto-generated SignerRefId[{i + 1}]: {generated}");
		}

		// ── Step 2: Call provider via fallback chain ─────────────────────────
		var (success, response, providerName) = await _fallbackService.FallbackAsync(request, correlationId);

		if (!success || response == null)
		{
			SafeLogger.App($"[SERVICE] All providers failed | CorrelationId: {correlationId}");
			throw new AppException("PROVIDER_FAILURE", "E-sign provider is unavailable. Please try again later.", 502);
		}

		SafeLogger.App($"[SERVICE] Provider call succeeded | Provider: {providerName} | DocketId: {response.DocketId}");

		// ── Step 3: Persist transaction ──────────────────────────────────────
		var now = DateTime.UtcNow;
		var transaction = new ESignTransaction
		{
			ProviderId = response.ProviderId,
			ReferenceId = request.ReferenceId!,
			DocketTitle = request.DocketTitle ?? "E-Sign Document",
			DocketId = response.DocketId,
			DocumentId = response.DocumentId,
			TransactionStatus = "PENDING",
			ExpiresAt = now.AddMinutes(10),
			CreatedAt = now
		};

		var transactionId = await _transactionRepo.InsertTransaction(transaction);
		SafeLogger.App($"[SERVICE] Transaction saved | TransactionId: {transactionId} | DocketId: {response.DocketId}");

		// ── Step 4: Persist signers (PII encryption happens inside repository) ─
		// We use our generated signer_ref_id (already set on request.Signers[i].SignerRefId above)
		// The provider echoes it back in response.SignerLinks — we match on that to get invitation links
		for (int i = 0; i < request.Signers.Count; i++)
		{
			var signerRequest = request.Signers[i];
			var signerRefId = signerRequest.SignerRefId!;  // already generated in Step 1

			// Match invitation link from provider response by our signer_ref_id
			var signerLink = response.SignerLinks.FirstOrDefault(sl => sl.SignerRefId == signerRefId);

			var position = signerRequest.SignaturePosition;

			var signer = new ESignSigner
			{
				TransactionId = transactionId,
				SignerRefId = signerRefId,                       // our generated ID
				SignerId = signerLink?.SignerId,              // provider's signer ID
				SignerName = signerRequest.SignerName ?? string.Empty,   // PII — encrypted in repo
				SignerEmail = signerRequest.SignerEmail,         // PII — encrypted in repo
				SignerMobile = signerRequest.SignerMobile ?? string.Empty, // PII — encrypted in repo
				SignerStatus = "NOT_SIGNED",
				InvitationLink = signerLink?.InvitationLink,       // PII — encrypted in repo
				PageNumber = 1,
				PositionX = position?.X1,
				PositionY = position?.Y1,
				CreatedAt = now
			};

			// InsertSigner calls PiiEncryptionService.EncryptSigner() internally
			await _signerRepo.InsertSigner(signer);

			SafeLogger.App($"[SERVICE] Signer saved (PII encrypted) | SignerRefId: {signerRefId} | TransactionId: {transactionId}");
		}

		SafeLogger.App($"[SERVICE] ESignService.CreateESignAsync END SUCCESS | TransactionId: {transactionId}");

		return (true, response, transactionId);
	}
}