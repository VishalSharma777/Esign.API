using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Utilities;
using Newtonsoft.Json;

namespace ESign.API.Infrastructure.Repositories.Implementations;


public class ESignSignerRepository : IESignSignerRepository
{
	private readonly DapperContext _context;
	private readonly EncryptionService _encryption;

	public ESignSignerRepository(DapperContext context, EncryptionService encryption)
	{
		_context = context;
		_encryption = encryption;
	}

	// ── Private PII helpers ───────────────────────────────────────────────────

	private string Encrypt(string plainText)
		=> _encryption.Encrypt(plainText);

	private string? EncryptOptional(string? plainText)
		=> string.IsNullOrEmpty(plainText) ? plainText : _encryption.Encrypt(plainText);

	private string Decrypt(string cipherText)
		=> _encryption.Decrypt(cipherText);

	private string? DecryptOptional(string? cipherText)
		=> string.IsNullOrEmpty(cipherText) ? cipherText : _encryption.Decrypt(cipherText);


	public async Task InsertSigner(ESignSigner signer)
	{
		SafeLogger.App($"[DB] InsertSigner START | SignerRefId: {signer.SignerRefId}");

		
		var positionJson = string.IsNullOrEmpty(signer.SignaturePosition)
			? null
			: signer.SignaturePosition;

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
            @SignaturePosition::jsonb,
            @CreatedAt
        );";

		using var db = _context.CreateConnection();

		try
		{
			await db.ExecuteAsync(sql, new
			{
				signer.TransactionId,
				signer.SignerRefId,                              // not PII
				signer.SignerId,                                 // not PII
				SignerName = Encrypt(signer.SignerName),     // PII — encrypted
				SignerEmail = EncryptOptional(signer.SignerEmail),  // PII — encrypted
				SignerMobile = Encrypt(signer.SignerMobile),   // PII — encrypted
				signer.SignerStatus,                             // not PII
				InvitationLink = EncryptOptional(signer.InvitationLink), // PII — encrypted
				signer.PageNumber,
				SignaturePosition = positionJson,                // JSONB — not PII, plain JSON
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

	// ── GetSignersByTransactionId ─────────────────────────────────────────────
	// Reads encrypted PII rows from DB, decrypts them before returning.
	// signature_position comes back as a JSON string from JSONB column — no decryption needed.
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

			// Decrypt PII fields, leave signature_position as-is (plain JSONB string)
			var decrypted = rows.Select(s => new ESignSigner
			{
				// Non-PII fields — copy as-is
				Id = s.Id,
				TransactionId = s.TransactionId,
				SignerRefId = s.SignerRefId,
				SignerId = s.SignerId,
				SignerStatus = s.SignerStatus,
				SignedAt = s.SignedAt,
				PageNumber = s.PageNumber,
				SignaturePosition = s.SignaturePosition,  // plain JSONB string — no decryption
				CreatedAt = s.CreatedAt,
				UpdatedAt = s.UpdatedAt,

				// PII fields — decrypt after reading
				SignerName = Decrypt(s.SignerName),
				SignerEmail = DecryptOptional(s.SignerEmail),
				SignerMobile = Decrypt(s.SignerMobile),
				InvitationLink = DecryptOptional(s.InvitationLink),
			}).ToList();

			SafeLogger.App($"[DB] GetSignersByTransactionId SUCCESS | Count: {decrypted.Count}");

			return decrypted;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, $"[DB] GetSignersByTransactionId FAILED | TransactionId: {transactionId}");
			throw;
		}
	}

	// ── UpdateSignerStatus ────────────────────────────────────────────────────
	// No PII and no position changes here — only status + timestamps updated
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