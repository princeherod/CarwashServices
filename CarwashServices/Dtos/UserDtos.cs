using CarwashServices.Dtos;
namespace CarwashServices.Dtos
{
    // ============================================================
    // User DTOs
    //   UserDto          – compact list row (used by combo boxes)
    //   AuthUserDto      – response from POST api/auth/login
    //   UserListItemDto  – one row from GET api/users (Manage Users screen)
    //   UserDetailDto    – one row for the edit dialog (adds Password slot)
    // ============================================================

    public class UserDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public int RoleId { get; set; }
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
    }

    public class AuthUserDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public int RoleId { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyCode { get; set; }
        public bool TermsAccepted { get; set; } = true;
        public string? TermsAcceptedVersion { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public bool MultiBranchEnabled { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
    }

    public class UserListItemDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public int RoleId { get; set; }
        public string Email { get; set; } = "";
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public int? CompanyId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
    }

    public class UserDetailDto : UserListItemDto
    {
        // Never sent back by the server — held only by the edit dialog.
        public string? Password { get; set; }
    }
}