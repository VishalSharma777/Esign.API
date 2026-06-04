using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Providers.Interfaces;

namespace ESign.API.Infrastructure.Providers.Implementations;


public class MockProvider : BaseProvider, IMockProviderService
{
	
	public MockProvider(IHttpClientFactory factory)
		: base(factory, "MockProviderClient")
	{
	}

	public async Task<(ESignCommonResponseDto response, string rawJson)> CreateESignAsync(
		ESignRequest request,
		ESignProviderConfig providerConfig,
		string correlationId)
	{
		SafeLogger.App(
			$"[MOCK PROVIDER] Making HTTP call to dead URL to trigger Polly retry + circuit breaker | " +
			$"URL: {providerConfig.ProviderBaseUrl}{providerConfig.ProviderEndpoint} | " +
			$"CorrelationId: {correlationId}");
		var rawJson = await PostAsync(
			baseUrl: providerConfig.ProviderBaseUrl,      
			endpoint: providerConfig.ProviderEndpoint,    
			headers: new Dictionary<string, string>
			{
				["Content-Type"] = "application/json"
			},
			payload: new { mock = true, correlationId },  // minimal payload — server is dead anyway
			correlationId: correlationId
		);

		// This line is never reached — PostAsync always throws for localhost:5001
		// It exists only to satisfy the compiler (method must return a value)
		throw new ProviderUnavailableException(
			$"[MOCK PROVIDER] PostAsync returned unexpectedly — localhost:5001 should always fail");
	}
}

// ProviderUnavailableException — typed exception for clarity in logs
// FallbackService catches all Exception types, so this is for readability only
public class ProviderUnavailableException : Exception
{
	public ProviderUnavailableException(string message) : base(message) { }
}