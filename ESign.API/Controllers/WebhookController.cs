using Microsoft.AspNetCore.Mvc;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Controllers;

// WebhookController receives the POST from SignDesk after all signers complete signing
// Route: POST /api/v1/esign/webhook/status
//
// HOW TO TEST IN DEVELOPMENT (no provider needed):
//   Open Postman → POST http://localhost:5000/api/v1/esign/webhook/status
//   Body (raw JSON):
//   {
//     "docket_id": "69e88b993963bec14cd67e7f",    ← must match a real docket_id in your DB
//     "status": "COMPLETED",
//     "signed_document_url": "https://fake.url/signed.pdf",
//     "response_time_stamp": "2026-05-01T10:00:00",
//     "signers": [
//       { "signer_ref_id": "REF001_s1", "signer_id": "abc", "status": "SIGNED", "signed_at": "2026-05-01T10:00:00" },
//       { "signer_ref_id": "REF001_s2", "signer_id": "def", "status": "SIGNED", "signed_at": "2026-05-01T10:01:00" }
//     ]
//   }
//   → Your DB should update: transaction status = SIGNED, both signers status = SIGNED
[ApiController]
[Route("api/v1/esign")]
public class WebhookController : ControllerBase
{
	private readonly IWebhookService _webhookService;

	public WebhookController(IWebhookService webhookService)
	{
		_webhookService = webhookService;
	}

	// POST /api/v1/esign/webhook/status
	// Receives the webhook payload from SignDesk after signing is complete
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

		// ── Basic request validation ──────────────────────────────────────────
		if (string.IsNullOrWhiteSpace(request.DocketId))
			return BadRequest(ResponseBuilder.InvalidRequest("docket_id is required in webhook payload.", correlationId));

		if (string.IsNullOrWhiteSpace(request.Status))
			return BadRequest(ResponseBuilder.InvalidRequest("status is required in webhook payload.", correlationId));

		// ── Attach signature to request object for service if needed ──────────
		request.WebhookSignature = webhookSignature;

		// ── Call WebhookService ───────────────────────────────────────────────
		// WebhookService handles:
		//   1. Find transaction by docket_id
		//   2. Idempotency check (is it already SIGNED?)
		//   3. Update each signer status to SIGNED
		//   4. Update transaction status to SIGNED / PARTIALLY_SIGNED
		// AppException is thrown inside service for 404 / 409 cases
		// GlobalExceptionMiddleware catches those and returns the correct status code
		await _webhookService.ProcessWebhookAsync(request, correlationId);

		SafeLogger.App($"[WEBHOOK CONTROLLER] Webhook processed successfully | DocketId: {request.DocketId} | CorrelationId: {correlationId}");

		// ── Return 200 immediately ────────────────────────────────────────────
		// SignDesk expects a 200 OK quickly — if we take too long they will retry
		// All heavy work (DB updates) is done synchronously before we reach here
		// In future, if DB updates were async jobs, we'd return 200 before the job completes
		return Ok(ResponseBuilder.WebhookProcessed(correlationId));
	}
}