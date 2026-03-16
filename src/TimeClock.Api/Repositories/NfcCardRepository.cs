using Dapper;
using TimeClock.Api.Data;
using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public class NfcCardRepository : INfcCardRepository
{
    private readonly IDbConnectionFactory _db;

    public NfcCardRepository(IDbConnectionFactory db) => _db = db;

    public async Task<NfcCard?> GetByCardUidAsync(string cardUid, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, CardUid, EmployeeId, IsActive, RegisteredAt, LastUsedAt
            FROM NfcCards WHERE CardUid = @CardUid AND IsActive = 1";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<NfcCard>(new CommandDefinition(sql, new { CardUid = cardUid }, cancellationToken: ct));
    }

    public async Task<NfcCard?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, CardUid, EmployeeId, IsActive, RegisteredAt, LastUsedAt
            FROM NfcCards WHERE Id = @Id";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<NfcCard>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<NfcCard>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, CardUid, EmployeeId, IsActive, RegisteredAt, LastUsedAt
            FROM NfcCards WHERE EmployeeId = @EmployeeId ORDER BY RegisteredAt DESC";
        using var conn = _db.CreateConnection();
        var list = await conn.QueryAsync<NfcCard>(new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct));
        return list.ToList();
    }

    public async Task<Guid> CreateAsync(NfcCard card, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO NfcCards (Id, CardUid, EmployeeId, IsActive, RegisteredAt, LastUsedAt)
            VALUES (@Id, @CardUid, @EmployeeId, @IsActive, @RegisteredAt, NULL)";

        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            card.CardUid,
            card.EmployeeId,
            IsActive = card.IsActive,
            RegisteredAt = card.RegisteredAt
        }, cancellationToken: ct));
        return id;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM NfcCards WHERE Id = @Id";
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task UpdateLastUsedAsync(Guid id, DateTime usedAt, CancellationToken ct = default)
    {
        const string sql = "UPDATE NfcCards SET LastUsedAt = @UsedAt WHERE Id = @Id";
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UsedAt = usedAt }, cancellationToken: ct));
    }
}
