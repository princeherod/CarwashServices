using System;
using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // Product / Service DTOs
    //   ProductDto  – returned by api/tenant/{id}/products
    //   ServiceDto  – minimal view used by service-request dialogs
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

        // Archive fields
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
    }

    public class ServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = "";
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
    }
}