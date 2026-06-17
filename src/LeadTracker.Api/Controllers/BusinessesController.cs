using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadTracker.Infrastructure.Models;
using Newtonsoft.Json;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using Client = Supabase.Postgrest.Client;

namespace LeadTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BusinessesController(Client db) : ControllerBase
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
                m.Role
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
            Messages = l.Conversation?.History?.ToString(Formatting.None) ?? "[]"
        });

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
