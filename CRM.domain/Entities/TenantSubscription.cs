using System;
using System.Collections.Generic;

namespace CRM.Domain.Entities;

public class TenantSubscription
{
    public int TenantSubscriptionId { get; set; }
    public int CompanyId { get; set; }
    public int? PlanId { get; set; }
    public string Status { get; set; } = "None"; // 'Active' | 'Inactive' | 'None'
    public DateTime? StartDate { get; set; }
    public DateTime? RenewalDate { get; set; }
    public bool AutoRenew { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public TenantSubscriptionPlan? Plan { get; set; }
    public ICollection<TenantBillingTransaction> Transactions { get; set; } = new List<TenantBillingTransaction>();
}
