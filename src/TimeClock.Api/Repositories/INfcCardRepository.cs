using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface INfcCardRepository
{
    Task<NfcCard?> GetByCardUidAsync(string cardUid, CancellationToken ct = default);
    Task<NfcCard?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<NfcCard>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<Guid> CreateAsync(NfcCard card, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task UpdateLastUsedAsync(Guid id, DateTime usedAt, CancellationToken ct = default);
}
