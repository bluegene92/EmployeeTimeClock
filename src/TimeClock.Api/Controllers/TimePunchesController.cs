using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Data;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimePunchesController : ControllerBase
{
    private readonly ITimePunchRepository _punches;
    private readonly IEmployeeRepository _employees;
    private readonly ILocationRepository _locations;

    public TimePunchesController(
        ITimePunchRepository punches,
        IEmployeeRepository employees,
        ILocationRepository locations)
    {
        _punches = punches;
        _employees = employees;
        _locations = locations;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimePunch>>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? locationId,
        CancellationToken ct)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? fromDate.Date.AddDays(1);
        var list = await _punches.GetByDateRangeAsync(fromDate, toDate, employeeId, locationId, ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<TimePunch>> Create([FromBody] CreateTimePunchRequest request, CancellationToken ct)
    {
        var error = await ValidateAsync(request.EmployeeId, request.LocationId, ct);
        if (error is not null)
            return BadRequest(new { message = error });

        var punch = new TimePunch
        {
            EmployeeId = request.EmployeeId,
            LocationId = request.LocationId,
            PunchType = request.PunchType,
            PunchedAt = WallTime.AsUnspecified(request.PunchedAt),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Web" : request.Source.Trim(),
            DeviceId = request.DeviceId
        };
        var id = await _punches.CreateAsync(punch, ct);
        var created = await _punches.GetByIdAsync(id, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TimePunch>> GetById(Guid id, CancellationToken ct)
    {
        var punch = await _punches.GetByIdAsync(id, ct);
        if (punch is null)
            return NotFound();
        return Ok(punch);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateTimePunchRequest request, CancellationToken ct)
    {
        var existing = await _punches.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        var error = await ValidateAsync(existing.EmployeeId, request.LocationId, ct);
        if (error is not null)
            return BadRequest(new { message = error });

        existing.PunchType = request.PunchType;
        existing.PunchedAt = WallTime.AsUnspecified(request.PunchedAt);
        existing.LocationId = request.LocationId;
        if (!string.IsNullOrWhiteSpace(request.Source))
            existing.Source = request.Source.Trim();

        await _punches.UpdateAsync(existing, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var found = await _punches.DeleteAsync(id, ct);
        if (!found)
            return NotFound();
        return NoContent();
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncPunchesResponse>> Sync([FromBody] SyncPunchesRequest request, CancellationToken ct)
    {
        var punches = request.Punches?.ToList() ?? [];
        foreach (var punch in punches)
        {
            var error = await ValidateAsync(punch.EmployeeId, punch.LocationId, ct);
            if (error is not null)
                return BadRequest(new { message = error });
            punch.PunchedAt = WallTime.AsUnspecified(punch.PunchedAt);
        }

        var count = await _punches.CreateBatchAsync(punches, ct);
        return Ok(new SyncPunchesResponse(count));
    }

    private async Task<string?> ValidateAsync(Guid employeeId, Guid? locationId, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null || employee.IsDeleted)
            return "Employee not found.";
        if (locationId is Guid location)
        {
            var found = await _locations.GetByIdAsync(location, ct);
            if (found is null)
                return "Location not found.";
        }
        return null;
    }
}

public record CreateTimePunchRequest(
    Guid EmployeeId,
    PunchType PunchType,
    DateTime PunchedAt,
    Guid? LocationId = null,
    string? Source = null,
    string? DeviceId = null);

public record UpdateTimePunchRequest(
    PunchType PunchType,
    DateTime PunchedAt,
    Guid? LocationId = null,
    string? Source = null);

public record SyncPunchesRequest(IEnumerable<TimePunch>? Punches);
public record SyncPunchesResponse(int SyncedCount);
