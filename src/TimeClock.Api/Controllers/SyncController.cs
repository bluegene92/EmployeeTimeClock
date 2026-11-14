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
    private readonly IClockDeviceRepository _devices;

    public SyncController(IEmployeeRepository employees, IClockDeviceRepository devices)
    {
        _employees = employees;
        _devices = devices;
    }

    [HttpGet("employees")]
    public async Task<ActionResult<SyncEmployeesResponse>> GetEmployeesForCache(CancellationToken ct)
    {
        // Read before the list: if the list changes in between, the clock sees a newer
        // version on its next check-in and downloads again.
        var version = await _devices.GetEmployeeListVersionAsync(ct);
        var employees = await _employees.GetAllAsync(isActive: true, ct: ct);
        var result = employees.Select(emp => new EmployeeSyncDto(
            emp.Id,
            emp.FirstName,
            emp.LastName,
            emp.EmployeeNumber,
            emp.HasPinSet,
            emp.CardUid,
            emp.LocationId,
            emp.LocationName
        )).ToList();
        return Ok(new SyncEmployeesResponse(result, version));
    }
}

public record EmployeeSyncDto(
    Guid Id,
    string FirstName,
    string LastName,
    string EmployeeNumber,
    bool HasPinSet,
    string? CardUid,
    Guid? LocationId,
    string? LocationName);

public record SyncEmployeesResponse(List<EmployeeSyncDto> Employees, string Version);
