using System;
using System.Collections.Generic;
using System.Text;
namespace CRM.Domain.Entities;

public class Product
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public int DurationMinutes { get; set; }
    public string? Category { get; set; }   // Exterior, Interior, Full Service, Specialty
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}