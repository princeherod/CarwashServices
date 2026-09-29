using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Shell;

namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Super Admin Panel shell UserControl.
    /// Hosts the Super Admin sidebar navigation and swaps content panels between
    /// the Super Admin placeholder UserControls.
    /// </summary>
    public class SuperAdminPanelView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color NavyBg = Color.FromArgb(0x0A, 0x14, 0x28);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE4, 0xE9, 0xF1);

        // Purple theme for Super Admin
        private static readonly Color PurplePillBg = Color.FromArgb(0x2E, 0x1A, 0x4D);
        private static readonly Color PurplePillBorder = Color.FromArgb(0x6D, 0x28, 0xD9);
        private static readonly Color PurpleDot = Color.FromArgb(0xA8, 0x55, 0xF7);
        private static readonly Color PurpleText = Color.FromArgb(0xF3, 0xE8, 0xFF);

        // ================================================================
        //  Fields
        // ================================================================
        private Panel _sidebarPanel = null!;
        private Panel _headerPanel = null!;
        private Label _headerTitle = null!;
        private Panel _contentPanel = null!;

        private string _activeModule = "";
        private readonly Dictionary<string, SidebarItemButton> _buttons = new();

        public event EventHandler<string>? ModuleSelected;

        public string ActiveModuleKey => _activeModule;

        public SuperAdminPanelView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            BuildShell();

            Sidebar.EnableDoubleBuffering(this);

            // Default navigate to first module
            NavigateTo("Manage Admin Accounts");
        }

        private void BuildShell()
        {
            SuspendLayout();

            // 1. Sidebar
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 280,
                BackColor = NavyBg,
                Padding = new Padding(0)
            };
            BuildSidebarControls(_sidebarPanel);
            Controls.Add(_sidebarPanel);

            // 2. Main Area (Header + Content)
            var mainArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };

            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Navy
            };

            _headerTitle = new Label
            {
                Text = SuperAdminLabels.SuperAdminRoleTitle,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13.5f),
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(24, 17)
            };
            _headerPanel.Controls.Add(_headerTitle);
            mainArea.Controls.Add(_headerPanel);

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            mainArea.Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            Controls.Add(mainArea);
            mainArea.BringToFront();

            ResumeLayout(true);
        }

        private void BuildSidebarControls(Panel sidebar)
        {
            int y = 24;

            // Brand Icon
            var brandIcon = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(38, 38),
                BackColor = Color.Transparent
            };
            brandIcon.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(Color.FromArgb(0x1E, 0x88, 0xE5));
                using var path = RoundedRect(new Rectangle(0, 0, 38, 38), 10);
                g.FillPath(bg, path);

                using var pen = new Pen(Color.White, 1.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                PointF[] pts =
                {
                    new PointF(9, 19),
                    new PointF(29, 10),
                    new PointF(21, 29),
                    new PointF(17, 22),
                    new PointF(9, 19)
                };
                g.DrawLines(pen, pts);
                g.DrawLine(pen, 17, 22, 29, 10);
            };
            sidebar.Controls.Add(brandIcon);

            var brandTitle = new Label
            {
                Text = SuperAdminLabels.BrandTitle,
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(72, y + 2),
                AutoSize = true
            };
            sidebar.Controls.Add(brandTitle);

            var brandSubtitle = new Label
            {
                Text = SuperAdminLabels.BrandSubtitle,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(70, y + 16),
                AutoSize = true
            };
            sidebar.Controls.Add(brandSubtitle);

            y += 68;

            // ============================================================
            // PURPLE "SUPER ADMIN" ROLE PILL
            // ============================================================
            var superAdminPill = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(140, 30),
                BackColor = PurplePillBg
            };
            superAdminPill.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, superAdminPill.Width - 1, superAdminPill.Height - 1), 15);
                using var brush = new SolidBrush(superAdminPill.BackColor);
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(PurplePillBorder, 1.2f);
                e.Graphics.DrawPath(pen, path);
            };

            var pillDot = new Panel
            {
                Location = new Point(14, 11),
                Size = new Size(8, 8),
                BackColor = PurpleDot
            };
            pillDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(PurpleDot);
                e.Graphics.FillEllipse(brush, 0, 0, 7, 7);
            };
            superAdminPill.Controls.Add(pillDot);

            var pillLabel = new Label
            {
                Text = "Super Admin",
                ForeColor = PurpleText,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(28, 6),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            superAdminPill.Controls.Add(pillLabel);
            sidebar.Controls.Add(superAdminPill);

            y += 54;

            // ============================================================
            // SUPER ADMIN MODULES HEADER
            // ============================================================
            var groupLbl = new Label
            {
                Text = SuperAdminLabels.SuperAdminModulesHeader,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(28, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            sidebar.Controls.Add(groupLbl);
            y += 30;

            // ============================================================
            // NAV ITEMS
            // ============================================================
            var navItems = new[]
            {
                ("Manage Admin Accounts", "admin"),
                ("Backup & Restore Data", "database"),
                ("Manage Subscription / Billing", "billing"),
                ("Terms & Conditions", "terms")
            };

            foreach (var (title, iconKey) in navItems)
            {
                var btn = new SidebarItemButton(iconKey, title)
                {
                    Width = 248,
                    Height = 42,
                    Left = 16,
                    Top = y,
                    Cursor = Cursors.Hand
                };

                var captured = title;
                btn.Click += (s, e) => NavigateTo(captured);
                sidebar.Controls.Add(btn);
                _buttons[captured] = btn;

                y += 46;
            }

            // ============================================================
            // BOTTOM USER CARD
            // ============================================================
            var card = new Panel
            {
                Location = new Point(20, sidebar.Height - 130),
                Size = new Size(240, 110),
                BackColor = Color.FromArgb(0x0F, 0x1A, 0x2E),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, card.Width, card.Height), 10);
                using var brush = new SolidBrush(card.BackColor);
                e.Graphics.FillPath(brush, path);
            };

            var avatar = new Panel
            {
                Location = new Point(12, 12),
                Size = new Size(36, 36),
                BackColor = Color.Transparent
            };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(PurpleDot);
                e.Graphics.FillEllipse(brush, 0, 0, 36, 36);
            };
            var avatarLbl = new Label
            {
                Text = !string.IsNullOrWhiteSpace(SessionUser.FullName) ? Initials(SessionUser.FullName) : "SA",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            avatar.Controls.Add(avatarLbl);
            card.Controls.Add(avatar);

            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Super Administrator" : SessionUser.FullName,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Location = new Point(56, 14),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            card.Controls.Add(new Label
            {
                Text = "Super Admin",
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(56, 32),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            sidebar.Controls.Add(card);
            card.BringToFront();
        }

        public void NavigateTo(string key)
        {
            if (_activeModule == key && _contentPanel.Controls.Count > 0)
                return;

            if (key is "Manage Admin Accounts" or "Backup & Restore Data" or "Manage Subscription / Billing" or "Manage Subscription/Billing")
            {
                if (SessionUser.RoleId != 4)
                {
                    ShowAccessDenied(key, "Super Admin (Role 4)");
                    return;
                }
            }
            else if (key == "Terms & Conditions")
            {
                if (SessionUser.RoleId != 4 && SessionUser.RoleId != 1)
                {
                    ShowAccessDenied(key, "Super Admin (Role 4) or Admin (Role 1)");
                    return;
                }
            }

            if (!string.IsNullOrEmpty(_activeModule) && _buttons.TryGetValue(_activeModule, out var oldBtn))
                oldBtn.SetActive(false);

            _activeModule = key;

            if (_buttons.TryGetValue(key, out var newBtn))
                newBtn.SetActive(true);

            _contentPanel.SuspendLayout();
            try
            {
                foreach (Control c in _contentPanel.Controls)
                    c.Dispose();
                _contentPanel.Controls.Clear();

                UserControl? view = key switch
                {
                    "Manage Admin Accounts" => new ManageAdminAccountsView(),
                    "Backup & Restore Data" => new BackupRestoreDataView(),
                    "Manage Subscription / Billing" => new ManageSubscriptionBillingView(),
                    "Terms & Conditions" => new TermsAndConditionsView(isReadOnly: SessionUser.RoleId != 4),
                    _ => null
                };

                if (view != null)
                {
                    view.Dock = DockStyle.Fill;
                    _contentPanel.Controls.Add(view);
                    _headerTitle.Text = key;
                }
            }
            finally
            {
                _contentPanel.ResumeLayout(true);
            }

            ModuleSelected?.Invoke(this, key);
        }

        private void ShowAccessDenied(string key, string requiredRole)
        {
            _contentPanel.SuspendLayout();
            try
            {
                foreach (Control c in _contentPanel.Controls)
                    c.Dispose();
                _contentPanel.Controls.Clear();

                var deniedView = new AccessDeniedView(key, requiredRole, () =>
                {
                    if (SessionUser.RoleId == 1)
                        NavigateTo("Terms & Conditions");
                });
                deniedView.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(deniedView);
                _headerTitle.Text = SuperAdminLabels.AccessDeniedHeader;
            }
            finally
            {
                _contentPanel.ResumeLayout(true);
            }
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "SA";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

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
        //  Sidebar Item Button
        // ================================================================
        private sealed class SidebarItemButton : Button
        {
            private static readonly Color BgNormal = Color.FromArgb(0x0A, 0x14, 0x28);
            private static readonly Color BgActive = Color.FromArgb(0x1A, 0x16, 0x38); // Soft purple-navy active
            private static readonly Color BgHover = Color.FromArgb(0x14, 0x1C, 0x34);
            private static readonly Color Accent = Color.FromArgb(0xA8, 0x55, 0xF7); // Purple accent
            private static readonly Color IconIdle = Color.FromArgb(0x9A, 0xA8, 0xC0);
            private static readonly Color TextMain = Color.White;

            private readonly string _iconKey;
            private readonly string _label;
            private bool _active;
            private bool _hover;

            public SidebarItemButton(string iconKey, string label)
            {
                _iconKey = iconKey ?? "admin";
                _label = label ?? "";

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = BgNormal;
                ForeColor = TextMain;
                Font = new Font("Segoe UI", 9f);
                Text = "";

                MouseEnter += (s, e) => { _hover = true; Invalidate(); };
                MouseLeave += (s, e) => { _hover = false; Invalidate(); };
            }

            public void SetActive(bool active)
            {
                _active = active;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs pevent)
            {
                var g = pevent.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color bg = _active ? BgActive : (_hover ? BgHover : BgNormal);
                using (var brush = new SolidBrush(bg))
                    g.FillRectangle(brush, ClientRectangle);

                if (_active)
                {
                    using var accent = new SolidBrush(Accent);
                    g.FillRectangle(accent, 0, 0, 4, Height);
                }

                Color iconColor = _active ? Accent : IconIdle;
                var iconRect = new RectangleF(16, (Height - 20) / 2f, 20, 20);
                DrawIcon(g, _iconKey, iconRect, iconColor);

                var textRect = new Rectangle(44, 0, Width - 46, Height);
                TextRenderer.DrawText(
                    g, _label, Font, textRect, TextMain,
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

                switch (key)
                {
                    case "admin":
                        PointF[] shield =
                        {
                            new PointF(10, 2),
                            new PointF(17, 5),
                            new PointF(17, 11),
                            new PointF(10, 18),
                            new PointF(3, 11),
                            new PointF(3, 5)
                        };
                        g.DrawPolygon(pen, shield);
                        break;
                    case "database":
                        g.DrawEllipse(pen, 3, 3, 14, 5);
                        g.DrawLine(pen, 3, 5, 3, 15);
                        g.DrawLine(pen, 17, 5, 17, 15);
                        g.DrawArc(pen, 3, 7, 14, 5, 0, 180);
                        g.DrawArc(pen, 3, 12, 14, 5, 0, 180);
                        break;
                    case "billing":
                        g.DrawRectangle(pen, 2, 4, 16, 12);
                        g.DrawLine(pen, 2, 8, 18, 8);
                        g.DrawLine(pen, 5, 12, 9, 12);
                        break;
                    case "terms":
                        g.DrawRectangle(pen, 3, 2, 14, 16);
                        g.DrawLine(pen, 6, 6, 14, 6);
                        g.DrawLine(pen, 6, 10, 14, 10);
                        g.DrawLine(pen, 6, 14, 11, 14);
                        break;
                    default:
                        g.DrawEllipse(pen, 4, 4, 12, 12);
                        break;
                }

                g.Restore(state);
            }
        }
    }
}
