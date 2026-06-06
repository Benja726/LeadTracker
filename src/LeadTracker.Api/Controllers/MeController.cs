using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MeController : ControllerBase
{
    /// <summary>
    /// Smoke-test endpoint: returns the identity extracted from the Supabase JWT.
    /// Call it with: Authorization: Bearer &lt;access_token from supabase.auth.signInWithPassword&gt;
    /// </summary>
    [HttpGet]
    [Authorize]
    public IActionResult Get()
    {
        // Supabase standard claims:
        var userId = User.FindFirstValue("sub");   // the auth.users.id
        var email = User.FindFirstValue("email");
        var role = User.FindFirstValue("role");    // usually "authenticated"

        // NOTE on tenancy: this system is multi-tenant by business_id, and a user can
        // belong to MANY businesses (via business_memberships). So there is no single
        // tenant claim in the token. The set of businesses a user may access is resolved
        // server-side by querying business_memberships with this userId (sub).
        return Ok(new
        {
            userId,
            email,
            role,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}
