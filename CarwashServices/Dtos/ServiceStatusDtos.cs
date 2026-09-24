using System.Collections.Generic;

namespace CarwashServices.Dtos
{
    /// <summary>
    /// One row in the "Service Status" panel. Mirrors the ServiceRequest
    /// shape that Manage Service Requests uses, enriched with the latest
    /// log entry so we can show Last Updated / Updated By.
    /// </summary>
    public class ServiceStatusRowDto
    {
        public int RequestId { get; set; }
        public int CustomerId { get; set; }
        public string Customer { get; set; } = "";
        public string Plate { get; set; } = "";

        public int ServiceId { get; set; }
        public string Service { get; set; } = "";
        public decimal ServicePrice { get; set; }

        public int? AssignedStaffId { get; set; }
        public string AssignedStaff { get; set; } = "";

        public string Status { get; set; } = "";
        public string Priority { get; set; } = "Normal";

        public string RequestedDate { get; set; } = "";
        public string ScheduledDate { get; set; } = "";
        public string CompletedDate { get; set; } = "";

        public string LastUpdated { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
    }

    public class ServiceStatusHistoryRowDto
    {
        public int LogId { get; set; }
        public int RequestId { get; set; }
        public string Customer { get; set; } = "";
        public string Service { get; set; } = "";
        public string Status { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public class ServiceStatusResponseDto
    {
        public List<ServiceStatusRowDto> Current { get; set; } = new();
        public List<ServiceStatusHistoryRowDto> History { get; set; } = new();
        public int Pending { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public string GeneratedAt { get; set; } = "";
    }
}