using Microsoft.AspNetCore.Mvc;
using Npgsql;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationRepository _locations;

    public LocationsController(ILocationRepository locations) => _locations = locations;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Location>>> GetAll([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _locations.GetAllAsync(isActive, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Location>> GetById(Guid id, CancellationToken ct)
    {
        var location = await _locations.GetByIdAsync(id, ct);
        if (location is null)
            return NotFound();
        return Ok(location);
    }

    [HttpPost]
    public async Task<ActionResult<Location>> Create([FromBody] SaveLocationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Location name is required." });

        try
        {
            var id = await _locations.CreateAsync(new Location
            {
                Name = request.Name.Trim(),
                IsActive = request.IsActive
            }, ct);
            var created = await _locations.GetByIdAsync(id, ct);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return BadRequest(new { message = "A location with that name already exists." });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] SaveLocationRequest request, CancellationToken ct)
    {
        var existing = await _locations.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Location name is required." });

        existing.Name = request.Name.Trim();
        existing.IsActive = request.IsActive;
        try
        {
            await _locations.UpdateAsync(existing, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return BadRequest(new { message = "A location with that name already exists." });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var existing = await _locations.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();
        if (await _locations.IsInUseAsync(id, ct))
            return Conflict(new { message = "This location is assigned to employees or punches. Mark it inactive instead." });

        await _locations.DeleteAsync(id, ct);
        return NoContent();
    }
}

public record SaveLocationRequest(string Name, bool IsActive = true);
