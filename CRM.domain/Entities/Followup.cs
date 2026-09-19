using System;

namespace CRM.domain.Entities
{
    public class FollowUp
    {
        public int FollowUpId { get; set; }

        // FK → TenantCustomers (tenant DB). No FK constraint.
        public int CustomerId { get; set; }

        // "Service Reminder" | "Post-Service Feedback" | "Promotional Offer" | "Renewal"
        public string Type { get; set; } = "Service Reminder";

        // "SMS" | "Email" | "Call" | "Facebook Messenger"
        public string ContactMethod { get; set; } = "SMS";

        public string? Reason { get; set; }
        public string? DiscountOffer { get; set; }
        public string? Notes { get; set; }

        // "Pending" | "Scheduled" | "Sent" | "Contacted" | "Redeemed" | "Expired"
        public string Status { get; set; } = "Pending";

        public DateTime ScheduledDate { get; set; } = DateTime.Now;
        public DateTime? ValidUntil { get; set; }
        public DateTime? SentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
    }
}