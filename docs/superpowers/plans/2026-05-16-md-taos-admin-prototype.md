# MD TAOS ADMIN — Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a clickable Blazor WebAssembly PWA prototype for TAOS staff management (events, assignments, time-tracking, timesheet validation) with seeded in-memory data behind a swappable data layer.

**Architecture:** Standalone Blazor WASM PWA (.NET 10). All data flows through `IDataService`; the prototype uses `InMemoryDataService` seeded by `SeedData`. A `FirebaseDataService` can replace it later with zero UI changes. Mock auth via `AuthState` + localStorage. Two role shells (Manager / Employee) with a `NavigationGuard` enforcing route access. Repeated UI is factored into reusable components so pages stay thin.

**Tech Stack:** .NET 10, Blazor WebAssembly (PWA template), `Blazored.LocalStorage`, xUnit + bUnit for tests. Design system "Noir & luxe" in `app.css`. Playfair Display + Inter fonts bundled locally.

**Testing strategy (deliberate):** TDD with xUnit for all logic — model computations, `InMemoryDataService` CRUD + state transitions, `NavigationGuard.Resolve`, `AuthState`. bUnit smoke tests for the three interactive components with real logic (`Stepper`, quick-login list, pointage start/stop). Presentational pages are verified by `dotnet build` + a manual click-through checklist (Task 30), matching the spec's definition of done — brittle markup snapshot tests would add no value for a mock UI.

**Spec:** `docs/superpowers/specs/2026-05-16-md-taos-admin-design.md`

---

## File Structure

```
AdminTaos.sln
src/AdminTaos/
  Models/        Account.cs JobRole.cs RoleNeed.cs ServiceEvent.cs Assignment.cs Timesheet.cs Enums.cs
  Services/      IDataService.cs InMemoryDataService.cs SeedData.cs AuthState.cs NavigationGuard.cs
  Layout/        AuthLayout.razor ManagerLayout.razor EmployeeLayout.razor
  Components/    TopBar.razor BottomNav.razor Card.razor CtaButton.razor Stepper.razor
                 ToggleSwitch.razor StatusPill.razor Chrono.razor EmptyState.razor SectionLabel.razor
  Pages/Auth/    Splash.razor Login.razor Register.razor AccountPending.razor
  Pages/Manager/ MHome.razor MEvents.razor MEventDetail.razor MEventEdit.razor MAssignStaff.razor
                 MTeam.razor MEmployeeDetail.razor MRoles.razor MTimesheets.razor MTimesheetDetail.razor MProfile.razor
  Pages/Employee/ EHome.razor EEvents.razor EEventDetail.razor EActive.razor ERecap.razor
                  EHours.razor ETimesheetDetail.razor EProfile.razor
  App.razor  _Imports.razor  Program.cs
  wwwroot/   index.html manifest.webmanifest app.css icons/ fonts/ service-worker(.published).js
tests/AdminTaos.Tests/
  ModelTests.cs DataServiceTests.cs NavigationGuardTests.cs AuthStateTests.cs ComponentTests.cs
```

Each file has one responsibility. Pages are thin; logic lives in `Services/`; repeated markup lives in `Components/`.

---

## Phase 1 — Project skeleton, data layer, design system, PWA

### Task 1: Solution + projects

**Files:**
- Create: `AdminTaos.sln`, `src/AdminTaos/` (app), `tests/AdminTaos.Tests/` (tests)

- [ ] **Step 1: Create app project**

Run:
```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
dotnet new blazorwasm --pwa -o src/AdminTaos -n AdminTaos -f net10.0
```
Expected: project created under `src/AdminTaos`.

- [ ] **Step 2: Create test project + solution**

Run:
```bash
dotnet new xunit -o tests/AdminTaos.Tests -n AdminTaos.Tests -f net10.0
dotnet add tests/AdminTaos.Tests package bunit
dotnet add src/AdminTaos package Blazored.LocalStorage
dotnet add tests/AdminTaos.Tests reference src/AdminTaos/AdminTaos.csproj
dotnet new sln -n AdminTaos
dotnet sln add src/AdminTaos/AdminTaos.csproj tests/AdminTaos.Tests/AdminTaos.Tests.csproj
```

- [ ] **Step 3: Verify build + test run**

Run: `dotnet build`
Expected: Build succeeded.
Run: `dotnet test`
Expected: Passed (default xUnit sample test) — if the template added none, 0 tests is acceptable.

- [ ] **Step 4: Strip template demo pages**

Delete: `src/AdminTaos/Pages/Counter.razor`, `src/AdminTaos/Pages/Weather.razor`, `src/AdminTaos/Pages/Home.razor`, `src/AdminTaos/Layout/NavMenu.razor`, `src/AdminTaos/Layout/MainLayout.razor`, `src/AdminTaos/wwwroot/sample-data/` (if present), and the `SurveyPrompt.razor` component if present.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore: scaffold Blazor WASM PWA solution + test project"
```

---

### Task 2: Enums

**Files:**
- Create: `src/AdminTaos/Models/Enums.cs`
- Test: `tests/AdminTaos.Tests/ModelTests.cs`

- [ ] **Step 1: Write the failing test**

In `tests/AdminTaos.Tests/ModelTests.cs`:
```csharp
using AdminTaos.Models;
using Xunit;

namespace AdminTaos.Tests;

public class ModelTests
{
    [Fact]
    public void Enums_have_expected_members()
    {
        Assert.Equal(2, System.Enum.GetValues<AccountType>().Length);
        Assert.True(System.Enum.IsDefined(AccountStatus.Pending));
        Assert.True(System.Enum.IsDefined(TimesheetStatus.ToSend));
        Assert.True(System.Enum.IsDefined(AssignmentStatus.PendingApproval));
        Assert.True(System.Enum.IsDefined(EventStatus.InProgress));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter Enums_have_expected_members`
Expected: FAIL — `AccountType` does not exist.

- [ ] **Step 3: Create enums**

`src/AdminTaos/Models/Enums.cs`:
```csharp
namespace AdminTaos.Models;

public enum AccountType { Manager, Employee }
public enum AccountStatus { Pending, Active, Rejected }
public enum EventStatus { Upcoming, InProgress, Past }
public enum AssignmentSource { AssignedByManager, SelfRequest }
public enum AssignmentStatus { PendingApproval, Confirmed, Rejected }
public enum TimesheetStatus { NotStarted, InProgress, ToSend, Sent, Validated, Rejected }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter Enums_have_expected_members`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: domain enums"
```

---

### Task 3: Entity models + Timesheet.Duration

**Files:**
- Create: `src/AdminTaos/Models/{Account,JobRole,RoleNeed,ServiceEvent,Assignment,Timesheet}.cs`
- Test: `tests/AdminTaos.Tests/ModelTests.cs`

- [ ] **Step 1: Add failing test for duration**

Append to `ModelTests.cs`:
```csharp
[Fact]
public void Timesheet_duration_uses_raw_times()
{
    var t = new Timesheet {
        StartedAt = new System.DateTime(2026,5,18,17,0,0),
        EndedAt   = new System.DateTime(2026,5,18,23,12,0)
    };
    Assert.Equal(System.TimeSpan.FromMinutes(372), t.Duration);
}

[Fact]
public void Timesheet_duration_prefers_manager_adjustments()
{
    var t = new Timesheet {
        StartedAt = new System.DateTime(2026,5,18,17,0,0),
        EndedAt   = new System.DateTime(2026,5,18,23,0,0),
        ManagerAdjustedStart = new System.DateTime(2026,5,18,17,2,0),
        ManagerAdjustedEnd   = new System.DateTime(2026,5,18,23,14,0)
    };
    Assert.Equal(System.TimeSpan.FromMinutes(372), t.Duration);
}

[Fact]
public void Timesheet_duration_null_when_not_finished()
{
    Assert.Null(new Timesheet { StartedAt = System.DateTime.Now }.Duration);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter Timesheet_duration_uses_raw_times`
Expected: FAIL — `Timesheet` does not exist.

- [ ] **Step 3: Create models**

`src/AdminTaos/Models/Account.cs`:
```csharp
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
```

`src/AdminTaos/Models/JobRole.cs`:
```csharp
namespace AdminTaos.Models;

public class JobRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#E7C76B";
}
```

`src/AdminTaos/Models/RoleNeed.cs`:
```csharp
namespace AdminTaos.Models;

public class RoleNeed
{
    public string JobRoleId { get; set; } = "";
    public int CountNeeded { get; set; } = 1;
    public decimal HourlyRate { get; set; }
}
```

`src/AdminTaos/Models/ServiceEvent.cs`:
```csharp
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
    public List<RoleNeed> RoleNeeds { get; set; } = new();
    public bool IsOpenForSignup { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Upcoming;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

`src/AdminTaos/Models/Assignment.cs`:
```csharp
namespace AdminTaos.Models;

public class Assignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string JobRoleId { get; set; } = "";
    public AssignmentSource Source { get; set; } = AssignmentSource.AssignedByManager;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Confirmed;
}
```

`src/AdminTaos/Models/Timesheet.cs`:
```csharp
namespace AdminTaos.Models;

public class Timesheet
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AssignmentId { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimesheetStatus Status { get; set; } = TimesheetStatus.NotStarted;
    public DateTime? ManagerAdjustedStart { get; set; }
    public DateTime? ManagerAdjustedEnd { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ValidatedAt { get; set; }

    public DateTime? EffectiveStart => ManagerAdjustedStart ?? StartedAt;
    public DateTime? EffectiveEnd => ManagerAdjustedEnd ?? EndedAt;

    public TimeSpan? Duration =>
        EffectiveStart is { } s && EffectiveEnd is { } e ? e - s : null;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ModelTests`
Expected: PASS (all duration tests green).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: entity models with Timesheet.Duration"
```

---

### Task 4: IDataService interface

**Files:**
- Create: `src/AdminTaos/Services/IDataService.cs`

- [ ] **Step 1: Create the interface**

`src/AdminTaos/Services/IDataService.cs`:
```csharp
using AdminTaos.Models;

namespace AdminTaos.Services;

public interface IDataService
{
    // Accounts
    Task<List<Account>> GetAccountsAsync();
    Task<Account?> GetAccountAsync(string id);
    Task<Account?> GetAccountByEmailAsync(string email);
    Task<Account> CreateAccountAsync(Account a);
    Task UpdateAccountAsync(Account a);

    // Job roles
    Task<List<JobRole>> GetJobRolesAsync();
    Task<JobRole> CreateJobRoleAsync(JobRole r);
    Task UpdateJobRoleAsync(JobRole r);
    Task DeleteJobRoleAsync(string id);

    // Events
    Task<List<ServiceEvent>> GetEventsAsync();
    Task<ServiceEvent?> GetEventAsync(string id);
    Task<ServiceEvent> CreateEventAsync(ServiceEvent e);
    Task UpdateEventAsync(ServiceEvent e);

    // Assignments
    Task<List<Assignment>> GetAssignmentsAsync();
    Task<List<Assignment>> GetAssignmentsForEventAsync(string eventId);
    Task<List<Assignment>> GetAssignmentsForAccountAsync(string accountId);
    Task<Assignment> CreateAssignmentAsync(Assignment a);
    Task UpdateAssignmentAsync(Assignment a);

    // Timesheets
    Task<List<Timesheet>> GetTimesheetsAsync();
    Task<Timesheet?> GetTimesheetAsync(string id);
    Task<Timesheet?> GetTimesheetForAssignmentAsync(string assignmentId);
    Task<Timesheet> CreateTimesheetAsync(Timesheet t);
    Task UpdateTimesheetAsync(Timesheet t);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: IDataService abstraction"
```

---

### Task 5: SeedData

**Files:**
- Create: `src/AdminTaos/Services/SeedData.cs`
- Test: covered indirectly in Task 6 (`DataServiceTests`)

- [ ] **Step 1: Create deterministic seed**

`src/AdminTaos/Services/SeedData.cs`:
```csharp
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
                new() { JobRoleId = RoleHost, CountNeeded = 1, HourlyRate = 15m },
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
        var events = new List<ServiceEvent> { gala, cocktail, todayEvt, pastEvt };

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
        var assignments = new List<Assignment> { aGalaServer, aCocktailReq, aTodayHost, aPastServer };

        var timesheets = new List<Timesheet>
        {
            // Future gala — not started
            new() { Id = "ts-1", AssignmentId = aGalaServer.Id, Status = TimesheetStatus.NotStarted },
            // Today — in progress (started 30 min ago)
            new() { Id = "ts-2", AssignmentId = aTodayHost.Id,
                    StartedAt = DateTime.Now.AddMinutes(-30), Status = TimesheetStatus.InProgress },
            // Past — sent, awaiting manager validation
            new() { Id = "ts-3", AssignmentId = aPastServer.Id,
                    StartedAt = DateTime.Today.AddDays(-6).AddHours(15),
                    EndedAt   = DateTime.Today.AddDays(-6).AddHours(23).AddMinutes(12),
                    SentAt = DateTime.Today.AddDays(-5), Status = TimesheetStatus.Sent },
        };

        return (accounts, roles, events, assignments, timesheets);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: deterministic seed data covering all states"
```

---

### Task 6: InMemoryDataService (TDD)

**Files:**
- Create: `src/AdminTaos/Services/InMemoryDataService.cs`
- Test: `tests/AdminTaos.Tests/DataServiceTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AdminTaos.Tests/DataServiceTests.cs`:
```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class DataServiceTests
{
    static InMemoryDataService New() => new();

    [Fact]
    public async Task Seeds_all_collections()
    {
        var db = New();
        Assert.Equal(5, (await db.GetAccountsAsync()).Count);
        Assert.Equal(2, (await db.GetJobRolesAsync()).Count);
        Assert.Equal(4, (await db.GetEventsAsync()).Count);
        Assert.Equal(4, (await db.GetAssignmentsAsync()).Count);
        Assert.Equal(3, (await db.GetTimesheetsAsync()).Count);
    }

    [Fact]
    public async Task GetAccountByEmail_is_case_insensitive()
    {
        var db = New();
        var a = await db.GetAccountByEmailAsync("MANAGER@taos.be");
        Assert.NotNull(a);
        Assert.Equal(AccountType.Manager, a!.Type);
    }

    [Fact]
    public async Task CreateAccount_then_update_persists()
    {
        var db = New();
        var created = await db.CreateAccountAsync(new Account { FullName = "X", Email = "x@taos.be" });
        created.Status = AccountStatus.Active;
        await db.UpdateAccountAsync(created);
        Assert.Equal(AccountStatus.Active, (await db.GetAccountAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task DeleteJobRole_removes_it()
    {
        var db = New();
        await db.DeleteJobRoleAsync(SeedData.RoleHost);
        Assert.Single(await db.GetJobRolesAsync());
    }

    [Fact]
    public async Task Assignments_filtered_by_event_and_account()
    {
        var db = New();
        Assert.NotEmpty(await db.GetAssignmentsForEventAsync("evt-gala"));
        Assert.NotEmpty(await db.GetAssignmentsForAccountAsync(SeedData.EmpActiveServer));
    }

    [Fact]
    public async Task GetTimesheetForAssignment_returns_match()
    {
        var db = New();
        var ts = await db.GetTimesheetForAssignmentAsync("asg-1");
        Assert.Equal("ts-1", ts!.Id);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter DataServiceTests`
Expected: FAIL — `InMemoryDataService` does not exist.

- [ ] **Step 3: Implement the service**

`src/AdminTaos/Services/InMemoryDataService.cs`:
```csharp
using AdminTaos.Models;

namespace AdminTaos.Services;

public class InMemoryDataService : IDataService
{
    private readonly List<Account> _accounts;
    private readonly List<JobRole> _roles;
    private readonly List<ServiceEvent> _events;
    private readonly List<Assignment> _assignments;
    private readonly List<Timesheet> _timesheets;

    public InMemoryDataService()
    {
        (_accounts, _roles, _events, _assignments, _timesheets) = SeedData.Build();
    }

    private static Task<T> Done<T>(T v) => Task.FromResult(v);

    public Task<List<Account>> GetAccountsAsync() => Done(_accounts.ToList());
    public Task<Account?> GetAccountAsync(string id) => Done(_accounts.FirstOrDefault(a => a.Id == id));
    public Task<Account?> GetAccountByEmailAsync(string email) =>
        Done(_accounts.FirstOrDefault(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase)));
    public Task<Account> CreateAccountAsync(Account a) { _accounts.Add(a); return Done(a); }
    public Task UpdateAccountAsync(Account a)
    {
        var i = _accounts.FindIndex(x => x.Id == a.Id);
        if (i >= 0) _accounts[i] = a;
        return Task.CompletedTask;
    }

    public Task<List<JobRole>> GetJobRolesAsync() => Done(_roles.ToList());
    public Task<JobRole> CreateJobRoleAsync(JobRole r) { _roles.Add(r); return Done(r); }
    public Task UpdateJobRoleAsync(JobRole r)
    {
        var i = _roles.FindIndex(x => x.Id == r.Id);
        if (i >= 0) _roles[i] = r;
        return Task.CompletedTask;
    }
    public Task DeleteJobRoleAsync(string id) { _roles.RemoveAll(x => x.Id == id); return Task.CompletedTask; }

    public Task<List<ServiceEvent>> GetEventsAsync() => Done(_events.OrderBy(e => e.Date).ToList());
    public Task<ServiceEvent?> GetEventAsync(string id) => Done(_events.FirstOrDefault(e => e.Id == id));
    public Task<ServiceEvent> CreateEventAsync(ServiceEvent e) { _events.Add(e); return Done(e); }
    public Task UpdateEventAsync(ServiceEvent e)
    {
        var i = _events.FindIndex(x => x.Id == e.Id);
        if (i >= 0) _events[i] = e;
        return Task.CompletedTask;
    }

    public Task<List<Assignment>> GetAssignmentsAsync() => Done(_assignments.ToList());
    public Task<List<Assignment>> GetAssignmentsForEventAsync(string eventId) =>
        Done(_assignments.Where(a => a.EventId == eventId).ToList());
    public Task<List<Assignment>> GetAssignmentsForAccountAsync(string accountId) =>
        Done(_assignments.Where(a => a.AccountId == accountId).ToList());
    public Task<Assignment> CreateAssignmentAsync(Assignment a) { _assignments.Add(a); return Done(a); }
    public Task UpdateAssignmentAsync(Assignment a)
    {
        var i = _assignments.FindIndex(x => x.Id == a.Id);
        if (i >= 0) _assignments[i] = a;
        return Task.CompletedTask;
    }

    public Task<List<Timesheet>> GetTimesheetsAsync() => Done(_timesheets.ToList());
    public Task<Timesheet?> GetTimesheetAsync(string id) => Done(_timesheets.FirstOrDefault(t => t.Id == id));
    public Task<Timesheet?> GetTimesheetForAssignmentAsync(string assignmentId) =>
        Done(_timesheets.FirstOrDefault(t => t.AssignmentId == assignmentId));
    public Task<Timesheet> CreateTimesheetAsync(Timesheet t) { _timesheets.Add(t); return Done(t); }
    public Task UpdateTimesheetAsync(Timesheet t)
    {
        var i = _timesheets.FindIndex(x => x.Id == t.Id);
        if (i >= 0) _timesheets[i] = t;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter DataServiceTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: InMemoryDataService backed by seed"
```

---

### Task 7: Design system CSS + fonts + PWA manifest + icons

**Files:**
- Create: `src/AdminTaos/wwwroot/app.css`, `src/AdminTaos/wwwroot/fonts/` (Playfair + Inter woff2)
- Modify: `src/AdminTaos/wwwroot/index.html`, `src/AdminTaos/wwwroot/manifest.webmanifest`
- Create: `src/AdminTaos/wwwroot/icons/icon-192.png`, `icon-512.png`, `icon-maskable.png`

- [ ] **Step 1: Generate PWA icons from the logo**

Run:
```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
mkdir -p src/AdminTaos/wwwroot/icons
sips -s format png -z 192 192 logo/taoslogo.png --out src/AdminTaos/wwwroot/icons/icon-192.png
sips -s format png -z 512 512 logo/taoslogo.png --out src/AdminTaos/wwwroot/icons/icon-512.png
cp src/AdminTaos/wwwroot/icons/icon-512.png src/AdminTaos/wwwroot/icons/icon-maskable.png
```
Expected: three PNG files created.

- [ ] **Step 2: Download fonts locally (offline PWA)**

Run:
```bash
mkdir -p src/AdminTaos/wwwroot/fonts
curl -sL "https://fonts.gstatic.com/s/playfairdisplay/v37/nuFvD-vYSZviVYUb_rj3ij__anPXJzDwcbmjWBN2PKdFvXDXbtY.woff2" -o src/AdminTaos/wwwroot/fonts/playfair-700.woff2
curl -sL "https://fonts.gstatic.com/s/inter/v13/UcCO3FwrK3iLTeHuS_fvQtMwCp50KnMa1ZL7.woff2" -o src/AdminTaos/wwwroot/fonts/inter-var.woff2
ls -la src/AdminTaos/wwwroot/fonts
```
Expected: two `.woff2` files > 10 KB each. (If a URL 404s, fetch the current woff2 URL from `https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700&family=Inter:wght@400;600;700` and retry.)

- [ ] **Step 3: Write the design system**

`src/AdminTaos/wwwroot/app.css`:
```css
@font-face{font-family:'Playfair';src:url('fonts/playfair-700.woff2') format('woff2');font-weight:700;font-display:swap}
@font-face{font-family:'Inter';src:url('fonts/inter-var.woff2') format('woff2');font-weight:400 700;font-display:swap}

:root{
  --bg:#141414; --surface:#1F1F1F; --line:#2A2A2A; --gline:#3A2F16;
  --txt:#EDEDED; --mut:#9A9A9A; --gold:#E7C76B; --gold2:#C2A14D; --ok:#7BD88F;
  --pf:'Playfair',Georgia,serif; --sf:'Inter',system-ui,sans-serif;
}
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%}
body{background:var(--bg);color:var(--txt);font-family:var(--sf);
  -webkit-font-smoothing:antialiased}
#app,.app-shell{min-height:100%}
.app-shell{max-width:480px;margin:0 auto;display:flex;flex-direction:column;
  min-height:100dvh;position:relative}
.content{flex:1;padding:8px 18px calc(76px + env(safe-area-inset-bottom));overflow-y:auto}
.content.no-nav{padding-bottom:24px}

.topbar{display:flex;align-items:center;gap:10px;padding:14px 18px 10px}
.topbar .wm{font-family:var(--pf);font-weight:700;letter-spacing:3px;font-size:20px;
  background:linear-gradient(135deg,#E7C76B,#B8902F);-webkit-background-clip:text;
  background-clip:text;color:transparent}
.topbar .back{color:var(--gold);font-size:22px;background:none;border:0;cursor:pointer}
.topbar .ttl{font-family:var(--pf);font-weight:700;font-size:18px}

h1,.greet{font-family:var(--pf);font-weight:700;font-size:24px;margin:6px 0 2px}
.sub{font-size:12px;color:var(--mut);margin-bottom:14px}
.lab{font-size:10px;letter-spacing:1.5px;text-transform:uppercase;color:var(--mut);
  margin:16px 0 8px}
.card{background:var(--surface);border:1px solid var(--line);border-radius:16px;
  padding:14px;margin-bottom:10px;display:block;width:100%;text-align:left;
  color:inherit;text-decoration:none;cursor:pointer}
.card.glow{background:linear-gradient(160deg,#211D12,#1A1A1A);border-color:var(--gline);
  box-shadow:0 0 18px rgba(214,176,84,.10)}
.card .t{font-family:var(--pf);font-weight:700;font-size:16px;color:var(--gold);margin-bottom:4px}
.card .m{font-size:12px;color:var(--mut);line-height:1.6}

.btn{display:block;width:100%;text-align:center;padding:13px;border-radius:12px;
  font-weight:700;font-size:14px;border:0;cursor:pointer;font-family:var(--sf)}
.btn.primary{background:linear-gradient(135deg,#E7C76B,#C2A14D);color:#1A1500}
.btn.ghost{background:transparent;color:var(--gold);border:1px solid var(--gline)}
.btn.danger{background:transparent;color:#E58A8A;border:1px solid #5A2A2A}
.btn+.btn{margin-top:8px}
.row2{display:flex;gap:8px}.row2 .btn{flex:1}

.field{display:flex;justify-content:space-between;align-items:center;font-size:13px;
  padding:11px 0;border-bottom:1px solid var(--line);color:var(--mut);gap:10px}
.field b{color:var(--txt);font-weight:600;text-align:right}
.field input,.field select{background:var(--surface);border:1px solid var(--line);
  color:var(--txt);border-radius:8px;padding:8px;font-family:var(--sf);font-size:13px;
  max-width:60%}

.pill{display:inline-block;font-size:10px;padding:4px 9px;border-radius:20px;
  border:1px solid var(--gline);background:rgba(231,199,107,.13);color:var(--gold)}
.pill.ok{color:var(--ok);border-color:#2c5a36;background:rgba(123,216,143,.12)}
.pill.bad{color:#E58A8A;border-color:#5A2A2A;background:rgba(229,138,138,.10)}
.pill.wait{color:var(--mut);border-color:var(--line);background:#222}

.stepper{display:flex;align-items:center;gap:10px}
.stepper button{width:26px;height:26px;border-radius:7px;background:var(--surface);
  border:1px solid var(--line);color:var(--gold);font-size:15px;cursor:pointer}
.stepper b{min-width:54px;text-align:center;color:var(--txt)}

.toggle{width:42px;height:24px;border-radius:20px;border:0;cursor:pointer;
  background:var(--line);position:relative;transition:.15s}
.toggle.on{background:var(--gline)}
.toggle::after{content:'';position:absolute;top:2px;left:2px;width:20px;height:20px;
  border-radius:50%;background:var(--mut);transition:.15s}
.toggle.on::after{left:20px;background:var(--gold)}

.chrono{font-family:var(--pf);font-weight:700;font-size:46px;color:var(--gold);
  text-align:center;margin:18px 0 4px;letter-spacing:1px}
.live{text-align:center;font-size:10px;color:var(--ok);letter-spacing:2px;margin-bottom:18px}

.bottomnav{position:fixed;left:0;right:0;bottom:0;max-width:480px;margin:0 auto;
  display:flex;justify-content:space-around;background:#0E0E0E;border-top:1px solid var(--line);
  padding:9px 0 calc(12px + env(safe-area-inset-bottom))}
.bottomnav a{display:flex;flex-direction:column;align-items:center;gap:3px;
  font-size:10px;color:var(--mut);text-decoration:none}
.bottomnav a.active{color:var(--gold)}
.bottomnav a .i{font-size:17px}

.empty{text-align:center;color:var(--mut);padding:48px 16px}
.empty .ic{font-size:34px;color:var(--gline);margin-bottom:10px}
.empty .e-t{font-family:var(--pf);font-size:16px;color:var(--txt);margin-bottom:4px}

a{color:var(--gold)}
```

- [ ] **Step 4: Wire CSS + manifest in index.html**

Replace the `<head>` style links in `src/AdminTaos/wwwroot/index.html` so it references `app.css` and the manifest, and set theme color:
```html
<link rel="stylesheet" href="app.css" />
<link rel="manifest" href="manifest.webmanifest" />
<meta name="theme-color" content="#141414" />
<link rel="apple-touch-icon" href="icons/icon-192.png" />
```
Remove the template's `bootstrap` and `AdminTaos.styles.css`/default css links if present. Set `<body>` content wrapper to keep `<div id="app">`.

- [ ] **Step 5: Replace manifest.webmanifest**

`src/AdminTaos/wwwroot/manifest.webmanifest`:
```json
{
  "name": "TAOS — The Art of Service",
  "short_name": "TAOS",
  "start_url": "./",
  "display": "standalone",
  "background_color": "#141414",
  "theme_color": "#141414",
  "icons": [
    { "src": "icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "icons/icon-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

- [ ] **Step 6: Verify build + visual smoke**

Run: `dotnet build`
Expected: Build succeeded.
Run: `dotnet run --project src/AdminTaos` then open the printed URL.
Expected: dark background, no console 404 for `app.css`/fonts/manifest. (App content is empty until Task 9 — only the dark shell is expected.)

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: noir & luxe design system, local fonts, PWA manifest+icons"
```

---

### Task 8: _Imports + Program.cs DI registration

**Files:**
- Modify: `src/AdminTaos/_Imports.razor`, `src/AdminTaos/Program.cs`

- [ ] **Step 1: Add usings to _Imports.razor**

Append to `src/AdminTaos/_Imports.razor`:
```razor
@using AdminTaos.Models
@using AdminTaos.Services
@using AdminTaos.Components
@using AdminTaos.Layout
@using Blazored.LocalStorage
```

- [ ] **Step 2: Register services in Program.cs**

In `src/AdminTaos/Program.cs`, before `await builder.Build().RunAsync();`:
```csharp
using AdminTaos.Services;
using Blazored.LocalStorage;

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddSingleton<IDataService, InMemoryDataService>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<NavigationGuard>();
```
(`AuthState` and `NavigationGuard` are created in Tasks 9–10; this registration compiles only after them — keep this step’s commit deferred to Task 10.)

- [ ] **Step 3: Verify build of _Imports only**

Run: `dotnet build`
Expected: FAIL referencing `AuthState`/`NavigationGuard` (resolved in Task 10). Proceed to Task 9.

---

## Phase 2 — Auth, navigation guard, shells, onboarding

### Task 9: AuthState (TDD)

**Files:**
- Create: `src/AdminTaos/Services/AuthState.cs`
- Test: `tests/AdminTaos.Tests/AuthStateTests.cs`

- [ ] **Step 1: Write failing tests with a fake localStorage**

`tests/AdminTaos.Tests/AuthStateTests.cs`:
```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Blazored.LocalStorage;
using Xunit;

namespace AdminTaos.Tests;

class FakeLocalStorage : ILocalStorageService
{
    readonly Dictionary<string,string> _d = new();
    public ValueTask<T> GetItemAsync<T>(string k, CancellationToken c = default)
        => new(_d.TryGetValue(k, out var v) && v is T tv ? tv : default!);
    public ValueTask SetItemAsync<T>(string k, T v, CancellationToken c = default)
    { _d[k] = v?.ToString() ?? ""; return ValueTask.CompletedTask; }
    public ValueTask RemoveItemAsync(string k, CancellationToken c = default)
    { _d.Remove(k); return ValueTask.CompletedTask; }
    // Unused members throw — keeps the fake minimal and honest.
    public ValueTask<string> GetItemAsStringAsync(string k, CancellationToken c=default)=>new(_d.GetValueOrDefault(k,""));
    public ValueTask SetItemAsStringAsync(string k,string v,CancellationToken c=default){_d[k]=v;return ValueTask.CompletedTask;}
    public ValueTask<bool> ContainKeyAsync(string k,CancellationToken c=default)=>new(_d.ContainsKey(k));
    public ValueTask ClearAsync(CancellationToken c=default){_d.Clear();return ValueTask.CompletedTask;}
    public ValueTask<int> LengthAsync(CancellationToken c=default)=>new(_d.Count);
    public ValueTask<string> KeyAsync(int i,CancellationToken c=default)=>new(_d.Keys.ElementAt(i));
    public ValueTask<IEnumerable<string>> KeysAsync(CancellationToken c=default)=>new(_d.Keys.AsEnumerable());
    public event EventHandler<ChangingEventArgs>? Changing;
    public event EventHandler<ChangedEventArgs>? Changed;
}

public class AuthStateTests
{
    [Fact]
    public async Task Login_with_known_email_sets_current_user()
    {
        var auth = new AuthState(new InMemoryDataService(), new FakeLocalStorage());
        var ok = await auth.LoginAsync("manager@taos.be");
        Assert.True(ok);
        Assert.Equal(AccountType.Manager, auth.CurrentUser!.Type);
    }

    [Fact]
    public async Task Login_unknown_email_fails()
    {
        var auth = new AuthState(new InMemoryDataService(), new FakeLocalStorage());
        Assert.False(await auth.LoginAsync("nobody@taos.be"));
        Assert.Null(auth.CurrentUser);
    }

    [Fact]
    public async Task Initialize_restores_session()
    {
        var ls = new FakeLocalStorage();
        var a1 = new AuthState(new InMemoryDataService(), ls);
        await a1.LoginAsync("sarah@taos.be");
        var a2 = new AuthState(new InMemoryDataService(), ls);
        await a2.InitializeAsync();
        Assert.Equal("sarah@taos.be", a2.CurrentUser!.Email);
    }

    [Fact]
    public async Task Logout_clears_session()
    {
        var auth = new AuthState(new InMemoryDataService(), new FakeLocalStorage());
        await auth.LoginAsync("sarah@taos.be");
        await auth.LogoutAsync();
        Assert.Null(auth.CurrentUser);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter AuthStateTests`
Expected: FAIL — `AuthState` does not exist.

- [ ] **Step 3: Implement AuthState**

`src/AdminTaos/Services/AuthState.cs`:
```csharp
using AdminTaos.Models;
using Blazored.LocalStorage;

namespace AdminTaos.Services;

public class AuthState
{
    private const string Key = "taos.session.email";
    private readonly IDataService _data;
    private readonly ILocalStorageService _ls;

    public AuthState(IDataService data, ILocalStorageService ls) { _data = data; _ls = ls; }

    public Account? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event Action? OnChange;
    private void Notify() => OnChange?.Invoke();

    public async Task InitializeAsync()
    {
        var email = await _ls.GetItemAsStringAsync(Key);
        if (!string.IsNullOrWhiteSpace(email))
            CurrentUser = await _data.GetAccountByEmailAsync(email);
        Notify();
    }

    public async Task<bool> LoginAsync(string email)
    {
        var acc = await _data.GetAccountByEmailAsync(email.Trim());
        if (acc is null) return false;
        CurrentUser = acc;
        await _ls.SetItemAsStringAsync(Key, acc.Email);
        Notify();
        return true;
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        await _ls.RemoveItemAsync(Key);
        Notify();
    }

    public void Refresh() => Notify();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter AuthStateTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: AuthState mock auth with localStorage session"
```

---

### Task 10: NavigationGuard (TDD)

**Files:**
- Create: `src/AdminTaos/Services/NavigationGuard.cs`
- Test: `tests/AdminTaos.Tests/NavigationGuardTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/AdminTaos.Tests/NavigationGuardTests.cs`:
```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class NavigationGuardTests
{
    static Account Mgr   = new(){ Type=AccountType.Manager,  Status=AccountStatus.Active };
    static Account Emp   = new(){ Type=AccountType.Employee, Status=AccountStatus.Active };
    static Account Pend  = new(){ Type=AccountType.Employee, Status=AccountStatus.Pending };
    static Account Rej   = new(){ Type=AccountType.Employee, Status=AccountStatus.Rejected };

    readonly NavigationGuard g = new();

    [Fact] public void Anonymous_on_protected_goes_login()
        => Assert.Equal("login", g.Resolve("m/events", null));
    [Fact] public void Anonymous_on_login_is_allowed()
        => Assert.Null(g.Resolve("login", null));
    [Fact] public void Pending_forced_to_pending_screen()
        => Assert.Equal("pending", g.Resolve("e", Pend));
    [Fact] public void Pending_on_pending_allowed()
        => Assert.Null(g.Resolve("pending", Pend));
    [Fact] public void Rejected_sent_to_login()
        => Assert.Equal("login", g.Resolve("e", Rej));
    [Fact] public void Manager_in_employee_area_redirected_home()
        => Assert.Equal("m", g.Resolve("e/hours", Mgr));
    [Fact] public void Employee_in_manager_area_redirected_home()
        => Assert.Equal("e", g.Resolve("m/team", Emp));
    [Fact] public void Manager_in_manager_area_allowed()
        => Assert.Null(g.Resolve("m/events", Mgr));
    [Fact] public void Authenticated_on_auth_page_redirected_home()
        => Assert.Equal("m", g.Resolve("login", Mgr));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter NavigationGuardTests`
Expected: FAIL — `NavigationGuard` does not exist.

- [ ] **Step 3: Implement the guard (pure logic)**

`src/AdminTaos/Services/NavigationGuard.cs`:
```csharp
using AdminTaos.Models;

namespace AdminTaos.Services;

public class NavigationGuard
{
    private static readonly string[] AuthPages = { "", "splash", "login", "register" };

    /// <summary>Returns a relative path to redirect to, or null if the path is allowed.</summary>
    public string? Resolve(string path, Account? user)
    {
        path = path.Trim('/').ToLowerInvariant();
        var isAuthPage = AuthPages.Contains(path);

        if (user is null)
            return isAuthPage ? null : "login";

        if (user.Status == AccountStatus.Rejected)
            return path == "login" ? null : "login";

        if (user.Status == AccountStatus.Pending)
            return path == "pending" ? null : "pending";

        // Active user
        var area = user.Type == AccountType.Manager ? "m" : "e";
        if (isAuthPage || path == "pending") return area;
        var root = path.Split('/')[0];
        if (root != area) return area;
        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter NavigationGuardTests`
Expected: PASS (9 tests).

- [ ] **Step 5: Build whole solution (Program.cs DI now resolves)**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: NavigationGuard route access rules + DI wiring"
```

---

### Task 11: App.razor with guard + shells

**Files:**
- Modify: `src/AdminTaos/App.razor`
- Create: `src/AdminTaos/Layout/AuthLayout.razor`, `ManagerLayout.razor`, `EmployeeLayout.razor`
- Create: `src/AdminTaos/Components/TopBar.razor`, `BottomNav.razor`

- [ ] **Step 1: TopBar component**

`src/AdminTaos/Components/TopBar.razor`:
```razor
@inject NavigationManager Nav

<div class="topbar">
    @if (ShowBack)
    {
        <button class="back" @onclick="GoBack" aria-label="Retour">‹</button>
        <span class="ttl">@Title</span>
    }
    else
    {
        <span class="wm">TAOS</span>
    }
</div>

@code {
    [Parameter] public bool ShowBack { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? BackTo { get; set; }
    void GoBack() => Nav.NavigateTo(BackTo ?? "javascript:history.back()");
}
```

- [ ] **Step 2: BottomNav component**

`src/AdminTaos/Components/BottomNav.razor`:
```razor
<nav class="bottomnav">
    @foreach (var i in Items)
    {
        <NavLink href="@i.Href" class="@(IsActive(i.Href) ? "active" : "")" Match="NavLinkMatch.Prefix">
            <span class="i">@i.Icon</span>@i.Label
        </NavLink>
    }
</nav>

@code {
    public record NavItem(string Href, string Icon, string Label);
    [Parameter, EditorRequired] public List<NavItem> Items { get; set; } = new();
    [Inject] NavigationManager Nav { get; set; } = default!;
    bool IsActive(string href)
    {
        var cur = "/" + Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0];
        return cur == href || cur.StartsWith(href + "/");
    }
}
```

- [ ] **Step 3: Layouts**

`src/AdminTaos/Layout/AuthLayout.razor`:
```razor
@inherits LayoutComponentBase
<div class="app-shell"><div class="content no-nav">@Body</div></div>
```

`src/AdminTaos/Layout/ManagerLayout.razor`:
```razor
@inherits LayoutComponentBase
<div class="app-shell">
    <div class="content">@Body</div>
    <BottomNav Items="@_nav" />
</div>
@code {
    readonly List<BottomNav.NavItem> _nav = new()
    {
        new("/m","⌂","Accueil"), new("/m/events","▦","Events"),
        new("/m/team","◴","Équipe"), new("/m/profile","○","Profil"),
    };
}
```

`src/AdminTaos/Layout/EmployeeLayout.razor`:
```razor
@inherits LayoutComponentBase
<div class="app-shell">
    <div class="content">@Body</div>
    <BottomNav Items="@_nav" />
</div>
@code {
    readonly List<BottomNav.NavItem> _nav = new()
    {
        new("/e","⌂","Accueil"), new("/e/events","▦","Events"),
        new("/e/hours","◷","Mes heures"), new("/e/profile","○","Profil"),
    };
}
```

- [ ] **Step 4: App.razor with guard**

`src/AdminTaos/App.razor`:
```razor
@inject AuthState Auth
@inject NavigationGuard Guard
@inject NavigationManager Nav

<Router AppAssembly="@typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(AuthLayout)" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(AuthLayout)"><p>Page introuvable.</p></LayoutView>
    </NotFound>
</Router>

@code {
    protected override async Task OnInitializedAsync()
    {
        await Auth.InitializeAsync();
        Nav.LocationChanged += (_, __) => Enforce();
        Auth.OnChange += Enforce;
        Enforce();
    }

    void Enforce()
    {
        var path = Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0];
        var target = Guard.Resolve(path, Auth.CurrentUser);
        if (target is not null && target != path)
            Nav.NavigateTo(target, replace: true);
    }
}
```

- [ ] **Step 5: Build + commit**

Run: `dotnet build`
Expected: Build succeeded.
```bash
git add -A && git commit -m "feat: routed shells + guard enforcement in App.razor"
```

---

### Task 12: Auth pages (Splash, Login w/ quick-login, Register, AccountPending)

**Files:**
- Create: `src/AdminTaos/Pages/Auth/Splash.razor`, `Login.razor`, `Register.razor`, `AccountPending.razor`
- Test: `tests/AdminTaos.Tests/ComponentTests.cs`

- [ ] **Step 1: Write a bUnit failing test for quick-login list**

`tests/AdminTaos.Tests/ComponentTests.cs`:
```csharp
using AdminTaos.Pages.Auth;
using AdminTaos.Services;
using Blazored.LocalStorage;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ComponentTests : TestContext
{
    void Wire()
    {
        Services.AddSingleton<IDataService, InMemoryDataService>();
        Services.AddSingleton<ILocalStorageService>(new FakeLocalStorage());
        Services.AddScoped<AuthState>();
        Services.AddScoped<NavigationGuard>();
    }

    [Fact]
    public void Login_lists_five_seeded_test_accounts()
    {
        Wire();
        var cut = RenderComponent<Login>();
        cut.WaitForState(() => cut.FindAll(".card.quick").Count == 5);
        Assert.Contains("Manager", cut.Markup);
        Assert.Contains("En attente", cut.Markup);
        Assert.Contains("Refusé", cut.Markup);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter Login_lists_five_seeded_test_accounts`
Expected: FAIL — `Login` does not exist.

- [ ] **Step 3: Create the auth pages**

`src/AdminTaos/Pages/Auth/Splash.razor`:
```razor
@page "/"
@page "/splash"
@inject NavigationManager Nav
@inject AuthState Auth

<div class="app-shell"><div class="content no-nav"
     style="display:flex;align-items:center;justify-content:center">
    <div style="text-align:center">
        <div class="wm" style="font-size:34px;font-family:var(--pf)">TAOS</div>
        <div class="sub" style="letter-spacing:3px;margin-top:8px">THE ART OF SERVICE</div>
    </div>
</div></div>

@code {
    protected override void OnAfterRender(bool first)
    {
        if (first)
            Nav.NavigateTo(Auth.IsAuthenticated
                ? (Auth.CurrentUser!.Type == AccountType.Manager ? "m" : "e")
                : "login", replace: true);
    }
}
```

`src/AdminTaos/Pages/Auth/Login.razor`:
```razor
@page "/login"
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar />
<h1>Connexion</h1>
<p class="sub">Entre ton email, ou choisis un compte de test.</p>

<div class="field"><span>Email</span>
    <input @bind="_email" placeholder="manager@taos.be" /></div>
@if (_error) { <p class="pill bad" style="margin-top:10px">Email inconnu</p> }
<button class="btn primary" style="margin-top:14px" @onclick="DoLogin">Se connecter</button>
<a class="btn ghost" href="register">Créer un compte</a>

<div class="lab">Comptes de test</div>
@foreach (var a in _accounts)
{
    <button class="card quick" @onclick="() => Quick(a.Email)">
        <div class="t">@a.FullName</div>
        <div class="m">@TypeLabel(a) · @RoleLabel(a) · @StatusLabel(a.Status)</div>
    </button>
}

@code {
    List<Account> _accounts = new();
    Dictionary<string,string> _roleNames = new();
    string _email = "";
    bool _error;

    protected override async Task OnInitializedAsync()
    {
        _accounts = await Data.GetAccountsAsync();
        _roleNames = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
    }

    string TypeLabel(Account a) => a.Type == AccountType.Manager ? "Manager" : "Employé";
    string RoleLabel(Account a) => a.JobRoleIds.Count == 0 ? "—"
        : string.Join(", ", a.JobRoleIds.Select(id => _roleNames.GetValueOrDefault(id, "?")));
    string StatusLabel(AccountStatus s) => s switch
    {
        AccountStatus.Active => "Actif",
        AccountStatus.Pending => "En attente",
        _ => "Refusé"
    };

    async Task DoLogin() => await Quick(_email);

    async Task Quick(string email)
    {
        _error = !await Auth.LoginAsync(email);
        if (_error) return;
        var u = Auth.CurrentUser!;
        Nav.NavigateTo(u.Status switch
        {
            AccountStatus.Pending => "pending",
            AccountStatus.Rejected => "login",
            _ => u.Type == AccountType.Manager ? "m" : "e"
        }, replace: true);
    }
}
```

`src/AdminTaos/Pages/Auth/Register.razor`:
```razor
@page "/register"
@inject IDataService Data
@inject NavigationManager Nav

<TopBar ShowBack="true" Title="Inscription" BackTo="login" />
<h1>Créer un compte</h1>
<p class="sub">Ton compte sera validé par un manager.</p>

<div class="field"><span>Nom complet</span><input @bind="_name" /></div>
<div class="field"><span>Email</span><input @bind="_email" /></div>
<div class="field"><span>Mot de passe</span><input type="password" @bind="_pwd" /></div>
<div class="lab">Rôle(s) souhaité(s)</div>
@foreach (var r in _roles)
{
    <button class="card @(_selected.Contains(r.Id) ? "glow" : "")"
            @onclick="() => Toggle(r.Id)">
        <div class="t">@r.Name</div>
        <div class="m">@(_selected.Contains(r.Id) ? "Sélectionné" : "Toucher pour choisir")</div>
    </button>
}
@if (_err) { <p class="pill bad" style="margin-top:10px">Remplis nom, email et au moins un rôle.</p> }
<button class="btn primary" style="margin-top:14px" @onclick="Submit">S'inscrire</button>

@code {
    List<JobRole> _roles = new();
    readonly HashSet<string> _selected = new();
    string _name = "", _email = "", _pwd = "";
    bool _err;

    protected override async Task OnInitializedAsync() => _roles = await Data.GetJobRolesAsync();
    void Toggle(string id) { if (!_selected.Add(id)) _selected.Remove(id); }

    async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_name) || string.IsNullOrWhiteSpace(_email) || _selected.Count == 0)
        { _err = true; return; }
        await Data.CreateAccountAsync(new Account {
            FullName = _name, Email = _email, Type = AccountType.Employee,
            Status = AccountStatus.Pending, JobRoleIds = _selected.ToList() });
        Nav.NavigateTo("login");
    }
}
```

`src/AdminTaos/Pages/Auth/AccountPending.razor`:
```razor
@page "/pending"
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar />
<div class="empty" style="margin-top:60px">
    <div class="ic">◷</div>
    <div class="e-t">Compte en attente de validation</div>
    <p>Un manager doit valider ton compte avant l'accès.<br/>Reviens un peu plus tard.</p>
</div>
<button class="btn ghost" @onclick="Logout">Se déconnecter</button>

@code {
    async Task Logout() { await Auth.LogoutAsync(); Nav.NavigateTo("login", replace:true); }
}
```

- [ ] **Step 4: Apply role layouts via _Imports or @layout**

Add to `src/AdminTaos/_Imports.razor` nothing layout-wide; instead each Manager page declares `@layout ManagerLayout` and each Employee page `@layout EmployeeLayout` (done in their tasks). Auth pages use the default `AuthLayout` from `App.razor`.

- [ ] **Step 5: Run test + manual check**

Run: `dotnet test --filter Login_lists_five_seeded_test_accounts`
Expected: PASS.
Run: `dotnet run --project src/AdminTaos`, open URL.
Expected: Splash → redirected to Login; 5 test-account cards visible with name · type · role · status; clicking "Hervé T." (manager) lands on `/m` (blank until Phase 3); clicking "Léa B." lands on `/pending`; clicking "Tom V." stays on login.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: auth pages incl. quick-login test accounts"
```

---

## Phase 3 — Manager space

### Task 13: Shared helpers — DataContext extension

**Files:**
- Create: `src/AdminTaos/Services/ViewHelpers.cs`

- [ ] **Step 1: Create read helpers used across pages**

`src/AdminTaos/Services/ViewHelpers.cs`:
```csharp
using AdminTaos.Models;

namespace AdminTaos.Services;

public static class ViewHelpers
{
    public static string Fmt(this TimeSpan? d) =>
        d is { } v ? $"{(int)v.TotalHours} h {v.Minutes:00}" : "—";
    public static string Fmt(this DateOnly d) => d.ToString("ddd d MMM",
        new System.Globalization.CultureInfo("fr-FR"));
    public static string Fmt(this TimeOnly t) => t.ToString("HH:mm");
    public static string Fmt(this DateTime? dt) => dt?.ToString("HH:mm") ?? "—";

    public static string StatusFr(this TimesheetStatus s) => s switch
    {
        TimesheetStatus.NotStarted => "À démarrer",
        TimesheetStatus.InProgress => "En cours",
        TimesheetStatus.ToSend     => "À envoyer",
        TimesheetStatus.Sent       => "Envoyée",
        TimesheetStatus.Validated  => "Validée",
        TimesheetStatus.Rejected   => "Refusée",
        _ => s.ToString()
    };
    public static string PillClass(this TimesheetStatus s) => s switch
    {
        TimesheetStatus.Validated => "pill ok",
        TimesheetStatus.Rejected  => "pill bad",
        TimesheetStatus.Sent      => "pill",
        _ => "pill wait"
    };
}
```

- [ ] **Step 2: Build + commit**

Run: `dotnet build`
Expected: Build succeeded.
```bash
git add -A && git commit -m "feat: shared view formatting helpers"
```

---

### Task 14: Manager Home (`/m`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MHome.razor`

- [ ] **Step 1: Create the page**

`src/AdminTaos/Pages/Manager/MHome.razor`:
```razor
@page "/m"
@layout ManagerLayout
@inject IDataService Data

<TopBar />
<h1>À traiter</h1>
<p class="sub">Tableau de bord manager</p>

<NavLink class="card" href="m/team?filter=pending">
    <div class="t">@_pendingAccounts comptes à valider</div>
    <div class="m">@_pendingAccountNames</div>
</NavLink>
<NavLink class="card" href="m/events">
    <div class="t">@_joinRequests demande(s) de participation</div>
    <div class="m">À valider sur les events ouverts</div>
</NavLink>
<NavLink class="card" href="m/timesheets">
    <div class="t">@_pendingTimesheets timesheet(s) à valider</div>
    <div class="m">Reçues, en attente de validation</div>
</NavLink>

<div class="lab">Prochains events</div>
@if (_upcoming.Count == 0) { <div class="card"><div class="m">Aucun event à venir</div></div> }
@foreach (var e in _upcoming)
{
    <NavLink class="card glow" href="@($"m/events/{e.Id}")">
        <div class="t">@e.Name</div>
        <div class="m">@e.Date.Fmt() · @e.Venue</div>
    </NavLink>
}

@code {
    int _pendingAccounts, _joinRequests, _pendingTimesheets;
    string _pendingAccountNames = "";
    List<ServiceEvent> _upcoming = new();

    protected override async Task OnInitializedAsync()
    {
        var accs = await Data.GetAccountsAsync();
        var pend = accs.Where(a => a.Status == AccountStatus.Pending).ToList();
        _pendingAccounts = pend.Count;
        _pendingAccountNames = pend.Count == 0 ? "Aucun" : string.Join(" · ", pend.Select(a => a.FullName));

        var asgs = await Data.GetAssignmentsAsync();
        _joinRequests = asgs.Count(a => a.Status == AssignmentStatus.PendingApproval);

        var ts = await Data.GetTimesheetsAsync();
        _pendingTimesheets = ts.Count(t => t.Status == TimesheetStatus.Sent);

        _upcoming = (await Data.GetEventsAsync())
            .Where(e => e.Status != EventStatus.Past).OrderBy(e => e.Date).Take(5).ToList();
    }
}
```

- [ ] **Step 2: Build + manual check + commit**

Run: `dotnet build` → Build succeeded.
Run app, log in as manager → `/m` shows "1 comptes à valider" (Léa B.), "1 demande(s)", "1 timesheet(s)", and upcoming events Gala/Déjeuner/Cocktail. Bottom nav visible.
```bash
git add -A && git commit -m "feat: manager home dashboard"
```

---

### Task 15: Manager Events list + detail (`/m/events`, `/m/events/{id}`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MEvents.razor`, `MEventDetail.razor`

- [ ] **Step 1: Create events list**

`src/AdminTaos/Pages/Manager/MEvents.razor`:
```razor
@page "/m/events"
@layout ManagerLayout
@inject IDataService Data

<TopBar />
<h1>Events</h1>
<p class="sub">@_events.Count event(s)</p>
<NavLink class="btn primary" href="m/events/new">+ Nouvel event</NavLink>

@if (_events.Count == 0)
{
    <div class="empty"><div class="ic">▦</div><div class="e-t">Aucun event</div>
        <p>Crée ton premier event.</p></div>
}
@foreach (var e in _events)
{
    <NavLink class="card" href="@($"m/events/{e.Id}")">
        <div class="t">@e.Name</div>
        <div class="m">@e.Date.Fmt() · @e.Venue · @StatusFr(e.Status)</div>
    </NavLink>
}

@code {
    List<ServiceEvent> _events = new();
    protected override async Task OnInitializedAsync() => _events = await Data.GetEventsAsync();
    string StatusFr(EventStatus s) => s switch {
        EventStatus.Upcoming => "À venir", EventStatus.InProgress => "En cours", _ => "Passé" };
}
```

- [ ] **Step 2: Create event detail**

`src/AdminTaos/Pages/Manager/MEventDetail.razor`:
```razor
@page "/m/events/{Id}"
@layout ManagerLayout
@inject IDataService Data
@inject NavigationManager Nav

@if (_e is null) { <TopBar ShowBack="true" Title="Event" BackTo="m/events" /><p class="sub">Introuvable.</p> }
else
{
    <TopBar ShowBack="true" Title="@_e.Name" BackTo="m/events" />
    <div class="card glow">
        <div class="t">@_e.Name</div>
        <div class="m">@_e.Venue — @_e.Address<br/>@_e.Date.Fmt() · @_e.MeetingTime.Fmt() → @_e.ExpectedEndTime.Fmt()
        @if(!string.IsNullOrWhiteSpace(_e.DressCode)){<text><br/>Tenue : @_e.DressCode</text>}
        @if(!string.IsNullOrWhiteSpace(_e.OnSiteContact)){<text><br/>Contact : @_e.OnSiteContact</text>}</div>
    </div>

    <div class="lab">Effectif par rôle</div>
    @foreach (var rn in _e.RoleNeeds)
    {
        var c = _confirmed.Count(a => a.JobRoleId == rn.JobRoleId);
        <div class="card"><div class="t">@RoleName(rn.JobRoleId)</div>
            <div class="m">@c / @rn.CountNeeded assigné(s) · @rn.HourlyRate €/h</div></div>
    }

    <div class="lab">Personnel assigné</div>
    @if (_confirmed.Count == 0) { <div class="card"><div class="m">Personne pour l'instant</div></div> }
    @foreach (var a in _confirmed)
    {
        <div class="card"><div class="t">@Name(a.AccountId)</div>
            <div class="m">@RoleName(a.JobRoleId)</div></div>
    }

    @if (_requests.Count > 0)
    {
        <div class="lab">Demandes de participation</div>
        @foreach (var a in _requests)
        {
            <div class="card">
                <div class="t">@Name(a.AccountId)</div>
                <div class="m">@RoleName(a.JobRoleId) · demande à rejoindre</div>
                <div class="row2" style="margin-top:10px">
                    <button class="btn danger" @onclick="() => Decide(a, false)">Refuser</button>
                    <button class="btn primary" @onclick="() => Decide(a, true)">Accepter</button>
                </div>
            </div>
        }
    }

    <NavLink class="btn ghost" href="@($"m/events/{_e.Id}/assign")">Assigner du personnel</NavLink>
    <NavLink class="btn ghost" href="@($"m/events/{_e.Id}/edit")">Éditer l'event</NavLink>
}

@code {
    [Parameter] public string Id { get; set; } = "";
    ServiceEvent? _e;
    List<Assignment> _confirmed = new(), _requests = new();
    Dictionary<string,string> _names = new(), _roles = new();

    protected override async Task OnInitializedAsync() => await Load();

    async Task Load()
    {
        _e = await Data.GetEventAsync(Id);
        _names = (await Data.GetAccountsAsync()).ToDictionary(a => a.Id, a => a.FullName);
        _roles = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
        var asgs = await Data.GetAssignmentsForEventAsync(Id);
        _confirmed = asgs.Where(a => a.Status == AssignmentStatus.Confirmed).ToList();
        _requests  = asgs.Where(a => a.Status == AssignmentStatus.PendingApproval).ToList();
    }

    string Name(string id) => _names.GetValueOrDefault(id, "?");
    string RoleName(string id) => _roles.GetValueOrDefault(id, "?");

    async Task Decide(Assignment a, bool accept)
    {
        a.Status = accept ? AssignmentStatus.Confirmed : AssignmentStatus.Rejected;
        await Data.UpdateAssignmentAsync(a);
        await Load();
    }
}
```

- [ ] **Step 3: Build + manual check + commit**

Run: `dotnet build` → succeeded.
Run app as manager → Events lists 4 events; open "Cocktail privé" → shows role fill, the pending request from Marc D., Accept/Refuse buttons work and the request disappears after a decision.
```bash
git add -A && git commit -m "feat: manager events list + detail with join-request validation"
```

---

### Task 16: Manager Event create/edit (`/m/events/new`, `/m/events/{id}/edit`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MEventEdit.razor`
- Create: `src/AdminTaos/Components/Stepper.razor`, `ToggleSwitch.razor`
- Test: `tests/AdminTaos.Tests/ComponentTests.cs` (Stepper)

- [ ] **Step 1: Failing bUnit test for Stepper**

Append to `ComponentTests.cs`:
```csharp
[Fact]
public void Stepper_increments_and_clamps_at_min()
{
    var val = 1;
    var cut = RenderComponent<AdminTaos.Components.Stepper>(p => p
        .Add(s => s.Value, 1).Add(s => s.Min, 0)
        .Add(s => s.ValueChanged, (int v) => val = v));
    cut.FindAll("button")[1].Click(); // +
    Assert.Equal(2, val);
    cut.FindAll("button")[0].Click(); // -
    cut.FindAll("button")[0].Click(); // - (clamp at 0)
    Assert.Equal(0, val);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter Stepper_increments_and_clamps_at_min`
Expected: FAIL — `Stepper` does not exist.

- [ ] **Step 3: Create Stepper + ToggleSwitch**

`src/AdminTaos/Components/Stepper.razor`:
```razor
<div class="stepper">
    <button @onclick="Dec">–</button>
    <b>@Value@(Suffix)</b>
    <button @onclick="Inc">+</button>
</div>
@code {
    [Parameter] public int Value { get; set; }
    [Parameter] public EventCallback<int> ValueChanged { get; set; }
    [Parameter] public int Min { get; set; } = 0;
    [Parameter] public int Max { get; set; } = 999;
    [Parameter] public string Suffix { get; set; } = "";
    async Task Inc() { if (Value < Max) await ValueChanged.InvokeAsync(Value + 1); }
    async Task Dec() { if (Value > Min) await ValueChanged.InvokeAsync(Value - 1); }
}
```

`src/AdminTaos/Components/ToggleSwitch.razor`:
```razor
<button class="toggle @(Value ? "on" : "")" @onclick="Flip" aria-pressed="@Value"></button>
@code {
    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }
    Task Flip() => ValueChanged.InvokeAsync(!Value);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter Stepper_increments_and_clamps_at_min`
Expected: PASS.

- [ ] **Step 5: Create the edit page**

`src/AdminTaos/Pages/Manager/MEventEdit.razor`:
```razor
@page "/m/events/new"
@page "/m/events/{Id}/edit"
@layout ManagerLayout
@inject IDataService Data
@inject NavigationManager Nav

<TopBar ShowBack="true" Title="@(_isNew ? "Nouvel event" : "Éditer")" BackTo="m/events" />
<h1>@(_isNew ? "Nouvel event" : _e.Name)</h1>

<div class="field"><span>Nom</span><input @bind="_e.Name" /></div>
<div class="field"><span>Lieu</span><input @bind="_e.Venue" /></div>
<div class="field"><span>Adresse</span><input @bind="_e.Address" /></div>
<div class="field"><span>Date</span><input type="date" @bind="_date" /></div>
<div class="field"><span>RDV</span><input type="time" @bind="_meet" /></div>
<div class="field"><span>Fin prévue</span><input type="time" @bind="_end" /></div>
<div class="field"><span>Tenue</span><input @bind="_e.DressCode" /></div>
<div class="field"><span>Consignes</span><input @bind="_e.Instructions" /></div>
<div class="field"><span>Contact sur place</span><input @bind="_e.OnSiteContact" /></div>
<div class="field"><span>Ouvert à l'inscription</span>
    <ToggleSwitch Value="_e.IsOpenForSignup"
        ValueChanged="v => _e.IsOpenForSignup = v" /></div>

<div class="lab">Effectif & rémunération par rôle</div>
@foreach (var r in _roles)
{
    var need = _e.RoleNeeds.FirstOrDefault(n => n.JobRoleId == r.Id);
    <div class="card">
        <div class="t">@r.Name</div>
        <div class="field"><span>Personnes</span>
            <Stepper Value="need?.CountNeeded ?? 0"
                     ValueChanged="v => SetCount(r.Id, v)" /></div>
        <div class="field"><span>Taux €/h</span>
            <input type="number" style="max-width:80px"
                   value="@((need?.HourlyRate ?? 0m))"
                   @onchange="e => SetRate(r.Id, e.Value)" /></div>
    </div>
}

@if (_err) { <p class="pill bad">Le nom est obligatoire.</p> }
<button class="btn primary" @onclick="Save">@(_isNew ? "Créer l'event" : "Enregistrer")</button>

@code {
    [Parameter] public string? Id { get; set; }
    ServiceEvent _e = new();
    List<JobRole> _roles = new();
    bool _isNew, _err;
    DateOnly _date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    TimeOnly _meet = new(17,0), _end = new(23,0);

    protected override async Task OnInitializedAsync()
    {
        _roles = await Data.GetJobRolesAsync();
        _isNew = string.IsNullOrEmpty(Id);
        if (!_isNew)
        {
            _e = await Data.GetEventAsync(Id!) ?? new ServiceEvent();
            _date = _e.Date; _meet = _e.MeetingTime; _end = _e.ExpectedEndTime;
        }
    }

    void SetCount(string roleId, int v)
    {
        var n = _e.RoleNeeds.FirstOrDefault(x => x.JobRoleId == roleId);
        if (n is null) { if (v > 0) _e.RoleNeeds.Add(new RoleNeed { JobRoleId = roleId, CountNeeded = v }); }
        else if (v == 0) _e.RoleNeeds.Remove(n);
        else n.CountNeeded = v;
    }
    void SetRate(string roleId, object? raw)
    {
        if (!decimal.TryParse(raw?.ToString(), out var rate)) return;
        var n = _e.RoleNeeds.FirstOrDefault(x => x.JobRoleId == roleId)
                ?? AddEmpty(roleId);
        n.HourlyRate = rate;
    }
    RoleNeed AddEmpty(string roleId)
    { var n = new RoleNeed { JobRoleId = roleId, CountNeeded = 1 }; _e.RoleNeeds.Add(n); return n; }

    async Task Save()
    {
        if (string.IsNullOrWhiteSpace(_e.Name)) { _err = true; return; }
        _e.Date = _date; _e.MeetingTime = _meet; _e.ExpectedEndTime = _end;
        if (_isNew) await Data.CreateEventAsync(_e);
        else await Data.UpdateEventAsync(_e);
        Nav.NavigateTo("m/events");
    }
}
```

- [ ] **Step 6: Build + manual check + commit**

Run: `dotnet build` → succeeded.
Run app as manager → "+ Nouvel event", fill name, adjust steppers/toggle, Create → returns to list with the new event. Edit an existing event → values prefilled, save persists.
```bash
git add -A && git commit -m "feat: event create/edit with Stepper + ToggleSwitch"
```

---

### Task 17: Manager Assign staff (`/m/events/{id}/assign`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MAssignStaff.razor`

- [ ] **Step 1: Create the page**

`src/AdminTaos/Pages/Manager/MAssignStaff.razor`:
```razor
@page "/m/events/{Id}/assign"
@layout ManagerLayout
@inject IDataService Data
@inject NavigationManager Nav

<TopBar ShowBack="true" Title="Assigner" BackTo="@($"m/events/{Id}")" />
<h1>Assigner du personnel</h1>
<p class="sub">@_event?.Name</p>

@foreach (var r in _eventRoles)
{
    <div class="lab">@r.Name</div>
    @foreach (var emp in _employees.Where(e => e.JobRoleIds.Contains(r.Id)))
    {
        var already = _assigned.Any(a => a.AccountId == emp.Id && a.JobRoleId == r.Id);
        <div class="card">
            <div class="t">@emp.FullName</div>
            <div class="m">@(already ? "Déjà assigné" : "Disponible")</div>
            @if (!already)
            {
                <button class="btn ghost" style="margin-top:8px"
                        @onclick="() => Assign(emp.Id, r.Id)">Assigner</button>
            }
        </div>
    }
    @if (!_employees.Any(e => e.JobRoleIds.Contains(r.Id)))
    {
        <div class="card"><div class="m">Aucun employé actif pour ce rôle</div></div>
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";
    ServiceEvent? _event;
    List<Account> _employees = new();
    List<JobRole> _eventRoles = new();
    List<Assignment> _assigned = new();

    protected override async Task OnInitializedAsync() => await Load();

    async Task Load()
    {
        _event = await Data.GetEventAsync(Id);
        var roles = await Data.GetJobRolesAsync();
        _eventRoles = roles.Where(r => _event!.RoleNeeds.Any(n => n.JobRoleId == r.Id)).ToList();
        _employees = (await Data.GetAccountsAsync())
            .Where(a => a.Type == AccountType.Employee && a.Status == AccountStatus.Active).ToList();
        _assigned = await Data.GetAssignmentsForEventAsync(Id);
    }

    async Task Assign(string accId, string roleId)
    {
        await Data.CreateAssignmentAsync(new Assignment {
            EventId = Id, AccountId = accId, JobRoleId = roleId,
            Source = AssignmentSource.AssignedByManager, Status = AssignmentStatus.Confirmed });
        await Load();
    }
}
```

- [ ] **Step 2: Build + manual check + commit**

Run: `dotnet build` → succeeded.
Run app as manager → Event detail → "Assigner du personnel" → assign Sarah K. (Hôtesse) to the Gala; returning to detail shows her in the assigned list and the role fill incremented.
```bash
git add -A && git commit -m "feat: manager assign staff to event"
```

---

### Task 18: Manager Team list + employee detail (`/m/team`, `/m/team/{id}`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MTeam.razor`, `MEmployeeDetail.razor`

- [ ] **Step 1: Create team list with status tabs**

`src/AdminTaos/Pages/Manager/MTeam.razor`:
```razor
@page "/m/team"
@layout ManagerLayout
@inject IDataService Data
@inject NavigationManager Nav

<TopBar />
<h1>Équipe</h1>
<div class="row2" style="margin-bottom:6px">
    <button class="btn @(_tab=="active"?"primary":"ghost")" @onclick='() => _tab="active"'>Actifs</button>
    <button class="btn @(_tab=="pending"?"primary":"ghost")" @onclick='() => _tab="pending"'>En attente</button>
</div>
<NavLink class="btn ghost" href="m/roles">Gérer les rôles</NavLink>

@{ var list = _tab == "active"
       ? _emps.Where(e => e.Status == AccountStatus.Active)
       : _emps.Where(e => e.Status == AccountStatus.Pending); }
@if (!list.Any())
{
    <div class="empty"><div class="ic">◴</div><div class="e-t">Personne ici</div></div>
}
@foreach (var e in list)
{
    <NavLink class="card" href="@($"m/team/{e.Id}")">
        <div class="t">@e.FullName</div>
        <div class="m">@RoleNames(e) · @(e.Status==AccountStatus.Active?"Actif":"En attente")</div>
    </NavLink>
}

@code {
    string _tab = "active";
    List<Account> _emps = new();
    Dictionary<string,string> _roles = new();
    protected override async Task OnInitializedAsync()
    {
        _emps = (await Data.GetAccountsAsync()).Where(a => a.Type == AccountType.Employee).ToList();
        _roles = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
    }
    string RoleNames(Account a) => a.JobRoleIds.Count == 0 ? "—"
        : string.Join(", ", a.JobRoleIds.Select(id => _roles.GetValueOrDefault(id,"?")));
}
```
(Note: the `?filter=pending` link from MHome lands on this page; default tab `active` is acceptable — the manager taps "En attente". Keep simple.)

- [ ] **Step 2: Create employee detail with validate/reject**

`src/AdminTaos/Pages/Manager/MEmployeeDetail.razor`:
```razor
@page "/m/team/{Id}"
@layout ManagerLayout
@inject IDataService Data
@inject NavigationManager Nav

@if (_a is null) { <TopBar ShowBack="true" Title="Employé" BackTo="m/team" /><p class="sub">Introuvable.</p> }
else
{
    <TopBar ShowBack="true" Title="@_a.FullName" BackTo="m/team" />
    <div class="card glow">
        <div class="t">@_a.FullName</div>
        <div class="m">@_a.Email<br/>Rôles : @RoleNames()<br/>
            Statut : <span class="@StatusPill()">@StatusFr()</span></div>
    </div>

    @if (_a.Status == AccountStatus.Pending)
    {
        <div class="row2" style="margin-top:12px">
            <button class="btn danger" @onclick="() => Set(AccountStatus.Rejected)">Refuser</button>
            <button class="btn primary" @onclick="() => Set(AccountStatus.Active)">Valider le compte</button>
        </div>
    }
    else if (_a.Status == AccountStatus.Active)
    {
        <button class="btn danger" @onclick="() => Set(AccountStatus.Rejected)">Suspendre</button>
    }
    else
    {
        <button class="btn primary" @onclick="() => Set(AccountStatus.Active)">Réactiver</button>
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";
    Account? _a;
    Dictionary<string,string> _roles = new();

    protected override async Task OnInitializedAsync()
    {
        _a = await Data.GetAccountAsync(Id);
        _roles = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
    }
    string RoleNames() => _a!.JobRoleIds.Count == 0 ? "—"
        : string.Join(", ", _a.JobRoleIds.Select(id => _roles.GetValueOrDefault(id,"?")));
    string StatusFr() => _a!.Status switch {
        AccountStatus.Active => "Actif", AccountStatus.Pending => "En attente", _ => "Refusé" };
    string StatusPill() => _a!.Status switch {
        AccountStatus.Active => "pill ok", AccountStatus.Pending => "pill wait", _ => "pill bad" };

    async Task Set(AccountStatus s)
    {
        _a!.Status = s;
        await Data.UpdateAccountAsync(_a);
        Nav.NavigateTo("m/team");
    }
}
```

- [ ] **Step 3: Build + manual check + commit**

Run: `dotnet build` → succeeded.
Run app as manager → Équipe → "En attente" tab shows Léa B. → open → "Valider le compte" → she moves to Actifs; logging in as Léa now reaches `/e` instead of `/pending`.
```bash
git add -A && git commit -m "feat: manager team list + account validation"
```

---

### Task 19: Manager Roles management (`/m/roles`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MRoles.razor`

- [ ] **Step 1: Create the page**

`src/AdminTaos/Pages/Manager/MRoles.razor`:
```razor
@page "/m/roles"
@layout ManagerLayout
@inject IDataService Data

<TopBar ShowBack="true" Title="Rôles" BackTo="m/team" />
<h1>Gérer les rôles</h1>

@foreach (var r in _roles)
{
    <div class="card">
        @if (_editId == r.Id)
        {
            <input @bind="_editName" />
            <div class="row2" style="margin-top:8px">
                <button class="btn ghost" @onclick="() => _editId=null">Annuler</button>
                <button class="btn primary" @onclick="() => SaveEdit(r)">OK</button>
            </div>
        }
        else
        {
            <div class="t">@r.Name</div>
            <div class="row2" style="margin-top:8px">
                <button class="btn ghost" @onclick="() => StartEdit(r)">Éditer</button>
                <button class="btn danger" @onclick="() => Delete(r)">Supprimer</button>
            </div>
        }
    </div>
}

<div class="lab">Nouveau rôle</div>
<div class="field"><span>Nom</span><input @bind="_newName" /></div>
<button class="btn primary" @onclick="Add">Ajouter le rôle</button>

@code {
    List<JobRole> _roles = new();
    string _newName = "", _editName = "";
    string? _editId;

    protected override async Task OnInitializedAsync() => await Load();
    async Task Load() => _roles = await Data.GetJobRolesAsync();

    async Task Add()
    {
        if (string.IsNullOrWhiteSpace(_newName)) return;
        await Data.CreateJobRoleAsync(new JobRole { Name = _newName.Trim() });
        _newName = ""; await Load();
    }
    void StartEdit(JobRole r) { _editId = r.Id; _editName = r.Name; }
    async Task SaveEdit(JobRole r)
    {
        r.Name = _editName.Trim();
        await Data.UpdateJobRoleAsync(r); _editId = null; await Load();
    }
    async Task Delete(JobRole r) { await Data.DeleteJobRoleAsync(r.Id); await Load(); }
}
```

- [ ] **Step 2: Build + manual check + commit**

Run: `dotnet build` → succeeded.
Run app as manager → Équipe → Gérer les rôles → add "Barman", edit it, delete it; Serveur/Hôtesse remain.
```bash
git add -A && git commit -m "feat: manager job-role management"
```

---

### Task 20: Manager Profile (`/m/profile`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MProfile.razor`

- [ ] **Step 1: Create the page**

`src/AdminTaos/Pages/Manager/MProfile.razor`:
```razor
@page "/m/profile"
@layout ManagerLayout
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar />
<h1>Profil</h1>
<div class="card glow">
    <div class="t">@Auth.CurrentUser?.FullName</div>
    <div class="m">@Auth.CurrentUser?.Email<br/>Manager</div>
</div>
<button class="btn ghost" @onclick="Logout">Se déconnecter</button>

@code {
    async Task Logout() { await Auth.LogoutAsync(); Nav.NavigateTo("login", replace:true); }
}
```

- [ ] **Step 2: Build + manual check + commit**

Run: `dotnet build` → succeeded. Manager → Profil shows name/email; Logout returns to login.
```bash
git add -A && git commit -m "feat: manager profile + logout"
```

---

## Phase 4 — Employee space + pointage

### Task 21: Employee Home (`/e`)

**Files:**
- Create: `src/AdminTaos/Pages/Employee/EHome.razor`, `src/AdminTaos/Components/Chrono.razor`
- Test: `tests/AdminTaos.Tests/ComponentTests.cs` (Chrono renders elapsed)

- [ ] **Step 1: Failing bUnit test for Chrono**

Append to `ComponentTests.cs`:
```csharp
[Fact]
public void Chrono_formats_elapsed_since_start()
{
    var start = System.DateTime.Now.AddMinutes(-90).AddSeconds(-5);
    var cut = RenderComponent<AdminTaos.Components.Chrono>(p => p.Add(c => c.Since, start));
    Assert.Matches(@"01:3[01]:\d\d", cut.Find(".chrono").TextContent);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter Chrono_formats_elapsed_since_start`
Expected: FAIL — `Chrono` does not exist.

- [ ] **Step 3: Create Chrono**

`src/AdminTaos/Components/Chrono.razor`:
```razor
@implements IDisposable
<div class="chrono">@Display</div>

@code {
    [Parameter, EditorRequired] public DateTime Since { get; set; }
    System.Threading.Timer? _t;
    string Display = "00:00:00";

    protected override void OnInitialized()
    {
        Tick();
        _t = new System.Threading.Timer(_ =>
            InvokeAsync(() => { Tick(); StateHasChanged(); }), null, 1000, 1000);
    }
    void Tick()
    {
        var e = DateTime.Now - Since;
        if (e < TimeSpan.Zero) e = TimeSpan.Zero;
        Display = $"{(int)e.TotalHours:00}:{e.Minutes:00}:{e.Seconds:00}";
    }
    public void Dispose() => _t?.Dispose();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter Chrono_formats_elapsed_since_start`
Expected: PASS.

- [ ] **Step 5: Create Employee Home**

`src/AdminTaos/Pages/Employee/EHome.razor`:
```razor
@page "/e"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar />
<h1>Bonjour, @FirstName</h1>
<p class="sub">@RoleLine · Compte actif</p>

<div class="lab">Prochaine prestation</div>
@if (_next is null)
{
    <div class="card"><div class="m">Aucune prestation à venir</div></div>
}
else
{
    <div class="card glow">
        <div class="t">@_next.Value.ev.Name</div>
        <div class="m">@_next.Value.ev.Venue · @_next.Value.ev.Date.Fmt()
            · RDV @_next.Value.ev.MeetingTime.Fmt()</div>
    </div>
    @if (_ts is { Status: TimesheetStatus.InProgress })
    {
        <NavLink class="btn ghost" href="@($"e/events/{_next.Value.ev.Id}/active")">
            Reprendre — prestation en cours</NavLink>
    }
    else if (CanStartToday)
    {
        <button class="btn primary" @onclick="Start">Commencer la prestation</button>
    }
    else
    {
        <NavLink class="btn ghost" href="@($"e/events/{_next.Value.ev.Id}")">Voir le détail</NavLink>
    }
}

@code {
    string FirstName = "", RoleLine = "";
    (ServiceEvent ev, Assignment asg)? _next;
    Timesheet? _ts;
    bool CanStartToday;

    protected override async Task OnInitializedAsync()
    {
        var u = Auth.CurrentUser!;
        FirstName = u.FullName.Split(' ')[0];
        var roleMap = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
        RoleLine = string.Join(", ", u.JobRoleIds.Select(id => roleMap.GetValueOrDefault(id,"?")));

        var asgs = (await Data.GetAssignmentsForAccountAsync(u.Id))
            .Where(a => a.Status == AssignmentStatus.Confirmed).ToList();
        var events = await Data.GetEventsAsync();
        var joined = asgs
            .Select(a => (ev: events.FirstOrDefault(e => e.Id == a.EventId), asg: a))
            .Where(x => x.ev is not null && x.ev!.Status != EventStatus.Past)
            .OrderBy(x => x.ev!.Date).ToList();
        if (joined.Count > 0)
        {
            _next = (joined[0].ev!, joined[0].asg);
            _ts = await Data.GetTimesheetForAssignmentAsync(joined[0].asg.Id);
            CanStartToday = _next.Value.ev.Date == DateOnly.FromDateTime(DateTime.Today)
                && (_ts is null || _ts.Status == TimesheetStatus.NotStarted);
        }
    }

    async Task Start()
    {
        var asg = _next!.Value.asg;
        _ts ??= await Data.GetTimesheetForAssignmentAsync(asg.Id);
        if (_ts is null)
            _ts = await Data.CreateTimesheetAsync(new Timesheet { AssignmentId = asg.Id });
        _ts.StartedAt = DateTime.Now;
        _ts.Status = TimesheetStatus.InProgress;
        await Data.UpdateTimesheetAsync(_ts);
        Nav.NavigateTo($"e/events/{_next.Value.ev.Id}/active");
    }
}
```

- [ ] **Step 6: Build + manual check + commit**

Run: `dotnet test --filter Chrono` → PASS. `dotnet build` → succeeded.
Log in as Sarah K. (assigned to today's "Déjeuner d'affaires", seeded InProgress) → home shows the event + "Reprendre — prestation en cours". Log in as Marc D. → next event Gala (future) → "Voir le détail".
```bash
git add -A && git commit -m "feat: employee home + Chrono + start pointage"
```

---

### Task 22: Employee Events list + detail (`/e/events`, `/e/events/{id}`)

**Files:**
- Create: `src/AdminTaos/Pages/Employee/EEvents.razor`, `EEventDetail.razor`

- [ ] **Step 1: Create events list (mine + open)**

`src/AdminTaos/Pages/Employee/EEvents.razor`:
```razor
@page "/e/events"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth

<TopBar />
<h1>Events</h1>

<div class="lab">Mes events</div>
@if (_mine.Count == 0) { <div class="card"><div class="m">Aucun event assigné</div></div> }
@foreach (var e in _mine)
{
    <NavLink class="card" href="@($"e/events/{e.Id}")">
        <div class="t">@e.Name</div><div class="m">@e.Date.Fmt() · @e.Venue</div>
    </NavLink>
}

<div class="lab">Events ouverts à rejoindre</div>
@if (_open.Count == 0) { <div class="card"><div class="m">Aucun event ouvert</div></div> }
@foreach (var e in _open)
{
    <NavLink class="card glow" href="@($"e/events/{e.Id}")">
        <div class="t">@e.Name</div><div class="m">@e.Date.Fmt() · @e.Venue · ouvert</div>
    </NavLink>
}

@code {
    List<ServiceEvent> _mine = new(), _open = new();
    protected override async Task OnInitializedAsync()
    {
        var u = Auth.CurrentUser!;
        var events = await Data.GetEventsAsync();
        var myAsg = await Data.GetAssignmentsForAccountAsync(u.Id);
        var myEventIds = myAsg.Select(a => a.EventId).ToHashSet();
        _mine = events.Where(e => myEventIds.Contains(e.Id)).ToList();
        _open = events.Where(e => e.IsOpenForSignup && e.Status != EventStatus.Past
                                   && !myEventIds.Contains(e.Id)).ToList();
    }
}
```

- [ ] **Step 2: Create event detail (request to join / pointage entry)**

`src/AdminTaos/Pages/Employee/EEventDetail.razor`:
```razor
@page "/e/events/{Id}"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

@if (_e is null) { <TopBar ShowBack="true" Title="Event" BackTo="e/events" /><p class="sub">Introuvable.</p> }
else
{
    <TopBar ShowBack="true" Title="@_e.Name" BackTo="e/events" />
    <div class="card glow">
        <div class="t">@_e.Name</div>
        <div class="m">@_e.Venue — @_e.Address<br/>@_e.Date.Fmt() · @_e.MeetingTime.Fmt() → @_e.ExpectedEndTime.Fmt()
        @if(!string.IsNullOrWhiteSpace(_e.DressCode)){<text><br/>Tenue : @_e.DressCode</text>}
        @if(!string.IsNullOrWhiteSpace(_e.Instructions)){<text><br/>@_e.Instructions</text>}
        @if(!string.IsNullOrWhiteSpace(_e.OnSiteContact)){<text><br/>Contact : @_e.OnSiteContact</text>}</div>
    </div>

    <div class="lab">Rémunération</div>
    @foreach (var rn in _e.RoleNeeds.Where(r => _myRoleIds.Contains(r.JobRoleId)))
    { <div class="card"><div class="m">@RoleName(rn.JobRoleId) · @rn.HourlyRate €/h</div></div> }

    @if (_asg is null && _e.IsOpenForSignup)
    {
        <button class="btn primary" @onclick="RequestJoin">Demander à participer</button>
    }
    else if (_asg is { Status: AssignmentStatus.PendingApproval })
    {
        <p class="pill wait">Demande en attente de validation</p>
    }
    else if (_asg is { Status: AssignmentStatus.Rejected })
    {
        <p class="pill bad">Demande refusée</p>
    }
    else if (_asg is { Status: AssignmentStatus.Confirmed })
    {
        if (_ts is { Status: TimesheetStatus.InProgress })
        { <NavLink class="btn ghost" href="@($"e/events/{_e.Id}/active")">Prestation en cours</NavLink> }
        else if (_ts is { Status: TimesheetStatus.ToSend })
        { <NavLink class="btn primary" href="@($"e/events/{_e.Id}/recap")">Finaliser & envoyer</NavLink> }
        else if (_e.Date == DateOnly.FromDateTime(DateTime.Today))
        { <button class="btn primary" @onclick="Start">Commencer la prestation</button> }
        else
        { <p class="pill ok">Tu es confirmé(e) sur cet event</p> }
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";
    ServiceEvent? _e;
    Assignment? _asg;
    Timesheet? _ts;
    HashSet<string> _myRoleIds = new();
    Dictionary<string,string> _roles = new();

    protected override async Task OnInitializedAsync() => await Load();
    async Task Load()
    {
        var u = Auth.CurrentUser!;
        _myRoleIds = u.JobRoleIds.ToHashSet();
        _e = await Data.GetEventAsync(Id);
        _roles = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
        _asg = (await Data.GetAssignmentsForAccountAsync(u.Id)).FirstOrDefault(a => a.EventId == Id);
        if (_asg is not null) _ts = await Data.GetTimesheetForAssignmentAsync(_asg.Id);
    }
    string RoleName(string id) => _roles.GetValueOrDefault(id, "?");

    async Task RequestJoin()
    {
        var u = Auth.CurrentUser!;
        var role = _e!.RoleNeeds.FirstOrDefault(r => _myRoleIds.Contains(r.JobRoleId))?.JobRoleId
                   ?? u.JobRoleIds.First();
        await Data.CreateAssignmentAsync(new Assignment {
            EventId = Id, AccountId = u.Id, JobRoleId = role,
            Source = AssignmentSource.SelfRequest, Status = AssignmentStatus.PendingApproval });
        await Load();
    }

    async Task Start()
    {
        _ts ??= await Data.CreateTimesheetAsync(new Timesheet { AssignmentId = _asg!.Id });
        _ts.StartedAt = DateTime.Now; _ts.Status = TimesheetStatus.InProgress;
        await Data.UpdateTimesheetAsync(_ts);
        Nav.NavigateTo($"e/events/{Id}/active");
    }
}
```

- [ ] **Step 3: Build + manual check + commit**

Run: `dotnet build` → succeeded.
As Marc D. → Events shows Gala in "Mes events" and Cocktail in "Events ouverts" (his self-request is pending → opening Cocktail shows "Demande en attente"). As Sarah K. open today's event → "Prestation en cours".
```bash
git add -A && git commit -m "feat: employee events list + detail with join request"
```

---

### Task 23: Employee Active prestation + Recap (`/e/events/{id}/active`, `/recap`)

**Files:**
- Create: `src/AdminTaos/Pages/Employee/EActive.razor`, `ERecap.razor`

- [ ] **Step 1: Create active (running) screen**

`src/AdminTaos/Pages/Employee/EActive.razor`:
```razor
@page "/e/events/{Id}/active"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar />
@if (_e is not null && _ts is not null)
{
    <div class="lab">Prestation en cours</div>
    <div class="card glow">
        <div class="t">@_e.Name</div>
        <div class="m">@_e.Venue · @RoleName</div>
    </div>
    <Chrono Since="_ts.StartedAt!.Value" />
    <div class="live">● EN COURS — DÉBUT @_ts.StartedAt.Fmt()</div>
    <button class="btn ghost" @onclick="Finish">Terminer la prestation</button>
}

@code {
    [Parameter] public string Id { get; set; } = "";
    ServiceEvent? _e;
    Timesheet? _ts;
    string RoleName = "";

    protected override async Task OnInitializedAsync()
    {
        var u = Auth.CurrentUser!;
        _e = await Data.GetEventAsync(Id);
        var asg = (await Data.GetAssignmentsForAccountAsync(u.Id)).FirstOrDefault(a => a.EventId == Id);
        if (asg is not null)
        {
            _ts = await Data.GetTimesheetForAssignmentAsync(asg.Id);
            RoleName = (await Data.GetJobRolesAsync())
                .FirstOrDefault(r => r.Id == asg.JobRoleId)?.Name ?? "";
        }
    }

    async Task Finish()
    {
        _ts!.EndedAt = DateTime.Now;
        _ts.Status = TimesheetStatus.ToSend;
        await Data.UpdateTimesheetAsync(_ts);
        Nav.NavigateTo($"e/events/{Id}/recap");
    }
}
```

- [ ] **Step 2: Create recap + send screen**

`src/AdminTaos/Pages/Employee/ERecap.razor`:
```razor
@page "/e/events/{Id}/recap"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar ShowBack="true" Title="Prestation terminée" BackTo="e" />
@if (_e is not null && _ts is not null)
{
    <div class="lab">Récapitulatif</div>
    <div class="card glow">
        <div class="t">@_e.Name</div>
        <div class="m">@_e.Date.Fmt() · @RoleName</div>
    </div>
    <div class="chrono" style="font-size:34px">@_ts.Duration.Fmt()</div>
    <div class="field">Début <b>@_ts.StartedAt.Fmt()</b></div>
    <div class="field">Fin <b>@_ts.EndedAt.Fmt()</b></div>
    <span class="@_ts.Status.PillClass()">@_ts.Status.StatusFr()</span>

    @if (_ts.Status == TimesheetStatus.ToSend)
    {
        <button class="btn primary" style="margin-top:16px" @onclick="Send">Envoyer la timesheet</button>
        <p class="m" style="text-align:center;margin-top:8px">Le manager la recevra pour validation.</p>
    }
    else
    {
        <p class="pill ok" style="margin-top:16px">Timesheet envoyée ✓</p>
        <NavLink class="btn ghost" href="e/hours">Voir mes heures</NavLink>
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";
    ServiceEvent? _e;
    Timesheet? _ts;
    string RoleName = "";

    protected override async Task OnInitializedAsync()
    {
        var u = Auth.CurrentUser!;
        _e = await Data.GetEventAsync(Id);
        var asg = (await Data.GetAssignmentsForAccountAsync(u.Id)).FirstOrDefault(a => a.EventId == Id);
        if (asg is not null)
        {
            _ts = await Data.GetTimesheetForAssignmentAsync(asg.Id);
            RoleName = (await Data.GetJobRolesAsync())
                .FirstOrDefault(r => r.Id == asg.JobRoleId)?.Name ?? "";
        }
    }

    async Task Send()
    {
        _ts!.Status = TimesheetStatus.Sent;
        _ts.SentAt = DateTime.Now;
        await Data.UpdateTimesheetAsync(_ts);
    }
}
```

- [ ] **Step 3: Build + manual check + commit**

Run: `dotnet build` → succeeded.
As Sarah K. → home "Reprendre" → Active shows live chrono ticking → "Terminer" → Recap shows duration/start/end → "Envoyer la timesheet" → confirmation pill.
```bash
git add -A && git commit -m "feat: employee active prestation + recap/send"
```

---

### Task 24: Employee My Hours + timesheet detail (`/e/hours`, `/e/hours/{id}`)

**Files:**
- Create: `src/AdminTaos/Pages/Employee/EHours.razor`, `ETimesheetDetail.razor`

- [ ] **Step 1: Create history list**

`src/AdminTaos/Pages/Employee/EHours.razor`:
```razor
@page "/e/hours"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth

<TopBar />
<h1>Mes heures</h1>

@if (_rows.Count == 0)
{
    <div class="empty"><div class="ic">◷</div><div class="e-t">Aucune prestation</div>
        <p>Tes timesheets apparaîtront ici.</p></div>
}
@foreach (var r in _rows)
{
    <NavLink class="card" href="@($"e/hours/{r.ts.Id}")">
        <div class="t">@r.ev.Name</div>
        <div class="m">@r.ev.Date.Fmt() · @r.ts.Duration.Fmt()
            · <span class="@r.ts.Status.PillClass()">@r.ts.Status.StatusFr()</span></div>
    </NavLink>
}

@code {
    List<(Timesheet ts, ServiceEvent ev)> _rows = new();
    protected override async Task OnInitializedAsync()
    {
        var u = Auth.CurrentUser!;
        var asgs = await Data.GetAssignmentsForAccountAsync(u.Id);
        var events = await Data.GetEventsAsync();
        foreach (var a in asgs)
        {
            var ts = await Data.GetTimesheetForAssignmentAsync(a.Id);
            var ev = events.FirstOrDefault(e => e.Id == a.EventId);
            if (ts is not null && ev is not null) _rows.Add((ts, ev));
        }
        _rows = _rows.OrderByDescending(r => r.ev.Date).ToList();
    }
}
```

- [ ] **Step 2: Create timesheet detail (read-only + resend if rejected)**

`src/AdminTaos/Pages/Employee/ETimesheetDetail.razor`:
```razor
@page "/e/hours/{Id}"
@layout EmployeeLayout
@inject IDataService Data

@if (_ts is null) { <TopBar ShowBack="true" Title="Timesheet" BackTo="e/hours" /><p class="sub">Introuvable.</p> }
else
{
    <TopBar ShowBack="true" Title="Timesheet" BackTo="e/hours" />
    <div class="card glow">
        <div class="t">@_eventName</div>
        <div class="m">Durée : @_ts.Duration.Fmt()</div>
    </div>
    <div class="field">Début <b>@_ts.EffectiveStart.Fmt()</b></div>
    <div class="field">Fin <b>@_ts.EffectiveEnd.Fmt()</b></div>
    <div class="field">Statut <b><span class="@_ts.Status.PillClass()">@_ts.Status.StatusFr()</span></b></div>

    @if (_ts.Status == TimesheetStatus.Rejected)
    {
        <div class="card"><div class="t">Motif du refus</div>
            <div class="m">@(_ts.RejectionReason ?? "Non précisé")</div></div>
        <button class="btn primary" @onclick="Resend">Renvoyer la timesheet</button>
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";
    Timesheet? _ts;
    string _eventName = "";

    protected override async Task OnInitializedAsync() => await Load();
    async Task Load()
    {
        _ts = await Data.GetTimesheetAsync(Id);
        if (_ts is null) return;
        var asgs = await Data.GetAssignmentsAsync();
        var asg = asgs.FirstOrDefault(a => a.Id == _ts.AssignmentId);
        var ev = asg is null ? null : await Data.GetEventAsync(asg.EventId);
        _eventName = ev?.Name ?? "Event";
    }

    async Task Resend()
    {
        _ts!.Status = TimesheetStatus.Sent;
        _ts.SentAt = DateTime.Now;
        _ts.RejectionReason = null;
        await Data.UpdateTimesheetAsync(_ts);
        await Load();
    }
}
```

- [ ] **Step 3: Build + manual check + commit**

Run: `dotnet build` → succeeded.
As Marc D. → Mes heures lists the past Mariage timesheet (Sent). After Task 25, a rejected one shows the motif + Renvoyer.
```bash
git add -A && git commit -m "feat: employee my-hours history + timesheet detail"
```

---

### Task 25: Employee Profile (`/e/profile`)

**Files:**
- Create: `src/AdminTaos/Pages/Employee/EProfile.razor`

- [ ] **Step 1: Create the page**

`src/AdminTaos/Pages/Employee/EProfile.razor`:
```razor
@page "/e/profile"
@layout EmployeeLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<TopBar />
<h1>Profil</h1>
<div class="card glow">
    <div class="t">@Auth.CurrentUser?.FullName</div>
    <div class="m">@Auth.CurrentUser?.Email<br/>Rôle(s) : @_roles<br/>
        Statut : <span class="pill ok">Actif</span></div>
</div>
<button class="btn ghost" @onclick="Logout">Se déconnecter</button>

@code {
    string _roles = "";
    protected override async Task OnInitializedAsync()
    {
        var map = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
        _roles = string.Join(", ", (Auth.CurrentUser?.JobRoleIds ?? new())
            .Select(id => map.GetValueOrDefault(id, "?")));
    }
    async Task Logout() { await Auth.LogoutAsync(); Nav.NavigateTo("login", replace:true); }
}
```

- [ ] **Step 2: Build + manual check + commit**

Run: `dotnet build` → succeeded. Employee → Profil shows roles + Actif; Logout works.
```bash
git add -A && git commit -m "feat: employee profile + logout"
```

---

## Phase 5 — Timesheet validation cycle (manager)

### Task 26: Manager Timesheets list (`/m/timesheets`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MTimesheets.razor`

- [ ] **Step 1: Create the list**

`src/AdminTaos/Pages/Manager/MTimesheets.razor`:
```razor
@page "/m/timesheets"
@layout ManagerLayout
@inject IDataService Data

<TopBar ShowBack="true" Title="Timesheets" BackTo="m" />
<h1>Timesheets</h1>
<div class="row2" style="margin-bottom:6px">
    <button class="btn @(_tab=="sent"?"primary":"ghost")" @onclick='() => _tab="sent"'>À valider</button>
    <button class="btn @(_tab=="all"?"primary":"ghost")" @onclick='() => _tab="all"'>Toutes</button>
</div>

@{ var rows = _tab == "sent" ? _rows.Where(r => r.ts.Status == TimesheetStatus.Sent) : _rows; }
@if (!rows.Any())
{
    <div class="empty"><div class="ic">◷</div><div class="e-t">Rien à afficher</div></div>
}
@foreach (var r in rows)
{
    <NavLink class="card" href="@($"m/timesheets/{r.ts.Id}")">
        <div class="t">@r.who — @r.ev.Name</div>
        <div class="m">@r.ev.Date.Fmt() · @r.ts.Duration.Fmt()
            · <span class="@r.ts.Status.PillClass()">@r.ts.Status.StatusFr()</span></div>
    </NavLink>
}

@code {
    string _tab = "sent";
    List<(Timesheet ts, ServiceEvent ev, string who)> _rows = new();

    protected override async Task OnInitializedAsync()
    {
        var ts = await Data.GetTimesheetsAsync();
        var asgs = await Data.GetAssignmentsAsync();
        var events = await Data.GetEventsAsync();
        var accs = (await Data.GetAccountsAsync()).ToDictionary(a => a.Id, a => a.FullName);
        foreach (var t in ts)
        {
            var a = asgs.FirstOrDefault(x => x.Id == t.AssignmentId);
            var ev = a is null ? null : events.FirstOrDefault(e => e.Id == a.EventId);
            if (a is null || ev is null) continue;
            _rows.Add((t, ev, accs.GetValueOrDefault(a.AccountId, "?")));
        }
        _rows = _rows.OrderByDescending(r => r.ev.Date).ToList();
    }
}
```

- [ ] **Step 2: Build + manual check + commit**

Run: `dotnet build` → succeeded. Manager → Accueil → "timesheet(s) à valider" card → list shows the seeded Sent timesheet (Marc D. — Mariage Dupont).
```bash
git add -A && git commit -m "feat: manager timesheets list"
```

---

### Task 27: Manager Timesheet detail — validate/correct/reject (`/m/timesheets/{id}`)

**Files:**
- Create: `src/AdminTaos/Pages/Manager/MTimesheetDetail.razor`
- Test: `tests/AdminTaos.Tests/DataServiceTests.cs` (transition helpers)

- [ ] **Step 1: Failing test for the validate/reject transition**

Append to `DataServiceTests.cs`:
```csharp
[Fact]
public async Task Validate_sets_status_and_timestamp()
{
    var db = New();
    var ts = await db.GetTimesheetAsync("ts-3");
    ts!.Status = TimesheetStatus.Validated;
    ts.ValidatedAt = new System.DateTime(2026,5,17);
    await db.UpdateTimesheetAsync(ts);
    var reloaded = await db.GetTimesheetAsync("ts-3");
    Assert.Equal(TimesheetStatus.Validated, reloaded!.Status);
    Assert.NotNull(reloaded.ValidatedAt);
}

[Fact]
public async Task Manager_adjusted_times_change_duration()
{
    var db = New();
    var ts = await db.GetTimesheetAsync("ts-3");
    ts!.ManagerAdjustedStart = new System.DateTime(2026,5,12,15,30,0);
    ts.ManagerAdjustedEnd   = new System.DateTime(2026,5,12,23,0,0);
    await db.UpdateTimesheetAsync(ts);
    Assert.Equal(System.TimeSpan.FromMinutes(450),
        (await db.GetTimesheetAsync("ts-3"))!.Duration);
}
```

- [ ] **Step 2: Run tests to verify they fail then pass**

Run: `dotnet test --filter Validate_sets_status_and_timestamp`
Expected: PASS immediately (logic already supported by Task 3/6 — these tests lock the behavior the page relies on). If FAIL, fix `Timesheet`/`InMemoryDataService` before continuing.

- [ ] **Step 3: Create the detail page**

`src/AdminTaos/Pages/Manager/MTimesheetDetail.razor`:
```razor
@page "/m/timesheets/{Id}"
@layout ManagerLayout
@inject IDataService Data
@inject NavigationManager Nav

@if (_ts is null) { <TopBar ShowBack="true" Title="Timesheet" BackTo="m/timesheets" /><p class="sub">Introuvable.</p> }
else
{
    <TopBar ShowBack="true" Title="Timesheet" BackTo="m/timesheets" />
    <div class="card glow">
        <div class="t">@_who — @_role</div>
        <div class="m">@_eventName · @_eventDate.Fmt()</div>
    </div>

    <div class="lab">Heures (modifiables)</div>
    <div class="field"><span>Début</span>
        <input type="time" value="@_start.ToString("HH\\:mm")" @onchange="OnStart" /></div>
    <div class="field"><span>Fin</span>
        <input type="time" value="@_end.ToString("HH\\:mm")" @onchange="OnEnd" /></div>
    <div class="field"><span>Durée</span><b style="color:var(--gold)">@Dur()</b></div>
    <span class="@_ts.Status.PillClass()">@_ts.Status.StatusFr()</span>

    @if (_ts.Status is TimesheetStatus.Sent or TimesheetStatus.Validated or TimesheetStatus.Rejected)
    {
        <div class="lab">Refuser — motif</div>
        <div class="field"><span>Motif</span><input @bind="_reason" placeholder="Heures incorrectes…" /></div>
        <div class="row2" style="margin-top:8px">
            <button class="btn danger" @onclick="Reject">Refuser</button>
            <button class="btn primary" @onclick="Validate">Valider</button>
        </div>
        @if (_saved) { <p class="pill ok" style="margin-top:10px">Enregistré ✓</p> }
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";
    Timesheet? _ts;
    string _who = "", _role = "", _eventName = "", _reason = "";
    DateOnly _eventDate;
    DateTime _start, _end;
    bool _saved;

    protected override async Task OnInitializedAsync()
    {
        _ts = await Data.GetTimesheetAsync(Id);
        if (_ts is null) return;
        var asg = (await Data.GetAssignmentsAsync()).FirstOrDefault(a => a.Id == _ts.AssignmentId);
        if (asg is not null)
        {
            _who = (await Data.GetAccountAsync(asg.AccountId))?.FullName ?? "?";
            _role = (await Data.GetJobRolesAsync()).FirstOrDefault(r => r.Id == asg.JobRoleId)?.Name ?? "?";
            var ev = await Data.GetEventAsync(asg.EventId);
            _eventName = ev?.Name ?? "Event";
            _eventDate = ev?.Date ?? default;
        }
        _start = _ts.EffectiveStart ?? DateTime.Today;
        _end   = _ts.EffectiveEnd ?? DateTime.Today;
        _reason = _ts.RejectionReason ?? "";
    }

    void OnStart(ChangeEventArgs e)
    { if (TimeOnly.TryParse(e.Value?.ToString(), out var t)) _start = _start.Date + t.ToTimeSpan(); }
    void OnEnd(ChangeEventArgs e)
    { if (TimeOnly.TryParse(e.Value?.ToString(), out var t)) _end = _end.Date + t.ToTimeSpan(); }
    string Dur() => (_end > _start ? (TimeSpan?)(_end - _start) : null).Fmt();

    async Task Validate()
    {
        _ts!.ManagerAdjustedStart = _start;
        _ts.ManagerAdjustedEnd = _end;
        _ts.Status = TimesheetStatus.Validated;
        _ts.ValidatedAt = DateTime.Now;
        _ts.RejectionReason = null;
        await Data.UpdateTimesheetAsync(_ts);
        _saved = true;
    }

    async Task Reject()
    {
        _ts!.Status = TimesheetStatus.Rejected;
        _ts.RejectionReason = string.IsNullOrWhiteSpace(_reason) ? "Non précisé" : _reason;
        await Data.UpdateTimesheetAsync(_ts);
        _saved = true;
    }
}
```

- [ ] **Step 4: Build + manual check + commit**

Run: `dotnet test --filter DataServiceTests` → PASS. `dotnet build` → succeeded.
Manager → Timesheets → open Marc D./Mariage → change Début/Fin (duration recomputes) → "Valider" → status becomes Validée. Re-open another, "Refuser" with a motif → status Refusée; logging in as that employee shows the motif + Renvoyer (Task 24), which returns it to Sent.
```bash
git add -A && git commit -m "feat: manager timesheet validate/correct/reject"
```

---

## Phase 6 — Polish: empty states, responsive, final pass

### Task 28: EmptyState + SectionLabel components (DRY pass)

**Files:**
- Create: `src/AdminTaos/Components/EmptyState.razor`, `SectionLabel.razor`
- Modify: pages currently inlining `.empty` / `.lab` markup (MEvents, MTeam, MTimesheets, EHours, EEvents) to use the components

- [ ] **Step 1: Create components**

`src/AdminTaos/Components/EmptyState.razor`:
```razor
<div class="empty">
    <div class="ic">@Icon</div>
    <div class="e-t">@Title</div>
    @if (!string.IsNullOrWhiteSpace(Hint)) { <p>@Hint</p> }
</div>
@code {
    [Parameter] public string Icon { get; set; } = "○";
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter] public string? Hint { get; set; }
}
```

`src/AdminTaos/Components/SectionLabel.razor`:
```razor
<div class="lab">@Text</div>
@code { [Parameter, EditorRequired] public string Text { get; set; } = ""; }
```

- [ ] **Step 2: Replace inline usages**

In `MEvents.razor`, `MTeam.razor`, `MTimesheets.razor`, `EHours.razor`, `EEvents.razor`, `MHome.razor`, `EHome.razor`: replace each `<div class="empty">…</div>` block with `<EmptyState Icon="…" Title="…" Hint="…" />` and each `<div class="lab">X</div>` with `<SectionLabel Text="X" />`. Keep behavior identical.

- [ ] **Step 3: Build + commit**

Run: `dotnet build` → succeeded. Spot-check one list with no data (e.g. log in as a freshly registered pending→validated account with no assignments → `/e/events` shows the EmptyState component).
```bash
git add -A && git commit -m "refactor: shared EmptyState + SectionLabel components"
```

---

### Task 29: PWA service worker offline + dark loading screen

**Files:**
- Modify: `src/AdminTaos/wwwroot/index.html` (loading UI), `src/AdminTaos/wwwroot/service-worker.published.js` (cache assets)

- [ ] **Step 1: Dark themed loading + install meta**

In `src/AdminTaos/wwwroot/index.html`, replace the default `<div id="app">Loading...</div>` with:
```html
<div id="app">
  <div style="display:flex;height:100dvh;align-items:center;justify-content:center;
       background:#141414;color:#E7C76B;font-family:Georgia,serif;letter-spacing:3px">
    TAOS
  </div>
</div>
```
Confirm `<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />` is present (add if missing).

- [ ] **Step 2: Ensure fonts/icons are cached offline**

In `service-worker.published.js`, confirm the asset include pattern caches `app.css`, `fonts/`, `icons/`, `manifest.webmanifest` (the template's `onInstall` caches everything in `assetsManifest` — verify those files appear in `wwwroot` so the build includes them; no code change needed if they do). If `assetsManifest` filtering excludes `.woff2`, add `'woff2'` to the cached extensions list.

- [ ] **Step 3: Verify published PWA**

Run:
```bash
dotnet publish src/AdminTaos -c Release -o publish
```
Expected: publish succeeds; `publish/wwwroot` contains `app.css`, `fonts/*.woff2`, `icons/*.png`, `manifest.webmanifest`, `service-worker.js`.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: dark loading screen + offline asset caching"
```

---

### Task 30: Full click-through verification + responsive pass

**Files:** none (verification task) — fix any defects found in the relevant page file and commit per fix.

- [ ] **Step 1: Run the app**

Run: `dotnet run --project src/AdminTaos`; open the URL in the browser; open devtools, toggle device toolbar to **375×812 (mobile)** and verify again at **desktop width**.

- [ ] **Step 2: Walk every flow (checklist)**

Verify each, fixing defects in-place:
- [ ] Splash → Login; 5 quick-login accounts show name·type·role·status.
- [ ] Manager: Accueil counts correct; Events list; create event; edit event; event detail role-fill; assign staff; accept/refuse a join request.
- [ ] Manager: Équipe Actifs/En attente; validate Léa → becomes Active; Roles add/edit/delete; Profil logout.
- [ ] Manager: Timesheets list; open seeded Sent → adjust times (duration recomputes) → Valider; another → Refuser with motif.
- [ ] Employee (Sarah): Accueil shows in-progress event → Reprendre → Chrono ticks → Terminer → Recap → Envoyer.
- [ ] Employee (Marc): Events mine/open; Cocktail shows pending request; Mes heures lists timesheets; rejected one shows motif + Renvoyer → returns to Sent.
- [ ] Employee: Profil logout. Register a new account → "compte en attente" → guard blocks app until manager validates.
- [ ] Route guard: typing `/m/events` while logged out → login; employee typing `/m` → `/e`.
- [ ] No layout overflow/clipping at 375px; bottom nav fixed; safe-area padding present.

- [ ] **Step 3: Commit any fixes**

```bash
git add -A && git commit -m "fix: click-through + responsive defects from verification pass"
```

- [ ] **Step 4: Final full test run**

Run: `dotnet test`
Expected: all tests PASS (ModelTests, DataServiceTests, AuthStateTests, NavigationGuardTests, ComponentTests).

- [ ] **Step 5: Final commit**

```bash
git add -A && git commit -m "chore: MD TAOS ADMIN clickable prototype complete" --allow-empty
```

---

## Self-Review

**Spec coverage check (spec §-by-§):**
- §2 roles/onboarding/roles-mgmt/events/assignment/pointage/validation → Tasks 9–27 ✓
- §3 .NET 10 / Blazor WASM PWA / IDataService swappable / mock auth / localStorage / structure → Tasks 1,4,6,7,9,29 ✓
- §4 roles & route guard → Tasks 10–11 ✓
- §5 data model (Account/JobRole/RoleNeed/ServiceEvent/Assignment/Timesheet + statuses + seed) → Tasks 2,3,5 ✓
- §6 all 23 screens → Tasks 12 (4 auth), 14–20 (11 manager), 21–25 (8 employee) ✓
- §7 core flows → Tasks 12,15,17,18,22,23,26,27 ✓
- §8 edge/empty states → seed (Task 5) + Task 28 + Task 30 checklist ✓
- §9 design system / fonts / PWA manifest / responsive → Tasks 7,29,30 ✓
- §10 DoD (build, click-through, routes, seed states, IDataService sole gate, no runtime network) → Tasks 7,29,30 ✓
- Spec login quick-login test accounts → Task 12 ✓

**Placeholder scan:** No "TBD/TODO/handle edge cases" left; every code step has complete code; no "similar to Task N" — each page carries full markup.

**Type consistency:** `IDataService` signatures (Task 4) match all call sites; `Timesheet.EffectiveStart/EffectiveEnd/Duration` (Task 3) used consistently in Tasks 23,24,27; `BottomNav.NavItem` record used by both layouts (Task 11); `ViewHelpers` extension names (`Fmt`, `StatusFr`, `PillClass`) match usages in Tasks 22–27; `SeedData` constant ids referenced by tests match definitions. No mismatches found.

---

## Execution Handoff

(Provided to the user after saving.)
