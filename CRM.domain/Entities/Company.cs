namespace CRM.Domain.Entities;

public class Company
{
    public int CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool TermsAccepted { get; set; } = false;
    public DateTime? TermsAcceptedAt { get; set; }
    public string? TermsAcceptedBy { get; set; }
    public string? TermsAcceptedVersion { get; set; }

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<CompanyDatabase> Databases { get; set; } = new List<CompanyDatabase>();
    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
}