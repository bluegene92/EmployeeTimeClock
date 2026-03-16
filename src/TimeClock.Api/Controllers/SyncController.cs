using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

/// <summary>
/// Endpoints for the desktop app to sync data (offline cache).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IEmployeeRepository _employees;

    public SyncController(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    /// <summary>
    /// Returns all active employees for desktop cache. Includes HasPinSet so desktop knows who can clock in.
    /// </summary>
    [HttpGet("employees")]
    public async Task<ActionResult<SyncEmployeesResponse>> GetEmployeesForCache(CancellationToken ct)
    {
        var employees = await _employees.GetAllAsync(isActive: true, ct);
        var result = employees.Select(emp => new EmployeeSyncDto(
            emp.Id,
            emp.FirstName,
            emp.LastName,
            emp.EmployeeNumber,
            !string.IsNullOrEmpty(emp.PinHash)
        )).ToList();
        return Ok(new SyncEmployeesResponse(result));
    }
}

public record EmployeeSyncDto(Guid Id, string FirstName, string LastName, string EmployeeNumber, bool HasPinSet);
public record SyncEmployeesResponse(List<EmployeeSyncDto> Employees);
