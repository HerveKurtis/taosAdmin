# MD TAOS ADMIN — Dashboard Reskin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reskin the existing, fully-working MD TAOS ADMIN Blazor WASM prototype from a phone-app shell into a responsive **dashboard website** (dark gold sidebar + light content), without changing any behavior, route, data, or business logic.

**Architecture:** Pure presentation change. Rewrite the design system (`app.css`) to a light/pro + gold theme; replace the phone shell + bottom-nav with a single `DashboardLayout` (dark sidebar + light topbar, responsive: full sidebar ≥1024px → icon rail 768–1023px → hamburger drawer <768px); auth pages get a centered light `AuthShell`. Introduce `ResponsiveTable` (table on desktop/tablet, stacked cards on mobile, CSS-only) and `StatCard`. Every page keeps its existing `@code` block byte-for-byte; only the markup above `@code` is reworked, plus shift-flow copy is upgraded.

**Tech Stack:** Existing .NET 10 Blazor WebAssembly, bUnit/xUnit. No new packages.

**Spec:** `docs/superpowers/specs/2026-05-17-md-taos-admin-dashboard-reskin-design.md` (and base spec `2026-05-16-md-taos-admin-design.md`).

**Hard guarantees:**
- No `@code` block of any page is modified (no logic/route/data change). Only markup + copy.
- All **29 tests stay green with ZERO test-file edits**. To guarantee this, the reskin **preserves these markup anchors**: Login quick-login buttons keep `class="card quick"`; `Stepper.razor` keeps `<div class="stepper">` with exactly two `<button>` (− then +); `Chrono.razor` keeps `<div class="chrono">`. `app.css` **restyles** these classes, never renames them.
- `dotnet build` (solution) 0 errors; `dotnet test` 29/29 at every commit.

**Repo state:** branch work happens per the execution skill. Solution = `AdminTaos.slnx` (both `src/AdminTaos/AdminTaos.csproj` + `tests/AdminTaos.Tests/AdminTaos.Tests.csproj`). Build/test from repo root: `dotnet build` / `dotnet test`. The test project references the app via ProjectReference — do NOT run `dotnet sln add` (it side-effect-strips that reference on .NET 10 .slnx; if a build ever fails with CS0246 for app types, restore the test csproj `<ProjectReference Include="..\..\src\AdminTaos\AdminTaos.csproj" />` from git).

---

## File Structure

```
src/AdminTaos/
  wwwroot/app.css                 REWRITTEN — light dashboard design system (same class names)
  wwwroot/index.html              MOD — light loading splash + theme-color #F4F5F7
  wwwroot/manifest.webmanifest    MOD — theme_color #F4F5F7
  App.razor                       MOD — DefaultLayout = AuthShell
  Layout/AuthShell.razor          NEW — centered light auth panel (no sidebar)
  Layout/DashboardLayout.razor    NEW — sidebar + topbar + responsive drawer (role-aware menu)
  Layout/AuthLayout.razor         DELETE
  Layout/ManagerLayout.razor      DELETE
  Layout/EmployeeLayout.razor     DELETE
  Components/Sidebar.razor        NEW — dark gold nav (role menu, collapsed/drawer states)
  Components/Topbar.razor         NEW — light bar: hamburger(mobile)+page title+user
  Components/ResponsiveTable.razor NEW — generic table↔card list
  Components/StatCard.razor       NEW — big number + label, optional NavLink
  Components/TopBar.razor         DELETE  (replaced; pages stop using it)
  Components/BottomNav.razor      DELETE
  Components/{Stepper,ToggleSwitch,Chrono,EmptyState,SectionLabel}.razor  UNCHANGED
  Pages/Auth/*.razor              MOD markup (use AuthShell content; drop <TopBar>)
  Pages/Manager/*.razor           MOD markup (@layout DashboardLayout; drop <TopBar>; tables/stat-cards/panels)
  Pages/Employee/*.razor          MOD markup (idem) + §3 shift copy upgrade
tests/AdminTaos.Tests/*           UNCHANGED (anchors preserved)
```

Each new component has one responsibility. Pages stay thin (markup only; logic untouched).

---

## Phase 1 — Light dashboard design system

### Task 1: Rewrite `app.css`

**Files:** Modify (full rewrite): `src/AdminTaos/wwwroot/app.css`

- [ ] **Step 1: Replace the entire file** with EXACTLY:

```css
@font-face{font-family:'Playfair';src:url('fonts/playfair-700.woff2') format('woff2');font-weight:700;font-display:swap}
@font-face{font-family:'Inter';src:url('fonts/inter-var.woff2') format('woff2');font-weight:400 700;font-display:swap}

:root{
  --bg:#F4F5F7; --surface:#FFFFFF; --line:#E6E8EC; --line2:#EEF0F3;
  --txt:#1F2329; --mut:#6B7280;
  --gold:#B8902F; --gold2:#C2A14D; --goldbg:rgba(194,161,77,.13);
  --side:#17181B; --side2:#1F2024; --sidetxt:#C9CDD4; --sidegold:#E7C76B;
  --ok:#2E7D32; --okbg:#E6F4EA; --warn:#B8902F; --warnbg:#FFF4DE;
  --bad:#C0392B; --badbg:#FBEAEA;
  --pf:'Playfair',Georgia,serif; --sf:'Inter',system-ui,sans-serif;
  --side-w:240px; --rail-w:64px;
}
*{box-sizing:border-box;margin:0;padding:0}
html,body{height:100%}
body{background:var(--bg);color:var(--txt);font-family:var(--sf);
  -webkit-font-smoothing:antialiased;font-size:14px}
a{color:var(--gold);text-decoration:none}
h1{font-family:var(--pf);font-weight:700;font-size:22px;margin-bottom:2px}
.sub{font-size:13px;color:var(--mut);margin-bottom:18px}
.lab{font-size:11px;letter-spacing:.6px;text-transform:uppercase;color:var(--mut);
  font-weight:600;margin:22px 0 10px}

/* ---------- Dashboard shell ---------- */
.layout{display:flex;min-height:100dvh}
.side{width:var(--side-w);flex:0 0 var(--side-w);background:var(--side);
  color:var(--sidetxt);display:flex;flex-direction:column;padding:18px 12px;
  position:fixed;inset:0 auto 0 0;z-index:40;transition:transform .22s ease}
.side .wm{font-family:var(--pf);font-weight:700;letter-spacing:3px;font-size:22px;
  color:var(--sidegold);padding:6px 12px 20px;white-space:nowrap;overflow:hidden}
.side nav{display:flex;flex-direction:column;gap:3px;flex:1}
.side a.nav{display:flex;align-items:center;gap:12px;padding:11px 12px;border-radius:10px;
  color:var(--sidetxt);font-weight:500;opacity:.78;white-space:nowrap;overflow:hidden}
.side a.nav .i{width:18px;text-align:center;flex:0 0 18px;font-size:15px}
.side a.nav:hover{opacity:1;background:var(--side2)}
.side a.nav.active{opacity:1;background:rgba(231,199,107,.15);color:var(--sidegold);font-weight:600}
.side .foot{margin-top:auto;font-size:12px;color:#7c8088;padding:12px;white-space:nowrap;overflow:hidden}
.main{flex:1;margin-left:var(--side-w);display:flex;flex-direction:column;min-width:0}
.topbar{height:60px;flex:0 0 60px;background:var(--surface);border-bottom:1px solid var(--line);
  display:flex;align-items:center;gap:14px;padding:0 24px;position:sticky;top:0;z-index:20}
.topbar .ttl{font-family:var(--pf);font-weight:700;font-size:18px;flex:1;min-width:0;
  overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.topbar .usr{display:flex;align-items:center;gap:10px;color:var(--mut);font-size:13px;white-space:nowrap}
.topbar .av{width:32px;height:32px;border-radius:50%;background:var(--goldbg);color:var(--gold);
  display:flex;align-items:center;justify-content:center;font-weight:700;font-size:12px}
.burger{display:none;background:none;border:0;color:var(--txt);font-size:20px;cursor:pointer;
  padding:4px 6px;line-height:1}
.scrim{display:none;position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:30}
.content{padding:24px;flex:1;width:100%;max-width:1200px}

/* ---------- Auth shell ---------- */
.auth-wrap{min-height:100dvh;display:flex;align-items:center;justify-content:center;padding:24px}
.auth-card{background:var(--surface);border:1px solid var(--line);border-radius:16px;
  padding:30px 26px;width:100%;max-width:420px;box-shadow:0 10px 40px rgba(20,25,40,.06)}
.auth-card .wm{font-family:var(--pf);font-weight:700;letter-spacing:4px;font-size:28px;
  color:var(--gold);text-align:center;display:block;margin-bottom:4px}
.auth-card .tag{text-align:center;color:var(--mut);font-size:11px;letter-spacing:3px;margin-bottom:22px}

/* ---------- Cards / panels ---------- */
.panel{background:var(--surface);border:1px solid var(--line);border-radius:14px;
  padding:20px;max-width:640px}
.panel+.panel{margin-top:14px}
.card{background:var(--surface);border:1px solid var(--line);border-radius:12px;padding:15px;
  margin-bottom:10px;display:block;width:100%;text-align:left;color:inherit;cursor:pointer}
.card:hover{border-color:#d8c9a0}
.card.glow{background:var(--surface)}
.card .t{font-family:var(--pf);font-weight:700;font-size:15px;color:var(--txt);margin-bottom:4px}
.card .m{font-size:12.5px;color:var(--mut);line-height:1.7}

/* ---------- Stat cards grid ---------- */
.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:14px;margin-bottom:8px}
.stat{background:var(--surface);border:1px solid var(--line);border-radius:12px;padding:16px 18px;
  display:block;color:inherit;cursor:pointer}
.stat:hover{border-color:#d8c9a0}
.stat .n{font-family:var(--pf);font-weight:700;font-size:26px;color:var(--gold);line-height:1}
.stat .l{font-size:12px;color:var(--mut);margin-top:6px}

/* ---------- Buttons ---------- */
.btn{display:inline-flex;align-items:center;justify-content:center;gap:6px;
  padding:11px 16px;border-radius:10px;font-weight:600;font-size:13.5px;border:1px solid transparent;
  cursor:pointer;font-family:var(--sf);text-decoration:none}
.btn.block{display:flex;width:100%}
.btn.primary{background:linear-gradient(135deg,#C2A14D,#B8902F);color:#fff}
.btn.primary:hover{filter:brightness(1.04)}
.btn.ghost{background:var(--surface);color:var(--txt);border-color:var(--line)}
.btn.ghost:hover{border-color:#d8c9a0}
.btn.danger{background:#fff;color:var(--bad);border-color:#e7c3c0}
.btn+.btn{margin-top:8px}
.row2{display:flex;gap:8px}.row2 .btn{flex:1;margin-top:0}
.actions{display:flex;flex-wrap:wrap;gap:8px;margin-top:16px}
.actions .btn{margin-top:0}
.backlink{display:inline-flex;align-items:center;gap:5px;color:var(--mut);font-size:13px;
  font-weight:600;margin-bottom:14px;cursor:pointer;background:none;border:0;padding:0}
.backlink:hover{color:var(--gold)}

/* ---------- Fields ---------- */
.field{display:flex;justify-content:space-between;align-items:center;font-size:13px;
  padding:12px 0;border-bottom:1px solid var(--line2);color:var(--mut);gap:12px}
.field:last-child{border-bottom:0}
.field b{color:var(--txt);font-weight:600;text-align:right}
.field input,.field select{background:#fff;border:1px solid var(--line);color:var(--txt);
  border-radius:8px;padding:9px 10px;font-family:var(--sf);font-size:13px;max-width:62%}

/* ---------- Pills ---------- */
.pill{display:inline-block;font-size:11px;font-weight:600;padding:4px 10px;border-radius:20px;
  background:var(--goldbg);color:var(--gold)}
.pill.ok{background:var(--okbg);color:var(--ok)}
.pill.bad{background:var(--badbg);color:var(--bad)}
.pill.wait{background:#EEF0F3;color:var(--mut)}

/* ---------- Stepper / Toggle / Chrono (names preserved for tests) ---------- */
.stepper{display:flex;align-items:center;gap:10px}
.stepper button{width:30px;height:30px;border-radius:8px;background:#fff;border:1px solid var(--line);
  color:var(--gold);font-size:16px;cursor:pointer}
.stepper button:hover{border-color:#d8c9a0}
.stepper b{min-width:54px;text-align:center;color:var(--txt)}
.toggle{width:44px;height:24px;border-radius:20px;border:0;cursor:pointer;background:#D7DBE0;
  position:relative;transition:.15s}
.toggle.on{background:var(--gold2)}
.toggle::after{content:'';position:absolute;top:2px;left:2px;width:20px;height:20px;border-radius:50%;
  background:#fff;transition:.15s;box-shadow:0 1px 3px rgba(0,0,0,.2)}
.toggle.on::after{left:22px}
.chrono{font-family:var(--pf);font-weight:700;font-size:52px;color:var(--gold);text-align:center;
  margin:14px 0 4px;letter-spacing:1px}
.live{text-align:center;font-size:11px;color:var(--ok);letter-spacing:2px;margin-bottom:18px;font-weight:600}

/* ---------- Responsive data table ---------- */
.rt{width:100%;background:var(--surface);border:1px solid var(--line);border-radius:12px;
  overflow:hidden;border-collapse:collapse;font-size:13px}
.rt thead th{text-align:left;padding:12px 16px;background:#FAFBFC;color:var(--mut);font-weight:600;
  font-size:10.5px;letter-spacing:.6px;text-transform:uppercase;border-bottom:1px solid var(--line)}
.rt tbody td{padding:13px 16px;border-bottom:1px solid var(--line2);vertical-align:middle}
.rt tbody tr:last-child td{border-bottom:0}
.rt tbody tr.clickable{cursor:pointer}
.rt tbody tr.clickable:hover{background:#FAFAF7}
.rt td b{font-weight:600;color:var(--txt)}
.rt-empty{padding:42px 16px;text-align:center;color:var(--mut)}
.rt-empty .ic{font-size:30px;color:#d8c9a0;display:block;margin-bottom:8px}
.rt-empty .e-t{font-family:var(--pf);font-size:15px;color:var(--txt);margin-bottom:3px}

/* ---------- Empty state (name preserved) ---------- */
.empty{text-align:center;color:var(--mut);padding:46px 16px}
.empty .ic{font-size:30px;color:#d8c9a0;margin-bottom:10px}
.empty .e-t{font-family:var(--pf);font-size:16px;color:var(--txt);margin-bottom:4px}

/* ---------- Tablet: icon rail ---------- */
@media (max-width:1023px) and (min-width:768px){
  :root{--side-w:var(--rail-w)}
  .side{padding:18px 8px}
  .side .wm{font-size:0;padding:6px 0 18px;text-align:center}
  .side .wm::after{content:'T';font-size:22px}
  .side a.nav{justify-content:center;padding:11px 0}
  .side a.nav span.lbl{display:none}
  .side a.nav .i{flex:0 0 auto}
  .side .foot{display:none}
}

/* ---------- Mobile: drawer ---------- */
@media (max-width:767px){
  .main{margin-left:0}
  .side{transform:translateX(-100%);width:var(--rail-w);width:260px}
  .layout.drawer .side{transform:translateX(0)}
  .layout.drawer .scrim{display:block}
  .burger{display:inline-flex}
  .content{padding:16px;max-width:100%}
  .topbar{padding:0 16px}
  .panel{padding:16px}
  .field input,.field select{max-width:55%}
  /* table → stacked cards */
  .rt,.rt thead,.rt tbody,.rt tr,.rt td{display:block;width:100%}
  .rt{border:0;background:transparent}
  .rt thead{display:none}
  .rt tbody tr{background:var(--surface);border:1px solid var(--line);border-radius:12px;
    padding:12px 14px;margin-bottom:10px}
  .rt tbody tr.clickable:hover{background:var(--surface)}
  .rt tbody td{border:0;padding:5px 0;display:flex;justify-content:space-between;gap:14px;
    font-size:13px}
  .rt tbody td::before{content:attr(data-label);color:var(--mut);font-size:11px;
    text-transform:uppercase;letter-spacing:.5px;font-weight:600;flex:0 0 auto}
  .rt tbody td[data-label=""]{display:block}
  .rt tbody td[data-label=""]::before{display:none}
}
@media (min-width:1024px){ .burger{display:none} }
```

- [ ] **Step 2: Build + tests still green**

Run: `dotnet build` → Expected: Build succeeded, 0 errors.
Run: `dotnet test` → Expected: Passed 29/0 (CSS-only change; component tests rely on class names which are all preserved: `.stepper`+buttons, `.chrono`, `.card`/`.card.quick`).

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/wwwroot/app.css
git commit -m "reskin: light dashboard design system (app.css rewrite, class names preserved)"
```

---

## Phase 2 — Dashboard shell (sidebar + topbar + drawer) & auth shell

### Task 2: `Sidebar` + `Topbar` components

**Files:** Create `src/AdminTaos/Components/Sidebar.razor`, `src/AdminTaos/Components/Topbar.razor`

- [ ] **Step 1: Create `src/AdminTaos/Components/Sidebar.razor`**

```razor
@inject AuthState Auth

<aside class="side">
    <span class="wm">TAOS</span>
    <nav>
        @foreach (var i in Items)
        {
            <NavLink class="nav" href="@i.Href" Match="@(i.Href.Length<=2?NavLinkMatch.All:NavLinkMatch.Prefix)"
                     @onclick="OnNavigate">
                <span class="i">@i.Icon</span><span class="lbl">@i.Label</span>
            </NavLink>
        }
    </nav>
    <div class="foot">@Auth.CurrentUser?.FullName · @RoleLabel</div>
</aside>

@code {
    public record NavItem(string Href, string Icon, string Label);
    [Parameter] public EventCallback OnNavigate { get; set; }

    string RoleLabel => Auth.CurrentUser?.Type == AccountType.Manager ? "Manager" : "Employé";

    List<NavItem> Items => Auth.CurrentUser?.Type == AccountType.Manager
        ? new()
          {
              new("/m","▦","Tableau de bord"), new("/m/events","◷","Events"),
              new("/m/team","◴","Équipe"), new("/m/timesheets","≣","Timesheets"),
              new("/m/roles","✦","Rôles"), new("/m/profile","○","Profil"),
          }
        : new()
          {
              new("/e","▦","Tableau de bord"), new("/e/events","◷","Events"),
              new("/e/hours","≣","Mes heures"), new("/e/profile","○","Profil"),
          };
}
```

- [ ] **Step 2: Create `src/AdminTaos/Components/Topbar.razor`**

```razor
<header class="topbar">
    <button class="burger" @onclick="OnBurger" aria-label="Menu">☰</button>
    <span class="ttl">@Title</span>
    <span class="usr">@User<span class="av">@Initials</span></span>
</header>

@code {
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string User { get; set; } = "";
    [Parameter] public EventCallback OnBurger { get; set; }
    string Initials => string.Concat((User ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(p => char.ToUpper(p[0]))) is { Length: > 0 } s ? s : "·";
}
```

- [ ] **Step 3: Build (components not yet used — must still compile)**

Run: `dotnet build` → Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Components/Sidebar.razor src/AdminTaos/Components/Topbar.razor
git commit -m "reskin: Sidebar + Topbar components"
```

---

### Task 3: `DashboardLayout` + `AuthShell`; delete old layouts/nav; wire App.razor

**Files:**
- Create: `src/AdminTaos/Layout/DashboardLayout.razor`, `src/AdminTaos/Layout/AuthShell.razor`
- Delete: `src/AdminTaos/Layout/AuthLayout.razor`, `ManagerLayout.razor`, `EmployeeLayout.razor`, `src/AdminTaos/Components/BottomNav.razor`, `src/AdminTaos/Components/TopBar.razor`
- Modify: `src/AdminTaos/App.razor`

- [ ] **Step 1: Create `src/AdminTaos/Layout/DashboardLayout.razor`**

```razor
@inherits LayoutComponentBase
@inject AuthState Auth

<div class="layout @(_drawer ? "drawer" : "")">
    <div class="scrim" @onclick="Close"></div>
    <Sidebar OnNavigate="Close" />
    <div class="main">
        <Topbar Title="@PageTitle" User="@(Auth.CurrentUser?.FullName ?? "")" OnBurger="Toggle" />
        <div class="content">@Body</div>
    </div>
</div>

@code {
    [CascadingParameter] public string? PageTitle { get; set; }
    bool _drawer;
    void Toggle() => _drawer = !_drawer;
    void Close() => _drawer = false;
}
```

Note: pages set their title via the existing `<h1>` in their own markup; the topbar
title is optional chrome. To keep it simple and avoid touching `@code`, `PageTitle`
stays null and the topbar shows an empty title slot is acceptable — BUT we want a
title. Use `Microsoft.AspNetCore.Components.NavigationManager`-free approach: the
page's `<h1>` remains the visible title inside content; topbar shows the app section.
Set `PageTitle` from the route via `NavigationManager` in Step 1b:

- [ ] **Step 1b: Replace the `@code` block of `DashboardLayout.razor`** with a route-derived title (no page changes needed):

```razor
@code {
    [Inject] NavigationManager Nav { get; set; } = default!;
    bool _drawer;
    void Toggle() => _drawer = !_drawer;
    void Close() => _drawer = false;

    string PageTitle
    {
        get
        {
            var p = "/" + Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0].TrimEnd('/');
            return p switch
            {
                "/m" or "/e" => "Tableau de bord",
                "/m/events" or "/e/events" => "Events",
                "/m/events/new" => "Nouvel event",
                "/m/team" => "Équipe",
                "/m/timesheets" => "Timesheets",
                "/m/roles" => "Rôles",
                "/m/profile" or "/e/profile" => "Profil",
                "/e/hours" => "Mes heures",
                _ when p.StartsWith("/m/events") => "Event",
                _ when p.StartsWith("/m/team") => "Employé",
                _ when p.StartsWith("/m/timesheets") => "Timesheet",
                _ when p.StartsWith("/e/events") && p.EndsWith("/active") => "Shift en cours",
                _ when p.StartsWith("/e/events") && p.EndsWith("/recap") => "Récapitulatif",
                _ when p.StartsWith("/e/events") => "Event",
                _ when p.StartsWith("/e/hours") => "Timesheet",
                _ => "TAOS"
            };
        }
    }
}
```
And remove the `[CascadingParameter] PageTitle` + the `<div class="layout ...">` stays as in Step 1 (it references `PageTitle` which is now this computed property). Final file = Step 1 markup + Step 1b `@code`.

- [ ] **Step 2: Create `src/AdminTaos/Layout/AuthShell.razor`**

```razor
@inherits LayoutComponentBase
<div class="auth-wrap">
    <div class="auth-card">
        <span class="wm">TAOS</span>
        <div class="tag">THE ART OF SERVICE</div>
        @Body
    </div>
</div>
```

- [ ] **Step 3: Delete old layouts + nav components**

```bash
git rm src/AdminTaos/Layout/AuthLayout.razor src/AdminTaos/Layout/ManagerLayout.razor src/AdminTaos/Layout/EmployeeLayout.razor src/AdminTaos/Components/BottomNav.razor src/AdminTaos/Components/TopBar.razor
```

- [ ] **Step 4: Update `src/AdminTaos/App.razor`** — change ONLY the default layout from `AuthLayout` to `AuthShell`. The file becomes EXACTLY:

```razor
@inject AuthState Auth
@inject NavigationGuard Guard
@inject NavigationManager Nav

<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(AuthShell)" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(AuthShell)"><p>Page introuvable.</p></LayoutView>
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

- [ ] **Step 5: Build — expect failures in pages still referencing `ManagerLayout`/`EmployeeLayout`/`TopBar`**

Run: `dotnet build`
Expected: FAIL — pages still have `@layout ManagerLayout` / `<TopBar>`. This is expected; Phases 4–6 migrate every page. Do NOT fix pages here.

- [ ] **Step 6: Commit (build intentionally red until pages migrate)**

```bash
git add -A
git commit -m "reskin: DashboardLayout + AuthShell; remove phone-shell layouts & bottom-nav"
```

---

## Phase 3 — ResponsiveTable + StatCard

### Task 4: `ResponsiveTable` + `StatCard`

**Files:** Create `src/AdminTaos/Components/ResponsiveTable.razor`, `src/AdminTaos/Components/StatCard.razor`

- [ ] **Step 1: Create `src/AdminTaos/Components/ResponsiveTable.razor`**

```razor
@typeparam TItem

<table class="rt">
    <thead>
        <tr>
            @foreach (var c in Columns) { <th>@c.Header</th> }
            @if (Action is not null) { <th></th> }
        </tr>
    </thead>
    <tbody>
        @if (!Items.Any())
        {
            <tr><td data-label="" colspan="@(Columns.Count + (Action is null ? 0 : 1))">
                <div class="rt-empty">
                    <span class="ic">@EmptyIcon</span>
                    <div class="e-t">@EmptyTitle</div>
                    @if (!string.IsNullOrWhiteSpace(EmptyHint)) { <div>@EmptyHint</div> }
                </div>
            </td></tr>
        }
        @foreach (var item in Items)
        {
            <tr class="@(RowClick is null ? "" : "clickable")"
                @onclick="@(() => RowClick.HasDelegate ? RowClick.InvokeAsync(item) : Task.CompletedTask)">
                @foreach (var c in Columns)
                {
                    <td data-label="@c.Header">@c.Cell(item)</td>
                }
                @if (Action is not null) { <td data-label="">@Action(item)</td> }
            </tr>
        }
    </tbody>
</table>

@code {
    public record Column(string Header, RenderFragment<TItem> Cell);

    [Parameter, EditorRequired] public IReadOnlyList<TItem> Items { get; set; } = Array.Empty<TItem>();
    [Parameter, EditorRequired] public List<Column> Columns { get; set; } = new();
    [Parameter] public RenderFragment<TItem>? Action { get; set; }
    [Parameter] public EventCallback<TItem> RowClick { get; set; }
    [Parameter] public string EmptyIcon { get; set; } = "○";
    [Parameter] public string EmptyTitle { get; set; } = "Rien à afficher";
    [Parameter] public string? EmptyHint { get; set; }
}
```

- [ ] **Step 2: Create `src/AdminTaos/Components/StatCard.razor`**

```razor
@if (Href is null)
{
    <div class="stat"><div class="n">@Value</div><div class="l">@Label</div></div>
}
else
{
    <NavLink class="stat" href="@Href"><div class="n">@Value</div><div class="l">@Label</div></NavLink>
}

@code {
    [Parameter, EditorRequired] public string Value { get; set; } = "";
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public string? Href { get; set; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: still FAIL (pages not yet migrated) BUT no NEW errors from these two components (they compile). Confirm the only errors are the page `@layout`/`<TopBar>` ones from Task 3 Step 5.

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Components/ResponsiveTable.razor src/AdminTaos/Components/StatCard.razor
git commit -m "reskin: ResponsiveTable + StatCard components"
```

---

## Phase 4 — Manager pages reskin

> For every page task below: **replace ONLY the markup ABOVE the `@code {` line** with the markup shown; **leave the entire `@code { … }` block byte-for-byte identical** to the current committed file (no logic/route/data changes). Remove the `@layout ManagerLayout` and any `<TopBar … />`; add `@layout DashboardLayout`. The markup references the SAME fields/methods that already exist in each page's `@code`.

### Task 5: Manager dashboard (`MHome.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MHome.razor` (markup only)

- [ ] **Step 1: Set the new markup** (everything before `@code {`):

```razor
@page "/m"
@layout DashboardLayout
@inject IDataService Data

<h1>À traiter</h1>
<p class="sub">Tableau de bord manager</p>

<div class="stats">
    <StatCard Value="@_pendingAccounts.ToString()" Label="Comptes à valider" Href="m/team?filter=pending" />
    <StatCard Value="@_joinRequests.ToString()" Label="Demandes de participation" Href="m/events" />
    <StatCard Value="@_pendingTimesheets.ToString()" Label="Timesheets à valider" Href="m/timesheets" />
    <StatCard Value="@_upcoming.Count.ToString()" Label="Events à venir" Href="m/events" />
</div>

<div class="lab">Comptes à valider</div>
<div class="card"><div class="m">@_pendingAccountNames</div></div>

<div class="lab">Prochains events</div>
@if (_upcoming.Count == 0)
{
    <div class="card"><div class="m">Aucun event à venir</div></div>
}
else
{
    <ResponsiveTable TItem="ServiceEvent" Items="_upcoming"
        RowClick="e => Nav.NavigateTo($\"m/events/{e.Id}\")"
        Columns="@(new(){
            new(\"Event\", e => @<b>@e.Name</b>),
            new(\"Date\", e => @<text>@e.Date.Fmt()</text>),
            new(\"Lieu\", e => @<text>@e.Venue</text>) })" />
}
```
Then ADD `@inject NavigationManager Nav` at the top (after `@inject IDataService Data`) — the existing `@code` has no Nav; adding an injected dependency is a markup/directive addition, not a logic change, and is required for `RowClick`. (If `@code` already injected Nav, skip.) Leave `@code` unchanged.

- [ ] **Step 2: Build + test**

Run: `dotnet build` → 0 NEW errors for this file (other unmigrated pages may still error — that's fine until Phase 6 completes; from here track that THIS file compiles).
After ALL Phase 4–6 tasks the full build must be green; within Phase 4 verify incrementally with `dotnet build 2>&1 | grep MHome` returns nothing.

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MHome.razor
git commit -m "reskin: manager dashboard (stat cards + events table)"
```

### Task 6: Manager Events list (`MEvents.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MEvents.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/events"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px">
    <div><h1>Events</h1><p class="sub" style="margin:0">@_events.Count event(s)</p></div>
    <NavLink class="btn primary" href="m/events/new">+ Nouvel event</NavLink>
</div>
<div style="height:16px"></div>

<ResponsiveTable TItem="ServiceEvent" Items="_events"
    EmptyIcon="◷" EmptyTitle="Aucun event" EmptyHint="Crée ton premier event."
    RowClick="e => Nav.NavigateTo($\"m/events/{e.Id}\")"
    Columns="@(new(){
        new(\"Event\", e => @<b>@e.Name</b>),
        new(\"Date\", e => @<text>@e.Date.Fmt()</text>),
        new(\"Lieu\", e => @<text>@e.Venue</text>),
        new(\"Statut\", e => @<span class=\"pill @(e.Status==EventStatus.Past?\"wait\":\"\")\">@StatusFr(e.Status)</span>) })" />
```
(Keep the existing `@code` block — it already has `_events`, `OnInitializedAsync`, and `string StatusFr(EventStatus s)`. Add `@inject NavigationManager Nav` directive if not present.)

- [ ] **Step 2: Build (incremental) + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MEvents.razor
git commit -m "reskin: manager events table"
```

### Task 7: Manager Event detail (`MEventDetail.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MEventDetail.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/events/{Id}"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("m/events")'>‹ Events</button>

@if (_e is null)
{
    <div class="panel"><p class="sub">Introuvable.</p></div>
}
else
{
    <h1>@_e.Name</h1>
    <div class="panel">
        <div class="m">@_e.Venue — @_e.Address<br/>@_e.Date.Fmt() · @_e.MeetingTime.Fmt() → @_e.ExpectedEndTime.Fmt()
        @if(!string.IsNullOrWhiteSpace(_e.DressCode)){<text><br/>Tenue : @_e.DressCode</text>}
        @if(!string.IsNullOrWhiteSpace(_e.OnSiteContact)){<text><br/>Contact : @_e.OnSiteContact</text>}</div>
    </div>

    <div class="lab">Effectif par rôle</div>
    <div class="panel">
        @foreach (var rn in _e.RoleNeeds)
        {
            var c = _confirmed.Count(a => a.JobRoleId == rn.JobRoleId);
            <div class="field"><span>@RoleName(rn.JobRoleId)</span>
                <b>@c / @rn.CountNeeded assigné(s) · @rn.HourlyRate €/h</b></div>
        }
    </div>

    <div class="lab">Personnel assigné</div>
    @if (_confirmed.Count == 0)
    {
        <div class="panel"><div class="m">Personne pour l'instant</div></div>
    }
    else
    {
        <div class="panel">
        @foreach (var a in _confirmed)
        {
            <div class="field"><span>@Name(a.AccountId)</span><b>@RoleName(a.JobRoleId)</b></div>
        }
        </div>
    }

    @if (_requests.Count > 0)
    {
        <div class="lab">Demandes de participation</div>
        @foreach (var a in _requests)
        {
            <div class="panel">
                <div class="t">@Name(a.AccountId)</div>
                <div class="m">@RoleName(a.JobRoleId) · demande à rejoindre</div>
                <div class="row2" style="margin-top:12px">
                    <button class="btn danger" @onclick="() => Decide(a, false)">Refuser</button>
                    <button class="btn primary" @onclick="() => Decide(a, true)">Accepter</button>
                </div>
            </div>
        }
    }

    <div class="actions">
        <NavLink class="btn ghost" href="@($"m/events/{_e.Id}/assign")">Assigner du personnel</NavLink>
        <NavLink class="btn ghost" href="@($"m/events/{_e.Id}/edit")">Éditer l'event</NavLink>
    </div>
}
```
(Existing `@code` already injects `NavigationManager Nav` and has `_e/_confirmed/_requests/Name/RoleName/Decide`. Leave `@code` unchanged.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MEventDetail.razor
git commit -m "reskin: manager event detail panels"
```

### Task 8: Manager Event create/edit (`MEventEdit.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MEventEdit.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/events/new"
@page "/m/events/{Id}/edit"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("m/events")'>‹ Events</button>
<h1>@(_isNew ? "Nouvel event" : _e.Name)</h1>

<div class="panel">
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
        <ToggleSwitch Value="_e.IsOpenForSignup" ValueChanged="v => _e.IsOpenForSignup = v" /></div>
</div>

<div class="lab">Effectif & rémunération par rôle</div>
@foreach (var r in _roles)
{
    var need = _e.RoleNeeds.FirstOrDefault(n => n.JobRoleId == r.Id);
    <div class="panel">
        <div class="t">@r.Name</div>
        <div class="field"><span>Personnes</span>
            <Stepper Value="need?.CountNeeded ?? 0" ValueChanged="v => SetCount(r.Id, v)" /></div>
        <div class="field"><span>Taux €/h</span>
            <input type="number" style="max-width:90px" value="@((need?.HourlyRate ?? 0m))"
                   @onchange="e => SetRate(r.Id, e.Value)" /></div>
    </div>
}

@if (_err) { <p class="pill bad" style="display:block;margin:12px 0">Le nom est obligatoire.</p> }
<button class="btn primary block" @onclick="Save">@(_isNew ? "Créer l'event" : "Enregistrer")</button>
```
(Leave `@code` unchanged — `_e/_roles/_isNew/_err/_date/_meet/_end/SetCount/SetRate/Save` all exist.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MEventEdit.razor
git commit -m "reskin: manager event create/edit panel"
```

### Task 9: Manager Assign staff (`MAssignStaff.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MAssignStaff.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/events/{Id}/assign"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo($"m/events/{Id}")'>‹ Détail event</button>
<h1>Assigner du personnel</h1>
<p class="sub">@_event?.Name</p>

@foreach (var r in _eventRoles)
{
    <div class="lab">@r.Name</div>
    @if (!_employees.Any(e => e.JobRoleIds.Contains(r.Id)))
    {
        <div class="panel"><div class="m">Aucun employé actif pour ce rôle</div></div>
    }
    @foreach (var emp in _employees.Where(e => e.JobRoleIds.Contains(r.Id)))
    {
        var already = _assigned.Any(a => a.AccountId == emp.Id && a.JobRoleId == r.Id);
        <div class="panel" style="display:flex;justify-content:space-between;align-items:center;gap:12px">
            <div><div class="t">@emp.FullName</div>
                <div class="m">@(already ? "Déjà assigné" : "Disponible")</div></div>
            @if (!already)
            {
                <button class="btn primary" @onclick="() => Assign(emp.Id, r.Id)">Assigner</button>
            }
        </div>
    }
}
```
(Leave `@code` unchanged — `_event/_eventRoles/_employees/_assigned/Assign/Id` exist.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MAssignStaff.razor
git commit -m "reskin: manager assign staff panels"
```

### Task 10: Manager Team (`MTeam.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MTeam.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/team"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px">
    <h1>Équipe</h1>
    <NavLink class="btn ghost" href="m/roles">Gérer les rôles</NavLink>
</div>
<div class="row2" style="max-width:320px;margin:14px 0">
    <button class="btn @(_tab=="active"?"primary":"ghost")" @onclick='() => _tab="active"'>Actifs</button>
    <button class="btn @(_tab=="pending"?"primary":"ghost")" @onclick='() => _tab="pending"'>En attente</button>
</div>

@{ var list = (_tab == "active"
       ? _emps.Where(e => e.Status == AccountStatus.Active)
       : _emps.Where(e => e.Status == AccountStatus.Pending)).ToList(); }
<ResponsiveTable TItem="Account" Items="list"
    EmptyIcon="◴" EmptyTitle="Personne ici"
    RowClick="e => Nav.NavigateTo($\"m/team/{e.Id}\")"
    Columns="@(new(){
        new(\"Nom\", e => @<b>@e.FullName</b>),
        new(\"Rôles\", e => @<text>@RoleNames(e)</text>),
        new(\"Statut\", e => @<span class=\"pill @(e.Status==AccountStatus.Active?\"ok\":\"wait\")\">@(e.Status==AccountStatus.Active?\"Actif\":\"En attente\")</span>) })" />
```
(Leave `@code` unchanged — `_tab/_emps/RoleNames/OnInitializedAsync` exist; the `[SupplyParameterFromQuery] Filter` logic stays in `@code`.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MTeam.razor
git commit -m "reskin: manager team table"
```

### Task 11: Manager Employee detail (`MEmployeeDetail.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MEmployeeDetail.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/team/{Id}"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("m/team")'>‹ Équipe</button>

@if (_a is null)
{
    <div class="panel"><p class="sub">Introuvable.</p></div>
}
else
{
    <h1>@_a.FullName</h1>
    <div class="panel">
        <div class="field"><span>Email</span><b>@_a.Email</b></div>
        <div class="field"><span>Rôles</span><b>@RoleNames()</b></div>
        <div class="field"><span>Statut</span><b><span class="@StatusPill()">@StatusFr()</span></b></div>
    </div>
    @if (_a.Status == AccountStatus.Pending)
    {
        <div class="row2" style="max-width:420px">
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
```
(Leave `@code` unchanged — `_a/RoleNames/StatusFr/StatusPill/Set/Id` exist.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MEmployeeDetail.razor
git commit -m "reskin: manager employee detail panel"
```

### Task 12: Manager Roles (`MRoles.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MRoles.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/roles"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("m/team")'>‹ Équipe</button>
<h1>Gérer les rôles</h1>

@foreach (var r in _roles)
{
    <div class="panel">
        @if (_editId == r.Id)
        {
            <div class="field"><span>Nom</span><input @bind="_editName" /></div>
            <div class="row2" style="margin-top:10px">
                <button class="btn ghost" @onclick="() => _editId=null">Annuler</button>
                <button class="btn primary" @onclick="() => SaveEdit(r)">OK</button>
            </div>
        }
        else
        {
            <div style="display:flex;justify-content:space-between;align-items:center;gap:12px">
                <div class="t">@r.Name</div>
                <div class="row2" style="max-width:240px">
                    <button class="btn ghost" @onclick="() => StartEdit(r)">Éditer</button>
                    <button class="btn danger" @onclick="() => Delete(r)">Supprimer</button>
                </div>
            </div>
        }
    </div>
}

<div class="lab">Nouveau rôle</div>
<div class="panel">
    <div class="field"><span>Nom</span><input @bind="_newName" /></div>
    <button class="btn primary block" style="margin-top:12px" @onclick="Add">Ajouter le rôle</button>
</div>
```
(Leave `@code` unchanged — `_roles/_newName/_editName/_editId/Add/StartEdit/SaveEdit/Delete` exist; the page already injects `IDataService`; `Nav` directive added for backlink.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MRoles.razor
git commit -m "reskin: manager roles panels"
```

### Task 13: Manager Timesheets list (`MTimesheets.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MTimesheets.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/timesheets"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("m")'>‹ Tableau de bord</button>
<h1>Timesheets</h1>
<div class="row2" style="max-width:320px;margin:14px 0">
    <button class="btn @(_tab=="sent"?"primary":"ghost")" @onclick='() => _tab="sent"'>À valider</button>
    <button class="btn @(_tab=="all"?"primary":"ghost")" @onclick='() => _tab="all"'>Toutes</button>
</div>

@{ var rows = (_tab == "sent" ? _rows.Where(r => r.ts.Status == TimesheetStatus.Sent) : _rows).ToList(); }
<ResponsiveTable TItem="(Timesheet ts, ServiceEvent ev, string who)" Items="rows"
    EmptyIcon="≣" EmptyTitle="Rien à afficher"
    RowClick="r => Nav.NavigateTo($\"m/timesheets/{r.ts.Id}\")"
    Columns="@(new(){
        new(\"Employé\", r => @<b>@r.who</b>),
        new(\"Event\", r => @<text>@r.ev.Name</text>),
        new(\"Date\", r => @<text>@r.ev.Date.Fmt()</text>),
        new(\"Durée\", r => @<text>@r.ts.Duration.Fmt()</text>),
        new(\"Statut\", r => @<span class=\"@r.ts.Status.PillClass()\">@r.ts.Status.StatusFr()</span>) })" />
```
(Leave `@code` unchanged — `_tab/_rows/OnInitializedAsync` exist; tuple type matches the existing `_rows` declaration `List<(Timesheet ts, ServiceEvent ev, string who)>`.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MTimesheets.razor
git commit -m "reskin: manager timesheets table"
```

### Task 14: Manager Timesheet detail (`MTimesheetDetail.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MTimesheetDetail.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/timesheets/{Id}"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("m/timesheets")'>‹ Timesheets</button>

@if (_ts is null)
{
    <div class="panel"><p class="sub">Introuvable.</p></div>
}
else
{
    <h1>Timesheet</h1>
    <div class="panel">
        <div class="t">@_who — @_role</div>
        <div class="m">@_eventName · @_eventDate.Fmt()</div>
    </div>

    <div class="lab">Heures (modifiables)</div>
    <div class="panel">
        <div class="field"><span>Début</span>
            <input type="time" value="@_start.ToString("HH\\:mm")" @onchange="OnStart" /></div>
        <div class="field"><span>Fin</span>
            <input type="time" value="@_end.ToString("HH\\:mm")" @onchange="OnEnd" /></div>
        <div class="field"><span>Durée</span><b style="color:var(--gold)">@Dur()</b></div>
        <div class="field"><span>Statut</span><b><span class="@_ts.Status.PillClass()">@_ts.Status.StatusFr()</span></b></div>
    </div>

    @if (_ts.Status is TimesheetStatus.Sent or TimesheetStatus.Validated or TimesheetStatus.Rejected)
    {
        <div class="lab">Refuser — motif</div>
        <div class="panel">
            <div class="field"><span>Motif</span><input @bind="_reason" placeholder="Heures incorrectes…" /></div>
            <div class="row2" style="margin-top:12px">
                <button class="btn danger" @onclick="Reject">Refuser</button>
                <button class="btn primary" @onclick="Validate">Valider</button>
            </div>
            @if (_saved) { <p class="pill ok" style="display:block;margin-top:12px">Enregistré ✓</p> }
        </div>
    }
}
```
(Leave `@code` unchanged — all symbols exist; page already injects `IDataService`+`NavigationManager`.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MTimesheetDetail.razor
git commit -m "reskin: manager timesheet detail panel"
```

### Task 15: Manager Profile (`MProfile.razor`)

**Files:** Modify `src/AdminTaos/Pages/Manager/MProfile.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/m/profile"
@layout DashboardLayout
@inject AuthState Auth
@inject NavigationManager Nav

<h1>Profil</h1>
<div class="panel">
    <div class="field"><span>Nom</span><b>@Auth.CurrentUser?.FullName</b></div>
    <div class="field"><span>Email</span><b>@Auth.CurrentUser?.Email</b></div>
    <div class="field"><span>Rôle</span><b>Manager</b></div>
</div>
<button class="btn ghost" style="margin-top:16px" @onclick="Logout">Se déconnecter</button>
```
(Leave `@code` unchanged — `Logout` exists.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MProfile.razor
git commit -m "reskin: manager profile panel"
```

---

## Phase 5 — Employee pages reskin (incl. §3 shift flow)

> Same rule: markup only, `@code` untouched, `@layout DashboardLayout`, drop `<TopBar>`.

### Task 16: Employee dashboard + shift start (`EHome.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/EHome.razor` (markup only). Spec §3: prominent **"Commencer mon shift"**.

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<h1>Bonjour, @FirstName</h1>
<p class="sub">@RoleLine · Compte actif</p>

<div class="lab">Prochaine prestation</div>
@if (_next is null)
{
    <div class="panel"><div class="m">Aucune prestation à venir</div></div>
}
else
{
    <div class="panel">
        <div class="t">@_next.Value.ev.Name</div>
        <div class="m">@_next.Value.ev.Venue · @_next.Value.ev.Date.Fmt() · RDV @_next.Value.ev.MeetingTime.Fmt()</div>
        <div style="margin-top:16px">
        @if (_ts is { Status: TimesheetStatus.InProgress })
        {
            <NavLink class="btn primary block" href="@($"e/events/{_next.Value.ev.Id}/active")">
                Reprendre mon shift en cours</NavLink>
        }
        else if (CanStartToday)
        {
            <button class="btn primary block" @onclick="Start">Commencer mon shift</button>
        }
        else
        {
            <NavLink class="btn ghost block" href="@($"e/events/{_next.Value.ev.Id}")">Voir le détail</NavLink>
        }
        </div>
    </div>
}
```
(Leave `@code` unchanged — `FirstName/RoleLine/_next/_ts/CanStartToday/Start` exist. Copy upgrade only: "Commencer mon shift" / "Reprendre mon shift en cours".)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/EHome.razor
git commit -m "reskin: employee dashboard + 'Commencer mon shift' (spec §3)"
```

### Task 17: Employee Events list (`EEvents.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/EEvents.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/events"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<h1>Events</h1>

<div class="lab">Mes events</div>
<ResponsiveTable TItem="ServiceEvent" Items="_mine"
    EmptyIcon="◷" EmptyTitle="Aucun event assigné"
    RowClick="e => Nav.NavigateTo($\"e/events/{e.Id}\")"
    Columns="@(new(){
        new(\"Event\", e => @<b>@e.Name</b>),
        new(\"Date\", e => @<text>@e.Date.Fmt()</text>),
        new(\"Lieu\", e => @<text>@e.Venue</text>) })" />

<div class="lab">Events ouverts à rejoindre</div>
<ResponsiveTable TItem="ServiceEvent" Items="_open"
    EmptyIcon="◷" EmptyTitle="Aucun event ouvert"
    RowClick="e => Nav.NavigateTo($\"e/events/{e.Id}\")"
    Columns="@(new(){
        new(\"Event\", e => @<b>@e.Name</b>),
        new(\"Date\", e => @<text>@e.Date.Fmt()</text>),
        new(\"Lieu\", e => @<text>@e.Venue · ouvert</text>) })" />
```
(Leave `@code` unchanged — `_mine/_open/OnInitializedAsync` exist; add `@inject NavigationManager Nav` directive if absent.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/EEvents.razor
git commit -m "reskin: employee events tables"
```

### Task 18: Employee Event detail (`EEventDetail.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/EEventDetail.razor` (markup only). Spec §3: prominent **"Commencer mon shift"** when it's the day & confirmed.

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/events/{Id}"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("e/events")'>‹ Events</button>

@if (_e is null)
{
    <div class="panel"><p class="sub">Introuvable.</p></div>
}
else
{
    <h1>@_e.Name</h1>
    <div class="panel">
        <div class="m">@_e.Venue — @_e.Address<br/>@_e.Date.Fmt() · @_e.MeetingTime.Fmt() → @_e.ExpectedEndTime.Fmt()
        @if(!string.IsNullOrWhiteSpace(_e.DressCode)){<text><br/>Tenue : @_e.DressCode</text>}
        @if(!string.IsNullOrWhiteSpace(_e.Instructions)){<text><br/>@_e.Instructions</text>}
        @if(!string.IsNullOrWhiteSpace(_e.OnSiteContact)){<text><br/>Contact : @_e.OnSiteContact</text>}</div>
    </div>

    <div class="lab">Rémunération</div>
    <div class="panel">
    @foreach (var rn in _e.RoleNeeds.Where(r => _myRoleIds.Contains(r.JobRoleId)))
    { <div class="field"><span>@RoleName(rn.JobRoleId)</span><b>@rn.HourlyRate €/h</b></div> }
    </div>

    <div style="margin-top:18px">
    @if (_asg is null && _e.IsOpenForSignup)
    {
        <button class="btn primary block" @onclick="RequestJoin">Demander à participer</button>
    }
    else if (_asg is { Status: AssignmentStatus.PendingApproval })
    {
        <p class="pill wait" style="display:block;text-align:center">Demande en attente de validation</p>
    }
    else if (_asg is { Status: AssignmentStatus.Rejected })
    {
        <p class="pill bad" style="display:block;text-align:center">Demande refusée</p>
    }
    else if (_asg is { Status: AssignmentStatus.Confirmed })
    {
        if (_ts is { Status: TimesheetStatus.InProgress })
        { <NavLink class="btn primary block" href="@($"e/events/{_e.Id}/active")">Reprendre mon shift en cours</NavLink> }
        else if (_ts is { Status: TimesheetStatus.ToSend })
        { <NavLink class="btn primary block" href="@($"e/events/{_e.Id}/recap")">Finaliser & envoyer ma timesheet</NavLink> }
        else if (_e.Date == DateOnly.FromDateTime(DateTime.Today))
        { <button class="btn primary block" @onclick="Start">Commencer mon shift</button> }
        else
        { <p class="pill ok" style="display:block;text-align:center">Tu es confirmé(e) sur cet event</p> }
    }
    </div>
}
```
(Leave `@code` unchanged — `_e/_asg/_ts/_myRoleIds/RoleName/RequestJoin/Start/Id` exist. Copy upgrade only.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/EEventDetail.razor
git commit -m "reskin: employee event detail + shift CTA (spec §3)"
```

### Task 19: Employee Shift in progress (`EActive.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/EActive.razor` (markup only). Spec §3: clean centered shift screen, big chrono, **"Terminer mon shift"**.

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/events/{Id}/active"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

@if (_e is not null && _ts is not null)
{
    <h1>Shift en cours</h1>
    <div class="panel" style="text-align:center;max-width:480px;margin:0 auto">
        <div class="t">@_e.Name</div>
        <div class="m">@_e.Venue · @RoleName</div>
        <Chrono Since="_ts.StartedAt!.Value" />
        <div class="live">● EN COURS — DÉBUT @_ts.StartedAt.Fmt()</div>
        <button class="btn primary block" @onclick="Finish">Terminer mon shift</button>
    </div>
}
```
(Leave `@code` unchanged — `_e/_ts/RoleName/Finish/Id` exist. `Chrono` keeps `.chrono` class. Copy upgrade only: "Terminer mon shift".)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/EActive.razor
git commit -m "reskin: employee shift-in-progress + 'Terminer mon shift' (spec §3)"
```

### Task 20: Employee Recap & send (`ERecap.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/ERecap.razor` (markup only). Spec §3: exact recap sentence + **"Envoyer ma timesheet pour validation"**.

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/events/{Id}/recap"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

@if (_e is not null && _ts is not null)
{
    <h1>Shift terminé</h1>
    <div class="panel" style="max-width:540px">
        <div class="lab" style="margin-top:0">Récapitulatif</div>
        <p style="font-size:15px;line-height:1.9;color:var(--txt)">
            <b>Event @_e.Name</b> · @_e.Date.Fmt() · Rôle @RoleName<br/>
            Tu as travaillé de <b>@_ts.StartedAt.Fmt()</b> à <b>@_ts.EndedAt.Fmt()</b>,
            soit <b style="color:var(--gold)">@_ts.Duration.Fmt()</b> au total.
        </p>
        <div style="margin:14px 0">
            <span class="@_ts.Status.PillClass()">@_ts.Status.StatusFr()</span>
        </div>
        @if (_ts.Status == TimesheetStatus.ToSend)
        {
            <button class="btn primary block" @onclick="Send">Envoyer ma timesheet pour validation</button>
            <p class="m" style="text-align:center;margin-top:10px">Le manager la recevra pour validation.</p>
        }
        else
        {
            <p class="pill ok" style="display:block;text-align:center">Timesheet envoyée pour validation ✓</p>
            <NavLink class="btn ghost block" style="margin-top:10px" href="e/hours">Voir mes heures</NavLink>
        }
    </div>
}
```
(Leave `@code` unchanged — `_e/_ts/RoleName/Send/Id` exist. Copy upgrade only — matches spec §3 sentence exactly.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/ERecap.razor
git commit -m "reskin: employee recap with exact §3 sentence + send-for-validation"
```

### Task 21: Employee My Hours (`EHours.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/EHours.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/hours"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<h1>Mes heures</h1>
<ResponsiveTable TItem="(Timesheet ts, ServiceEvent ev)" Items="_rows"
    EmptyIcon="≣" EmptyTitle="Aucune prestation" EmptyHint="Tes timesheets apparaîtront ici."
    RowClick="r => Nav.NavigateTo($\"e/hours/{r.ts.Id}\")"
    Columns="@(new(){
        new(\"Event\", r => @<b>@r.ev.Name</b>),
        new(\"Date\", r => @<text>@r.ev.Date.Fmt()</text>),
        new(\"Durée\", r => @<text>@r.ts.Duration.Fmt()</text>),
        new(\"Statut\", r => @<span class=\"@r.ts.Status.PillClass()\">@r.ts.Status.StatusFr()</span>) })" />
```
(Leave `@code` unchanged — `_rows` is `List<(Timesheet ts, ServiceEvent ev)>`; `OnInitializedAsync` exists; add `@inject NavigationManager Nav` if absent.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/EHours.razor
git commit -m "reskin: employee my-hours table"
```

### Task 22: Employee Timesheet detail (`ETimesheetDetail.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/ETimesheetDetail.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/hours/{Id}"
@layout DashboardLayout
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("e/hours")'>‹ Mes heures</button>

@if (_ts is null)
{
    <div class="panel"><p class="sub">Introuvable.</p></div>
}
else
{
    <h1>Timesheet</h1>
    <div class="panel">
        <div class="t">@_eventName</div>
        <div class="m">Durée : @_ts.Duration.Fmt()</div>
    </div>
    <div class="panel">
        <div class="field"><span>Début</span><b>@_ts.EffectiveStart.Fmt()</b></div>
        <div class="field"><span>Fin</span><b>@_ts.EffectiveEnd.Fmt()</b></div>
        <div class="field"><span>Statut</span><b><span class="@_ts.Status.PillClass()">@_ts.Status.StatusFr()</span></b></div>
    </div>
    @if (_ts.Status == TimesheetStatus.Rejected)
    {
        <div class="panel">
            <div class="t">Motif du refus</div>
            <div class="m">@(_ts.RejectionReason ?? "Non précisé")</div>
        </div>
        <button class="btn primary block" @onclick="Resend">Renvoyer la timesheet</button>
    }
}
```
(Leave `@code` unchanged — `_ts/_eventName/Resend/Id` exist; page already injects `IDataService`; `Nav` directive added for backlink.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/ETimesheetDetail.razor
git commit -m "reskin: employee timesheet detail panel"
```

### Task 23: Employee Profile (`EProfile.razor`)

**Files:** Modify `src/AdminTaos/Pages/Employee/EProfile.razor` (markup only)

- [ ] **Step 1: New markup (before `@code {`):**

```razor
@page "/e/profile"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<h1>Profil</h1>
<div class="panel">
    <div class="field"><span>Nom</span><b>@Auth.CurrentUser?.FullName</b></div>
    <div class="field"><span>Email</span><b>@Auth.CurrentUser?.Email</b></div>
    <div class="field"><span>Rôle(s)</span><b>@_roles</b></div>
    <div class="field"><span>Statut</span><b><span class="pill ok">Actif</span></b></div>
</div>
<button class="btn ghost" style="margin-top:16px" @onclick="Logout">Se déconnecter</button>
```
(Leave `@code` unchanged — `_roles/Logout/OnInitializedAsync` exist.)

- [ ] **Step 2: Build incremental + Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Employee/EProfile.razor
git commit -m "reskin: employee profile panel"
```

---

## Phase 6 — Auth pages + PWA + final verification

### Task 24: Auth pages on `AuthShell`

**Files:** Modify `src/AdminTaos/Pages/Auth/Splash.razor`, `Login.razor`, `Register.razor`, `AccountPending.razor` (markup only; auth pages use the default layout = `AuthShell` set in Task 3; remove any `<TopBar>` / `app-shell` wrappers).

- [ ] **Step 1: `Splash.razor` markup (before `@code {`):**

```razor
@page "/"
@page "/splash"
@inject NavigationManager Nav
@inject AuthState Auth

<div style="text-align:center;color:var(--mut)">Chargement…</div>
```
(Leave `@code` unchanged — `OnAfterRender` redirect logic stays.)

- [ ] **Step 2: `Login.razor` markup (before `@code {`)** — **MUST keep `class="card quick"` on the quick-login buttons** (bUnit anchor):

```razor
@page "/login"
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

<h1 style="text-align:center;font-size:20px">Connexion</h1>
<p class="sub" style="text-align:center">Entre ton email, ou choisis un compte de test.</p>

<div class="field"><span>Email</span><input @bind="_email" placeholder="manager@taos.be" /></div>
@if (_error) { <p class="pill bad" style="display:block;text-align:center;margin-top:10px">Email inconnu</p> }
<button class="btn primary block" style="margin-top:14px" @onclick="DoLogin">Se connecter</button>
<a class="btn ghost block" href="register">Créer un compte</a>

<div class="lab" style="text-align:center">Comptes de test</div>
@foreach (var a in _accounts)
{
    <button class="card quick" @onclick="() => Quick(a.Email)">
        <div class="t">@a.FullName</div>
        <div class="m">@TypeLabel(a) · @RoleLabel(a) · @StatusLabel(a.Status)</div>
    </button>
}
```
(Leave `@code` unchanged — `_accounts/_email/_error/DoLogin/Quick/TypeLabel/RoleLabel/StatusLabel` exist. `.card.quick` preserved → `Login_lists_five_seeded_test_accounts` stays green unchanged.)

- [ ] **Step 3: `Register.razor` markup (before `@code {`):**

```razor
@page "/register"
@inject IDataService Data
@inject NavigationManager Nav

<button class="backlink" @onclick='() => Nav.NavigateTo("login")'>‹ Connexion</button>
<h1 style="font-size:20px">Créer un compte</h1>
<p class="sub">Ton compte sera validé par un manager.</p>

<div class="field"><span>Nom complet</span><input @bind="_name" /></div>
<div class="field"><span>Email</span><input @bind="_email" /></div>
<div class="field"><span>Mot de passe</span><input type="password" @bind="_pwd" /></div>
<div class="lab">Rôle(s) souhaité(s)</div>
@foreach (var r in _roles)
{
    <button class="card @(_selected.Contains(r.Id) ? "glow" : "")" @onclick="() => Toggle(r.Id)">
        <div class="t">@r.Name</div>
        <div class="m">@(_selected.Contains(r.Id) ? "Sélectionné ✓" : "Toucher pour choisir")</div>
    </button>
}
@if (_err) { <p class="pill bad" style="display:block;margin-top:10px">Remplis nom, email et au moins un rôle.</p> }
<button class="btn primary block" style="margin-top:14px" @onclick="Submit">S'inscrire</button>
```
(Leave `@code` unchanged — `_roles/_selected/_name/_email/_pwd/_err/Toggle/Submit` exist.)

- [ ] **Step 4: `AccountPending.razor` markup (before `@code {`):**

```razor
@page "/pending"
@inject AuthState Auth
@inject NavigationManager Nav

<div class="empty" style="padding:20px 0">
    <div class="ic">◷</div>
    <div class="e-t">Compte en attente de validation</div>
    <p>Un manager doit valider ton compte avant l'accès.<br/>Reviens un peu plus tard.</p>
</div>
<button class="btn ghost block" @onclick="Logout">Se déconnecter</button>
```
(Leave `@code` unchanged — `Logout` exists.)

- [ ] **Step 5: Full solution build + tests**

Run: `dotnet build`
Expected: **Build succeeded, 0 errors** (all pages now migrated; no `ManagerLayout`/`EmployeeLayout`/`TopBar`/`BottomNav` references remain — verify: `grep -rn "ManagerLayout\|EmployeeLayout\|<TopBar\|BottomNav" src/AdminTaos` returns nothing).
Run: `dotnet test`
Expected: **29/29 passed, 0 failed** (Login `.card.quick` preserved; Stepper `.stepper`+buttons preserved; Chrono `.chrono` preserved — no test edits).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "reskin: auth pages on AuthShell (quick-login anchor preserved)"
```

### Task 25: PWA light theme + loading splash

**Files:** Modify `src/AdminTaos/wwwroot/index.html`, `src/AdminTaos/wwwroot/manifest.webmanifest`

- [ ] **Step 1: `index.html`** — set the loading `<div id="app">` block and theme-color to light. Replace the `<div id="app">…</div>` content with:

```html
<div id="app">
  <div style="display:flex;height:100dvh;align-items:center;justify-content:center;
       background:#F4F5F7;color:#B8902F;font-family:Georgia,serif;letter-spacing:3px">
    TAOS
  </div>
</div>
```
And change the theme-color meta to `<meta name="theme-color" content="#F4F5F7" />`. Leave everything else (app.css link, manifest link, apple-touch-icon, blazor script, SW registration, viewport-fit=cover) intact.

- [ ] **Step 2: `manifest.webmanifest`** — change `"background_color"` and `"theme_color"` to `"#F4F5F7"`. Keep name/short_name/icons/display unchanged. Final:

```json
{
  "name": "TAOS — The Art of Service",
  "short_name": "TAOS",
  "start_url": "./",
  "display": "standalone",
  "background_color": "#F4F5F7",
  "theme_color": "#F4F5F7",
  "icons": [
    { "src": "icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "icons/icon-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

- [ ] **Step 3: Build + publish smoke**

Run: `dotnet build` → 0 errors. `dotnet test` → 29/29.
Run: `dotnet publish src/AdminTaos -c Release -o /tmp/taospub25 2>&1 | tail -2` → succeeds; `ls /tmp/taospub25/wwwroot/{app.css,manifest.webmanifest}` exist; then `rm -rf /tmp/taospub25`.

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/wwwroot/index.html src/AdminTaos/wwwroot/manifest.webmanifest
git commit -m "reskin: light PWA theme + loading splash"
```

### Task 26: Final responsive verification pass

**Files:** none (verification); fix any defect found in the specific file + commit per fix.

- [ ] **Step 1: Build, full test, run smoke**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
dotnet build 2>&1 | tail -2
dotnet test 2>&1 | tail -2
grep -rn "ManagerLayout\|EmployeeLayout\|<TopBar\|BottomNav\|app-shell" src/AdminTaos --include=*.razor --include=*.css || echo "OK no stale refs"
dotnet run --project src/AdminTaos --urls http://localhost:5240 >/tmp/r.log 2>&1 & P=$!; sleep 14
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5240/
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5240/app.css
kill $P 2>/dev/null; rm -f /tmp/r.log
```
Expected: build 0 errors; 29/29; no stale refs; HTTP 200.

- [ ] **Step 2: Static responsive audit (reason through `app.css` + a few pages)**

Confirm by inspection: at ≥1024px `.side` fixed 240px + `.main{margin-left:240px}`; at 768–1023px the rail media query hides labels (`.lbl`) & shrinks to 64px; at <767px `.side` is translated off-screen, `.burger` shows, `.layout.drawer .side` slides in with `.scrim`; `.rt` becomes stacked cards (`thead` hidden, `td::before` shows `data-label`); `.content` max-width 1200 desktop / 100% mobile; tap targets (`.btn` padding ≥11px, `.stepper button` 30px) comfortable. Fix any real CSS defect found, commit `fix: <what> (responsive pass)`.

- [ ] **Step 3: Verify spec §3 wording present**

```bash
grep -rn "Commencer mon shift\|Terminer mon shift\|Envoyer ma timesheet pour validation\|Tu as travaillé de" src/AdminTaos/Pages/Employee
```
Expected: matches in `EHome.razor`/`EEventDetail.razor` (Commencer mon shift), `EActive.razor` (Terminer mon shift), `ERecap.razor` (the recap sentence + "Envoyer ma timesheet pour validation"). If any missing, fix that page's markup (copy only) + commit.

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "chore: dashboard reskin complete — responsive verified, 29 tests green" --allow-empty
```

---

## Self-Review

**Spec coverage (spec §-by-§):**
- §2 portée/garanties (reskin only, @code untouched, 29 green, anchors preserved) → every page task says "markup only, @code byte-for-byte"; Task 1/24 preserve `.stepper`/`.chrono`/`.card.quick` → ✓
- §3 shift flow priority + exact wording → Tasks 16,18 (Commencer mon shift), 19 (Terminer mon shift), 20 (exact recap sentence + "Envoyer ma timesheet pour validation"), Task 26 Step 3 greps to verify → ✓
- §4 layout/nav (DashboardLayout, Sidebar dark, Topbar, rail, drawer, AuthShell, TopBar migration via .backlink) → Tasks 2,3 + every page backlink → ✓
- §5 design system light+gold, names preserved → Task 1 → ✓
- §6 ResponsiveTable + StatCard + panels → Task 4; used Tasks 5,6,10,13,17,21 (tables), 5 (stat cards), detail/form/auth panels → ✓
- §7 tests impact: zero edits via preserved anchors → Tasks 1,24 + Task 25/26 verify 29 → ✓
- §8 screen mapping (23 routes) → Tasks 5–24 cover all 23 + auth → ✓
- §9 DoD (build 0, 29 green, dashboard responsive, §3 visible, PWA light, IDataService untouched) → Tasks 25,26 → ✓
- §10 phases → Phases 1–6 mirror the suggested decomposition → ✓

**Placeholder scan:** No TBD/TODO; every step has concrete code/commands. Per-page tasks reference the existing committed `@code` symbols by exact name (e.g. `_events`, `_rows` tuple shape, `StatusFr`, `RoleNames`, `Decide`, `Start`, `Finish`, `Send`, `_ts`, `_e`, `Logout`) — these exist in the current files (verified during the original build). "Leave @code unchanged" is a precise instruction about existing code, not a placeholder.

**Type consistency:** `ResponsiveTable<TItem>` `Column(string Header, RenderFragment<TItem> Cell)` + `Items IReadOnlyList<TItem>` + `RowClick EventCallback<TItem>` used consistently; tuple item types match existing `@code` declarations (`List<(Timesheet ts, ServiceEvent ev, string who)>` for MTimesheets, `List<(Timesheet ts, ServiceEvent ev)>` for EHours). `StatCard` `Value`/`Label`/`Href` consistent. `Sidebar.NavItem`/`Topbar` params consistent with `DashboardLayout` usage. `.Fmt()`/`.StatusFr()`/`.PillClass()` already global via `@using AdminTaos.Services`. Class-name anchors `.card.quick`/`.stepper`/`.chrono` preserved in app.css and markup. No mismatches.

**Risk note for executor:** Some pages currently lack `@inject NavigationManager Nav`; tasks that add a `.backlink`/`RowClick` explicitly say to add the `@inject NavigationManager Nav` directive if absent — this is a directive addition (not a `@code` logic change) and is safe. If any page's `@code` already declares `Nav`, do not duplicate the directive.

---

## Execution Handoff

(Presented to the user after saving.)
