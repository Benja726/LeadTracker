using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LeadTracker.Infrastructure.Models;

[Table("business_memberships")]
public sealed class BusinessMembership : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    // Embedded via PostgREST resource embedding (business_memberships -> businesses FK).
    [Reference(typeof(Business))]
    public Business? Business { get; set; }
}
