using System.Data;
using Npgsql;

namespace TimeClock.Api.Data;

public static class TestDatabaseRequest
{
    public const string ItemKey = "TimeClock.UseTestDatabase";
}

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _connectionString;
    private readonly string _testConnectionString;

    public string ActiveDatabaseName { get; }
    public string TestDatabaseName { get; }

    public DbConnectionFactory(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        var conn = configuration["DATABASE_URL"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DATABASE_URL' or 'DefaultConnection' is not configured.");

        var testDatabase = configuration["TEST_DATABASE"];
        if (string.IsNullOrWhiteSpace(testDatabase))
            testDatabase = configuration["DebugDatabases:Test"];
        if (string.IsNullOrWhiteSpace(testDatabase))
            testDatabase = "timeclock_test";

        _testConnectionString = WithDatabase(conn, testDatabase.Trim());

        var selectedDatabase = configuration["DebugDatabaseOverride"];
        if (!string.IsNullOrWhiteSpace(selectedDatabase))
            conn = WithDatabase(conn, selectedDatabase.Trim());

        _connectionString = ToNpgsqlConnectionString(conn);
        ActiveDatabaseName = DescribeDatabase(_connectionString);
        TestDatabaseName = DescribeDatabase(_testConnectionString);
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

    public IDbConnection CreateConnection()
    {
        var useTest = _httpContextAccessor.HttpContext?.Items[TestDatabaseRequest.ItemKey] is true;
        return new NpgsqlConnection(useTest ? _testConnectionString : _connectionString);
    }

    public IDbConnection CreateTestConnection() => new NpgsqlConnection(_testConnectionString);

    private static string DescribeDatabase(string connectionString)
    {
        return TryDescribe(connectionString, out _, out var database) ? database : "";
    }

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
