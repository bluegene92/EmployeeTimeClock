using TimeClock.Api.Data;
using TimeClock.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Railway (and similar) inject PORT; bind to 0.0.0.0 for external access
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database & Dapper
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

// Repositories
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ITimePunchRepository, TimePunchRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
