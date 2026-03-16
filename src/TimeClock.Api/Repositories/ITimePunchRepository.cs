using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface ITimePunchRepository
{
    Task<TimePunch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TimePunch>> GetByEmployeeAndDateRangeAsync(Guid employeeId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<TimePunch>> GetByDateRangeAsync(DateTime from, DateTime to, Guid? employeeId = null, CancellationToken ct = default);
    Task<Guid> CreateAsync(TimePunch punch, CancellationToken ct = default);
    Task<int> CreateBatchAsync(IEnumerable<TimePunch> punches, CancellationToken ct = default);
}
