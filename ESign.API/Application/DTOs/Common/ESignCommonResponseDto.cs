namespace ESign.API.Application.DTOs.Common
{
	public class ESignCommonResponseDto
	{

		public string? ProviderName { get; set; }
		public long ProviderId { get; set; }

		public string? DocketId { get; set; }
		public string? DocumentId { get; set; }
		public List<ESignSignerLinkDto> SignerLinks { get; set; } = new();
		public bool IsSuccess { get; set; }
		public string? ErrorMessage { get; set; }
	}


	public class ESignSignerLinkDto
	{
		public string? SignerRefId { get; set; }
		public string? SignerId { get; set; }
		public string? InvitationLink { get; set; }
	}
}