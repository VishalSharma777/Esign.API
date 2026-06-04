using Newtonsoft.Json;
using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.DTOs.Response;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Utilities;

namespace ESign.API.Infrastructure.Providers.Implementations;


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

		var decryptedApiKey = _encryption.Decrypt(providerConfig.EncryptedApiKey ?? string.Empty);
		var headers = ParseHeaders(providerConfig.RequestHeadersJson, decryptedApiKey);
		var referenceDocId = request.ReferenceDocId
			?? throw new AppException("INVALID_REQUEST", "reference_doc_id is required", 400);

		var signer1 = request.Signers.ElementAtOrDefault(0)
			?? throw new AppException("INVALID_REQUEST", "Signer 1 is missing", 400);

		var signer2 = request.Signers.ElementAtOrDefault(1)
			?? throw new AppException("INVALID_REQUEST", "Signer 2 is missing", 400);
		var signer1RefId = signer1.SignerRefId
			?? throw new AppException("INVALID_REQUEST", "signer 1: signer_ref_id is required", 400);

		var signer2RefId = signer2.SignerRefId
			?? throw new AppException("INVALID_REQUEST", "signer 2: signer_ref_id is required", 400);
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

		// ──  Call the provider API ─────────────────────────────────────
		// PostAsync() (from BaseProvider) handles HTTP POST + Polly retry + circuit breaker
		var rawJson = await PostAsync(
			baseUrl: providerConfig.ProviderBaseUrl,
			endpoint: providerConfig.ProviderEndpoint,
			headers: headers,
			payload: SignDeskPayload,
			correlationId: correlationId
		);

		
		var providerResponse = JsonConvert.DeserializeObject<SignDeskProviderResponse>(rawJson)
			?? throw new Exception("[SignDesk] Failed to deserialize provider response");

		// ──  Verify provider returned success ───────────────────────────
		if (!string.Equals(providerResponse.Status, "success", StringComparison.OrdinalIgnoreCase))
			throw new Exception($"[SignDesk] Provider returned non-success status: {providerResponse.Status}");

		// ──  Map to normalized ESignCommonResponseDto ──────────────────
		var dto = new ESignCommonResponseDto
		{
			ProviderName = providerConfig.ProviderName,
			ProviderId = providerConfig.Id,
			DocketId = providerResponse.DocketId,     
			DocumentId = providerResponse.DocumentId,  
			IsSuccess = true,

			
			SignerLinks = providerResponse.SignerInfo.Select(s => new ESignSignerLinkDto
			{
				SignerRefId = s.SignerRefId,    
				SignerId = s.SignerId,         
				InvitationLink = s.InvitationLink   
			}).ToList()
		};

		SafeLogger.App($"[SignDesk] END CreateESignAsync | DocketId: {dto.DocketId} | CorrelationId: {correlationId}");

		return (dto, rawJson);
	}


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
