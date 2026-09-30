using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using CarwashServices.Auth;

namespace CarwashServices.Common
{
    public enum TenantUiMode
    {
        AquaShine,        // Tenant A: Main Transactions & Data Collection (Deep Ocean / Marine Blue)
        SparkleRide,      // Tenant B: Business Intelligence & Actions (Sapphire / Cobalt Blue)
        CleanRide,        // Tenant C: Multi-Branch Enterprise (Steel / Slate Ice Blue)
        MasterSuperAdmin, // Super Admin Master Platform (Executive Midnight Blue)
        LegacyCrm         // Restored Old Super Admin & Admin (Classic Navy Blue)
    }

    public class TenantUiTheme
    {
        public TenantUiMode Mode { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public string BrandSubtitle { get; set; } = string.Empty;
        public string BrandBadge { get; set; } = string.Empty;
        public string BrandCode { get; set; } = string.Empty;
        public string TenantSummary { get; set; } = string.Empty;

        // All colors strictly blue-based
        public Color SidebarBg { get; set; }
        public Color HeaderBg { get; set; }
        public Color ActiveItemBg { get; set; }
        public Color HoverItemBg { get; set; }
        public Color AccentBlue { get; set; }
        public Color BorderBlue { get; set; }
        public Color PillBg { get; set; }
        public Color PillBorder { get; set; }
        public Color PillDot { get; set; }
        public Color PillText { get; set; }
        public Color GroupHeaderColor { get; set; }
        public Color UserCardBg { get; set; }
        public Color PrimaryButtonBg { get; set; }
        public Color PrimaryButtonHover { get; set; }
        public Color BadgeBg { get; set; }
        public Color BadgeText { get; set; }
        public Color TagBorder { get; set; }
        public Color SectionDivider { get; set; }

        public Action<Graphics, RectangleF> DrawBrandIcon { get; set; } = (_, _) => { };
    }

    public static class TenantThemeManager
    {
        public static TenantUiTheme Current => GetThemeForSession();

        public static TenantUiTheme GetThemeForSession()
        {
            var email = (SessionUser.Email ?? "").ToLowerInvariant();
            var code = (SessionUser.CompanyCode ?? "").ToUpperInvariant();
            var name = (SessionUser.CompanyName ?? "").ToLowerInvariant();
            var role = SessionUser.Role;

            if (role == UserRole.SuperAdmin)
            {
                return CreateMasterSuperAdminTheme();
            }

            if (role == UserRole.Admin)
            {
                if (code.Contains("AQUA") || code == "COMP001" || name.Contains("aquashine") || email == "admin@aquashine.com")
                {
                    return CreateAquaShineTheme();
                }

                if (code.Contains("SPARK") || code == "COMP002" || name.Contains("sparkleride") || email == "admin@sparkleride.com")
                {
                    return CreateSparkleRideTheme();
                }

                if (code.Contains("CLEAN") || code == "COMP003" || name.Contains("cleanride") || email == "admin@cleanride.com")
                {
                    return CreateCleanRideTheme();
                }

                return CreateLegacyCrmTheme("CARWASH CRM", "Restored Admin", "ADMIN", "Standard Management");
            }

            // Role == Manager or ServiceStaff or other tenant user
            if (code.Contains("CLEAN") || name.Contains("cleanride"))
                return CreateCleanRideTheme();

            if (code.Contains("SPARK") || name.Contains("sparkleride"))
                return CreateSparkleRideTheme();

            if (code.Contains("AQUA") || name.Contains("aquashine"))
                return CreateAquaShineTheme();

            return CreateLegacyCrmTheme(
                !string.IsNullOrWhiteSpace(SessionUser.CompanyCode) ? SessionUser.CompanyCode.ToUpperInvariant() : "CARWASH",
                !string.IsNullOrWhiteSpace(SessionUser.CompanyName) ? SessionUser.CompanyName : "CRM System",
                role.ToString().ToUpperInvariant(),
                "Operational Portal"
            );
        }

        /// <summary>
        /// Tenant A: AquaShine - Oceanic / Marine Navy Blue CRM
        /// Focus: Main Transactions & Data Collection
        /// </summary>
        private static TenantUiTheme CreateAquaShineTheme()
        {
            return new TenantUiTheme
            {
                Mode = TenantUiMode.AquaShine,
                BrandName = "AquaShine Car Wash",
                BrandSubtitle = "Main Transactions & Data Collection",
                BrandBadge = "TENANT A",
                BrandCode = "AQUASHINE",
                TenantSummary = "Single Company • Core Operations",
                SidebarBg = Color.FromArgb(0x06, 0x16, 0x2E),
                HeaderBg = Color.FromArgb(0x09, 0x1E, 0x3D),
                ActiveItemBg = Color.FromArgb(0x11, 0x35, 0x63),
                HoverItemBg = Color.FromArgb(0x0C, 0x24, 0x46),
                AccentBlue = Color.FromArgb(0x38, 0xB6, 0xFF), // Marine Cyan-Blue
                BorderBlue = Color.FromArgb(0x18, 0x42, 0x75),
                PillBg = Color.FromArgb(0x0F, 0x2C, 0x54),
                PillBorder = Color.FromArgb(0x1F, 0x55, 0x99),
                PillDot = Color.FromArgb(0x38, 0xB6, 0xFF),
                PillText = Color.FromArgb(0xE0, 0xF2, 0xFE),
                GroupHeaderColor = Color.FromArgb(0x60, 0xA5, 0xFA),
                UserCardBg = Color.FromArgb(0x09, 0x1D, 0x39),
                PrimaryButtonBg = Color.FromArgb(0x02, 0x84, 0xC7),
                PrimaryButtonHover = Color.FromArgb(0x03, 0x69, 0xA1),
                BadgeBg = Color.FromArgb(0x0E, 0x38, 0x68),
                BadgeText = Color.FromArgb(0xBA, 0xE6, 0xFD),
                TagBorder = Color.FromArgb(0x1D, 0x4E, 0x89),
                SectionDivider = Color.FromArgb(0x10, 0x2E, 0x56),
                DrawBrandIcon = (g, bounds) =>
                {
                    using var pen = new Pen(Color.FromArgb(0x38, 0xB6, 0xFF), 2.2f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    // Ocean Wave surge icon
                    g.DrawArc(pen, bounds.X + 4, bounds.Y + 12, bounds.Width - 8, 16, 180, 180);
                    g.DrawArc(pen, bounds.X + 8, bounds.Y + 6, bounds.Width - 16, 12, 190, 170);
                    g.DrawLine(pen, bounds.X + 6, bounds.Y + 20, bounds.Right - 6, bounds.Y + 20);
                }
            };
        }

        /// <summary>
        /// Tenant B: SparkleRide - Sapphire / Royal Cobalt Blue CRM
        /// Focus: Business Intelligence & Actions
        /// </summary>
        private static TenantUiTheme CreateSparkleRideTheme()
        {
            return new TenantUiTheme
            {
                Mode = TenantUiMode.SparkleRide,
                BrandName = "SparkleRide Auto Spa",
                BrandSubtitle = "Business Intelligence & Actions",
                BrandBadge = "TENANT B",
                BrandCode = "SPARKLERIDE",
                TenantSummary = "Single Company • BI Analytics",
                SidebarBg = Color.FromArgb(0x0A, 0x15, 0x36),
                HeaderBg = Color.FromArgb(0x0E, 0x1E, 0x4A),
                ActiveItemBg = Color.FromArgb(0x1C, 0x30, 0x6E),
                HoverItemBg = Color.FromArgb(0x14, 0x24, 0x52),
                AccentBlue = Color.FromArgb(0x5A, 0x8E, 0xFA), // Brilliant Sapphire
                BorderBlue = Color.FromArgb(0x23, 0x3C, 0x82),
                PillBg = Color.FromArgb(0x14, 0x27, 0x5C),
                PillBorder = Color.FromArgb(0x28, 0x48, 0x9E),
                PillDot = Color.FromArgb(0x5A, 0x8E, 0xFA),
                PillText = Color.FromArgb(0xE8, 0xEE, 0xFF),
                GroupHeaderColor = Color.FromArgb(0x81, 0x8C, 0xF8),
                UserCardBg = Color.FromArgb(0x0E, 0x1B, 0x44),
                PrimaryButtonBg = Color.FromArgb(0x25, 0x63, 0xEB),
                PrimaryButtonHover = Color.FromArgb(0x1D, 0x4E, 0xD8),
                BadgeBg = Color.FromArgb(0x18, 0x2F, 0x6D),
                BadgeText = Color.FromArgb(0xC7, 0xD7, 0xFE),
                TagBorder = Color.FromArgb(0x2B, 0x45, 0x96),
                SectionDivider = Color.FromArgb(0x18, 0x29, 0x5A),
                DrawBrandIcon = (g, bounds) =>
                {
                    using var pen = new Pen(Color.FromArgb(0x5A, 0x8E, 0xFA), 2f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    float cx = bounds.X + bounds.Width / 2f;
                    float cy = bounds.Y + bounds.Height / 2f;
                    // Radiant 4-point sparkle diamond
                    PointF[] diamond =
                    {
                        new(cx, bounds.Y + 4),
                        new(bounds.Right - 6, cy),
                        new(cx, bounds.Bottom - 4),
                        new(bounds.X + 6, cy)
                    };
                    g.DrawPolygon(pen, diamond);
                    g.DrawLine(pen, cx, bounds.Y + 8, cx, bounds.Bottom - 8);
                    g.DrawLine(pen, bounds.X + 10, cy, bounds.Right - 10, cy);
                }
            };
        }

        /// <summary>
        /// Tenant C: CleanRide - Steel / Slate Ice Blue Enterprise CRM
        /// Focus: Multi-Branching, Business Intelligence & Actions
        /// </summary>
        private static TenantUiTheme CreateCleanRideTheme()
        {
            return new TenantUiTheme
            {
                Mode = TenantUiMode.CleanRide,
                BrandName = "CleanRide Car Wash",
                BrandSubtitle = "Multi-Branch Enterprise Suite",
                BrandBadge = "TENANT C • MULTI-BRANCH",
                BrandCode = "CLEANRIDE",
                TenantSummary = "Enterprise Multi-Branch Suite",
                SidebarBg = Color.FromArgb(0x0A, 0x19, 0x2F),
                HeaderBg = Color.FromArgb(0x0D, 0x22, 0x3E),
                ActiveItemBg = Color.FromArgb(0x18, 0x3B, 0x66),
                HoverItemBg = Color.FromArgb(0x12, 0x2A, 0x4B),
                AccentBlue = Color.FromArgb(0x60, 0xA5, 0xFA), // Ice Steel Blue
                BorderBlue = Color.FromArgb(0x1E, 0x48, 0x7E),
                PillBg = Color.FromArgb(0x13, 0x2F, 0x54),
                PillBorder = Color.FromArgb(0x2B, 0x5D, 0x9E),
                PillDot = Color.FromArgb(0x60, 0xA5, 0xFA),
                PillText = Color.FromArgb(0xDB, 0xEA, 0xFE),
                GroupHeaderColor = Color.FromArgb(0x93, 0xC5, 0xFD),
                UserCardBg = Color.FromArgb(0x0E, 0x23, 0x40),
                PrimaryButtonBg = Color.FromArgb(0x1D, 0x4E, 0xD8),
                PrimaryButtonHover = Color.FromArgb(0x1E, 0x40, 0xAF),
                BadgeBg = Color.FromArgb(0x17, 0x39, 0x66),
                BadgeText = Color.FromArgb(0xBF, 0xDB, 0xFE),
                TagBorder = Color.FromArgb(0x27, 0x56, 0x94),
                SectionDivider = Color.FromArgb(0x16, 0x31, 0x52),
                DrawBrandIcon = (g, bounds) =>
                {
                    using var pen = new Pen(Color.FromArgb(0x60, 0xA5, 0xFA), 2f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    using var brush = new SolidBrush(Color.FromArgb(0x60, 0xA5, 0xFA));

                    // Multi-branch network node diagram: main hub + two branch branches
                    float cx = bounds.X + bounds.Width / 2f;
                    float cy = bounds.Y + bounds.Height / 2f;

                    // Main hub node
                    g.FillEllipse(brush, bounds.X + 4, cy - 4, 8, 8);
                    // Right upper branch
                    g.FillEllipse(brush, bounds.Right - 12, bounds.Y + 6, 8, 8);
                    // Right lower branch
                    g.FillEllipse(brush, bounds.Right - 12, bounds.Bottom - 14, 8, 8);

                    // Connecting network branches
                    g.DrawLine(pen, bounds.X + 12, cy, cx, cy);
                    g.DrawLine(pen, cx, cy, bounds.Right - 12, bounds.Y + 10);
                    g.DrawLine(pen, cx, cy, bounds.Right - 12, bounds.Bottom - 10);
                }
            };
        }

        /// <summary>
        /// Master Super Admin Platform - Executive Midnight Navy Blue CRM
        /// Focus: Admin Panel, BI, Subscriptions (NO PURPLE)
        /// </summary>
        private static TenantUiTheme CreateMasterSuperAdminTheme()
        {
            return new TenantUiTheme
            {
                Mode = TenantUiMode.MasterSuperAdmin,
                BrandName = "MASTER CRM",
                BrandSubtitle = "Super Admin Platform",
                BrandBadge = "MASTER PLATFORM",
                BrandCode = "MASTER",
                TenantSummary = "Cross-Tenant Platform Administration",
                SidebarBg = Color.FromArgb(0x07, 0x12, 0x26),
                HeaderBg = Color.FromArgb(0x0B, 0x19, 0x33),
                ActiveItemBg = Color.FromArgb(0x13, 0x2C, 0x54),
                HoverItemBg = Color.FromArgb(0x0E, 0x21, 0x40),
                AccentBlue = Color.FromArgb(0x3B, 0x82, 0xF6), // Royal Azure
                BorderBlue = Color.FromArgb(0x1E, 0x3A, 0x6B),
                PillBg = Color.FromArgb(0x10, 0x25, 0x4A),
                PillBorder = Color.FromArgb(0x25, 0x4E, 0x8C),
                PillDot = Color.FromArgb(0x60, 0xA5, 0xFA),
                PillText = Color.FromArgb(0xDB, 0xEA, 0xFE),
                GroupHeaderColor = Color.FromArgb(0x60, 0xA5, 0xFA),
                UserCardBg = Color.FromArgb(0x0A, 0x17, 0x2E),
                PrimaryButtonBg = Color.FromArgb(0x1E, 0x40, 0xAF),
                PrimaryButtonHover = Color.FromArgb(0x1D, 0x35, 0x8F),
                BadgeBg = Color.FromArgb(0x15, 0x30, 0x5E),
                BadgeText = Color.FromArgb(0xBF, 0xDB, 0xFE),
                TagBorder = Color.FromArgb(0x22, 0x47, 0x85),
                SectionDivider = Color.FromArgb(0x12, 0x23, 0x42),
                DrawBrandIcon = (g, bounds) =>
                {
                    using var pen = new Pen(Color.FromArgb(0x3B, 0x82, 0xF6), 2f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    // Master Shield icon
                    PointF[] shield =
                    {
                        new(bounds.X + bounds.Width / 2f, bounds.Y + 4),
                        new(bounds.Right - 6, bounds.Y + 8),
                        new(bounds.Right - 6, bounds.Y + 18),
                        new(bounds.X + bounds.Width / 2f, bounds.Bottom - 4),
                        new(bounds.X + 6, bounds.Y + 18),
                        new(bounds.X + 6, bounds.Y + 8)
                    };
                    g.DrawPolygon(pen, shield);
                    g.DrawLine(pen, bounds.X + bounds.Width / 2f, bounds.Y + 8, bounds.X + bounds.Width / 2f, bounds.Bottom - 8);
                }
            };
        }

        /// <summary>
        /// Restored Old Super Admin & Admin - Classic Navy Blue CRM
        /// </summary>
        private static TenantUiTheme CreateLegacyCrmTheme(string bCode, string bName, string bTag, string summary)
        {
            return new TenantUiTheme
            {
                Mode = TenantUiMode.LegacyCrm,
                BrandName = bName,
                BrandSubtitle = "Management System",
                BrandBadge = bTag,
                BrandCode = bCode,
                TenantSummary = summary,
                SidebarBg = Color.FromArgb(0x0A, 0x14, 0x28),
                HeaderBg = Color.FromArgb(0x0A, 0x14, 0x28),
                ActiveItemBg = Color.FromArgb(0x14, 0x2A, 0x52),
                HoverItemBg = Color.FromArgb(0x12, 0x22, 0x40),
                AccentBlue = Color.FromArgb(0x42, 0xA5, 0xF5),
                BorderBlue = Color.FromArgb(0x1E, 0x3A, 0x6B),
                PillBg = Color.FromArgb(0x1A, 0x2A, 0x48),
                PillBorder = Color.Transparent,
                PillDot = Color.FromArgb(0x42, 0xA5, 0xF5),
                PillText = Color.White,
                GroupHeaderColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                UserCardBg = Color.FromArgb(0x0F, 0x1A, 0x2E),
                PrimaryButtonBg = Color.FromArgb(0x1E, 0x88, 0xE5),
                PrimaryButtonHover = Color.FromArgb(0x19, 0x76, 0xD2),
                BadgeBg = Color.FromArgb(0x16, 0x2A, 0x4E),
                BadgeText = Color.FromArgb(0xBA, 0xE6, 0xFD),
                TagBorder = Color.FromArgb(0x20, 0x3E, 0x72),
                SectionDivider = Color.FromArgb(0x14, 0x22, 0x3E),
                DrawBrandIcon = (g, bounds) =>
                {
                    using var pen = new Pen(Color.FromArgb(0x42, 0xA5, 0xF5), 1.8f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round
                    };
                    PointF[] pts =
                    {
                        new(bounds.X + 4, bounds.Y + 16),
                        new(bounds.Right - 4, bounds.Y + 6),
                        new(bounds.X + bounds.Width * 0.65f, bounds.Bottom - 4),
                        new(bounds.X + bounds.Width * 0.45f, bounds.Y + 19),
                        new(bounds.X + 4, bounds.Y + 16)
                    };
                    g.DrawLines(pen, pts);
                    g.DrawLine(pen, bounds.X + bounds.Width * 0.45f, bounds.Y + 19, bounds.Right - 4, bounds.Y + 6);
                }
            };
        }
    }
}
