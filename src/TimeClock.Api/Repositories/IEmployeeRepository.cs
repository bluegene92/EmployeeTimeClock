using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetAllAsync(bool? isActive = null, Guid? locationId = null, bool includeDeleted = false, CancellationToken ct = default);
    Task<Guid> CreateAsync(Employee employee, CancellationToken ct = default);
    Task<CreateEmployeeResult> CreateWithGeneratedNumberAsync(Employee employee, CancellationToken ct = default);
    Task<bool> UpdateAsync(Employee employee, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<string?> GetNextEmployeeNumberAsync(CancellationToken ct = default);
    Task<string?> AssignGeneratedNumberAsync(Guid id, CancellationToken ct = default);
    /// <summary>Returns a message when the number is unusable, or null when it is free.</summary>
    Task<string?> CheckEmployeeNumberAsync(string employeeNumber, Guid? excludeId, CancellationToken ct = default);
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

public sealed class CreateEmployeeResult
{
    public bool Success { get; init; }
    public Guid Id { get; init; }
    public string? Error { get; init; }

    public static CreateEmployeeResult Ok(Guid id) => new() { Success = true, Id = id };
    public static CreateEmployeeResult Fail(string error) => new() { Error = error };
}
