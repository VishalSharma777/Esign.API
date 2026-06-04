using ESign.API.Utilities;

namespace ESign.API.Utilities;


public class PiiEncryptionService
{
	private readonly EncryptionService _encryption;

	public PiiEncryptionService(EncryptionService encryption)
	{
		_encryption = encryption;
	}

	// ── Encrypt helpers ──────────────────────────────────────────────────────
	public string EncryptRequired(string plainText)
		=> _encryption.Encrypt(plainText);

	public string? EncryptOptional(string? plainText)
		=> string.IsNullOrEmpty(plainText) ? plainText : _encryption.Encrypt(plainText);

	// ── Decrypt helpers ──────────────────────────────────────────────────────


	public string DecryptRequired(string cipherText)
		=> _encryption.Decrypt(cipherText);

	public string? DecryptOptional(string? cipherText)
		=> string.IsNullOrEmpty(cipherText) ? cipherText : _encryption.Decrypt(cipherText);

	// ── Entity-level helpers (encrypt whole signer for DB write) ────────────

	public ESign.API.Infrastructure.Entities.ESignSigner EncryptSigner(
		ESign.API.Infrastructure.Entities.ESignSigner signer)
	{
		return new ESign.API.Infrastructure.Entities.ESignSigner
		{
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

			SignerName = EncryptRequired(signer.SignerName),
			SignerEmail = EncryptOptional(signer.SignerEmail),
			SignerMobile = EncryptRequired(signer.SignerMobile),
			InvitationLink = EncryptOptional(signer.InvitationLink),
		};
	}

// call when reading from db
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