using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetAllAsync(bool? isActive = null, Guid? locationId = null, bool includeDeleted = false, CancellationToken ct = default);
    Task<Guid> CreateAsync(Employee employee, CancellationToken ct = default);
    Task<bool> UpdateAsync(Employee employee, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<PinUpdateResult> SetPinAsync(Guid id, string pin, CancellationToken ct = default);
    Task<bool> ClearPinAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> VerifyPinAndGetEmployeeAsync(string pin, CancellationToken ct = default);
}

public sealed class PinUpdateResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static PinUpdateResult Ok() => new() { Success = true };
    public static PinUpdateResult Fail(string error) => new() { Error = error };
}
