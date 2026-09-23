namespace CRM.Domain.Entities;

public class TenantCustomer
{
    public int TenantCustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? ContactNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Vehicle info ----
    public string? PlateNumber { get; set; }
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public int? VehicleYear { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehicleType { get; set; }
    public string? Source { get; set; }

    // ---- Archive extension ----
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
}