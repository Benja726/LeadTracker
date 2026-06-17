using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LeadTracker.Web;
using LeadTracker.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? builder.HostEnvironment.BaseAddress)
});

// ---- GoTrue client: auto-refreshing session, persisted to localStorage ----
builder.Services.AddSingleton<LocalStorageSessionPersistence>();
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration;
    var url = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url not configured");
    var anonKey = config["Supabase:AnonKey"] ?? throw new InvalidOperationException("Supabase:AnonKey not configured");

    var client = new Supabase.Gotrue.Client(new Supabase.Gotrue.ClientOptions
    {
        Url = $"{url.TrimEnd('/')}/auth/v1",
        Headers = new Dictionary<string, string>
        {
            ["apikey"] = anonKey,
            ["Authorization"] = $"Bearer {anonKey}",
        },
        AutoRefreshToken = true,
    });
    client.SetPersistence(sp.GetRequiredService<LocalStorageSessionPersistence>());
    return client;
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiService>();

var host = builder.Build();

// Restore a saved session from localStorage and refresh it on startup so the dashboard
// loads with a live token (and stays live via the SDK's background refresh).
var gotrue = host.Services.GetRequiredService<Supabase.Gotrue.Client>();
gotrue.LoadSession();
await gotrue.RetrieveSessionAsync();

await host.RunAsync();
