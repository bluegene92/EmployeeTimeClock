using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task<Guid> CreateAsync(Location location, CancellationToken ct = default);
    Task<bool> UpdateAsync(Location location, CancellationToken ct = default);
    Task<bool> IsInUseAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
