using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class Business
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Slug { get; set; }

    public string? WhatsappNumber { get; set; }

    public string? CrmAccount { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BusinessMembership> BusinessMemberships { get; set; } = new List<BusinessMembership>();

    public virtual ICollection<BusinessWhatsappNumber> BusinessWhatsappNumbers { get; set; } = new List<BusinessWhatsappNumber>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();

    public virtual ICollection<MessageBuffer> MessageBuffers { get; set; } = new List<MessageBuffer>();

    public virtual ICollection<Property> Properties { get; set; } = new List<Property>();
}
