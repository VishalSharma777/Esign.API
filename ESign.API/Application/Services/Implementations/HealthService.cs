using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Application.Services.Implementations;


public class HealthService : IHealthService
{
	private readonly IHealthRepository _healthRepo;

	public HealthService(IHealthRepository healthRepo)
	{
		_healthRepo = healthRepo;
	}

	
	public Task<object> GetHealthAsync()
	{
		SafeLogger.App("[HEALTH] App health check OK");

		return Task.FromResult<object>(new
		{
			status = "Healthy",
			service = "ESign.API",
			timestamp = DateTime.UtcNow
		});
	}

	
	public async Task<object> GetHealthReadyAsync()
	{
		SafeLogger.App("[HEALTH] DB health check START");

		var isDbHealthy = await _healthRepo.IsDatabaseHealthyAsync();

		if (isDbHealthy)
		{
			SafeLogger.App("[HEALTH] DB health check OK");
			return new { status = "Healthy", database = "Connected", timestamp = DateTime.UtcNow };
		}

		SafeLogger.App("[HEALTH] DB health check FAILED");
		return new { status = "Unhealthy", database = "Disconnected", timestamp = DateTime.UtcNow };
	}
}