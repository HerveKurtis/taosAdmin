namespace AdminTaos.Models;

public enum AccountType { Manager, Employee }
public enum AccountStatus { Pending, Active, Rejected }
public enum EventStatus { Upcoming, InProgress, Past }
public enum AssignmentSource { AssignedByManager, SelfRequest }
public enum AssignmentStatus { PendingApproval, Confirmed, Rejected }
public enum TimesheetStatus { NotStarted, InProgress, ToSend, Sent, Validated, Rejected }
