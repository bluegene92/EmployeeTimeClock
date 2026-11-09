using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IDayRateRepository
{
    Task<IReadOnlyList<ResolvedDayRate>> GetResolvedAsync(DateOnly from, DateOnly to, Guid? employeeId, CancellationToken ct = default);
    Task UpsertOverrideAsync(Guid employeeId, DateOnly workDate, decimal hourlyRate, CancellationToken ct = default);
    Task<bool> DeleteOverrideAsync(Guid employeeId, DateOnly workDate, CancellationToken ct = default);
    Task<bool> IsVerifiedAsync(Guid employeeId, DateOnly periodStart, CancellationToken ct = default);
    Task VerifyAsync(Guid employeeId, DateOnly periodStart, IReadOnlyList<FrozenDay> days, CancellationToken ct = default);
    Task<bool> UnverifyAsync(Guid employeeId, DateOnly periodStart, CancellationToken ct = default);
    Task<bool> AnyVerifiedAsync(CancellationToken ct = default);
}

public sealed record FrozenDay(DateOnly WorkDate, decimal? HourlyRate);
