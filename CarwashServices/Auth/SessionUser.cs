namespace CarwashServices.Auth
{
    /// <summary>
    /// Holds the currently logged-in user for the lifetime of the app.
    /// Populated by LoginForm, read by Sidebar / MainForm / any role view.
    /// </summary>
    public static class SessionUser
    {
        public static int UserId { get; set; }
        public static string FullName { get; set; } = "";
        public static string Email { get; set; } = "";
        public static UserRole Role { get; set; } = UserRole.Unknown;

        public static bool IsLoggedIn => UserId > 0;

        public static void Clear()
        {
            UserId = 0;
            FullName = "";
            Email = "";
            Role = UserRole.Unknown;
        }
    }
}