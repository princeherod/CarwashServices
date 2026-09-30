using System;

namespace CarwashServices.Dtos
{
    public class BranchDto
    {
        public int BranchId { get; set; }
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public int ServiceRequestsCount { get; set; }
        public int CustomersCount { get; set; }

        public int? AssignedAdminId { get; set; }
        public string? AssignedAdminName { get; set; }
        public string? AssignedAdminEmail { get; set; }

        public string DisplayLocation
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(City))
                    return $"{Address}, {City}";
                if (!string.IsNullOrWhiteSpace(City))
                    return City;
                if (!string.IsNullOrWhiteSpace(Address))
                    return Address;
                return "Location not set";
            }
        }
    }

    public class CreateBranchDto
    {
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class BranchCapabilityDto
    {
        public int CompanyId { get; set; }
        public bool MultiBranchEnabled { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int ActiveBranchesCount { get; set; }
    }

    public class EligibleBranchUserDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int? CurrentBranchId { get; set; }
        public string? CurrentBranchName { get; set; }
        public bool IsAssignedToThisBranch { get; set; }
    }

    public class AssignBranchAdminRequestDto
    {
        public int? UserId { get; set; }
    }

    public class CreateBranchAdminRequestDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RoleId { get; set; } = 2; // Default to Admin (2)
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
    }
}
