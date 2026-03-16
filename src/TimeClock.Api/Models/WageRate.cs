namespace TimeClock.Api.Models;

public enum RateType
{
    Hourly = 0,
    Salary = 1
}

public class WageRate
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal? SalaryAmount { get; set; }
    public RateType RateType { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
