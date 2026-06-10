using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class Conversation
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string Phone { get; set; } = null!;

    public string? ProfileName { get; set; }

    public string History { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? WhatsappNumberId { get; set; }

    public virtual Business Business { get; set; } = null!;

    public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();

    public virtual BusinessWhatsappNumber? WhatsappNumber { get; set; }
}
