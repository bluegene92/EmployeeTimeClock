using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Pay;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/pay-periods")]
public class PayPeriodsController : ControllerBase
{
    private readonly IPayPeriodRepository _payPeriods;
    private readonly IDayRateRepository _dayRates;
    private readonly IEmployeeRepository _employees;
    private readonly IWageRateRepository _wages;

    public PayPeriodsController(
        IPayPeriodRepository payPeriods,
        IDayRateRepository dayRates,
        IEmployeeRepository employees,
        IWageRateRepository wages)
    {
        _payPeriods = payPeriods;
        _dayRates = dayRates;
        _employees = employees;
        _wages = wages;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<PayPeriodSettings>> GetSettings(CancellationToken ct)
        => Ok(await _payPeriods.GetAsync(ct));

    [HttpPut("settings")]
    public async Task<ActionResult<PayPeriodSettings>> SetSettings([FromBody] PayPeriodSettingsRequest request, CancellationToken ct)
    {
        var current = await _payPeriods.GetAsync(ct);
        if (current.AnchorDate != request.AnchorDate && await _dayRates.AnyVerifiedAsync(ct))
            return Conflict(new { message = "Unverify pay periods before changing the bi-weekly start date." });

        return Ok(await _payPeriods.SetAnchorAsync(request.AnchorDate, ct));
    }

    [HttpPost("verify")]
    public async Task<ActionResult> Verify([FromBody] VerifyPayPeriodRequest request, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(request.EmployeeId, ct);
        if (employee is null || employee.IsDeleted)
            return NotFound(new { message = "Employee not found." });

        var settings = await _payPeriods.GetAsync(ct);
        if (!PayPeriodCalculator.IsPeriodStart(settings.AnchorDate, request.PeriodStart))
            return BadRequest(new { message = "That date is not the start of a bi-weekly period." });
        if (await _dayRates.IsVerifiedAsync(request.EmployeeId, request.PeriodStart, ct))
            return Conflict(new { message = "This pay period is already verified." });

        var wage = await _wages.GetCurrentByEmployeeIdAsync(request.EmployeeId, ct);
        var periodDays = PayPeriodCalculator.DaysInPeriod(request.PeriodStart);
        var resolved = wage is { RateType: RateType.Hourly }
            ? await _dayRates.GetResolvedAsync(periodDays[0], periodDays[^1], request.EmployeeId, ct)
            : [];
        var byDate = resolved.ToDictionary(row => row.WorkDate);
        var days = periodDays.Select(day =>
        {
            decimal? rate = null;
            if (wage is { RateType: RateType.Hourly })
                rate = byDate.TryGetValue(day, out var row) ? row.HourlyRate ?? wage.HourlyRate ?? 0 : wage.HourlyRate ?? 0;
            return new FrozenDay(day, rate);
        }).ToList();

        await _dayRates.VerifyAsync(request.EmployeeId, request.PeriodStart, days, ct);
        return NoContent();
    }

    [HttpPost("unverify")]
    public async Task<ActionResult> Unverify([FromBody] VerifyPayPeriodRequest request, CancellationToken ct)
    {
        var removed = await _dayRates.UnverifyAsync(request.EmployeeId, request.PeriodStart, ct);
        if (!removed)
            return NotFound(new { message = "That pay period is not verified." });
        return NoContent();
    }
}

public record PayPeriodSettingsRequest(DateOnly AnchorDate);
public record VerifyPayPeriodRequest(Guid EmployeeId, DateOnly PeriodStart);
