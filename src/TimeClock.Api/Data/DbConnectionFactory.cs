using System.Data;
using Npgsql;

namespace TimeClock.Api.Data;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        var conn = configuration["DATABASE_URL"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DATABASE_URL' or 'DefaultConnection' is not configured.");
        // Railway and some providers use postgres://; Npgsql expects postgresql://
        _connectionString = conn.Replace("postgres://", "postgresql://", StringComparison.OrdinalIgnoreCase);
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
