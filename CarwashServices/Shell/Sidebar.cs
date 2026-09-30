using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CarwashServices.Auth;
using CarwashServices.Common;

namespace CarwashServices.Shell
{
    public class Sidebar : Panel
    {
        public event EventHandler<string>? ModuleSelected;
        public event EventHandler? SignOutRequested;

        private string _activeModule = "";
        public string ActiveModuleKey => _activeModule;

        private readonly Dictionary<string, SidebarButton> _moduleButtons = new();
        private SidebarButton? _overviewHeaderBtn;
        private Label? _branchStatusLbl;
        private Panel _navPanel = null!;

        public Sidebar()
        {
            Dock = DockStyle.Left;
            Width = 260;
            Padding = new Padding(0);
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);

            Build();

            SessionUser.BranchChanged += () =>
            {
                if (_branchStatusLbl != null && !_branchStatusLbl.IsDisposed)
                {
                    _branchStatusLbl.Text = string.IsNullOrEmpty(SessionUser.CurrentBranchName)
                        ? "📍 Context: All Branches"
                        : $"📍 Context: {SessionUser.CurrentBranchName}";
                }
            };

            EnableDoubleBuffering(this);
        }

        public void SetActiveModule(string? moduleKey)
        {
            if (_activeModule == moduleKey) return;

            if (!string.IsNullOrEmpty(_activeModule) &&
                _moduleButtons.TryGetValue(_activeModule, out var oldBtn))
            {
                oldBtn.SetActive(false);
            }

            _activeModule = moduleKey ?? "";

            if (!string.IsNullOrEmpty(moduleKey) && _moduleButtons.TryGetValue(moduleKey, out var newBtn))
            {
                newBtn.SetActive(true);
            }

            // Sync Overview top pill button active highlight
            if (_overviewHeaderBtn != null)
            {
                bool isOverviewActive = _activeModule is "View Dashboard" or "Overview";
                _overviewHeaderBtn.SetActive(isOverviewActive);
            }
        }

        private void Build()
        {
            Controls.Clear();
            _moduleButtons.Clear();

            var theme = TenantThemeManager.Current;
            BackColor = theme.SidebarBg;

            // ============================================================
            // 1. TOP HEADER PANEL (Brand + Badges)
            // ============================================================
            int topHeaderHeight = SessionUser.MultiBranchEnabled ? 134 : 100;
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = topHeaderHeight,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            int y = 14;

            // Brand Icon
            var brandIcon = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(38, 38),
                BackColor = Color.Transparent
            };
            brandIcon.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var bg = new SolidBrush(theme.PillBg);
                using var path = RoundedRect(new Rectangle(0, 0, 37, 37), 10);
                g.FillPath(bg, path);

                using var borderPen = new Pen(theme.BorderBlue, 1.2f);
                g.DrawPath(borderPen, path);

                theme.DrawBrandIcon?.Invoke(g, new RectangleF(0, 0, 38, 38));
            };
            topPanel.Controls.Add(brandIcon);

            var brandCodeLbl = new Label
            {
                Text = theme.BrandCode,
                ForeColor = theme.AccentBlue,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(64, y),
                AutoSize = true,
                MaximumSize = new Size(185, 16),
                AutoEllipsis = true
            };
            topPanel.Controls.Add(brandCodeLbl);

            var brandNameLbl = new Label
            {
                Text = theme.BrandName,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5f),
                Location = new Point(63, y + 16),
                AutoSize = true,
                MaximumSize = new Size(188, 22),
                AutoEllipsis = true
            };
            topPanel.Controls.Add(brandNameLbl);

            y += 44;

            // Badges row (Brand Badge + Role Pill)
            var badgesContainer = new FlowLayoutPanel
            {
                Location = new Point(18, y),
                Size = new Size(230, 30),
                BackColor = Color.Transparent,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };

            var tenantBadge = new Label
            {
                Text = theme.BrandBadge,
                ForeColor = theme.BadgeText,
                BackColor = theme.BadgeBg,
                Font = new Font("Segoe UI Semibold", 7.5f),
                AutoSize = false,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 6, 0)
            };
            using (var g = CreateGraphics())
            {
                var sz = g.MeasureString(tenantBadge.Text, tenantBadge.Font);
                tenantBadge.Width = (int)sz.Width + 12;
            }
            tenantBadge.Paint += (s, e) =>
            {
                using var p = new Pen(theme.TagBorder, 1f);
                using var path = RoundedRect(new Rectangle(0, 0, tenantBadge.Width - 1, tenantBadge.Height - 1), 6);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(p, path);
            };
            badgesContainer.Controls.Add(tenantBadge);

            string roleLabel = SessionUser.Role switch
            {
                UserRole.SuperAdmin => "Super Admin",
                UserRole.Admin => "Admin",
                UserRole.Manager => "Manager",
                UserRole.ServiceStaff => "Service Staff",
                _ => "User"
            };

            var rolePill = new Panel
            {
                Height = 22,
                BackColor = theme.PillBg,
                Margin = new Padding(0)
            };

            var roleDot = new Panel
            {
                Location = new Point(7, 7),
                Size = new Size(7, 7),
                BackColor = theme.PillDot
            };
            roleDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(theme.PillDot);
                e.Graphics.FillEllipse(brush, 0, 0, 6, 6);
            };
            rolePill.Controls.Add(roleDot);

            var roleText = new Label
            {
                Text = roleLabel,
                ForeColor = theme.PillText,
                Font = new Font("Segoe UI Semibold", 7.8f),
                Location = new Point(17, 3),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            rolePill.Controls.Add(roleText);

            using (var g = CreateGraphics())
            {
                var sz = g.MeasureString(roleLabel, roleText.Font);
                rolePill.Width = (int)sz.Width + 28;
            }
            rolePill.Paint += (s, e) =>
            {
                using var path = RoundedRect(new Rectangle(0, 0, rolePill.Width - 1, rolePill.Height - 1), 8);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (theme.PillBorder != Color.Transparent)
                {
                    using var pen = new Pen(theme.PillBorder, 1f);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            badgesContainer.Controls.Add(rolePill);
            topPanel.Controls.Add(badgesContainer);

            y += 34;

            // Multi-branch banner if enabled
            if (SessionUser.MultiBranchEnabled)
            {
                var branchBanner = new Panel
                {
                    Location = new Point(18, y),
                    Size = new Size(224, 24),
                    BackColor = theme.BadgeBg
                };
                branchBanner.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var p = new Pen(theme.TagBorder, 1f);
                    using var path = RoundedRect(new Rectangle(0, 0, branchBanner.Width - 1, branchBanner.Height - 1), 6);
                    e.Graphics.DrawPath(p, path);
                };

                _branchStatusLbl = new Label
                {
                    Text = string.IsNullOrEmpty(SessionUser.CurrentBranchName)
                        ? "📍 Context: All Branches"
                        : $"📍 Context: {SessionUser.CurrentBranchName}",
                    ForeColor = theme.AccentBlue,
                    Font = new Font("Segoe UI Semibold", 8f),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 4, 0),
                    BackColor = Color.Transparent
                };
                branchBanner.Controls.Add(_branchStatusLbl);
                topPanel.Controls.Add(branchBanner);
            }

            Controls.Add(topPanel);

            // ============================================================
            // 2. BOTTOM PANEL (Settings + Logout)
            // ============================================================
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 92,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            // Divider above bottom actions
            bottomPanel.Paint += (s, e) =>
            {
                using var p = new Pen(theme.SectionDivider, 1f);
                e.Graphics.DrawLine(p, 18, 1, 242, 1);
            };

            var settingsBtn = new SidebarButton("settings", "Settings", theme)
            {
                Width = 224,
                Height = 36,
                Left = 18,
                Top = 8,
                Cursor = Cursors.Hand,
                Tag = "Settings"
            };
            settingsBtn.Click += (s, e) => ModuleSelected?.Invoke(this, "Settings");
            bottomPanel.Controls.Add(settingsBtn);

            var logoutBtn = new SidebarButton("logout", "Logout", theme)
            {
                Width = 224,
                Height = 36,
                Left = 18,
                Top = 48,
                Cursor = Cursors.Hand,
                Tag = "Logout"
            };
            logoutBtn.Click += (s, e) => SignOut(theme);
            bottomPanel.Controls.Add(logoutBtn);

            Controls.Add(bottomPanel);

            // ============================================================
            // 3. MIDDLE SCROLLABLE NAVIGATION CONTAINER
            // ============================================================
            _navPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            Controls.Add(_navPanel);
            _navPanel.BringToFront();

            int navY = 6;
            var sections = RoleRouter.GetSectionsFor(SessionUser.Role);

            bool isFirstSection = true;
            foreach (var section in sections)
            {
                if (section.Title == "OVERVIEW")
                {
                    // Render featured Overview blue pill button
                    _overviewHeaderBtn = new SidebarButton("home", "Overview", theme, isPillStyle: true)
                    {
                        Width = 224,
                        Height = 40,
                        Left = 18,
                        Top = navY,
                        Cursor = Cursors.Hand,
                        Tag = "Overview"
                    };
                    _overviewHeaderBtn.Click += (s, e) => ModuleSelected?.Invoke(this, "View Dashboard");
                    _navPanel.Controls.Add(_overviewHeaderBtn);
                    navY += 44;

                    // Child items under Overview
                    foreach (var moduleKey in section.Modules)
                    {
                        navY = AddNavItem(moduleKey, IconKeyFor(moduleKey), navY, moduleKey, theme, isSubItem: true);
                    }
                    navY += 8;

                    // Divider after Overview
                    navY = AddDivider(navY, theme);
                }
                else
                {
                    if (!isFirstSection && section.Title != "OVERVIEW")
                    {
                        // Add divider before section if not first
                    }

                    navY = AddSectionHeader(section.Title, navY, theme);
                    foreach (var moduleKey in section.Modules)
                    {
                        navY = AddNavItem(moduleKey, IconKeyFor(moduleKey), navY, moduleKey, theme, isSubItem: false);
                    }
                    navY += 8;

                    // Divider after section
                    navY = AddDivider(navY, theme);
                }

                isFirstSection = false;
            }
        }

        private int AddSectionHeader(string text, int y, TenantUiTheme theme)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = theme.GroupHeaderColor,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(22, y),
                Size = new Size(220, 20),
                BackColor = Color.Transparent
            };
            _navPanel.Controls.Add(lbl);
            return y + 24;
        }

        private int AddDivider(int y, TenantUiTheme theme)
        {
            var pnl = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(224, 1),
                BackColor = theme.SectionDivider
            };
            _navPanel.Controls.Add(pnl);
            return y + 10;
        }

        private int AddNavItem(string label, string iconKey, int y, string? key, TenantUiTheme theme, bool isSubItem)
        {
            bool isComingSoon = key != null && ComingSoonModules.Contains(key);

            var btn = new SidebarButton(iconKey, label, theme, isComingSoon, isPillStyle: false, isSubItem: isSubItem)
            {
                Width = 224,
                Height = 36,
                Left = 18,
                Top = y,
                Cursor = Cursors.Hand,
                Tag = key
            };

            if (key != null && key == _activeModule)
                btn.SetActive(true);

            if (key != null)
            {
                var capturedKey = key;
                btn.Click += (s, e) => ModuleSelected?.Invoke(this, capturedKey);
                _moduleButtons[capturedKey] = btn;
            }

            _navPanel.Controls.Add(btn);

            if (isComingSoon)
            {
                var tag = new Label
                {
                    Text = "SOON",
                    ForeColor = Color.FromArgb(0xBA, 0xE6, 0xFD),
                    BackColor = theme.BadgeBg,
                    Font = new Font("Segoe UI Semibold", 7f),
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(44, 16),
                    Location = new Point(18 + 224 - 50, y + 10)
                };
                using (var path = RoundedRect(new Rectangle(0, 0, tag.Width, tag.Height), 8))
                    tag.Region = new Region(path);
                _navPanel.Controls.Add(tag);
                tag.BringToFront();
            }

            return y + 38;
        }

        private void SignOut(TenantUiTheme theme)
        {
            var confirm = MessageBox.Show(
                $"Sign out of {theme.BrandName}?",
                "Sign Out",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            SessionUser.Clear();
            SignOutRequested?.Invoke(this, EventArgs.Empty);
        }

        private static string IconKeyFor(string moduleKey) => moduleKey switch
        {
            "Overview" => "home",
            "View Dashboard" => "dot",
            "Analytics" => "analytics",
            "Business Intelligence" => "analytics",
            "View Reports" => "reports",
            "Manage Users" => "users",
            "Manage Customers" => "customers",
            "Manage Services" => "services",
            "Manage Service Requests" => "requests",
            "Follow-Ups / Reminders" => "reminders",
            "Admin Panel" => "home",
            "Manage Businesses" => "businesses",
            "Manage Admin Accounts" => "admin",
            "Subscriptions" => "billing",
            "Manage Subscription / Billing" => "billing",
            "Backup" => "database",
            "Backup & Restore" => "database",
            "Backup & Restore Data" => "database",
            "Backup and Restore Data" => "database",
            "Branches" => "branches",
            "Branching" => "branches",
            "Terms & Conditions" => "terms",
            "Assign Service Staff" => "users",
            "Monitor Service Status" => "analytics",
            "View Assigned Requests" => "requests",
            "Update Service Status" => "requests",
            "Settings" => "settings",
            "Logout" => "logout",
            _ => "dashboard"
        };

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y + bounds.Height - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Y + bounds.Height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ================================================================
        //  SidebarButton
        // ================================================================
        private sealed class SidebarButton : Button
        {
            private readonly string _iconKey;
            private readonly string _label;
            private readonly TenantUiTheme _theme;
            private readonly bool _comingSoon;
            private readonly bool _isPillStyle;
            private readonly bool _isSubItem;
            private bool _active;
            private bool _hover;

            public SidebarButton(
                string iconKey,
                string label,
                TenantUiTheme theme,
                bool comingSoon = false,
                bool isPillStyle = false,
                bool isSubItem = false)
            {
                _iconKey = iconKey ?? "dashboard";
                _label = label ?? "";
                _theme = theme;
                _comingSoon = comingSoon;
                _isPillStyle = isPillStyle;
                _isSubItem = isSubItem;

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = _theme.SidebarBg;
                ForeColor = Color.White;
                Font = new Font("Segoe UI", isPillStyle ? 9.5f : 9.2f);
                Text = "";
            }

            public void SetActive(bool active)
            {
                if (_active == active) return;
                _active = active;
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hover = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hover = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (_isPillStyle)
                {
                    // Featured Overview Blue Pill (matches top of screenshot)
                    Color pillColor = _active ? Color.FromArgb(0x1D, 0x4E, 0xD8) : (_hover ? _theme.HoverItemBg : Color.FromArgb(0x13, 0x2C, 0x54));
                    using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8);
                    using var brush = new SolidBrush(pillColor);
                    g.FillPath(brush, path);

                    var iconRect = new RectangleF(12, (Height - 20) / 2f, 20, 20);
                    DrawIcon(g, _iconKey, iconRect, Color.White);

                    var textRect = new Rectangle(40, 0, Width - 44, Height);
                    var pillFont = new Font("Segoe UI Semibold", 9.5f);
                    TextRenderer.DrawText(
                        g, _label, pillFont, textRect, Color.White,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    return;
                }

                // Standard Menu Item
                Color bg = _active
                    ? _theme.ActiveItemBg
                    : (_hover ? _theme.HoverItemBg : _theme.SidebarBg);

                using (var b = new SolidBrush(bg))
                    g.FillRectangle(b, ClientRectangle);

                if (_active && !_isSubItem)
                {
                    // Left accent bar indicator
                    using var accent = new SolidBrush(_theme.AccentBlue);
                    g.FillRectangle(accent, 0, 0, 3, Height);
                }

                Color iconColor = _comingSoon
                    ? Color.FromArgb(0x56, 0x69, 0x8C)
                    : (_active ? _theme.AccentBlue : Color.FromArgb(0x9A, 0xA8, 0xC0));

                int iconLeft = _isSubItem ? 16 : 12;
                var itemIconRect = new RectangleF(iconLeft, (Height - 18) / 2f, 18, 18);
                DrawIcon(g, _iconKey, itemIconRect, iconColor);

                int textLeft = _isSubItem ? 42 : 38;
                int textWidth = _comingSoon ? Width - textLeft - 48 : Width - textLeft - 6;
                var itemTextRect = new Rectangle(textLeft, 0, Math.Max(20, textWidth), Height);

                Color textColor = _comingSoon
                    ? Color.FromArgb(0x73, 0x88, 0xAD)
                    : (_active ? Color.White : Color.FromArgb(0xDE, 0xE7, 0xF5));

                Font font = _active ? new Font(Font, FontStyle.Bold) : Font;

                TextRenderer.DrawText(
                    g, _label, font, itemTextRect, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            private static void DrawIcon(Graphics g, string key, RectangleF bounds, Color color)
            {
                var state = g.Save();
                g.SmoothingMode = SmoothingMode.AntiAlias;

                g.TranslateTransform(bounds.X, bounds.Y);
                g.ScaleTransform(bounds.Width / 20f, bounds.Height / 20f);

                using var pen = new Pen(color, 1.6f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using var brush = new SolidBrush(color);

                switch (key)
                {
                    case "home":
                        PointF[] roof = { new(10, 2), new(18, 9), new(2, 9) };
                        g.DrawPolygon(pen, roof);
                        g.DrawRectangle(pen, 5, 9, 10, 9);
                        g.DrawRectangle(pen, 8, 12, 4, 6);
                        break;

                    case "dot":
                        g.FillEllipse(brush, 6, 7, 7, 7);
                        break;

                    case "dashboard":
                        g.DrawRectangle(pen, 2, 2, 7, 7);
                        g.DrawRectangle(pen, 11, 2, 7, 7);
                        g.DrawRectangle(pen, 2, 11, 7, 7);
                        g.DrawRectangle(pen, 11, 11, 7, 7);
                        break;

                    case "analytics":
                        g.DrawLine(pen, 2, 18, 2, 3);
                        g.DrawLine(pen, 2, 18, 18, 18);
                        g.FillRectangle(brush, 5, 11, 3, 7);
                        g.FillRectangle(brush, 10, 7, 3, 11);
                        g.FillRectangle(brush, 15, 3, 3, 15);
                        break;

                    case "reports":
                        g.DrawRectangle(pen, 3, 2, 14, 16);
                        g.DrawLine(pen, 6, 6, 14, 6);
                        g.DrawLine(pen, 6, 10, 14, 10);
                        g.DrawLine(pen, 6, 14, 11, 14);
                        break;

                    case "users":
                        g.DrawEllipse(pen, 3, 3, 6, 6);
                        g.DrawArc(pen, 1, 9, 10, 9, 180, 180);
                        g.DrawEllipse(pen, 13, 7, 5, 5);
                        g.DrawArc(pen, 11, 12, 8, 7, 180, 180);
                        break;

                    case "customers":
                        g.DrawEllipse(pen, 6, 2, 7, 7);
                        g.DrawArc(pen, 2, 9, 15, 11, 180, 180);
                        break;

                    case "services":
                        g.DrawArc(pen, 1, 1, 10, 10, 30, 300);
                        g.DrawLine(pen, 9, 9, 17, 17);
                        g.DrawEllipse(pen, 15, 15, 4, 4);
                        break;

                    case "requests":
                        g.DrawRectangle(pen, 3, 2, 14, 16);
                        g.DrawLine(pen, 6, 6, 7, 6);
                        g.DrawLine(pen, 9, 6, 14, 6);
                        g.DrawLine(pen, 6, 10, 7, 10);
                        g.DrawLine(pen, 9, 10, 14, 10);
                        g.DrawLine(pen, 6, 14, 7, 14);
                        g.DrawLine(pen, 9, 14, 14, 14);
                        break;

                    case "reminders":
                        g.DrawArc(pen, 4, 3, 12, 12, 180, 180);
                        g.DrawLine(pen, 4, 11, 4, 15);
                        g.DrawLine(pen, 16, 11, 16, 15);
                        g.DrawLine(pen, 3, 15, 17, 15);
                        g.DrawEllipse(pen, 8, 16, 4, 3);
                        break;

                    case "businesses":
                        g.DrawRectangle(pen, 3, 4, 14, 14);
                        g.DrawLine(pen, 3, 8, 17, 8);
                        g.DrawLine(pen, 7, 8, 7, 18);
                        g.DrawLine(pen, 10, 8, 10, 18);
                        g.DrawLine(pen, 13, 8, 13, 18);
                        break;

                    case "admin":
                        PointF[] shield =
                        {
                            new(10, 2),
                            new(17, 5),
                            new(17, 11),
                            new(10, 18),
                            new(3, 11),
                            new(3, 5)
                        };
                        g.DrawPolygon(pen, shield);
                        g.DrawEllipse(pen, 8, 5, 4, 4);
                        g.DrawArc(pen, 6, 10, 8, 6, 180, 180);
                        break;

                    case "billing":
                        g.DrawRectangle(pen, 2, 4, 16, 12);
                        g.DrawLine(pen, 2, 8, 18, 8);
                        g.DrawLine(pen, 5, 12, 9, 12);
                        break;

                    case "database":
                        g.DrawEllipse(pen, 3, 3, 14, 5);
                        g.DrawLine(pen, 3, 5, 3, 15);
                        g.DrawLine(pen, 17, 5, 17, 15);
                        g.DrawArc(pen, 3, 7, 14, 5, 0, 180);
                        g.DrawArc(pen, 3, 12, 14, 5, 0, 180);
                        break;

                    case "terms":
                        g.DrawRectangle(pen, 3, 2, 14, 16);
                        g.DrawLine(pen, 6, 6, 14, 6);
                        g.DrawLine(pen, 6, 10, 14, 10);
                        g.DrawLine(pen, 6, 14, 11, 14);
                        break;

                    case "branches":
                        g.DrawEllipse(pen, 2, 8, 4, 4);
                        g.DrawEllipse(pen, 13, 3, 4, 4);
                        g.DrawEllipse(pen, 13, 13, 4, 4);
                        g.DrawLine(pen, 6, 10, 10, 10);
                        g.DrawLine(pen, 10, 10, 13, 5);
                        g.DrawLine(pen, 10, 10, 13, 15);
                        break;

                    case "settings":
                        g.DrawEllipse(pen, 6, 6, 8, 8);
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = i * Math.PI / 4.0;
                            float x1 = 10 + (float)(4.5 * Math.Cos(angle));
                            float y1 = 10 + (float)(4.5 * Math.Sin(angle));
                            float x2 = 10 + (float)(7.8 * Math.Cos(angle));
                            float y2 = 10 + (float)(7.8 * Math.Sin(angle));
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;

                    case "logout":
                        g.DrawLine(pen, 11, 3, 4, 3);
                        g.DrawLine(pen, 4, 3, 4, 17);
                        g.DrawLine(pen, 4, 17, 11, 17);
                        g.DrawLine(pen, 8, 10, 17, 10);
                        g.DrawLine(pen, 13, 6, 17, 10);
                        g.DrawLine(pen, 13, 14, 17, 10);
                        break;

                    default:
                        g.DrawEllipse(pen, 4, 4, 12, 12);
                        break;
                }

                g.Restore(state);
            }
        }

        // ================================================================
        //  DOUBLE-BUFFERING HELPER
        // ================================================================
        internal static void EnableDoubleBuffering(Control parent)
        {
            var dbProp = typeof(Control).GetProperty(
                "DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            foreach (Control child in parent.Controls)
            {
                dbProp?.SetValue(child, true);
                EnableDoubleBuffering(child);
            }
        }
    }
}