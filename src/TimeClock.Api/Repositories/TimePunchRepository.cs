using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class TimePunchRepository : ITimePunchRepository
{
    private const string SelectSql = """
        SELECT
            p."Id",
            p."EmployeeId",
            e."FirstName" AS "EmployeeFirstName",
            e."LastName" AS "EmployeeLastName",
            e."IsDeleted" AS "EmployeeIsDeleted",
            p."LocationId",
            l."Name" AS "LocationName",
            p."PunchType",
            p."PunchedAt",
            p."Source",
            p."DeviceId"
        FROM "TimePunches" p
        JOIN "Employees" e ON e."Id" = p."EmployeeId"
        LEFT JOIN "Locations" l ON l."Id" = p."LocationId"
        """;

    private readonly IDbConnectionFactory _db;

    public TimePunchRepository(IDbConnectionFactory db) => _db = db;

    public async Task<TimePunch?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sql = SelectSql + """ WHERE p."Id" = @Id """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<TimePunch>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TimePunch>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        Guid? employeeId = null,
        Guid? locationId = null,
        CancellationToken ct = default)
    {
        var sql = SelectSql + """
            WHERE p."PunchedAt" >= @From
              AND p."PunchedAt" < @To
              AND (@EmployeeId IS NULL OR p."EmployeeId" = @EmployeeId)
              AND (@LocationId IS NULL OR p."LocationId" = @LocationId)
            ORDER BY p."PunchedAt"
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<TimePunch>(new CommandDefinition(sql, new
        {
            From = WallTime.AsUnspecified(from),
            To = WallTime.AsUnspecified(to),
            EmployeeId = employeeId,
            LocationId = locationId
        }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<Guid> CreateAsync(TimePunch punch, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var sql = """
            INSERT INTO "TimePunches" (
                "Id", "EmployeeId", "LocationId", "PunchType", "PunchedAt", "Source", "DeviceId"
            )
            VALUES (
                @Id, @EmployeeId, @LocationId, @PunchType, @PunchedAt, @Source, @DeviceId
            )
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            punch.EmployeeId,
            punch.LocationId,
            PunchType = (int)punch.PunchType,
            PunchedAt = WallTime.AsUnspecified(punch.PunchedAt),
            punch.Source,
            punch.DeviceId
        }, cancellationToken: ct));
        return id;
    }

    public async Task<int> CreateBatchAsync(IEnumerable<TimePunch> punches, CancellationToken ct = default)
    {
        var list = punches.ToList();
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var count = 0;
        foreach (var punch in list)
        {
            var id = punch.Id == Guid.Empty ? Guid.NewGuid() : punch.Id;
            var sql = """
                INSERT INTO "TimePunches" (
                    "Id", "EmployeeId", "LocationId", "PunchType", "PunchedAt", "Source", "DeviceId"
                )
                VALUES (
                    @Id, @EmployeeId, @LocationId, @PunchType, @PunchedAt, @Source, @DeviceId
                )
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                punch.EmployeeId,
                punch.LocationId,
                PunchType = (int)punch.PunchType,
                PunchedAt = WallTime.AsUnspecified(punch.PunchedAt),
                Source = string.IsNullOrWhiteSpace(punch.Source) ? "Desktop" : punch.Source,
                punch.DeviceId
            }, transaction: tx, cancellationToken: ct));
            count++;
        }
        tx.Commit();
        return count;
    }

    public async Task<bool> UpdateAsync(TimePunch punch, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "TimePunches"
            SET
                "LocationId" = @LocationId,
                "PunchType" = @PunchType,
                "PunchedAt" = @PunchedAt,
                "Source" = @Source,
                "DeviceId" = @DeviceId
            WHERE "Id" = @Id
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            punch.Id,
            punch.LocationId,
            PunchType = (int)punch.PunchType,
            PunchedAt = WallTime.AsUnspecified(punch.PunchedAt),
            punch.Source,
            punch.DeviceId
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """DELETE FROM "TimePunches" WHERE "Id" = @Id""";
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }
}
