using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Infrastructure.Repositories.Implementations;


public class ESignRepository : IESignRepository
{
	private readonly DapperContext _context;

	public ESignRepository(DapperContext context)
	{
		_context = context;
	}


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



	public async Task UpdateTransactionStatus(
	long transactionId,
	string status,
	DateTime? completedAt,
	DateTime updatedAt,
	string? signedPdfPath = null)   // optional — pass only when PDF is present
	{
		using var db = _context.CreateConnection();
		await db.ExecuteAsync(
			"CALL usp_update_esign_transaction_status(@p_transaction_id, @p_status, @p_completed_at, @p_updated_at, @p_signed_pdf_path)",
			new
			{
				p_transaction_id = transactionId,
				p_status = status,
				p_completed_at = completedAt,
				p_updated_at = updatedAt,
				p_signed_pdf_path = signedPdfPath   // null if no PDF in webhook
			}
		);
	}
}