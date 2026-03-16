using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimePunchesController : ControllerBase
{
    private readonly ITimePunchRepository _punches;

    public TimePunchesController(ITimePunchRepository punches)
    {
        _punches = punches;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimePunch>>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? employeeId,
        CancellationToken ct)
    {
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = to ?? fromDate.AddDays(1);
        var list = await _punches.GetByDateRangeAsync(fromDate, toDate, employeeId, ct);
        return Ok(list);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncPunchesResponse>> Sync([FromBody] SyncPunchesRequest request, CancellationToken ct)
    {
        var count = await _punches.CreateBatchAsync(request.Punches, ct);
        return Ok(new SyncPunchesResponse(count));
    }
}

public record SyncPunchesRequest(IEnumerable<TimePunch> Punches);
public record SyncPunchesResponse(int SyncedCount);
