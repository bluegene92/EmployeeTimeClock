namespace TimeClock.Api.Models;

public class ClockDevice
{
    public string DeviceId { get; set; } = "";
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    /// <summary>Version of the employee list the clock last downloaded.</summary>
    public string? EmployeeListVersion { get; set; }
    public int PendingPunches { get; set; }
    public int PendingFailures { get; set; }
    public string? FirmwareVersion { get; set; }
    public int? WifiRssi { get; set; }
    public DateTime? BootedAt { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    /// <summary>Recorded outages overlapping the last 24 hours / 7 days, in seconds. Excludes an outage still in progress.</summary>
    public double Downtime24hSeconds { get; set; }
    public double Downtime7dSeconds { get; set; }
    public int Outages7d { get; set; }
}

public class ClockDeviceOutage
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public bool Restarted { get; set; }
}
