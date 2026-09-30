namespace CarwashServices.Auth
{
    /// <summary>
    /// Holds the currently logged-in user for the lifetime of the app.
    /// Populated by LoginForm, read by Sidebar / MainForm / any role view.
    /// </summary>
    public static class SessionUser
    {
        private static int _roleId;
        private static UserRole _role = UserRole.Unknown;

        public static int UserId { get; set; }
        public static string FullName { get; set; } = "";
        public static string Email { get; set; } = "";

        public static UserRole Role
        {
            get => _role;
            set
            {
                _role = value;
                _roleId = value switch
                {
                    UserRole.SuperAdmin => 1,
                    UserRole.Admin => 2,
                    UserRole.Manager => 3,
                    UserRole.ServiceStaff => 4,
                    _ => (int)value
                };
            }
        }

        /// <summary>
        /// Numeric role ID:
        /// 1 = SuperAdmin, 2 = Admin, 3 = Manager, 4 = ServiceStaff
        /// </summary>
        public static int RoleId
        {
            get => _roleId != 0 ? _roleId : (int)_role;
            set
            {
                _roleId = value;
                _role = ResolveRole(value, Email, CompanyId);
            }
        }

        public static UserRole ResolveRole(int roleId, string? email = null, int? companyId = null)
        {
            var em = (email ?? Email ?? "").ToLowerInvariant();
            var cid = companyId ?? CompanyId;
            bool isSuperAdminAccount = em.Contains("superadmin") || cid == null || cid <= 0;

            if (isSuperAdminAccount)
                return UserRole.SuperAdmin;

            return roleId switch
            {
                1 => UserRole.Admin,         // Existing tenant admin accounts
                2 => UserRole.Admin,         // Branch Admin & Admin (RoleId 2 = Admin)
                3 => UserRole.Manager,       // Manager
                4 => UserRole.ServiceStaff,  // Service Staff
                _ => UserRole.Admin
            };
        }

        public static int? CompanyId { get; set; }
        public static string CompanyName { get; set; } = "";
        public static string CompanyCode { get; set; } = "";
        public static bool TermsAccepted { get; set; } = true;
        public static string? TermsAcceptedVersion { get; set; }

        public static bool MultiBranchEnabled { get; set; } = false;
        public static int? AssignedBranchId { get; set; } = null;
        public static string? AssignedBranchName { get; set; } = null;
        public static bool IsSingleBranchUser => AssignedBranchId.HasValue && AssignedBranchId.Value > 0;

        public static int? CurrentBranchId { get; set; } = null;
        public static string CurrentBranchName { get; set; } = "All Branches";
        public static event System.Action? BranchChanged;

        public static void SetBranch(int? branchId, string? branchName)
        {
            if (IsSingleBranchUser)
            {
                CurrentBranchId = AssignedBranchId;
                CurrentBranchName = AssignedBranchName ?? "Branch";
            }
            else
            {
                CurrentBranchId = branchId;
                CurrentBranchName = string.IsNullOrWhiteSpace(branchName) ? "All Branches" : branchName;
            }
            BranchChanged?.Invoke();
        }

        /// <summary>
        /// Resolved company ID for tenant API operations.
        /// Defaults to 1 if no specific tenant is assigned.
        /// </summary>
        public static int CurrentCompanyId => (CompanyId.HasValue && CompanyId.Value > 0) ? CompanyId.Value : 1;

        public static bool IsLoggedIn => UserId > 0;

        public static void Clear()
        {
            UserId = 0;
            FullName = "";
            Email = "";
            _role = UserRole.Unknown;
            _roleId = 0;
            CompanyId = null;
            CompanyName = "";
            CompanyCode = "";
            TermsAccepted = true;
            TermsAcceptedVersion = null;
            MultiBranchEnabled = false;
            AssignedBranchId = null;
            AssignedBranchName = null;
            CurrentBranchId = null;
            CurrentBranchName = "All Branches";
        }
    }
}