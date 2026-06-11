namespace ESign.API.Infrastructure.Entities
{

	public class ESignTransaction
	{
		public long Id { get; set; }
		public long ProviderId { get; set; }
		public string ReferenceId { get; set; } = string.Empty;
		public string DocketTitle { get; set; } = string.Empty;
		public string? DocketId { get; set; }
		public string? DocumentId { get; set; }
		public string TransactionStatus { get; set; } = string.Empty;

	
		public string? SignedPdfPath { get; set; }

		public DateTime? ExpiresAt { get; set; }
		public DateTime? CompletedAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
	}
}