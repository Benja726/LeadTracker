using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace LeadTracker.Web.Services;

public class AuthService(HttpClient http, IJSRuntime js, IConfiguration config)
{
    private const string StorageKey = "sb_token";
    private string? _token;

    public async Task<bool> SignInAsync(string email, string password)
    {
        var supabaseUrl = config["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url not configured");
        var anonKey = config["Supabase:AnonKey"] ?? throw new InvalidOperationException("Supabase:AnonKey not configured");

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/auth/v1/token?grant_type=password");
        req.Headers.Add("apikey", anonKey);
        req.Content = JsonContent.Create(new { email, password });

        using var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return false;

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        _token = doc!.RootElement.GetProperty("access_token").GetString();
        if (_token is null) return false;

        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, _token);
        return true;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (_token is null)
            _token = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        return _token;
    }

    public async Task<bool> IsAuthenticatedAsync()
        => !string.IsNullOrEmpty(await GetTokenAsync());

    public async Task SignOutAsync()
    {
        _token = null;
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
