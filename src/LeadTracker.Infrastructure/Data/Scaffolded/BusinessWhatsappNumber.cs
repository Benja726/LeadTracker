using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class BusinessWhatsappNumber
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string WhatsappNumber { get; set; } = null!;

    public string? Label { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Business Business { get; set; } = null!;

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
}
