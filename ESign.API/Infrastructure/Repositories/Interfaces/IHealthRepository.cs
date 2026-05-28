namespace ESign.API.Infrastructure.Repositories.Interfaces;

// IHealthRepository — checks if the DB connection is healthy
// Called by HealthService from the GET /api/v1/esign/health/database endpoint
public interface IHealthRepository
{
	Task<bool> IsDatabaseHealthyAsync();
}