//using ESign.API.Application.DTOs.Common;
//using ESign.API.Application.DTOs.Request;
//using ESign.API.Application.Services.Interfaces;
//using ESign.API.Infrastructure.Entities;
//using ESign.API.Infrastructure.Logging;
//using ESign.API.Infrastructure.Providers.Interfaces;
//using ESign.API.Infrastructure.Repositories.Interfaces;

//namespace ESign.API.Application.Services.Implementations;

//// ESignFallbackService tries providers in priority order (priority 1 first, then 2, etc.)
//// If priority-1 provider fails (exception or bad response) → automatically tries priority-2
//// This is the SAME pattern as GstFallbackService in the GST project
//// Flow:
////   1. Read providers from cache (loaded at startup by CacheWarmupService)
////   2. Order by priority ASC (1 = first to try)
////   3. Try each provider — if it succeeds return result immediately
////   4. If all fail → return (false, null, "NONE") and let ESignService throw 502
//public class ESignFallbackService : IESignFallbackService
//{
//	private readonly ISignDeskService _SignDeskService;    // Priority 2 — real sandbox provider
//	private readonly IESignMasterRepository _masterRepository;  // Used as fallback if cache is empty
//	private readonly IESignCacheService _cacheService;      // In-memory cache of provider configs

//	public ESignFallbackService(
//		ISignDeskService SignDeskService,
//		IESignMasterRepository masterRepository,
//		IESignCacheService cacheService)
//	{
//		_SignDeskService = SignDeskService;
//		_masterRepository = masterRepository;
//		_cacheService = cacheService;
//	}

//	// FallbackAsync — tries each active provider in priority order
//	// Returns (success=true, response, providerName) on first success
//	// Returns (success=false, null, "NONE") if all providers fail
//	public async Task<(bool success, ESignCommonResponseDto? response, string providerName)> FallbackAsync(
//		ESignRequest request,
//		string correlationId)
//	{
//		// ── Step 1: Get providers from cache ──────────────────────────────────
//		var providers = _cacheService.GetProviders();

//		// If cache is empty (e.g. app just restarted and warmup hasn't run yet)
//		// fall back to a direct DB query and repopulate the cache
//		if (!providers.Any())
//		{
//			SafeLogger.App("[FALLBACK] Cache empty — loading providers from DB");
//			providers = await _masterRepository.GetAllActiveProviders();
//			_cacheService.SetProviders(providers);
//		}

//		// ── Step 2: Filter active providers and sort by priority ──────────────
//		// Only IsActive=true providers are tried
//		// Lower priority number = tried first (priority 1 before priority 2)
//		var ordered = providers
//			.Where(p => p.IsActive)
//			.OrderBy(p => p.Priority)
//			.ToList();

//		SafeLogger.App($"[FALLBACK] {ordered.Count} active providers to try | CorrelationId: {correlationId}");

//		// ── Step 3: Try each provider in order ────────────────────────────────
//		var attemptNumber = 0;

//		foreach (var provider in ordered)
//		{
//			attemptNumber++;
//			try
//			{
//				SafeLogger.App($"[FALLBACK] Trying provider: {provider.ProviderName} | Attempt: {attemptNumber}");

//				// Route to the correct provider implementation based on ProviderName in DB
//				// Add more cases here when new providers are added in future
//				var (response, _) = provider.ProviderName.ToLower() switch
//				{
//					"SignDesksandbox" => await _SignDeskService.CreateESignAsync(request, provider, correlationId),
//					"mockprovider" => await _SignDeskService.CreateESignAsync(request, provider, correlationId),  // Mock uses same interface for now
//					_ => throw new Exception($"[FALLBACK] Unknown provider name in DB: {provider.ProviderName}")
//				};

//				// Attach which provider was used — ESignService saves this to DB
//				response.ProviderName = provider.ProviderName;
//				response.ProviderId = provider.Id;

//				SafeLogger.App($"[FALLBACK] SUCCESS — Provider: {provider.ProviderName} | DocketId: {response.DocketId}");

//				return (true, response, provider.ProviderName);
//			}
//			catch (Exception ex)
//			{
//				// This provider failed — log it and try the next one
//				SafeLogger.Error(ex, $"[FALLBACK] FAILED — Provider: {provider.ProviderName} | Attempt: {attemptNumber}");
//			}
//		}

//		// ── Step 4: All providers failed ─────────────────────────────────────
//		// ESignService will throw AppException(502) when it sees success=false
//		SafeLogger.App("[FALLBACK] All providers exhausted — returning failure");
//		return (false, null, "NONE");
//	}
//}







using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Application.Services.Implementations;


public class ESignFallbackService : IESignFallbackService
{
    private readonly ISignDeskService _SignDeskService;    
    private readonly IESignMasterRepository _masterRepository;  
    private readonly IESignCacheService _cacheService;     

    public ESignFallbackService(
        ISignDeskService SignDeskService,
        IESignMasterRepository masterRepository,
        IESignCacheService cacheService)
    {
        _SignDeskService = SignDeskService;
        _masterRepository = masterRepository;
        _cacheService = cacheService;
    }

    
    public async Task<(bool success, ESignCommonResponseDto? response, string providerName)> FallbackAsync(
        ESignRequest request,
        string correlationId)
    {
        var providers = _cacheService.GetProviders();

        if (!providers.Any())
        {
            SafeLogger.App("[FALLBACK] Cache empty — loading providers from DB");
            providers = await _masterRepository.GetAllActiveProviders();
            _cacheService.SetProviders(providers);
        }

        var ordered = providers
            .Where(p => p.IsActive)
            .OrderBy(p => p.Priority)
            .ToList();

        SafeLogger.App($"[FALLBACK] {ordered.Count} active providers to try | CorrelationId: {correlationId}");

        var attemptNumber = 0;

        foreach (var provider in ordered)
        {
            attemptNumber++;
            try
            {
                SafeLogger.App($"[FALLBACK] Trying provider: {provider.ProviderName} | Attempt: {attemptNumber}");

                var (response, _) = provider.ProviderName.ToLower() switch
                {
                    "signdesksandbox" => await _SignDeskService.CreateESignAsync(request, provider, correlationId),
                    "mockprovider" => await _SignDeskService.CreateESignAsync(request, provider, correlationId),  // Mock uses same interface for now
                    _ => throw new Exception($"[FALLBACK] Unknown provider name in DB: {provider.ProviderName}")
                };

                response.ProviderName = provider.ProviderName;
                response.ProviderId = provider.Id;

                SafeLogger.App($"[FALLBACK] SUCCESS — Provider: {provider.ProviderName} | DocketId: {response.DocketId}");

                return (true, response, provider.ProviderName);
            }
            catch (Exception ex)
            {
               
                SafeLogger.Error(ex, $"[FALLBACK] FAILED — Provider: {provider.ProviderName} | Attempt: {attemptNumber} | Error: {ex.Message}");
            }
        }

        
        SafeLogger.App("[FALLBACK] All providers exhausted — returning failure");
        return (false, null, "NONE");
    }
}