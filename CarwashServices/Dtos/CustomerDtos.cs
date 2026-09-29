using System;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Tenant customer DTOs
    //   TenantCustomerDto   – returned by api/tenant/{id}/tenant-customers
    //   CustomerDto         – minimal view used by dialogs / combo boxes
    //   SegmentCustomerDto  – analytics: one row per customer + segment info
    //   SegmentCountsDto    – analytics: counts + percentages per segment
    // ============================================================

    public class TenantCustomerDto
    {
        public int TenantCustomerId { get; set; }
        public string CustomerCode { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? PlateNumber { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public int? VehicleYear { get; set; }
        public string? VehicleColor { get; set; }
        public string? VehicleType { get; set; }
        public string? Source { get; set; }

        // Archive fields
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
    }

    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Address { get; set; }
    }

    public class CustomerInteractionDto
    {
        public int InteractionId { get; set; }
        public int CustomerId { get; set; }
        public string Kind { get; set; } = "Feedback";        // "Complaint" | "Feedback"
        public string Severity { get; set; } = "Normal";      // "Low" | "Normal" | "High"
        public string Title { get; set; } = "";
        public string? Details { get; set; }
        public string Status { get; set; } = "Open";          // "Open" | "Resolved"
        public DateTime CreatedAt { get; set; }
        public string? RecordedBy { get; set; }
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

        // Follow-up info
        public bool HasOpenFollowUp { get; set; }
        public string? LastFollowUpStatus { get; set; }
        public string? LastFollowUpType { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public int? LastFollowUpId { get; set; }
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
}