using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IEmployeeRepository _employees;

    public AuthController(IEmployeeRepository employees) => _employees = employees;

    /// <summary>
    /// Verify a 4-digit PIN or employee number and return the employee. Used by the clock app
    /// when someone clocks in without an NFC badge.
    /// </summary>
    [HttpPost("verify-pin")]
    public async Task<ActionResult<VerifyPinResponse>> VerifyPin([FromBody] VerifyPinRequest request, CancellationToken ct)
    {
        var employee = await _employees.VerifyPinAndGetEmployeeAsync(request.Pin ?? "", ct);
        if (employee is null)
            return Unauthorized(new { message = "Invalid PIN." });

        return Ok(new VerifyPinResponse(
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.EmployeeNumber,
            employee.LocationId,
            employee.LocationName
        ));
    }
}

public record VerifyPinRequest(string? Pin);
public record VerifyPinResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string EmployeeNumber,
    Guid? LocationId,
    string? LocationName);
