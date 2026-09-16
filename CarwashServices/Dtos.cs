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
    // Product / Service DTOs — used by ServicesView + ServiceEditDialog
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
    // Service Request DTOs — used by ServiceRequestsView
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