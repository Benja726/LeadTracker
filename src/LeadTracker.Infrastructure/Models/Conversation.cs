using Newtonsoft.Json.Linq;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LeadTracker.Infrastructure.Models;

[Table("conversations")]
public sealed class Conversation : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    // jsonb. PostgREST returns it as real JSON (usually an array), so it must be a JSON
    // token, not a string. The controller serializes it back to a JSON string for the Web app.
    [Column("history")]
    public JToken? History { get; set; }
}
