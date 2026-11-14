using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

/// <summary>
/// Clocks cannot be reached from the internet, so they check in every few seconds instead.
/// The check-in is the clock's heartbeat: gaps in it are recorded as downtime, and the reply
/// carries the current employee list version. A clock with an older version downloads the
/// list and checks in again, which is how admins see that a push arrived.
/// </summary>
[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    /// <summary>Clocks check in every 15 seconds, so a minute of silence means offline.</summary>
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);

    private readonly IClockDeviceRepository _devices;
    private readonly ILocationRepository _locations;

    public DevicesController(IClockDeviceRepository devices, ILocationRepository locations)
    {
        _devices = devices;
        _locations = locations;
    }

    [HttpPost("check-in")]
    public async Task<ActionResult<CheckInResponse>> CheckIn([FromBody] CheckInRequest request, CancellationToken ct)
    {
        var deviceId = request.DeviceId?.Trim();
        if (string.IsNullOrEmpty(deviceId) || deviceId.Length > 100)
            return BadRequest(new { message = "Device ID is required." });

        Guid? locationId = null;
        if (request.LocationId is Guid location && await _locations.GetByIdAsync(location, ct) is not null)
            locationId = location;

        long? uptime = request.UptimeSeconds is >= 0 ? request.UptimeSeconds : null;
        int? rssi = request.WifiRssi is < 0 and >= -127 ? request.WifiRssi : null;

        await _devices.RecordCheckInAsync(new ClockDevice
        {
            DeviceId = deviceId,
            LocationId = locationId,
            EmployeeListVersion = Truncate(request.EmployeeListVersion?.Trim(), 64),
            PendingPunches = Math.Max(0, request.PendingPunches),
            PendingFailures = Math.Max(0, request.PendingFailures),
            FirmwareVersion = Truncate(request.FirmwareVersion?.Trim(), 50),
            WifiRssi = rssi
        }, uptime, OnlineWindow, ct);

        return Ok(new CheckInResponse(await _devices.GetEmployeeListVersionAsync(ct)));
    }

    [HttpGet]
    public async Task<ActionResult<DevicesStatusResponse>> Get(CancellationToken ct) =>
        Ok(await BuildStatusAsync(ct));

    /// <summary>Downtime history for one clock, newest first. An outage still in progress comes first, with no end time.</summary>
    [HttpGet("{deviceId}/outages")]
    public async Task<ActionResult<List<OutageDto>>> GetOutages(string deviceId, [FromQuery] int days = 7, CancellationToken ct = default)
    {
        var device = await _devices.GetAsync(deviceId, ct);
        if (device is null)
            return NotFound();

        var now = DateTime.UtcNow;
        var since = now.AddDays(-Math.Clamp(days, 1, 90));
        var result = new List<OutageDto>();

        var lastSeen = Utc(device.LastSeenAt);
        if (now - lastSeen > OnlineWindow)
            result.Add(new OutageDto(lastSeen, null, (int)(now - lastSeen).TotalSeconds, false));

        foreach (var o in await _devices.GetOutagesAsync(deviceId, since, 500, ct))
        {
            var started = Utc(o.StartedAt);
            var ended = Utc(o.EndedAt);
            result.Add(new OutageDto(started, ended, (int)(ended - started).TotalSeconds, o.Restarted));
        }
        return Ok(result);
    }

    /// <summary>Makes every clock download the employee list on its next check-in.</summary>
    [HttpPost("push-employee-list")]
    public async Task<ActionResult<DevicesStatusResponse>> PushEmployeeList(CancellationToken ct)
    {
        await _devices.BumpEmployeeListAsync(ct);
        return Ok(await BuildStatusAsync(ct));
    }

    [HttpDelete("{deviceId}")]
    public async Task<ActionResult> Delete(string deviceId, CancellationToken ct)
    {
        if (!await _devices.DeleteAsync(deviceId, ct))
            return NotFound();
        return NoContent();
    }

    private async Task<DevicesStatusResponse> BuildStatusAsync(CancellationToken ct)
    {
        var version = await _devices.GetEmployeeListVersionAsync(ct);
        var now = DateTime.UtcNow;
        var devices = (await _devices.GetAllAsync(ct))
            .Select(d =>
            {
                var lastSeen = Utc(d.LastSeenAt);
                var online = now - lastSeen <= OnlineWindow;
                var offlineFor = online ? 0 : (now - lastSeen).TotalSeconds;
                return new DeviceStatusDto(
                    d.DeviceId,
                    d.LocationId,
                    d.LocationName,
                    d.EmployeeListVersion,
                    d.EmployeeListVersion == version,
                    online,
                    lastSeen,
                    (int)Math.Max(0, (now - lastSeen).TotalSeconds),
                    d.PendingPunches,
                    d.PendingFailures,
                    d.FirmwareVersion,
                    d.WifiRssi,
                    d.BootedAt is DateTime booted ? Utc(booted) : null,
                    Utc(d.FirstSeenAt),
                    (int)(d.Downtime24hSeconds + Math.Min(offlineFor, TimeSpan.FromHours(24).TotalSeconds)),
                    (int)(d.Downtime7dSeconds + Math.Min(offlineFor, TimeSpan.FromDays(7).TotalSeconds)),
                    d.Outages7d + (online ? 0 : 1));
            })
            .ToList();
        return new DevicesStatusResponse(version, devices);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}

public record CheckInRequest(
    string? DeviceId,
    string? EmployeeListVersion = null,
    int PendingPunches = 0,
    int PendingFailures = 0,
    string? FirmwareVersion = null,
    Guid? LocationId = null,
    long? UptimeSeconds = null,
    int? WifiRssi = null);

public record CheckInResponse(string EmployeeListVersion);

public record DeviceStatusDto(
    string DeviceId,
    Guid? LocationId,
    string? LocationName,
    string? EmployeeListVersion,
    bool UpToDate,
    bool Online,
    DateTime LastSeenAt,
    int SecondsSinceSeen,
    int PendingPunches,
    int PendingFailures,
    string? FirmwareVersion,
    int? WifiRssi,
    DateTime? BootedAt,
    DateTime FirstSeenAt,
    /// <summary>Includes an outage still in progress.</summary>
    int Downtime24hSeconds,
    int Downtime7dSeconds,
    int Outages7d);

public record OutageDto(DateTime StartedAt, DateTime? EndedAt, int DurationSeconds, bool Restarted);

public record DevicesStatusResponse(string EmployeeListVersion, List<DeviceStatusDto> Devices);
