using Microsoft.AspNetCore.Mvc;
using Npgsql;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeRepository _employees;
    private readonly IWageRateRepository _wages;
    private readonly ILocationRepository _locations;

    public EmployeesController(
        IEmployeeRepository employees,
        IWageRateRepository wages,
        ILocationRepository locations)
    {
        _employees = employees;
        _wages = wages;
        _locations = locations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Employee>>> GetAll(
        [FromQuery] bool? isActive,
        [FromQuery] Guid? locationId,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var list = await _employees.GetAllAsync(isActive, locationId, includeDeleted, ct);
        await AttachWagesAsync(list, ct);
        return Ok(list);
    }

    [HttpGet("next-number")]
    public async Task<ActionResult<NextEmployeeNumberResponse>> GetNextNumber(CancellationToken ct)
    {
        var number = await _employees.GetNextEmployeeNumberAsync(ct);
        if (number is null)
            return BadRequest(new { message = "No 4-digit employee numbers are left." });
        return Ok(new NextEmployeeNumberResponse(number));
    }

    [HttpGet("employee-number/check")]
    public async Task<ActionResult<EmployeeNumberCheckResponse>> CheckEmployeeNumber(
        [FromQuery] string? number,
        [FromQuery] Guid? excludeId,
        CancellationToken ct)
    {
        var trimmed = number?.Trim() ?? "";
        var message = await _employees.CheckEmployeeNumberAsync(trimmed, excludeId, ct);
        return Ok(new EmployeeNumberCheckResponse(trimmed, message is null, message));
    }

    [HttpPost("{id:guid}/employee-number")]
    public async Task<ActionResult<Employee>> GenerateEmployeeNumber(Guid id, CancellationToken ct)
    {
        var error = await _employees.AssignGeneratedNumberAsync(id, ct);
        if (error is not null)
        {
            if (error == "Employee not found.")
                return NotFound(new { message = error });
            return BadRequest(new { message = error });
        }

        var employee = await _employees.GetByIdAsync(id, ct);
        if (employee is null)
            return NotFound();
        employee.WageRate = await _wages.GetCurrentByEmployeeIdAsync(id, ct);
        return Ok(employee);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Employee>> GetById(Guid id, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(id, ct);
        if (employee is null)
            return NotFound();
        employee.WageRate = await _wages.GetCurrentByEmployeeIdAsync(id, ct);
        return Ok(employee);
    }

    [HttpPost]
    public async Task<ActionResult<Employee>> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var error = await ValidateAsync(
            request.FirstName,
            request.LastName,
            request.Email,
            request.EmployeeNumber,
            request.LocationId,
            null,
            requireEmployeeNumber: !request.GenerateEmployeeNumber,
            ct);
        if (error is not null)
            return BadRequest(new { message = error });

        var payError = ValidatePay(request.RateType, request.HourlyRate, request.SalaryAmount, requireAmount: false);
        if (payError is not null)
            return BadRequest(new { message = payError });

        var employee = new Employee
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            EmployeeNumber = request.GenerateEmployeeNumber ? "" : request.EmployeeNumber.Trim(),
            IsActive = request.IsActive,
            LocationId = request.LocationId
        };

        Guid id;
        if (request.GenerateEmployeeNumber)
        {
            var generated = await _employees.CreateWithGeneratedNumberAsync(employee, ct);
            if (!generated.Success)
                return BadRequest(new { message = generated.Error });
            id = generated.Id;
        }
        else
        {
            try
            {
                id = await _employees.CreateAsync(employee, ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return BadRequest(new { message = "Employee number is already in use." });
            }
        }

        if (request.RateType == RateType.Salary && request.SalaryAmount is decimal salary)
            await _wages.SetCurrentAsync(id, RateType.Salary, null, salary, ct);
        else if (request.HourlyRate is decimal hourly)
            await _wages.SetCurrentAsync(id, RateType.Hourly, hourly, null, ct);

        var created = await _employees.GetByIdAsync(id, ct);
        if (created is not null)
            created.WageRate = await _wages.GetCurrentByEmployeeIdAsync(id, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
    {
        var existing = await _employees.GetByIdAsync(id, ct);
        if (existing is null || existing.IsDeleted)
            return NotFound();

        var error = await ValidateAsync(request.FirstName, request.LastName, request.Email, request.EmployeeNumber, request.LocationId, id, requireEmployeeNumber: true, ct);
        if (error is not null)
            return BadRequest(new { message = error });

        existing.FirstName = request.FirstName.Trim();
        existing.LastName = request.LastName.Trim();
        existing.Email = request.Email.Trim();
        existing.EmployeeNumber = request.EmployeeNumber.Trim();
        existing.IsActive = request.IsActive;
        existing.LocationId = request.LocationId;

        try
        {
            await _employees.UpdateAsync(existing, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return BadRequest(new { message = "Employee number is already in use." });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var found = await _employees.SoftDeleteAsync(id, ct);
        if (!found)
            return NotFound();
        return NoContent();
    }

    private async Task AttachWagesAsync(IReadOnlyList<Employee> employees, CancellationToken ct)
    {
        var wages = await _wages.GetCurrentByEmployeeIdsAsync(employees.Select(e => e.Id), ct);
        foreach (var employee in employees)
        {
            if (wages.TryGetValue(employee.Id, out var wage))
                employee.WageRate = wage;
        }
    }

    private async Task<string?> ValidateAsync(
        string firstName,
        string lastName,
        string email,
        string employeeNumber,
        Guid? locationId,
        Guid? currentId,
        bool requireEmployeeNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return "First and last name are required.";
        if (string.IsNullOrWhiteSpace(email))
            return "Email is required.";
        if (requireEmployeeNumber)
        {
            var numberError = await _employees.CheckEmployeeNumberAsync(employeeNumber ?? "", currentId, ct);
            if (numberError is not null)
                return numberError;
        }

        if (locationId is Guid location)
        {
            var found = await _locations.GetByIdAsync(location, ct);
            if (found is null)
                return "Location not found.";
        }
        return null;
    }

    internal static string? ValidatePay(RateType rateType, decimal? hourlyRate, decimal? salaryAmount, bool requireAmount)
    {
        if (rateType == RateType.Salary)
        {
            if (salaryAmount is null && !requireAmount)
                return null;
            if (salaryAmount is null || salaryAmount < 0)
                return "Salary amount must be zero or greater.";
            return null;
        }

        if (hourlyRate is null && !requireAmount)
            return null;
        if (hourlyRate is null || hourlyRate < 0)
            return "Hourly rate must be zero or greater.";
        return null;
    }
}

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string EmployeeNumber,
    bool IsActive = true,
    Guid? LocationId = null,
    RateType RateType = RateType.Hourly,
    decimal? HourlyRate = null,
    decimal? SalaryAmount = null,
    bool GenerateEmployeeNumber = false);

public record NextEmployeeNumberResponse(string EmployeeNumber);

public record EmployeeNumberCheckResponse(string EmployeeNumber, bool Available, string? Message);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string EmployeeNumber,
    bool IsActive,
    Guid? LocationId);
