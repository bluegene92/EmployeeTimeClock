using TimeClock.Api.Models;

namespace TimeClock.Api.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> GetByEmployeeNumberAsync(string employeeNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task<Guid> CreateAsync(Employee employee, CancellationToken ct = default);
    Task<bool> UpdateAsync(Employee employee, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> SetPinAsync(Guid id, string pin, CancellationToken ct = default);
    Task<bool> ClearPinAsync(Guid id, CancellationToken ct = default);
    Task<Employee?> VerifyPinAndGetEmployeeAsync(string pin, CancellationToken ct = default);
}
