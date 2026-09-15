using System;
using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public ICollection<CustomerSubscription> Subscriptions { get; set; } = new List<CustomerSubscription>();
    }
}