using Polly;
using Polly.Extensions.Http;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Infrastructure.Resilience;

// PollyPolicies provides retry and circuit breaker policies for HTTP calls to the e-sign provider
// These are registered in Program.cs on the named HttpClient
// They run automatically every time we call _client.SendAsync() in the provider
//
// RETRY: if provider returns 5xx or network error → wait and retry 2 more times
// CIRCUIT BREAKER: if provider fails 3 times in a row → stop calling for 30 seconds
//   This protects us from hammering a down provider and gives it time to recover
public static class PollyPolicies
{
	// GetRetryPolicy — retries the HTTP call 2 times with exponential backoff
	// Wait times: 2s after 1st fail, 4s after 2nd fail, then give up
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

	// GetCircuitBreakerPolicy — trips the circuit after 3 consecutive failures
	// Once tripped, all calls fail immediately for 30 seconds (no actual HTTP call is made)
	// After 30s it goes HALF-OPEN: one test call allowed — if it succeeds, circuit closes again
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