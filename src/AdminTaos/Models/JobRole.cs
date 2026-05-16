namespace AdminTaos.Models;

public class JobRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#E7C76B";
}
