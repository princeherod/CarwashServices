using System;
using System.Collections.Generic;

namespace CarwashServices.Dtos
{
    public class PlatformAnalyticsDto
    {
        public PlatformOverviewDto PlatformOverview { get; set; } = new();
        public TenantAnalyticsDto TenantAnalytics { get; set; } = new();
        public SubscriptionAnalyticsDto SubscriptionAnalytics { get; set; } = new();
        public BillingAnalyticsDto BillingAnalytics { get; set; } = new();
        public List<RecentSubscriptionActivityDto> RecentActivity { get; set; } = new();
    }

    public class PlatformOverviewDto
    {
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int InactiveTenants { get; set; }
        public int TotalActiveSubscriptions { get; set; }
        public decimal TotalRevenue { get; set; }
        public string TotalRevenueFormatted { get; set; } = "₱0.00";
        public int TotalRegisteredUsers { get; set; }
        public int TotalBranches { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string OutstandingAmountFormatted { get; set; } = "₱0.00";
    }

    public class TenantAnalyticsDto
    {
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int InactiveTenants { get; set; }
        public int NewlyRegisteredTenants { get; set; }
        public List<GrowthPointDto> TenantGrowth { get; set; } = new();
    }

    public class SubscriptionAnalyticsDto
    {
        public int TotalActiveSubscriptions { get; set; }
        public int ExpiringSubscriptions { get; set; }
        public int ExpiredSubscriptions { get; set; }
        public List<SubscriptionPlanCountDto> SubscriptionsByPlan { get; set; } = new();
        public List<StatusCountDto> StatusDistribution { get; set; } = new();
        public List<GrowthPointDto> SubscriptionGrowth { get; set; } = new();
    }

    public class SubscriptionPlanCountDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string PriceFormatted { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = "Monthly";
        public int Count { get; set; }
    }

    public class StatusCountDto
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class GrowthPointDto
    {
        public string Period { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class BillingAnalyticsDto
    {
        public decimal TotalSubscriptionRevenue { get; set; }
        public string TotalSubscriptionRevenueFormatted { get; set; } = "₱0.00";
        public int PaidBillingTransactions { get; set; }
        public decimal OutstandingBillingAmount { get; set; }
        public string OutstandingBillingAmountFormatted { get; set; } = "₱0.00";
        public decimal OverdueBillingAmount { get; set; }
        public string OverdueBillingAmountFormatted { get; set; } = "₱0.00";
        public List<RevenueMonthDto> RevenueByMonth { get; set; } = new();
        public List<RevenueByPlanDto> RevenueByPlan { get; set; } = new();
    }

    public class RevenueMonthDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Formatted { get; set; } = "₱0.00";
        public int Count { get; set; }
    }

    public class RevenueByPlanDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Formatted { get; set; } = "₱0.00";
        public double Percentage { get; set; }
        public int TransactionCount { get; set; }
    }

    public class RecentSubscriptionActivityDto
    {
        public int TransactionId { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string AmountFormatted { get; set; } = "₱0.00";
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string DateFormatted { get; set; } = string.Empty;
    }
}
