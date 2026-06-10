using System;
using System.Collections.Generic;

namespace LeadTracker.Infrastructure.Data.Scaffolded;

public partial class Property
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string Ref { get; set; } = null!;

    public string? Type { get; set; }

    public string? Operation { get; set; }

    public string? Zone { get; set; }

    public int? Bedrooms { get; set; }

    public int? Bathrooms { get; set; }

    public int? M2 { get; set; }

    public decimal? Price { get; set; }

    public string? Currency { get; set; }

    public string? PriceLabel { get; set; }

    public decimal? ExpensesAmount { get; set; }

    public string? ExpensesLabel { get; set; }

    public string? Status { get; set; }

    public string? Title { get; set; }

    public string? Extra { get; set; }

    public string? Url { get; set; }

    public string? PhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal? PriceFortnight { get; set; }

    public decimal? PriceWeek { get; set; }

    public decimal? PriceDay { get; set; }

    public bool? IsSeasonal { get; set; }

    public virtual Business Business { get; set; } = null!;
}
