using System;

namespace CarwashServices.Auth
{
    /// <summary>
    /// Central place that decides which sidebar modules a role can see.
    /// Add module keys here as you build out each role's screens.
    /// </summary>
    public static class RoleRouter
    {
        public static string[] ModulesFor(UserRole role) => role switch
        {
            UserRole.SuperAdmin => new[]
            {
                "View Dashboard",
                "Analytics",
                "View Reports",
                "Manage Users",
                "Manage Customers",
                "Manage Services",
                "Manage Service Requests",
                "Follow-Ups / Reminders",
                "Manage Admin Accounts"
            },
            UserRole.Admin => new[]
            {
                "View Dashboard",
                "Analytics",
                "View Reports",
                "Manage Users",
                "Manage Customers",
                "Manage Services",
                "Manage Service Requests",
                "Follow-Ups / Reminders"
            },
            UserRole.Manager => new[]
            {
                "View Dashboard",
                "Analytics",
                "View Reports",
                "Manage Customers",
                "Manage Services",
                "Manage Service Requests",
                "Follow-Ups / Reminders"
            },
            UserRole.ServiceStaff => new[]
            {
                "View Dashboard",
                "Manage Service Requests"
            },
            _ => Array.Empty<string>()
        };
    }
}