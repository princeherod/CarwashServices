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
                _roleId = (int)value;
            }
        }

        /// <summary>
        /// Numeric role ID:
        /// 1 = Admin, 2 = Manager, 3 = Staff/ServiceStaff, 4 = Super Admin
        /// </summary>
        public static int RoleId
        {
            get => _roleId != 0 ? _roleId : (int)_role;
            set
            {
                _roleId = value;
                _role = value switch
                {
                    1 => UserRole.Admin,
                    2 => UserRole.Manager,
                    3 => UserRole.ServiceStaff,
                    4 => UserRole.SuperAdmin,
                    _ => (UserRole)value
                };
            }
        }

        public static int? CompanyId { get; set; }
        public static string CompanyName { get; set; } = "";
        public static string CompanyCode { get; set; } = "";
        public static bool TermsAccepted { get; set; } = true;
        public static string? TermsAcceptedVersion { get; set; }

        /// <summary>
        /// Resolved company ID for tenant API operations.
        /// Defaults to 1 if no specific tenant is assigned.
        /// </summary>
        public static int CurrentCompanyId => CompanyId.GetValueOrDefault(1) > 0 ? CompanyId.Value : 1;

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
        }
    }
}