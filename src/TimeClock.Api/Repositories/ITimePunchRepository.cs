using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface ITimePunchRepository
{
    Task<TimePunch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TimePunch>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? employeeId = null, Guid? locationId = null, CancellationToken ct = default);
    Task<Guid> CreateAsync(TimePunch punch, CancellationToken ct = default);
    Task<int> CreateBatchAsync(IEnumerable<TimePunch> punches, CancellationToken ct = default);
    Task<bool> UpdateAsync(TimePunch punch, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
