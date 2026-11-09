using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

/// <summary>
/// Endpoints for the clock app to cache employees and their home location.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IEmployeeRepository _employees;

    public SyncController(IEmployeeRepository employees) => _employees = employees;

    [HttpGet("employees")]
    public async Task<ActionResult<SyncEmployeesResponse>> GetEmployeesForCache(CancellationToken ct)
    {
        var employees = await _employees.GetAllAsync(isActive: true, ct: ct);
        var result = employees.Select(emp => new EmployeeSyncDto(
            emp.Id,
            emp.FirstName,
            emp.LastName,
            emp.EmployeeNumber,
            emp.HasPinSet,
            emp.LocationId,
            emp.LocationName
        )).ToList();
        return Ok(new SyncEmployeesResponse(result));
    }
}

public record EmployeeSyncDto(
    Guid Id,
    string FirstName,
    string LastName,
    string EmployeeNumber,
    bool HasPinSet,
    Guid? LocationId,
    string? LocationName);

public record SyncEmployeesResponse(List<EmployeeSyncDto> Employees);
