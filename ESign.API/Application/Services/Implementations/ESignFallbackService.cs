
using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Application.Services.Implementations;

public class ESignFallbackService : IESignFallbackService
{
	private readonly ISignDeskService _signDeskService;
	private readonly IMockProviderService _mockProviderService;
	private readonly IESignMasterRepository _masterRepository;
	private readonly IESignCacheService _cacheService;

	public ESignFallbackService(
		ISignDeskService signDeskService,
		IMockProviderService mockProviderService,
		IESignMasterRepository masterRepository,
		IESignCacheService cacheService)
	{
		_signDeskService = signDeskService;
		_mockProviderService = mockProviderService;
		_masterRepository = masterRepository;
		_cacheService = cacheService;
	}

	public async Task<(bool success, ESignCommonResponseDto? response, string providerName)> FallbackAsync(
		ESignRequest request,
		string correlationId)
	{
		// ── Step 1: Load providers from cache ────────────────────────────────
		var providers = _cacheService.GetProviders();

		if (!providers.Any())
		{
			SafeLogger.App("[FALLBACK] Cache empty — loading from DB");
			providers = await _masterRepository.GetAllActiveProviders();
			_cacheService.SetProviders(providers);
		}

	
		var ordered = providers
			.Where(p => p.IsActive)
			.OrderBy(p => p.Priority)
			.ToList();

		SafeLogger.App($"[FALLBACK] {ordered.Count} active provider(s) | CorrelationId: {correlationId}");

		for (int attempt = 0; attempt < ordered.Count; attempt++)
		{
			var provider = ordered[attempt];
			SafeLogger.App($"[FALLBACK] Attempt {attempt + 1}/{ordered.Count} → Provider: {provider.ProviderName} (priority={provider.Priority})");

			try
			{
				var (response, _) = provider.ProviderName.ToLower() switch
				{
					"mockprovider" => await _mockProviderService.CreateESignAsync(request, provider, correlationId),
					"signdesksandbox" => await _signDeskService.CreateESignAsync(request, provider, correlationId),

					_ => throw new Exception($"Unknown provider name in DB: '{provider.ProviderName}'")
				};

				response.ProviderName = provider.ProviderName;
				response.ProviderId = provider.Id;

				SafeLogger.App($"[FALLBACK] SUCCESS | Provider: {provider.ProviderName} | DocketId: {response.DocketId}");

				return (true, response, provider.ProviderName);
			}
			catch (Exception ex)
			{
				SafeLogger.Error(ex,
					$"[FALLBACK] FAILED | Provider: {provider.ProviderName} | " +
					$"Attempt: {attempt + 1} | Reason: {ex.Message}");

			}
		}

		// ── All providers exhausted ───────────────────────────────────────────
		SafeLogger.App("[FALLBACK] All providers exhausted — returning failure");
		return (false, null, "NONE");
	}
}