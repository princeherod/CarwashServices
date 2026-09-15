using System;

namespace CRM.domain.Entities
{
    public class FollowUp
    {
        public int FollowUpId { get; set; }
        public int CustomerId { get; set; }
        public int? RequestId { get; set; }
        public string Type { get; set; } // Reminder, FollowUpCall, Renewal
        public DateTime ScheduledDate { get; set; }
        public string Status { get; set; } // Pending, Done, Cancelled
        public int CreatedBy { get; set; }
        public string Notes { get; set; }

        public Customer Customer { get; set; }
        public ServiceRequest ServiceRequest { get; set; }
        public User CreatedByUser { get; set; }
    }
}