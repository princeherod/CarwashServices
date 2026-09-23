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

    // ---- Archive extension ----
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
}