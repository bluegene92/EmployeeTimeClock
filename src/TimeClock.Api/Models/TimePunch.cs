namespace TimeClock.Api.Models;

public enum PunchType
{
    In = 0,
    Out = 1
}

public class TimePunch
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public PunchType PunchType { get; set; }
    public DateTime PunchedAt { get; set; }
    public string Source { get; set; } = "Desktop";
    public string? DeviceId { get; set; }
}
