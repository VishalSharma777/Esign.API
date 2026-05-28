
using Newtonsoft.Json;

namespace ESign.API.Application.DTOs.Request;


public class SignDeskRequest
{

	[JsonProperty("reference_id")]
	public string? ReferenceId { get; set; }

	[JsonProperty("docket_title")]
	public string? DocketTitle { get; set; }

	[JsonProperty("remarks")]
	public string Remarks { get; set; } = "NA";

	[JsonProperty("enable_email_notification")]
	public bool EnableEmailNotification { get; set; } = true;

	[JsonProperty("documents")]
	public List<SignDeskDocument> Documents { get; set; } = new();

	[JsonProperty("signers_info")]
	public List<SignDeskSigner> SignersInfo { get; set; } = new();
}

public class SignDeskDocument
{
	[JsonProperty("reference_doc_id")]
	public string? ReferenceDocId { get; set; }

	[JsonProperty("content_type")]
	public string ContentType { get; set; } = "pdf";
	[JsonProperty("return_url")]
	public string? ReturnUrl { get; set; }

	[JsonProperty("content")]
	public string? Content { get; set; }

	[JsonProperty("callback_file_content")]
	public string CallbackFileContent { get; set; } = "false";

	[JsonProperty("signature_sequence")]
	public string SignatureSequence { get; set; } = "parallel";
}

public class SignDeskSigner
{
	[JsonProperty("signer_position")]
	public SignDeskSignerPosition? SignerPosition { get; set; }
	[JsonProperty("document_to_be_signed")]
	public string? DocumentToBeSigned { get; set; }

	[JsonProperty("signer_ref_id")]
	public string? SignerRefId { get; set; }

	[JsonProperty("signer_email")]
	public string? SignerEmail { get; set; }

	[JsonProperty("signer_name")]
	public string? SignerName { get; set; }

	[JsonProperty("sequence")]
	public string Sequence { get; set; } = "1";

	[JsonProperty("page_number")]
	public string PageNumber { get; set; } = "all";

	[JsonProperty("esign_type")]
	public string? EsignType { get; set; } = null;

	[JsonProperty("signer_mobile")]
	public string? SignerMobile { get; set; }

	[JsonProperty("signature_type")]
	public string SignatureType { get; set; } = "aadhaar";

	[JsonProperty("trigger_esign_request")]
	public bool TriggerEsignRequest { get; set; } = true;

	[JsonProperty("access_type")]
	public string? AccessType { get; set; } = null;

	[JsonProperty("trigger_esign_request_invitation")]
	public string TriggerEsignRequestInvitation { get; set; } = "mobile";
}

public class SignDeskSignerPosition
{
	[JsonProperty("appearance")]
	public List<SignDeskAppearance> Appearance { get; set; } = new();
}

public class SignDeskAppearance
{
	[JsonProperty("x1")] public int X1 { get; set; }   // Left edge
	[JsonProperty("x2")] public int X2 { get; set; }   // Right edge
	[JsonProperty("y1")] public int Y1 { get; set; }   // Top edge
	[JsonProperty("y2")] public int Y2 { get; set; }   // Bottom edge
}