using Microsoft.AspNetCore.Mvc;
using TimeClock.Api.Models;
using TimeClock.Api.Repositories;

namespace TimeClock.Api.Controllers;

[ApiController]
[Route("api/employees/{employeeId:guid}/nfc-cards")]
public class NfcCardsController : ControllerBase
{
    private readonly INfcCardRepository _cards;
    private readonly IEmployeeRepository _employees;

    public NfcCardsController(INfcCardRepository cards, IEmployeeRepository employees)
    {
        _cards = cards;
        _employees = employees;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NfcCard>>> GetByEmployee(Guid employeeId, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return NotFound();
        var list = await _cards.GetByEmployeeIdAsync(employeeId, ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<NfcCard>> Register(Guid employeeId, [FromBody] RegisterNfcCardRequest request, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return NotFound();

        var existing = await _cards.GetByCardUidAsync(request.CardUid, ct);
        if (existing is not null)
            return Conflict("Card UID is already registered to another employee.");

        var card = new NfcCard
        {
            CardUid = request.CardUid,
            EmployeeId = employeeId,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };
        var id = await _cards.CreateAsync(card, ct);
        var created = await _cards.GetByIdAsync(id, ct);
        return Created($"/api/employees/{employeeId}/nfc-cards/{id}", created);
    }

    [HttpDelete("{cardId:guid}")]
    public async Task<ActionResult> Unlink(Guid employeeId, Guid cardId, CancellationToken ct)
    {
        var card = await _cards.GetByIdAsync(cardId, ct);
        if (card is null || card.EmployeeId != employeeId)
            return NotFound();
        await _cards.DeleteAsync(cardId, ct);
        return NoContent();
    }
}

public record RegisterNfcCardRequest(string CardUid);
