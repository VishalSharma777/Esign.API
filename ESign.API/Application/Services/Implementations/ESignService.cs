using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;
using Newtonsoft.Json;

namespace ESign.API.Application.Services.Implementations;

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
		SafeLogger.App($"[SERVICE] CreateESignAsync START | CorrelationId: {correlationId}");

		// ── Step 1: Auto-generate IDs before calling provider ─────────────────
		if (string.IsNullOrWhiteSpace(request.ReferenceId))
		{
			request.ReferenceId = ReferenceIdGenerator.NewReferenceId();
			SafeLogger.App($"[SERVICE] Auto-generated ReferenceId: {request.ReferenceId}");
		}

		if (string.IsNullOrWhiteSpace(request.ReferenceDocId))
		{
			request.ReferenceDocId = ReferenceIdGenerator.NewReferenceDocId();
			SafeLogger.App($"[SERVICE] Auto-generated ReferenceDocId: {request.ReferenceDocId}");
		}

		// Generate signer_ref_id on the request BEFORE calling provider
		// so the provider echoes them back in SignerLinks → invitation link lookup works
		for (int i = 0; i < request.Signers.Count; i++)
		{
			var generated = ReferenceIdGenerator.NewSignerRefId(i + 1);
			request.Signers[i].SignerRefId = generated;
			SafeLogger.App($"[SERVICE] Auto-generated SignerRefId[{i + 1}]: {generated}");
		}

		// ── Step 2: Call provider via fallback chain ──────────────────────────
		var (success, response, providerName) = await _fallbackService.FallbackAsync(request, correlationId);

		if (!success || response == null)
		{
			SafeLogger.App($"[SERVICE] All providers failed | CorrelationId: {correlationId}");
			throw new AppException("PROVIDER_FAILURE", "E-sign provider unavailable. Please try again.", 502);
		}

		SafeLogger.App($"[SERVICE] Provider SUCCESS | Provider: {providerName} | DocketId: {response.DocketId}");

		// ── Step 3: Persist transaction ───────────────────────────────────────
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
		SafeLogger.App($"[SERVICE] Transaction saved | TransactionId: {transactionId}");

		// ── Step 4: Persist signers ───────────────────────────────────────────
		for (int i = 0; i < request.Signers.Count; i++)
		{
			var signerRequest = request.Signers[i];
			var signerRefId = signerRequest.SignerRefId!;

			var signerLink = response.SignerLinks
				.FirstOrDefault(sl => sl.SignerRefId == signerRefId);

			if (signerLink == null)
				SafeLogger.App($"[SERVICE] WARNING: No SignerLink matched for SignerRefId: {signerRefId}");

			// Build the signature_position JSON string from all 4 coordinates
			// Stored as JSONB in DB: { "x1": 20, "y1": 20, "x2": 120, "y2": 60 }
			// If caller didn't provide a position, store null
			var position = signerRequest.SignaturePosition;
			var positionJson = position == null
				? null
				: JsonConvert.SerializeObject(new
				{
					x1 = position.X1,
					y1 = position.Y1,
					x2 = position.X2,
					y2 = position.Y2
				});

			var signer = new ESignSigner
			{
				TransactionId = transactionId,
				SignerRefId = signerRefId,
				SignerId = signerLink?.SignerId,
				SignerName = signerRequest.SignerName ?? string.Empty,
				SignerEmail = signerRequest.SignerEmail,
				SignerMobile = signerRequest.SignerMobile ?? string.Empty,
				SignerStatus = "NOT_SIGNED",
				InvitationLink = signerLink?.InvitationLink,
				PageNumber = 1,
				SignaturePosition = positionJson,   // all 4 coords as JSON string → stored as JSONB
				CreatedAt = now
			};

			await _signerRepo.InsertSigner(signer);

			SafeLogger.App($"[SERVICE] Signer saved | SignerRefId: {signerRefId} " +
						   $"| Position: {positionJson ?? "null"} " +
						   $"| InvitationLink: {(signerLink?.InvitationLink != null ? "present" : "null")}");
		}

		SafeLogger.App($"[SERVICE] CreateESignAsync END SUCCESS | TransactionId: {transactionId}");

		return (true, response, transactionId);
	}
}