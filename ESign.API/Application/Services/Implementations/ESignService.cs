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
		SafeLogger.App($"[SERVICE] ESignService.CreateESignAsync START | ReferenceId: {request.ReferenceId} | CorrelationId: {correlationId}");

		var (success, response, providerName) = await _fallbackService.FallbackAsync(request, correlationId);

		if (!success || response == null)
		{
			SafeLogger.App($"[SERVICE] All providers failed | CorrelationId: {correlationId}");
			throw new AppException("PROVIDER_FAILURE", "E-sign provider is unavailable. Please try again later.", 502);
		}

		SafeLogger.App($"[SERVICE] Provider call succeeded | Provider: {providerName} | DocketId: {response.DocketId}");

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

		// Insert transaction and get the auto-generated DB primary key
		var transactionId = await _transactionRepo.InsertTransaction(transaction);

		SafeLogger.App($"[SERVICE] Transaction saved | TransactionId: {transactionId} | DocketId: {response.DocketId}");


		for (int i = 0; i < request.Signers.Count; i++)
		{
			var signerRequest = request.Signers[i];

			//  pattern: "{reference_id}_s1", "{reference_id}_s2"
			var signerRefId = $"{request.ReferenceId}_s{i + 1}";
			var signerLink = response.SignerLinks.FirstOrDefault(sl => sl.SignerRefId == signerRefId);

			var position = signerRequest.SignaturePosition;

			var signer = new ESignSigner
			{
				TransactionId = transactionId,
				SignerRefId = signerRefId,
				SignerId = signerLink?.SignerId,         // Provider's signer ID from response
				SignerName = signerRequest.SignerName ?? string.Empty,
				SignerEmail = signerRequest.SignerEmail,
				SignerMobile = signerRequest.SignerMobile ?? string.Empty,
				SignerStatus = "NOT_SIGNED",                // Initial status — signer hasn't signed yet
				InvitationLink = signerLink?.InvitationLink,  // SMS link from provider response — save for future resend
				PageNumber = 1,                           // Default page 1 (or use request if provided)
				PositionX = position?.X1,
				PositionY = position?.Y1,
				CreatedAt = now
			};

			await _signerRepo.InsertSigner(signer);

			SafeLogger.App($"[SERVICE] Signer saved | SignerRefId: {signerRefId} | TransactionId: {transactionId}");
		}

		SafeLogger.App($"[SERVICE] ESignService.CreateESignAsync END SUCCESS | TransactionId: {transactionId}");

		return (true, response, transactionId);
	}
}