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

    // Profiles — fiche partagée, lisible par tout compte actif
    Task<List<Profile>> GetProfilesAsync();
    Task UpsertProfileAsync(Profile p);
    Task DeleteProfileAsync(string id);

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
    Task DeleteAssignmentAsync(string id);

    // Timesheets
    Task<List<Timesheet>> GetTimesheetsAsync();
    Task<Timesheet?> GetTimesheetAsync(string id);
    Task<Timesheet?> GetTimesheetForAssignmentAsync(string assignmentId);
    Task<Timesheet> CreateTimesheetAsync(Timesheet t);
    Task UpdateTimesheetAsync(Timesheet t);
}
