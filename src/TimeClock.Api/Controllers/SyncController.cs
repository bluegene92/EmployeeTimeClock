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
    private readonly INfcCardRepository _cards;

    public SyncController(IEmployeeRepository employees, INfcCardRepository cards)
    {
        _employees = employees;
        _cards = cards;
    }

    /// <summary>
    /// Returns all active employees with their NFC cards for desktop cache.
    /// </summary>
    [HttpGet("employees")]
    public async Task<ActionResult<SyncEmployeesResponse>> GetEmployeesForCache(CancellationToken ct)
    {
        var employees = await _employees.GetAllAsync(isActive: true, ct);
        var result = new List<EmployeeWithCardsDto>();
        foreach (var emp in employees)
        {
            var cards = await _cards.GetByEmployeeIdAsync(emp.Id, ct);
            result.Add(new EmployeeWithCardsDto(
                emp.Id,
                emp.FirstName,
                emp.LastName,
                emp.EmployeeNumber,
                cards.Select(c => new NfcCardDto(c.Id, c.CardUid)).ToList()
            ));
        }
        return Ok(new SyncEmployeesResponse(result));
    }
}

public record EmployeeWithCardsDto(Guid Id, string FirstName, string LastName, string EmployeeNumber, List<NfcCardDto> Cards);
public record NfcCardDto(Guid Id, string CardUid);
public record SyncEmployeesResponse(List<EmployeeWithCardsDto> Employees);
