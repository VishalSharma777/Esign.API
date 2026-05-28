using Microsoft.Extensions.Caching.Memory;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Application.Services.Implementations;


public class ESignCacheService : IESignCacheService
{
	private readonly IMemoryCache _cache;

	private const string ProvidersKey = "providers:esign:master";

	public ESignCacheService(IMemoryCache cache)
	{
		_cache = cache;
	}

	public List<ESignProviderConfig> GetProviders()
	{
		_cache.TryGetValue(ProvidersKey, out List<ESignProviderConfig>? providers);
		return providers ?? new List<ESignProviderConfig>();
	}

	public void SetProviders(List<ESignProviderConfig> providers)
	{
		var options = new MemoryCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),   // Max 24 hours no matter what
			SlidingExpiration = TimeSpan.FromHours(6)     // Reset if accessed within 6 hours
		};

		_cache.Set(ProvidersKey, providers, options);

		SafeLogger.App($"[CACHE] Providers SET | Count: {providers.Count}");
	}

	// Call this if a provider is added/updated in DB and you want to reload immediately
	public void InvalidateProviders()
	{
		_cache.Remove(ProvidersKey);
		SafeLogger.App("[CACHE] Providers INVALIDATED");
	}
}
