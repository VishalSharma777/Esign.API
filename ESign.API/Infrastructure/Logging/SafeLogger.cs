using Serilog;

namespace ESign.API.Infrastructure.Logging
{

	// SafeLogger wraps Serilog and adds a LogType property to every log entry
	// This LogType is used in LoggerConfig to route logs to separate files:
	//   APPLICATION → logs/Application.log
	//   REQUEST     → logs/Request.log
	//   RESPONSE    → logs/Response.log
	//   ERROR       → logs/Error.log
	public static class SafeLogger
	{
		// App() → general application flow logs (service calls, DB calls, provider calls)
		public static void App(string eventName, object? data = null)
		{
			Log.ForContext("LogType", "APPLICATION", destructureObjects: false)
			   .Information("{EventName} {@Data}", eventName, data);
		}

		// Request() → logs the incoming HTTP request body + metadata
		public static void Request(object data)
		{
			Log.ForContext("LogType", "REQUEST", false)
			   .Information("REQUEST {@Data}", data);
		}

		// Response() → logs the outgoing HTTP response body + status code
		public static void Response(object data)
		{
			Log.ForContext("LogType", "RESPONSE", false)
			   .Information("RESPONSE {@Data}", data);
		}

		// Error() → logs exceptions with full stack trace
		public static void Error(Exception ex, string eventName, object? data = null)
		{
			Log.ForContext("LogType", "ERROR", false)
			   .Error(ex, "{EventName} {@Data}", eventName, data);
		}
	}
}