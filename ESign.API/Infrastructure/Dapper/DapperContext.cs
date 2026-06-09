using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Infrastructure.Dapper;


public class DapperContext
{
	private readonly string _connectionString;

	public DapperContext(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString("Default")
			?? throw new Exception("Connection string 'Default' is missing from appsettings.json");

		SafeLogger.App("[DapperContext] Initialized — DB connection string loaded");
	}

	public virtual IDbConnection CreateConnection()
	{
		SafeLogger.App("[DapperContext] Opening new DB connection");
		return new NpgsqlConnection(_connectionString);    // Npgsql = PostgreSQL driver
	}
}