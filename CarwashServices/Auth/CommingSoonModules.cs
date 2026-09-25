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
    /// don't yet have a working view.
    ///
    /// Service Staff:
    ///   - Follow-Ups / Reminders    → implemented
    ///   - View Assigned Requests    → implemented
    ///   - Update Service Status     → implemented
    /// </summary>
    internal static class ComingSoonModules
    {
        private static readonly Dictionary<UserRole, string[]> PerRole = new()
        {
            // Manager: all modules implemented.
            [UserRole.Manager] = Array.Empty<string>(),

            // Service Staff: all modules implemented.
            [UserRole.ServiceStaff] = Array.Empty<string>()
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