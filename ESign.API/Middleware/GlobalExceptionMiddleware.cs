using System.Text.Json;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Middleware;


public class GlobalExceptionMiddleware
{
	private readonly RequestDelegate _next;

	public GlobalExceptionMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (AppException ex)
		{
		
			SafeLogger.Error(ex, "[EXCEPTION] AppException caught", new
			{
				Code = ex.Code,
				Message = ex.Message,
				Status = ex.HttpStatus
			});

			var correlationId = context.Items["CorrelationId"]?.ToString();

			await WriteJsonResponse(context, ex.HttpStatus, ex.Code, ex.Message, correlationId);
		}
		catch (Exception ex)
		{
			var correlationId = context.Items["CorrelationId"]?.ToString();

			SafeLogger.Error(ex, "[EXCEPTION] Unhandled exception caught", new
			{
				CorrelationId = correlationId,
				Path = context.Request.Path.Value
			});

			await WriteJsonResponse(
				context,
				statusCode: 500,
				code: "INTERNAL_SERVER_ERROR",
				message: "An unexpected error occurred. Please try again.",
				correlationId: correlationId
			);
		}
	}


	private static async Task WriteJsonResponse(
		HttpContext context,
		int statusCode,
		string code,
		string message,
		string? correlationId)
	{
		if (context.Response.HasStarted) return;

		context.Response.StatusCode = statusCode;
		context.Response.ContentType = "application/json";

		var response = ResponseBuilder.Error(code, message, correlationId, statusCode);

		// Serialize to camelCase JSON — matches the JSON convention used by controllers
		var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		});

		await context.Response.WriteAsync(json);
	}
}