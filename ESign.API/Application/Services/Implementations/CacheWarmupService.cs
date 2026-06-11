using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Application.Services.Implementations
{


	public class CacheWarmupService : IHostedService
	{

		private readonly IServiceScopeFactory _scopeFactory;

		public CacheWarmupService(IServiceScopeFactory scopeFactory)
		{
			_scopeFactory = scopeFactory;
		}


		public async Task StartAsync(CancellationToken cancellationToken)
		{
			SafeLogger.App("[CACHE WARMUP] Starting provider cache warmup");


			using var scope = _scopeFactory.CreateScope();


			var masterRepo = scope.ServiceProvider.GetRequiredService<IESignMasterRepository>();
			var cacheService = scope.ServiceProvider.GetRequiredService<IESignCacheService>();

			var providers = await masterRepo.GetAllActiveProviders();

			cacheService.SetProviders(providers);

			SafeLogger.App($"[CACHE WARMUP] Done — {providers.Count} providers loaded into cache");
		}

		// StopAsync — called when app shuts down, nothing to clean up for cache warmup
		public Task StopAsync(CancellationToken cancellationToken)
		{
			SafeLogger.App("[CACHE WARMUP] Stopped");
			return Task.CompletedTask;
		}
	}
}