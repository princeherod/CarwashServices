using System;
using System.Collections.Generic;
using System.Linq;

namespace CarwashServices.Auth
{
    public record ModuleSection(string Title, string[] Modules);

    /// <summary>
    /// Central place that decides which sidebar modules a role/tenant can see.
    /// Supports tenant-specific module arrangements for:
    ///   - AquaShine: Main Transactions & Data Collection
    ///   - SparkleRide: Business Intelligence & Actions
    ///   - CleanRide: Branching, Business Intelligence & Actions
    ///   - Super Admin: Master Platform or Restored Old Super Admin
    /// </summary>
    public static class RoleRouter
    {
        public static ModuleSection[] GetSectionsFor(UserRole role)
        {
            if (role == UserRole.SuperAdmin)
            {
                return new[]
                {
                    new ModuleSection("OVERVIEW", new[]
                    {
                        "Business Intelligence",
                        "Subscriptions"
                    }),
                    new ModuleSection("ADMINISTRATION", new[]
                    {
                        "Manage Admin Accounts",
                        "Manage Businesses",
                        "Backup",
                        "Terms & Conditions"
                    })
                };
            }

            if (role == UserRole.Admin)
            {
                var email = (SessionUser.Email ?? "").ToLowerInvariant();
                var code = (SessionUser.CompanyCode ?? "").ToUpperInvariant();
                var name = (SessionUser.CompanyName ?? "").ToLowerInvariant();

                // Tenant A: AquaShine -> Main Transactions & Data Collection
                if (code.Contains("AQUA") || code == "COMP001" || name.Contains("aquashine") || email == "admin@aquashine.com")
                {
                    return new[]
                    {
                        new ModuleSection("MAIN TRANSACTIONS", new[]
                        {
                            "View Dashboard",
                            "Manage Service Requests",
                            "Manage Services"
                        }),
                        new ModuleSection("DATA COLLECTION", new[]
                        {
                            "Manage Customers",
                            "Manage Users",
                            "Terms & Conditions"
                        })
                    };
                }

                // Tenant B: SparkleRide -> Business Intelligence & Actions
                if (code.Contains("SPARK") || code == "COMP002" || name.Contains("sparkleride") || email == "admin@sparkleride.com")
                {
                    return new[]
                    {
                        new ModuleSection("BUSINESS INTELLIGENCE", new[]
                        {
                            "View Dashboard",
                            "Analytics",
                            "View Reports"
                        }),
                        new ModuleSection("ACTIONS", new[]
                        {
                            "Manage Service Requests",
                            "Follow-Ups / Reminders",
                            "Manage Users",
                            "Terms & Conditions"
                        })
                    };
                }

                // Tenant C: CleanRide -> Branching, Business Intelligence & Actions
                if (code.Contains("CLEAN") || code == "COMP003" || name.Contains("cleanride") || email.Contains("cleanride"))
                {
                    var sections = new List<ModuleSection>();

                    if (!SessionUser.IsSingleBranchUser)
                    {
                        sections.Add(new ModuleSection("BRANCHING", new[]
                        {
                            "Branches"
                        }));
                    }

                    sections.Add(new ModuleSection("BUSINESS INTELLIGENCE", new[]
                    {
                        "View Dashboard",
                        "Analytics",
                        "View Reports"
                    }));

                    sections.Add(new ModuleSection("ACTIONS", new[]
                    {
                        "Manage Service Requests",
                        "Manage Customers",
                        "Manage Services",
                        "Follow-Ups / Reminders",
                        "Manage Users",
                        "Terms & Conditions"
                    }));

                    return sections.ToArray();
                }

                // Restored / Old Admin test account (admin@carwashcrm.com) & Default full Admin modules
                return new[]
                {
                    new ModuleSection("OVERVIEW", new[]
                    {
                        "View Dashboard",
                        "Analytics",
                        "View Reports"
                    }),
                    new ModuleSection("MANAGEMENT", new[]
                    {
                        "Manage Users",
                        "Manage Customers",
                        "Manage Services",
                        "Manage Service Requests",
                        "Follow-Ups / Reminders",
                        "Terms & Conditions"
                    })
                };
            }

            if (role == UserRole.Manager)
            {
                return new[]
                {
                    new ModuleSection("OVERVIEW", new[]
                    {
                        "View Dashboard",
                        "Analytics",
                        "View Reports"
                    }),
                    new ModuleSection("OPERATIONS", new[]
                    {
                        "Manage Service Requests",
                        "Assign Service Staff",
                        "Follow-Ups / Reminders",
                        "Monitor Service Status"
                    })
                };
            }

            if (role == UserRole.ServiceStaff)
            {
                return new[]
                {
                    new ModuleSection("MY TASKS", new[]
                    {
                        "View Dashboard",
                        "Follow-Ups / Reminders",
                        "View Assigned Requests",
                        "Update Service Status"
                    })
                };
            }

            return Array.Empty<ModuleSection>();
        }

        public static string[] ModulesFor(UserRole role)
        {
            var sections = GetSectionsFor(role);
            var list = new List<string>();
            foreach (var section in sections)
            {
                list.AddRange(section.Modules);
            }
            return list.ToArray();
        }
    }
}