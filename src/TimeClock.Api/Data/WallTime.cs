namespace TimeClock.Api.Data;

public static class WallTime
{
    /// <summary>
    /// Keeps the clock numbers and drops any UTC/local kind so PostgreSQL
    /// stores them in a timestamp without time zone.
    /// </summary>
    public static DateTime AsUnspecified(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
