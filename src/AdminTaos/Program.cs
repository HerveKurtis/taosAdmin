using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AdminTaos;
using AdminTaos.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

if (builder.HostEnvironment.IsDevelopment())
{
    // --- Mode démonstration : jeu de données en mémoire, aucune connexion à Firebase. ---
    // Sert à faire tourner l'app en local et à produire les captures de la page d'aide sans
    // exposer de données réelles. `dotnet publish` compile en environnement Production :
    // cette branche ne part jamais en ligne.
    builder.Services.AddSingleton<IDataService, InMemoryDataService>();
    builder.Services.AddSingleton<IAuthClient, DemoAuthClient>();
    builder.Services.AddSingleton<IStorageClient, DemoStorageClient>();
}
else
{
    // --- Firebase / Firestore (prod) ---
    builder.Services.AddSingleton<IAuthClient, FirebaseAuthClient>();
    builder.Services.AddSingleton<IStorageClient, FirebaseStorageClient>();
    builder.Services.AddSingleton<IDataService, FirestoreDataService>();
}
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<NavigationGuard>();

await builder.Build().RunAsync();
