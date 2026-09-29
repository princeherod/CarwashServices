using System;
using System.Collections.Generic;

namespace CRM.domain.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; }
        public string Email { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
        public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public ICollection<CustomerSubscription> Subscriptions { get; set; } = new List<CustomerSubscription>();
    }
}