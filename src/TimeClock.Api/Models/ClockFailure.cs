namespace TimeClock.Api.Models;

public static class ClockFailureReasons
{
    /// <summary>The clock read a card that is not in its employee list.</summary>
    public const string UnknownCard = "UnknownCard";

    /// <summary>The API refused a punch sent by a clock, for example for a deleted employee.</summary>
    public const string PunchRejected = "PunchRejected";
}

public class ClockFailure
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = "";
    public string Message { get; set; } = "";
    public string? CardUid { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeFirstName { get; set; }
    public string? EmployeeLastName { get; set; }
    public PunchType? PunchType { get; set; }
    /// <summary>Local wall-clock time of the scan. Not UTC.</summary>
    public DateTime? OccurredAt { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? Source { get; set; }
    public string? DeviceId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
