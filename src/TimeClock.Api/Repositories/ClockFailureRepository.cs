using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class ClockFailureRepository : IClockFailureRepository
{
    private readonly IDbConnectionFactory _db;

    public ClockFailureRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ClockFailure>> GetAsync(bool includeResolved, int limit, CancellationToken ct = default)
    {
        var sql = """
            SELECT
                f."Id",
                f."Reason",
                f."Message",
                f."CardUid",
                f."EmployeeId",
                e."FirstName" AS "EmployeeFirstName",
                e."LastName" AS "EmployeeLastName",
                f."PunchType",
                f."OccurredAt",
                f."LocationId",
                l."Name" AS "LocationName",
                f."Source",
                f."DeviceId",
                f."ReceivedAt",
                f."ResolvedAt"
            FROM "ClockFailures" f
            LEFT JOIN "Employees" e ON e."Id" = f."EmployeeId"
            LEFT JOIN "Locations" l ON l."Id" = f."LocationId"
            WHERE (@IncludeResolved OR f."ResolvedAt" IS NULL)
            ORDER BY f."ReceivedAt" DESC
            LIMIT @Limit
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<ClockFailure>(new CommandDefinition(sql, new
        {
            IncludeResolved = includeResolved,
            Limit = limit
        }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<bool> CreateAsync(ClockFailure failure, CancellationToken ct = default)
    {
        var sql = """
            INSERT INTO "ClockFailures" (
                "Id", "Reason", "Message", "CardUid", "EmployeeId", "PunchType",
                "OccurredAt", "LocationId", "Source", "DeviceId"
            )
            VALUES (
                @Id, @Reason, @Message, @CardUid, @EmployeeId, @PunchType,
                @OccurredAt, @LocationId, @Source, @DeviceId
            )
            ON CONFLICT ("Id") DO NOTHING
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = failure.Id == Guid.Empty ? Guid.NewGuid() : failure.Id,
            failure.Reason,
            Message = Truncate(failure.Message, 500),
            failure.CardUid,
            failure.EmployeeId,
            PunchType = failure.PunchType is PunchType type ? (int?)type : null,
            OccurredAt = failure.OccurredAt is DateTime at ? WallTime.AsUnspecified(at) : (DateTime?)null,
            failure.LocationId,
            Source = Truncate(failure.Source, 50),
            DeviceId = Truncate(failure.DeviceId, 100)
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetResolvedAsync(Guid id, bool resolved, CancellationToken ct = default)
    {
        var sql = resolved
            ? """UPDATE "ClockFailures" SET "ResolvedAt" = COALESCE("ResolvedAt", NOW()) WHERE "Id" = @Id"""
            : """UPDATE "ClockFailures" SET "ResolvedAt" = NULL WHERE "Id" = @Id""";
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<int> ResolveCardAsync(string cardUid, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "ClockFailures"
            SET "ResolvedAt" = NOW()
            WHERE "CardUid" = @CardUid AND "Reason" = @Reason AND "ResolvedAt" IS NULL
            """;
        using var conn = _db.CreateConnection();
        return await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CardUid = cardUid,
            Reason = ClockFailureReasons.UnknownCard
        }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """DELETE FROM "ClockFailures" WHERE "Id" = @Id""";
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<(Guid Id, string Name)?> FindCardOwnerAsync(string cardUid, CancellationToken ct = default)
    {
        var sql = """
            SELECT "Id", "FirstName", "LastName"
            FROM "Employees"
            WHERE "CardUid" = @CardUid AND "IsDeleted" = false
            LIMIT 1
            """;
        using var conn = _db.CreateConnection();
        var owner = await conn.QuerySingleOrDefaultAsync<CardOwnerRow>(
            new CommandDefinition(sql, new { CardUid = cardUid }, cancellationToken: ct));
        if (owner is null)
            return null;
        return (owner.Id, $"{owner.FirstName} {owner.LastName}".Trim());
    }

    private sealed class CardOwnerRow
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
