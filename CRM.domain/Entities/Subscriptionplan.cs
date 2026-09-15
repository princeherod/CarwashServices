using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class SubscriptionPlan
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; }
        public decimal Price { get; set; }
        public string BillingCycle { get; set; } // Monthly, Quarterly, Yearly
        public string Description { get; set; }

        public ICollection<CustomerSubscription> CustomerSubscriptions { get; set; } = new List<CustomerSubscription>();
    }
}