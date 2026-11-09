using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Pay;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/day-rates")]
public class DayRatesController : ControllerBase
{
    private readonly IDayRateRepository _dayRates;
    private readonly IEmployeeRepository _employees;
    private readonly IWageRateRepository _wages;
    private readonly IPayPeriodRepository _payPeriods;

    public DayRatesController(
        IDayRateRepository dayRates,
        IEmployeeRepository employees,
        IWageRateRepository wages,
        IPayPeriodRepository payPeriods)
    {
        _dayRates = dayRates;
        _employees = employees;
        _wages = wages;
        _payPeriods = payPeriods;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResolvedDayRate>>> Get(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? employeeId,
        CancellationToken ct)
    {
        if (to < from)
            return BadRequest(new { message = "The end date must be on or after the start date." });
        if (to.DayNumber - from.DayNumber > 62)
            return BadRequest(new { message = "Choose a range of 62 days or fewer." });
        return Ok(await _dayRates.GetResolvedAsync(from, to, employeeId, ct));
    }

    [HttpPut]
    public async Task<ActionResult> Set([FromBody] SetDayRateRequest request, CancellationToken ct)
    {
        if (request.HourlyRate < 0)
            return BadRequest(new { message = "Hourly rate must be zero or greater." });

        var employee = await _employees.GetByIdAsync(request.EmployeeId, ct);
        if (employee is null || employee.IsDeleted)
            return NotFound(new { message = "Employee not found." });

        var wage = await _wages.GetCurrentByEmployeeIdAsync(request.EmployeeId, ct);
        if (wage is null || wage.RateType != RateType.Hourly)
            return BadRequest(new { message = "A daily rate applies to hourly employees. Salary pay is set on the employee." });

        var settings = await _payPeriods.GetAsync(ct);
        var periodStart = PayPeriodCalculator.GetPeriodStart(settings.AnchorDate, request.WorkDate);
        if (await _dayRates.IsVerifiedAsync(request.EmployeeId, periodStart, ct))
            return Conflict(new { message = "This pay period is verified. Unverify it before changing the daily rate." });

        if (wage.HourlyRate == request.HourlyRate)
            await _dayRates.DeleteOverrideAsync(request.EmployeeId, request.WorkDate, ct);
        else
            await _dayRates.UpsertOverrideAsync(request.EmployeeId, request.WorkDate, request.HourlyRate, ct);

        return NoContent();
    }
}

public record SetDayRateRequest(Guid EmployeeId, DateOnly WorkDate, decimal HourlyRate);
