using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IClockDeviceRepository
{
    /// <summary>
    /// Fingerprint of the employee data clocks cache. It changes whenever an active
    /// employee's name, card, or location changes, or an admin pushes the list.
    /// </summary>
    Task<string> GetEmployeeListVersionAsync(CancellationToken ct = default);
    Task BumpEmployeeListAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the clock's row. If the previous check-in is older than <paramref name="offlineAfter"/>,
    /// or the clock restarted since, the gap is recorded as an outage.
    /// </summary>
    Task RecordCheckInAsync(ClockDevice device, long? uptimeSeconds, TimeSpan offlineAfter, CancellationToken ct = default);
    Task<IReadOnlyList<ClockDevice>> GetAllAsync(CancellationToken ct = default);
    Task<ClockDevice?> GetAsync(string deviceId, CancellationToken ct = default);
    Task<IReadOnlyList<ClockDeviceOutage>> GetOutagesAsync(string deviceId, DateTime sinceUtc, int limit, CancellationToken ct = default);
    Task<bool> DeleteAsync(string deviceId, CancellationToken ct = default);
}
