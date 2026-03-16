using System.Data;

namespace TimeClock.Api.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
