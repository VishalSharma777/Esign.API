using Newtonsoft.Json;
using System.ComponentModel;

namespace ESign.API.Application.DTOs.Request;


/// Required fields:
///   docket_title, pdf_base64, return_url
///   signers[].signer_name, signer_mobile, signer_email

public class ESignRequest
{

	[JsonProperty("reference_id")]
	[DefaultValue(null)]
	public string? ReferenceId { get; set; }

	
	[JsonProperty("reference_doc_id")]
	[DefaultValue(null)]
	public string? ReferenceDocId { get; set; }

	[JsonProperty("docket_title")]
	[DefaultValue("Loan Agreement")]
	public string? DocketTitle { get; set; }

	[JsonProperty("pdf_base64")]
	[DefaultValue("JVBERi0xLjQKMSAwIG9iago...")]
	public string? PdfBase64 { get; set; }

	[JsonProperty("return_url")]
	[DefaultValue("https://yourapp.com/sign-complete")]
	public string? ReturnUrl { get; set; }

	[JsonProperty("signers")]
	public List<SignerRequest> Signers { get; set; } = new();
}

public class SignerRequest
{
	
	[JsonProperty("signer_ref_id")]
	[DefaultValue(null)]
	public string? SignerRefId { get; set; }

	[JsonProperty("signer_name")]
	[DefaultValue("Bhupendra")]
	public string? SignerName { get; set; }

	[JsonProperty("signer_email")]
	[DefaultValue("bhupendra@lala.com")]
	public string? SignerEmail { get; set; }

	[JsonProperty("signer_mobile")]
	[DefaultValue("9004031234")]
	public string? SignerMobile { get; set; }

	[JsonProperty("page_number")]
	[DefaultValue("all")]
	public string PageNumber { get; set; } = "all";

	[JsonProperty("signature_position")]
	public SignaturePosition? SignaturePosition { get; set; }
}

public class SignaturePosition
{
	[JsonProperty("x1")][DefaultValue(20)] public int X1 { get; set; }
	[JsonProperty("y1")][DefaultValue(20)] public int Y1 { get; set; }
	[JsonProperty("x2")][DefaultValue(120)] public int X2 { get; set; }
	[JsonProperty("y2")][DefaultValue(60)] public int Y2 { get; set; }
}