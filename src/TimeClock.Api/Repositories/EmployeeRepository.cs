using System.Globalization;
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
            e."CardUid",
            e."LocationId",
            l."Name" AS "LocationName",
            e."CreatedAt",
            e."UpdatedAt"
        FROM "Employees" e
        LEFT JOIN "Locations" l ON l."Id" = e."LocationId"
        """;

    /// <summary>
    /// Smallest unused code from 1000 through 9999. Open 4-digit employee numbers and PINs
    /// are both taken, so the generated code cannot match another person's clock code.
    /// </summary>
    private const string NextNumberSql = """
        SELECT s.n
        FROM generate_series(1000, 9999) AS s(n)
        WHERE NOT EXISTS (
            SELECT 1
            FROM "Employees" e
            WHERE e."IsDeleted" = false
              AND (@ExcludeId IS NULL OR e."Id" <> @ExcludeId)
              AND (
                    (e."EmployeeNumber" ~ '^[1-9][0-9]{3}$' AND e."EmployeeNumber"::int = s.n)
                    OR (e."Pin" ~ '^[1-9][0-9]{3}$' AND e."Pin"::int = s.n)
                  )
        )
        ORDER BY s.n
        LIMIT 1
        """;

    private const long EmployeeNumberLockKey = 7421000;

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

    public async Task<CreateEmployeeResult> CreateWithGeneratedNumberAsync(Employee employee, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var conn = _db.CreateConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();
                await LockNumbersAsync(conn, tx, ct);
                var number = await NextNumberAsync(conn, tx, null, ct);
                if (number is null)
                    return CreateEmployeeResult.Fail("No 4-digit employee numbers are left.");

                var id = Guid.NewGuid();
                var now = DateTime.UtcNow;
                var sql = """
                    INSERT INTO "Employees" (
                        "Id", "FirstName", "LastName", "Email", "EmployeeNumber",
                        "IsActive", "IsDeleted", "Pin", "LocationId", "CreatedAt", "UpdatedAt"
                    )
                    VALUES (
                        @Id, @FirstName, @LastName, @Email, @EmployeeNumber,
                        @IsActive, false, @Pin, @LocationId, @CreatedAt, @UpdatedAt
                    )
                    """;
                await conn.ExecuteAsync(new CommandDefinition(sql, new
                {
                    Id = id,
                    employee.FirstName,
                    employee.LastName,
                    employee.Email,
                    EmployeeNumber = number,
                    employee.IsActive,
                    Pin = number,
                    employee.LocationId,
                    CreatedAt = now,
                    UpdatedAt = now
                }, tx, cancellationToken: ct));
                tx.Commit();
                return CreateEmployeeResult.Ok(id);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                if (attempt == 2)
                    return CreateEmployeeResult.Fail("Employee number is already in use.");
            }
        }

        return CreateEmployeeResult.Fail("Employee number is already in use.");
    }

    public async Task<string?> GetNextEmployeeNumberAsync(CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await NextNumberAsync(conn, null, null, ct);
    }

    public async Task<string?> AssignGeneratedNumberAsync(Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var conn = _db.CreateConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();
                await LockNumbersAsync(conn, tx, ct);

                var exists = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                    """SELECT EXISTS(SELECT 1 FROM "Employees" WHERE "Id" = @Id AND "IsDeleted" = false)""",
                    new { Id = id },
                    tx,
                    cancellationToken: ct));
                if (!exists)
                    return "Employee not found.";

                var number = await NextNumberAsync(conn, tx, id, ct);
                if (number is null)
                    return "No 4-digit employee numbers are left.";

                var sql = """
                    UPDATE "Employees"
                    SET "EmployeeNumber" = @EmployeeNumber,
                        "Pin" = @Pin,
                        "UpdatedAt" = @UpdatedAt
                    WHERE "Id" = @Id
                      AND "IsDeleted" = false
                    """;
                var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
                {
                    Id = id,
                    EmployeeNumber = number,
                    Pin = number,
                    UpdatedAt = DateTime.UtcNow
                }, tx, cancellationToken: ct));
                if (rows == 0)
                    return "Employee not found.";

                tx.Commit();
                return null;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                if (attempt == 2)
                    return "Employee number is already in use.";
            }
        }

        return "Employee number is already in use.";
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

        var taken = await FindOtherByCodeAsync(pin, id, includePin: true, ct);
        if (taken is not null)
        {
            return PinUpdateResult.Fail(taken.EmployeeNumber == pin
                ? $"{pin} is already the employee number for {taken.FirstName} {taken.LastName}."
                : $"That PIN is already assigned to {taken.FirstName} {taken.LastName}.");
        }

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

    public async Task<PinUpdateResult> SetCardAsync(Guid id, string cardUid, CancellationToken ct = default)
    {
        var uid = NormalizeCardUid(cardUid);
        if (uid is null)
            return PinUpdateResult.Fail("Card ID must be 8 to 20 hex characters, for example 04A1B2C3.");

        var employee = await GetByIdAsync(id, ct);
        if (employee is null || employee.IsDeleted)
            return PinUpdateResult.Fail("Employee not found.");

        var sql = """
            UPDATE "Employees"
            SET "CardUid" = @CardUid, "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;
        try
        {
            using var conn = _db.CreateConnection();
            var owner = await conn.QueryFirstOrDefaultAsync<Employee>(new CommandDefinition(
                SelectSql + """ WHERE e."CardUid" = @CardUid AND e."IsDeleted" = false AND e."Id" <> @Id LIMIT 1""",
                new { CardUid = uid, Id = id },
                cancellationToken: ct));
            if (owner is not null)
                return PinUpdateResult.Fail($"That card is already assigned to {owner.FirstName} {owner.LastName}.");

            var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                CardUid = uid,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken: ct));
            return rows > 0 ? PinUpdateResult.Ok() : PinUpdateResult.Fail("Employee not found.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return PinUpdateResult.Fail("That card is already assigned to another employee.");
        }
    }

    public async Task<bool> ClearCardAsync(Guid id, CancellationToken ct = default)
    {
        var sql = """
            UPDATE "Employees"
            SET "CardUid" = NULL, "UpdatedAt" = @UpdatedAt
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

    /// <summary>
    /// Uppercase hex with spaces, colons, and dashes removed. PN532 UIDs are 4, 7, or 10 bytes.
    /// </summary>
    public static string? NormalizeCardUid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var uid = new string(value.Where(c => c is not (' ' or ':' or '-')).ToArray()).ToUpperInvariant();
        if (uid.Length < 8 || uid.Length > 20 || uid.Length % 2 != 0 || !uid.All(Uri.IsHexDigit))
            return null;
        return uid;
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
        var byPin = await conn.QuerySingleOrDefaultAsync<Employee>(
            new CommandDefinition(sql, new { Pin = pin }, cancellationToken: ct));
        if (byPin is not null)
            return byPin;

        var byNumber = await GetByEmployeeNumberAsync(pin, ct);
        if (byNumber is null || !byNumber.IsActive)
            return null;
        return byNumber;
    }

    private static Task LockNumbersAsync(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(@Key)",
            new { Key = EmployeeNumberLockKey },
            tx,
            cancellationToken: ct));

    private static async Task<string?> NextNumberAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction? tx,
        Guid? excludeId,
        CancellationToken ct)
    {
        var next = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
            NextNumberSql,
            new { ExcludeId = excludeId },
            tx,
            cancellationToken: ct));
        if (next is null or < 1000 or > 9999)
            return null;
        return next.Value.ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsValidPin(string? pin)
        => !string.IsNullOrEmpty(pin)
           && pin.Length == 4
           && pin[0] >= '1'
           && pin[0] <= '9'
           && pin.All(char.IsDigit);

    public async Task<string?> CheckEmployeeNumberAsync(string employeeNumber, Guid? excludeId, CancellationToken ct = default)
    {
        var number = employeeNumber.Trim();
        if (number.Length == 0)
            return "Employee number is required.";
        if (number.Length > 50)
            return "Employee number must be 50 characters or fewer.";

        var owner = await FindOtherByCodeAsync(number, excludeId, includePin: IsValidPin(number), ct);
        if (owner is null)
            return null;
        return owner.EmployeeNumber == number
            ? $"Employee number {number} is already used by {owner.FirstName} {owner.LastName}."
            : $"{number} is already the clock PIN for {owner.FirstName} {owner.LastName}.";
    }

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

    /// <summary>
    /// An open employee other than <paramref name="excludeId"/> whose employee number, or PIN when
    /// <paramref name="includePin"/> is set, equals <paramref name="code"/>.
    /// </summary>
    private async Task<Employee?> FindOtherByCodeAsync(string code, Guid? excludeId, bool includePin, CancellationToken ct)
    {
        var sql = SelectSql + """
             WHERE e."IsDeleted" = false
               AND (@ExcludeId IS NULL OR e."Id" <> @ExcludeId)
               AND (e."EmployeeNumber" = @Code OR (@IncludePin AND e."Pin" = @Code))
             ORDER BY (e."EmployeeNumber" = @Code) DESC
             LIMIT 1
            """;
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Employee>(
            new CommandDefinition(sql, new { Code = code, ExcludeId = excludeId, IncludePin = includePin }, cancellationToken: ct));
    }
}
