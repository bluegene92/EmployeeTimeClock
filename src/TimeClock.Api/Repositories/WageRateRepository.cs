using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class WageRateRepository : IWageRateRepository
{
    private const string SelectColumns = """
        "Id", "EmployeeId", "HourlyRate", "SalaryAmount", "RateType", "EffectiveFrom", "EffectiveTo"
        """;

    private readonly IDbConnectionFactory _db;

    public WageRateRepository(IDbConnectionFactory db) => _db = db;

    public async Task<WageRate?> GetCurrentByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM "WageRates"
            WHERE "EmployeeId" = @EmployeeId
              AND "EffectiveTo" IS NULL
            ORDER BY "EffectiveFrom" DESC
            LIMIT 1
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<WageRate>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyDictionary<Guid, WageRate>> GetCurrentByEmployeeIdsAsync(
        IEnumerable<Guid> employeeIds, CancellationToken ct = default)
    {
        var ids = employeeIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, WageRate>();

        var sql = $"""
            SELECT DISTINCT ON ("EmployeeId") {SelectColumns}
            FROM "WageRates"
            WHERE "EmployeeId" = ANY(@Ids)
              AND "EffectiveTo" IS NULL
            ORDER BY "EmployeeId", "EffectiveFrom" DESC
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<WageRate>(
            new CommandDefinition(sql, new { Ids = ids }, cancellationToken: ct));
        return list.ToDictionary(w => w.EmployeeId);
    }

    public async Task<IReadOnlyList<WageRate>> GetHistoryAsync(Guid employeeId, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT {SelectColumns}
            FROM "WageRates"
            WHERE "EmployeeId" = @EmployeeId
            ORDER BY "EffectiveFrom" DESC
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<WageRate>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<WageRate> SetCurrentAsync(
        Guid employeeId,
        RateType rateType,
        decimal? hourlyRate,
        decimal? salaryAmount,
        CancellationToken ct = default)
    {
        var current = await GetCurrentByEmployeeIdAsync(employeeId, ct);
        if (current is not null && SameRate(current, rateType, hourlyRate, salaryAmount))
            return current;

        var now = DateTime.UtcNow;
        var created = new WageRate
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            HourlyRate = rateType == RateType.Hourly ? hourlyRate : null,
            SalaryAmount = rateType == RateType.Salary ? salaryAmount : null,
            RateType = rateType,
            EffectiveFrom = now
        };

        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        if (current is not null)
        {
            var closeSql = """
                UPDATE "WageRates"
                SET "EffectiveTo" = @EffectiveTo
                WHERE "Id" = @Id AND "EffectiveTo" IS NULL
                """;
            await conn.ExecuteAsync(new CommandDefinition(closeSql, new
            {
                current.Id,
                EffectiveTo = now
            }, transaction: tx, cancellationToken: ct));
        }

        var insertSql = """
            INSERT INTO "WageRates" (
                "Id", "EmployeeId", "HourlyRate", "SalaryAmount", "RateType", "EffectiveFrom", "EffectiveTo"
            )
            VALUES (
                @Id, @EmployeeId, @HourlyRate, @SalaryAmount, @RateType, @EffectiveFrom, NULL
            )
            """;
        await conn.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            created.Id,
            created.EmployeeId,
            created.HourlyRate,
            created.SalaryAmount,
            RateType = (int)created.RateType,
            created.EffectiveFrom
        }, transaction: tx, cancellationToken: ct));
        tx.Commit();
        return created;
    }

    private static bool SameRate(WageRate current, RateType rateType, decimal? hourlyRate, decimal? salaryAmount)
    {
        if (current.RateType != rateType)
            return false;
        return rateType == RateType.Hourly
            ? current.HourlyRate == hourlyRate
            : current.SalaryAmount == salaryAmount;
    }
}
