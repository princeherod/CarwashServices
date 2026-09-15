using System;

namespace CRM.domain.Entities
{
    public class BillingTransaction
    {
        public int TransactionId { get; set; }
        public int? CustomerSubscriptionId { get; set; }
        public int? RequestId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } // Paid, Pending, Failed
        public DateTime TransactionDate { get; set; }

        public CustomerSubscription CustomerSubscription { get; set; }
        public ServiceRequest ServiceRequest { get; set; }
    }
}