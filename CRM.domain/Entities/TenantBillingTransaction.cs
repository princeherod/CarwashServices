using System;

namespace CRM.Domain.Entities;

public class TenantBillingTransaction
{
    public int TransactionId { get; set; }
    public int? TenantSubscriptionId { get; set; }
    public int CompanyId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? ReferenceNumber { get; set; }

    public TenantSubscription? TenantSubscription { get; set; }
    public Company? Company { get; set; }
}
