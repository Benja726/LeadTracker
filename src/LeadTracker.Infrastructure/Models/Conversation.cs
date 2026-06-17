using Newtonsoft.Json.Linq;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LeadTracker.Infrastructure.Models;

[Table("conversations")]
public sealed class Conversation : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    // jsonb. PostgREST returns it as real JSON (usually an array), so it must be a JSON
    // token, not a string. The controller serializes it back to a JSON string for the Web app.
    [Column("history")]
    public JToken? History { get; set; }

    // Bot handoff state. Written only via the set_bot_enabled RPC (tenancy-guarded), never
    // updated directly. The dashboard reads these to render the toggle + status line.
    [Column("bot_enabled")]
    public bool BotEnabled { get; set; } = true;

    [Column("bot_disabled_reason")]
    public string? BotDisabledReason { get; set; }

    [Column("bot_disabled_at")]
    public DateTime? BotDisabledAt { get; set; }
}
