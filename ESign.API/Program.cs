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
LoggerConfig.ConfigureLogger();

// ── STEP 2: Enable Dapper snake_case → PascalCase mapping ────────────────────
DefaultTypeMap.MatchNamesWithUnderscores = true;

try
{
	var builder = WebApplication.CreateBuilder(args);

	// ── STEP 3: Wire up Serilog ───────────────────────────────────────────────
	builder.Host.UseSerilog();

	// ── STEP 4: Register Controllers with Newtonsoft.Json ────────────────────
	builder.Services.AddControllers()
		.AddNewtonsoftJson(options => { });

	// ── STEP 5: Register Swagger / OpenAPI ───────────────────────────────────
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen(c =>
	{
		c.SwaggerDoc("v1", new() { Title = "ESign API", Version = "v1" });
		c.SupportNonNullableReferenceTypes();
	});
	builder.Services.AddSwaggerGenNewtonsoftSupport();

	// ── STEP 6: Register IMemoryCache ─────────────────────────────────────────
	builder.Services.AddMemoryCache();

	// ── STEP 7: Register DapperContext as Singleton ───────────────────────────
	builder.Services.AddSingleton<DapperContext>();

	// ── STEP 8: Register EncryptionService as Singleton ──────────────────────
	// Reads AES Key/IV from appsettings.json once at startup
	builder.Services.AddSingleton<EncryptionService>();

	// ── STEP 8b: Register PiiEncryptionService as Singleton ──────────────────
	// Wraps EncryptionService with entity-level PII encrypt/decrypt helpers.
	// Singleton is safe — no mutable state, depends only on EncryptionService (also Singleton).
	// Used by ESignSignerRepository to encrypt PII on write and decrypt on read.
	builder.Services.AddSingleton<PiiEncryptionService>();

	// ── STEP 9: Register named HttpClient with Polly policies ────────────────
	builder.Services.AddHttpClient("SignDeskClient")
		.AddPolicyHandler(PollyPolicies.GetRetryPolicy("SignDeskSandbox"))
		.AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("SignDeskSandbox"));

	// ── STEP 10: Register Repositories (Scoped) ───────────────────────────────
	builder.Services.AddScoped<IESignMasterRepository, ESignMasterRepository>();
	builder.Services.AddScoped<IESignRepository, ESignRepository>();
	builder.Services.AddScoped<IESignSignerRepository, ESignSignerRepository>();
	builder.Services.AddScoped<IHealthRepository, HealthRepository>();

	// ── STEP 11: Register Provider (Scoped) ───────────────────────────────────
	builder.Services.AddScoped<ISignDeskService, SignDeskProvider>();

	// ── STEP 12: Register Cache Service (Singleton) ───────────────────────────
	builder.Services.AddSingleton<IESignCacheService, ESignCacheService>();

	// ── STEP 13: Register Application Services (Scoped) ───────────────────────
	builder.Services.AddScoped<IESignFallbackService, ESignFallbackService>();
	builder.Services.AddScoped<IESignService, ESignService>();
	builder.Services.AddScoped<IWebhookService, WebhookService>();
	builder.Services.AddScoped<IHealthService, HealthService>();

	// ── STEP 14: Register PdfStorageService ───────────────────────────────────
	builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

	// ── STEP 15: Register CacheWarmupService as Hosted Service ────────────────
	builder.Services.AddHostedService<CacheWarmupService>();

	// ── BUILD THE APP ──────────────────────────────────────────────────────────
	var app = builder.Build();

	if (app.Environment.IsDevelopment())
	{
		app.UseSwagger();
		app.UseSwaggerUI();
	}

	app.UseMiddleware<CorrelationIdMiddleware>();
	app.UseMiddleware<GlobalExceptionMiddleware>();
	app.MapControllers();

	Log.Information("[STARTUP] ESign.API starting up");
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "[STARTUP] ESign.API failed to start");
}
finally
{
	Log.CloseAndFlush();
}