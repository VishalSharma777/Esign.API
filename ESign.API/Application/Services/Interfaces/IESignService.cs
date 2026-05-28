using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Infrastructure.Entities;

namespace ESign.API.Application.Services.Interfaces;


public interface IESignCacheService
{
	List<ESignProviderConfig> GetProviders();                    
	void SetProviders(List<ESignProviderConfig> providers);     
	void InvalidateProviders();                                  // Clear cache (forces DB reload)
}


public interface IESignFallbackService
{
	Task<(bool success, ESignCommonResponseDto? response, string providerName)> FallbackAsync(
		ESignRequest request,
		string correlationId);
}


public interface IESignService
{
	Task<(bool isSuccess, ESignCommonResponseDto? result, long transactionId)> CreateESignAsync(
		ESignRequest request,
		string correlationId);
}


public interface IWebhookService
{
	Task ProcessWebhookAsync(WebhookRequest webhookRequest, string correlationId);
}

public interface IHealthService
{
	Task<object> GetHealthAsync();
	Task<object> GetHealthReadyAsync();
}