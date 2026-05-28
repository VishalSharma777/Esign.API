using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Infrastructure.Dapper;

// DapperContext is registered as a Singleton in Program.cs
// It holds the connection string and creates new DB connections on demand
// Why Singleton? The connection string never changes — no need to create this more than once
// Note: We do NOT keep a single open connection. We create a NEW connection per repository call
//       This is safer — each use is scoped and disposed after the query
public class DapperContext
{
	private readonly string _connectionString;

	public DapperContext(IConfiguration configuration)
	{
		// Load connection string from appsettings.json → "ConnectionStrings:Default"
		_connectionString = configuration.GetConnectionString("Default")
			?? throw new Exception("Connection string 'Default' is missing from appsettings.json");

		SafeLogger.App("[DapperContext] Initialized — DB connection string loaded");
	}

	// CreateConnection — opens and returns a new NpgsqlConnection (PostgreSQL)
	// Always called inside a using() block in repositories so it is auto-disposed
	public virtual IDbConnection CreateConnection()
	{
		SafeLogger.App("[DapperContext] Opening new DB connection");
		return new NpgsqlConnection(_connectionString);    // Npgsql = PostgreSQL driver
	}
}