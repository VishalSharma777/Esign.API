using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Infrastructure.Repositories.Implementations;

// ESignMasterRepository reads provider configuration from the esign_providers table
// Uses stored procedure: usp_get_active_esign_providers()
// Registered as Scoped in Program.cs — new instance per HTTP request
public class ESignMasterRepository : IESignMasterRepository
{
	private readonly DapperContext _context;

	public ESignMasterRepository(DapperContext context)
	{
		_context = context;
	}

	// GetAllActiveProviders — calls stored procedure usp_get_active_esign_providers()
	// Returns all providers where is_active = true, ordered by priority ASC
	// Result is cached in memory by CacheWarmupService at startup
	public async Task<List<ESignProviderConfig>> GetAllActiveProviders()
	{
		SafeLogger.App("[DB] ESignMasterRepository.GetAllActiveProviders START");

		// CreateConnection() returns a new NpgsqlConnection — auto-disposed by using()
		using var db = _context.CreateConnection();

		try
		{
			// Call stored procedure — Dapper maps column names to ESignProviderConfig properties
			// Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true handles snake_case → PascalCase
			// e.g. provider_base_url → ProviderBaseUrl automatically
			var result = (await db.QueryAsync<ESignProviderConfig>(
				"SELECT * FROM usp_get_active_esign_providers()"
			)).ToList();

			SafeLogger.App($"[DB] ESignMasterRepository.GetAllActiveProviders SUCCESS | Count: {result.Count}");

			return result;
		}
		catch (Exception ex)
		{
			SafeLogger.Error(ex, "[DB] ESignMasterRepository.GetAllActiveProviders FAILED");
			throw;
		}
	}
}