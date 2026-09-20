using System;

namespace CRM.Domain.Entities
{
    public class CustomerInteraction
    {
        public int InteractionId { get; set; }
        public int CustomerId { get; set; }
        public string Kind { get; set; } = "Feedback";        // Complaint | Feedback
        public string Severity { get; set; } = "Normal";      // Low | Normal | High
        public string Title { get; set; } = "";
        public string? Details { get; set; }
        public string Status { get; set; } = "Open";          // Open | Resolved
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? RecordedBy { get; set; }
    }
}