using ESign.API.Utilities;

namespace ESign.API.Utilities;

/// <summary>
/// PiiEncryptionService — encrypts and decrypts PII fields before DB storage.
///
/// PII fields in this microservice:
///   esign_signers:
///     - signer_name    (full legal name)
///     - signer_email   (email address)
///     - signer_mobile  (mobile number)
///     - invitation_link (signing URL — contains signer identity token)
///
///   esign_transactions:
///     - (no PII — only IDs, statuses, timestamps)
///
/// Strategy:
///   - Encrypt on WRITE  (InsertSigner / UpdateSigner)
///   - Decrypt on READ   (GetSignersByTransactionId / UpdateSignerStatus lookup)
///   - Never store plaintext PII
///   - Null/empty → returned as-is (no crash on optional fields)
/// </summary>
public class PiiEncryptionService
{
	private readonly EncryptionService _encryption;

	public PiiEncryptionService(EncryptionService encryption)
	{
		_encryption = encryption;
	}

	// ── Encrypt helpers ──────────────────────────────────────────────────────

	/// <summary>Encrypts a required PII string (e.g. signer_name, signer_mobile).</summary>
	public string EncryptRequired(string plainText)
		=> _encryption.Encrypt(plainText);

	/// <summary>Encrypts an optional PII string. Returns null if input is null/empty.</summary>
	public string? EncryptOptional(string? plainText)
		=> string.IsNullOrEmpty(plainText) ? plainText : _encryption.Encrypt(plainText);

	// ── Decrypt helpers ──────────────────────────────────────────────────────

	/// <summary>Decrypts a required PII string. Throws if cipherText is null/empty.</summary>
	public string DecryptRequired(string cipherText)
		=> _encryption.Decrypt(cipherText);

	/// <summary>Decrypts an optional PII string. Returns null if input is null/empty.</summary>
	public string? DecryptOptional(string? cipherText)
		=> string.IsNullOrEmpty(cipherText) ? cipherText : _encryption.Decrypt(cipherText);

	// ── Entity-level helpers (encrypt whole signer for DB write) ────────────

	/// <summary>
	/// Returns a copy of the signer entity with all PII fields encrypted.
	/// Call this BEFORE InsertSigner() so we never write plaintext to DB.
	/// </summary>
	public ESign.API.Infrastructure.Entities.ESignSigner EncryptSigner(
		ESign.API.Infrastructure.Entities.ESignSigner signer)
	{
		return new ESign.API.Infrastructure.Entities.ESignSigner
		{
			// Non-PII fields — copy as-is
			Id = signer.Id,
			TransactionId = signer.TransactionId,
			SignerRefId = signer.SignerRefId,       // our own reference ID — not PII
			SignerId = signer.SignerId,           // provider's internal ID — not PII
			SignerStatus = signer.SignerStatus,
			PageNumber = signer.PageNumber,
			PositionX = signer.PositionX,
			PositionY = signer.PositionY,
			SignedAt = signer.SignedAt,
			CreatedAt = signer.CreatedAt,
			UpdatedAt = signer.UpdatedAt,

			// PII fields — encrypt before storing
			SignerName = EncryptRequired(signer.SignerName),
			SignerEmail = EncryptOptional(signer.SignerEmail),
			SignerMobile = EncryptRequired(signer.SignerMobile),
			InvitationLink = EncryptOptional(signer.InvitationLink),
		};
	}

	/// <summary>
	/// Returns a copy of the signer entity with all PII fields decrypted.
	/// Call this AFTER reading from DB so the rest of the app sees plaintext.
	/// </summary>
	public ESign.API.Infrastructure.Entities.ESignSigner DecryptSigner(
		ESign.API.Infrastructure.Entities.ESignSigner signer)
	{
		return new ESign.API.Infrastructure.Entities.ESignSigner
		{
			// Non-PII fields — copy as-is
			Id = signer.Id,
			TransactionId = signer.TransactionId,
			SignerRefId = signer.SignerRefId,
			SignerId = signer.SignerId,
			SignerStatus = signer.SignerStatus,
			PageNumber = signer.PageNumber,
			PositionX = signer.PositionX,
			PositionY = signer.PositionY,
			SignedAt = signer.SignedAt,
			CreatedAt = signer.CreatedAt,
			UpdatedAt = signer.UpdatedAt,

			// PII fields — decrypt after reading from DB
			SignerName = DecryptRequired(signer.SignerName),
			SignerEmail = DecryptOptional(signer.SignerEmail),
			SignerMobile = DecryptRequired(signer.SignerMobile),
			InvitationLink = DecryptOptional(signer.InvitationLink),
		};
	}
}