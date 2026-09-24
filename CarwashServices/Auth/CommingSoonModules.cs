using System;
using System.Collections.Generic;

namespace CarwashServices.Auth
{
    /// <summary>
    /// Module keys that appear on a role's sidebar but aren't implemented yet.
    /// MainForm routes these to a "Coming Soon" dialog instead of trying to
    /// construct a view that doesn't exist.
    ///
    /// The list is role-aware: a key is only "coming soon" for the roles that
    /// don't yet have a working view. Admin and SuperAdmin ship with every
    /// operational view, so nothing is coming soon for them.
    ///
    /// Manager: Follow-Ups / Reminders is now implemented.
    /// Manager: Monitor Service Status is still a stub.
    /// </summary>
    internal static class ComingSoonModules
    {
        private static readonly Dictionary<UserRole, string[]> PerRole = new()
        {
            // Manager: all modules implemented.
            [UserRole.Manager] = Array.Empty<string>(),

            // Service Staff: both operational modules are still stubs.
            [UserRole.ServiceStaff] = new[]
        {
        "View Assigned Requests",
        "Update Service Status"
            }   
        };

        public static bool Contains(string key)
            => Contains(SessionUser.Role, key);

        public static bool Contains(UserRole role, string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!PerRole.TryGetValue(role, out var keys)) return false;
            foreach (var k in keys)
                if (k == key) return true;
            return false;
        }
    }
}