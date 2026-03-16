using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/employees/{employeeId:guid}/pin")]
public class EmployeePinsController : ControllerBase
{
    private readonly IEmployeeRepository _employees;

    public EmployeePinsController(IEmployeeRepository employees) => _employees = employees;

    [HttpPut]
    public async Task<ActionResult> SetPin(Guid employeeId, [FromBody] SetPinRequest request, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return NotFound();

        if (string.IsNullOrEmpty(request.Pin) || request.Pin.Length != 4 || !request.Pin.All(char.IsDigit))
            return BadRequest("PIN must be exactly 4 digits.");

        var success = await _employees.SetPinAsync(employeeId, request.Pin, ct);
        if (!success)
            return BadRequest("Failed to set PIN.");
        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult> ClearPin(Guid employeeId, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return NotFound();

        var success = await _employees.ClearPinAsync(employeeId, ct);
        if (!success)
            return BadRequest("Failed to clear PIN.");
        return NoContent();
    }
}

public record SetPinRequest(string Pin);
