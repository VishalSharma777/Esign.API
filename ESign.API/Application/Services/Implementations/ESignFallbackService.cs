using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Application.Services.Implementations
{


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
}