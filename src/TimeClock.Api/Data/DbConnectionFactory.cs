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

        var selectedDatabase = configuration["DebugDatabaseOverride"];
        if (!string.IsNullOrWhiteSpace(selectedDatabase))
            conn = WithDatabase(conn, selectedDatabase.Trim());

        _connectionString = ToNpgsqlConnectionString(conn);
    }

    internal static string WithDatabase(string conn, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(ToNpgsqlConnectionString(conn))
        {
            Database = database,
        };
        return builder.ConnectionString;
    }

    internal static bool TryDescribe(string conn, out string host, out string database)
    {
        host = "";
        database = "";
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(ToNpgsqlConnectionString(conn));
            host = builder.Host ?? "";
            database = builder.Database ?? "";
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    internal static string ToNpgsqlConnectionString(string conn)
    {
        if (!conn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !conn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return conn;
        }

        var uri = new Uri(conn);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        };

        var isLocal = builder.Host is "localhost" or "127.0.0.1" or "::1";
        if (!isLocal)
            builder.SslMode = SslMode.Require;

        return builder.ConnectionString;
    }
}
