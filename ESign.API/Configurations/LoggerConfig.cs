using Serilog;
using Serilog.Events;
using ESign.API.Utilities;

namespace ESign.API.Configurations;

// LoggerConfig sets up Serilog before the app starts (called in Program.cs before builder)
// Each log type (APPLICATION, REQUEST, RESPONSE, ERROR) goes to its own file
// This makes debugging much easier — you open one file per concern
public static class LoggerConfig
{
	public static void ConfigureLogger()
	{
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()                                          // Capture DEBUG and above
			.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)    // Suppress noisy MS framework logs
			.Enrich.FromLogContext()                                       // Allow per-log context properties (LogType)
			.WriteTo.Console()                                             // Also print to console in dev

			// ── APPLICATION log file ─────────────────────────────────────
			// Captures all SafeLogger.App() calls — service flow, DB calls, provider calls
			.WriteTo.Logger(lc => lc
				.Filter.ByIncludingOnly(e =>
					e.Properties.TryGetValue("LogType", out var val) &&
					val.ToString().Trim('"') == "APPLICATION")
				.WriteTo.File(
					path: LogPathHelper.GetPath("Application"),
					rollingInterval: RollingInterval.Infinite,
					shared: true,
					buffered: false
				)
			)

			// ── REQUEST log file ──────────────────────────────────────────
			// Captures all incoming HTTP requests (logged in CorrelationIdMiddleware)
			.WriteTo.Logger(lc => lc
				.Filter.ByIncludingOnly(e =>
					e.Properties.TryGetValue("LogType", out var val) &&
					val.ToString().Trim('"') == "REQUEST")
				.WriteTo.File(
					path: LogPathHelper.GetPath("Request"),
					rollingInterval: RollingInterval.Infinite,
					shared: true,
					buffered: false
				)
			)

			// ── RESPONSE log file ─────────────────────────────────────────
			// Captures all outgoing HTTP responses (logged in CorrelationIdMiddleware)
			.WriteTo.Logger(lc => lc
				.Filter.ByIncludingOnly(e =>
					e.Properties.TryGetValue("LogType", out var val) &&
					val.ToString().Trim('"') == "RESPONSE")
				.WriteTo.File(
					path: LogPathHelper.GetPath("Response"),
					rollingInterval: RollingInterval.Infinite,
					shared: true,
					buffered: false
				)
			)

			// ── ERROR log file ────────────────────────────────────────────
			// Captures all SafeLogger.Error() calls — exceptions with stack traces
			.WriteTo.Logger(lc => lc
				.Filter.ByIncludingOnly(e =>
					e.Properties.TryGetValue("LogType", out var val) &&
					val.ToString().Trim('"') == "ERROR")
				.WriteTo.File(
					path: LogPathHelper.GetPath("Error"),
					rollingInterval: RollingInterval.Infinite,
					shared: true,
					buffered: false
				)
			)

			.CreateLogger();
	}
}