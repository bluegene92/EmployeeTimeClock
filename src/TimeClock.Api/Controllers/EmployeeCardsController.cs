using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

/// <summary>
/// Assigns the NFC card the clock reads to an employee.
/// </summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/card")]
public class EmployeeCardsController : ControllerBase
{
    private readonly IEmployeeRepository _employees;
    private readonly IClockFailureRepository _failures;

    public EmployeeCardsController(IEmployeeRepository employees, IClockFailureRepository failures)
    {
        _employees = employees;
        _failures = failures;
    }

    [HttpPut]
    public async Task<ActionResult> SetCard(Guid employeeId, [FromBody] SetCardRequest request, CancellationToken ct)
    {
        var result = await _employees.SetCardAsync(employeeId, request.CardUid ?? "", ct);
        if (!result.Success)
        {
            if (result.Error == "Employee not found.")
                return NotFound(new { message = result.Error });
            return BadRequest(new { message = result.Error });
        }

        // Unknown-card reports for this card are answered once it belongs to someone.
        if (EmployeeRepository.NormalizeCardUid(request.CardUid) is { } cardUid)
            await _failures.ResolveCardAsync(cardUid, ct);

        return NoContent();
    }

    [HttpDelete]
    public async Task<ActionResult> ClearCard(Guid employeeId, CancellationToken ct)
    {
        var success = await _employees.ClearCardAsync(employeeId, ct);
        if (!success)
            return NotFound(new { message = "Employee not found." });
        return NoContent();
    }
}

public record SetCardRequest(string? CardUid);
