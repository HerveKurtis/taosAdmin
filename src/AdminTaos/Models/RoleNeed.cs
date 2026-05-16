namespace AdminTaos.Models;

public class RoleNeed
{
    public string JobRoleId { get; set; } = "";
    public int CountNeeded { get; set; } = 1;
    public decimal HourlyRate { get; set; }
}
