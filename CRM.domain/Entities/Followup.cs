namespace CRM.domain.Entities;

public class FollowUp
{
    public int FollowUpId { get; set; }
    public int CustomerId { get; set; }

    public string Type { get; set; } = "Service Reminder";
    public string ContactMethod { get; set; } = "SMS";
    public string? Reason { get; set; }
    public string? DiscountOffer { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime ScheduledDate { get; set; } = DateTime.Now;
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }

    // ---- Branch association ----
    public int? BranchId { get; set; }
    public TenantBranch? Branch { get; set; }

    // ---- Archive extension ----
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    // ---- Service Staff approval extension ----
    // "NotRequired" → created by Admin/Manager, no approval gate.
    // "Pending"     → created by Service Staff, awaiting Admin/Manager review.
    // "Approved"    → Admin/Manager approved; row may now be Scheduled / Sent.
    // "Rejected"    → Admin/Manager rejected; row must never be sent.
    public string ApprovalStatus { get; set; } = "NotRequired";
    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
}