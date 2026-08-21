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

    public Task DeleteAssignmentAsync(string id)
    { _assignments.RemoveAll(a => a.Id == id); return Task.CompletedTask; }

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
