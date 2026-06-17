using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace LeadTracker.Infrastructure.Models;

[Table("businesses")]
public sealed class Business : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("slug")]
    public string? Slug { get; set; }

    // Business hours the bot is allowed to answer in. `time` columns come back from PostgREST
    // as "HH:MM:SS" strings. Written only via the set_business_hours RPC, never directly.
    [Column("answer_start")]
    public string? AnswerStart { get; set; }

    [Column("answer_end")]
    public string? AnswerEnd { get; set; }

    [Column("answer_tz")]
    public string? AnswerTz { get; set; }
}
