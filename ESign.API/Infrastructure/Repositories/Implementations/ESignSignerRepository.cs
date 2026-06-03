using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Infrastructure.Repositories.Implementations;

/// <summary>
/// ESignSignerRepository — all DB operations on esign_signers table.
///
/// PII ENCRYPTION RULE (enforced here, at the DB boundary):
///   WRITE  → EncryptSigner() before every INSERT
///   READ   → DecryptSigner() after every SELECT
///
/// This keeps plaintext PII only in application memory, never on disk.
/// The rest of the codebase (services, controllers) always works with plaintext.
/// </summary>
public class ESignSignerRepository : IESignSignerRepository
{
	private readonly DapperContext _context;
	private readonly PiiEncryptionService _pii;

	public ESignSignerRepository(DapperContext context, PiiEncryptionService pii)
	{
		_context = context;
		_pii = pii;
	}

	// ── InsertSigner ─────────────────────────────────────────────────────────
	// Encrypts PII fields before calling the stored procedure.
	// Called twice after every transaction insert (once per signer).
	public async Task InsertSigner(ESignSigner signer)
	{
		SafeLogger.App($"[DB] InsertSigner START | SignerRefId: {signer.SignerRefId}");

		// ── Encrypt PII before writing to DB ─────────────────────────────────
		var encrypted = _pii.EncryptSigner(signer);

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
			await db.ExecuteAsync(sql, new
			{
				encrypted.TransactionId,
				encrypted.SignerRefId,
				encrypted.SignerId,
				encrypted.SignerName,       // encrypted
				encrypted.SignerEmail,      // encrypted
				encrypted.SignerMobile,     // encrypted
				encrypted.SignerStatus,
				encrypted.InvitationLink,   // encrypted
				encrypted.PageNumber,
				encrypted.PositionX,
				encrypted.PositionY,
				encrypted.CreatedAt
			});

			SafeLogger.App($"[DB] InsertSigner SUCCESS | SignerRefId: {signer.SignerRefId}");
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] InsertSigner FAILED | SignerRefId: {signer.SignerRefId}");
			throw;
		}
	}

	// ── GetSignersByTransactionId ─────────────────────────────────────────────
	// Decrypts PII fields after reading from DB.
	// Returns plaintext signers to the caller (WebhookService).
	public async Task<List<ESignSigner>> GetSignersByTransactionId(long transactionId)
	{
		SafeLogger.App($"[DB] GetSignersByTransactionId START | TransactionId: {transactionId}");

		using var db = _context.CreateConnection();

		try
		{
			var rows = (await db.QueryAsync<ESignSigner>(
				"SELECT * FROM usp_get_esign_signers_by_transaction_id(@p_transaction_id)",
				new { p_transaction_id = transactionId }
			)).ToList();

			// ── Decrypt PII after reading from DB ─────────────────────────────
			var decrypted = rows.Select(_pii.DecryptSigner).ToList();

			SafeLogger.App($"[DB] GetSignersByTransactionId SUCCESS | Count: {decrypted.Count}");

			return decrypted;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] GetSignersByTransactionId FAILED | TransactionId: {transactionId}");
			throw;
		}
	}

	// ── UpdateSignerStatus ───────────────────────────────────────────────────
	// No PII written here — only status and timestamps are updated.
	// signer_ref_id is our own internal ID (not PII), used as the lookup key.
	public async Task UpdateSignerStatus(
		string signerRefId, string status, DateTime signedAt, DateTime updatedAt)
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