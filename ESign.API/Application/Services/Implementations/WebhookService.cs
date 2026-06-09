
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Application.Services.Implementations
{
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
			SafeLogger.App($"[WEBHOOK] START | DocketId: {webhookRequest.DocketId} | CorrelationId: {correlationId}");

			// ── Step 1: Find transaction by docket_id ─────────────────────────────
			var transaction = await _transactionRepo.GetByDocketId(webhookRequest.DocketId ?? string.Empty);

			if (transaction == null)
			{
				SafeLogger.App($"[WEBHOOK] Transaction NOT FOUND | DocketId: {webhookRequest.DocketId}");
				throw new AppException("TRANSACTION_NOT_FOUND",
					$"No transaction found for docket_id: {webhookRequest.DocketId}", 404);
			}

			SafeLogger.App($"[WEBHOOK] Transaction found | Id: {transaction.Id} | Status: {transaction.TransactionStatus}");

			// ── Step 2: Idempotency check ─────────────────────────────────────────
			if (transaction.TransactionStatus == "SIGNED")
			{
				SafeLogger.App($"[WEBHOOK] Already SIGNED — duplicate webhook ignored | TransactionId: {transaction.Id}");
				throw new AppException("WEBHOOK_DUPLICATE", "Already processed.", 409);
			}

			var now = DateTime.UtcNow;

			// ── Step 3: Update signer statuses ────────────────────────────────────
	

			var hasUsableSignerRefIds = webhookRequest.Signers != null
				&& webhookRequest.Signers.Any(s => !string.IsNullOrEmpty(s.SignerRefId));

			if (hasUsableSignerRefIds)
			{
				// ── PATH A: provider sent real signer_ref_ids ─────────────────────
				SafeLogger.App($"[WEBHOOK] PATH A — updating {webhookRequest.Signers!.Count} signers by ref_id");

				foreach (var ws in webhookRequest.Signers!)
				{
					if (string.IsNullOrEmpty(ws.SignerRefId)) continue;

					var signedAt = now;
					if (!string.IsNullOrEmpty(ws.SignedAt))
						DateTime.TryParse(ws.SignedAt, out signedAt);

					await _signerRepo.UpdateSignerStatus(ws.SignerRefId, "SIGNED", signedAt, now);
					SafeLogger.App($"[WEBHOOK] PATH A — signer updated | SignerRefId: {ws.SignerRefId}");
				}
			}
			else
			{
				// ── PATH B: provider sent null signer_ref_ids (your exact scenario) ─
				// Load all signer rows for this transaction from DB, mark all SIGNED
				SafeLogger.App($"[WEBHOOK] PATH B — provider sent null signer_ref_ids. Loading signers from DB | TransactionId: {transaction.Id}");

				var dbSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);

				if (dbSigners.Count == 0)
				{
					SafeLogger.App($"[WEBHOOK] WARNING: No signers in DB for TransactionId: {transaction.Id}");
				}

				foreach (var dbSigner in dbSigners)
				{
					if (string.IsNullOrEmpty(dbSigner.SignerRefId))
					{
						SafeLogger.App($"[WEBHOOK] Skipping signer id={dbSigner.Id} — no SignerRefId in DB");
						continue;
					}

					if (dbSigner.SignerStatus == "SIGNED")
					{
						SafeLogger.App($"[WEBHOOK] Signer already SIGNED — skip | SignerRefId: {dbSigner.SignerRefId}");
						continue;
					}

					await _signerRepo.UpdateSignerStatus(dbSigner.SignerRefId, "SIGNED", now, now);
					SafeLogger.App($"[WEBHOOK] PATH B — signer updated via DB lookup | SignerRefId: {dbSigner.SignerRefId}");
				}
			}

			// ── Step 4: Save signed PDF ───────────────────────────────────────────
			string? signedPdfPath = null;

			if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentBase64))
			{
				SafeLogger.App($"[WEBHOOK] Saving Base64 PDF | DocketId: {webhookRequest.DocketId}");
				signedPdfPath = await _pdfStorage.SaveSignedPdfAsync(
					webhookRequest.SignedDocumentBase64,
					webhookRequest.DocketId!,
					now);
				SafeLogger.App($"[WEBHOOK] PDF saved | Path: {signedPdfPath}");
			}
			else if (!string.IsNullOrEmpty(webhookRequest.SignedDocumentUrl))
			{
				signedPdfPath = $"URL:{webhookRequest.SignedDocumentUrl}";
				SafeLogger.App($"[WEBHOOK] PDF URL stored | DocketId: {webhookRequest.DocketId}");
			}


			if (!string.IsNullOrEmpty(signedPdfPath))
			{
				await _transactionRepo.UpdateTransactionStatus(transaction.Id, "SIGNED", now, now, signedPdfPath);
			}

			// ── Step 5: Re-fetch signers and decide final transaction status ──────
			// ALWAYS re-fetch after updates — never trust the in-memory list
			var allSigners = await _signerRepo.GetSignersByTransactionId(transaction.Id);
			var unsignedCount = allSigners.Count(s => s.SignerStatus != "SIGNED");

			string newStatus = "PENDING";
			DateTime? completedAt = null;

			if (allSigners.Count > 0 && unsignedCount == 0)
			{
				newStatus = "SIGNED";
				completedAt = now;
				SafeLogger.App($"[WEBHOOK] All {allSigners.Count} signers SIGNED → transaction SIGNED | TransactionId: {transaction.Id}");
			}
			else if (unsignedCount < allSigners.Count)
			{
				newStatus = "PARTIALLY_SIGNED";
				SafeLogger.App($"[WEBHOOK] {allSigners.Count - unsignedCount}/{allSigners.Count} signed → PARTIALLY_SIGNED | TransactionId: {transaction.Id}");
			}
			else
			{
				// unsignedCount == allSigners.Count — nothing changed (shouldn't normally happen)
				SafeLogger.App($"[WEBHOOK] WARNING: No signers were updated. Transaction stays PENDING | TransactionId: {transaction.Id}");
			}

			// ── Step 6: Update transaction status ────────────────────────────────
			await _transactionRepo.UpdateTransactionStatus(transaction.Id, newStatus, completedAt, now);

			SafeLogger.App($"[WEBHOOK] END | TransactionId: {transaction.Id} | FinalStatus: {newStatus}");
		}
	}
}