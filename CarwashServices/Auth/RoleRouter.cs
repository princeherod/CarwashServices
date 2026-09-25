using System;

namespace CarwashServices.Auth
{
    /// <summary>
    /// Central place that decides which sidebar modules a role can see.
    /// Role ids MUST match Auth/UserRole.cs:
    ///   1 = SuperAdmin, 2 = Admin, 3 = Manager, 4 = ServiceStaff
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
                "Manage Service Requests",
                "Assign Service Staff",
                "Follow-Ups / Reminders",
                "Monitor Service Status"
            },

            UserRole.ServiceStaff => new[]
{
    "View Dashboard",
    "Follow-Ups / Reminders",
    "View Assigned Requests",
    "Update Service Status"
},

            _ => Array.Empty<string>()
        };
    }
}