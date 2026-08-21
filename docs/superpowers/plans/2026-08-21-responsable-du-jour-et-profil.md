# Responsable du jour & profil enrichi — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettre à un collaborateur de renseigner ses coordonnées de paie, et à un responsable désigné par event de voir son équipe du jour et d'en pointer les présences.

**Architecture:** Le modèle binaire de l'application (`AccountType` décide des droits *et* de l'espace `/m` ou `/e`) reste intact. Le responsable du jour est un champ sur l'event, pas un type de compte ; la page d'équipe est un composant unique exposé sous deux routes pour que `NavigationGuard` laisse entrer chacun par sa porte. Le pointage de présence est un champ sur l'assignation, indépendant du chronomètre des timesheets.

**Tech Stack:** Blazor WebAssembly (.NET 10), Firestore via interop JS, xUnit + bUnit 2.7, Firebase Hosting.

**Spec:** `docs/superpowers/specs/2026-08-21-responsable-du-jour-et-profil-design.md`

## Global Constraints

- Langue de toute l'interface : français, tutoiement, cohérent avec l'existant.
- Vocabulaire imposé : « Admin » et « Collaborateur » (jamais Manager/Employé côté UI), « Rôles métier », « Suspendu » pour `AccountStatus.Rejected`.
- `AccountType` et `NavigationGuard` ne doivent pas être modifiés.
- Aucune migration de données : tout nouveau champ est optionnel ou porte une valeur par défaut.
- Les champs `ServiceEvent.OnSiteContact` et `RoleNeed.HourlyRate` restent dans le modèle même sans éditeur.
- Hors périmètre, ne pas implémenter : validation des heures par le responsable, démarrage d'un shift à la place d'un autre, notifications, calcul de rémunération, historique des pointages.
- Chaque tâche se termine par `dotnet test` vert sur la suite entière, puis un commit.
- Le déploiement (`dotnet publish` + `firebase deploy` + `git push`) n'a lieu qu'aux tâches 3, 8 et 9.

---

## File Structure

| Fichier | Responsabilité | Tâche |
|---|---|---|
| `src/AdminTaos/Services/IbanFormat.cs` | *Créé.* Normalisation et validation structurelle d'un IBAN, sans dépendance UI | 1 |
| `src/AdminTaos/Models/Account.cs` | *Modifié.* Trois champs de coordonnées | 1 |
| `src/AdminTaos/Components/ProfileInfoPanel.razor` | *Créé.* Panneau éditable partagé par les deux profils | 2 |
| `src/AdminTaos/Pages/Employee/EProfile.razor` | *Modifié.* Accueille le panneau | 2 |
| `src/AdminTaos/Pages/Manager/MProfile.razor` | *Modifié.* Accueille le panneau | 2 |
| `src/AdminTaos/Pages/Manager/MEmployeeDetail.razor` | *Modifié.* Affiche les coordonnées en lecture seule | 3 |
| `src/AdminTaos/Models/Enums.cs` | *Modifié.* `PresenceStatus` | 4 |
| `src/AdminTaos/Models/ServiceEvent.cs` | *Modifié.* `ResponsableAccountId` | 4 |
| `src/AdminTaos/Models/Assignment.cs` | *Modifié.* `Presence` | 4 |
| `src/AdminTaos/Pages/Manager/MEventDetail.razor` | *Modifié.* Affiche le responsable, puis le décompte et le lien | 4, 8 |
| `src/AdminTaos/Pages/Manager/MEventEdit.razor` | *Modifié.* Sélecteur de responsable à la place du contact sur place | 5 |
| `src/AdminTaos/Pages/Shared/EventTeam.razor` | *Créé.* Page d'équipe du jour, double route, contrôle d'accès, pointage | 6, 7 |
| `src/AdminTaos/Pages/Employee/EEventDetail.razor` | *Modifié.* Bandeau et lien pour le responsable | 8 |
| `firestore.rules` | *Modifié.* Lecture des assignments ouverte, écriture du seul champ `presence` | 9 |
| `tests/AdminTaos.Tests/FakeStorageClient.cs` | *Créé.* Stub d'`IStorageClient` pour rendre les pages de profil | 2 |
| `tests/AdminTaos.Tests/IbanFormatTests.cs` | *Créé.* | 1 |
| `tests/AdminTaos.Tests/ProfileEditTests.cs` | *Créé.* | 2, 3 |
| `tests/AdminTaos.Tests/ResponsableTests.cs` | *Créé.* | 4, 5 |
| `tests/AdminTaos.Tests/EventTeamTests.cs` | *Créé.* | 6, 7, 8 |

---

## Task 1 : IBAN et champs de coordonnées

**Files:**
- Create: `src/AdminTaos/Services/IbanFormat.cs`
- Modify: `src/AdminTaos/Models/Account.cs`
- Test: `tests/AdminTaos.Tests/IbanFormatTests.cs`

**Interfaces:**
- Consumes: rien.
- Produces: `AdminTaos.Services.IbanFormat.Normalize(string?) -> string?` et `IbanFormat.IsAcceptable(string?) -> bool`. Champs `Account.Phone`, `Account.PostalAddress`, `Account.Iban`, tous `string?` et nuls par défaut.

- [ ] **Step 1: Écrire les tests qui échouent**

Créer `tests/AdminTaos.Tests/IbanFormatTests.cs` :

```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Xunit;

namespace AdminTaos.Tests;

public class IbanFormatTests
{
    [Theory]
    [InlineData("be68 5390 0754 7034", "BE68539007547034")]
    [InlineData("  FR7630006000011234567890189  ", "FR7630006000011234567890189")]
    [InlineData("BE68\t5390\n0754 7034", "BE68539007547034")]
    public void Normalize_uppercases_and_strips_whitespace(string raw, string expected)
        => Assert.Equal(expected, IbanFormat.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_turns_blank_into_null(string? raw)
        => Assert.Null(IbanFormat.Normalize(raw));

    [Fact]
    public void An_absent_iban_is_acceptable()
        => Assert.True(IbanFormat.IsAcceptable(null));

    [Theory]
    [InlineData("BE68539007547034")]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("NL91ABNA0417164300")]
    public void A_structurally_valid_iban_is_acceptable(string value)
        => Assert.True(IbanFormat.IsAcceptable(value));

    [Theory]
    [InlineData("1234567890123456")]   // pas de code pays
    [InlineData("BEXX539007547034")]   // lettres à la place des chiffres de contrôle
    [InlineData("BE68")]               // trop court
    [InlineData("BE68-5390-0754-7034")] // la ponctuation survit à la normalisation
    public void A_malformed_iban_is_rejected(string value)
        => Assert.False(IbanFormat.IsAcceptable(value));

    [Fact]
    public void A_new_account_carries_no_contact_details()
    {
        var a = new Account();
        Assert.Null(a.Phone);
        Assert.Null(a.PostalAddress);
        Assert.Null(a.Iban);
    }
}
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~IbanFormatTests"`
Expected: échec de compilation — `IbanFormat` n'existe pas, `Account.Phone` non plus.

- [ ] **Step 3: Créer `src/AdminTaos/Services/IbanFormat.cs`**

```csharp
using System.Text.RegularExpressions;

namespace AdminTaos.Services;

/// <summary>
/// Traitement structurel d'un IBAN : on normalise ce qui entre, on refuse ce qui ne peut pas
/// en être un. Pas de contrôle de la clé modulo 97 — le coût dépasse le bénéfice ici, et un
/// IBAN structurellement correct mais faux reste rattrapable à la relecture d'un virement.
/// </summary>
public static class IbanFormat
{
    private static readonly Regex Shape =
        new("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.Compiled);

    /// <summary>Majuscules, espaces supprimés. Une saisie vide devient null.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var compact = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }

    /// <summary>Vrai si la valeur est absente (c'est permis) ou structurellement un IBAN.</summary>
    public static bool IsAcceptable(string? normalized)
        => normalized is null || Shape.IsMatch(normalized);
}
```

- [ ] **Step 4: Ajouter les trois champs à `src/AdminTaos/Models/Account.cs`**

Insérer après la propriété `PhotoUrl` :

```csharp
    public string? Phone { get; set; }
    public string? PostalAddress { get; set; }
    public string? Iban { get; set; }
```

- [ ] **Step 5: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS, aucun test existant cassé.

- [ ] **Step 6: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Services/IbanFormat.cs src/AdminTaos/Models/Account.cs tests/AdminTaos.Tests/IbanFormatTests.cs
git commit -m "feat(profil): coordonnées sur Account + validation structurelle d'IBAN"
```

---

## Task 2 : Panneau « Mes informations » dans les deux profils

**Files:**
- Create: `src/AdminTaos/Components/ProfileInfoPanel.razor`
- Create: `tests/AdminTaos.Tests/FakeStorageClient.cs`
- Modify: `src/AdminTaos/Pages/Employee/EProfile.razor`
- Modify: `src/AdminTaos/Pages/Manager/MProfile.razor`
- Test: `tests/AdminTaos.Tests/ProfileEditTests.cs`

**Interfaces:**
- Consumes: `IbanFormat.Normalize`, `IbanFormat.IsAcceptable`, `Account.Phone/PostalAddress/Iban` (tâche 1).
- Produces: composant `ProfileInfoPanel` avec deux paramètres — `[Parameter, EditorRequired] Account Account` et `[Parameter] EventCallback OnSaved`. Marqueurs CSS stables pour les tests : `input.pf-phone`, `input.pf-address`, `input.pf-iban`, `button.pf-save`.

- [ ] **Step 1: Créer le stub de stockage**

Créer `tests/AdminTaos.Tests/FakeStorageClient.cs` :

```csharp
using AdminTaos.Services;

namespace AdminTaos.Tests;

/// <summary>IStorageClient inerte : les pages de profil l'injectent, aucun test ne téléverse.</summary>
public class FakeStorageClient : IStorageClient
{
    public Task<string?> PickAndUploadAvatarAsync(string uid) => Task.FromResult<string?>(null);
}
```

- [ ] **Step 2: Écrire les tests qui échouent**

Créer `tests/AdminTaos.Tests/ProfileEditTests.cs` :

```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ProfileEditTests : BunitContext
{
    async Task<InMemoryDataService> SignIn(Account who)
    {
        var db = new InMemoryDataService();
        await db.CreateAccountAsync(who);
        var fake = new FakeAuthClient();
        fake.PreRegister(who.Id, who.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(who.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        Services.AddSingleton<IStorageClient>(new FakeStorageClient());
        return db;
    }

    static Account Collaborateur() => new() {
        Id = "acc-p", FullName = "Test P.", Email = "p@taos.be",
        Type = AccountType.Employee, Status = AccountStatus.Active,
        JobRoleIds = new() { SeedData.RoleServer } };

    static Account Admin() => new() {
        Id = "acc-adm", FullName = "Admin A.", Email = "adm@taos.be",
        Type = AccountType.Manager, Status = AccountStatus.Active };

    [Fact]
    public async Task Collaborateur_saves_phone_address_and_iban()
    {
        var db = await SignIn(Collaborateur());
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        cut.Find("input.pf-phone").Change("0470 12 34 56");
        cut.Find("input.pf-address").Change("Rue des Bouchers 12, 1000 Bruxelles");
        cut.Find("input.pf-iban").Change("be68 5390 0754 7034");
        cut.Find("button.pf-save").Click();

        var saved = (await db.GetAccountAsync("acc-p"))!;
        Assert.Equal("0470 12 34 56", saved.Phone);
        Assert.Equal("Rue des Bouchers 12, 1000 Bruxelles", saved.PostalAddress);
        Assert.Equal("BE68539007547034", saved.Iban);
    }

    [Fact]
    public async Task Blank_fields_are_stored_as_null_not_empty_strings()
    {
        var who = Collaborateur();
        who.Phone = "0470"; who.PostalAddress = "X"; who.Iban = "BE68539007547034";
        var db = await SignIn(who);
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        cut.Find("input.pf-phone").Change("   ");
        cut.Find("input.pf-address").Change("");
        cut.Find("input.pf-iban").Change("");
        cut.Find("button.pf-save").Click();

        var saved = (await db.GetAccountAsync("acc-p"))!;
        Assert.Null(saved.Phone);
        Assert.Null(saved.PostalAddress);
        Assert.Null(saved.Iban);
    }

    [Fact]
    public async Task A_malformed_iban_is_refused_and_nothing_is_saved()
    {
        var db = await SignIn(Collaborateur());
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        cut.Find("input.pf-phone").Change("0470 12 34 56");
        cut.Find("input.pf-iban").Change("PAS-UN-IBAN");
        cut.Find("button.pf-save").Click();

        Assert.Contains("IBAN invalide", cut.Markup);
        var saved = (await db.GetAccountAsync("acc-p"))!;
        Assert.Null(saved.Iban);
        Assert.Null(saved.Phone);   // rien n'est écrit tant que la saisie est refusée
    }

    [Fact]
    public async Task An_admin_has_the_same_panel()
    {
        var db = await SignIn(Admin());
        var cut = Render<AdminTaos.Pages.Manager.MProfile>();

        cut.Find("input.pf-phone").Change("0499 99 99 99");
        cut.Find("button.pf-save").Click();

        Assert.Equal("0499 99 99 99", (await db.GetAccountAsync("acc-adm"))!.Phone);
    }

    [Fact]
    public async Task Existing_values_are_prefilled()
    {
        var who = Collaborateur();
        who.Phone = "0470 11 22 33";
        who.Iban = "BE68539007547034";
        await SignIn(who);
        var cut = Render<AdminTaos.Pages.Employee.EProfile>();

        Assert.Equal("0470 11 22 33", cut.Find("input.pf-phone").GetAttribute("value"));
        Assert.Equal("BE68539007547034", cut.Find("input.pf-iban").GetAttribute("value"));
    }
}
```

- [ ] **Step 3: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~ProfileEditTests"`
Expected: FAIL — `Bunit.ElementNotFoundException` sur `input.pf-phone`.

- [ ] **Step 4: Créer `src/AdminTaos/Components/ProfileInfoPanel.razor`**

```razor
@inject IDataService Data

<div class="lab">Mes informations</div>
<div class="panel">
    <div class="field"><span>Téléphone</span>
        <input class="pf-phone" type="tel" inputmode="tel" @bind="_phone" placeholder="0470 00 00 00" /></div>
    <div class="field"><span>Adresse postale</span>
        <input class="pf-address" @bind="_address" placeholder="Rue, numéro, code postal, ville" /></div>
    <div class="field"><span>IBAN</span>
        <input class="pf-iban" @bind="_iban" placeholder="BE68 5390 0754 7034" /></div>

    @if (_err is not null) { <p class="pill bad" style="display:block;margin-top:10px">@_err</p> }
    else if (_saved) { <p class="pill ok" style="display:block;margin-top:10px">Informations enregistrées</p> }

    <button class="btn primary block pf-save" style="margin-top:12px" @onclick="Save">Enregistrer</button>
</div>

@code {
    [Parameter, EditorRequired] public Account Account { get; set; } = default!;
    [Parameter] public EventCallback OnSaved { get; set; }

    string? _phone, _address, _iban, _err;
    bool _saved;

    protected override void OnInitialized()
    {
        _phone = Account.Phone;
        _address = Account.PostalAddress;
        _iban = Account.Iban;
    }

    async Task Save()
    {
        _err = null; _saved = false;

        // Rien n'est écrit tant que l'IBAN est refusé : une sauvegarde partielle laisserait
        // l'utilisateur croire que tout est passé.
        var iban = IbanFormat.Normalize(_iban);
        if (!IbanFormat.IsAcceptable(iban))
        {
            _err = "IBAN invalide. Format attendu : deux lettres, deux chiffres, puis le numéro de compte.";
            return;
        }

        Account.Phone = Blank(_phone);
        Account.PostalAddress = Blank(_address);
        Account.Iban = iban;
        await Data.UpdateAccountAsync(Account);

        _iban = iban;   // renvoie la version normalisée à l'écran
        _saved = true;
        await OnSaved.InvokeAsync();
    }

    static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
```

- [ ] **Step 5: Brancher le panneau dans `EProfile.razor`**

Dans `src/AdminTaos/Pages/Employee/EProfile.razor`, insérer entre le panneau photo et le bouton « Se déconnecter » :

```razor
@if (Auth.CurrentUser is not null)
{
    <ProfileInfoPanel Account="Auth.CurrentUser" OnSaved="@(() => Auth.Refresh())" />
}
```

- [ ] **Step 6: Brancher le panneau dans `MProfile.razor`**

Dans `src/AdminTaos/Pages/Manager/MProfile.razor`, insérer au même endroit — entre le panneau photo et le bouton « Se déconnecter » — le bloc identique :

```razor
@if (Auth.CurrentUser is not null)
{
    <ProfileInfoPanel Account="Auth.CurrentUser" OnSaved="@(() => Auth.Refresh())" />
}
```

- [ ] **Step 7: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Components/ProfileInfoPanel.razor src/AdminTaos/Pages/Employee/EProfile.razor src/AdminTaos/Pages/Manager/MProfile.razor tests/AdminTaos.Tests/FakeStorageClient.cs tests/AdminTaos.Tests/ProfileEditTests.cs
git commit -m "feat(profil): panneau Mes informations éditable dans les deux profils"
```

---

## Task 3 : Coordonnées visibles par l'Admin, puis déploiement du chantier A

**Files:**
- Modify: `src/AdminTaos/Pages/Manager/MEmployeeDetail.razor`
- Test: `tests/AdminTaos.Tests/ProfileEditTests.cs`

**Interfaces:**
- Consumes: `Account.Phone/PostalAddress/Iban` (tâche 1).
- Produces: rien de nouveau pour les tâches suivantes.

- [ ] **Step 1: Écrire le test qui échoue**

Ajouter à la classe `ProfileEditTests` dans `tests/AdminTaos.Tests/ProfileEditTests.cs` :

```csharp
    [Fact]
    public async Task Admin_sees_the_contact_details_of_a_collaborateur()
    {
        var db = await SignIn(Admin());
        var target = Collaborateur();
        target.Phone = "0470 12 34 56";
        target.PostalAddress = "Rue des Bouchers 12, 1000 Bruxelles";
        target.Iban = "BE68539007547034";
        await db.CreateAccountAsync(target);

        var cut = Render<AdminTaos.Pages.Manager.MEmployeeDetail>(p => p.Add(x => x.Id, target.Id));

        Assert.Contains("0470 12 34 56", cut.Markup);
        Assert.Contains("Rue des Bouchers 12", cut.Markup);
        Assert.Contains("BE68539007547034", cut.Markup);
    }

    [Fact]
    public async Task Missing_contact_details_show_a_dash_not_an_empty_row()
    {
        var db = await SignIn(Admin());
        var target = Collaborateur();
        await db.CreateAccountAsync(target);

        var cut = Render<AdminTaos.Pages.Manager.MEmployeeDetail>(p => p.Add(x => x.Id, target.Id));
        var row = cut.FindAll(".field").Single(f => f.TextContent.Contains("IBAN"));
        Assert.Contains("—", row.TextContent);
    }
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~ProfileEditTests"`
Expected: FAIL — la chaîne `0470 12 34 56` est absente du markup.

- [ ] **Step 3: Ajouter les trois lignes dans `MEmployeeDetail.razor`**

Dans le premier `<div class="panel">`, juste après la ligne du champ « Statut », ajouter :

```razor
        <div class="field"><span>Téléphone</span><b>@(_a.Phone ?? "—")</b></div>
        <div class="field"><span>Adresse</span><b>@(_a.PostalAddress ?? "—")</b></div>
        <div class="field"><span>IBAN</span><b>@(_a.Iban ?? "—")</b></div>
```

- [ ] **Step 4: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Pages/Manager/MEmployeeDetail.razor tests/AdminTaos.Tests/ProfileEditTests.cs
git commit -m "feat(profil): coordonnées visibles par l'Admin sur la fiche d'un collaborateur"
```

- [ ] **Step 6: Publier et déployer le chantier A**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
rm -rf publish
dotnet publish src/AdminTaos/AdminTaos.csproj -c Release -o publish
firebase deploy --only hosting
git push origin firebase-v1
```
Expected: `Deploy complete!` et `https://admintaos.web.app` répond 200.

---

## Task 4 : Modèle du responsable et affichage sur la fiche event

**Files:**
- Modify: `src/AdminTaos/Models/Enums.cs`
- Modify: `src/AdminTaos/Models/ServiceEvent.cs`
- Modify: `src/AdminTaos/Models/Assignment.cs`
- Modify: `src/AdminTaos/Pages/Manager/MEventDetail.razor`
- Test: `tests/AdminTaos.Tests/ResponsableTests.cs`

**Interfaces:**
- Consumes: `Account.Phone` (tâche 1).
- Produces: `enum PresenceStatus { Expected, Present, Absent }` ; `ServiceEvent.ResponsableAccountId` de type `string?` ; `Assignment.Presence` de type `PresenceStatus` valant `Expected` par défaut.

- [ ] **Step 1: Écrire les tests qui échouent**

Créer `tests/AdminTaos.Tests/ResponsableTests.cs` :

```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class ResponsableTests : BunitContext
{
    InMemoryDataService Db()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        return db;
    }

    [Fact]
    public void New_models_carry_safe_defaults()
    {
        Assert.Null(new ServiceEvent().ResponsableAccountId);
        Assert.Equal(PresenceStatus.Expected, new Assignment().Presence);
        Assert.Equal(3, System.Enum.GetValues<PresenceStatus>().Length);
    }

    [Fact]
    public async Task MEventDetail_names_the_responsable_with_their_phone()
    {
        var db = Db();
        var mgr = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        mgr.Phone = "0470 12 34 56";
        await db.UpdateAccountAsync(mgr);

        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = mgr.Id;
        await db.UpdateEventAsync(e);

        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("Responsable du jour", cut.Markup);
        Assert.Contains("Marc D.", cut.Markup);
        Assert.Contains("0470 12 34 56", cut.Markup);
    }

    [Fact]
    public void MEventDetail_falls_back_to_the_legacy_contact_when_no_responsable_is_set()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        // evt-gala porte OnSiteContact = "Julie — 0470 00 00 00" et aucun responsable.
        Assert.Contains("Julie", cut.Markup);
        Assert.DoesNotContain("Responsable du jour", cut.Markup);
    }
}
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~ResponsableTests"`
Expected: échec de compilation — `PresenceStatus` et `ResponsableAccountId` n'existent pas.

- [ ] **Step 3: Ajouter l'énumération dans `src/AdminTaos/Models/Enums.cs`**

Ajouter en fin de fichier :

```csharp
public enum PresenceStatus { Expected, Present, Absent }
```

- [ ] **Step 4: Ajouter le champ sur `src/AdminTaos/Models/ServiceEvent.cs`**

Insérer juste après la propriété `OnSiteContact` :

```csharp
    /// <summary>Compte désigné responsable pour cet event. Remplace OnSiteContact, conservé en repli.</summary>
    public string? ResponsableAccountId { get; set; }
```

- [ ] **Step 5: Ajouter le champ sur `src/AdminTaos/Models/Assignment.cs`**

Insérer juste après la propriété `Status` :

```csharp
    /// <summary>Pointage du responsable du jour. Indépendant du chronomètre de la timesheet.</summary>
    public PresenceStatus Presence { get; set; } = PresenceStatus.Expected;
```

- [ ] **Step 6: Afficher le responsable dans `MEventDetail.razor`**

Remplacer la ligne :

```razor
        @if(!string.IsNullOrWhiteSpace(_e.OnSiteContact)){<text><br/>Contact : @_e.OnSiteContact</text>}
```

par :

```razor
        @if (_responsable is not null)
        {
            <text><br/>Responsable du jour : @_responsable.FullName@(string.IsNullOrWhiteSpace(_responsable.Phone) ? "" : $" — {_responsable.Phone}")</text>
        }
        else if (!string.IsNullOrWhiteSpace(_e.OnSiteContact))
        {
            <text><br/>Contact : @_e.OnSiteContact</text>
        }
```

Dans le bloc `@code`, déclarer le champ à côté des autres :

```csharp
    Account? _responsable;
```

et, dans la méthode `Load()`, après le chargement de `_e`, ajouter :

```csharp
        _responsable = string.IsNullOrEmpty(_e?.ResponsableAccountId)
            ? null
            : await Data.GetAccountAsync(_e.ResponsableAccountId);
```

- [ ] **Step 7: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Models tests/AdminTaos.Tests/ResponsableTests.cs src/AdminTaos/Pages/Manager/MEventDetail.razor
git commit -m "feat(event): champ responsable du jour et statut de présence, affichés sur la fiche event"
```

---

## Task 5 : Sélecteur de responsable avec recherche

**Files:**
- Modify: `src/AdminTaos/Pages/Manager/MEventEdit.razor`
- Test: `tests/AdminTaos.Tests/ResponsableTests.cs`

**Interfaces:**
- Consumes: `ServiceEvent.ResponsableAccountId` (tâche 4).
- Produces: marqueurs CSS stables — `input.resp-search`, `button.resp-option[data-account-id]`, `button.resp-clear`.

- [ ] **Step 1: Écrire les tests qui échouent**

Ajouter à la classe `ResponsableTests` :

```csharp
    [Fact]
    public async Task The_search_filters_candidates_by_name_and_email()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("marc");
        Assert.Single(cut.FindAll("button.resp-option"));
        Assert.Contains("Marc D.", cut.Find("button.resp-option").TextContent);

        cut.Find("input.resp-search").Input("sarah@taos.be");
        Assert.Single(cut.FindAll("button.resp-option"));
        Assert.Contains("Sarah K.", cut.Find("button.resp-option").TextContent);

        await Task.CompletedTask;
    }

    [Fact]
    public void Suspended_and_pending_accounts_are_not_selectable()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("Tom");     // Tom V. est suspendu
        Assert.Empty(cut.FindAll("button.resp-option"));

        cut.Find("input.resp-search").Input("Léa");     // Léa B. est en attente
        Assert.Empty(cut.FindAll("button.resp-option"));
    }

    [Fact]
    public void An_admin_is_selectable_as_responsable()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("Hervé");
        Assert.Single(cut.FindAll("button.resp-option"));
    }

    [Fact]
    public async Task Picking_a_candidate_then_saving_persists_the_responsable()
    {
        var db = Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));

        cut.Find("input.resp-search").Input("marc");
        cut.Find($"button.resp-option[data-account-id='{SeedData.EmpActiveServer}']").Click();
        cut.Find("button.btn.primary.block").Click();

        Assert.Equal(SeedData.EmpActiveServer, (await db.GetEventAsync("evt-gala"))!.ResponsableAccountId);
    }

    [Fact]
    public async Task Clearing_the_responsable_puts_the_field_back_to_null()
    {
        var db = Db();
        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = SeedData.EmpActiveServer;
        await db.UpdateEventAsync(e);

        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));
        Assert.Contains("Marc D.", cut.Markup);

        cut.Find("button.resp-clear").Click();
        cut.Find("button.btn.primary.block").Click();

        Assert.Null((await db.GetEventAsync("evt-gala"))!.ResponsableAccountId);
    }

    [Fact]
    public void The_on_site_contact_text_field_is_gone()
    {
        Db();
        var cut = Render<AdminTaos.Pages.Manager.MEventEdit>(p => p.Add(x => x.Id, "evt-gala"));
        Assert.DoesNotContain("Contact sur place", cut.Markup);
    }
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~ResponsableTests"`
Expected: FAIL — `input.resp-search` introuvable.

- [ ] **Step 3: Retirer le champ « Contact sur place »**

Dans `src/AdminTaos/Pages/Manager/MEventEdit.razor`, supprimer cette ligne du premier panneau :

```razor
    <div class="field"><span>Contact sur place</span><input @bind="_e.OnSiteContact" /></div>
```

- [ ] **Step 4: Ajouter le panneau de désignation**

Insérer juste après la fermeture `</div>` du premier panneau, donc **avant** le bloc « Effectif par rôle » :

```razor
<div class="lab">Responsable du jour</div>
<div class="panel">
    @if (_responsable is not null)
    {
        <div class="field"><span>Désigné</span><b>@_responsable.FullName</b></div>
        <button class="btn ghost block resp-clear" style="margin-top:10px" @onclick="ClearResponsable">Aucun responsable</button>
    }
    else
    {
        <div class="field"><span>Rechercher</span>
            <input class="resp-search" @bind="_search" @bind:event="oninput" placeholder="Nom ou email" /></div>
        @foreach (var a in Candidates)
        {
            <button class="card resp-option" data-account-id="@a.Id" @onclick="() => PickResponsable(a)">
                <div class="t">@a.FullName</div>
                <div class="m">@a.Email · @(a.Type == AccountType.Manager ? "Admin" : "Collaborateur")</div>
            </button>
        }
        @if (Candidates.Count == 0)
        {
            <div class="m" style="margin-top:10px">Aucun compte actif ne correspond.</div>
        }
    }
</div>
```

- [ ] **Step 5: Ajouter la logique dans le bloc `@code` de `MEventEdit.razor`**

Déclarer les champs à côté de `_roles` :

```csharp
    List<Account> _accounts = new();
    Account? _responsable;
    string _search = "";
```

Ajouter les membres suivants après la méthode `SetCount` :

```csharp
    /// <summary>Comptes proposés : actifs uniquement, Admins compris. Sans recherche, on en montre
    /// huit pour ne pas noyer l'écran ; avec recherche, on élargit sans jamais tout déverser.</summary>
    IReadOnlyList<Account> Candidates
    {
        get
        {
            var q = _search.Trim();
            return _accounts
                .Where(a => q.Length == 0
                            || a.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || a.Email.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.FullName)
                .Take(q.Length == 0 ? 8 : 25)
                .ToList();
        }
    }

    void PickResponsable(Account a)
    {
        _responsable = a;
        _e.ResponsableAccountId = a.Id;
    }

    void ClearResponsable()
    {
        _responsable = null;
        _e.ResponsableAccountId = null;
        _search = "";
    }
```

Enfin, dans `OnInitializedAsync`, charger les comptes **avant** de résoudre le responsable — l'ordre compte, `_responsable` se résout depuis `_accounts` :

```csharp
    protected override async Task OnInitializedAsync()
    {
        _roles = await Data.GetJobRolesAsync();
        _accounts = (await Data.GetAccountsAsync())
            .Where(a => a.Status == AccountStatus.Active).ToList();
        _isNew = string.IsNullOrEmpty(Id);
        if (!_isNew)
        {
            _e = await Data.GetEventAsync(Id!) ?? new ServiceEvent();
            _date = _e.Date; _meet = _e.MeetingTime; _end = _e.ExpectedEndTime;
            _responsable = _accounts.FirstOrDefault(a => a.Id == _e.ResponsableAccountId);
        }
    }
```

- [ ] **Step 6: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS. Attention particulière aux tests `EventStaffingTests` qui sélectionnent `.field input` par index — le champ Nom doit rester le premier.

- [ ] **Step 7: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Pages/Manager/MEventEdit.razor tests/AdminTaos.Tests/ResponsableTests.cs
git commit -m "feat(event): désignation du responsable du jour par recherche, remplace le contact sur place"
```

---

## Task 6 : Page d'équipe du jour — accès et affichage

**Files:**
- Create: `src/AdminTaos/Pages/Shared/EventTeam.razor`
- Test: `tests/AdminTaos.Tests/EventTeamTests.cs`

**Interfaces:**
- Consumes: `ServiceEvent.ResponsableAccountId`, `Assignment.Presence` (tâche 4), `Account.Phone` (tâche 1).
- Produces: composant `AdminTaos.Pages.Shared.EventTeam` avec `[Parameter] public string Id`. Marqueurs CSS stables : `.team-member[data-account-id]`, `a.team-phone`, `.team-denied`.

- [ ] **Step 1: Écrire les tests qui échouent**

Créer `tests/AdminTaos.Tests/EventTeamTests.cs` :

```csharp
using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

public class EventTeamTests : BunitContext
{
    /// <summary>Connecte le compte donné et désigne Marc responsable du gala, sauf indication contraire.</summary>
    async Task<InMemoryDataService> Setup(string signedInAs, string? responsable = SeedData.EmpActiveServer)
    {
        var db = new InMemoryDataService();

        var e = (await db.GetEventAsync("evt-gala"))!;
        e.ResponsableAccountId = responsable;
        await db.UpdateEventAsync(e);

        // Sarah rejoint le gala pour que l'équipe compte deux personnes.
        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-gala-host", EventId = "evt-gala", AccountId = SeedData.EmpActiveHost,
            JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed });

        var me = (await db.GetAccountAsync(signedInAs))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);
        return db;
    }

    IRenderedComponent<AdminTaos.Pages.Shared.EventTeam> Page() =>
        Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-gala"));

    [Fact]
    public async Task The_designated_responsable_sees_the_team()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        Assert.Empty(cut.FindAll(".team-denied"));
        Assert.Equal(2, cut.FindAll(".team-member").Count);
        Assert.Contains("Marc D.", cut.Markup);
        Assert.Contains("Sarah K.", cut.Markup);
    }

    [Fact]
    public async Task An_admin_may_supervise_the_page()
    {
        await Setup(SeedData.MgrId);
        Assert.Empty(Page().FindAll(".team-denied"));
    }

    [Fact]
    public async Task A_collaborateur_who_is_not_the_responsable_is_refused()
    {
        await Setup(SeedData.EmpActiveHost);   // Sarah n'est pas responsable
        var cut = Page();

        Assert.Single(cut.FindAll(".team-denied"));
        Assert.Empty(cut.FindAll(".team-member"));
    }

    [Fact]
    public async Task The_event_briefing_is_shown()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        Assert.Contains("Hôtel Plaza", cut.Markup);
        Assert.Contains("Arriver 15 min avant", cut.Markup);
    }

    [Fact]
    public async Task A_phone_number_is_a_tel_link_and_its_absence_is_stated()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var sarah = (await db.GetAccountAsync(SeedData.EmpActiveHost))!;
        sarah.Phone = "0470 99 88 77";
        await db.UpdateAccountAsync(sarah);

        var cut = Page();

        var link = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] a.team-phone");
        Assert.Equal("tel:0470998877", link.GetAttribute("href"));

        var marc = cut.Find($".team-member[data-account-id='{SeedData.EmpActiveServer}']");
        Assert.Empty(marc.QuerySelectorAll("a.team-phone"));
        Assert.Contains("Téléphone non renseigné", marc.TextContent);
    }

    [Fact]
    public async Task Only_confirmed_assignments_appear()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        await db.CreateAssignmentAsync(new Assignment {
            Id = "asg-gala-pending", EventId = "evt-gala", AccountId = SeedData.EmpPending,
            JobRoleId = SeedData.RoleServer, Status = AssignmentStatus.PendingApproval });

        Assert.Equal(2, Page().FindAll(".team-member").Count);
    }
}
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~EventTeamTests"`
Expected: échec de compilation — le type `AdminTaos.Pages.Shared.EventTeam` n'existe pas.

- [ ] **Step 3: Créer `src/AdminTaos/Pages/Shared/EventTeam.razor`**

```razor
@page "/e/events/{Id}/equipe"
@page "/m/events/{Id}/equipe"
@layout DashboardLayout
@inject IDataService Data
@inject AuthState Auth
@inject NavigationManager Nav

@* Une seule page, deux routes : NavigationGuard n'inspecte que le premier segment du chemin,
   donc un Collaborateur entre par /e et un Admin par /m, sans toucher au guard. *@

<button class="backlink" @onclick="GoBack">‹ Détail event</button>

@if (_loading)
{
    <div class="panel"><div class="m">Chargement…</div></div>
}
else if (_e is null)
{
    <div class="panel"><p class="sub">Event introuvable.</p></div>
}
else if (!_allowed)
{
    <div class="panel team-denied">
        <div class="t">Accès réservé</div>
        <div class="m">Seul le responsable désigné pour cet event, ou un Admin, peut voir cette page.</div>
    </div>
}
else
{
    <h1>Équipe du jour</h1>
    <p class="sub">@_e.Name · @_e.Date.Fmt()</p>

    <div class="panel">
        <div class="m">@_e.Venue — @_e.Address<br/>@_e.MeetingTime.Fmt() → @_e.ExpectedEndTime.Fmt()</div>
    </div>

    @if (!string.IsNullOrWhiteSpace(_e.Instructions))
    {
        <div class="lab">Consignes</div>
        <div class="panel consignes"><p>@_e.Instructions</p></div>
    }

    <div class="lab">Effectif</div>
    <div class="panel"><div class="m">@_team.Count personne(s) confirmée(s)</div></div>

    @foreach (var group in _team.GroupBy(m => m.RoleId))
    {
        <div class="lab">@RoleName(group.Key)</div>
        @foreach (var m in group)
        {
            <div class="panel team-member" data-account-id="@m.Account.Id">
                <div class="t">@m.Account.FullName</div>
                @if (string.IsNullOrWhiteSpace(m.Account.Phone))
                {
                    <div class="m">Téléphone non renseigné</div>
                }
                else
                {
                    <div class="m"><a class="team-phone" href="@TelHref(m.Account.Phone)">@m.Account.Phone</a></div>
                }
            </div>
        }
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";

    public record Member(Account Account, Assignment Asg, string RoleId);

    ServiceEvent? _e;
    List<Member> _team = new();
    Dictionary<string,string> _roleNames = new();
    bool _allowed, _loading = true;

    protected override async Task OnInitializedAsync() => await Load();

    async Task Load()
    {
        _e = await Data.GetEventAsync(Id);
        if (_e is null) { _loading = false; return; }

        var me = Auth.CurrentUser;
        _allowed = me is not null
                   && (me.Type == AccountType.Manager || me.Id == _e.ResponsableAccountId);
        if (!_allowed) { _loading = false; return; }

        _roleNames = (await Data.GetJobRolesAsync()).ToDictionary(r => r.Id, r => r.Name);
        var accounts = (await Data.GetAccountsAsync()).ToDictionary(a => a.Id);

        _team = (await Data.GetAssignmentsForEventAsync(Id))
            .Where(a => a.Status == AssignmentStatus.Confirmed && accounts.ContainsKey(a.AccountId))
            .Select(a => new Member(accounts[a.AccountId], a, a.JobRoleId))
            .OrderBy(m => m.Account.FullName)
            .ToList();

        _loading = false;
    }

    string RoleName(string id) => _roleNames.GetValueOrDefault(id, "Rôle inconnu");

    /// <summary>Un lien tel: ne tolère pas les espaces de mise en forme du numéro.</summary>
    static string TelHref(string phone)
        => "tel:" + new string(phone.Where(c => !char.IsWhiteSpace(c)).ToArray());

    void GoBack()
    {
        var area = Auth.CurrentUser?.Type == AccountType.Manager ? "m" : "e";
        Nav.NavigateTo($"{area}/events/{Id}");
    }
}
```

- [ ] **Step 4: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Pages/Shared/EventTeam.razor tests/AdminTaos.Tests/EventTeamTests.cs
git commit -m "feat(equipe): page équipe du jour en double route, réservée au responsable et aux Admins"
```

---

## Task 7 : Pointage des présences

**Files:**
- Modify: `src/AdminTaos/Pages/Shared/EventTeam.razor`
- Test: `tests/AdminTaos.Tests/EventTeamTests.cs`

**Interfaces:**
- Consumes: `Assignment.Presence`, `PresenceStatus` (tâche 4) ; composant `EventTeam` (tâche 6).
- Produces: marqueurs CSS stables — `button.presence-present`, `button.presence-absent`, `.presence-count`.

- [ ] **Step 1: Écrire les tests qui échouent**

Ajouter à la classe `EventTeamTests` :

```csharp
    [Fact]
    public async Task Everyone_starts_as_expected()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Page();
        Assert.Contains("0 présent · 0 absent · 2 attendus", cut.Find(".presence-count").TextContent);
    }

    [Fact]
    public async Task Marking_someone_present_persists_and_updates_the_count()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var cut = Page();

        cut.Find($".team-member[data-account-id='{SeedData.EmpActiveHost}'] button.presence-present").Click();

        var asg = (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveHost);
        Assert.Equal(PresenceStatus.Present, asg.Presence);
        Assert.Contains("1 présent · 0 absent · 1 attendu", cut.Find(".presence-count").TextContent);
    }

    [Fact]
    public async Task Marking_someone_absent_then_present_again_switches_cleanly()
    {
        var db = await Setup(SeedData.EmpActiveServer);
        var cut = Page();
        var row = $".team-member[data-account-id='{SeedData.EmpActiveHost}']";

        cut.Find($"{row} button.presence-absent").Click();
        Assert.Equal(PresenceStatus.Absent, (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveHost).Presence);

        cut.Find($"{row} button.presence-present").Click();
        Assert.Equal(PresenceStatus.Present, (await db.GetAssignmentsForEventAsync("evt-gala"))
            .Single(a => a.AccountId == SeedData.EmpActiveHost).Presence);
    }

    [Fact]
    public async Task A_refused_write_is_shown_instead_of_failing_silently()
    {
        // Un seul service pour l'authentification ET la page, sinon les deux voient des données
        // différentes. RefusingDataService reconstruit le seed puis désigne le responsable.
        var db = new RefusingDataService();

        var me = (await db.GetAccountAsync(SeedData.EmpActiveServer))!;
        var fake = new FakeAuthClient();
        fake.PreRegister(me.Id, me.Email, "pwd");
        var auth = new AuthState(fake, db);
        await auth.LoginAsync(me.Email, "pwd");

        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(auth);

        var cut = Page();
        cut.FindAll("button.presence-present").First().Click();

        Assert.Contains("Échec du pointage", cut.Markup);
    }

    /// <summary>Lit normalement, refuse toute écriture d'assignation — tient lieu de refus des règles Firestore.</summary>
    sealed class RefusingDataService : InMemoryDataService, IDataService
    {
        public RefusingDataService()
        {
            // InMemoryDataService est synchrone sous le capot : ces Task sont déjà complétées.
            var e = GetEventAsync("evt-gala").Result!;
            e.ResponsableAccountId = SeedData.EmpActiveServer;
            UpdateEventAsync(e).Wait();

            CreateAssignmentAsync(new Assignment {
                Id = "asg-gala-host", EventId = "evt-gala", AccountId = SeedData.EmpActiveHost,
                JobRoleId = SeedData.RoleHost, Status = AssignmentStatus.Confirmed }).Wait();
        }

        Task IDataService.UpdateAssignmentAsync(Assignment a)
            => throw new InvalidOperationException("Missing or insufficient permissions.");
    }
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~EventTeamTests"`
Expected: FAIL — `.presence-count` introuvable.

- [ ] **Step 3: Ajouter le décompte et les boutons dans `EventTeam.razor`**

Remplacer le bloc :

```razor
    <div class="lab">Effectif</div>
    <div class="panel"><div class="m">@_team.Count personne(s) confirmée(s)</div></div>
```

par :

```razor
    <div class="lab">Effectif</div>
    <div class="panel">
        <div class="m presence-count">@Present présent@(Present > 1 ? "s" : "") · @Absent absent@(Absent > 1 ? "s" : "") · @Expected attendu@(Expected > 1 ? "s" : "")</div>
    </div>

    @if (_err is not null) { <p class="pill bad" style="display:block;margin:12px 0">@_err</p> }
```

Puis, dans le `<div class="panel team-member">`, après le bloc téléphone, ajouter :

```razor
                <div class="row2" style="margin-top:10px">
                    <button class="btn @(m.Asg.Presence == PresenceStatus.Present ? "primary" : "ghost") presence-present"
                            @onclick="() => SetPresence(m.Asg, PresenceStatus.Present)">Présent</button>
                    <button class="btn @(m.Asg.Presence == PresenceStatus.Absent ? "danger" : "ghost") presence-absent"
                            @onclick="() => SetPresence(m.Asg, PresenceStatus.Absent)">Absent</button>
                </div>
```

- [ ] **Step 4: Ajouter la logique dans le bloc `@code`**

Déclarer le champ d'erreur à côté de `_allowed` :

```csharp
    string? _err;
```

Ajouter les membres suivants après `RoleName` :

```csharp
    int Present  => _team.Count(m => m.Asg.Presence == PresenceStatus.Present);
    int Absent   => _team.Count(m => m.Asg.Presence == PresenceStatus.Absent);
    int Expected => _team.Count(m => m.Asg.Presence == PresenceStatus.Expected);

    async Task SetPresence(Assignment a, PresenceStatus status)
    {
        _err = null;
        var previous = a.Presence;
        try
        {
            a.Presence = status;
            await Data.UpdateAssignmentAsync(a);
            await Load();
        }
        catch (Exception ex)
        {
            a.Presence = previous;   // l'écran ne doit pas montrer un pointage qui n'a pas été écrit
            _err = $"Échec du pointage : {ex.Message}";
        }
    }
```

- [ ] **Step 5: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Pages/Shared/EventTeam.razor tests/AdminTaos.Tests/EventTeamTests.cs
git commit -m "feat(equipe): pointage des présences par le responsable, indépendant des timesheets"
```

---

## Task 8 : Points d'entrée vers la page d'équipe, puis déploiement

**Files:**
- Modify: `src/AdminTaos/Pages/Employee/EEventDetail.razor`
- Modify: `src/AdminTaos/Pages/Manager/MEventDetail.razor`
- Test: `tests/AdminTaos.Tests/EventTeamTests.cs`

**Interfaces:**
- Consumes: route `/e/events/{Id}/equipe` et `/m/events/{Id}/equipe` (tâche 6), `ServiceEvent.ResponsableAccountId` (tâche 4).
- Produces: rien pour les tâches suivantes.

- [ ] **Step 1: Écrire les tests qui échouent**

Ajouter à la classe `EventTeamTests` :

```csharp
    [Fact]
    public async Task The_responsable_gets_a_banner_and_a_link_on_the_event_page()
    {
        await Setup(SeedData.EmpActiveServer);
        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("Tu es responsable du jour", cut.Markup);
        Assert.Contains("e/events/evt-gala/equipe", cut.Markup);
    }

    [Fact]
    public async Task A_plain_collaborateur_gets_no_banner()
    {
        await Setup(SeedData.EmpActiveHost);
        var cut = Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.DoesNotContain("Tu es responsable du jour", cut.Markup);
        Assert.DoesNotContain("/equipe", cut.Markup);
    }

    [Fact]
    public async Task The_admin_event_page_links_to_the_team_page()
    {
        await Setup(SeedData.MgrId);
        var cut = Render<AdminTaos.Pages.Manager.MEventDetail>(p => p.Add(x => x.Id, "evt-gala"));

        Assert.Contains("m/events/evt-gala/equipe", cut.Markup);
    }
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test --filter "FullyQualifiedName~EventTeamTests"`
Expected: FAIL — la chaîne « Tu es responsable du jour » est absente.

- [ ] **Step 3: Ajouter le bandeau dans `EEventDetail.razor`**

Juste avant l'ouverture du `<div style="margin-top:18px">` qui contient les boutons d'action, insérer :

```razor
    @if (Auth.CurrentUser?.Id == _e.ResponsableAccountId)
    {
        <div class="panel" style="margin-top:18px">
            <div class="t">Tu es responsable du jour</div>
            <div class="m">Tu peux consulter ton équipe et pointer les présences.</div>
            <NavLink class="btn primary block" style="margin-top:12px"
                     href="@($"e/events/{Id}/equipe")">Gérer mon équipe</NavLink>
        </div>
    }
```

- [ ] **Step 4: Ajouter le lien dans `MEventDetail.razor`**

Dans le bloc d'actions qui contient déjà le `NavLink` « Assigner du personnel », ajouter juste après :

```razor
                    <NavLink class="btn ghost" href="@($"m/events/{_e.Id}/equipe")">Équipe du jour</NavLink>
```

- [ ] **Step 5: Lancer la suite entière**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add src/AdminTaos/Pages/Employee/EEventDetail.razor src/AdminTaos/Pages/Manager/MEventDetail.razor tests/AdminTaos.Tests/EventTeamTests.cs
git commit -m "feat(equipe): points d'entrée vers la page équipe côté collaborateur et côté admin"
```

- [ ] **Step 7: Publier et déployer**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
rm -rf publish
dotnet publish src/AdminTaos/AdminTaos.csproj -c Release -o publish
firebase deploy --only hosting
git push origin firebase-v1
```

Note : à ce stade le pointage échouera en production, les règles Firestore refusant encore
l'écriture. Le message « Échec du pointage » s'affichera — c'est le comportement attendu jusqu'à
la tâche 9, et c'est la raison pour laquelle cette remontée d'erreur a été écrite.

---

## Task 9 : Règles Firestore

**Files:**
- Modify: `firestore.rules`

**Interfaces:**
- Consumes: `ServiceEvent.ResponsableAccountId`, `Assignment.Presence` (tâche 4).
- Produces: rien.

- [ ] **Step 1: Remplacer le bloc `assignments` dans `firestore.rules`**

Remplacer intégralement :

```
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
```

par :

```
    // ---------- Assignments ----------
    match /assignments/{id} {
      function eventOf(eid) {
        return get(/databases/$(db)/documents/events/$(eid)).data;
      }
      // Le responsable du jour est désigné sur l'event, pas porté par le compte.
      function isResponsableOf(eid) {
        return isEmployee() && eventOf(eid).responsableAccountId == request.auth.uid;
      }

      // v1 simplification, identique à /events : tout compte actif lit, l'UI filtre.
      // Une règle conditionnelle serait plus étroite mais déclencherait un get() par document,
      // et Firestore plafonne à 20 get() par requête — un event d'une dizaine de personnes
      // ferait échouer la requête entière.
      allow read:   if isActive();

      allow create: if isManager()
                    || (isEmployee()
                        && request.resource.data.accountId == request.auth.uid
                        && request.resource.data.source    == "SelfRequest"
                        && request.resource.data.status    == "PendingApproval");

      // Le responsable ne peut toucher que le pointage, jamais le reste de l'assignation.
      // FirestoreDataService écrit via setDoc : diff() ne retient que les valeurs qui
      // changent réellement, donc hasOnly(['presence']) reste satisfait.
      allow update: if isManager()
                    || (isResponsableOf(resource.data.eventId)
                        && request.resource.data.diff(resource.data)
                               .affectedKeys().hasOnly(['presence']));

      allow delete: if isManager();
    }
```

- [ ] **Step 2: Vérifier la syntaxe des règles**

Run: `cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos" && firebase deploy --only firestore:rules --dry-run`
Expected: aucune erreur de compilation des règles. Si `--dry-run` n'est pas supporté par la
version installée du CLI, passer directement à l'étape 3 et lire attentivement la sortie.

- [ ] **Step 3: Déployer les règles**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
firebase deploy --only firestore:rules
```
Expected: `Deploy complete!`

- [ ] **Step 4: Vérifier en production, à la main**

Cette vérification n'est pas automatisable par la suite bUnit, qui tourne contre
`InMemoryDataService`. Dérouler ces quatre contrôles sur https://admintaos.web.app :

1. Connecté en Admin, désigner un collaborateur responsable d'un event à venir.
2. Se connecter avec ce collaborateur, ouvrir l'event, cliquer « Gérer mon équipe »,
   pointer quelqu'un présent. Attendu : le décompte change, aucun message d'erreur.
3. Se connecter avec un autre collaborateur assigné au même event et ouvrir
   `/e/events/{id}/equipe` directement dans la barre d'adresse. Attendu : « Accès réservé ».
4. Depuis la console Firestore, vérifier que le document d'assignation porte bien
   `presence: "Present"` et que rien d'autre n'a changé.

Si le contrôle 2 échoue avec « Missing or insufficient permissions », la cause la plus probable
est que `responsableAccountId` n'est pas encore écrit sur l'event : vérifier le document dans la
console avant de toucher aux règles.

- [ ] **Step 5: Commit**

```bash
cd "/Users/hervekurtis/Documents/VS Projects/AdminTaos"
git add firestore.rules
git commit -m "feat(rules): le responsable du jour peut écrire le seul champ presence"
git push origin firebase-v1
```

---

## Self-Review

**Couverture de la spec :**

| Section de la spec | Tâche |
|---|---|
| A.1 Modèle | 1 |
| A.2 Écrans | 2 |
| A.3 Validation | 1, 2 |
| A.4 Sécurité (aucun changement) | — vérifié, aucune règle touchée en tâches 1–3 |
| A.5 Tests | 1, 2, 3 |
| B.1 Modèle | 4 |
| B.2 Désignation | 5 |
| B.3 Page responsable | 6, 7 |
| B.4 Points d'entrée | 8 |
| B.5 Vue admin | 4, 8 |
| B.6 Règles Firestore | 9 |
| B.7 Tests | 4, 5, 6, 7, 8, 9 |

**Cohérence des types :** `PresenceStatus` est défini en tâche 4 et consommé sous ce nom exact en
tâches 6, 7 et 9. `ResponsableAccountId` est écrit en tâche 5, lu en tâches 4, 6 et 8, et référencé
sous sa forme JSON `responsableAccountId` en tâche 9 — la sérialisation `JsonSerializerDefaults.Web`
de `FirestoreDataService` produit bien ce camelCase. Les marqueurs CSS annoncés dans les blocs
**Produces** sont ceux utilisés par les tests des tâches suivantes.

**Écarts assumés :** les règles Firestore de la tâche 9 ne sont couvertes que par une vérification
manuelle. C'est la faiblesse connue de ce plan, déjà signalée dans la spec §B.7.
