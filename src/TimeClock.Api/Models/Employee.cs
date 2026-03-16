using System.Text.Json.Serialization;

namespace TimeClock.Api.Models;

public class Employee
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    [JsonIgnore]
    public string? PinHash { get; set; }
    public bool HasPinSet => !string.IsNullOrEmpty(PinHash);
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
