
using Newtonsoft.Json;

namespace ESign.API.Application.DTOs.Response;


public class SignDeskProviderResponse
{
	
	[JsonProperty("status")]
	public string? Status { get; set; }

	
	[JsonProperty("docket_id")]
	public string? DocketId { get; set; }

	[JsonProperty("document_id")]
	public string? DocumentId { get; set; }

	[JsonProperty("response_time_stamp")]
	public string? ResponseTimeStamp { get; set; }

	
	[JsonProperty("signer_info")]
	public List<SignDeskSignerInfo> SignerInfo { get; set; } = new();
}

public class SignDeskSignerInfo
{
	[JsonProperty("signer_ref_id")]
	public string? SignerRefId { get; set; }

	[JsonProperty("signer_id")]
	public string? SignerId { get; set; }

	[JsonProperty("document_id")]
	public string? DocumentId { get; set; }

	[JsonProperty("reference_doc_id")]
	public string? ReferenceDocId { get; set; }

	
	[JsonProperty("invitation_link")]
	public string? InvitationLink { get; set; }
}