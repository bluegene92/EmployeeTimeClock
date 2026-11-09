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
    public string? EmployeeFirstName { get; set; }
    public string? EmployeeLastName { get; set; }
    public bool EmployeeIsDeleted { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public PunchType PunchType { get; set; }
    /// <summary>Local wall-clock time. Not UTC.</summary>
    public DateTime PunchedAt { get; set; }
    public string Source { get; set; } = "Web";
    public string? DeviceId { get; set; }
}
