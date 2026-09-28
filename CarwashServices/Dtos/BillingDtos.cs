using System;

namespace CarwashServices.Dtos
{
    public class BillingSummaryDto
    {
        public decimal TotalPaid { get; set; }
        public string TotalPaidFormatted { get; set; } = "₱0.00";
        public decimal Outstanding { get; set; }
        public string OutstandingFormatted { get; set; } = "₱0.00";
        public int ActiveSubscriptions { get; set; }
    }

    public class SubscriptionPlanItemDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PriceFormatted { get; set; } = "₱0.00";
        public string BillingCycle { get; set; } = "Monthly";
        public string Description { get; set; } = string.Empty;
        public int ActiveSubscribers { get; set; }
    }

    public class CustomerSubscriptionItemDto
    {
        public int SubscriptionId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string TenantCompany { get; set; } = string.Empty;
        public string AdminUser { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = "Monthly";
        public decimal Price { get; set; }
        public string PriceFormatted { get; set; } = "₱0.00";
        public int ManagedBy { get; set; }
        public string ManagedByName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class BillingTransactionItemDto
    {
        public int TransactionId { get; set; }
        public int? SubscriptionId { get; set; }
        public string TenantCompany { get; set; } = string.Empty;
        public string TenantCompanyCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountFormatted { get; set; } = "₱0.00";
        public string PaymentStatus { get; set; } = "Paid";
        public DateTime TransactionDate { get; set; }
    }
}
