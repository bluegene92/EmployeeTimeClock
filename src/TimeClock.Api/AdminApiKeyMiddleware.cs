using System.Security.Cryptography;
using System.Text;

namespace TimeClock.Api;

/// <summary>
/// Requires the shared admin key on every API route except the health check.
/// Development with no key configured stays open for local debugging.
/// </summary>
public class AdminApiKeyMiddleware
{
    public const string HeaderName = "X-Admin-Api-Key";

    private readonly RequestDelegate _next;
    private readonly string? _apiKey;
    private readonly bool _requireKey;

    public AdminApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment environment, ILogger<AdminApiKeyMiddleware> logger)
    {
        _next = next;
        _apiKey = configuration["ADMIN_API_KEY"];
        var configured = !string.IsNullOrWhiteSpace(_apiKey);
        _requireKey = configured || !environment.IsDevelopment();
        if (!configured && !environment.IsDevelopment())
            logger.LogError("ADMIN_API_KEY is not set. API requests other than the health check will be rejected.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_requireKey || IsHealthCheck(context.Request) || HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(_apiKey) || !FixedTimeEquals(_apiKey, provided))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Unauthorized.\"}");
            return;
        }

        await _next(context);
    }

    private static bool IsHealthCheck(HttpRequest request)
    {
        var path = request.Path.Value ?? "";
        return path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/health/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
