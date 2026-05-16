namespace AdminTaos.Models;

public class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public AccountType Type { get; set; } = AccountType.Employee;
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    public List<string> JobRoleIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
