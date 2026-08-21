using AdminTaos.Models;
using AdminTaos.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AdminTaos.Tests;

/// <summary>
/// Une page de l'espace collaborateur peut être rendue une fraction de seconde avant que le
/// garde de navigation n'ait redirigé — au chargement direct d'une URL, ou quand la session
/// expire. Aucune ne doit lever : l'utilisateur verrait « Une erreur s'est produite ».
/// </summary>
public class NoSessionRenderTests : BunitContext
{
    void NobodySignedIn()
    {
        var db = new InMemoryDataService();
        Services.AddSingleton<IDataService>(db);
        Services.AddSingleton(new AuthState(new FakeAuthClient(), db));
        Services.AddSingleton<IStorageClient>(new FakeStorageClient());
    }

    [Fact] public void EHome_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.EHome>(); }

    [Fact] public void EEvents_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.EEvents>(); }

    [Fact] public void EHours_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.EHours>(); }

    [Fact] public void EEventDetail_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.EEventDetail>(p => p.Add(x => x.Id, "evt-gala")); }

    [Fact] public void EActive_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.EActive>(p => p.Add(x => x.Id, "evt-today")); }

    [Fact] public void ERecap_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.ERecap>(p => p.Add(x => x.Id, "evt-gala")); }

    [Fact] public void EProfile_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Employee.EProfile>(); }

    [Fact] public void EventTeam_survives_a_missing_session()
    { NobodySignedIn(); Render<AdminTaos.Pages.Shared.EventTeam>(p => p.Add(x => x.Id, "evt-gala")); }
}
