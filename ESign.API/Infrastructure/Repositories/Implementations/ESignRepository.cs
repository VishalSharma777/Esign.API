using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Infrastructure.Repositories.Implementations;

// ESignRepository handles all DB operations on esign_transactions table
// All calls go through stored procedures — no raw SQL in application code
public class ESignRepository : IESignRepository
{
	private readonly DapperContext _context;

	public ESignRepository(DapperContext context)
	{
		_context = context;
	}

	// InsertTransaction — calls usp_insert_esign_transaction()
	// Inserts a new row, returns the auto-generated primary key
	public async Task<long> InsertTransaction(ESignTransaction transaction)
	{
		SafeLogger.App($"[DB] InsertTransaction START | ReferenceId: {transaction.ReferenceId}");

		var sql = @"SELECT usp_insert_esign_transaction(
            @ProviderId, @ReferenceId, @DocketTitle, @DocketId,
            @DocumentId, @TransactionStatus, @ExpiresAt, @CreatedAt
        );";

		using var db = _context.CreateConnection();

		try
		{
			var newId = await db.ExecuteScalarAsync<long>(sql, new
			{
				transaction.ProviderId,
				transaction.ReferenceId,
				transaction.DocketTitle,
				transaction.DocketId,
				transaction.DocumentId,
				transaction.TransactionStatus,
				transaction.ExpiresAt,
				transaction.CreatedAt
			});

			SafeLogger.App($"[DB] InsertTransaction SUCCESS | NewId: {newId}");
			return newId;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] InsertTransaction FAILED | ReferenceId: {transaction.ReferenceId}");
			throw;
		}
	}

	// GetByDocketId — calls usp_get_esign_transaction_by_docket_id()
	// Finds a transaction by provider's docket_id (received in webhook)
	public async Task<ESignTransaction?> GetByDocketId(string docketId)
	{
		SafeLogger.App($"[DB] GetByDocketId START | DocketId: {docketId}");

		using var db = _context.CreateConnection();

		try
		{
			var result = await db.QueryFirstOrDefaultAsync<ESignTransaction>(
				"SELECT * FROM usp_get_esign_transaction_by_docket_id(@p_docket_id)",
				new { p_docket_id = docketId }
			);

			SafeLogger.App(result != null
				? $"[DB] GetByDocketId HIT | TransactionId: {result.Id}"
				: $"[DB] GetByDocketId MISS | DocketId: {docketId}");

			return result;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] GetByDocketId FAILED | DocketId: {docketId}");
			throw;
		}
	}

	// UpdateTransactionStatus — calls usp_update_esign_transaction_status()
	// Updates status + completed_at + updated_at when signing completes
	public async Task UpdateTransactionStatus(
		long transactionId, string status, DateTime? completedAt, DateTime updatedAt)
	{
		SafeLogger.App($"[DB] UpdateTransactionStatus START | TransactionId: {transactionId} | Status: {status}");

		using var db = _context.CreateConnection();

		try
		{
			await db.ExecuteAsync(
				"CALL usp_update_esign_transaction_status(@p_transaction_id, @p_status, @p_completed_at, @p_updated_at)",
				new { p_transaction_id = transactionId, p_status = status, p_completed_at = completedAt, p_updated_at = updatedAt }
			);

			SafeLogger.App($"[DB] UpdateTransactionStatus SUCCESS | TransactionId: {transactionId}");
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] UpdateTransactionStatus FAILED | TransactionId: {transactionId}");
			throw;
		}
	}

	// UpdateSignedPdfPath — calls usp_update_esign_signed_pdf_path()
	// Saves the relative file path of the signed PDF after it is written to disk
	// Called from WebhookService after PdfStorageService.SaveSignedPdfAsync() returns the path
	public async Task UpdateSignedPdfPath(long transactionId, string signedPdfPath, DateTime updatedAt)
	{
		SafeLogger.App($"[DB] UpdateSignedPdfPath START | TransactionId: {transactionId} | Path: {signedPdfPath}");

		using var db = _context.CreateConnection();

		try
		{
			await db.ExecuteAsync(
				"CALL usp_update_esign_signed_pdf_path(@p_transaction_id, @p_signed_pdf_path, @p_updated_at)",
				new
				{
					p_transaction_id = transactionId,
					p_signed_pdf_path = signedPdfPath,
					p_updated_at = updatedAt
				}
			);

			SafeLogger.App($"[DB] UpdateSignedPdfPath SUCCESS | TransactionId: {transactionId}");
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] UpdateSignedPdfPath FAILED | TransactionId: {transactionId}");
			throw;
		}
	}
}