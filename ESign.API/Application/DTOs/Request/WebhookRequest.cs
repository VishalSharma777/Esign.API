//using System.Text.Json.Serialization;
//using Newtonsoft.Json;

//namespace ESign.API.Application.DTOs.Request;


//public class WebhookRequest
//{

//	[JsonProperty("docket_id")]
//	public string? DocketId { get; set; }

//	[JsonProperty("status")]
//	public string? Status { get; set; }

//	[JsonProperty("signed_document_url")]
//	public string? SignedDocumentUrl { get; set; }

//	[JsonProperty("response_time_stamp")]
//	public string? ResponseTimeStamp { get; set; }

//	[JsonProperty("signers")]
//	public List<WebhookSignerInfo> Signers { get; set; } = new();


//	[Newtonsoft.Json.JsonIgnore]
//	public string? WebhookSignature { get; set; }
//}

//public class WebhookSignerInfo
//{
//	[JsonProperty("signer_ref_id")]
//	public string? SignerRefId { get; set; }

//	[JsonProperty("signer_id")]
//	public string? SignerId { get; set; }

//	[JsonProperty("status")]
//	public string? Status { get; set; }

//	[JsonProperty("signed_at")]
//	public string? SignedAt { get; set; }
//}








using Newtonsoft.Json;

namespace ESign.API.Application.DTOs.Request;


public class WebhookRequest
{
	
	[JsonProperty("docket_id")]
	public string? DocketId { get; set; }

	[JsonProperty("status")]
	public string? Status { get; set; }

	[JsonProperty("signed_document_base64")]
	public string? SignedDocumentBase64 { get; set; }

	[JsonProperty("signed_document_url")]
	public string? SignedDocumentUrl { get; set; }

	[JsonProperty("response_time_stamp")]
	public string? ResponseTimeStamp { get; set; }

	
	[JsonProperty("signers")]
	public List<WebhookSignerInfo> Signers { get; set; } = new();

	// HMAC signature from X-Webhook-Signature header — validated before processing
	[JsonIgnore]
	public string? WebhookSignature { get; set; }
}

// WebhookSignerInfo — one signer's completion details inside the webhook payload
public class WebhookSignerInfo
{
	
	[JsonProperty("signer_ref_id")]
	public string? SignerRefId { get; set; }

	[JsonProperty("signer_id")]
	public string? SignerId { get; set; }

	[JsonProperty("status")]
	public string? Status { get; set; }

	[JsonProperty("signed_at")]
	public string? SignedAt { get; set; }
}