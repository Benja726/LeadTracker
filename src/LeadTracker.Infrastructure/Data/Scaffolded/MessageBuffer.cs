using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class MessageBuffer
{
    public long Id { get; set; }

    public Guid BusinessId { get; set; }

    public string Phone { get; set; } = null!;

    public string? Message { get; set; }

    public string? MessageSid { get; set; }

    public bool Processed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Business Business { get; set; } = null!;
}
