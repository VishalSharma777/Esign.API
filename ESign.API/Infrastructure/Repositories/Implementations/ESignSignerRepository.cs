using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Infrastructure.Repositories.Implementations;

// ESignSignerRepository handles all DB operations on the esign_signers table
// All calls go through stored procedures — no raw SQL in app code
// Registered as Scoped in Program.cs — new instance per HTTP request
public class ESignSignerRepository : IESignSignerRepository
{
	private readonly DapperContext _context;

	public ESignSignerRepository(DapperContext context)
	{
		_context = context;
	}

	// InsertSigner — calls usp_insert_esign_signer() stored procedure
	// Called twice after InsertTransaction — once for each signer
	// signer.TransactionId links this signer row back to the parent transaction
	public async Task InsertSigner(ESignSigner signer)
	{
		SafeLogger.App($"[DB] InsertSigner START | SignerRefId: {signer.SignerRefId}");

		var sql = @"CALL usp_insert_esign_signer(
            @TransactionId,
            @SignerRefId,
            @SignerId,
            @SignerName,
            @SignerEmail,
            @SignerMobile,
            @SignerStatus,
            @InvitationLink,
            @PageNumber,
            @PositionX,
            @PositionY,
            @CreatedAt
        );";

		using var db = _context.CreateConnection();

		try
		{
			// ExecuteAsync for INSERT with no return value (procedure uses CALL not SELECT)
			await db.ExecuteAsync(sql, new
			{
				signer.TransactionId,
				signer.SignerRefId,
				signer.SignerId,
				signer.SignerName,
				signer.SignerEmail,
				signer.SignerMobile,
				signer.SignerStatus,
				signer.InvitationLink,
				signer.PageNumber,
				signer.PositionX,
				signer.PositionY,
				signer.CreatedAt
			});

			SafeLogger.App($"[DB] InsertSigner SUCCESS | SignerRefId: {signer.SignerRefId}");
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] InsertSigner FAILED | SignerRefId: {signer.SignerRefId}");
			throw;
		}
	}

	// GetSignersByTransactionId — calls usp_get_esign_signers_by_transaction_id()
	// Returns both signer rows for a given transaction
	// Called in webhook handler to check if ALL signers have now signed
	public async Task<List<ESignSigner>> GetSignersByTransactionId(long transactionId)
	{
		SafeLogger.App($"[DB] GetSignersByTransactionId START | TransactionId: {transactionId}");

		using var db = _context.CreateConnection();

		try
		{
			var result = (await db.QueryAsync<ESignSigner>(
				"SELECT * FROM usp_get_esign_signers_by_transaction_id(@p_transaction_id)",
				new { p_transaction_id = transactionId }
			)).ToList();

			SafeLogger.App($"[DB] GetSignersByTransactionId SUCCESS | Count: {result.Count}");

			return result;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] GetSignersByTransactionId FAILED | TransactionId: {transactionId}");
			throw;
		}
	}

	// UpdateSignerStatus — calls usp_update_esign_signer_status() stored procedure
	// Marks a signer as SIGNED and records when they signed
	// Called once per signer in the webhook signers list
	public async Task UpdateSignerStatus(string signerRefId, string status, DateTime signedAt, DateTime updatedAt)
	{
		SafeLogger.App($"[DB] UpdateSignerStatus START | SignerRefId: {signerRefId} | Status: {status}");

		using var db = _context.CreateConnection();

		try
		{
			await db.ExecuteAsync(
				"CALL usp_update_esign_signer_status(@p_signer_ref_id, @p_status, @p_signed_at, @p_updated_at)",
				new
				{
					p_signer_ref_id = signerRefId,
					p_status = status,
					p_signed_at = signedAt,
					p_updated_at = updatedAt
				}
			);

			SafeLogger.App($"[DB] UpdateSignerStatus SUCCESS | SignerRefId: {signerRefId}");
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] UpdateSignerStatus FAILED | SignerRefId: {signerRefId}");
			throw;
		}
	}
}