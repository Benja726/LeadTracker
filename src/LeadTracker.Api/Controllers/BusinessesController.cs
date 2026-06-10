using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadTracker.Infrastructure.Data.Scaffolded;

namespace LeadTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BusinessesController(LeadTrackerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBusinesses(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var businesses = await db.BusinessMemberships
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.Business.Id,
                m.Business.Name,
                m.Business.Slug,
                m.Role
            })
            .ToListAsync(ct);

        return Ok(businesses);
    }

    [HttpGet("{businessId:guid}/leads")]
    public async Task<IActionResult> GetLeads(Guid businessId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var isMember = await db.BusinessMemberships
            .AnyAsync(m => m.UserId == userId && m.BusinessId == businessId, ct);

        if (!isMember) return Forbid();

        var leads = await db.Leads
            .Where(l => l.BusinessId == businessId)
            .OrderByDescending(l => l.UpdatedAt)
            .Select(l => new
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
                Messages = l.Conversation != null ? l.Conversation.History : "[]"
            })
            .ToListAsync(ct);

        return Ok(leads);
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
