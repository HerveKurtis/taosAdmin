using AdminTaos.Models;

namespace AdminTaos.Services;

public static class SeedData
{
    // Stable ids so pages/tests can reference seed entities.
    public const string RoleServer = "role-server";
    public const string RoleHost   = "role-host";
    public const string MgrId      = "acc-manager";
    public const string EmpActiveServer = "acc-emp-server";
    public const string EmpActiveHost   = "acc-emp-host";
    public const string EmpPending      = "acc-emp-pending";
    public const string EmpRejected     = "acc-emp-rejected";

    public static (List<Account>, List<JobRole>, List<ServiceEvent>, List<Assignment>, List<Timesheet>) Build()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var roles = new List<JobRole>
        {
            new() { Id = RoleServer, Name = "Serveur", Color = "#E7C76B" },
            new() { Id = RoleHost,   Name = "Hôtesse", Color = "#C2A14D" },
        };

        var accounts = new List<Account>
        {
            new() { Id = MgrId, FullName = "Hervé T.", Email = "manager@taos.be",
                    Type = AccountType.Manager, Status = AccountStatus.Active },
            new() { Id = EmpActiveServer, FullName = "Marc D.", Email = "marc@taos.be",
                    Type = AccountType.Employee, Status = AccountStatus.Active,
                    JobRoleIds = new() { RoleServer } },
            new() { Id = EmpActiveHost, FullName = "Sarah K.", Email = "sarah@taos.be",
                    Type = AccountType.Employee, Status = AccountStatus.Active,
                    JobRoleIds = new() { RoleHost } },
            new() { Id = EmpPending, FullName = "Léa B.", Email = "lea@taos.be",
                    Type = AccountType.Employee, Status = AccountStatus.Pending,
                    JobRoleIds = new() { RoleServer } },
            new() { Id = EmpRejected, FullName = "Tom V.", Email = "tom@taos.be",
                    Type = AccountType.Employee, Status = AccountStatus.Rejected,
                    JobRoleIds = new() { RoleHost } },
        };

        var gala = new ServiceEvent {
            Id = "evt-gala", Name = "Gala d'entreprise", Venue = "Hôtel Plaza",
            Address = "Bd de Waterloo 1, Bruxelles", Date = today.AddDays(2),
            MeetingTime = new TimeOnly(17,0), ExpectedEndTime = new TimeOnly(23,0),
            DressCode = "Noir élégant", Instructions = "Arriver 15 min avant.",
            OnSiteContact = "Julie — 0470 00 00 00", IsOpenForSignup = false,
            Status = EventStatus.Upcoming,
            RoleNeeds = new() {
                new() { JobRoleId = RoleServer, CountNeeded = 4, HourlyRate = 14m },
                new() { JobRoleId = RoleHost,   CountNeeded = 2, HourlyRate = 15m },
            }
        };
        var cocktail = new ServiceEvent {
            Id = "evt-cocktail", Name = "Cocktail privé", Venue = "Villa Empain",
            Address = "Av. Franklin Roosevelt 67, Bruxelles", Date = today.AddDays(8),
            MeetingTime = new TimeOnly(18,30), ExpectedEndTime = new TimeOnly(23,30),
            DressCode = "Tenue de ville", Instructions = "", OnSiteContact = "",
            IsOpenForSignup = true, Status = EventStatus.Upcoming,
            RoleNeeds = new() {
                new() { JobRoleId = RoleServer, CountNeeded = 3, HourlyRate = 14m },
            }
        };
        var todayEvt = new ServiceEvent {
            Id = "evt-today", Name = "Déjeuner d'affaires", Venue = "Steigenberger",
            Address = "Av. Louise 71, Bruxelles", Date = today,
            MeetingTime = new TimeOnly(11,0), ExpectedEndTime = new TimeOnly(16,0),
            DressCode = "Noir élégant", Instructions = "", OnSiteContact = "",
            IsOpenForSignup = false, Status = EventStatus.InProgress,
            RoleNeeds = new() {
                new() { JobRoleId = RoleServer, CountNeeded = 1, HourlyRate = 14m },
                new() { JobRoleId = RoleHost,   CountNeeded = 1, HourlyRate = 15m },
            }
        };
        var pastEvt = new ServiceEvent {
            Id = "evt-past", Name = "Mariage Dupont", Venue = "Château de la Hulpe",
            Address = "Chaussée de Bruxelles 111", Date = today.AddDays(-6),
            MeetingTime = new TimeOnly(15,0), ExpectedEndTime = new TimeOnly(23,0),
            DressCode = "Noir élégant", Instructions = "", OnSiteContact = "",
            IsOpenForSignup = false, Status = EventStatus.Past,
            RoleNeeds = new() {
                new() { JobRoleId = RoleServer, CountNeeded = 2, HourlyRate = 14m },
            }
        };
        var pastEvt2 = new ServiceEvent {
            Id = "evt-past2", Name = "Soirée corporate", Venue = "BOZAR",
            Address = "Rue Ravenstein 23, Bruxelles", Date = today.AddDays(-12),
            MeetingTime = new TimeOnly(18,0), ExpectedEndTime = new TimeOnly(23,30),
            DressCode = "Noir élégant", Instructions = "", OnSiteContact = "",
            IsOpenForSignup = false, Status = EventStatus.Past,
            RoleNeeds = new() {
                new() { JobRoleId = RoleServer, CountNeeded = 2, HourlyRate = 14m },
                new() { JobRoleId = RoleHost,   CountNeeded = 1, HourlyRate = 15m },
            }
        };
        var pastEvt3 = new ServiceEvent {
            Id = "evt-past3", Name = "Brunch dominical", Venue = "The Hoxton",
            Address = "Square Victoria Régina 1, Bruxelles", Date = today.AddDays(-3),
            MeetingTime = new TimeOnly(9,30), ExpectedEndTime = new TimeOnly(15,0),
            DressCode = "Tenue de ville", Instructions = "", OnSiteContact = "",
            IsOpenForSignup = false, Status = EventStatus.Past,
            RoleNeeds = new() {
                new() { JobRoleId = RoleServer, CountNeeded = 1, HourlyRate = 14m },
            }
        };
        var events = new List<ServiceEvent> { gala, cocktail, todayEvt, pastEvt, pastEvt2, pastEvt3 };

        var aGalaServer = new Assignment { Id = "asg-1", EventId = gala.Id,
            AccountId = EmpActiveServer, JobRoleId = RoleServer,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var aCocktailReq = new Assignment { Id = "asg-2", EventId = cocktail.Id,
            AccountId = EmpActiveServer, JobRoleId = RoleServer,
            Source = AssignmentSource.SelfRequest, Status = AssignmentStatus.PendingApproval };
        var aTodayHost = new Assignment { Id = "asg-3", EventId = todayEvt.Id,
            AccountId = EmpActiveHost, JobRoleId = RoleHost,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var aPastServer = new Assignment { Id = "asg-4", EventId = pastEvt.Id,
            AccountId = EmpActiveServer, JobRoleId = RoleServer,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var aP2Host = new Assignment { Id = "asg-5", EventId = pastEvt2.Id,
            AccountId = EmpActiveHost, JobRoleId = RoleHost,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var aP2Server = new Assignment { Id = "asg-6", EventId = pastEvt2.Id,
            AccountId = EmpActiveServer, JobRoleId = RoleServer,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var aP3Server = new Assignment { Id = "asg-7", EventId = pastEvt3.Id,
            AccountId = EmpActiveServer, JobRoleId = RoleServer,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var aTodayServer = new Assignment { Id = "asg-8", EventId = todayEvt.Id,
            AccountId = EmpActiveServer, JobRoleId = RoleServer,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed };
        var assignments = new List<Assignment> { aGalaServer, aCocktailReq, aTodayHost, aPastServer,
            aP2Host, aP2Server, aP3Server, aTodayServer };

        var timesheets = new List<Timesheet>
        {
            // Future gala — not started
            new() { Id = "ts-1", AssignmentId = aGalaServer.Id, Status = TimesheetStatus.NotStarted },
            // Past — sent, awaiting manager validation
            new() { Id = "ts-3", AssignmentId = aPastServer.Id,
                    StartedAt = DateTime.Today.AddDays(-6).AddHours(15),
                    EndedAt   = DateTime.Today.AddDays(-6).AddHours(23).AddMinutes(12),
                    SentAt = DateTime.Today.AddDays(-5), Status = TimesheetStatus.Sent },
            // Past event 2 — validated by manager (with adjusted times)
            new() { Id = "ts-4", AssignmentId = aP2Host.Id,
                    StartedAt = DateTime.Today.AddDays(-12).AddHours(18).AddMinutes(3),
                    EndedAt   = DateTime.Today.AddDays(-12).AddHours(23).AddMinutes(40),
                    ManagerAdjustedStart = DateTime.Today.AddDays(-12).AddHours(18),
                    ManagerAdjustedEnd   = DateTime.Today.AddDays(-12).AddHours(23).AddMinutes(30),
                    SentAt = DateTime.Today.AddDays(-11),
                    ValidatedAt = DateTime.Today.AddDays(-10), Status = TimesheetStatus.Validated },
            // Past event 2 — rejected by manager (with reason)
            new() { Id = "ts-5", AssignmentId = aP2Server.Id,
                    StartedAt = DateTime.Today.AddDays(-12).AddHours(18),
                    EndedAt   = DateTime.Today.AddDays(-12).AddHours(22),
                    SentAt = DateTime.Today.AddDays(-11),
                    RejectionReason = "Heures de fin incohérentes, merci de corriger.",
                    Status = TimesheetStatus.Rejected },
            // Past event 3 — finished, to send (not yet sent)
            new() { Id = "ts-6", AssignmentId = aP3Server.Id,
                    StartedAt = DateTime.Today.AddDays(-3).AddHours(9).AddMinutes(28),
                    EndedAt   = DateTime.Today.AddDays(-3).AddHours(15).AddMinutes(10),
                    Status = TimesheetStatus.ToSend },
        };

        return (accounts, roles, events, assignments, timesheets);
    }
}
