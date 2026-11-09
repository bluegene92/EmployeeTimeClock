using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/employees/{employeeId:guid}/wages")]
public class EmployeeWagesController : ControllerBase
{
    private readonly IEmployeeRepository _employees;
    private readonly IWageRateRepository _wages;

    public EmployeeWagesController(IEmployeeRepository employees, IWageRateRepository wages)
    {
        _employees = employees;
        _wages = wages;
    }

    [HttpGet]
    public async Task<ActionResult<WageRate>> GetCurrent(Guid employeeId, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null || employee.IsDeleted)
            return NotFound();

        var wage = await _wages.GetCurrentByEmployeeIdAsync(employeeId, ct);
        if (wage is null)
            return NotFound();
        return Ok(wage);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<WageRate>>> GetHistory(Guid employeeId, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null || employee.IsDeleted)
            return NotFound();
        return Ok(await _wages.GetHistoryAsync(employeeId, ct));
    }

    [HttpPut]
    public async Task<ActionResult<WageRate>> SetCurrent(Guid employeeId, [FromBody] UpsertWageRequest request, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null || employee.IsDeleted)
            return NotFound();

        var error = EmployeesController.ValidatePay(request.RateType, request.HourlyRate, request.SalaryAmount, requireAmount: true);
        if (error is not null)
            return BadRequest(new { message = error });

        var wage = await _wages.SetCurrentAsync(
            employeeId,
            request.RateType,
            request.RateType == RateType.Hourly ? request.HourlyRate : null,
            request.RateType == RateType.Salary ? request.SalaryAmount : null,
            ct);
        return Ok(wage);
    }
}

public record UpsertWageRequest(RateType RateType, decimal? HourlyRate, decimal? SalaryAmount);
