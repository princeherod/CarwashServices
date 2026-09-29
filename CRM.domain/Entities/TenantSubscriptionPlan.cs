using System;
using System.Collections.Generic;

namespace CRM.Domain.Entities;

public class TenantSubscriptionPlan
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string BillingCycle { get; set; } = "Monthly";
    public int MaxUsers { get; set; }
    public int MaxCustomers { get; set; }
    public bool MultiBranchEnabled { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
}
