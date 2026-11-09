using Dapper;
using Npgsql;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private const string SelectSql = """
        SELECT
            e."Id",
            e."FirstName",
            e."LastName",
            e."Email",
            e."EmployeeNumber",
            e."IsActive",
            e."IsDeleted",
            e."DeletedAt",
            e."Pin",
            e."LocationId",
            l."Name" AS "LocationName",
            e."CreatedAt",
            e."UpdatedAt"
        FROM "Employees" e
        LEFT JOIN "Locations" l ON l."Id" = e."LocationId"
        """;

    private readonly IDbConnectionFactory _db;

    public EmployeeRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sql = SelectSql + """ WHERE e."Id" = @Id """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Employee>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken ct = default)
    {
        var sql = SelectSql + """
             WHERE e."EmployeeNumber" = @EmployeeNumber
               AND e."IsDeleted" = false
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Employee>(
            new CommandDefinition(sql, new { EmployeeNumber = employeeNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(
        bool? isActive = null,
        Guid? locationId = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var sql = SelectSql + """
            WHERE (@IncludeDeleted = true OR e."IsDeleted" = false)
              AND (@IsActive IS NULL OR e."IsActive" = @IsActive)
              AND (@LocationId IS NULL OR e."LocationId" = @LocationId)
            ORDER BY e."LastName", e."FirstName"
            """;
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<Employee>(new CommandDefinition(sql, new
        {
            IncludeDeleted = includeDeleted,
            IsActive = isActive,
            LocationId = locationId
        }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<Guid> CreateAsync(Employee employee, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var sql = """
            INSERT INTO "Employees" (
                "Id", "FirstName", "LastName", "Email", "EmployeeNumber",
                "IsActive", "IsDeleted", "LocationId", "CreatedAt", "UpdatedAt"
            )
            VALUES (
                @Id, @FirstName, @LastName, @Email, @EmployeeNumber,
                @IsActive, false, @LocationId, @CreatedAt, @UpdatedAt
            )
            """;
        var now = DateTime.UtcNow;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.EmployeeNumber,
            employee.IsActive,
            employee.LocationId,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken: ct));
        return id;
    }

    public async Task<bool> UpdateAsync(Employee employee, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "Employees"
            SET
                "FirstName" = @FirstName,
                "LastName" = @LastName,
                "Email" = @Email,
                "EmployeeNumber" = @EmployeeNumber,
                "IsActive" = @IsActive,
                "LocationId" = @LocationId,
                "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id
              AND "IsDeleted" = false
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.EmployeeNumber,
            employee.IsActive,
            employee.LocationId,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "Employees"
            SET
                "IsDeleted" = true,
                "IsActive" = false,
                "DeletedAt" = @DeletedAt,
                "UpdatedAt" = @DeletedAt
            WHERE "Id" = @Id
              AND "IsDeleted" = false
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            DeletedAt = DateTime.UtcNow
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<PinUpdateResult> SetPinAsync(Guid id, string pin, CancellationToken ct = default)
    {
        if (!IsValidPin(pin))
            return PinUpdateResult.Fail("PIN must be 4 digits and cannot start with 0.");

        var employee = await GetByIdAsync(id, ct);
        if (employee is null || employee.IsDeleted)
            return PinUpdateResult.Fail("Employee not found.");

        var taken = await FindActiveByPinAsync(pin, ct);
        if (taken is not null && taken.Id != id)
            return PinUpdateResult.Fail($"That PIN is already assigned to {taken.FirstName} {taken.LastName}.");

        var sql = """
            UPDATE "Employees"
            SET "Pin" = @Pin, "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;
        try
        {
            using var conn = _db.CreateConnection();
            var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                Pin = pin,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken: ct));
            return rows > 0
                ? PinUpdateResult.Ok()
                : PinUpdateResult.Fail("Employee not found.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return PinUpdateResult.Fail("That PIN is already assigned to another employee.");
        }
    }

    public async Task<bool> ClearPinAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "Employees"
            SET "Pin" = NULL, "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<Employee?> VerifyPinAndGetEmployeeAsync(string pin, CancellationToken ct = default)
    {
        if (!IsValidPin(pin))
            return null;
        var sql = SelectSql + """
             WHERE e."Pin" = @Pin
               AND e."IsDeleted" = false
               AND e."IsActive" = true
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Employee>(
            new CommandDefinition(sql, new { Pin = pin }, cancellationToken: ct));
    }

    public static bool IsValidPin(string? pin)
        => !string.IsNullOrEmpty(pin)
           && pin.Length == 4
           && pin[0] >= '1'
           && pin[0] <= '9'
           && pin.All(char.IsDigit);

    private async Task<Employee?> FindActiveByPinAsync(string pin, CancellationToken ct)
    {
        var sql = SelectSql + """
             WHERE e."Pin" = @Pin
               AND e."IsDeleted" = false
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Employee>(
            new CommandDefinition(sql, new { Pin = pin }, cancellationToken: ct));
    }
}
