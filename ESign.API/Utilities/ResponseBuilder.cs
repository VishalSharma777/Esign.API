namespace ESign.API.Utilities;


public static class ResponseBuilder
{
	
	public static ApiResponse<object> ESignCreated(
		string transactionId,
		string docketId,
		string documentId,
		List<SignerLinkDto> signerLinks,
		string? correlationId)
	{
		return new ApiResponse<object>
		{
			Status = "SUCCESS",
			Code = "ESIGN_CREATED",
			Message = "E-sign request created successfully. Signers have been notified.",
			CorrelationId = correlationId,
			Data = new
			{
				transactionId,          
				docketId,               
				documentId,             
				signers = signerLinks   
			}
		};
	}

	public static ApiResponse<object> WebhookProcessed(string? correlationId)
	{
		return new ApiResponse<object>
		{
			Status = "SUCCESS",
			Code = "WEBHOOK_PROCESSED",
			Message = "Webhook received and processed successfully.",
			CorrelationId = correlationId,
			Data = null
		};
	}



	// 400 — Missing or malformed request body/fields
	public static ApiResponse<object> InvalidRequest(string message, string? correlationId = null)
		=> Error("INVALID_REQUEST", message, correlationId);

	// 401 — Webhook signature validation failed
	public static ApiResponse<object> WebhookUnauthorized(string? correlationId = null)
		=> Error("WEBHOOK_UNAUTHORIZED", "Webhook signature is invalid or missing.", correlationId, 401);

	// 409 — Duplicate webhook received (idempotency check — already processed)
	public static ApiResponse<object> WebhookAlreadyProcessed(string? correlationId = null)
		=> Error("WEBHOOK_DUPLICATE", "This webhook event has already been processed.", correlationId, 409);

	// 404 — Transaction not found in DB (docket_id in webhook does not match any record)
	public static ApiResponse<object> TransactionNotFound(string? correlationId = null)
		=> Error("TRANSACTION_NOT_FOUND", "No e-sign transaction found for the given reference.", correlationId, 404);

	// 502 — All providers failed after retries
	public static ApiResponse<object> AllProvidersFailed(string? correlationId = null)
		=> Error("PROVIDER_FAILURE", "E-sign provider is unavailable. Please try again later.", correlationId, 502);

	// 500 — Unhandled exception (GlobalExceptionMiddleware calls this)
	public static ApiResponse<object> ServerError(string? correlationId = null)
		=> Error("INTERNAL_SERVER_ERROR", "Something went wrong. Please try again.", correlationId, 500);

	// ── BASE ERROR BUILDER ────────────────────────────────────────────────────
	// All error methods above call this — single place to build error envelope
	public static ApiResponse<object> Error(
		string code,
		string message,
		string? correlationId = null,
		int httpStatus = 400)
	{
		return new ApiResponse<object>
		{
			Status = "FAILED",
			Code = code,
			Message = message,
			CorrelationId = correlationId,
			Data = null
		};
	}
}


public class ApiResponse<T>
{
	public string? Status { get; set; }   
	public string? Code { get; set; }   
	public string? Message { get; set; }   
	public string? CorrelationId { get; set; }  
	public T? Data { get; set; } 
}


public class SignerLinkDto
{
	public string? SignerRefId { get; set; } 
	public string? SignerName { get; set; } 
	public string? InvitationLink { get; set; }  
}