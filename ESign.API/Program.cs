//////using Dapper;
//////using ESign.API.Application.Services.Implementations;
//////using ESign.API.Application.Services.Interfaces;
//////using ESign.API.Configurations;
//////using ESign.API.Infrastructure.Dapper;
//////using ESign.API.Infrastructure.Providers.Implementations;
//////using ESign.API.Infrastructure.Providers.Interfaces;
//////using ESign.API.Infrastructure.Repositories.Implementations;
//////using ESign.API.Infrastructure.Repositories.Interfaces;
//////using ESign.API.Infrastructure.Resilience;
//////using ESign.API.Middleware;
//////using ESign.API.Utilities;
//////using Microsoft.OpenApi.Models;
//////using Newtonsoft.Json.Serialization;
//////using Serilog;

//////// ── STEP 1: Configure Serilog before anything else ───────────────────────────
//////// Must happen before WebApplication.CreateBuilder so Serilog captures startup logs too
//////LoggerConfig.ConfigureLogger();

//////// ── STEP 2: Enable Dapper snake_case → PascalCase mapping ────────────────────
//////// PostgreSQL columns use snake_case (provider_base_url, encrypted_api_key)
//////// Our entity classes use PascalCase (ProviderBaseUrl, EncryptedApiKey)
//////// MatchNamesWithUnderscores = true makes Dapper convert automatically — no manual mapping needed
//////DefaultTypeMap.MatchNamesWithUnderscores = true;

//////try
//////{
//////	var builder = WebApplication.CreateBuilder(args);

//////	// ── STEP 3: Wire up Serilog as the ASP.NET logger ──────────────────────────
//////	// Replaces the default Microsoft.Extensions.Logging with Serilog
//////	builder.Host.UseSerilog();

//////	// ── STEP 4: Register Controllers with Newtonsoft.Json ────────────────────
//////	// AddNewtonsoftJson() replaces System.Text.Json with Newtonsoft for ALL controller
//////	// request/response binding — this means [JsonProperty("reference_id")] on our DTOs
//////	// is now respected when reading [FromBody] — incoming JSON must use snake_case keys
//////	// Without this, [FromBody] uses System.Text.Json which ignores [JsonProperty] completely
//////	builder.Services.AddControllers()
//////		.AddNewtonsoftJson(options =>
//////		{
//////			// ContractResolver stays default (no camelCase override)
//////			// We rely entirely on [JsonProperty("snake_case")] on each DTO property
//////			// This gives us explicit control over every field name — nothing is automatic
//////		});

//////	// ── STEP 5: Register Swagger / OpenAPI ───────────────────────────────────
//////	// Generates interactive API documentation at /swagger in Development
//////	builder.Services.AddEndpointsApiExplorer();

//////	builder.Services.AddSwaggerGen(c =>
//////	{
//////		c.SwaggerDoc("v1", new OpenApiInfo
//////		{
//////			Title = "ESign API",
//////			Version = "v1"
//////		});

//////		c.UseAllOfToExtendReferenceSchemas();
//////		c.SupportNonNullableReferenceTypes();
//////		c.UseInlineDefinitionsForEnums();
//////	});

//////	builder.Services.AddSwaggerGenNewtonsoftSupport();

//////	// ── STEP 6: Register IMemoryCache ─────────────────────────────────────────
//////	// Used by ESignCacheService to store provider configs in memory
//////	// IMemoryCache is thread-safe and lives for the app's lifetime (Singleton)
//////	builder.Services.AddMemoryCache();

//////	// ── STEP 7: Register DapperContext as Singleton ───────────────────────────
//////	// DapperContext only holds the connection string — it is safe as a Singleton
//////	// Actual DB connections are created fresh inside each repository method
//////	builder.Services.AddSingleton<DapperContext>();

//////	// ── STEP 8: Register EncryptionService as Singleton ──────────────────────
//////	// Reads Key/IV from config once — safe as Singleton, no state changes
//////	builder.Services.AddSingleton<EncryptionService>();

//////	// ── STEP 9: Register named HttpClient with Polly policies ────────────────
//////	// "SignDeskClient" is the named client used by SignDeskProvider (via BaseProvider)
//////	// Polly retry + circuit breaker policies are attached here — they run automatically
//////	// on every _client.SendAsync() call inside SignDeskProvider
//////	builder.Services.AddHttpClient("SignDeskClient")
//////		.AddPolicyHandler(PollyPolicies.GetRetryPolicy("SignDeskSandbox"))
//////		.AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("SignDeskSandbox"));

//////	// ── STEP 10: Register Repositories (Scoped) ───────────────────────────────
//////	// Scoped = new instance per HTTP request
//////	// Each request gets its own repository instance with its own DB connection lifecycle
//////	builder.Services.AddScoped<IESignMasterRepository, ESignMasterRepository>();
//////	builder.Services.AddScoped<IESignRepository, ESignRepository>();
//////	builder.Services.AddScoped<IESignSignerRepository, ESignSignerRepository>();
//////	builder.Services.AddScoped<IHealthRepository, HealthRepository>();

//////	// ── STEP 11: Register Provider (Scoped) ───────────────────────────────────
//////	// SignDeskProvider makes HTTP calls to SignDesk's sandbox API
//////	// Scoped so a new instance is used per request (no shared state between requests)
//////	builder.Services.AddScoped<ISignDeskService, SignDeskProvider>();

//////	// ── STEP 12: Register Cache Service (Singleton) ───────────────────────────
//////	// ESignCacheService wraps IMemoryCache — must be Singleton to share cache across requests
//////	builder.Services.AddSingleton<IESignCacheService, ESignCacheService>();

//////	// ── STEP 13: Register Application Services (Scoped) ───────────────────────
//////	// Scoped because they depend on Scoped repositories
//////	builder.Services.AddScoped<IESignFallbackService, ESignFallbackService>();
//////	builder.Services.AddScoped<IESignService, ESignService>();
//////	builder.Services.AddScoped<IWebhookService, WebhookService>();
//////	builder.Services.AddScoped<IHealthService, HealthService>();

//////	// ── STEP 14: Register CacheWarmupService as Hosted Service ────────────────
//////	// IHostedService.StartAsync() is called automatically when the app starts
//////	// It pre-loads active providers from DB into the in-memory cache
//////	builder.Services.AddHostedService<CacheWarmupService>();

//////	// ── BUILD THE APP ──────────────────────────────────────────────────────────
//////	var app = builder.Build();

//////	// ── STEP 15: Middleware pipeline (ORDER MATTERS) ───────────────────────────
//////	// Middleware runs in the ORDER they are added here
//////	// Every request flows through each middleware top-to-bottom before reaching the controller

//////	// 1. Swagger UI — only in Development environment
//////	//    Must be first so /swagger is reachable even if auth middleware rejects the request
//////	if (app.Environment.IsDevelopment())
//////	{
//////		app.UseSwagger();
//////		app.UseSwaggerUI();
//////	}

//////	// 2. GatewayAuthMiddleware — checks X-Consumer-Username header (injected by APISix gateway)
//////	//    Rejects requests that didn't come through the gateway (except /health and /swagger)
//////	//app.UseMiddleware<GatewayAuthMiddleware>();

//////	// 3. CorrelationIdMiddleware — generates/reads X-Correlation-ID, logs request + response
//////	//    Must run AFTER GatewayAuth so we don't log rejected requests
//////	//    Must run BEFORE GlobalException so correlation ID is set before any exception can occur
//////	app.UseMiddleware<CorrelationIdMiddleware>();

//////	// 4. GlobalExceptionMiddleware — catches all unhandled exceptions, returns structured JSON
//////	//    Must run AFTER CorrelationId so it can read the correlation ID from HttpContext.Items
//////	app.UseMiddleware<GlobalExceptionMiddleware>();

//////	// 5. Route to controllers
//////	app.MapControllers();

//////	Log.Information("[STARTUP] ESign.API starting up");

//////	app.Run();
//////}
//////catch (Exception ex)
//////{
//////	// Capture fatal startup errors (e.g. DB unreachable on startup, bad config)
//////	Log.Fatal(ex, "[STARTUP] ESign.API failed to start");
//////}
//////finally
//////{
//////	// Flush all Serilog buffers before the process exits — ensures last logs are written to file
//////	Log.CloseAndFlush();
//////}





////using Dapper;
////using Newtonsoft.Json.Serialization;
////using Serilog;
////using ESign.API.Application.Services.Implementations;
////using ESign.API.Application.Services.Interfaces;
////using ESign.API.Configurations;
////using ESign.API.Infrastructure.Dapper;
////using ESign.API.Infrastructure.Providers.Implementations;
////using ESign.API.Infrastructure.Providers.Interfaces;
////using ESign.API.Infrastructure.Repositories.Implementations;
////using ESign.API.Infrastructure.Repositories.Interfaces;
////using ESign.API.Infrastructure.Resilience;
////using ESign.API.Middleware;
////using ESign.API.Utilities;

////// ── STEP 1: Configure Serilog before anything else ───────────────────────────
////// Must happen before WebApplication.CreateBuilder so Serilog captures startup logs too
////LoggerConfig.ConfigureLogger();

////// ── STEP 2: Enable Dapper snake_case → PascalCase mapping ────────────────────
////// PostgreSQL columns use snake_case (provider_base_url, encrypted_api_key)
////// Our entity classes use PascalCase (ProviderBaseUrl, EncryptedApiKey)
////// MatchNamesWithUnderscores = true makes Dapper convert automatically — no manual mapping needed
////DefaultTypeMap.MatchNamesWithUnderscores = true;

////try
////{
////	var builder = WebApplication.CreateBuilder(args);

////	// ── STEP 3: Wire up Serilog as the ASP.NET logger ──────────────────────────
////	// Replaces the default Microsoft.Extensions.Logging with Serilog
////	builder.Host.UseSerilog();

////	// ── STEP 4: Register Controllers with Newtonsoft.Json ────────────────────
////	// AddNewtonsoftJson() replaces System.Text.Json with Newtonsoft for ALL controller
////	// request/response binding — this means [JsonProperty("reference_id")] on our DTOs
////	// is now respected when reading [FromBody] — incoming JSON must use snake_case keys
////	// Without this, [FromBody] uses System.Text.Json which ignores [JsonProperty] completely
////	builder.Services.AddControllers()
////		.AddNewtonsoftJson(options =>
////		{
////			// ContractResolver stays default (no camelCase override)
////			// We rely entirely on [JsonProperty("snake_case")] on each DTO property
////			// This gives us explicit control over every field name — nothing is automatic
////		});

////	// ── STEP 5: Register Swagger / OpenAPI ───────────────────────────────────
////	// Generates interactive API documentation at /swagger in Development
////	builder.Services.AddEndpointsApiExplorer();
////	builder.Services.AddSwaggerGen(c =>
////	{
////		c.SwaggerDoc("v1", new() { Title = "ESign API", Version = "v1" });

////		// SupportNonNullableReferenceTypes — makes nullable string fields show as optional in schema
////		c.SupportNonNullableReferenceTypes();

////		// NOTE: UseAllOfToExtendReferenceSchemas() was REMOVED — it caused Swagger to set
////		// Content-Type to "application/json-patch+json" instead of "application/json"
////		// which made every Swagger request fail with 415 Unsupported Media Type
////	});

////	// AddSwaggerGenNewtonsoftSupport() — CRITICAL: tells Swagger to read [JsonProperty] attributes
////	// Without this: Swagger shows camelCase C# property names (referenceId, signerRefId)
////	// With this:    Swagger reads [JsonProperty("reference_id")] and shows snake_case
////	// This is why your Swagger was showing camelCase even though the DTO had [JsonProperty]
////	builder.Services.AddSwaggerGenNewtonsoftSupport();

////	// ── STEP 6: Register IMemoryCache ─────────────────────────────────────────
////	// Used by ESignCacheService to store provider configs in memory
////	// IMemoryCache is thread-safe and lives for the app's lifetime (Singleton)
////	builder.Services.AddMemoryCache();

////	// ── STEP 7: Register DapperContext as Singleton ───────────────────────────
////	// DapperContext only holds the connection string — it is safe as a Singleton
////	// Actual DB connections are created fresh inside each repository method
////	builder.Services.AddSingleton<DapperContext>();

////	// ── STEP 8: Register EncryptionService as Singleton ──────────────────────
////	// Reads Key/IV from config once — safe as Singleton, no state changes
////	builder.Services.AddSingleton<EncryptionService>();

////	// ── STEP 9: Register named HttpClient with Polly policies ────────────────
////	// "SignDeskClient" is the named client used by SignDeskProvider (via BaseProvider)
////	// Polly retry + circuit breaker policies are attached here — they run automatically
////	// on every _client.SendAsync() call inside SignDeskProvider
////	builder.Services.AddHttpClient("SignDeskClient")
////		.AddPolicyHandler(PollyPolicies.GetRetryPolicy("SignDeskSandbox"))
////		.AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("SignDeskSandbox"));

////	// ── STEP 10: Register Repositories (Scoped) ───────────────────────────────
////	// Scoped = new instance per HTTP request
////	// Each request gets its own repository instance with its own DB connection lifecycle
////	builder.Services.AddScoped<IESignMasterRepository, ESignMasterRepository>();
////	builder.Services.AddScoped<IESignRepository, ESignRepository>();
////	builder.Services.AddScoped<IESignSignerRepository, ESignSignerRepository>();
////	builder.Services.AddScoped<IHealthRepository, HealthRepository>();

////	// ── STEP 11: Register Provider (Scoped) ───────────────────────────────────
////	// SignDeskProvider makes HTTP calls to SignDesk's sandbox API
////	// Scoped so a new instance is used per request (no shared state between requests)
////	builder.Services.AddScoped<ISignDeskService, SignDeskProvider>();

////	// ── STEP 12: Register Cache Service (Singleton) ───────────────────────────
////	// ESignCacheService wraps IMemoryCache — must be Singleton to share cache across requests
////	builder.Services.AddSingleton<IESignCacheService, ESignCacheService>();

////	// ── STEP 13: Register Application Services (Scoped) ───────────────────────
////	// Scoped because they depend on Scoped repositories
////	builder.Services.AddScoped<IESignFallbackService, ESignFallbackService>();
////	builder.Services.AddScoped<IESignService, ESignService>();
////	builder.Services.AddScoped<IWebhookService, WebhookService>();
////	builder.Services.AddScoped<IHealthService, HealthService>();

////	// ── STEP 14: Register CacheWarmupService as Hosted Service ────────────────
////	// IHostedService.StartAsync() is called automatically when the app starts
////	// It pre-loads active providers from DB into the in-memory cache
////	builder.Services.AddHostedService<CacheWarmupService>();

////	// ── BUILD THE APP ──────────────────────────────────────────────────────────
////	var app = builder.Build();

////	// ── STEP 15: Middleware pipeline (ORDER MATTERS) ───────────────────────────
////	// Middleware runs in the ORDER they are added here
////	// Every request flows through each middleware top-to-bottom before reaching the controller

////	// 1. Swagger UI — only in Development environment
////	//    Must be first so /swagger is reachable even if auth middleware rejects the request
////	if (app.Environment.IsDevelopment())
////	{
////		app.UseSwagger();
////		app.UseSwaggerUI();
////	}

////	// 2. GatewayAuthMiddleware — checks X-Consumer-Username header (injected by APISix gateway)
////	//    Rejects requests that didn't come through the gateway (except /health and /swagger)
////	//app.UseMiddleware<GatewayAuthMiddleware>();

////	// 3. CorrelationIdMiddleware — generates/reads X-Correlation-ID, logs request + response
////	//    Must run AFTER GatewayAuth so we don't log rejected requests
////	//    Must run BEFORE GlobalException so correlation ID is set before any exception can occur
////	app.UseMiddleware<CorrelationIdMiddleware>();

////	// 4. GlobalExceptionMiddleware — catches all unhandled exceptions, returns structured JSON
////	//    Must run AFTER CorrelationId so it can read the correlation ID from HttpContext.Items
////	app.UseMiddleware<GlobalExceptionMiddleware>();

////	// 5. Route to controllers
////	app.MapControllers();

////	Log.Information("[STARTUP] ESign.API starting up");

////	app.Run();
////}
////catch (Exception ex)
////{
////	// Capture fatal startup errors (e.g. DB unreachable on startup, bad config)
////	Log.Fatal(ex, "[STARTUP] ESign.API failed to start");
////}
////finally
////{
////	// Flush all Serilog buffers before the process exits — ensures last logs are written to file
////	Log.CloseAndFlush();
////}





//using Dapper;
//using Newtonsoft.Json.Serialization;
//using Serilog;
//using ESign.API.Application.Services.Implementations;
//using ESign.API.Application.Services.Interfaces;
//using ESign.API.Configurations;
//using ESign.API.Infrastructure.Dapper;
//using ESign.API.Infrastructure.Providers.Implementations;
//using ESign.API.Infrastructure.Providers.Interfaces;
//using ESign.API.Infrastructure.Repositories.Implementations;
//using ESign.API.Infrastructure.Repositories.Interfaces;
//using ESign.API.Infrastructure.Resilience;
//using ESign.API.Middleware;
//using ESign.API.Utilities;

//// ── STEP 1: Configure Serilog before anything else ───────────────────────────
//// Must happen before WebApplication.CreateBuilder so Serilog captures startup logs too
//LoggerConfig.ConfigureLogger();

//// ── STEP 2: Enable Dapper snake_case → PascalCase mapping ────────────────────
//// PostgreSQL columns use snake_case (provider_base_url, encrypted_api_key)
//// Our entity classes use PascalCase (ProviderBaseUrl, EncryptedApiKey)
//// MatchNamesWithUnderscores = true makes Dapper convert automatically — no manual mapping needed
//DefaultTypeMap.MatchNamesWithUnderscores = true;

//try
//{
//    var builder = WebApplication.CreateBuilder(args);

//    // ── STEP 3: Wire up Serilog as the ASP.NET logger ──────────────────────────
//    // Replaces the default Microsoft.Extensions.Logging with Serilog
//    builder.Host.UseSerilog();

//    // ── STEP 4: Register Controllers with Newtonsoft.Json ────────────────────
//    // AddNewtonsoftJson() replaces System.Text.Json with Newtonsoft for ALL controller
//    // request/response binding — this means [JsonProperty("reference_id")] on our DTOs
//    // is now respected when reading [FromBody] — incoming JSON must use snake_case keys
//    // Without this, [FromBody] uses System.Text.Json which ignores [JsonProperty] completely
//    builder.Services.AddControllers()
//        .AddNewtonsoftJson(options =>
//        {
//            // ContractResolver stays default (no camelCase override)
//            // We rely entirely on [JsonProperty("snake_case")] on each DTO property
//            // This gives us explicit control over every field name — nothing is automatic
//        });

//    // ── STEP 5: Register Swagger / OpenAPI ───────────────────────────────────
//    // Generates interactive API documentation at /swagger in Development
//    builder.Services.AddEndpointsApiExplorer();
//    builder.Services.AddSwaggerGen(c =>
//    {
//        c.SwaggerDoc("v1", new() { Title = "ESign API", Version = "v1" });

//        // SupportNonNullableReferenceTypes — makes nullable string fields show as optional in schema
//        c.SupportNonNullableReferenceTypes();

//        // NOTE: UseAllOfToExtendReferenceSchemas() was REMOVED — it caused Swagger to set
//        // Content-Type to "application/json-patch+json" instead of "application/json"
//        // which made every Swagger request fail with 415 Unsupported Media Type
//    });

//    // AddSwaggerGenNewtonsoftSupport() — CRITICAL: tells Swagger to read [JsonProperty] attributes
//    // Without this: Swagger shows camelCase C# property names (referenceId, signerRefId)
//    // With this:    Swagger reads [JsonProperty("reference_id")] and shows snake_case
//    // This is why your Swagger was showing camelCase even though the DTO had [JsonProperty]
//    builder.Services.AddSwaggerGenNewtonsoftSupport();

//    // ── STEP 6: Register IMemoryCache ─────────────────────────────────────────
//    // Used by ESignCacheService to store provider configs in memory
//    // IMemoryCache is thread-safe and lives for the app's lifetime (Singleton)
//    builder.Services.AddMemoryCache();

//    // ── STEP 7: Register DapperContext as Singleton ───────────────────────────
//    // DapperContext only holds the connection string — it is safe as a Singleton
//    // Actual DB connections are created fresh inside each repository method
//    builder.Services.AddSingleton<DapperContext>();

//    // ── STEP 8: Register EncryptionService as Singleton ──────────────────────
//    // Reads Key/IV from config once — safe as Singleton, no state changes
//    builder.Services.AddSingleton<EncryptionService>();

//    // ── STEP 9: Register named HttpClient with Polly policies ────────────────
//    // "SignDeskClient" is the named client used by SignDeskProvider (via BaseProvider)
//    // Polly retry + circuit breaker policies are attached here — they run automatically
//    // on every _client.SendAsync() call inside SignDeskProvider
//    builder.Services.AddHttpClient("SignDeskClient")
//        .AddPolicyHandler(PollyPolicies.GetRetryPolicy("SignDeskSandbox"))
//        .AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("SignDeskSandbox"));

//    // ── STEP 10: Register Repositories (Scoped) ───────────────────────────────
//    // Scoped = new instance per HTTP request
//    // Each request gets its own repository instance with its own DB connection lifecycle
//    builder.Services.AddScoped<IESignMasterRepository, ESignMasterRepository>();
//    builder.Services.AddScoped<IESignRepository, ESignRepository>();
//    builder.Services.AddScoped<IESignSignerRepository, ESignSignerRepository>();
//    builder.Services.AddScoped<IHealthRepository, HealthRepository>();

//    // ── STEP 11: Register Provider (Scoped) ───────────────────────────────────
//    // SignDeskProvider makes HTTP calls to SignDesk's sandbox API
//    // Scoped so a new instance is used per request (no shared state between requests)
//    builder.Services.AddScoped<ISignDeskService, SignDeskProvider>();

//    // ── STEP 12: Register Cache Service (Singleton) ───────────────────────────
//    // ESignCacheService wraps IMemoryCache — must be Singleton to share cache across requests
//    builder.Services.AddSingleton<IESignCacheService, ESignCacheService>();

//    // ── STEP 13: Register Application Services (Scoped) ───────────────────────
//    // Scoped because they depend on Scoped repositories
//    builder.Services.AddScoped<IESignFallbackService, ESignFallbackService>();
//    builder.Services.AddScoped<IESignService, ESignService>();
//    builder.Services.AddScoped<IWebhookService, WebhookService>();
//    builder.Services.AddScoped<IHealthService, HealthService>();

//    // ── STEP 14: Register CacheWarmupService as Hosted Service ────────────────
//    // IHostedService.StartAsync() is called automatically when the app starts
//    // It pre-loads active providers from DB into the in-memory cache
//    builder.Services.AddHostedService<CacheWarmupService>();

//    // ── BUILD THE APP ──────────────────────────────────────────────────────────
//    var app = builder.Build();

//    // ── STEP 15: Middleware pipeline (ORDER MATTERS) ───────────────────────────
//    // Middleware runs in the ORDER they are added here
//    // Every request flows through each middleware top-to-bottom before reaching the controller

//    // 1. Swagger UI — only in Development environment
//    //    Must be first so /swagger is reachable even if auth middleware rejects the request
//    if (app.Environment.IsDevelopment())
//    {
//        app.UseSwagger();
//        app.UseSwaggerUI();
//    }

//    // 2. GatewayAuthMiddleware — checks X-Consumer-Username header (injected by APISix gateway)
//    //    Rejects requests that didn't come through the gateway (except /health and /swagger)
//    //app.UseMiddleware<GatewayAuthMiddleware>();

//    // 3. CorrelationIdMiddleware — generates/reads X-Correlation-ID, logs request + response
//    //    Must run AFTER GatewayAuth so we don't log rejected requests
//    //    Must run BEFORE GlobalException so correlation ID is set before any exception can occur
//    app.UseMiddleware<CorrelationIdMiddleware>();

//    // 4. GlobalExceptionMiddleware — catches all unhandled exceptions, returns structured JSON
//    //    Must run AFTER CorrelationId so it can read the correlation ID from HttpContext.Items
//    app.UseMiddleware<GlobalExceptionMiddleware>();

//    // 5. Route to controllers
//    app.MapControllers();

//    Log.Information("[STARTUP] ESign.API starting up");

//    app.Run();
//}
//catch (Exception ex)
//{
//    // Capture fatal startup errors (e.g. DB unreachable on startup, bad config)
//    Log.Fatal(ex, "[STARTUP] ESign.API failed to start");
//}
//finally
//{
//    // Flush all Serilog buffers before the process exits — ensures last logs are written to file
//    Log.CloseAndFlush();
//}










using Dapper;
using Newtonsoft.Json.Serialization;
using Serilog;
using ESign.API.Application.Services.Implementations;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Configurations;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Providers.Implementations;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Infrastructure.Repositories.Implementations;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Infrastructure.Resilience;
using ESign.API.Middleware;
using ESign.API.Utilities;

// ── STEP 1: Configure Serilog before anything else ───────────────────────────
// Must happen before WebApplication.CreateBuilder so Serilog captures startup logs too
LoggerConfig.ConfigureLogger();

// ── STEP 2: Enable Dapper snake_case → PascalCase mapping ────────────────────
// PostgreSQL columns use snake_case (provider_base_url, encrypted_api_key)
// Our entity classes use PascalCase (ProviderBaseUrl, EncryptedApiKey)
// MatchNamesWithUnderscores = true makes Dapper convert automatically — no manual mapping needed
DefaultTypeMap.MatchNamesWithUnderscores = true;

try
{
	var builder = WebApplication.CreateBuilder(args);

	// ── STEP 3: Wire up Serilog as the ASP.NET logger ──────────────────────────
	// Replaces the default Microsoft.Extensions.Logging with Serilog
	builder.Host.UseSerilog();

	// ── STEP 4: Register Controllers with Newtonsoft.Json ────────────────────
	// AddNewtonsoftJson() replaces System.Text.Json with Newtonsoft for ALL controller
	// request/response binding — this means [JsonProperty("reference_id")] on our DTOs
	// is now respected when reading [FromBody] — incoming JSON must use snake_case keys
	// Without this, [FromBody] uses System.Text.Json which ignores [JsonProperty] completely
	builder.Services.AddControllers()
		.AddNewtonsoftJson(options =>
		{
			// ContractResolver stays default (no camelCase override)
			// We rely entirely on [JsonProperty("snake_case")] on each DTO property
			// This gives us explicit control over every field name — nothing is automatic
		});

	// ── STEP 5: Register Swagger / OpenAPI ───────────────────────────────────
	// Generates interactive API documentation at /swagger in Development
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen(c =>
	{
		c.SwaggerDoc("v1", new() { Title = "ESign API", Version = "v1" });

		// SupportNonNullableReferenceTypes — makes nullable string fields show as optional in schema
		c.SupportNonNullableReferenceTypes();

		// NOTE: UseAllOfToExtendReferenceSchemas() was REMOVED — it caused Swagger to set
		// Content-Type to "application/json-patch+json" instead of "application/json"
		// which made every Swagger request fail with 415 Unsupported Media Type
	});

	// AddSwaggerGenNewtonsoftSupport() — CRITICAL: tells Swagger to read [JsonProperty] attributes
	// Without this: Swagger shows camelCase C# property names (referenceId, signerRefId)
	// With this:    Swagger reads [JsonProperty("reference_id")] and shows snake_case
	// This is why your Swagger was showing camelCase even though the DTO had [JsonProperty]
	builder.Services.AddSwaggerGenNewtonsoftSupport();

	// ── STEP 6: Register IMemoryCache ─────────────────────────────────────────
	// Used by ESignCacheService to store provider configs in memory
	// IMemoryCache is thread-safe and lives for the app's lifetime (Singleton)
	builder.Services.AddMemoryCache();

	// ── STEP 7: Register DapperContext as Singleton ───────────────────────────
	// DapperContext only holds the connection string — it is safe as a Singleton
	// Actual DB connections are created fresh inside each repository method
	builder.Services.AddSingleton<DapperContext>();

	// ── STEP 8: Register EncryptionService as Singleton ──────────────────────
	// Reads Key/IV from config once — safe as Singleton, no state changes
	builder.Services.AddSingleton<EncryptionService>();

	// ── STEP 9: Register named HttpClient with Polly policies ────────────────
	// "SignDeskClient" is the named client used by SignDeskProvider (via BaseProvider)
	// Polly retry + circuit breaker policies are attached here — they run automatically
	// on every _client.SendAsync() call inside SignDeskProvider
	builder.Services.AddHttpClient("SignDeskClient")
		.AddPolicyHandler(PollyPolicies.GetRetryPolicy("SignDeskSandbox"))
		.AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("SignDeskSandbox"));

	// ── STEP 10: Register Repositories (Scoped) ───────────────────────────────
	// Scoped = new instance per HTTP request
	// Each request gets its own repository instance with its own DB connection lifecycle
	builder.Services.AddScoped<IESignMasterRepository, ESignMasterRepository>();
	builder.Services.AddScoped<IESignRepository, ESignRepository>();
	builder.Services.AddScoped<IESignSignerRepository, ESignSignerRepository>();
	builder.Services.AddScoped<IHealthRepository, HealthRepository>();

	// ── STEP 11: Register Provider (Scoped) ───────────────────────────────────
	// SignDeskProvider makes HTTP calls to SignDesk's sandbox API
	// Scoped so a new instance is used per request (no shared state between requests)
	builder.Services.AddScoped<ISignDeskService, SignDeskProvider>();

	// ── STEP 12: Register Cache Service (Singleton) ───────────────────────────
	// ESignCacheService wraps IMemoryCache — must be Singleton to share cache across requests
	builder.Services.AddSingleton<IESignCacheService, ESignCacheService>();

	// ── STEP 13: Register Application Services (Scoped) ───────────────────────
	// Scoped because they depend on Scoped repositories
	builder.Services.AddScoped<IESignFallbackService, ESignFallbackService>();
	builder.Services.AddScoped<IESignService, ESignService>();
	builder.Services.AddScoped<IWebhookService, WebhookService>();
	builder.Services.AddScoped<IHealthService, HealthService>();

	// ── STEP 14b: Register PdfStorageService ──────────────────────────────────
	// Handles PDF validation + Base64 conversion (upload API)
	// AND saves signed PDFs from webhook to disk (esign-storage folder)
	builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

	// ── STEP 14: Register CacheWarmupService as Hosted Service ────────────────
	// IHostedService.StartAsync() is called automatically when the app starts
	// It pre-loads active providers from DB into the in-memory cache
	builder.Services.AddHostedService<CacheWarmupService>();

	// ── BUILD THE APP ──────────────────────────────────────────────────────────
	var app = builder.Build();

	// ── STEP 15: Middleware pipeline (ORDER MATTERS) ───────────────────────────
	// Middleware runs in the ORDER they are added here
	// Every request flows through each middleware top-to-bottom before reaching the controller

	// 1. Swagger UI — only in Development environment
	//    Must be first so /swagger is reachable even if auth middleware rejects the request
	if (app.Environment.IsDevelopment())
	{
		app.UseSwagger();
		app.UseSwaggerUI();
	}

	// 2. GatewayAuthMiddleware — checks X-Consumer-Username header (injected by APISix gateway)
	//    Rejects requests that didn't come through the gateway (except /health and /swagger)
	//app.UseMiddleware<GatewayAuthMiddleware>();

	// 3. CorrelationIdMiddleware — generates/reads X-Correlation-ID, logs request + response
	//    Must run AFTER GatewayAuth so we don't log rejected requests
	//    Must run BEFORE GlobalException so correlation ID is set before any exception can occur
	app.UseMiddleware<CorrelationIdMiddleware>();

	// 4. GlobalExceptionMiddleware — catches all unhandled exceptions, returns structured JSON
	//    Must run AFTER CorrelationId so it can read the correlation ID from HttpContext.Items
	app.UseMiddleware<GlobalExceptionMiddleware>();

	// 5. Route to controllers
	app.MapControllers();

	Log.Information("[STARTUP] ESign.API starting up");

	app.Run();
}
catch (Exception ex)
{
	// Capture fatal startup errors (e.g. DB unreachable on startup, bad config)
	Log.Fatal(ex, "[STARTUP] ESign.API failed to start");
}
finally
{
	// Flush all Serilog buffers before the process exits — ensures last logs are written to file
	Log.CloseAndFlush();
}