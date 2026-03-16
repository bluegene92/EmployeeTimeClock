using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class TimePunchRepository : ITimePunchRepository
{
    private readonly IDbConnectionFactory _db;

    public TimePunchRepository(IDbConnectionFactory db) => _db = db;

    public async Task<TimePunch?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ""Id"", ""EmployeeId"", ""PunchType"", ""PunchedAt"", ""Source"", ""DeviceId""
            FROM ""TimePunches"" WHERE ""Id"" = @Id";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TimePunch>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TimePunch>> GetByEmployeeAndDateRangeAsync(Guid employeeId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ""Id"", ""EmployeeId"", ""PunchType"", ""PunchedAt"", ""Source"", ""DeviceId""
            FROM ""TimePunches""
            WHERE ""EmployeeId"" = @EmployeeId AND ""PunchedAt"" >= @From AND ""PunchedAt"" < @To
            ORDER BY ""PunchedAt""";
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<TimePunch>(new CommandDefinition(sql, new { EmployeeId = employeeId, From = from, To = to }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<IReadOnlyList<TimePunch>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? employeeId = null, CancellationToken ct = default)
    {
        var sql = @"
            SELECT ""Id"", ""EmployeeId"", ""PunchType"", ""PunchedAt"", ""Source"", ""DeviceId""
            FROM ""TimePunches""
            WHERE ""PunchedAt"" >= @From AND ""PunchedAt"" < @To";
        if (employeeId.HasValue)
            sql += @" AND ""EmployeeId"" = @EmployeeId";
        sql += @" ORDER BY ""PunchedAt""";

        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<TimePunch>(new CommandDefinition(sql, new { From = from, To = to, EmployeeId = employeeId }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<Guid> CreateAsync(TimePunch punch, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO ""TimePunches"" (""Id"", ""EmployeeId"", ""PunchType"", ""PunchedAt"", ""Source"", ""DeviceId"")
            VALUES (@Id, @EmployeeId, @PunchType, @PunchedAt, @Source, @DeviceId)";

        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            punch.EmployeeId,
            PunchType = (int)punch.PunchType,
            punch.PunchedAt,
            punch.Source,
            punch.DeviceId
        }, cancellationToken: ct));
        return id;
    }

    public async Task<int> CreateBatchAsync(IEnumerable<TimePunch> punches, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO ""TimePunches"" (""Id"", ""EmployeeId"", ""PunchType"", ""PunchedAt"", ""Source"", ""DeviceId"")
            VALUES (@Id, @EmployeeId, @PunchType, @PunchedAt, @Source, @DeviceId)";

        var list = punches.ToList();
        using var conn = _db.CreateConnection();
        var count = 0;
        foreach (var p in list)
        {
            var id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id;
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                p.EmployeeId,
                PunchType = (int)p.PunchType,
                p.PunchedAt,
                p.Source,
                p.DeviceId
            }, cancellationToken: ct));
            count++;
        }
        return count;
    }
}
