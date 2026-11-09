namespace TimeClock.Api.Models;

public enum DayRateSource
{
    Current = 0,
    Override = 1,
    Frozen = 2
}

public class ResolvedDayRate
{
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal? HourlyRate { get; set; }
    public DayRateSource Source { get; set; }
    public bool IsVerified { get; set; }
    public RateType RateType { get; set; }
}
