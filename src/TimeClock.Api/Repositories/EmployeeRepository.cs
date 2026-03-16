using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly IDbConnectionFactory _db;

    public EmployeeRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ""Id"", ""FirstName"", ""LastName"", ""Email"", ""EmployeeNumber"", ""IsActive"", ""PinHash"", ""CreatedAt"", ""UpdatedAt""
            FROM ""Employees"" WHERE ""Id"" = @Id";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Employee>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ""Id"", ""FirstName"", ""LastName"", ""Email"", ""EmployeeNumber"", ""IsActive"", ""PinHash"", ""CreatedAt"", ""UpdatedAt""
            FROM ""Employees"" WHERE ""EmployeeNumber"" = @EmployeeNumber";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Employee>(new CommandDefinition(sql, new { EmployeeNumber = employeeNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var sql = @"
            SELECT ""Id"", ""FirstName"", ""LastName"", ""Email"", ""EmployeeNumber"", ""IsActive"", ""PinHash"", ""CreatedAt"", ""UpdatedAt""
            FROM ""Employees""";
        if (isActive.HasValue)
            sql += @" WHERE ""IsActive"" = @IsActive";
        sql += @" ORDER BY ""LastName"", ""FirstName""";

        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<Employee>(new CommandDefinition(sql, new { IsActive = isActive }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<Guid> CreateAsync(Employee employee, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO ""Employees"" (""Id"", ""FirstName"", ""LastName"", ""Email"", ""EmployeeNumber"", ""IsActive"", ""PinHash"", ""CreatedAt"", ""UpdatedAt"")
            VALUES (@Id, @FirstName, @LastName, @Email, @EmployeeNumber, @IsActive, @PinHash, @CreatedAt, @UpdatedAt)";

        var now = DateTime.UtcNow;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.EmployeeNumber,
            IsActive = employee.IsActive,
            PinHash = employee.PinHash,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken: ct));
        return id;
    }

    public async Task<bool> UpdateAsync(Employee employee, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ""Employees"" SET
                ""FirstName"" = @FirstName,
                ""LastName"" = @LastName,
                ""Email"" = @Email,
                ""EmployeeNumber"" = @EmployeeNumber,
                ""IsActive"" = @IsActive,
                ""PinHash"" = @PinHash,
                ""UpdatedAt"" = @UpdatedAt
            WHERE ""Id"" = @Id";

        var now = DateTime.UtcNow;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.EmployeeNumber,
            employee.IsActive,
            employee.PinHash,
            UpdatedAt = now
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"DELETE FROM ""Employees"" WHERE ""Id"" = @Id";
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> SetPinAsync(Guid id, string pin, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
            return false;
        var hash = BCrypt.Net.BCrypt.HashPassword(pin, workFactor: 10);
        const string sql = @"UPDATE ""Employees"" SET ""PinHash"" = @PinHash, ""UpdatedAt"" = @UpdatedAt WHERE ""Id"" = @Id";
        var now = DateTime.UtcNow;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, PinHash = hash, UpdatedAt = now }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> ClearPinAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"UPDATE ""Employees"" SET ""PinHash"" = NULL, ""UpdatedAt"" = @UpdatedAt WHERE ""Id"" = @Id";
        var now = DateTime.UtcNow;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedAt = now }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<Employee?> VerifyPinAndGetEmployeeAsync(string pin, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
            return null;
        var employees = await GetAllAsync(isActive: true, ct);
        foreach (var emp in employees)
        {
            if (!string.IsNullOrEmpty(emp.PinHash) && BCrypt.Net.BCrypt.Verify(pin, emp.PinHash))
                return emp;
        }
        return null;
    }
}
