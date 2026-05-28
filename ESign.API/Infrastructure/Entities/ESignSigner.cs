namespace ESign.API.Infrastructure.Entities;

// ESignSigner maps to one row in the esign_signers table
// One transaction has 2 signer rows (signer 1 and signer 2)
// Signer status flows: NOT_SIGNED → SIGNED
public class ESignSigner
{
	public long Id { get; set; }                    // Auto-increment primary key
	public long TransactionId { get; set; }                    // FK → esign_transactions.id
	public string? SignerRefId { get; set; }                   // Your signer reference ID e.g. "C000012744_000bb98"
	public string? SignerId { get; set; }                   // Provider's signer ID (from provider API response)
	public string SignerName { get; set; } = string.Empty;   // Signer full name
	public string? SignerEmail { get; set; }                   // Signer email (optional)
	public string SignerMobile { get; set; } = string.Empty;   // Signer mobile — required for OTP + SMS invite
	public string SignerStatus { get; set; } = string.Empty;   // NOT_SIGNED / SIGNED
	public string? InvitationLink { get; set; }                   // Signing URL sent to signer by provider via SMS
	public DateTime? SignedAt { get; set; }                   // When this signer completed signing (set on webhook)
	public int PageNumber { get; set; }                   // PDF page where signature appears
	public decimal? PositionX { get; set; }                   // Signature X-axis position on that page
	public decimal? PositionY { get; set; }                   // Signature Y-axis position on that page
	public DateTime CreatedAt { get; set; }                   // Record creation time
	public DateTime? UpdatedAt { get; set; }                   // Record last updated time
}