using Dapper;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Entities;
using ESign.API.Infrastructure.Logging;
using ESign.API.Infrastructure.Repositories.Interfaces;

namespace ESign.API.Infrastructure.Repositories.Implementations;


public class ESignMasterRepository : IESignMasterRepository
{
	private readonly DapperContext _context;

	public ESignMasterRepository(DapperContext context)
	{
		_context = context;
	}


	public async Task<List<ESignProviderConfig>> GetAllActiveProviders()
	{
		SafeLogger.App("[DB] ESignMasterRepository.GetAllActiveProviders START");

		using var db = _context.CreateConnection();

		try
		{
	
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