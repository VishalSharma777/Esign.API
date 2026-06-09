using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;

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


		for (int i = 0; i < request.Signers.Count; i++)
		{
			var generated = ReferenceIdGenerator.NewSignerRefId(i + 1);
			request.Signers[i].SignerRefId = generated;
			SafeLogger.App($"[SERVICE] Auto-generated SignerRefId[{i + 1}]: {generated}");
		}

		var (success, response, providerName) = await _fallbackService.FallbackAsync(request, correlationId);

		if (!success || response == null)
		{
			SafeLogger.App($"[SERVICE] All providers failed | CorrelationId: {correlationId}");
			throw new AppException("PROVIDER_FAILURE", "E-sign provider unavailable. Please try again.", 502);
		}

		SafeLogger.App($"[SERVICE] Provider SUCCESS | Provider: {providerName} | DocketId: {response.DocketId}");

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
		for (int i = 0; i < request.Signers.Count; i++)
		{
			var signerRequest = request.Signers[i];
			var signerRefId = signerRequest.SignerRefId!;   

			var signerLink = response.SignerLinks
				.FirstOrDefault(sl => sl.SignerRefId == signerRefId);

			if (signerLink == null)
			{
				SafeLogger.App($"[SERVICE] WARNING: No SignerLink found for SignerRefId: {signerRefId} " +
							   $"| Available: [{string.Join(", ", response.SignerLinks.Select(x => x.SignerRefId))}]");
			}

			var position = signerRequest.SignaturePosition;

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
				PositionX = position?.X1,
				PositionY = position?.Y1,
				CreatedAt = now
			};

			await _signerRepo.InsertSigner(signer);
			SafeLogger.App($"[SERVICE] Signer saved | SignerRefId: {signerRefId} | InvitationLink: {(signerLink?.InvitationLink != null ? "present" : "null")}");
		}

		SafeLogger.App($"[SERVICE] CreateESignAsync END SUCCESS | TransactionId: {transactionId}");

		return (true, response, transactionId);
	}
}