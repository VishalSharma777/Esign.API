using System.Text;
using Newtonsoft.Json;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Infrastructure.Providers;

// BaseProvider — shared HTTP POST logic used by all provider implementations
// Extended by SignDeskProvider — never instantiated directly
public abstract class BaseProvider
{
    protected readonly HttpClient _client;

    protected BaseProvider(IHttpClientFactory factory, string clientName)
    {
        _client = factory.CreateClient(clientName);
    }

    // PostAsync — makes HTTP POST to provider, returns raw response JSON
    // Logs the FULL request URL + response body so you can see exactly what the provider said
    protected async Task<string> PostAsync(
        string baseUrl,
        string endpoint,
        Dictionary<string, string> headers,
        object payload,
        string correlationId)
    {
        // Build full URL
        var url = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        SafeLogger.App($"[HTTP POST] URL: {url} | CorrelationId: {correlationId}");

        // Log all outgoing headers (except API key value — mask it)
        var headerLog = headers
            .Select(h => h.Key.ToLower().Contains("api-key") || h.Key.ToLower().Contains("auth")
                ? $"{h.Key}: ***MASKED***"
                : $"{h.Key}: {h.Value}")
            .ToList();
        SafeLogger.App($"[HTTP POST] Headers: {string.Join(" | ", headerLog)} | CorrelationId: {correlationId}");

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        // Add all provider headers from DB
        foreach (var header in headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // Serialize payload — log it so you can verify the exact body sent to SignDesk
        var json = JsonConvert.SerializeObject(payload);
        SafeLogger.App($"[HTTP POST] Request body (first 500 chars): {json[..Math.Min(500, json.Length)]} | CorrelationId: {correlationId}");

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        // Make the HTTP call — Polly retry + circuit breaker fire automatically here
        var response = await _client.SendAsync(request);

        // Read the full response body BEFORE checking status code
        // This is critical — if SignDesk returns 400/401/500, we want to log what they said
        // Previously EnsureSuccessStatusCode() threw BEFORE we read the body, hiding the real error
        var responseBody = await response.Content.ReadAsStringAsync();

        SafeLogger.App($"[HTTP POST] Provider responded | Status: {(int)response.StatusCode} {response.StatusCode} | CorrelationId: {correlationId}");
        SafeLogger.App($"[HTTP POST] Response body: {responseBody} | CorrelationId: {correlationId}");

        // Now check if the response was successful
        // If SignDesk returned 4xx/5xx, throw with the response body included in the message
        // This means the real SignDesk error message will appear in Error.log AND the terminal
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Provider returned {(int)response.StatusCode} {response.StatusCode}. " +
                $"Response body: {responseBody}. " +
                $"URL: {url}");
        }

        return responseBody;
    }
}