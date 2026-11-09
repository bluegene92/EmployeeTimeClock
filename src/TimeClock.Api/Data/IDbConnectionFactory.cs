using System.Data;

namespace TimeClock.Api.Data;

public interface IDbConnectionFactory
{
    string ActiveDatabaseName { get; }
    string TestDatabaseName { get; }
    IDbConnection CreateConnection();
    IDbConnection CreateTestConnection();
}
