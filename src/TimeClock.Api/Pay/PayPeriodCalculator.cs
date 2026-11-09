namespace TimeClock.Api.Pay;

public static class PayPeriodCalculator
{
    public const int PeriodLengthDays = 14;

    public static DateOnly GetPeriodStart(DateOnly anchor, DateOnly date)
    {
        var diff = date.DayNumber - anchor.DayNumber;
        var index = (int)Math.Floor(diff / (double)PeriodLengthDays);
        return anchor.AddDays(index * PeriodLengthDays);
    }

    public static bool IsPeriodStart(DateOnly anchor, DateOnly periodStart)
        => GetPeriodStart(anchor, periodStart) == periodStart;

    public static IReadOnlyList<DateOnly> DaysInPeriod(DateOnly periodStart)
        => Enumerable.Range(0, PeriodLengthDays).Select(periodStart.AddDays).ToArray();
}
