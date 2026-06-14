using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AdminTaos;
using AdminTaos.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// --- Firebase / Firestore (prod) ---
builder.Services.AddSingleton<IAuthClient, FirebaseAuthClient>();
builder.Services.AddSingleton<IStorageClient, FirebaseStorageClient>();
builder.Services.AddSingleton<IDataService, FirestoreDataService>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<NavigationGuard>();

await builder.Build().RunAsync();
