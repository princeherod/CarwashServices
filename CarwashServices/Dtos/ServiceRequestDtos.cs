using System;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Service request DTOs
    //   ServiceRequestDto – returned by api/service-requests
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

        // Archive fields
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
    }
}