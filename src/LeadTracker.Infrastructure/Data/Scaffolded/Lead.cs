using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class Lead
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Guid? ConversationId { get; set; }

    public string Phone { get; set; } = null!;

    public string? ProfileName { get; set; }

    public string? Name { get; set; }

    public string? Operation { get; set; }

    public string? Zone { get; set; }

    public string? Type { get; set; }

    public int? Bedrooms { get; set; }

    public string? Budget { get; set; }

    public decimal? BudgetAmount { get; set; }

    public string? Timeline { get; set; }

    public string? Financing { get; set; }

    public string? PropertyOfInterest { get; set; }

    public string Classification { get; set; } = null!;

    public string? Reason { get; set; }

    public bool ReadyForHandoff { get; set; }

    public bool HandoffDone { get; set; }

    public string? LastMessage { get; set; }

    public string? LastReply { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? WhatsappNumberId { get; set; }

    public virtual Business Business { get; set; } = null!;

    public virtual Conversation? Conversation { get; set; }

    public virtual BusinessWhatsappNumber? WhatsappNumber { get; set; }
}
