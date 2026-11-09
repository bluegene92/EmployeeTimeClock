using System.Text.Json.Serialization;
using Dapper;
using TimeClock.Api;
using TimeClock.Api.Data;
using TimeClock.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Render (and similar) inject PORT; bind to 0.0.0.0 for external access
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

string? debugDatabase = null;
string? debugHost = null;
if (builder.Environment.IsDevelopment() && OperatingSystem.IsWindows())
{
    var productionDatabase = builder.Configuration["DebugDatabases:Production"] ?? "timeclock_x2fo";
    var testDatabase = builder.Configuration["DebugDatabases:Test"] ?? "timeclock_test";
    var baseConnection = builder.Configuration["DATABASE_URL"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(baseConnection))
    {
        Console.Error.WriteLine("Connection string 'DATABASE_URL' or 'DefaultConnection' is not configured.");
        return;
    }

    var server = "unknown server";
    var serverIsLocal = false;
    if (DbConnectionFactory.TryDescribe(baseConnection, out var describedHost, out _))
    {
        server = string.IsNullOrWhiteSpace(describedHost) ? server : describedHost;
        serverIsLocal = describedHost is "localhost" or "127.0.0.1" or "::1";
    }

    string? selected;
    try
    {
        selected = DebugDatabaseDialog.Choose(productionDatabase, testDatabase, server, serverIsLocal);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not open the database picker: {ex.Message}");
        return;
    }

    if (string.IsNullOrWhiteSpace(selected))
    {
        Console.Error.WriteLine("Database selection cancelled. The API was not started.");
        return;
    }

    try
    {
        if (DbConnectionFactory.TryDescribe(DbConnectionFactory.WithDatabase(baseConnection, selected), out var host, out _)
            && !string.IsNullOrWhiteSpace(host))
        {
            debugHost = host;
        }
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Could not use database {selected}: {ex.Message}");
        return;
    }

    debugDatabase = selected;
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DebugDatabaseOverride"] = selected,
    });
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

var corsOrigins = builder.Configuration["CORS_ORIGINS"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ITimePunchRepository, TimePunchRepository>();
builder.Services.AddScoped<IWageRateRepository, WageRateRepository>();
builder.Services.AddScoped<IDayRateRepository, DayRateRepository>();
builder.Services.AddScoped<IPayPeriodRepository, PayPeriodRepository>();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(debugDatabase))
    app.Logger.LogInformation("Using Postgres database {Database} on {Host}.", debugDatabase, debugHost ?? "unknown host");

var databases = app.Services.GetRequiredService<IDbConnectionFactory>();
try
{
    await SchemaInitializer.ApplyAsync(databases, app.Environment, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to apply database schema on startup.");
    throw;
}

if (!string.Equals(databases.ActiveDatabaseName, databases.TestDatabaseName, StringComparison.OrdinalIgnoreCase))
{
    try
    {
        await SchemaInitializer.ApplyAsync(
            databases.CreateTestConnection,
            app.Environment,
            app.Logger,
            databases.TestDatabaseName);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply schema to test database {Database}.", databases.TestDatabaseName);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseMiddleware<AdminApiKeyMiddleware>();
// Rewrite /api/test before route matching. Without this call, routing runs first and /api/test is a 404.
app.UseMiddleware<TestDatabaseMiddleware>();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
