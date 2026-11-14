using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

/// <summary>
/// Scans that did not become a punch. Clocks report unknown cards here;
/// punches rejected by /api/timepunches/sync are recorded by the API itself.
/// </summary>
[ApiController]
[Route("api/clock-failures")]
public class ClockFailuresController : ControllerBase
{
    private const int MaxBatch = 100;

    private readonly IClockFailureRepository _failures;

    public ClockFailuresController(IClockFailureRepository failures) => _failures = failures;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClockFailure>>> Get(
        [FromQuery] bool includeResolved = false,
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
    {
        var list = await _failures.GetAsync(includeResolved, Math.Clamp(limit, 1, 1000), ct);
        return Ok(list);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<SyncClockFailuresResponse>> Sync(
        [FromBody] SyncClockFailuresRequest request,
        CancellationToken ct)
    {
        var reported = request.Failures?.ToList() ?? [];
        if (reported.Count > MaxBatch)
            return BadRequest(new { message = $"Send at most {MaxBatch} failures at a time." });

        var count = 0;
        foreach (var item in reported)
        {
            var failure = await BuildAsync(item, ct);
            if (failure is null)
                continue;
            if (await _failures.CreateAsync(failure, ct))
                count++;
        }
        return Ok(new SyncClockFailuresResponse(count));
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult> Resolve(Guid id, CancellationToken ct)
    {
        if (!await _failures.SetResolvedAsync(id, true, ct))
            return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult> Reopen(Guid id, CancellationToken ct)
    {
        if (!await _failures.SetResolvedAsync(id, false, ct))
            return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await _failures.DeleteAsync(id, ct))
            return NotFound();
        return NoContent();
    }

    private async Task<ClockFailure?> BuildAsync(ReportedClockFailure item, CancellationToken ct)
    {
        var reason = item.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            return null;

        var failure = new ClockFailure
        {
            Id = item.Id,
            Reason = reason.Length > 50 ? reason[..50] : reason,
            Message = item.Message?.Trim() ?? "",
            EmployeeId = item.EmployeeId,
            PunchType = item.PunchType,
            OccurredAt = item.OccurredAt,
            LocationId = item.LocationId,
            Source = item.Source?.Trim(),
            DeviceId = item.DeviceId?.Trim()
        };

        var rawCard = item.CardUid?.Trim();
        failure.CardUid = EmployeeRepository.NormalizeCardUid(rawCard)
            ?? (string.IsNullOrEmpty(rawCard) ? null : rawCard.Length > 32 ? rawCard[..32] : rawCard);

        if (failure.Reason == ClockFailureReasons.UnknownCard)
        {
            var owner = failure.CardUid is null ? null : await _failures.FindCardOwnerAsync(failure.CardUid, ct);
            if (owner is { } found)
            {
                // The card was assigned after the clock last downloaded its employee list.
                failure.EmployeeId = found.Id;
                failure.Message = $"Card belongs to {found.Name}, but the clock had not downloaded it yet. No punch was recorded.";
            }
            else
            {
                failure.Message = "Card is not assigned to any employee. No punch was recorded.";
            }
        }
        else if (failure.Message.Length == 0)
        {
            failure.Message = "No punch was recorded.";
        }

        return failure;
    }
}

public record ReportedClockFailure(
    Guid Id,
    string? Reason,
    string? Message = null,
    string? CardUid = null,
    Guid? EmployeeId = null,
    PunchType? PunchType = null,
    DateTime? OccurredAt = null,
    Guid? LocationId = null,
    string? Source = null,
    string? DeviceId = null);

public record SyncClockFailuresRequest(IEnumerable<ReportedClockFailure>? Failures);
public record SyncClockFailuresResponse(int ReceivedCount);
