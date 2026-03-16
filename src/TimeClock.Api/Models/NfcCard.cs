namespace TimeClock.Api.Models;

public class NfcCard
{
    public Guid Id { get; set; }
    public string CardUid { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public bool IsActive { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
