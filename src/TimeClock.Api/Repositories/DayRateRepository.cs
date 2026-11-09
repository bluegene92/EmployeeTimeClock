using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class DayRateRepository : IDayRateRepository
{
    private readonly IDbConnectionFactory _db;

    public DayRateRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ResolvedDayRate>> GetResolvedAsync(
        DateOnly from, DateOnly to, Guid? employeeId, CancellationToken ct = default)
    {
        var sql = """
            SELECT
                e."Id" AS "EmployeeId",
                d::date AS "WorkDate",
                CASE
                    WHEN v."EmployeeId" IS NOT NULL THEN f."HourlyRate"
                    WHEN w."RateType" = 1 THEN NULL
                    WHEN o."HourlyRate" IS NOT NULL THEN o."HourlyRate"
                    ELSE w."HourlyRate"
                END AS "HourlyRate",
                CASE
                    WHEN v."EmployeeId" IS NOT NULL THEN 2
                    WHEN o."HourlyRate" IS NOT NULL AND COALESCE(w."RateType", 0) = 0 THEN 1
                    ELSE 0
                END AS "Source",
                (v."EmployeeId" IS NOT NULL) AS "IsVerified",
                COALESCE(w."RateType", 0) AS "RateType"
            FROM "Employees" e
            CROSS JOIN generate_series(@From::date, @To::date, interval '1 day') AS d
            LEFT JOIN "WageRates" w
                ON w."EmployeeId" = e."Id" AND w."EffectiveTo" IS NULL
            LEFT JOIN "DayRateOverrides" o
                ON o."EmployeeId" = e."Id" AND o."WorkDate" = d::date
            LEFT JOIN "PayPeriodSettings" s ON s."Id" = 1
            LEFT JOIN "PayPeriodVerifications" v
                ON v."EmployeeId" = e."Id"
               AND v."PeriodStart" = (
                    COALESCE(s."AnchorDate", DATE '2020-01-01')
                    + (floor(((d::date - COALESCE(s."AnchorDate", DATE '2020-01-01'))::numeric) / 14) * 14)::int
               )
            LEFT JOIN "FrozenDayRates" f
                ON f."EmployeeId" = e."Id" AND f."WorkDate" = d::date
            WHERE e."IsDeleted" = false
              AND (@EmployeeId IS NULL OR e."Id" = @EmployeeId)
            ORDER BY e."Id", d::date
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<ResolvedDayRate>(new CommandDefinition(sql, new
        {
            From = from,
            To = to,
            EmployeeId = employeeId
        }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task UpsertOverrideAsync(Guid employeeId, DateOnly workDate, decimal hourlyRate, CancellationToken ct = default)
    {
        var sql = """
            INSERT INTO "DayRateOverrides" ("EmployeeId", "WorkDate", "HourlyRate", "UpdatedAt")
            VALUES (@EmployeeId, @WorkDate, @HourlyRate, NOW())
            ON CONFLICT ("EmployeeId", "WorkDate")
            DO UPDATE SET "HourlyRate" = EXCLUDED."HourlyRate", "UpdatedAt" = NOW()
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EmployeeId = employeeId,
            WorkDate = workDate,
            HourlyRate = hourlyRate
        }, cancellationToken: ct));
    }

    public async Task<bool> DeleteOverrideAsync(Guid employeeId, DateOnly workDate, CancellationToken ct = default)
    {
        var sql = """
            DELETE FROM "DayRateOverrides"
            WHERE "EmployeeId" = @EmployeeId AND "WorkDate" = @WorkDate
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EmployeeId = employeeId,
            WorkDate = workDate
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> IsVerifiedAsync(Guid employeeId, DateOnly periodStart, CancellationToken ct = default)
    {
        var sql = """
            SELECT EXISTS(
                SELECT 1 FROM "PayPeriodVerifications"
                WHERE "EmployeeId" = @EmployeeId AND "PeriodStart" = @PeriodStart
            )
            """;
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new
        {
            EmployeeId = employeeId,
            PeriodStart = periodStart
        }, cancellationToken: ct));
    }

    public async Task VerifyAsync(Guid employeeId, DateOnly periodStart, IReadOnlyList<FrozenDay> days, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var insertVerification = """
            INSERT INTO "PayPeriodVerifications" ("EmployeeId", "PeriodStart", "VerifiedAt")
            VALUES (@EmployeeId, @PeriodStart, NOW())
            """;
        await conn.ExecuteAsync(new CommandDefinition(insertVerification, new
        {
            EmployeeId = employeeId,
            PeriodStart = periodStart
        }, transaction: tx, cancellationToken: ct));

        var insertDay = """
            INSERT INTO "FrozenDayRates" ("EmployeeId", "WorkDate", "PeriodStart", "HourlyRate")
            VALUES (@EmployeeId, @WorkDate, @PeriodStart, @HourlyRate)
            ON CONFLICT ("EmployeeId", "WorkDate")
            DO UPDATE SET "PeriodStart" = EXCLUDED."PeriodStart", "HourlyRate" = EXCLUDED."HourlyRate"
            """;
        foreach (var day in days)
        {
            await conn.ExecuteAsync(new CommandDefinition(insertDay, new
            {
                EmployeeId = employeeId,
                day.WorkDate,
                PeriodStart = periodStart,
                day.HourlyRate
            }, transaction: tx, cancellationToken: ct));
        }
        tx.Commit();
    }

    public async Task<bool> UnverifyAsync(Guid employeeId, DateOnly periodStart, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "FrozenDayRates" WHERE "EmployeeId" = @EmployeeId AND "PeriodStart" = @PeriodStart""",
            new { EmployeeId = employeeId, PeriodStart = periodStart }, tx, cancellationToken: ct));
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "PayPeriodVerifications" WHERE "EmployeeId" = @EmployeeId AND "PeriodStart" = @PeriodStart""",
            new { EmployeeId = employeeId, PeriodStart = periodStart }, tx, cancellationToken: ct));
        tx.Commit();
        return rows > 0;
    }

    public async Task<bool> AnyVerifiedAsync(CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition("""SELECT EXISTS(SELECT 1 FROM "PayPeriodVerifications")""", cancellationToken: ct));
    }
}
