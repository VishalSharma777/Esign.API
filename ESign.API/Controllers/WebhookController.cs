using Microsoft.AspNetCore.Mvc;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Controllers;

[ApiController]
[Route("api/v1/esign")]
public class WebhookController : ControllerBase
{
	private readonly IWebhookService _webhookService;

	public WebhookController(IWebhookService webhookService)
	{
		_webhookService = webhookService;
	}

	[HttpPost("webhook/status")]
	public async Task<IActionResult> WebhookStatus([FromBody] WebhookRequest request)
	{
		// ── Read correlation ID ───────────────────────────────────────────────
		var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

		SafeLogger.App($"[WEBHOOK CONTROLLER] POST /webhook/status received | DocketId: {request.DocketId} | CorrelationId: {correlationId}");

		// ── Validate webhook signature ────────────────────────────────────────
		// In production: SignDesk sends X-Webhook-Signature header
		// We verify this HMAC signature to confirm the webhook is genuinely from SignDesk
		// In development / testing: skip signature check (header won't be present in Postman test)
		var webhookSignature = HttpContext.Request.Headers["X-Webhook-Signature"].FirstOrDefault();

		if (!string.IsNullOrEmpty(webhookSignature))
		{
			// Production path: validate the signature
			// TODO: implement HMAC validation using SignDesk's shared secret when available
			// For now we log it — signature validation can be added once SignDesk provides the secret
			SafeLogger.App($"[WEBHOOK CONTROLLER] Signature header present — value logged for verification | CorrelationId: {correlationId}");
		}
		else
		{
			// Dev/test path: no signature header → allow through (Postman test scenario)
			// In production you would reject here:
			// return Unauthorized(ResponseBuilder.WebhookUnauthorized(correlationId));
			SafeLogger.App($"[WEBHOOK CONTROLLER] No signature header — allowing through (dev/test mode) | CorrelationId: {correlationId}");
		}

		if (string.IsNullOrWhiteSpace(request.DocketId))
			return BadRequest(ResponseBuilder.InvalidRequest("docket_id is required in webhook payload.", correlationId));

		if (string.IsNullOrWhiteSpace(request.Status))
			return BadRequest(ResponseBuilder.InvalidRequest("status is required in webhook payload.", correlationId));

		request.WebhookSignature = webhookSignature;
		await _webhookService.ProcessWebhookAsync(request, correlationId);

		SafeLogger.App($"[WEBHOOK CONTROLLER] Webhook processed successfully | DocketId: {request.DocketId} | CorrelationId: {correlationId}");

		// ── Return 200 immediately ────────────────────────────────────────────
		// SignDesk expects a 200 OK quickly — if we take too long they will retry
		return Ok(ResponseBuilder.WebhookProcessed(correlationId));
	}
}