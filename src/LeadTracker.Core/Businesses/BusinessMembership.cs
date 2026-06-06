namespace LeadTracker.Core.Businesses;

public enum MembershipRole { Owner, Admin, Member }

/// <summary>
/// Mirrors `business_memberships` — the many-to-many between dashboard users and
/// businesses. A single user can belong to several businesses, each with its own role.
/// This is what the API queries (by the JWT 'sub') to know which businesses a user
/// may access and what they can do.
/// </summary>
public sealed class BusinessMembership
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid UserId { get; set; }   // CONFIRM: maps to auth.users.id (the JWT 'sub')?
    public MembershipRole Role { get; set; }
}
