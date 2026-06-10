using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BusinessMembership> BusinessMemberships { get; set; } = new List<BusinessMembership>();
}
