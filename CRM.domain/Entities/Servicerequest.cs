using System;
using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class ServiceRequest
    {
        public int RequestId { get; set; }
        public int CustomerId { get; set; }
        public int ServiceId { get; set; }
        public int? AssignedStaffId { get; set; }
        public int CreatedBy { get; set; }
        public string Status { get; set; } // Pending, Assigned, InProgress, Completed, Cancelled
        public DateTime RequestedDate { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public Customer Customer { get; set; }
        public Service Service { get; set; }
        public User AssignedStaff { get; set; }
        public User CreatedByUser { get; set; }

        public ICollection<ServiceStatusLog> StatusLogs { get; set; } = new List<ServiceStatusLog>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public ICollection<BillingTransaction> BillingTransactions { get; set; } = new List<BillingTransaction>();
    }
}