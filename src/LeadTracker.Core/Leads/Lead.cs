namespace LeadTracker.Core.Leads;

/// <summary>
/// Mirrors the existing Supabase `leads` table — structured lead profile written by
/// the n8n agent. Unique by (business_id, phone).
/// NOTE: this is a hand sketch to anchor dashboard code. The authoritative entity
/// (exact types/nullability) should come from `dotnet ef dbcontext scaffold`.
/// </summary>
public sealed class Lead
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }          // tenant key — everything hangs off this
    public string Phone { get; set; } = "";
    public string? Name { get; set; }
    public string? Operation { get; set; }        // e.g. buy / rent
    public string? Zone { get; set; }
    public decimal? Budget { get; set; }
    public string? Classification { get; set; }
    public bool ReadyForHandoff { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
