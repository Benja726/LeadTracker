using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadTracker.Api.Auth;
using LeadTracker.Infrastructure.Models;
using Newtonsoft.Json;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using Client = Supabase.Postgrest.Client;

namespace LeadTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BusinessesController(Client db, SupabaseOptions supabase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBusinesses(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var memberships = await db.Table<BusinessMembership>()
            .Where(m => m.UserId == userId.Value)
            .Get(ct);

        var businesses = memberships.Models
            .Where(m => m.Business is not null)
            .Select(m => new
            {
                m.Business!.Id,
                m.Business.Name,
                m.Business.Slug,
                m.Role,
                // "HH:MM:SS" → "HH:MM" for <input type="time">.
                AnswerStart = ShortTime(m.Business.AnswerStart),
                AnswerEnd = ShortTime(m.Business.AnswerEnd),
                m.Business.AnswerTz,
            });

        return Ok(businesses);
    }

    [HttpGet("{businessId:guid}/leads")]
    public async Task<IActionResult> GetLeads(Guid businessId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Tenancy guard: caller must belong to the requested business.
        var membership = await db.Table<BusinessMembership>()
            .Where(m => m.UserId == userId.Value && m.BusinessId == businessId)
            .Get(ct);

        if (membership.Models.Count == 0) return Forbid();

        var leads = await db.Table<Lead>()
            .Where(l => l.BusinessId == businessId)
            .Order(l => l.UpdatedAt, Ordering.Descending)
            .Get(ct);

        var result = leads.Models.Select(l => new
        {
            l.Id,
            Name = l.Name ?? l.ProfileName ?? l.Phone,
            l.Phone,
            Temp = l.Classification,
            l.Operation,
            l.Zone,
            l.Budget,
            l.ReadyForHandoff,
            l.HandoffDone,
            l.LastMessage,
            l.CreatedAt,
            l.UpdatedAt,
            // Bot handoff state lives on the conversation (joined via conversation_id).
            // Default to "on" when there's no conversation row yet.
            BotEnabled = l.Conversation?.BotEnabled ?? true,
            BotDisabledReason = l.Conversation?.BotDisabledReason,
            BotDisabledAt = l.Conversation?.BotDisabledAt,
            Messages = l.Conversation?.History?.ToString(Formatting.None) ?? "[]"
        });

        return Ok(result);
    }

    // Toggle the bot on/off for one conversation. The write goes through the tenancy-guarded
    // set_bot_enabled RPC — never a direct UPDATE. The RPC checks business_memberships against
    // auth.uid(), so it must run with the *caller's* JWT (the service-role key has no auth.uid()).
    [HttpPost("{businessId:guid}/bot")]
    public async Task<IActionResult> SetBotEnabled(Guid businessId, [FromBody] BotToggleRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Phone)) return BadRequest("phone is required");

        // Tenancy guard (clean 403 before we even hit the RPC, same posture as GetLeads).
        var membership = await db.Table<BusinessMembership>()
            .Where(m => m.UserId == userId.Value && m.BusinessId == businessId)
            .Get(ct);

        if (membership.Models.Count == 0) return Forbid();

        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "manual" : req.Reason;

        // Per-request PostgREST client carrying the caller's JWT so auth.uid() resolves
        // inside the RPC. apikey stays the service-role key (gateway auth); PostgREST uses
        // the Authorization bearer to set the DB role + auth.uid().
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        var userClient = new Client(supabase.RestUrl, new ClientOptions());
        userClient.GetHeaders = () => new Dictionary<string, string>
        {
            ["apikey"] = supabase.ServiceRoleKey!,
            ["Authorization"] = $"Bearer {token}",
        };

        await userClient.Rpc("set_bot_enabled", new Dictionary<string, object?>
        {
            ["p_business_id"] = businessId,
            ["p_phone"] = req.Phone,
            ["p_enabled"] = req.Enabled,
            ["p_reason"] = reason,
        });

        // Revalidate: read the row back (service-role) and return the authoritative state.
        var convs = await db.Table<Conversation>()
            .Where(c => c.BusinessId == businessId && c.Phone == req.Phone)
            .Get(ct);
        var conv = convs.Models.FirstOrDefault();

        return Ok(new
        {
            Phone = req.Phone,
            BotEnabled = conv?.BotEnabled ?? req.Enabled,
            BotDisabledReason = conv?.BotDisabledReason,
            BotDisabledAt = conv?.BotDisabledAt,
        });
    }

    // Set the business answering hours. Admin-level setting: gated to owner/admin here, and
    // written via the tenancy-guarded set_business_hours RPC (caller's JWT), never a direct UPDATE.
    [HttpPost("{businessId:guid}/hours")]
    public async Task<IActionResult> SetBusinessHours(Guid businessId, [FromBody] BusinessHoursRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var membership = await db.Table<BusinessMembership>()
            .Where(m => m.UserId == userId.Value && m.BusinessId == businessId)
            .Get(ct);

        var role = membership.Models.FirstOrDefault()?.Role;
        if (role is null) return Forbid();
        if (role is not ("owner" or "admin")) return Forbid();

        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        var userClient = new Client(supabase.RestUrl, new ClientOptions());
        userClient.GetHeaders = () => new Dictionary<string, string>
        {
            ["apikey"] = supabase.ServiceRoleKey!,
            ["Authorization"] = $"Bearer {token}",
        };

        await userClient.Rpc("set_business_hours", new Dictionary<string, object?>
        {
            ["p_business_id"] = businessId,
            ["p_start"] = req.Start,
            ["p_end"] = req.End,
            ["p_tz"] = req.Tz,
        });

        var biz = (await db.Table<Business>()
            .Where(b => b.Id == businessId)
            .Get(ct)).Models.FirstOrDefault();

        return Ok(new
        {
            AnswerStart = ShortTime(biz?.AnswerStart),
            AnswerEnd = ShortTime(biz?.AnswerEnd),
            AnswerTz = biz?.AnswerTz,
        });
    }

    // Results-funnel stats for the dashboard. Everything comes from the single dashboard_stats
    // RPC (jsonb), which scopes to business_id and checks membership against auth.uid() — so it
    // runs with the *caller's* JWT, same posture as the bot/hours RPCs. We pass the jsonb through
    // untouched. p_from/p_to are optional (null = all-time).
    [HttpGet("{businessId:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid businessId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Tenancy guard: clean 403 before hitting the RPC (same posture as GetLeads).
        var membership = await db.Table<BusinessMembership>()
            .Where(m => m.UserId == userId.Value && m.BusinessId == businessId)
            .Get(ct);

        if (membership.Models.Count == 0) return Forbid();

        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        var userClient = new Client(supabase.RestUrl, new ClientOptions());
        userClient.GetHeaders = () => new Dictionary<string, string>
        {
            ["apikey"] = supabase.ServiceRoleKey!,
            ["Authorization"] = $"Bearer {token}",
        };

        var resp = await userClient.Rpc("dashboard_stats", new Dictionary<string, object?>
        {
            ["p_business_id"] = businessId,
            ["p_from"] = from,
            ["p_to"] = to,
        });

        return Content(resp.Content ?? "{}", "application/json");
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    // PostgREST returns `time` as "HH:MM:SS"; the dashboard's <input type="time"> wants "HH:MM".
    private static string? ShortTime(string? t) =>
        string.IsNullOrEmpty(t) ? t : t.Length >= 5 ? t[..5] : t;

    public sealed record BotToggleRequest(string Phone, bool Enabled, string? Reason);

    public sealed record BusinessHoursRequest(string Start, string End, string Tz);
}
