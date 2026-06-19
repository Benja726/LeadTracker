using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            // Float handoff-ready leads to the top; OrderBy is stable so recency order is kept within each bucket.
            return raw.Select(Map).OrderByDescending(l => l.ReadyForHandoff).ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>
    /// Toggle the bot for a conversation via the tenancy-guarded set_bot_enabled RPC (server-side).
    /// Returns the authoritative state on success, or null on failure (so the caller can revert).
    /// </summary>
    public async Task<BotStateDto?> SetBotEnabledAsync(string phone, bool enabled, string reason = "manual")
    {
        var businesses = await GetBusinessesAsync();
        var biz = businesses.FirstOrDefault();
        if (biz is null) return null;

        await SetAuthHeaderAsync();
        try
        {
            var resp = await http.PostAsJsonAsync($"api/businesses/{biz.Id}/bot",
                new { phone, enabled, reason });
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<BotStateDto>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Save the business answering hours via the role-gated set_business_hours RPC (server-side).
    /// Returns the authoritative state on success and updates the cached business, or null on failure.
    /// </summary>
    public async Task<BusinessHoursDto?> SetBusinessHoursAsync(string start, string end, string tz)
    {
        var businesses = await GetBusinessesAsync();
        var biz = businesses.FirstOrDefault();
        if (biz is null) return null;

        await SetAuthHeaderAsync();
        try
        {
            var resp = await http.PostAsJsonAsync($"api/businesses/{biz.Id}/hours",
                new { start, end, tz });
            if (!resp.IsSuccessStatusCode) return null;
            var dto = await resp.Content.ReadFromJsonAsync<BusinessHoursDto>();
            if (dto is not null)
            {
                biz.AnswerStart = dto.AnswerStart;
                biz.AnswerEnd = dto.AnswerEnd;
                biz.AnswerTz = dto.AnswerTz;
            }
            return dto;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whole stats page in one server call: the dashboard_stats RPC (funnel + temperatura + zona
    /// + fuera-horario). Pass from=null for all-time. Returns null on failure.
    /// </summary>
    public async Task<DashboardStats?> GetDashboardStatsAsync(DateTimeOffset? from, DateTimeOffset? to = null)
    {
        var businesses = await GetBusinessesAsync();
        var biz = businesses.FirstOrDefault();
        if (biz is null) return null;

        await SetAuthHeaderAsync();
        try
        {
            var qs = new List<string>();
            if (from.HasValue) qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
            if (to.HasValue)   qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
            var url = $"api/businesses/{biz.Id}/stats" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
            return await http.GetFromJsonAsync<DashboardStats>(url);
        }
        catch (HttpRequestException)
        {
            return null;
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
        return new Lead(d.Id, d.Name, d.Phone, temp, intent, daysAgo, time, false, ParseHistory(d.Messages))
        {
            BotEnabled = d.BotEnabled,
            BotDisabledReason = d.BotDisabledReason,
            BotDisabledAt = d.BotDisabledAt,
            ReadyForHandoff = d.ReadyForHandoff,
        };
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

public record BusinessDto(Guid Id, string Name, string Slug, string Role)
{
    // Populated from the API by name; mutable so a successful hours save updates the cache in place.
    public string? AnswerStart { get; set; }
    public string? AnswerEnd { get; set; }
    public string? AnswerTz { get; set; }

    public bool CanManage => Role is "owner" or "admin";
}

public record BusinessHoursDto(string? AnswerStart, string? AnswerEnd, string? AnswerTz);

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
    public bool BotEnabled { get; set; } = true;
    public string? BotDisabledReason { get; set; }
    public DateTime? BotDisabledAt { get; set; }
}

public record BotStateDto(string Phone, bool BotEnabled, string? BotDisabledReason, DateTime? BotDisabledAt);

// Payload of the dashboard_stats RPC (snake_case jsonb keys → mapped here).
public class DashboardStats
{
    public int Total { get; set; }
    public int Calificados { get; set; }
    public int Prontos { get; set; }
    public int Caliente { get; set; }
    public int Tibio { get; set; }
    public int Frio { get; set; }
    [JsonPropertyName("fuera_horario")] public int FueraHorario { get; set; }
    [JsonPropertyName("por_zona")] public List<ZonaStat> PorZona { get; set; } = [];
    [JsonPropertyName("por_operacion")] public List<OperacionStat> PorOperacion { get; set; } = [];
}

public class ZonaStat
{
    public string Zona { get; set; } = "";
    public int N { get; set; }
}

public class OperacionStat
{
    public string Operacion { get; set; } = "";
    public int N { get; set; }
}
