using System;
using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class CustomerSubscription
    {
        public int SubscriptionId { get; set; }
        public int CustomerId { get; set; }
        public int PlanId { get; set; }
        public int ManagedBy { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } // Active, Expired, Cancelled

        public Customer Customer { get; set; }
        public SubscriptionPlan Plan { get; set; }
        public User ManagedByUser { get; set; }

        public ICollection<BillingTransaction> BillingTransactions { get; set; } = new List<BillingTransaction>();
    }
}