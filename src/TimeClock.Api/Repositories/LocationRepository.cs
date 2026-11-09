using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly IDbConnectionFactory _db;

    public LocationRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """
            SELECT "Id", "Name", "IsActive", "CreatedAt", "UpdatedAt"
            FROM "Locations"
            WHERE "Id" = @Id
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Location>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Location>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var sql = """
            SELECT "Id", "Name", "IsActive", "CreatedAt", "UpdatedAt"
            FROM "Locations"
            WHERE (@IsActive IS NULL OR "IsActive" = @IsActive)
            ORDER BY "Name"
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<Location>(
            new CommandDefinition(sql, new { IsActive = isActive }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<Guid> CreateAsync(Location location, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var sql = """
            INSERT INTO "Locations" ("Id", "Name", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES (@Id, @Name, @IsActive, @CreatedAt, @UpdatedAt)
            """;
        var now = DateTime.UtcNow;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            location.Name,
            location.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken: ct));
        return id;
    }

    public async Task<bool> UpdateAsync(Location location, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "Locations"
            SET "Name" = @Name, "IsActive" = @IsActive, "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            location.Id,
            location.Name,
            location.IsActive,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """
            SELECT
                EXISTS(SELECT 1 FROM "Employees" WHERE "LocationId" = @Id)
                OR EXISTS(SELECT 1 FROM "TimePunches" WHERE "LocationId" = @Id)
            """;
        using var conn = _db.CreateConnection();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """DELETE FROM "Locations" WHERE "Id" = @Id""";
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }
}
