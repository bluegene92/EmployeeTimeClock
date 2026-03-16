using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeRepository _employees;

    public EmployeesController(IEmployeeRepository employees) => _employees = employees;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Employee>>> GetAll([FromQuery] bool? isActive, CancellationToken ct)
    {
        var list = await _employees.GetAllAsync(isActive, ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Employee>> GetById(Guid id, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(id, ct);
        if (employee is null)
            return NotFound();
        return Ok(employee);
    }

    [HttpPost]
    public async Task<ActionResult<Employee>> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var employee = new Employee
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            EmployeeNumber = request.EmployeeNumber,
            IsActive = request.IsActive
        };
        var id = await _employees.CreateAsync(employee, ct);
        var created = await _employees.GetByIdAsync(id, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
    {
        var existing = await _employees.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        existing.FirstName = request.FirstName;
        existing.LastName = request.LastName;
        existing.Email = request.Email;
        existing.EmployeeNumber = request.EmployeeNumber;
        existing.IsActive = request.IsActive;

        await _employees.UpdateAsync(existing, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var found = await _employees.DeleteAsync(id, ct);
        if (!found)
            return NotFound();
        return NoContent();
    }
}

public record CreateEmployeeRequest(string FirstName, string LastName, string Email, string EmployeeNumber, bool IsActive = true);
public record UpdateEmployeeRequest(string FirstName, string LastName, string Email, string EmployeeNumber, bool IsActive);
