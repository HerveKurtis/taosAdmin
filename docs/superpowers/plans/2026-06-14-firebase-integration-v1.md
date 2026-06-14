# MD TAOS ADMIN — Firebase v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the mock auth + in-memory data layer with real Firebase Auth + Firestore + Storage (profile photo with initials fallback). All UI/routes/markup unchanged except Login (quick-login removed) and Profile pages (avatar upload added).

**Architecture:** Firebase JS SDK v10 (modular) loaded as an ES module in `wwwroot/firebase.js`, exposing a `window.taos` namespace of helpers. C# calls everything via `IJSRuntime`. Two new abstractions: `IAuthClient` (with `FirebaseAuthClient` + `FakeAuthClient` for tests) and `IStorageClient` (with `FirebaseStorageClient`). The existing `IDataService` gets a new implementation `FirestoreDataService` registered in place of `InMemoryDataService` for prod. Tests continue to use `InMemoryDataService` + `FakeAuthClient` directly. Account model gains a `PhotoUrl` field.

**Tech Stack:** Existing .NET 10 Blazor WASM. Firebase JS SDK v10.13.0 (CDN). Firebase CLI for deploying rules. No new C# packages. **Remove** `Blazored.LocalStorage` (Firebase Auth persists session via IndexedDB).

**Spec:** [docs/superpowers/specs/2026-06-14-firebase-integration-design.md](../specs/2026-06-14-firebase-integration-design.md)

**Branch:** Create `firebase-v1` off the current `dashboard-reskin` branch (the reskin is the visual base; not yet merged to `main`). The Firebase work will integrate the same way (we'll decide merge/PR after Task 17).

**Hard guarantees:**
- No `@code` of any of the 23 events/team/timesheets/profile pages changes for business logic. Profile pages gain an avatar upload section (additive).
- `dotnet build` 0 errors; `dotnet test` **28/28 verts** at every commit (29 → 28 after removing the quick-login bUnit test).
- Firebase config (`firebase-config.js`) is committed; the values are not secrets (rules enforce security).
- No emulator setup; dev = real Firebase project (Spark plan).

---

## File Structure

```
src/AdminTaos/
  wwwroot/
    firebase-config.js          NEW — public Firebase config (apiKey, projectId, etc.)
    firebase.js                 NEW — ES module loading SDK v10 + window.taos helpers
    index.html                  MOD — add firebase.js <script type="module">
  Models/
    Account.cs                  MOD — add `string? PhotoUrl { get; set; }`
  Services/
    IAuthClient.cs              NEW — auth abstraction
    FirebaseAuthClient.cs       NEW — Firebase Auth via JS interop
    IStorageClient.cs           NEW — storage abstraction
    FirebaseStorageClient.cs    NEW — Firebase Storage via JS interop
    FirestoreDataService.cs     NEW — IDataService over Firestore via JS interop
    AuthState.cs                MOD — uses IAuthClient + IDataService; LoginAsync(email,pwd); RegisterAsync(...)
  Pages/Auth/
    Login.razor                 MOD — wire Firebase login; REMOVE quick-login + "Comptes de test"
    Register.razor              MOD — wire Firebase register (create Auth user + accounts/{uid})
  Pages/Manager/
    MProfile.razor              MOD — add avatar (img/initiales) + "Changer ma photo" button
  Pages/Employee/
    EProfile.razor              MOD — same avatar section
  Components/
    Sidebar.razor               MOD — foot shows avatar (img or initiales) next to name
    DashTopbar.razor            MOD — show <img> if photoUrl else existing initials
  Program.cs                    MOD — register IAuthClient/IStorageClient/FirestoreDataService; drop Blazored.LocalStorage reg
  AdminTaos.csproj              MOD — remove Blazored.LocalStorage PackageReference
tests/AdminTaos.Tests/
  FakeAuthClient.cs             NEW — in-memory IAuthClient for AuthState tests
  AuthStateTests.cs             MOD — replace FakeLocalStorage with FakeAuthClient; new LoginAsync(email,pwd) signature
  ComponentTests.cs             MOD — remove Login_lists_five_seeded_test_accounts (feature removed)
firestore.rules                 NEW — strict role/status-based rules (repo root)
storage.rules                   NEW — avatar write self-only + size/type checks (repo root)
firebase.json                   NEW — Firebase CLI config (links rules files)
```

---

## Phase 1 — Firebase project & JS module infrastructure

### Task 1: Create Firebase project + commit `firebase-config.js` + wire `index.html`

**Files:**
- Create: `src/AdminTaos/wwwroot/firebase-config.js`
- Modify: `src/AdminTaos/wwwroot/index.html`

- [ ] **Step 1: User creates the Firebase project (manual, one-time)**

In a browser, sign in to https://console.firebase.google.com → **Add project** → name `taos-admin` (or any) → disable Analytics → wait. Then:
- **Authentication → Get started → Email/Password → Enable → Save.**
- **Firestore Database → Create database → location `eur3 (Europe-west)` (or closest) → "Start in production mode" → Create.**
- **Storage → Get started → "Start in production mode" → Create.**
- In **Project settings (gear)** → **Your apps → Web (`</>` icon) → register app `taos-web`** (no Hosting checkbox needed). Copy the displayed `firebaseConfig` object — apiKey, authDomain, projectId, storageBucket, messagingSenderId, appId.

(This task has no code yet; capture the 6 string values into a temporary note for Step 2.)

- [ ] **Step 2: Create `src/AdminTaos/wwwroot/firebase-config.js`**

Replace the placeholders with the real values from Step 1:
```js
export const firebaseConfig = {
  apiKey: "PASTE_API_KEY_HERE",
  authDomain: "PASTE_AUTHDOMAIN_HERE",
  projectId: "PASTE_PROJECTID_HERE",
  storageBucket: "PASTE_STORAGEBUCKET_HERE",
  messagingSenderId: "PASTE_MESSAGINGSENDERID_HERE",
  appId: "PASTE_APPID_HERE"
};
```
(These values are public-by-design — security is enforced by rules. Commit as-is. If you later rotate the project, just update the file.)

- [ ] **Step 3: Wire `firebase.js` script tag into `src/AdminTaos/wwwroot/index.html`**

In the `<head>` (or just before the closing `</body>`, before the `blazor.webassembly.js` script — `firebase.js` must be loaded as an ES module and ready before C# calls it; load it in `<head>` with `defer`):
```html
<script type="module" src="firebase.js"></script>
```
Do NOT remove or reorder the existing `app.css` link, manifest link, theme-color meta, apple-touch-icon, viewport, or the Blazor `<script src="_framework/blazor.webassembly...">` and the service-worker registration. Insert the new line near the other `<script>` / `<link>` tags in `<head>`.

- [ ] **Step 4: Verify build & tests still green** (this task only adds files/script; `firebase.js` doesn't exist yet so a runtime 404 would happen — but no C# touches it yet, so `dotnet build` + `dotnet test` are unaffected)

Run:
```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
dotnet build
dotnet test
```
Expected: Build succeeded 0 errors; **29 passed**, 0 failed.

- [ ] **Step 5: Commit**

```bash
git checkout -b firebase-v1
git add src/AdminTaos/wwwroot/firebase-config.js src/AdminTaos/wwwroot/index.html
git commit -m "feat(firebase): public config + load firebase.js module from index.html"
```

---

### Task 2: Implement `firebase.js` JS module (init + auth/firestore/storage helpers)

**Files:**
- Create: `src/AdminTaos/wwwroot/firebase.js`

- [ ] **Step 1: Create `src/AdminTaos/wwwroot/firebase.js`** with EXACTLY this content:

```js
// ES module — loaded via <script type="module" src="firebase.js"></script>
// Exposes a global window.taos namespace of helpers callable from Blazor IJSRuntime.

import { initializeApp } from "https://www.gstatic.com/firebasejs/10.13.0/firebase-app.js";
import {
  getAuth, onAuthStateChanged,
  signInWithEmailAndPassword, createUserWithEmailAndPassword, signOut
} from "https://www.gstatic.com/firebasejs/10.13.0/firebase-auth.js";
import {
  getFirestore, doc, getDoc, setDoc, updateDoc, deleteDoc,
  collection, getDocs, query, where, Timestamp
} from "https://www.gstatic.com/firebasejs/10.13.0/firebase-firestore.js";
import {
  getStorage, ref as storageRef, uploadBytes, getDownloadURL
} from "https://www.gstatic.com/firebasejs/10.13.0/firebase-storage.js";

import { firebaseConfig } from "./firebase-config.js";

const app  = initializeApp(firebaseConfig);
const auth = getAuth(app);
const db   = getFirestore(app);
const st   = getStorage(app);

// --- helpers: JSON-safe conversions ---
// Firestore Timestamps must be converted to ISO strings for Blazor JSON marshalling,
// and back to Timestamps for writes. DateOnly/TimeOnly are stored as plain strings.
function fromFirestore(v) {
  if (v === null || v === undefined) return null;
  if (v instanceof Timestamp) return v.toDate().toISOString();
  if (Array.isArray(v)) return v.map(fromFirestore);
  if (typeof v === "object") {
    const o = {};
    for (const k of Object.keys(v)) o[k] = fromFirestore(v[k]);
    return o;
  }
  return v;
}
function toFirestore(v, key) {
  if (v === null || v === undefined) return null;
  // Field-name-driven Timestamp conversion: any field whose camelCase name ends with `At`
  // (e.g. createdAt, startedAt, sentAt, managerAdjustedStart) carrying an ISO string is a Timestamp.
  const isTimestampField = (k) => typeof k === "string"
    && (k.endsWith("At") || k.endsWith("Start") || k.endsWith("End") || k === "managerAdjustedStart" || k === "managerAdjustedEnd");
  if (isTimestampField(key) && typeof v === "string") {
    const d = new Date(v); if (!isNaN(d.getTime())) return Timestamp.fromDate(d);
  }
  if (Array.isArray(v)) return v.map((x) => toFirestore(x, null));
  if (typeof v === "object") {
    const o = {};
    for (const k of Object.keys(v)) o[k] = toFirestore(v[k], k);
    return o;
  }
  return v;
}

// --- Auth ---
async function login(email, password) {
  try { const c = await signInWithEmailAndPassword(auth, email, password); return c.user.uid; }
  catch (e) { return null; }
}
async function register(email, password) {
  try { const c = await createUserWithEmailAndPassword(auth, email, password); return c.user.uid; }
  catch (e) { return null; }
}
async function logout()         { await signOut(auth); }
function currentUid()           { return auth.currentUser ? auth.currentUser.uid : null; }
function watchAuth(dotnetRef) {
  onAuthStateChanged(auth, (u) => dotnetRef.invokeMethodAsync("OnAuthChangedFromJs", u ? u.uid : null));
}

// --- Firestore ---
async function getAll(col) {
  const snap = await getDocs(collection(db, col));
  return snap.docs.map(d => ({ id: d.id, ...fromFirestore(d.data()) }));
}
async function getOne(col, id) {
  const snap = await getDoc(doc(db, col, id));
  return snap.exists() ? ({ id: snap.id, ...fromFirestore(snap.data()) }) : null;
}
async function queryByField(col, field, value) {
  const q = query(collection(db, col), where(field, "==", value));
  const snap = await getDocs(q);
  return snap.docs.map(d => ({ id: d.id, ...fromFirestore(d.data()) }));
}
async function setOne(col, id, data) {
  const { id: _ignore, ...rest } = data; // never store `id` field inside the doc
  await setDoc(doc(db, col, id), toFirestore(rest, null));
}
async function updateOne(col, id, data) {
  const { id: _ignore, ...rest } = data;
  await updateDoc(doc(db, col, id), toFirestore(rest, null));
}
async function deleteOne(col, id) { await deleteDoc(doc(db, col, id)); }

// --- Storage (avatar upload + compression in JS) ---
async function pickAndUploadAvatar(uid) {
  return new Promise((resolve, reject) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "image/*";
    input.style.display = "none";
    document.body.appendChild(input);
    input.addEventListener("change", async () => {
      try {
        const file = input.files && input.files[0];
        document.body.removeChild(input);
        if (!file) { resolve(null); return; }
        if (file.size > 2 * 1024 * 1024) { reject(new Error("Fichier > 2 Mo")); return; }
        if (!file.type.startsWith("image/")) { reject(new Error("Pas une image")); return; }
        const blob = await compressToJpeg(file, 512, 0.85);
        const r = storageRef(st, `avatars/${uid}.jpg`);
        await uploadBytes(r, blob, { contentType: "image/jpeg" });
        const url = await getDownloadURL(r);
        resolve(url);
      } catch (e) { reject(e); }
    }, { once: true });
    input.click();
  });
}
function compressToJpeg(file, max, q) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    const r = new FileReader();
    r.onerror = reject;
    r.onload = () => { img.src = r.result; };
    img.onerror = reject;
    img.onload = () => {
      const side = Math.min(img.naturalWidth, img.naturalHeight);
      const sx = (img.naturalWidth - side) / 2;
      const sy = (img.naturalHeight - side) / 2;
      const c = document.createElement("canvas");
      c.width = max; c.height = max;
      const ctx = c.getContext("2d");
      ctx.drawImage(img, sx, sy, side, side, 0, 0, max, max);
      c.toBlob((b) => b ? resolve(b) : reject(new Error("toBlob failed")), "image/jpeg", q);
    };
    r.readAsDataURL(file);
  });
}

// --- Expose to Blazor ---
window.taos = {
  login, register, logout, currentUid, watchAuth,
  getAll, getOne, queryByField, setOne, updateOne, deleteOne,
  pickAndUploadAvatar
};
```

- [ ] **Step 2: Verify nothing C#-side is broken**

Run:
```bash
dotnet build
dotnet test
```
Expected: Build succeeded; **29 passed**.

(Runtime: opening the app in a browser, `window.taos` is now populated after page load; no C# yet calls it. Optional sanity check: `dotnet run --project src/AdminTaos --urls http://localhost:5260` then in DevTools Console type `taos` — you should see the object with the function names. Kill with Ctrl+C.)

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/wwwroot/firebase.js
git commit -m "feat(firebase): firebase.js module — Auth/Firestore/Storage JS helpers (window.taos)"
```

---

## Phase 2 — Auth abstraction + Firebase Auth client + `AuthState` refactor

### Task 3: Define `IAuthClient` interface

**Files:**
- Create: `src/AdminTaos/Services/IAuthClient.cs`

- [ ] **Step 1: Create the interface**

`src/AdminTaos/Services/IAuthClient.cs`:
```csharp
namespace AdminTaos.Services;

public interface IAuthClient
{
    /// <summary>Sign in with email+password. Returns the Firebase Auth uid on success, null on failure.</summary>
    Task<string?> LoginAsync(string email, string password);

    /// <summary>Create a new auth user with email+password. Returns the new uid on success, null on failure (e.g. email taken).</summary>
    Task<string?> RegisterAsync(string email, string password);

    /// <summary>Sign the current user out.</summary>
    Task LogoutAsync();

    /// <summary>Get the currently authenticated uid, or null if none.</summary>
    Task<string?> GetCurrentUidAsync();

    /// <summary>Subscribe to auth state changes. Called with uid on sign-in/restore, null on sign-out.</summary>
    event Action<string?>? OnAuthChanged;

    /// <summary>Start listening to underlying auth changes (call once at app start).</summary>
    Task InitializeAsync();
}
```

- [ ] **Step 2: Build + tests**

Run: `dotnet build` → Build succeeded.
Run: `dotnet test` → 29 passed.

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/Services/IAuthClient.cs
git commit -m "feat(firebase): IAuthClient abstraction"
```

---

### Task 4: Implement `FirebaseAuthClient` + add `FakeAuthClient` for tests

**Files:**
- Create: `src/AdminTaos/Services/FirebaseAuthClient.cs`
- Create: `tests/AdminTaos.Tests/FakeAuthClient.cs`

- [ ] **Step 1: Create `src/AdminTaos/Services/FirebaseAuthClient.cs`**

```csharp
using Microsoft.JSInterop;

namespace AdminTaos.Services;

public class FirebaseAuthClient : IAuthClient, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<FirebaseAuthClient>? _self;

    public event Action<string?>? OnAuthChanged;

    public FirebaseAuthClient(IJSRuntime js) { _js = js; }

    public async Task InitializeAsync()
    {
        _self = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("taos.watchAuth", _self);
    }

    [JSInvokable]
    public Task OnAuthChangedFromJs(string? uid)
    {
        OnAuthChanged?.Invoke(uid);
        return Task.CompletedTask;
    }

    public async Task<string?> LoginAsync(string email, string password)
        => await _js.InvokeAsync<string?>("taos.login", email, password);

    public async Task<string?> RegisterAsync(string email, string password)
        => await _js.InvokeAsync<string?>("taos.register", email, password);

    public Task LogoutAsync()
        => _js.InvokeVoidAsync("taos.logout").AsTask();

    public Task<string?> GetCurrentUidAsync()
        => _js.InvokeAsync<string?>("taos.currentUid").AsTask();

    public ValueTask DisposeAsync()
    {
        _self?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 2: Create `tests/AdminTaos.Tests/FakeAuthClient.cs`**

```csharp
using AdminTaos.Services;

namespace AdminTaos.Tests;

/// <summary>In-memory IAuthClient for tests. Maps email→uid; password is checked against a stored map.</summary>
public class FakeAuthClient : IAuthClient
{
    private readonly Dictionary<string,(string Uid,string Password)> _users = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentUid;

    public event Action<string?>? OnAuthChanged;

    /// <summary>Pre-register a user (uid known up front, e.g. matching a SeedData Account.Id).</summary>
    public void PreRegister(string uid, string email, string password)
        => _users[email] = (uid, password);

    public Task<string?> LoginAsync(string email, string password)
    {
        if (_users.TryGetValue(email, out var u) && u.Password == password)
        {
            _currentUid = u.Uid;
            OnAuthChanged?.Invoke(_currentUid);
            return Task.FromResult<string?>(_currentUid);
        }
        return Task.FromResult<string?>(null);
    }

    public Task<string?> RegisterAsync(string email, string password)
    {
        if (_users.ContainsKey(email)) return Task.FromResult<string?>(null);
        var uid = Guid.NewGuid().ToString();
        _users[email] = (uid, password);
        _currentUid = uid;
        OnAuthChanged?.Invoke(_currentUid);
        return Task.FromResult<string?>(_currentUid);
    }

    public Task LogoutAsync()
    {
        _currentUid = null;
        OnAuthChanged?.Invoke(null);
        return Task.CompletedTask;
    }

    public Task<string?> GetCurrentUidAsync() => Task.FromResult(_currentUid);

    public Task InitializeAsync() => Task.CompletedTask;
}
```

- [ ] **Step 3: Build + tests**

Run: `dotnet build` → Build succeeded.
Run: `dotnet test` → 29 passed (no test exercises these yet).

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Services/FirebaseAuthClient.cs tests/AdminTaos.Tests/FakeAuthClient.cs
git commit -m "feat(firebase): FirebaseAuthClient + FakeAuthClient test double"
```

---

### Task 5: Refactor `AuthState` to use `IAuthClient` + `IDataService` for accounts/{uid}; update `AuthStateTests`

**Files:**
- Modify: `src/AdminTaos/Services/AuthState.cs` (full rewrite)
- Modify: `tests/AdminTaos.Tests/AuthStateTests.cs` (rewrite the 4 tests for the new signature)

- [ ] **Step 1: Replace `src/AdminTaos/Services/AuthState.cs` entirely with**

```csharp
using AdminTaos.Models;

namespace AdminTaos.Services;

public class AuthState
{
    private readonly IAuthClient _auth;
    private readonly IDataService _data;

    public AuthState(IAuthClient auth, IDataService data)
    {
        _auth = auth;
        _data = data;
        _auth.OnAuthChanged += OnUidChanged;
    }

    public Account? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event Action? OnChange;
    private void Notify() => OnChange?.Invoke();

    /// <summary>Wires up the underlying auth listener and reads the current session (if any).</summary>
    public async Task InitializeAsync()
    {
        await _auth.InitializeAsync();
        var uid = await _auth.GetCurrentUidAsync();
        if (uid is not null) CurrentUser = await _data.GetAccountAsync(uid);
        Notify();
    }

    /// <summary>Sign in. Returns true on success (account exists in Firestore).</summary>
    public async Task<bool> LoginAsync(string email, string password)
    {
        var uid = await _auth.LoginAsync(email.Trim(), password);
        if (uid is null) return false;
        CurrentUser = await _data.GetAccountAsync(uid);
        Notify();
        return CurrentUser is not null;
    }

    /// <summary>Create a new Auth user AND the accounts/{uid} doc (Employee, Pending). Returns true on success.</summary>
    public async Task<bool> RegisterAsync(string fullName, string email, string password, List<string> jobRoleIds)
    {
        var uid = await _auth.RegisterAsync(email.Trim(), password);
        if (uid is null) return false;
        var acc = new Account {
            Id = uid,
            FullName = fullName,
            Email = email.Trim(),
            Type = AccountType.Employee,
            Status = AccountStatus.Pending,
            JobRoleIds = jobRoleIds,
            CreatedAt = DateTime.UtcNow
        };
        await _data.CreateAccountAsync(acc);
        CurrentUser = acc;
        Notify();
        return true;
    }

    public async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        CurrentUser = null;
        Notify();
    }

    public void Refresh() => Notify();

    private async void OnUidChanged(string? uid)
    {
        CurrentUser = uid is null ? null : await _data.GetAccountAsync(uid);
        Notify();
    }
}
```

- [ ] **Step 2: Write failing tests against the new signature**

Replace `tests/AdminTaos.Tests/AuthStateTests.cs` entirely with:
```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class AuthStateTests
{
    private static (AuthState auth, FakeAuthClient fakeAuth, InMemoryDataService data) Wire()
    {
        var fakeAuth = new FakeAuthClient();
        var data     = new InMemoryDataService();
        // Pre-register Firebase-Auth-side users for two seeded accounts; passwords arbitrary.
        fakeAuth.PreRegister(SeedData.MgrId,         "manager@taos.be", "pwd-mgr");
        fakeAuth.PreRegister(SeedData.EmpActiveHost, "sarah@taos.be",   "pwd-sarah");
        var state = new AuthState(fakeAuth, data);
        return (state, fakeAuth, data);
    }

    [Fact]
    public async Task Login_with_correct_credentials_sets_current_user()
    {
        var (auth, _, _) = Wire();
        var ok = await auth.LoginAsync("manager@taos.be", "pwd-mgr");
        Assert.True(ok);
        Assert.Equal(AccountType.Manager, auth.CurrentUser!.Type);
    }

    [Fact]
    public async Task Login_with_wrong_password_fails_and_user_stays_null()
    {
        var (auth, _, _) = Wire();
        Assert.False(await auth.LoginAsync("manager@taos.be", "bad-pwd"));
        Assert.Null(auth.CurrentUser);
    }

    [Fact]
    public async Task Initialize_restores_current_user_from_existing_session()
    {
        var fakeAuth = new FakeAuthClient();
        var data     = new InMemoryDataService();
        fakeAuth.PreRegister(SeedData.EmpActiveHost, "sarah@taos.be", "pwd-sarah");
        await fakeAuth.LoginAsync("sarah@taos.be", "pwd-sarah");   // session established outside AuthState

        var state = new AuthState(fakeAuth, data);
        await state.InitializeAsync();

        Assert.Equal("sarah@taos.be", state.CurrentUser!.Email);
    }

    [Fact]
    public async Task Logout_clears_current_user()
    {
        var (auth, _, _) = Wire();
        await auth.LoginAsync("manager@taos.be", "pwd-mgr");
        await auth.LogoutAsync();
        Assert.Null(auth.CurrentUser);
    }
}
```

- [ ] **Step 3: Run tests — expect 28 passed (or 27 + 1 failing on the very first auth test if FakeLocalStorage import is still around)**

Run: `dotnet test`
Expected: depending on transient state, either:
- 28 passed (if the old `FakeLocalStorage` class from the previous file was deleted by the rewrite — check by `grep -rn FakeLocalStorage tests` ; if empty, good), OR
- compile errors mentioning `Blazored.LocalStorage` from `FakeLocalStorage` — that means a leftover; remove the leftover class definition.

If `FakeLocalStorage` is referenced ONLY by the old AuthStateTests.cs (now rewritten), the rewrite eliminates it. Confirm:
```bash
grep -rn "FakeLocalStorage" tests src
```
should be empty. If a stale reference remains in `ComponentTests.cs` (the Login bUnit test wires up `ILocalStorageService`), that's expected for now — Task 7 removes that test.

Final expected: `dotnet test` → 28 passed (the 4 AuthState tests now use FakeAuthClient; everything else unaffected). If the build still fails because `ComponentTests.cs` references `FakeLocalStorage`, do NOT fix `ComponentTests.cs` here — Task 7 deletes that test. Instead, **temporarily** keep a minimal `FakeLocalStorage` stub in `ComponentTests.cs` (it was an `internal class` defined in that file in the past — check current state) so the test project compiles. If the codebase already has `FakeLocalStorage` defined outside AuthStateTests.cs, leave it; you'll remove `ComponentTests.cs` quick-login test in Task 7 and any orphaned `FakeLocalStorage` can go then.

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Services/AuthState.cs tests/AdminTaos.Tests/AuthStateTests.cs
git commit -m "feat(firebase): AuthState refactored over IAuthClient + IDataService; tests use FakeAuthClient"
```

---

## Phase 3 — Login/Register UI wiring + remove quick-login

### Task 6: Wire `Register.razor` to `AuthState.RegisterAsync`

**Files:**
- Modify: `src/AdminTaos/Pages/Auth/Register.razor`

- [ ] **Step 1: Replace the page's `@code` block to call AuthState (markup unchanged)**

Open the current file. Keep all the markup above `@code {` byte-for-byte. Replace the `@code { … }` block with:
```razor
@code {
    List<JobRole> _roles = new();
    readonly HashSet<string> _selected = new();
    string _name = "", _email = "", _pwd = "";
    bool _err;

    [Inject] IDataService Data { get; set; } = default!;
    [Inject] AuthState Auth { get; set; } = default!;
    [Inject] NavigationManager Nav { get; set; } = default!;

    protected override async Task OnInitializedAsync() => _roles = await Data.GetJobRolesAsync();
    void Toggle(string id) { if (!_selected.Add(id)) _selected.Remove(id); }

    async Task Submit()
    {
        if (string.IsNullOrWhiteSpace(_name)
            || string.IsNullOrWhiteSpace(_email)
            || string.IsNullOrWhiteSpace(_pwd)
            || _selected.Count == 0)
        { _err = true; return; }

        var ok = await Auth.RegisterAsync(_name.Trim(), _email.Trim(), _pwd, _selected.ToList());
        if (!ok) { _err = true; return; }
        Nav.NavigateTo("pending", replace: true);
    }
}
```

If the existing top-of-file `@inject` directives include `IDataService Data` / `NavigationManager Nav`, you can keep them as directives instead of `[Inject]` — both work; keep the page's existing style. Do NOT add `<TopBar>` or any new markup.

- [ ] **Step 2: Build + tests**

Run: `dotnet build` → Build succeeded.
Run: `dotnet test` → 28 passed (AuthStateTests rewritten, no Register test).

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/Pages/Auth/Register.razor
git commit -m "feat(firebase): Register wires to AuthState.RegisterAsync (Firebase Auth + accounts/{uid})"
```

---

### Task 7: Wire `Login.razor` to `AuthState.LoginAsync` + remove quick-login + remove the bUnit test

**Files:**
- Modify: `src/AdminTaos/Pages/Auth/Login.razor`
- Modify: `tests/AdminTaos.Tests/ComponentTests.cs` (remove `Login_lists_five_seeded_test_accounts` and any leftover `FakeLocalStorage`)

- [ ] **Step 1: Replace `src/AdminTaos/Pages/Auth/Login.razor` entirely with**

```razor
@page "/login"
@inject AuthState Auth
@inject NavigationManager Nav

<h1 style="text-align:center;font-size:20px">Connexion</h1>
<p class="sub" style="text-align:center">Entre ton email et ton mot de passe.</p>

<div class="field"><span>Email</span>
    <input @bind="_email" placeholder="ton.email@exemple.com" /></div>
<div class="field"><span>Mot de passe</span>
    <input type="password" @bind="_password" /></div>

@if (_error) { <p class="pill bad" style="display:block;text-align:center;margin-top:10px">@_errorMessage</p> }

<button class="btn primary block" style="margin-top:14px" @onclick="DoLogin">Se connecter</button>
<a class="btn ghost block" href="register">Créer un compte</a>

@code {
    string _email = "", _password = "";
    bool _error;
    string _errorMessage = "Identifiants invalides";

    async Task DoLogin()
    {
        _error = false;
        if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_password))
        { _error = true; _errorMessage = "Email et mot de passe requis"; return; }

        var ok = await Auth.LoginAsync(_email, _password);
        if (!ok) { _error = true; _errorMessage = "Identifiants invalides"; return; }

        var u = Auth.CurrentUser!;
        Nav.NavigateTo(u.Status switch
        {
            AccountStatus.Pending  => "pending",
            AccountStatus.Rejected => "login",
            _ => u.Type == AccountType.Manager ? "m" : "e"
        }, replace: true);
    }
}
```

The quick-login button list (`<button class="card quick">…</button>`) and the "Comptes de test" label are GONE.

- [ ] **Step 2: Remove the `Login_lists_five_seeded_test_accounts` test from `tests/AdminTaos.Tests/ComponentTests.cs`**

Open `ComponentTests.cs`. Delete the entire `[Fact] public void Login_lists_five_seeded_test_accounts() { … }` method body. Also delete:
- the `Wire()` helper if it's only used by that test (check by `grep -n Wire ComponentTests.cs` — if the only caller is the deleted test, delete `Wire()` too),
- any `using AdminTaos.Pages.Auth;` / `using Blazored.LocalStorage;` lines that become unused,
- the `internal class FakeLocalStorage : ILocalStorageService { … }` definition if it was declared inside ComponentTests.cs (it's no longer used by any test now — also delete it if you find it there).

Keep `Stepper_increments_and_clamps_at_min` and `Chrono_formats_elapsed_since_start` exactly as they are.

- [ ] **Step 3: Build + tests**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.
Run: `dotnet test`
Expected: **28 passed**, 0 failed (29 was the previous count; we just removed 1 by design).

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Pages/Auth/Login.razor tests/AdminTaos.Tests/ComponentTests.cs
git commit -m "feat(firebase): Login wires to AuthState.LoginAsync; remove quick-login + its bUnit test"
```

---

## Phase 4 — Firestore data layer (`FirestoreDataService`)

### Task 8: Add `Account.PhotoUrl` field and Firestore JSON conventions helper

**Files:**
- Modify: `src/AdminTaos/Models/Account.cs`

- [ ] **Step 1: Add `PhotoUrl` to `Account.cs`**

Open `src/AdminTaos/Models/Account.cs`. Add this property (keep all the others unchanged):
```csharp
    public string? PhotoUrl { get; set; }
```
Final shape (other properties unchanged):
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
    public string? PhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

- [ ] **Step 2: Build + tests**

Run: `dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed (no test asserts the new field).

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/Models/Account.cs
git commit -m "feat(firebase): Account.PhotoUrl (optional, default null)"
```

---

### Task 9: Implement `FirestoreDataService` — all 23 methods

**Files:**
- Create: `src/AdminTaos/Services/FirestoreDataService.cs`

This service marshals every `IDataService` call through `taos.*` JS helpers (Task 2). Field-name conventions in the JS helper (Task 2) automatically convert `…At` / `…Start` / `…End` fields to/from Firestore Timestamps. Everything else is plain JSON.

- [ ] **Step 1: Create the service**

```csharp
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
```

**Notes:**
- `DateOnly`/`TimeOnly` round-trip as ISO strings via `JsonSerializerDefaults.Web` (System.Text.Json supports these in .NET 8+). `DateTime` round-trips as ISO string in JSON, and `firebase.js` `toFirestore`/`fromFirestore` converts `…At` / `…Start` / `…End` field values to/from Firestore `Timestamp` automatically.
- Enums use `JsonStringEnumConverter` → stored as PascalCase strings.
- Property names: `JsonSerializerDefaults.Web` uses camelCase by default. So `FullName` ↔ `fullName`, `JobRoleIds` ↔ `jobRoleIds`, `PhotoUrl` ↔ `photoUrl`. Matches the spec §5.

- [ ] **Step 2: Build + tests**

Run: `dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed (no test uses `FirestoreDataService` — production-only path).

- [ ] **Step 3: Commit**

```bash
git add src/AdminTaos/Services/FirestoreDataService.cs
git commit -m "feat(firebase): FirestoreDataService — IDataService over Firestore via taos.* JS helpers"
```

---

## Phase 5 — DI swap

### Task 10: Swap `Program.cs` DI + remove `Blazored.LocalStorage` package

**Files:**
- Modify: `src/AdminTaos/Program.cs`
- Modify: `src/AdminTaos/AdminTaos.csproj`

- [ ] **Step 1: Update `src/AdminTaos/Program.cs`**

Replace the existing service registrations with EXACTLY these two blocks (do NOT touch the `WebAssemblyHostBuilder.CreateDefault(args)`, root component registration, `HttpClient` registration, or `await builder.Build().RunAsync()`):

```csharp
using AdminTaos.Services;

// --- Firebase / Firestore (prod) ---
builder.Services.AddSingleton<IAuthClient, FirebaseAuthClient>();
builder.Services.AddSingleton<IStorageClient, FirebaseStorageClient>();
builder.Services.AddSingleton<IDataService, FirestoreDataService>();

// --- App services ---
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<NavigationGuard>();
```

REMOVE any pre-existing `builder.Services.AddBlazoredLocalStorage();` line and the corresponding `using Blazored.LocalStorage;` directive if present.

If `IStorageClient`/`FirebaseStorageClient` types are not yet defined, the build will fail. Task 11 creates them. Either:
(a) defer this commit until after Task 11 — temporarily comment the `IStorageClient` line and uncomment it in Task 12, OR
(b) jump to Task 11 first, create `IStorageClient` + `FirebaseStorageClient` (no UI yet), come back to this step. RECOMMENDED: do Task 11 first, then come back to Task 10 Step 1. The plan is ordered this way so the test suite stays green between commits.

(Skip ahead to Task 11; once it's committed, return here.)

- [ ] **Step 2: Update `src/AdminTaos/AdminTaos.csproj` — remove `Blazored.LocalStorage`**

Find the `<PackageReference Include="Blazored.LocalStorage" … />` line and DELETE it. Save.

- [ ] **Step 3: Build + tests**

Run: `dotnet restore && dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed.

If a compile error mentions `Blazored.LocalStorage` namespace or `ILocalStorageService`, it means something still references it. Run:
```bash
grep -rn "Blazored\|ILocalStorageService" src tests
```
and remove the stale references (typically a forgotten `@using Blazored.LocalStorage` in `_Imports.razor` or a leftover `[Inject] ILocalStorageService Ls` somewhere).

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Program.cs src/AdminTaos/AdminTaos.csproj src/AdminTaos/_Imports.razor
git commit -m "chore(firebase): DI registers Firebase services; drop Blazored.LocalStorage"
```

---

## Phase 6 — Profile photo (Storage + UI)

### Task 11: `IStorageClient` + `FirebaseStorageClient`

**Files:**
- Create: `src/AdminTaos/Services/IStorageClient.cs`
- Create: `src/AdminTaos/Services/FirebaseStorageClient.cs`

- [ ] **Step 1: Create `src/AdminTaos/Services/IStorageClient.cs`**

```csharp
namespace AdminTaos.Services;

public interface IStorageClient
{
    /// <summary>Opens a native file picker, lets the user pick an image, compresses & uploads it to
    /// `avatars/{uid}.jpg`, and returns the public download URL. Returns null if the user cancels.
    /// Throws if the file is invalid (non-image or > 2 MB) or the upload fails.</summary>
    Task<string?> PickAndUploadAvatarAsync(string uid);
}
```

- [ ] **Step 2: Create `src/AdminTaos/Services/FirebaseStorageClient.cs`**

```csharp
using Microsoft.JSInterop;

namespace AdminTaos.Services;

public class FirebaseStorageClient : IStorageClient
{
    private readonly IJSRuntime _js;
    public FirebaseStorageClient(IJSRuntime js) { _js = js; }

    public Task<string?> PickAndUploadAvatarAsync(string uid)
        => _js.InvokeAsync<string?>("taos.pickAndUploadAvatar", uid).AsTask();
}
```

- [ ] **Step 3: Build + tests**

Run: `dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed.

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Services/IStorageClient.cs src/AdminTaos/Services/FirebaseStorageClient.cs
git commit -m "feat(firebase): IStorageClient + FirebaseStorageClient (avatar upload via taos.pickAndUploadAvatar)"
```

(Now return to Task 10 and complete its Steps 1–4 so DI registers everything cleanly.)

---

### Task 12: Profile pages — avatar section + "Changer ma photo" button

**Files:**
- Modify: `src/AdminTaos/Pages/Manager/MProfile.razor`
- Modify: `src/AdminTaos/Pages/Employee/EProfile.razor`

- [ ] **Step 1: Replace `src/AdminTaos/Pages/Manager/MProfile.razor` entirely with**

```razor
@page "/m/profile"
@layout DashboardLayout
@inject AuthState Auth
@inject IDataService Data
@inject IStorageClient Storage
@inject NavigationManager Nav

<h1>Profil</h1>

<div class="panel" style="display:flex;align-items:center;gap:18px">
    @if (!string.IsNullOrWhiteSpace(Auth.CurrentUser?.PhotoUrl))
    {
        <img src="@Auth.CurrentUser!.PhotoUrl" alt="Photo de profil"
             style="width:96px;height:96px;border-radius:50%;object-fit:cover;border:1px solid var(--line)" />
    }
    else
    {
        <div style="width:96px;height:96px;border-radius:50%;background:var(--goldbg);color:var(--gold);
             display:flex;align-items:center;justify-content:center;font-family:var(--pf);font-weight:700;font-size:32px">
            @Initials
        </div>
    }
    <div style="flex:1">
        <div class="t">@Auth.CurrentUser?.FullName</div>
        <div class="m">@Auth.CurrentUser?.Email · Manager</div>
        <button class="btn ghost" style="margin-top:10px" disabled="@_uploading" @onclick="ChangePhoto">
            @(_uploading ? "Téléversement…" : "Changer ma photo")
        </button>
        @if (_error is not null) { <p class="pill bad" style="display:block;margin-top:8px">@_error</p> }
    </div>
</div>

<button class="btn ghost" style="margin-top:16px" @onclick="Logout">Se déconnecter</button>

@code {
    bool _uploading;
    string? _error;

    string Initials => string.Concat((Auth.CurrentUser?.FullName ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(p => char.ToUpper(p[0]))) is { Length: > 0 } s ? s : "·";

    async Task ChangePhoto()
    {
        if (Auth.CurrentUser is null) return;
        _error = null; _uploading = true; StateHasChanged();
        try
        {
            var url = await Storage.PickAndUploadAvatarAsync(Auth.CurrentUser.Id);
            if (url is null) { return; } // user cancelled
            Auth.CurrentUser.PhotoUrl = url;
            await Data.UpdateAccountAsync(Auth.CurrentUser);
            Auth.Refresh();
        }
        catch (Exception ex) { _error = ex.Message ?? "Échec du téléversement"; }
        finally { _uploading = false; }
    }

    async Task Logout()
    {
        await Auth.LogoutAsync();
        Nav.NavigateTo("login", replace: true);
    }
}
```

- [ ] **Step 2: Replace `src/AdminTaos/Pages/Employee/EProfile.razor` entirely with**

(Identical body — the only differences are the route, layout-derived menu, role label "Employé", and the existing role-list line.)

```razor
@page "/e/profile"
@layout DashboardLayout
@inject AuthState Auth
@inject IDataService Data
@inject IStorageClient Storage
@inject NavigationManager Nav

<h1>Profil</h1>

<div class="panel" style="display:flex;align-items:center;gap:18px">
    @if (!string.IsNullOrWhiteSpace(Auth.CurrentUser?.PhotoUrl))
    {
        <img src="@Auth.CurrentUser!.PhotoUrl" alt="Photo de profil"
             style="width:96px;height:96px;border-radius:50%;object-fit:cover;border:1px solid var(--line)" />
    }
    else
    {
        <div style="width:96px;height:96px;border-radius:50%;background:var(--goldbg);color:var(--gold);
             display:flex;align-items:center;justify-content:center;font-family:var(--pf);font-weight:700;font-size:32px">
            @Initials
        </div>
    }
    <div style="flex:1">
        <div class="t">@Auth.CurrentUser?.FullName</div>
        <div class="m">@Auth.CurrentUser?.Email · @_roleNames</div>
        <button class="btn ghost" style="margin-top:10px" disabled="@_uploading" @onclick="ChangePhoto">
            @(_uploading ? "Téléversement…" : "Changer ma photo")
        </button>
        @if (_error is not null) { <p class="pill bad" style="display:block;margin-top:8px">@_error</p> }
    </div>
</div>

<button class="btn ghost" style="margin-top:16px" @onclick="Logout">Se déconnecter</button>

@code {
    bool _uploading;
    string? _error;
    string _roleNames = "Employé";

    string Initials => string.Concat((Auth.CurrentUser?.FullName ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(p => char.ToUpper(p[0]))) is { Length: > 0 } s ? s : "·";

    protected override async Task OnInitializedAsync()
    {
        var roles = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
        var ids = Auth.CurrentUser?.JobRoleIds ?? new();
        _roleNames = ids.Count == 0
            ? "Employé"
            : "Employé · " + string.Join(", ", ids.Select(id => roles.GetValueOrDefault(id, "?")));
    }

    async Task ChangePhoto()
    {
        if (Auth.CurrentUser is null) return;
        _error = null; _uploading = true; StateHasChanged();
        try
        {
            var url = await Storage.PickAndUploadAvatarAsync(Auth.CurrentUser.Id);
            if (url is null) return;
            Auth.CurrentUser.PhotoUrl = url;
            await Data.UpdateAccountAsync(Auth.CurrentUser);
            Auth.Refresh();
        }
        catch (Exception ex) { _error = ex.Message ?? "Échec du téléversement"; }
        finally { _uploading = false; }
    }

    async Task Logout()
    {
        await Auth.LogoutAsync();
        Nav.NavigateTo("login", replace: true);
    }
}
```

- [ ] **Step 3: Build + tests**

Run: `dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed.

- [ ] **Step 4: Commit**

```bash
git add src/AdminTaos/Pages/Manager/MProfile.razor src/AdminTaos/Pages/Employee/EProfile.razor
git commit -m "feat(firebase): profile pages — avatar (img/initials) + 'Changer ma photo'"
```

---

### Task 13: Sidebar foot avatar + DashTopbar conditional `<img>`

**Files:**
- Modify: `src/AdminTaos/Components/Sidebar.razor`
- Modify: `src/AdminTaos/Components/DashTopbar.razor`
- Modify: `src/AdminTaos/wwwroot/app.css` (small additions for `.ava`/`.ava-i`)

- [ ] **Step 1: Update `src/AdminTaos/Components/Sidebar.razor` — add an avatar `<img>`/initials block next to the `.who` line in `.foot`**

Locate the existing `<div class="foot"> … <div class="who">@Auth.CurrentUser?.FullName · @RoleLabel</div> … </div>` block. Replace it with:
```razor
    <div class="foot">
        <div class="who">
            @if (!string.IsNullOrWhiteSpace(Auth.CurrentUser?.PhotoUrl))
            {
                <img class="ava" src="@Auth.CurrentUser!.PhotoUrl" alt="" />
            }
            else
            {
                <span class="ava-i">@Initials</span>
            }
            <span class="who-txt">@Auth.CurrentUser?.FullName · @RoleLabel</span>
        </div>
        <button class="nav logout" @onclick="DoLogout">
            <span class="i">@Ico("logout")</span><span class="lbl">Se déconnecter</span>
        </button>
    </div>
```
Add the `Initials` getter inside the existing `@code { … }` block of `Sidebar.razor` (between `RoleLabel` and `Items`):
```csharp
    string Initials => string.Concat((Auth.CurrentUser?.FullName ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(p => char.ToUpper(p[0]))) is { Length: > 0 } s ? s : "·";
```
Keep everything else (DoLogout, Ico, OnNavigate, Items list, NavLink rendering) byte-for-byte.

- [ ] **Step 2: Update `src/AdminTaos/Components/DashTopbar.razor` — show `<img>` if `PhotoUrl` provided as parameter**

Replace the entire file with:
```razor
<header class="topbar">
    <button class="burger" @onclick="OnBurger" aria-label="Menu">☰</button>
    <span class="ttl">@Title</span>
    <span class="usr">
        @User
        @if (!string.IsNullOrWhiteSpace(PhotoUrl))
        {
            <img class="ava" src="@PhotoUrl" alt="" />
        }
        else
        {
            <span class="av">@Initials</span>
        }
    </span>
</header>

@code {
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string User { get; set; } = "";
    [Parameter] public string? PhotoUrl { get; set; }
    [Parameter] public EventCallback OnBurger { get; set; }
    string Initials => string.Concat((User ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(p => char.ToUpper(p[0]))) is { Length: > 0 } s ? s : "·";
}
```

Then in `src/AdminTaos/Layout/DashboardLayout.razor`, find the line:
```razor
<DashTopbar Title="@PageTitle" User="@(Auth.CurrentUser?.FullName ?? "")" OnBurger="Toggle" />
```
and add the `PhotoUrl` parameter:
```razor
<DashTopbar Title="@PageTitle" User="@(Auth.CurrentUser?.FullName ?? "")"
            PhotoUrl="@Auth.CurrentUser?.PhotoUrl" OnBurger="Toggle" />
```

- [ ] **Step 3: Append CSS to `src/AdminTaos/wwwroot/app.css`** (these classes are new; existing `.av` rule from Task 1 reskin already styles the topbar fallback initials chip — keep it)

Append at the end of the file:
```css
/* Avatar (img) — sidebar foot small + topbar */
.side .foot .who{display:flex;align-items:center;gap:10px}
.side .foot .who-txt{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.side .ava,.side .ava-i,.topbar .ava{width:28px;height:28px;border-radius:50%;flex:0 0 28px;
  display:inline-flex;align-items:center;justify-content:center}
.side .ava,.topbar .ava{object-fit:cover;border:1px solid var(--line2)}
.side .ava-i{background:rgba(231,199,107,.15);color:var(--sidegold);
  font-family:var(--pf);font-weight:700;font-size:11px}
.topbar .ava{border-color:var(--line)}
@media (max-width:1023px) and (min-width:768px){
  .side .foot .who{justify-content:center}
  .side .foot .who-txt{display:none}
}
```

- [ ] **Step 4: Build + tests**

Run: `dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed (Sidebar/DashTopbar are not under bUnit coverage).

- [ ] **Step 5: Commit**

```bash
git add src/AdminTaos/Components/Sidebar.razor src/AdminTaos/Components/DashTopbar.razor src/AdminTaos/Layout/DashboardLayout.razor src/AdminTaos/wwwroot/app.css
git commit -m "feat(firebase): sidebar foot + topbar show avatar (photo or initials)"
```

---

## Phase 7 — Firestore + Storage security rules

### Task 14: Create `firebase.json` + strict `firestore.rules` + `storage.rules`; deploy

**Files:**
- Create: `firebase.json` (repo root)
- Create: `firestore.rules` (repo root)
- Create: `storage.rules` (repo root)
- Create: `firestore.indexes.json` (repo root)

- [ ] **Step 1: Install Firebase CLI (user runs locally once)**

Run:
```bash
npm install -g firebase-tools
firebase login
```
(Opens a browser tab for Google sign-in. Use the same Google account that owns the Firebase project.)

- [ ] **Step 2: Create `firebase.json` at repo root**

```json
{
  "firestore": {
    "rules": "firestore.rules",
    "indexes": "firestore.indexes.json"
  },
  "storage": {
    "rules": "storage.rules"
  }
}
```

- [ ] **Step 3: Create `firestore.indexes.json`** (empty for now — no composite index needed in v1)

```json
{ "indexes": [], "fieldOverrides": [] }
```

- [ ] **Step 4: Create `firestore.rules`** — strict role/status-based rules

```
rules_version = '2';
service cloud.firestore {
  match /databases/{db}/documents {

    function isAuth()     { return request.auth != null; }
    function userDoc()    { return get(/databases/$(db)/documents/accounts/$(request.auth.uid)).data; }
    function isActive()   { return isAuth() && userDoc().status == "Active"; }
    function isManager()  { return isActive() && userDoc().type == "Manager"; }
    function isEmployee() { return isActive() && userDoc().type == "Employee"; }
    function isSelf(uid)  { return isAuth() && request.auth.uid == uid; }

    // Has the current user a Confirmed/Pending assignment on this event?
    function hasAssignmentOnEvent(eventId) {
      return exists(/databases/$(db)/documents/assignments)
             && false; // fallback unused — see queryByField helper below
    }

    // ---------- Accounts ----------
    match /accounts/{uid} {
      allow read: if isSelf(uid) || isManager();
      allow create: if isSelf(uid)
                    && request.resource.data.type == "Employee"
                    && request.resource.data.status == "Pending";
      // Self may update only profile fields; never elevate own type/status.
      allow update: if isManager()
                    || (isSelf(uid)
                        && request.resource.data.type   == resource.data.type
                        && request.resource.data.status == resource.data.status);
      allow delete: if isManager();
    }

    // ---------- Job roles ----------
    match /jobRoles/{id} {
      allow read:  if isActive();
      allow write: if isManager();
    }

    // ---------- Events ----------
    match /events/{eventId} {
      // Employees read events they're assigned to OR events open for signup.
      // To keep rules simple in v1, we let any Active user READ events; the UI still filters by assignment.
      // Write is manager-only.
      allow read:  if isActive();
      allow write: if isManager();
    }

    // ---------- Assignments ----------
    match /assignments/{id} {
      allow read:   if isManager()
                    || (isEmployee() && resource.data.accountId == request.auth.uid);
      allow create: if isManager()
                    || (isEmployee()
                        && request.resource.data.accountId == request.auth.uid
                        && request.resource.data.source    == "SelfRequest"
                        && request.resource.data.status    == "PendingApproval");
      allow update: if isManager();
      allow delete: if isManager();
    }

    // ---------- Timesheets ----------
    // Employees own a timesheet if its assignment belongs to them.
    function isMyAssignment(assignmentId) {
      let a = get(/databases/$(db)/documents/assignments/$(assignmentId)).data;
      return a.accountId == request.auth.uid;
    }

    match /timesheets/{id} {
      allow read:   if isManager()
                    || (isEmployee() && isMyAssignment(resource.data.assignmentId));
      allow create: if isManager()
                    || (isEmployee() && isMyAssignment(request.resource.data.assignmentId));
      allow update: if isManager()
                    || (isEmployee() && isMyAssignment(resource.data.assignmentId));
      allow delete: if isManager();
    }
  }
}
```

**Note on the Events read rule:** for v1 we allow any Active user to read all events (simpler; the UI already filters by assignment/open-signup). A tighter rule would gate event reads by "has-assignment-or-is-open"; that requires a `get()` per event in rules (no batch). Defer to v2 if data sensitivity grows. Spec §6 acknowledged "esquisse" — this is the final pragmatic decision.

- [ ] **Step 5: Create `storage.rules`**

```
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {
    match /avatars/{uid}.jpg {
      allow read:  if request.auth != null;
      allow write: if request.auth != null
                   && request.auth.uid == uid
                   && request.resource.size < 2 * 1024 * 1024
                   && request.resource.contentType.matches('image/.*');
    }
  }
}
```

- [ ] **Step 6: Deploy rules**

Run:
```bash
firebase use --add        # select the Firebase project; alias it "default" when prompted
firebase deploy --only firestore:rules,firestore:indexes,storage
```
Expected: each section reports `✔  Deploy complete!` and the rules show as active in the Firebase Console (Firestore → Rules tab; Storage → Rules tab).

- [ ] **Step 7: Build + tests** (no C# changed)

Run: `dotnet build` → 0 errors.
Run: `dotnet test` → 28 passed.

- [ ] **Step 8: Commit**

```bash
git add firebase.json firestore.rules storage.rules firestore.indexes.json
git commit -m "feat(firebase): strict firestore.rules + storage.rules (deployed)"
```

---

## Phase 8 — End-to-end verification on real Firebase

### Task 15: Run the full DoD scenario

**Files:** none (verification). Fix any defect found in the relevant page/service/rule + commit per fix.

- [ ] **Step 1: Final build + test + smoke**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
dotnet build 2>&1 | tail -3
dotnet test  2>&1 | tail -2
dotnet run --project src/AdminTaos --urls http://localhost:5270 &
P=$!
sleep 14
curl -s -o /dev/null -w "%{http_code} /\n"                http://localhost:5270/
curl -s -o /dev/null -w "%{http_code} /login\n"           http://localhost:5270/login
curl -s -o /dev/null -w "%{http_code} /firebase.js\n"     http://localhost:5270/firebase.js
curl -s -o /dev/null -w "%{http_code} /firebase-config.js\n" http://localhost:5270/firebase-config.js
kill $P 2>/dev/null
```
Expected: 28 passed, all routes 200. Leave the dev server up for the manual scenario below.

- [ ] **Step 2: DoD scenario in a browser** (`http://localhost:5270` open in a private window)

Walk through, ticking each off:
- [ ] On `/` → redirected to `/login`. No quick-login buttons. Email + Password fields shown.
- [ ] Click "Créer un compte" → fill name "Hervé Test", email "test1@taos.be", password "test1234", select at least one role → Submit → redirected to `/pending`. The doc `accounts/{uid}` is visible in the Firebase Console with `type=Employee, status=Pending`.
- [ ] In Firebase Console, edit that doc: `type=Manager, status=Active`. Save.
- [ ] Click "Se déconnecter" (from `/pending`), log in again → redirected to `/m`. Sidebar shows "Hervé Test · Manager". Topbar shows initials "HT".
- [ ] In `/m/roles`, create "Serveur" and "Hôtesse". Both appear (Firestore doc `jobRoles/{id}` created).
- [ ] In `/m/profile`, click "Changer ma photo" → pick an image → upload progresses → avatar swaps from "HT" to your photo. The doc `accounts/{uid}.photoUrl` is set. Reload the page — the photo persists.
- [ ] Create an event in `/m/events/new` for today. Assign yourself (or another seeded account) to it.
- [ ] Open a second private window, register `marc@taos.be / pwd1234` with role "Serveur" → land on `/pending`.
- [ ] Back as Manager, in `/m/team` → "En attente" tab → open Marc → "Valider le compte" → Marc now Active.
- [ ] Log in as Marc → he sees the event in `/e/events`. The `Auth.CurrentUser.PhotoUrl` is null → initials "M" shown in sidebar foot & topbar.
- [ ] Marc clicks "Commencer mon shift" → /active screen with Chrono → "Terminer mon shift" → /recap → "Envoyer ma timesheet pour validation" → confirmation pill.
- [ ] Manager sees the timesheet in `/m/timesheets` → opens it → adjusts the End time → "Valider" → status "Validée". Marc sees "Validée" in `/e/hours`.
- [ ] Negative rules check (manager-side): open another private window, sign up as a new employee `mallory@taos.be / pwd1234` → status Pending (unverified by manager). Try to navigate to `/e/events` — `Auth.CurrentUser.Status` is Pending → guard redirects to `/pending`. Now in Firebase Console, view "Rules → Playground", simulate `read` on `/databases/(default)/documents/timesheets/{any-id}` as auth uid=mallory's-uid → expected: **denied** (because mallory isn't Active).
- [ ] Storage rule check: in Console "Rules Playground" for Storage, simulate `write` on `/avatars/<other-uid>.jpg` as mallory → expected: **denied**.

- [ ] **Step 3: Stop the dev server** (Ctrl+C). Fix any defect found above:
- Markup/UX issues → fix the offending `.razor`, commit `fix: <issue> (DoD)`.
- Service mapping issues (e.g. DateOnly not round-tripping) → fix `FirestoreDataService` or `firebase.js` helpers, commit `fix: <issue> (DoD)`.
- Security rule issues → fix `firestore.rules`/`storage.rules` + redeploy + commit.

- [ ] **Step 4: Final summary commit**

```bash
git add -A
git commit -m "chore(firebase): Firebase v1 complete — auth + Firestore + Storage avatars + strict rules" --allow-empty
git log --oneline -20
```

---

## Self-Review

**1. Spec coverage:**
- §2 Scope (Auth, Firestore CRUD, Storage avatars, strict rules) → Tasks 1–14 ✓
- §3 Bootstrap & cycle de vie (sign-up Pending, manual Manager in console, in-app validation) → Task 6 (register), Task 15 Step 2 (manual edit) ✓
- §4 Architecture (Firebase JS SDK v10 modular, IAuthClient + FirebaseAuthClient, IStorageClient + FirebaseStorageClient, FirestoreDataService, AuthState refactor, model PhotoUrl, no other @code changes, Blazored.LocalStorage removed) → Tasks 2–13 ✓
- §5 Schéma Firestore (accounts/{uid}, jobRoles/{id}, events/{id}, assignments/{id}, timesheets/{id} with the exact field shapes & camelCase + enum-as-string + Timestamp/string conventions) → Task 9 (FirestoreDataService) + Task 2 (firebase.js conversions) ✓
- §5b Photos de profil (Storage layout, upload flow with client-side compression 512px JPEG q≈.85, fallback initials, sidebar/topbar/profil display, 2 MB limit, image/* check) → Tasks 11–13 ✓
- §6 Security rules baseline strict (Firestore + Storage) → Task 14 ✓
- §7 Tests (29 → 28: keep ModelTests/DataServiceTests/NavigationGuardTests/Chrono/Stepper; refactor AuthStateTests with FakeAuthClient; remove Login_lists_five_seeded_test_accounts) → Tasks 4, 5, 7 ✓
- §8 Config & dev (Spark plan, enable Auth Email/Password + Firestore prod + Storage prod, firebase-config.js, single project, no emulator) → Tasks 1, 14 ✓
- §9 DoD (28/28 tests, end-to-end on real Firebase incl. photo upload & initials fallback, negative rules check) → Task 15 ✓
- §10 Phases — plan phases 1–8 mirror spec §10's 8 phases ✓

**2. Placeholder scan:** No "TBD/TODO" left. Every step has concrete code/commands. The hard-to-test paths (real Firebase calls) are exercised in Task 15's manual DoD walkthrough rather than as in-process tests, per spec §7 (no emulator in v1).

**3. Type consistency:**
- `IAuthClient` signatures (Task 3) match exactly how `FirebaseAuthClient` (Task 4), `FakeAuthClient` (Task 4), and `AuthState` (Task 5) consume them.
- `IDataService` signatures unchanged from base spec; `FirestoreDataService` (Task 9) implements all 23 methods with the exact same return types as `InMemoryDataService`.
- `Account.PhotoUrl` (Task 8) is the same name used in Login/Register/AuthState (no), Profile pages (Task 12), Sidebar (Task 13), DashTopbar (Task 13).
- `IStorageClient.PickAndUploadAvatarAsync(uid)` (Task 11) is called from MProfile/EProfile (Task 12) with `Auth.CurrentUser.Id` — types match (`string`).
- Firestore field names in `firestore.rules` (Task 14) — `type`, `status`, `accountId`, `source`, `assignmentId` — match the camelCase serialization produced by `JsonSerializerDefaults.Web` in `FirestoreDataService` (Task 9). PascalCase enum values ("Manager"/"Employee"/"Active"/"Pending"/"SelfRequest"/"PendingApproval") match the `JsonStringEnumConverter` output.

**4. Task ordering pitfall:** Task 10 (DI swap) registers `IStorageClient` which is defined in Task 11. The plan explicitly says to do Task 11 first then return to Task 10 — flagged inside Task 10 Step 1.

---

## Execution Handoff

(Presented after saving the plan.)
