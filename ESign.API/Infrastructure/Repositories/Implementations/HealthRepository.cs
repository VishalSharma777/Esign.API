using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Infrastructure.Repositories.Implementations;

// HealthRepository checks whether the PostgreSQL database connection is alive
// Called by HealthService from GET /api/v1/esign/health/database
public class HealthRepository : IHealthRepository
{
	private readonly DapperContext _context;

	public HealthRepository(DapperContext context)
	{
		_context = context;
	}

	// IsDatabaseHealthyAsync — tries to run "SELECT 1" against the DB
	// If it succeeds → DB is reachable and healthy
	// If it throws → DB is down, return false (never throw — caller handles it gracefully)
	public async Task<bool> IsDatabaseHealthyAsync()
	{
		try
		{
			using var db = _context.CreateConnection();

			// "SELECT 1" is the lightest possible query — no tables involved
			// Just verifies the connection is open and responsive
			await db.ExecuteScalarAsync<int>("SELECT 1");

			SafeLogger.App("[HEALTH] DB ping OK");
			return true;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, "[HEALTH] DB ping FAILED");
			return false;    // Don't throw — let HealthService return 503 gracefully
		}
	}
}