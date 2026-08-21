namespace AdminTaos.Models;

public class ServiceEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Venue { get; set; } = "";
    public string Address { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly MeetingTime { get; set; }
    public TimeOnly ExpectedEndTime { get; set; }
    public string DressCode { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string OnSiteContact { get; set; } = "";
    /// <summary>Compte désigné responsable pour cet event. Remplace OnSiteContact, conservé en repli.</summary>
    public string? ResponsableAccountId { get; set; }
    public List<RoleNeed> RoleNeeds { get; set; } = new();
    public bool IsOpenForSignup { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Upcoming;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
