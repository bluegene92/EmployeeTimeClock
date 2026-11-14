using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IClockFailureRepository
{
    Task<IReadOnlyList<ClockFailure>> GetAsync(bool includeResolved, int limit, CancellationToken ct = default);
    /// <summary>Returns false when a failure with the same ID was already recorded.</summary>
    Task<bool> CreateAsync(ClockFailure failure, CancellationToken ct = default);
    Task<bool> SetResolvedAsync(Guid id, bool resolved, CancellationToken ct = default);
    Task<int> ResolveCardAsync(string cardUid, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>The open employee that currently has this card, if any.</summary>
    Task<(Guid Id, string Name)?> FindCardOwnerAsync(string cardUid, CancellationToken ct = default);
}
