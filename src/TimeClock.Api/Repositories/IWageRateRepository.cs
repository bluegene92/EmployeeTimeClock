using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IWageRateRepository
{
    Task<WageRate?> GetCurrentByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, WageRate>> GetCurrentByEmployeeIdsAsync(IEnumerable<Guid> employeeIds, CancellationToken ct = default);
    Task<IReadOnlyList<WageRate>> GetHistoryAsync(Guid employeeId, CancellationToken ct = default);
    Task<WageRate> SetCurrentAsync(Guid employeeId, RateType rateType, decimal? hourlyRate, decimal? salaryAmount, CancellationToken ct = default);
}
