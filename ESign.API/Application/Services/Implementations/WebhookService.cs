//using ESign.API.Application.DTOs.Request;
//using ESign.API.Application.Services.Interfaces;
//using ESign.API.Infrastructure.Logging;
//using ESign.API.Infrastructure.Repositories.Interfaces;
//using ESign.API.Utilities;

//namespace ESign.API.Application.Services.Implementations;

//// WebhookService processes the incoming POST from SignDesk after all signers complete signing
//// This is Step 5-7 from the flow diagram:
////   5. POST /webhook/esign-status received (completed status, signers, signed PDF link)
////   6. Validate webhook (signature + idempotency check)
////   7. Update transaction status = COMPLETED, save PDF URL
//// Dev testing: hit POST /api/v1/esign/webhook/status with Postman using a fake payload
////   — the controller calls this service, DB gets updated, you verify the logic works
//public class WebhookService : IWebhookService
//{
//	private readonly IESignRepository _transactionRepo;
//	private readonly IESignSignerRepository _signerRepo;

//	public WebhookService(
//		IESignRepository transactionRepo,
//		IESignSignerRepository signerRepo)
//	{
//		_transactionRepo = transactionRepo;
//		_signerRepo = signerRepo;
//	}

//	// ProcessWebhookAsync — full webhook handling flow
//	// Called from WebhookController after signature validation passes
//	public async Task ProcessWebhookAsync(WebhookRequest webhookRequest, string correlationId)
//	{
//		SafeLogger.App($"[WEBHOOK SERVICE] ProcessWebhookAsync START | DocketId: {webhookRequest.DocketId} | CorrelationId: {correlationId}");

//		// ── Step 1: Find the transaction in DB by docket_id ───────────────────
//		// The webhook payload contains docket_id — this is the same docket_id we saved
//		// in esign_transactions when we created the e-sign request
//		var transaction = await _transactionRepo.GetByDocketId(webhookRequest.DocketId ?? string.Empty);

//		if (transaction == null)
//		{
//			// Webhook references a docket we don't know about — throw 404
//			SafeLogger.App($"[WEBHOOK SERVICE] Transaction not found | DocketId: {webhookRequest.DocketId}");
//			throw new AppException("TRANSACTION_NOT_FOUND",
//				$"No transaction found for docket_id: {webhookRequest.DocketId}", 404);
//		}

//		SafeLogger.App($"[WEBHOOK SERVICE] Transaction found | TransactionId: {transaction.Id} | CurrentStatus: {transaction.TransactionStatus}");

//		// ── Step 2: Idempotency check ─────────────────────────────────────────
//		// If this transaction is already SIGNED, we already processed this webhook
//		// Return 409 Conflict — do NOT process again (prevents duplicate DB updates)
//		// This handles the case where SignDesk retries the webhook (e.g. our server was slow)
//		if (transaction.TransactionStatus == "SIGNED")
//		{
//			SafeLogger.App($"[WEBHOOK SERVICE] Duplicate webhook — already processed | TransactionId: {transaction.Id}");
//			throw new AppException("WEBHOOK_DUPLICATE",
//				"This webhook event has already been processed.", 409);
//		}

//		var now = DateTime.UtcNow;

//		// ── Step 3: Update each signer's status ───────────────────────────────
//		// Webhook payload contains a signers list with each signer's status + signed_at
//		// We update each signer row in esign_signers table
//		foreach (var webhookSigner in webhookRequest.Signers)
//		{
//			if (string.IsNullOrEmpty(webhookSigner.SignerRefId)) continue;

//			// Parse signed_at from webhook payload string to DateTime
//			// If parsing fails, use current UTC time as fallback
//			DateTime signedAt = now;
//			if (!string.IsNullOrEmpty(webhookSigner.SignedAt))
//			{
//				DateTime.TryParse(webhookSigner.SignedAt, out signedAt);
//			}

//			// Mark signer as SIGNED and record the exact time they signed
//			await _signerRepo.UpdateSignerStatus(
//				signerRefId: webhookSigner.SignerRefId,
//				status: "SIGNED",
//				signedAt: signedAt,
//				updatedAt: now
//			);

//			SafeLogger.App($"[WEBHOOK SERVICE] Signer updated to SIGNED | SignerRefId: {webhookSigner.SignerRefId}");
//		}

//		// ── Step 4: Check if ALL signers have now signed ──────────────────────
//		// After updating signers, re-read from DB to check statuses
//		var allSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);

//		// Count how many signers have NOT yet signed
//		var unsignedCount = allSigners.Count(s => s.SignerStatus != "SIGNED");

//		SafeLogger.App($"[WEBHOOK SERVICE] Signer check | Total: {allSigners.Count} | Unsigned: {unsignedCount}");

//		// ── Step 5: Update transaction status based on signing state ──────────
//		string newStatus;
//		DateTime? completedAt = null;

//		if (unsignedCount == 0 && allSigners.Count > 0)
//		{
//			// All signers have signed → SIGNED (fully complete)
//			newStatus = "SIGNED";
//			completedAt = now;
//			SafeLogger.App($"[WEBHOOK SERVICE] All signers completed — setting SIGNED | TransactionId: {transaction.Id}");
//		}
//		else if (unsignedCount < allSigners.Count)
//		{
//			// Some but not all signers signed → PARTIALLY_SIGNED
//			// This can happen if you use sequential signing instead of parallel
//			newStatus = "PARTIALLY_SIGNED";
//			SafeLogger.App($"[WEBHOOK SERVICE] Partial signing — setting PARTIALLY_SIGNED | TransactionId: {transaction.Id}");
//		}
//		else
//		{
//			// Webhook received but no one signed — keep as PENDING (shouldn't normally happen)
//			newStatus = "PENDING";
//		}

//		// Update transaction row with new status + completed_at timestamp
//		await _transactionRepo.UpdateTransactionStatus(
//			transactionId: transaction.Id,
//			status: newStatus,
//			completedAt: completedAt,
//			updatedAt: now
//		);

//		SafeLogger.App($"[WEBHOOK SERVICE] Transaction updated | TransactionId: {transaction.Id} | NewStatus: {newStatus}");
//		SafeLogger.App($"[WEBHOOK SERVICE] ProcessWebhookAsync END | DocketId: {webhookRequest.DocketId}");
//	}
//}








using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Application.Services.Implementations;

// WebhookService processes the incoming POST from SignDesk after all signers complete signing
// Updated flow:
//   1. Find transaction by docket_id
//   2. Idempotency check
//   3. Update each signer → SIGNED
//   4. If signed PDF Base64 is in webhook → save to disk → save path to DB
//   5. Update transaction status → SIGNED
public class WebhookService : IWebhookService
{
	private readonly IESignRepository _transactionRepo;
	private readonly IESignSignerRepository _signerRepo;
	private readonly IPdfStorageService _pdfStorage;     // saves signed PDF to disk

	public WebhookService(
		IESignRepository transactionRepo,
		IESignSignerRepository signerRepo,
		IPdfStorageService pdfStorage)
	{
		_transactionRepo = transactionRepo;
		_signerRepo = signerRepo;
		_pdfStorage = pdfStorage;
	}

	public async Task ProcessWebhookAsync(WebhookRequest webhookRequest, string correlationId)
	{
		SafeLogger.App($"[WEBHOOK SERVICE] START | DocketId: {webhookRequest.DocketId} | CorrelationId: {correlationId}");

		// ── Step 1: Find transaction by docket_id ─────────────────────────────
		var transaction = await _transactionRepo.GetByDocketId(webhookRequest.DocketId ?? string.Empty);

		if (transaction == null)
		{
			SafeLogger.App($"[WEBHOOK SERVICE] Transaction not found | DocketId: {webhookRequest.DocketId}");
			throw new AppException("TRANSACTION_NOT_FOUND",
				$"No transaction found for docket_id: {webhookRequest.DocketId}", 404);
		}

		SafeLogger.App($"[WEBHOOK SERVICE] Transaction found | TransactionId: {transaction.Id} | Status: {transaction.TransactionStatus}");

		// ── Step 2: Idempotency check ─────────────────────────────────────────
		// If already SIGNED, this webhook is a duplicate (SignDesk retrying)
		// Return 409 — do NOT process again or save the PDF twice
		if (transaction.TransactionStatus == "SIGNED")
		{
			SafeLogger.App($"[WEBHOOK SERVICE] Duplicate webhook — already SIGNED | TransactionId: {transaction.Id}");
			throw new AppException("WEBHOOK_DUPLICATE",
				"This webhook event has already been processed.", 409);
		}

		var now = DateTime.UtcNow;

		// ── Step 3: Update each signer status to SIGNED ───────────────────────
		foreach (var webhookSigner in webhookRequest.Signers)
		{
			if (string.IsNullOrEmpty(webhookSigner.SignerRefId)) continue;

			DateTime signedAt = now;
			if (!string.IsNullOrEmpty(webhookSigner.SignedAt))
				DateTime.TryParse(webhookSigner.SignedAt, out signedAt);

			await _signerRepo.UpdateSignerStatus(
				signerRefId: webhookSigner.SignerRefId,
				status: "SIGNED",
				signedAt: signedAt,
				updatedAt: now
			);

			SafeLogger.App($"[WEBHOOK SERVICE] Signer updated | SignerRefId: {webhookSigner.SignerRefId}");
		}

		// ── Step 4: Save signed PDF to disk ───────────────────────────────────
		// SignDesk sends the signed PDF as Base64 in the webhook payload (signed_document_base64)
		// OR provides a URL to download it (signed_document_url)
		// We handle Base64 here — if they only send a URL, store the URL in the path column instead
		string? signedPdfPath = null;

		if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentBase64))
		{
			// SignDesk sent the PDF as Base64 directly in the webhook body
			// Decode and save to disk using the folder structure:
			// esign-storage/signed-documents/2026/05/week_04/2026-05-27/docket_XXXXXXXX/signed_document.pdf
			SafeLogger.App($"[WEBHOOK SERVICE] Saving signed PDF from Base64 | DocketId: {webhookRequest.DocketId}");

			signedPdfPath = await _pdfStorage.SaveSignedPdfAsync(
				base64Content: webhookRequest.SignedDocumentBase64,
				docketId: webhookRequest.DocketId!,
				completedAt: now
			);

			SafeLogger.App($"[WEBHOOK SERVICE] Signed PDF saved | Path: {signedPdfPath}");
		}
		else if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentUrl))
		{
			// SignDesk sent a download URL instead of Base64
			// Store the URL directly as the "path" — we can download it later if needed
			// Format it clearly so it's obvious in DB it's a URL not a file path
			signedPdfPath = $"URL:{webhookRequest.SignedDocumentUrl}";

			SafeLogger.App($"[WEBHOOK SERVICE] Signed PDF URL received | URL stored as path | DocketId: {webhookRequest.DocketId}");
		}

		// Save the path (or URL) to DB if we have one
		if (!string.IsNullOrEmpty(signedPdfPath))
		{
			await _transactionRepo.UpdateSignedPdfPath(
				transactionId: transaction.Id,
				signedPdfPath: signedPdfPath,
				updatedAt: now
			);
		}

		// ── Step 5: Check if all signers are now SIGNED ───────────────────────
		var allSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);
		var unsignedCount = allSigners.Count(s => s.SignerStatus != "SIGNED");

		string newStatus;
		DateTime? completedAt = null;

		if (unsignedCount == 0 && allSigners.Count > 0)
		{
			newStatus = "SIGNED";
			completedAt = now;
			SafeLogger.App($"[WEBHOOK SERVICE] All signers done — SIGNED | TransactionId: {transaction.Id}");
		}
		else if (unsignedCount < allSigners.Count)
		{
			newStatus = "PARTIALLY_SIGNED";
			SafeLogger.App($"[WEBHOOK SERVICE] Partial — PARTIALLY_SIGNED | TransactionId: {transaction.Id}");
		}
		else
		{
			newStatus = "PENDING";
		}

		// ── Step 6: Update transaction status ────────────────────────────────
		await _transactionRepo.UpdateTransactionStatus(
			transactionId: transaction.Id,
			status: newStatus,
			completedAt: completedAt,
			updatedAt: now
		);

		SafeLogger.App($"[WEBHOOK SERVICE] END | TransactionId: {transaction.Id} | NewStatus: {newStatus}");
	}
}