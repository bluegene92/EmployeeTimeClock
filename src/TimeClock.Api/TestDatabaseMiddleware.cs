using TimeClock.Api.Data;

namespace TimeClock.Api;

/// <summary>
/// Routes /api/test/* to the same controllers and points those requests at the test database.
/// </summary>
public class TestDatabaseMiddleware
{
    private const string Prefix = "/api/test";
    private readonly RequestDelegate _next;

    public TestDatabaseMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.Equals(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Items[TestDatabaseRequest.ItemKey] = true;
            context.Request.Path = "/api";
        }
        else if (path.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            context.Items[TestDatabaseRequest.ItemKey] = true;
            context.Request.Path = "/api" + path[Prefix.Length..];
        }

        await _next(context);
    }
}
