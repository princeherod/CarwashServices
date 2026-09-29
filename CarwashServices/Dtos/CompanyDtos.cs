using System;

namespace CarwashServices.Dtos
{
    public class CompanyListItemDto
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public int AdminCount { get; set; }
        public int? AdminUserId { get; set; }
        public string AdminUser { get; set; } = "Unassigned";
        public string AdminEmail { get; set; } = "";
        public string? DatabaseServer { get; set; }
        public string? DatabaseName { get; set; }
    }

    public class InitialAdminDto
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class CreateCompanyRequestDto
    {
        public string CompanyCode { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? DatabaseServer { get; set; }
        public string? DatabaseName { get; set; }
        public InitialAdminDto? InitialAdmin { get; set; }
    }

    public class UpdateCompanyRequestDto
    {
        public string CompanyName { get; set; } = "";
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public bool? IsActive { get; set; }
        public int? AdminUserId { get; set; }
        public bool? UnassignAdmin { get; set; }
    }

    public class AvailableAdminDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public int RoleId { get; set; }
        public int? CompanyId { get; set; }
        public bool IsCurrentCompany { get; set; }
    }

    public class NextCompanyCodeDto
    {
        public string NextCode { get; set; } = "";
    }
}
