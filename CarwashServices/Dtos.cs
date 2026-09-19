using System;

namespace CarwashServices.Views
{
    // ============================================================
    // Tenant DTOs — used by CustomersView + CustomerEditDialog
    // ============================================================

    public class TenantCustomerDto
    {
        public int TenantCustomerId { get; set; }
        public string CustomerCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Vehicle extension fields
        public string? PlateNumber { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public int? VehicleYear { get; set; }
        public string? VehicleColor { get; set; }
        public string? VehicleType { get; set; }
        public string? Source { get; set; }
    }

    // ============================================================
    // Product / Service DTOs
    // ============================================================

    public class ProductDto
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public int DurationMinutes { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ============================================================
    // Service Request DTOs
    // ============================================================

    public class ServiceRequestDto
    {
        public int RequestId { get; set; }
        public int CustomerId { get; set; }
        public int ServiceId { get; set; }
        public int? AssignedStaffId { get; set; }
        public int CreatedBy { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Priority { get; set; } = "Normal";
        public DateTime RequestedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? Notes { get; set; }
    }

    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class ServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = "";
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public int RoleId { get; set; }
    }

    // ============================================================
    // Follow-Up DTOs
    // ============================================================

    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int CustomerId { get; set; }
        public string Type { get; set; } = "Service Reminder";
        public string ContactMethod { get; set; } = "SMS";
        public string? Reason { get; set; }
        public string? DiscountOffer { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime ScheduledDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class FollowUpStatsDto
    {
        public int DueToday { get; set; }
        public int OffersSent { get; set; }
        public int Redeemed { get; set; }
        public int Expired { get; set; }
    }
    public class AnalyticsSummaryDto
    {
        public int TotalCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public double ChurnRate { get; set; }
        public decimal AvgSpendPerCustomer { get; set; }
        public int CarsWashedThisMonth { get; set; }
        public decimal RevenueThisMonth { get; set; }
    }

    public class RetentionPointDto
    {
        public string Month { get; set; } = "";
        public int Value { get; set; }
    }

    public class SegmentCountsDto
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public double ActivePct { get; set; }
        public int AtRisk { get; set; }
        public double AtRiskPct { get; set; }
        public int Lost { get; set; }
        public double LostPct { get; set; }
    }

    public class SegmentCustomerDto
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Vehicle { get; set; }
        public DateTime LastVisit { get; set; }
        public int DaysSince { get; set; }
        public string Segment { get; set; } = "Active";
        public int DaysLeft { get; set; }
    }

    public class RevenuePointDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class RevenueResponseDto
    {
        public List<RevenuePointDto> Months { get; set; } = new();
        public decimal Ytd { get; set; }
        public string BestMonth { get; set; } = "";
        public decimal BestValue { get; set; }
    }
    public class ReportsResponseDto
    {
        public int TotalTransactions { get; set; }
        public int Completed { get; set; }
        public int PendingOrCancelled { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AvgTicket { get; set; }
        public List<ReportMonthPointDto> Months { get; set; } = new();
        public List<ReportServicePointDto> ByService { get; set; } = new();
        public List<ReportTxnDto> Transactions { get; set; } = new();
        public List<string> ServiceOptions { get; set; } = new();
        public List<string> VehicleOptions { get; set; } = new();
        public string GeneratedAt { get; set; } = "";
    }

    public class ReportMonthPointDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class ReportServicePointDto
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    public class ReportTxnDto
    {
        public string Txn { get; set; } = "";
        public string Date { get; set; } = "";
        public string Customer { get; set; } = "";
        public string Vehicle { get; set; } = "";
        public string Service { get; set; } = "";
        public decimal Amount { get; set; }
        public string Payment { get; set; } = "";
        public string Status { get; set; } = "";
    }
    public class DashboardResponseDto
    {
        public int TotalCustomers { get; set; }
        public int TodayJobs { get; set; }
        public int InProgress { get; set; }
        public int Pending { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public List<DashboardRequestDto> RecentRequests { get; set; } = new();
        public List<DashboardFollowUpDto> FollowUpQueue { get; set; } = new();
        public int FollowUpPendingCount { get; set; }
        public List<DashboardStaffDto> ServiceStaff { get; set; } = new();
        public List<DashboardLogDto> RecentLogs { get; set; } = new();
    }

    public class DashboardRequestDto
    {
        public int RequestId { get; set; }
        public string Customer { get; set; } = "";
        public string Plate { get; set; } = "";
        public string Service { get; set; } = "";
        public string ScheduledDate { get; set; } = "";
        public string AssignedStaff { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class DashboardFollowUpDto
    {
        public int FollowUpId { get; set; }
        public string Customer { get; set; } = "";
        public string Type { get; set; } = "";
        public string ScheduledDate { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class DashboardStaffDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsOnDuty { get; set; }
    }

    public class DashboardLogDto
    {
        public int LogId { get; set; }
        public int RequestId { get; set; }
        public string Status { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    // ============================================================
    // Combo helper
    // ============================================================

    public class ComboItem
    {
        public int? Id { get; }
        public string Text { get; }

        public ComboItem(int? id, string text)
        {
            Id = id;
            Text = text;
        }

        public override string ToString() => Text;
    }
}