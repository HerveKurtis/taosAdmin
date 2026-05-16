using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class NavigationGuardTests
{
    static readonly Account Mgr   = new(){ Type=AccountType.Manager,  Status=AccountStatus.Active };
    static readonly Account Emp   = new(){ Type=AccountType.Employee, Status=AccountStatus.Active };
    static readonly Account Pend  = new(){ Type=AccountType.Employee, Status=AccountStatus.Pending };
    static readonly Account Rej   = new(){ Type=AccountType.Employee, Status=AccountStatus.Rejected };

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
