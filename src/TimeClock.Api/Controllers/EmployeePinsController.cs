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
        var result = await _employees.SetPinAsync(employeeId, request.Pin?.Trim() ?? "", ct);
        if (!result.Success)
        {
            if (result.Error == "Employee not found.")
                return NotFound(new { message = result.Error });
            return BadRequest(new { message = result.Error });
        }
        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult> ClearPin(Guid employeeId, CancellationToken ct)
    {
        var success = await _employees.ClearPinAsync(employeeId, ct);
        if (!success)
            return NotFound(new { message = "Employee not found." });
        return NoContent();
    }
}

public record SetPinRequest(string? Pin);
