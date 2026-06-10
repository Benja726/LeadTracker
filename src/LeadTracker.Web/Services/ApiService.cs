using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LeadTracker.Web.Models;

namespace LeadTracker.Web.Services;

public class ApiService(HttpClient http, AuthService auth)
{
    private List<BusinessDto>? _businesses;
    public BusinessDto? CurrentBusiness => _businesses?.FirstOrDefault();

    public async Task<List<BusinessDto>> GetBusinessesAsync()
    {
        if (_businesses is not null) return _businesses;
        try
        {
            await SetAuthHeaderAsync();
            _businesses = await http.GetFromJsonAsync<List<BusinessDto>>("api/businesses") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        return _businesses;
    }

    public async Task<List<Lead>> GetLeadsAsync()
    {
        var businesses = await GetBusinessesAsync();
        var biz = businesses.FirstOrDefault();
        if (biz is null) return [];

        await SetAuthHeaderAsync();
        try
        {
            var raw = await http.GetFromJsonAsync<List<LeadApiDto>>($"api/businesses/{biz.Id}/leads") ?? [];
            return raw.Select(Map).ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await auth.GetTokenAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static Lead Map(LeadApiDto d)
    {
        var temp = d.Temp?.ToLower() switch
        {
            "caliente" => Temperatura.Caliente,
            "tibio"    => Temperatura.Tibio,
            _          => Temperatura.Frio
        };
        var daysAgo = (int)(DateTime.UtcNow - d.CreatedAt).TotalDays;
        var time    = d.UpdatedAt.ToLocalTime().ToString("HH:mm");
        var intent  = !string.IsNullOrWhiteSpace(d.Operation)
                    ? $"{d.Operation} {d.Zone}".Trim()
                    : d.LastMessage ?? "";
        return new Lead(d.Id, d.Name, d.Phone, temp, intent, daysAgo, time, false, ParseHistory(d.Messages));
    }

    private static List<Message> ParseHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return [];
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];
            return arr.Select(el =>
            {
                string from;
                if (el.TryGetProperty("role", out var role))
                    from = role.GetString() == "user" ? "lead" : "bot";
                else if (el.TryGetProperty("type", out var type))
                    from = type.GetString() == "human" ? "lead" : "bot";
                else if (el.TryGetProperty("from", out var f))
                    from = f.GetString() == "user" ? "lead" : "bot";
                else
                    from = "bot";

                var text = el.TryGetProperty("content", out var c) ? c.GetString() ?? ""
                         : el.TryGetProperty("text", out var t)    ? t.GetString() ?? ""
                         : el.TryGetProperty("body", out var b)    ? b.GetString() ?? ""
                         : "";

                var time = el.TryGetProperty("timestamp", out var ts) ? ParseTimestamp(ts)
                         : el.TryGetProperty("time", out var ti)      ? ti.GetString() ?? ""
                         : "";

                return new Message(from, text, time);
            }).Where(m => !string.IsNullOrEmpty(m.Text)).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string ParseTimestamp(JsonElement ts)
    {
        if (ts.ValueKind == JsonValueKind.Number && ts.TryGetInt64(out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString("HH:mm");
        if (ts.ValueKind == JsonValueKind.String && DateTime.TryParse(ts.GetString(), out var dt))
            return dt.ToLocalTime().ToString("HH:mm");
        return "";
    }
}

public record BusinessDto(Guid Id, string Name, string Slug, string Role);

public class LeadApiDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Temp { get; set; }
    public string? Operation { get; set; }
    public string? Zone { get; set; }
    public string? Budget { get; set; }
    public bool ReadyForHandoff { get; set; }
    public bool HandoffDone { get; set; }
    public string? LastMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Messages { get; set; }
}
