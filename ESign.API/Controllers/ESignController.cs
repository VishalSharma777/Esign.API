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
	private readonly IHealthService _healthService;

	public ESignController(IESignService eSignService, IHealthService healthService)
	{
		_eSignService = eSignService;
	    _healthService = healthService;
	}

	[HttpPost("create")]
	public async Task<IActionResult> CreateESign([FromBody] ESignRequest request)
	{
		var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

		SafeLogger.App($"[CONTROLLER] POST /api/v1/esign/create | CorrelationId: {correlationId}");

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

			// signer_name is required
			if (string.IsNullOrWhiteSpace(signer.SignerName))
				return BadRequest(ResponseBuilder.InvalidRequest($"signer {num}: signer_name is required.", correlationId));

			// signer_mobile must be a valid 10-digit Indian number
			if (!ValidationHelper.IsValidMobile(signer.SignerMobile))
				return BadRequest(ResponseBuilder.InvalidRequest(
					$"signer {num}: signer_mobile must be a valid 10-digit Indian mobile number.", correlationId));

			// signer_email is required and must be valid
			if (!ValidationHelper.IsValidEmail(signer.SignerEmail))
				return BadRequest(ResponseBuilder.InvalidRequest(
					$"signer {num}: signer_email is invalid.", correlationId));
		}

		// ── Call service ──────────────────────────────────────────────────────
		var (isSuccess, result, transactionId) = await _eSignService.CreateESignAsync(request, correlationId);

		if (!isSuccess || result == null)
			return StatusCode(502, ResponseBuilder.AllProvidersFailed(correlationId));

		// ── Build success response ────────────────────────────────────────────
		var signerLinks = result.SignerLinks.Select(sl =>
		{
			var matchedSigner = request.Signers
				.FirstOrDefault(s => s.SignerRefId == sl.SignerRefId);

			return new SignerLinkDto
			{
				SignerRefId = sl.SignerRefId,           // auto-generated ID echoed back
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


	[HttpGet("health")]
	public async Task<IActionResult> Health()
	{
		SafeLogger.App("[HEALTH CONTROLLER] GET /health");
		var result = await _healthService.GetHealthAsync();
		return Ok(result);
	}


	[HttpGet("health/database")]
	public async Task<IActionResult> DatabaseHealth()
	{
		SafeLogger.App("[HEALTH CONTROLLER] GET /health/database");
		var result = await _healthService.GetHealthReadyAsync();
		var status = result.GetType().GetProperty("status")?.GetValue(result)?.ToString();
		return status == "Healthy" ? Ok(result) : StatusCode(503, result);
	}

}
