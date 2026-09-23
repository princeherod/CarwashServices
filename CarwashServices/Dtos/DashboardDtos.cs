using System.Collections.Generic;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Dashboard DTOs (used by DashboardView)
    // ============================================================

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
        public int CustomerId { get; set; }              // NEW — TenantCustomerId
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
        public int CustomerId { get; set; }              // NEW — TenantCustomerId
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
        public int CustomerId { get; set; }              // NEW — TenantCustomerId
        public string Status { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string Notes { get; set; } = "";
    }
}