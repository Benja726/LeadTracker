namespace LeadTracker.Core.Businesses;

/// <summary>
/// Mirrors `businesses` — each real-estate client. The tenant root: every other
/// table references this via business_id.
/// </summary>
public sealed class Business
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? CrmAccount { get; set; }
}
