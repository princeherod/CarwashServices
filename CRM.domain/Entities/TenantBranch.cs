using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.domain.Entities;

[Table("Branches")]
public class TenantBranch
{
    [Key]
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public bool IsMainBranch { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Soft delete / archive
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
