using Polly;
using Polly.Extensions.Http;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Infrastructure.Resilience;


public static class PollyPolicies
{
	public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(string providerName)
	{
		return HttpPolicyExtensions
			.HandleTransientHttpError()                                   // Handles 5xx + network errors
			.OrResult(r => !r.IsSuccessStatusCode)                       // Also retry on 4xx responses
			.WaitAndRetryAsync(
				retryCount: 2,                                           // Retry maximum 2 times (3 total attempts)
				sleepDurationProvider: retry => TimeSpan.FromSeconds(Math.Pow(2, retry)),   // 2s, 4s
				onRetry: (outcome, timespan, retryCount, context) =>
				{
					SafeLogger.App(
						$"[POLLY RETRY] Provider={providerName} | Attempt={retryCount} | Wait={timespan.TotalSeconds}s"
					);
				});
	}

	
	public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(string providerName)
	{
		return HttpPolicyExtensions
			.HandleTransientHttpError()
			.OrResult(r => !r.IsSuccessStatusCode)
			.CircuitBreakerAsync(
				handledEventsAllowedBeforeBreaking: 3,                   // Open circuit after 3 failures
				durationOfBreak: TimeSpan.FromSeconds(30),               // Keep circuit open for 30 seconds
				onBreak: (outcome, breakDelay) =>
				{
					SafeLogger.App(
						$"[POLLY CIRCUIT OPEN] Provider={providerName} | Break={breakDelay.TotalSeconds}s"
					);
				},
				onReset: () =>
				{
					// Circuit closed — provider is healthy again
					SafeLogger.App($"[POLLY CIRCUIT CLOSED] Provider={providerName}");
				},
				onHalfOpen: () =>
				{
					// One test call will be allowed to check if provider recovered
					SafeLogger.App($"[POLLY HALF-OPEN] Provider={providerName}");
				});
	}
}