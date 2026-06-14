using System.Text.Json;
using AdminTaos.Models;
using Microsoft.JSInterop;

namespace AdminTaos.Services;

public class FirestoreDataService : IDataService
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public FirestoreDataService(IJSRuntime js) { _js = js; }

    // --- Generic helpers ---
    private async Task<List<T>> GetAllAsync<T>(string col)
    {
        var raw = await _js.InvokeAsync<JsonElement>("taos.getAll", col);
        return Deserialize<List<T>>(raw) ?? new();
    }
    private async Task<T?> GetOneAsync<T>(string col, string id)
    {
        var raw = await _js.InvokeAsync<JsonElement?>("taos.getOne", col, id);
        return raw.HasValue ? Deserialize<T>(raw.Value) : default;
    }
    private async Task<List<T>> QueryByFieldAsync<T>(string col, string field, string value)
    {
        var raw = await _js.InvokeAsync<JsonElement>("taos.queryByField", col, field, value);
        return Deserialize<List<T>>(raw) ?? new();
    }
    private Task SetAsync<T>(string col, string id, T data)
        => _js.InvokeVoidAsync("taos.setOne", col, id, Serialize(data)).AsTask();
    private Task UpdateAsync<T>(string col, string id, T data)
        => _js.InvokeVoidAsync("taos.updateOne", col, id, Serialize(data)).AsTask();
    private Task DeleteAsync(string col, string id)
        => _js.InvokeVoidAsync("taos.deleteOne", col, id).AsTask();

    private static T? Deserialize<T>(JsonElement el) => el.Deserialize<T>(JsonOpts);
    private static JsonElement Serialize<T>(T value) => JsonSerializer.SerializeToElement(value, JsonOpts);

    // ---------- Accounts ----------
    public Task<List<Account>>  GetAccountsAsync()                 => GetAllAsync<Account>("accounts");
    public Task<Account?>       GetAccountAsync(string id)         => GetOneAsync<Account>("accounts", id);
    public async Task<Account?> GetAccountByEmailAsync(string email)
    {
        var list = await QueryByFieldAsync<Account>("accounts", "email", email.Trim());
        return list.FirstOrDefault();
    }
    public async Task<Account>  CreateAccountAsync(Account a)      { await SetAsync("accounts", a.Id, a); return a; }
    public Task                 UpdateAccountAsync(Account a)      => SetAsync("accounts", a.Id, a);

    // ---------- Job roles ----------
    public Task<List<JobRole>>  GetJobRolesAsync()                  => GetAllAsync<JobRole>("jobRoles");
    public async Task<JobRole>  CreateJobRoleAsync(JobRole r)       { await SetAsync("jobRoles", r.Id, r); return r; }
    public Task                 UpdateJobRoleAsync(JobRole r)       => SetAsync("jobRoles", r.Id, r);
    public Task                 DeleteJobRoleAsync(string id)       => DeleteAsync("jobRoles", id);

    // ---------- Events ----------
    public async Task<List<ServiceEvent>> GetEventsAsync()
    {
        var list = await GetAllAsync<ServiceEvent>("events");
        return list.OrderBy(e => e.Date).ToList();
    }
    public Task<ServiceEvent?>  GetEventAsync(string id)            => GetOneAsync<ServiceEvent>("events", id);
    public async Task<ServiceEvent> CreateEventAsync(ServiceEvent e){ await SetAsync("events", e.Id, e); return e; }
    public Task                 UpdateEventAsync(ServiceEvent e)    => SetAsync("events", e.Id, e);

    // ---------- Assignments ----------
    public Task<List<Assignment>> GetAssignmentsAsync()              => GetAllAsync<Assignment>("assignments");
    public Task<List<Assignment>> GetAssignmentsForEventAsync(string eventId)
        => QueryByFieldAsync<Assignment>("assignments", "eventId", eventId);
    public Task<List<Assignment>> GetAssignmentsForAccountAsync(string accountId)
        => QueryByFieldAsync<Assignment>("assignments", "accountId", accountId);
    public async Task<Assignment> CreateAssignmentAsync(Assignment a){ await SetAsync("assignments", a.Id, a); return a; }
    public Task                  UpdateAssignmentAsync(Assignment a) => SetAsync("assignments", a.Id, a);

    // ---------- Timesheets ----------
    public Task<List<Timesheet>> GetTimesheetsAsync()                => GetAllAsync<Timesheet>("timesheets");
    public Task<Timesheet?>      GetTimesheetAsync(string id)        => GetOneAsync<Timesheet>("timesheets", id);
    public async Task<Timesheet?> GetTimesheetForAssignmentAsync(string assignmentId)
    {
        var list = await QueryByFieldAsync<Timesheet>("timesheets", "assignmentId", assignmentId);
        return list.FirstOrDefault();
    }
    public async Task<Timesheet> CreateTimesheetAsync(Timesheet t)   { await SetAsync("timesheets", t.Id, t); return t; }
    public Task                  UpdateTimesheetAsync(Timesheet t)   => SetAsync("timesheets", t.Id, t);
}
