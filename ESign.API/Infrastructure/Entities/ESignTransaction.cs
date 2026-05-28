//namespace ESign.API.Infrastructure.Entities;

//// ESignTransaction maps to one row in the esign_transactions table
//// One transaction = one e-sign request (one document, two signers)
//// Status flows: PENDING → PARTIALLY_SIGNED → SIGNED (or FAILED / EXPIRED)
//public class ESignTransaction
//{
//	public long Id { get; set; }                    // Auto-increment primary key
//	public long ProviderId { get; set; }                    // FK → esign_providers.id
//	public string ReferenceId { get; set; } = string.Empty;   // YOUR system's unique reference ID (from request)
//	public string DocketTitle { get; set; } = string.Empty;   // Document/docket title
//	public string? DocketId { get; set; }                   // Provider's docket ID (from provider API response)
//	public string? DocumentId { get; set; }                   // Provider's document ID (from provider API response)
//	public string TransactionStatus { get; set; } = string.Empty;   // PENDING / PARTIALLY_SIGNED / SIGNED / FAILED / EXPIRED
//	public DateTime? ExpiresAt { get; set; }                   // When signing session expires (e.g. NOW + 10 min)
//	public DateTime? CompletedAt { get; set; }                   // When both signers completed (set on webhook)
//	public DateTime CreatedAt { get; set; }                   // Record creation time
//	public DateTime? UpdatedAt { get; set; }                   // Record last updated time
//}





namespace ESign.API.Infrastructure.Entities
{

	// ESignTransaction maps to one row in the esign_transactions table
	// Status flow: PENDING → PARTIALLY_SIGNED → SIGNED (or FAILED / EXPIRED)
	public class ESignTransaction
	{
		public long Id { get; set; }
		public long ProviderId { get; set; }
		public string ReferenceId { get; set; } = string.Empty;
		public string DocketTitle { get; set; } = string.Empty;
		public string? DocketId { get; set; }
		public string? DocumentId { get; set; }
		public string TransactionStatus { get; set; } = string.Empty;

		// signed_pdf_path — relative path to the signed PDF file on disk
		// Stored after webhook is received and PDF is saved
		// Example: "esign-storage/signed-documents/2026/05/week_04/2026-05-27/docket_69e88b99/signed_document.pdf"
		// NULL until signing is completed
		public string? SignedPdfPath { get; set; }

		public DateTime? ExpiresAt { get; set; }
		public DateTime? CompletedAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
	}
}