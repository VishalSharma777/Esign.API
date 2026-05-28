using Microsoft.AspNetCore.Mvc;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Controllers;


[ApiController]
[Route("api/v1/esign")]
public class ESignController : ControllerBase
{
	private readonly IESignService _eSignService;

	public ESignController(IESignService eSignService)
	{
		_eSignService = eSignService;
	}

	
	[HttpPost("create")]
	public async Task<IActionResult> CreateESign([FromBody] ESignRequest request)
	{
		var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

		SafeLogger.App($"[CONTROLLER] POST /api/v1/esign/create | CorrelationId: {correlationId}");


		if (!ValidationHelper.IsValidReferenceId(request.ReferenceId))
			return BadRequest(ResponseBuilder.InvalidRequest("reference_id is required.", correlationId));

		if (string.IsNullOrWhiteSpace(request.ReferenceDocId))
			return BadRequest(ResponseBuilder.InvalidRequest("reference_doc_id is required.", correlationId));

		if (string.IsNullOrWhiteSpace(request.DocketTitle))
			return BadRequest(ResponseBuilder.InvalidRequest("docket_title is required.", correlationId));

		if (string.IsNullOrWhiteSpace(request.PdfBase64))
			return BadRequest(ResponseBuilder.InvalidRequest("pdf_base64 is required.", correlationId));

		if (string.IsNullOrWhiteSpace(request.ReturnUrl))
			return BadRequest(ResponseBuilder.InvalidRequest("return_url is required.", correlationId));

		if (request.Signers == null || request.Signers.Count != 2)
			return BadRequest(ResponseBuilder.InvalidRequest("Exactly 2 signers are required.", correlationId));

		for (int i = 0; i < request.Signers.Count; i++)
		{
			var signer = request.Signers[i];
			var num = i + 1;

		
			if (string.IsNullOrWhiteSpace(signer.SignerRefId))
				return BadRequest(ResponseBuilder.InvalidRequest($"signer {num}: signer_ref_id is required.", correlationId));

			if (string.IsNullOrWhiteSpace(signer.SignerName))
				return BadRequest(ResponseBuilder.InvalidRequest($"signer {num}: signer_name is required.", correlationId));

			if (!ValidationHelper.IsValidMobile(signer.SignerMobile))
				return BadRequest(ResponseBuilder.InvalidRequest($"signer {num}: signer_mobile must be a valid 10-digit Indian mobile number.", correlationId));

			if (!ValidationHelper.IsValidEmail(signer.SignerEmail))
				return BadRequest(ResponseBuilder.InvalidRequest($"signer {num}: signer_email is invalid.", correlationId));
		}

		// ── Call service ──────────────────────────────────────────────────────
		var (isSuccess, result, transactionId) = await _eSignService.CreateESignAsync(request, correlationId);

		if (!isSuccess || result == null)
			return StatusCode(502, ResponseBuilder.AllProvidersFailed(correlationId));

		// ── Build success response ────────────────────────────────────────────
		var signerLinks = result.SignerLinks.Select(sl =>
		{
			// Find the original signer in the request by matching signer_ref_id
			var matchedSigner = request.Signers
				.FirstOrDefault(s => s.SignerRefId == sl.SignerRefId);

			return new SignerLinkDto
			{
				SignerRefId = sl.SignerRefId,
				SignerName = matchedSigner?.SignerName,
				InvitationLink = sl.InvitationLink
			};
		}).ToList();

		SafeLogger.App($"[CONTROLLER] SUCCESS | TransactionId: {transactionId} | CorrelationId: {correlationId}");

		return Ok(ResponseBuilder.ESignCreated(
			transactionId: transactionId.ToString(),
			docketId: result.DocketId ?? string.Empty,
			documentId: result.DocumentId ?? string.Empty,
			signerLinks: signerLinks,
			correlationId: correlationId
		));
	}
}
