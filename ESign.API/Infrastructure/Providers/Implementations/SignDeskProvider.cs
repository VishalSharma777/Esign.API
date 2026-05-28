using Newtonsoft.Json;
using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.DTOs.Response;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Infrastructure.Providers.Implementations;

// SignDeskProvider implements ISignDeskService
// Extends BaseProvider to get shared PostAsync() HTTP method with Polly attached
// Responsibilities:
//   1. Build SignDesk's exact JSON payload from our ESignRequest
//   2. Hardcode the fixed fields (remarks, content_type, signature_type, etc.)
//   3. Use the caller's own IDs (reference_doc_id, signer_ref_id) — don't generate them
//   4. Call the provider via PostAsync()
//   5. Map raw JSON response to ESignCommonResponseDto
public class SignDeskProvider : BaseProvider, ISignDeskService
{
	private readonly EncryptionService _encryption;

	public SignDeskProvider(IHttpClientFactory factory, EncryptionService encryption)
		: base(factory, "SignDeskClient")   // "SignDeskClient" must match Program.cs AddHttpClient()
	{
		_encryption = encryption;
	}

	public async Task<(ESignCommonResponseDto response, string rawJson)> CreateESignAsync(
		ESignRequest request,
		ESignProviderConfig providerConfig,
		string correlationId)
	{
		SafeLogger.App($"[SignDesk] START CreateESignAsync | CorrelationId: {correlationId}");

		// ── Step 1: Decrypt the API key stored encrypted in DB ────────────────
		// EncryptionService decrypts the AES-256 ciphertext stored in esign_providers.encrypted_api_key
		// We decrypt here, just before the HTTP call — never log or store the decrypted key
		var decryptedApiKey = _encryption.Decrypt(providerConfig.EncryptedApiKey ?? string.Empty);

		// ── Step 2: Parse headers stored as JSON string in DB ─────────────────
		// DB stores: {"x-parse-rest-api-key": "xxx", "x-parse-application-id": "yyy"}
		// We deserialize to Dictionary and override the API key with the decrypted value
		var headers = ParseHeaders(providerConfig.RequestHeadersJson, decryptedApiKey);

		// ── Step 3: Use the caller's own IDs — do NOT generate them ───────────
		// reference_doc_id comes from the caller's ESignRequest — they control this ID
		// SignDesk uses it to link each signer to the specific document to sign
		// Same ID must appear in documents[].reference_doc_id AND signers[].document_to_be_signed
		var referenceDocId = request.ReferenceDocId
			?? throw new AppException("INVALID_REQUEST", "reference_doc_id is required", 400);

		var signer1 = request.Signers.ElementAtOrDefault(0)
			?? throw new AppException("INVALID_REQUEST", "Signer 1 is missing", 400);

		var signer2 = request.Signers.ElementAtOrDefault(1)
			?? throw new AppException("INVALID_REQUEST", "Signer 2 is missing", 400);

		// signer_ref_id also comes from the caller — SignDesk echoes this back in the response
		// AND in the webhook — we use it to look up the correct signer row in esign_signers table
		// So the caller must send the same signer_ref_id they stored or plan to look up by
		var signer1RefId = signer1.SignerRefId
			?? throw new AppException("INVALID_REQUEST", "signer 1: signer_ref_id is required", 400);

		var signer2RefId = signer2.SignerRefId
			?? throw new AppException("INVALID_REQUEST", "signer 2: signer_ref_id is required", 400);

		// ── Step 4: Build the SignDesk-specific JSON payload ───────────────────
		// Only caller-controlled fields come from the request
		// Fixed fields are hardcoded here — the caller never needs to send these
		var SignDeskPayload = new SignDeskRequest
		{
			ReferenceId = request.ReferenceId,
			DocketTitle = request.DocketTitle,
			Remarks = "NA",     // always "NA" — SignDesk requirement
			EnableEmailNotification = true,     // always true — SignDesk sends email to signers

			Documents = new List<SignDeskDocument>
			{
				new SignDeskDocument
				{
					ReferenceDocId      = referenceDocId,      // caller's doc ID
                    ContentType         = "pdf",               // always "pdf"
                    ReturnUrl           = request.ReturnUrl,   // caller's redirect URL after signing
                    Content             = request.PdfBase64,   // Base64 PDF content
                    CallbackFileContent = "false",             // "false" = provider gives URL, not raw bytes in webhook
                    SignatureSequence   = "parallel"           // both signers can sign at same time
                }
			},

			SignersInfo = new List<SignDeskSigner>
			{
                // ── Signer 1 ──────────────────────────────────────────────────
                new SignDeskSigner
				{
					SignerRefId                   = signer1RefId,       // caller's signer ref ID
                    SignerName                    = signer1.SignerName,
					SignerEmail                   = signer1.SignerEmail,
					SignerMobile                  = signer1.SignerMobile,
					DocumentToBeSigned            = referenceDocId,     // must match documents[].reference_doc_id
                    Sequence                      = "1",               // "1" = parallel signing group
                    PageNumber                    = signer1.PageNumber, // "all" or specific page
                    EsignType                     = null,              // null = default
                    SignatureType                 = "aadhaar",          // always aadhaar OTP
                    TriggerEsignRequest           = true,              // immediately send SMS invite
                    AccessType                    = null,              // null = default
                    TriggerEsignRequestInvitation = "mobile",          // send via SMS

                    // Signature position bounding box on the PDF page
                    SignerPosition = signer1.SignaturePosition == null ? null : new SignDeskSignerPosition
					{
						Appearance = new List<SignDeskAppearance>
						{
							new SignDeskAppearance
							{
								X1 = signer1.SignaturePosition.X1,
								X2 = signer1.SignaturePosition.X2,
								Y1 = signer1.SignaturePosition.Y1,
								Y2 = signer1.SignaturePosition.Y2
							}
						}
					}
				},

                // ── Signer 2 ──────────────────────────────────────────────────
                new SignDeskSigner
				{
					SignerRefId                   = signer2RefId,
					SignerName                    = signer2.SignerName,
					SignerEmail                   = signer2.SignerEmail,
					SignerMobile                  = signer2.SignerMobile,
					DocumentToBeSigned            = referenceDocId,
					Sequence                      = "1",
					PageNumber                    = signer2.PageNumber,
					EsignType                     = null,
					SignatureType                 = "aadhaar",
					TriggerEsignRequest           = true,
					AccessType                    = null,
					TriggerEsignRequestInvitation = "mobile",

					SignerPosition = signer2.SignaturePosition == null ? null : new SignDeskSignerPosition
					{
						Appearance = new List<SignDeskAppearance>
						{
							new SignDeskAppearance
							{
								X1 = signer2.SignaturePosition.X1,
								X2 = signer2.SignaturePosition.X2,
								Y1 = signer2.SignaturePosition.Y1,
								Y2 = signer2.SignaturePosition.Y2
							}
						}
					}
				}
			}
		};

		// ── Step 5: Call the provider API ─────────────────────────────────────
		// PostAsync() (from BaseProvider) handles HTTP POST + Polly retry + circuit breaker
		var rawJson = await PostAsync(
			baseUrl: providerConfig.ProviderBaseUrl,
			endpoint: providerConfig.ProviderEndpoint,
			headers: headers,
			payload: SignDeskPayload,
			correlationId: correlationId
		);

		// ── Step 6: Deserialize raw JSON response ─────────────────────────────
		var providerResponse = JsonConvert.DeserializeObject<SignDeskProviderResponse>(rawJson)
			?? throw new Exception("[SignDesk] Failed to deserialize provider response");

		// ── Step 7: Verify provider returned success ───────────────────────────
		if (!string.Equals(providerResponse.Status, "success", StringComparison.OrdinalIgnoreCase))
			throw new Exception($"[SignDesk] Provider returned non-success status: {providerResponse.Status}");

		// ── Step 8: Map to normalized ESignCommonResponseDto ──────────────────
		// ESignService works with this DTO — it never touches raw provider JSON
		var dto = new ESignCommonResponseDto
		{
			ProviderName = providerConfig.ProviderName,
			ProviderId = providerConfig.Id,
			DocketId = providerResponse.DocketId,     // provider's docket ID — save to DB, used in webhook
			DocumentId = providerResponse.DocumentId,   // provider's document ID — save to DB
			IsSuccess = true,

			// Map signer info: signer_ref_id + signer_id + invitation_link
			// signer_ref_id echoes back the caller's own signer_ref_id — used to match DB rows
			SignerLinks = providerResponse.SignerInfo.Select(s => new ESignSignerLinkDto
			{
				SignerRefId = s.SignerRefId,     // caller's ref ID echoed back
				SignerId = s.SignerId,         // provider's internal signer ID
				InvitationLink = s.InvitationLink   // SMS signing URL
			}).ToList()
		};

		SafeLogger.App($"[SignDesk] END CreateESignAsync | DocketId: {dto.DocketId} | CorrelationId: {correlationId}");

		return (dto, rawJson);
	}

	// ParseHeaders — deserializes the JSON headers string from DB to a Dictionary
	// DB value example: {"x-parse-rest-api-key": "abc", "x-parse-application-id": "xyz"}
	// Then overrides x-parse-rest-api-key with the decrypted real API key
	private Dictionary<string, string> ParseHeaders(string? headersJson, string decryptedApiKey)
	{
		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		if (!string.IsNullOrEmpty(headersJson))
		{
			var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(headersJson);
			if (parsed != null)
				foreach (var kv in parsed)
					headers[kv.Key] = kv.Value;
		}

		// Override the placeholder API key in DB with the real decrypted value
		if (!string.IsNullOrEmpty(decryptedApiKey))
			headers["x-parse-rest-api-key"] = decryptedApiKey;

		return headers;
	}
}
