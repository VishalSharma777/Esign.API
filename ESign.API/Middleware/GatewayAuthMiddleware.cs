namespace ESign.API.Middleware
{

	public class GatewayAuthMiddleware
	{
		private readonly RequestDelegate _next;

		public GatewayAuthMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var path = context.Request.Path;

			// ── Skip auth for health checks and swagger ────────────────────────────
			// Health endpoints are called by load balancers — they don't have JWT tokens
			// Swagger is for developers — only accessible in Development environment
			if (path.StartsWithSegments("/health") ||
				path.StartsWithSegments("/swagger") ||
				path.StartsWithSegments("/api/v1/esign/health"))
			{
				await _next(context);
				return;
			}

		
			var consumer = context.Request.Headers["X-Consumer-Username"].FirstOrDefault();

			if (string.IsNullOrEmpty(consumer))
			{
				context.Response.StatusCode = 401;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(
					"{\"error\":\"Unauthorized\",\"message\":\"All requests must go through the API gateway\"}");
				return;
			}

			context.Items["ConsumerUsername"] = consumer;
			context.Items["AppId"] = consumer.Split('_')[0];   
			context.Items["Environment"] = string.Join("_", consumer.Split('_').Skip(1));   

			await _next(context);
		}
	}
}