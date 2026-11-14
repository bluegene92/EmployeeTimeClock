using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class ClockDeviceRepository : IClockDeviceRepository
{
    private readonly IDbConnectionFactory _db;

    public ClockDeviceRepository(IDbConnectionFactory db) => _db = db;

    public async Task<string> GetEmployeeListVersionAsync(CancellationToken ct = default)
    {
        // Same employees and fields as GET /api/sync/employees sends to the clocks.
        var sql = """
            SELECT left(md5(
                COALESCE((SELECT "Generation"::text FROM "EmployeeListSync" WHERE "Id" = 1), '0')
                || '|' ||
                COALESCE((
                    SELECT string_agg(
                        e."Id"::text || ':' || e."FirstName" || ':' || e."LastName" || ':'
                            || COALESCE(e."CardUid", '') || ':' || COALESCE(e."LocationId"::text, ''),
                        ',' ORDER BY e."Id")
                    FROM "Employees" e
                    WHERE e."IsActive" = true AND e."IsDeleted" = false
                ), '')
            ), 16)
            """;
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<string>(new CommandDefinition(sql, cancellationToken: ct)) ?? "";
    }

    public async Task BumpEmployeeListAsync(CancellationToken ct = default)
    {
        var sql = """
            INSERT INTO "EmployeeListSync" ("Id", "Generation", "PushedAt")
            VALUES (1, 1, NOW())
            ON CONFLICT ("Id") DO UPDATE
            SET "Generation" = "EmployeeListSync"."Generation" + 1, "PushedAt" = NOW()
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task RecordCheckInAsync(ClockDevice device, long? uptimeSeconds, TimeSpan offlineAfter, CancellationToken ct = default)
    {
        // Boot time is derived from the clock's uptime, so it shifts by request latency on
        // every check-in. Only a jump of more than 30 seconds counts as a restart.
        // No FOR UPDATE on prev: the outage insert runs after the upsert, and a locking read
        // would skip the row this statement already updated.
        var sql = """
            WITH prev AS (
                SELECT "LastSeenAt", "BootedAt" FROM "ClockDevices"
                WHERE "DeviceId" = @DeviceId
            ),
            booted AS (
                SELECT CASE WHEN @UptimeSeconds::bigint IS NULL THEN NULL::timestamptz
                            ELSE NOW() - make_interval(secs => @UptimeSeconds::double precision) END AS "At"
            ),
            gap AS (
                SELECT prev."LastSeenAt",
                       (booted."At" IS NOT NULL AND prev."BootedAt" IS NOT NULL
                        AND booted."At" > prev."BootedAt" + INTERVAL '30 seconds') AS "Restarted"
                FROM prev, booted
            ),
            outage AS (
                INSERT INTO "ClockDeviceOutages" ("DeviceId", "StartedAt", "EndedAt", "Restarted")
                SELECT @DeviceId, gap."LastSeenAt", NOW(), gap."Restarted"
                FROM gap
                WHERE gap."Restarted" OR gap."LastSeenAt" < NOW() - make_interval(secs => @OfflineSeconds::double precision)
            )
            INSERT INTO "ClockDevices" (
                "DeviceId", "LocationId", "EmployeeListVersion", "PendingPunches",
                "PendingFailures", "FirmwareVersion", "WifiRssi", "BootedAt", "FirstSeenAt", "LastSeenAt"
            )
            SELECT
                @DeviceId, @LocationId, @EmployeeListVersion, @PendingPunches,
                @PendingFailures, @FirmwareVersion, @WifiRssi, booted."At", NOW(), NOW()
            FROM booted
            ON CONFLICT ("DeviceId") DO UPDATE
            SET "LocationId" = EXCLUDED."LocationId",
                "EmployeeListVersion" = EXCLUDED."EmployeeListVersion",
                "PendingPunches" = EXCLUDED."PendingPunches",
                "PendingFailures" = EXCLUDED."PendingFailures",
                "FirmwareVersion" = EXCLUDED."FirmwareVersion",
                "WifiRssi" = EXCLUDED."WifiRssi",
                "BootedAt" = CASE
                    WHEN EXCLUDED."BootedAt" IS NULL THEN "ClockDevices"."BootedAt"
                    WHEN "ClockDevices"."BootedAt" IS NULL
                         OR abs(extract(epoch FROM EXCLUDED."BootedAt" - "ClockDevices"."BootedAt")) > 30
                        THEN EXCLUDED."BootedAt"
                    ELSE "ClockDevices"."BootedAt"
                END,
                "LastSeenAt" = NOW()
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            device.DeviceId,
            device.LocationId,
            device.EmployeeListVersion,
            device.PendingPunches,
            device.PendingFailures,
            device.FirmwareVersion,
            device.WifiRssi,
            UptimeSeconds = uptimeSeconds,
            OfflineSeconds = offlineAfter.TotalSeconds
        }, cancellationToken: ct));
    }

    private const string SelectDevices = """
        SELECT
            d."DeviceId",
            d."LocationId",
            l."Name" AS "LocationName",
            d."EmployeeListVersion",
            d."PendingPunches",
            d."PendingFailures",
            d."FirmwareVersion",
            d."WifiRssi",
            d."BootedAt",
            d."FirstSeenAt",
            d."LastSeenAt",
            COALESCE(o24."Seconds", 0) AS "Downtime24hSeconds",
            COALESCE(o7."Seconds", 0) AS "Downtime7dSeconds",
            COALESCE(o7."Count", 0)::int AS "Outages7d"
        FROM "ClockDevices" d
        LEFT JOIN "Locations" l ON l."Id" = d."LocationId"
        LEFT JOIN LATERAL (
            SELECT SUM(extract(epoch FROM o."EndedAt" - GREATEST(o."StartedAt", NOW() - INTERVAL '24 hours')))::float8 AS "Seconds"
            FROM "ClockDeviceOutages" o
            WHERE o."DeviceId" = d."DeviceId" AND o."EndedAt" > NOW() - INTERVAL '24 hours'
        ) o24 ON true
        LEFT JOIN LATERAL (
            SELECT SUM(extract(epoch FROM o."EndedAt" - GREATEST(o."StartedAt", NOW() - INTERVAL '7 days')))::float8 AS "Seconds",
                   COUNT(*) AS "Count"
            FROM "ClockDeviceOutages" o
            WHERE o."DeviceId" = d."DeviceId" AND o."EndedAt" > NOW() - INTERVAL '7 days'
        ) o7 ON true
        """;

    public async Task<IReadOnlyList<ClockDevice>> GetAllAsync(CancellationToken ct = default)
    {
        var sql = SelectDevices + """
             ORDER BY d."LastSeenAt" DESC
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<ClockDevice>(new CommandDefinition(sql, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<ClockDevice?> GetAsync(string deviceId, CancellationToken ct = default)
    {
        var sql = SelectDevices + """
             WHERE d."DeviceId" = @DeviceId
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ClockDevice>(
            new CommandDefinition(sql, new { DeviceId = deviceId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ClockDeviceOutage>> GetOutagesAsync(string deviceId, DateTime sinceUtc, int limit, CancellationToken ct = default)
    {
        var sql = """
            SELECT "Id", "DeviceId", "StartedAt", "EndedAt", "Restarted"
            FROM "ClockDeviceOutages"
            WHERE "DeviceId" = @DeviceId AND "EndedAt" > @Since
            ORDER BY "EndedAt" DESC
            LIMIT @Limit
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<ClockDeviceOutage>(new CommandDefinition(
            sql, new { DeviceId = deviceId, Since = sinceUtc, Limit = limit }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<bool> DeleteAsync(string deviceId, CancellationToken ct = default)
    {
        var sql = """
            DELETE FROM "ClockDeviceOutages" WHERE "DeviceId" = @DeviceId;
            DELETE FROM "ClockDevices" WHERE "DeviceId" = @DeviceId;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { DeviceId = deviceId }, cancellationToken: ct));
        return rows > 0;
    }
}
