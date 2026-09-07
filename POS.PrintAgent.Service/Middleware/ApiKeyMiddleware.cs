using POS.PrintAgent.Core.Models;

namespace POS.PrintAgent.Service.Middleware;

/// <summary>
/// حماية بسيطة بـ API Key - عشان ميحصلش abuse من أي موقع تاني
/// </summary>
public class ApiKeyMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, AgentSettings settings)
    {
        _next = next;
        _apiKey = settings.ApiKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // اسمح بالـ health check + Swagger/Scalar من غير API key (local dev docs)
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/swagger")
            || context.Request.Path.StartsWithSegments("/scalar")
            || context.Request.Path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        // لو مفيش API key مفعّل، اسمح
        if (string.IsNullOrEmpty(_apiKey))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || !string.Equals(providedKey, _apiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing API key" });
            return;
        }

        await _next(context);
    }
}
