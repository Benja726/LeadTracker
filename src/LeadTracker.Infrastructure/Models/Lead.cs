using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LeadTracker.Infrastructure.Models;

[Table("leads")]
public sealed class Lead : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("conversation_id")]
    public Guid? ConversationId { get; set; }

    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    [Column("profile_name")]
    public string? ProfileName { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("operation")]
    public string? Operation { get; set; }

    [Column("zone")]
    public string? Zone { get; set; }

    [Column("budget")]
    public string? Budget { get; set; }

    [Column("classification")]
    public string? Classification { get; set; }

    [Column("ready_for_handoff")]
    public bool ReadyForHandoff { get; set; }

    [Column("handoff_done")]
    public bool HandoffDone { get; set; }

    [Column("last_message")]
    public string? LastMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Embedded via PostgREST resource embedding (leads -> conversations FK).
    [Reference(typeof(Conversation))]
    public Conversation? Conversation { get; set; }
}
