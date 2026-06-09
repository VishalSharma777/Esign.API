using Dapper;
using Newtonsoft.Json.Serialization;
using Serilog;
using ESign.API.Application.Services.Implementations;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Dapper;
using ESign.API.Infrastructure.Providers.Implementations;
using ESign.API.Infrastructure.Providers.Interfaces;
using ESign.API.Infrastructure.Repositories.Implementations;
using ESign.API.Infrastructure.Repositories.Interfaces;
using ESign.API.Infrastructure.Resilience;
using ESign.API.Middleware;
using ESign.API.Utilities;
using ESign.API.Utilities.Configurations;

// ── STEP 1: Configure Serilog before anything else 
LoggerConfig.ConfigureLogger();

// ── STEP 2: Enable Dapper snake_case → PascalCase column mapping 
DefaultTypeMap.MatchNamesWithUnderscores = true;

try
{
	var builder = WebApplication.CreateBuilder(args);

	// ── STEP 3: Wire up Serilog ───────────────────────────────────────────────
	builder.Host.UseSerilog();

	// ── STEP 4: Controllers + Newtonsoft.Json ─────────────────────────────────
	builder.Services.AddControllers()
		.AddNewtonsoftJson(options => { });

	// ── STEP 5: Swagger ───────────────────────────────────────────────────────
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen(c =>
	{
		c.SwaggerDoc("v1", new() { Title = "ESign API", Version = "v1" });
		c.SupportNonNullableReferenceTypes();
	});
	builder.Services.AddSwaggerGenNewtonsoftSupport();

	// ── STEP 6: Memory cache ──────────────────────────────────────────────────
	builder.Services.AddMemoryCache();

	// ── STEP 7: Infrastructure — Dapper + Encryption ─────────────────────────
	builder.Services.AddSingleton<DapperContext>();
	builder.Services.AddSingleton<EncryptionService>();


	// ── STEP 8: HTTP clients ──────────────────────────────────────────────────

	builder.Services.AddHttpClient("MockProviderClient")
		.AddPolicyHandler(PollyPolicies.GetRetryPolicy("MockProvider"))        // retries 2x
		.AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("MockProvider")); // breaks after 3 fails
																				  // SignDeskClient — real provider with Polly retry + circuit breaker
	builder.Services.AddHttpClient("SignDeskClient")
		.AddPolicyHandler(PollyPolicies.GetRetryPolicy("SignDeskSandbox"))
		.AddPolicyHandler(PollyPolicies.GetCircuitBreakerPolicy("SignDeskSandbox"));

	// ── STEP 9: Repositories ──────────────────────────────────────────────────
	builder.Services.AddScoped<IESignMasterRepository, ESignMasterRepository>();
	builder.Services.AddScoped<IESignRepository, ESignRepository>();
	builder.Services.AddScoped<IESignSignerRepository, ESignSignerRepository>();
	builder.Services.AddScoped<IHealthRepository, HealthRepository>();

	// ── STEP 10: Providers ────────────────────────────────────────────────────
	// ISignDeskService → real HTTP provider
	builder.Services.AddScoped<ISignDeskService, SignDeskProvider>();

	// IMockProviderService → always throws to prove fallback priority chain
	// No HTTP client needed — MockProvider throws in code, no network call
	builder.Services.AddScoped<IMockProviderService, MockProvider>();

	// ── STEP 11: Cache service ────────────────────────────────────────────────
	builder.Services.AddSingleton<IESignCacheService, ESignCacheService>();

	// ── STEP 12: Application services ────────────────────────────────────────
	// ESignFallbackService now injects both ISignDeskService + IMockProviderService
	builder.Services.AddScoped<IESignFallbackService, ESignFallbackService>();
	builder.Services.AddScoped<IESignService, ESignService>();
	builder.Services.AddScoped<IWebhookService, WebhookService>();
	builder.Services.AddScoped<IHealthService, HealthService>();
	builder.Services.AddScoped<IPdfStorageService, PdfStorageService>();

	// ── STEP 13: Hosted services ──────────────────────────────────────────────
	builder.Services.AddHostedService<CacheWarmupService>();

	// ── BUILD ─────────────────────────────────────────────────────────────────
	var app = builder.Build();

	if (app.Environment.IsDevelopment())
	{
		app.UseSwagger();
		app.UseSwaggerUI();
	}

	app.UseMiddleware<CorrelationIdMiddleware>();
	app.UseMiddleware<GlobalExceptionMiddleware>();
	app.MapControllers();

	Log.Information("[STARTUP] ESign.API starting");
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