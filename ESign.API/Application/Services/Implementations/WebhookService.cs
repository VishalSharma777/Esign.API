//using ESign.API.Application.DTOs.Request;
//using ESign.API.Application.Services.Interfaces;
//using ESign.API.Infrastructure.Logging;
//using ESign.API.Infrastructure.Repositories.Interfaces;
//using ESign.API.Utilities;

//namespace ESign.API.Application.Services.Implementations;

//// WebhookService processes the incoming POST from SignDesk after all signers complete signing
//// Updated flow:
////   1. Find transaction by docket_id
////   2. Idempotency check
////   3. Update each signer → SIGNED
////   4. If signed PDF Base64 is in webhook → save to disk → save path to DB
////   5. Update transaction status → SIGNED
//public class WebhookService : IWebhookService
//{
//	private readonly IESignRepository _transactionRepo;
//	private readonly IESignSignerRepository _signerRepo;
//	private readonly IPdfStorageService _pdfStorage;     // saves signed PDF to disk

//	public WebhookService(
//		IESignRepository transactionRepo,
//		IESignSignerRepository signerRepo,
//		IPdfStorageService pdfStorage)
//	{
//		_transactionRepo = transactionRepo;
//		_signerRepo = signerRepo;
//		_pdfStorage = pdfStorage;
//	}

//	public async Task ProcessWebhookAsync(WebhookRequest webhookRequest, string correlationId)
//	{
//		SafeLogger.App($"[WEBHOOK SERVICE] START | DocketId: {webhookRequest.DocketId} | CorrelationId: {correlationId}");

//		// ── Step 1: Find transaction by docket_id ─────────────────────────────
//		var transaction = await _transactionRepo.GetByDocketId(webhookRequest.DocketId ?? string.Empty);

//		if (transaction == null)
//		{
//			SafeLogger.App($"[WEBHOOK SERVICE] Transaction not found | DocketId: {webhookRequest.DocketId}");
//			throw new AppException("TRANSACTION_NOT_FOUND",
//				$"No transaction found for docket_id: {webhookRequest.DocketId}", 404);
//		}

//		SafeLogger.App($"[WEBHOOK SERVICE] Transaction found | TransactionId: {transaction.Id} | Status: {transaction.TransactionStatus}");

//		// ── Step 2: Idempotency check ─────────────────────────────────────────
//		// If already SIGNED, this webhook is a duplicate (SignDesk retrying)
//		// Return 409 — do NOT process again or save the PDF twice
//		if (transaction.TransactionStatus == "SIGNED")
//		{
//			SafeLogger.App($"[WEBHOOK SERVICE] Duplicate webhook — already SIGNED | TransactionId: {transaction.Id}");
//			throw new AppException("WEBHOOK_DUPLICATE",
//				"This webhook event has already been processed.", 409);
//		}

//		var now = DateTime.UtcNow;

//		// ── Step 3: Update each signer status to SIGNED ───────────────────────
//		foreach (var webhookSigner in webhookRequest.Signers)
//		{
//			if (string.IsNullOrEmpty(webhookSigner.SignerRefId)) continue;

//			DateTime signedAt = now;
//			if (!string.IsNullOrEmpty(webhookSigner.SignedAt))
//				DateTime.TryParse(webhookSigner.SignedAt, out signedAt);

//			await _signerRepo.UpdateSignerStatus(
//				signerRefId: webhookSigner.SignerRefId,
//				status: "SIGNED",
//				signedAt: signedAt,
//				updatedAt: now
//			);

//			SafeLogger.App($"[WEBHOOK SERVICE] Signer updated | SignerRefId: {webhookSigner.SignerRefId}");
//		}

//		// ── Step 4: Save signed PDF to disk ───────────────────────────────────
//		// SignDesk sends the signed PDF as Base64 in the webhook payload (signed_document_base64)
//		// OR provides a URL to download it (signed_document_url)
//		// We handle Base64 here — if they only send a URL, store the URL in the path column instead
//		string? signedPdfPath = null;

//		if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentBase64))
//		{
//			// SignDesk sent the PDF as Base64 directly in the webhook body
//			// Decode and save to disk using the folder structure:
//			// esign-storage/signed-documents/2026/05/week_04/2026-05-27/docket_XXXXXXXX/signed_document.pdf
//			SafeLogger.App($"[WEBHOOK SERVICE] Saving signed PDF from Base64 | DocketId: {webhookRequest.DocketId}");

//			signedPdfPath = await _pdfStorage.SaveSignedPdfAsync(
//				base64Content: webhookRequest.SignedDocumentBase64,
//				docketId: webhookRequest.DocketId!,
//				completedAt: now
//			);

//			SafeLogger.App($"[WEBHOOK SERVICE] Signed PDF saved | Path: {signedPdfPath}");
//		}
//		else if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentUrl))
//		{
//			// SignDesk sent a download URL instead of Base64
//			// Store the URL directly as the "path" — we can download it later if needed
//			// Format it clearly so it's obvious in DB it's a URL not a file path
//			signedPdfPath = $"URL:{webhookRequest.SignedDocumentUrl}";

//			SafeLogger.App($"[WEBHOOK SERVICE] Signed PDF URL received | URL stored as path | DocketId: {webhookRequest.DocketId}");
//		}

//		// Save the path (or URL) to DB if we have one
//		if (!string.IsNullOrEmpty(signedPdfPath))
//		{
//			await _transactionRepo.UpdateSignedPdfPath(
//				transactionId: transaction.Id,
//				signedPdfPath: signedPdfPath,
//				updatedAt: now
//			);
//		}

//		// ── Step 5: Check if all signers are now SIGNED ───────────────────────
//		var allSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);
//		var unsignedCount = allSigners.Count(s => s.SignerStatus != "SIGNED");

//		string newStatus;
//		DateTime? completedAt = null;

//		if (unsignedCount == 0 && allSigners.Count > 0)
//		{
//			newStatus = "SIGNED";
//			completedAt = now;
//			SafeLogger.App($"[WEBHOOK SERVICE] All signers done — SIGNED | TransactionId: {transaction.Id}");
//		}
//		else if (unsignedCount < allSigners.Count)
//		{
//			newStatus = "PARTIALLY_SIGNED";
//			SafeLogger.App($"[WEBHOOK SERVICE] Partial — PARTIALLY_SIGNED | TransactionId: {transaction.Id}");
//		}
//		else
//		{
//			newStatus = "PENDING";
//		}

//		// ── Step 6: Update transaction status ────────────────────────────────
//		await _transactionRepo.UpdateTransactionStatus(
//			transactionId: transaction.Id,
//			status: newStatus,
//			completedAt: completedAt,
//			updatedAt: now
//		);

//		SafeLogger.App($"[WEBHOOK SERVICE] END | TransactionId: {transaction.Id} | NewStatus: {newStatus}");
//	}
//}





using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Application.Services.Implementations;


///
/// SOLUTION — Null-signer fallback (NEW):
///   1. Find transaction by docket_id (as before)
///   2. If webhook.Signers is null or empty:
///        → fetch all signer rows from DB by transaction_id
///        → mark ALL of them SIGNED (provider confirmed the whole docket is done)
///      Else:
///        → update each signer individually by signer_ref_id (original flow)
///   3. Save signed PDF path (unchanged)
///   4. Update transaction status (unchanged)
///
/// The DB lookup uses transaction.Id (found via docket_id), which maps back to
/// esign_signers.transaction_id — no need to know signer_ref_id at all.
public class WebhookService : IWebhookService
{
	private readonly IESignRepository _transactionRepo;
	private readonly IESignSignerRepository _signerRepo;
	private readonly IPdfStorageService _pdfStorage;

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
		if (transaction.TransactionStatus == "SIGNED")
		{
			SafeLogger.App($"[WEBHOOK SERVICE] Duplicate webhook — already SIGNED | TransactionId: {transaction.Id}");
			throw new AppException("WEBHOOK_DUPLICATE",
				"This webhook event has already been processed.", 409);
		}

		var now = DateTime.UtcNow;

		// ── Step 3: Update signer statuses ────────────────────────────────────
		//
		// CASE A: Provider sent signer details in the webhook (standard flow)
		//   → update each signer individually by signer_ref_id
		//
		// CASE B: Provider sent signers = null / empty (MonolitSandbox behaviour)
		//   → fall back to DB: load all signers for this transaction by transaction_id
		//   → mark all of them SIGNED  (provider confirmed the docket is fully complete)
		//
		var hasWebhookSigners = webhookRequest.Signers != null && webhookRequest.Signers.Count > 0;

		if (hasWebhookSigners)
		{
			// ── CASE A: normal path — provider sent signer details ─────────────
			SafeLogger.App($"[WEBHOOK SERVICE] Signer details received in webhook | Count: {webhookRequest.Signers!.Count}");

			foreach (var webhookSigner in webhookRequest.Signers!)
			{
				if (string.IsNullOrEmpty(webhookSigner.SignerRefId))
				{
					SafeLogger.App("[WEBHOOK SERVICE] Skipping webhook signer — SignerRefId is null");
					continue;
				}

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
		}
		else
		{
			// ── CASE B: null-signer fallback ──────────────────────────────────
			// Provider didn't send signer details — we fetch all signers from DB
			// by transaction_id and mark all of them SIGNED
			SafeLogger.App($"[WEBHOOK SERVICE] Webhook signers null/empty — falling back to DB lookup | TransactionId: {transaction.Id}");

			// GetSignersByTransactionId returns decrypted signers (PII decryption in repo)
			var dbSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);

			if (dbSigners.Count == 0)
			{
				SafeLogger.App($"[WEBHOOK SERVICE] WARNING: No signers found in DB for TransactionId: {transaction.Id}");
			}

			foreach (var dbSigner in dbSigners)
			{
				if (string.IsNullOrEmpty(dbSigner.SignerRefId))
				{
					SafeLogger.App($"[WEBHOOK SERVICE] Skipping DB signer id={dbSigner.Id} — SignerRefId is null");
					continue;
				}

				// Skip signers already marked SIGNED (partial webhook scenario)
				if (dbSigner.SignerStatus == "SIGNED")
				{
					SafeLogger.App($"[WEBHOOK SERVICE] Signer already SIGNED — skipping | SignerRefId: {dbSigner.SignerRefId}");
					continue;
				}

				await _signerRepo.UpdateSignerStatus(
					signerRefId: dbSigner.SignerRefId,
					status: "SIGNED",
					signedAt: now,
					updatedAt: now
				);

				SafeLogger.App($"[WEBHOOK SERVICE] Signer updated via DB fallback | SignerRefId: {dbSigner.SignerRefId}");
			}
		}

		// ── Step 4: Save signed PDF ───────────────────────────────────────────
		string? signedPdfPath = null;

		if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentBase64))
		{
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
			signedPdfPath = $"URL:{webhookRequest.SignedDocumentUrl}";
			SafeLogger.App($"[WEBHOOK SERVICE] Signed PDF URL stored | DocketId: {webhookRequest.DocketId}");
		}

		if (!string.IsNullOrEmpty(signedPdfPath))
		{
			await _transactionRepo.UpdateSignedPdfPath(
				transactionId: transaction.Id,
				signedPdfPath: signedPdfPath,
				updatedAt: now
			);
		}

		// ── Step 5: Re-fetch all signers to determine final transaction status ─
		// We always re-fetch after updates — do NOT rely on the in-memory list
		var allSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);
		var unsignedCount = allSigners.Count(s => s.SignerStatus != "SIGNED");

		string newStatus = "PENDING";
		DateTime? completedAt = null;

		if (allSigners.Count > 0 && unsignedCount == 0)
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