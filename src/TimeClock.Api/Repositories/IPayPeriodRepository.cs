using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IPayPeriodRepository
{
    Task<PayPeriodSettings> GetAsync(CancellationToken ct = default);
    Task<PayPeriodSettings> SetAnchorAsync(DateOnly anchorDate, CancellationToken ct = default);
}
