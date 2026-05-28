using System.Text;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Middleware;


public class CorrelationIdMiddleware
{
	private readonly RequestDelegate _next;

	public CorrelationIdMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
	
		var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
							?? Guid.NewGuid().ToString();

		context.Response.Headers["X-Correlation-ID"] = correlationId;

	context.Items["CorrelationId"] = correlationId;

		context.Request.EnableBuffering();

		var requestBody = string.Empty;
		if (context.Request.ContentLength > 0 || context.Request.Body.CanRead)
		{
			using var reader = new StreamReader(
				context.Request.Body,
				encoding: Encoding.UTF8,
				detectEncodingFromByteOrderMarks: false,
				leaveOpen: true    // Keep stream open so controller can read it again
			);
			requestBody = await reader.ReadToEndAsync();
			context.Request.Body.Position = 0;    
		}

		SafeLogger.Request(new
		{
			CorrelationId = correlationId,
			Method = context.Request.Method,
			Path = context.Request.Path.Value,
			Body = MaskingHelper.MaskSensitiveData(requestBody),
			Timestamp = DateTime.UtcNow
		});

		var originalBody = context.Response.Body;
		var responseBuffer = new MemoryStream();
		context.Response.Body = responseBuffer;   

		try
		{
			
			await _next(context);
		}
		catch (Exception ex)
		{
			
			SafeLogger.Error(ex, "[CORRELATION] Unhandled exception in pipeline");

			context.Response.StatusCode = StatusCodes.Status500InternalServerError;
			context.Response.ContentType = "application/json";

			var errorJson = System.Text.Json.JsonSerializer.Serialize(new
			{
				Message = "Internal Server Error",
				CorrelationId = correlationId
			});

			await context.Response.WriteAsync(errorJson);
		}
		finally
		{
		
			responseBuffer.Seek(0, SeekOrigin.Begin);
			var responseBody = await new StreamReader(responseBuffer).ReadToEndAsync();

			SafeLogger.Response(new
			{
				CorrelationId = correlationId,
				StatusCode = context.Response.StatusCode,
				Body = MaskingHelper.MaskSensitiveData(responseBody),
				Timestamp = DateTime.UtcNow
			});

			responseBuffer.Seek(0, SeekOrigin.Begin);
			await responseBuffer.CopyToAsync(originalBody);

			context.Response.Body = originalBody;
			await responseBuffer.DisposeAsync();
		}
	}
}