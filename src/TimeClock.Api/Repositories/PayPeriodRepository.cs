using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class PayPeriodRepository : IPayPeriodRepository
{
    private readonly IDbConnectionFactory _db;

    public PayPeriodRepository(IDbConnectionFactory db) => _db = db;

    public async Task<PayPeriodSettings> GetAsync(CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        var existing = await conn.QuerySingleOrDefaultAsync<PayPeriodSettings>(
            new CommandDefinition("""
                SELECT "AnchorDate", "UpdatedAt"
                FROM "PayPeriodSettings"
                WHERE "Id" = 1
                """, cancellationToken: ct));
        if (existing is not null)
            return existing;

        return await conn.QuerySingleAsync<PayPeriodSettings>(new CommandDefinition("""
            INSERT INTO "PayPeriodSettings" ("Id", "AnchorDate")
            VALUES (1, DATE '2020-01-01')
            RETURNING "AnchorDate", "UpdatedAt"
            """, cancellationToken: ct));
    }

    public async Task<PayPeriodSettings> SetAnchorAsync(DateOnly anchorDate, CancellationToken ct = default)
    {
        var sql = """
            INSERT INTO "PayPeriodSettings" ("Id", "AnchorDate", "UpdatedAt")
            VALUES (1, @AnchorDate, NOW())
            ON CONFLICT ("Id") DO UPDATE
            SET "AnchorDate" = EXCLUDED."AnchorDate", "UpdatedAt" = NOW()
            RETURNING "AnchorDate", "UpdatedAt"
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<PayPeriodSettings>(
            new CommandDefinition(sql, new { AnchorDate = anchorDate }, cancellationToken: ct));
    }
}
