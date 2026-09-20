using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using CarwashServices.Auth;

namespace CarwashServices.Shell
{
    public class Sidebar : Panel
    {
        public event EventHandler<string>? ModuleSelected;

        private string _activeModule = "";

        public string ActiveModuleKey => _activeModule;

        // Kept so we can just toggle colours on click instead of rebuilding the whole sidebar.
        private readonly Dictionary<string, Button> _moduleButtons = new();
        private readonly Dictionary<string, Panel> _moduleAccents = new();

        // ---- Palette ----
        private static readonly Color NavyBg = Color.FromArgb(0x0A, 0x14, 0x28);
        private static readonly Color NavyActive = Color.FromArgb(0x14, 0x2A, 0x52);
        private static readonly Color NavyHover = Color.FromArgb(0x12, 0x22, 0x40);
        private static readonly Color AccentBlue = Color.FromArgb(0x42, 0xA5, 0xF5);
        private static readonly Color TextMuted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color TextDim = Color.FromArgb(0x9A, 0xA8, 0xC0);
        private static readonly Color TextMain = Color.White;

        public Sidebar()
        {
            Dock = DockStyle.Left;
            Width = 280;
            BackColor = NavyBg;
            Padding = new Padding(0);
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);

            _moduleButtons.Clear();
            _moduleAccents.Clear();
            Build();
        }

        /// <summary>
        /// Just updates the visual highlight — does NOT rebuild the sidebar.
        /// </summary>
        public void SetActiveModule(string moduleKey)
        {
            if (_activeModule == moduleKey) return;

            // Dim the previously active item
            if (!string.IsNullOrEmpty(_activeModule) &&
                _moduleButtons.TryGetValue(_activeModule, out var oldBtn))
            {
                oldBtn.BackColor = NavyBg;
                oldBtn.FlatAppearance.MouseOverBackColor = NavyHover;
                if (_moduleAccents.TryGetValue(_activeModule, out var oldAccent))
                    oldAccent.Visible = false;
            }

            _activeModule = moduleKey;

            // Highlight the newly active item
            if (_moduleButtons.TryGetValue(moduleKey, out var newBtn))
            {
                newBtn.BackColor = NavyActive;
                newBtn.FlatAppearance.MouseOverBackColor = NavyActive;
                if (_moduleAccents.TryGetValue(moduleKey, out var newAccent))
                    newAccent.Visible = true;
            }
        }

        private void Build()
        {
            int y = 24;

            // ============================================================
            // BRAND
            // ============================================================
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

                using var pen = new Pen(Color.White, 1.8f);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

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
            Controls.Add(brandIcon);

            var brandTitle = new Label
            {
                Text = "AQUASHINE",
                ForeColor = TextDim,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(72, y + 2),
                AutoSize = true
            };
            Controls.Add(brandTitle);

            var brandSubtitle = new Label
            {
                Text = "CRM System",
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(70, y + 16),
                AutoSize = true
            };
            Controls.Add(brandSubtitle);

            y += 68;

            // ============================================================
            // ROLE PILL
            // ============================================================
            string roleLabel = SessionUser.Role switch
            {
                UserRole.SuperAdmin => "Super Admin",
                UserRole.Admin => "Admin",
                UserRole.Manager => "Manager",
                UserRole.ServiceStaff => "Service Staff",
                _ => "Unknown"
            };

            var adminPill = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(0x1A, 0x2A, 0x48)
            };
            adminPill.Paint += (s, e) =>
            {
                using var path = RoundedRect(new Rectangle(0, 0, adminPill.Width, adminPill.Height), 15);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(adminPill.BackColor);
                e.Graphics.FillPath(brush, path);
            };

            var adminDot = new Panel
            {
                Location = new Point(14, 12),
                Size = new Size(7, 7),
                BackColor = AccentBlue
            };
            adminDot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(AccentBlue);
                e.Graphics.FillEllipse(brush, 0, 0, 7, 7);
            };
            adminPill.Controls.Add(adminDot);

            var adminLabel = new Label
            {
                Text = roleLabel,
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(28, 6),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            adminPill.Controls.Add(adminLabel);
            Controls.Add(adminPill);

            y += 54;

            // ============================================================
            // MENU — driven by RoleRouter
            // ============================================================
            var modules = RoleRouter.ModulesFor(SessionUser.Role);
            var overviewKeys = new[] { "View Dashboard", "Analytics", "View Reports" };

            y = AddGroupLabel("OVERVIEW", y);
            foreach (var key in modules)
                if (Array.IndexOf(overviewKeys, key) >= 0)
                    y = AddItem(key, IconKeyFor(key), y, key);

            y += 22;

            y = AddGroupLabel("MODULES", y);
            foreach (var key in modules)
                if (Array.IndexOf(overviewKeys, key) < 0)
                    y = AddItem(key, IconKeyFor(key), y, key);

            // ============================================================
            // BOTTOM USER CARD
            // ============================================================
            var card = new Panel
            {
                Location = new Point(20, Height - 130),
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
                using var brush = new SolidBrush(Color.FromArgb(0x1E, 0x88, 0xE5));
                e.Graphics.FillEllipse(brush, 0, 0, 36, 36);
            };
            var avatarLbl = new Label
            {
                Text = Initials(SessionUser.FullName),
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 10f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            avatar.Controls.Add(avatarLbl);
            card.Controls.Add(avatar);

            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Not signed in" : SessionUser.FullName,
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 10f),
                Location = new Point(58, 16),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            card.Controls.Add(new Label
            {
                Text = roleLabel,
                ForeColor = TextDim,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(58, 34),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var signOutBtn = new Button
            {
                Text = "Sign Out",
                ForeColor = TextMain,
                BackColor = Color.FromArgb(0x0F, 0x1A, 0x2E),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(12, 68),
                Size = new Size(216, 30),
                Cursor = Cursors.Hand
            };
            signOutBtn.FlatAppearance.BorderSize = 0;
            signOutBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x1A, 0x2A, 0x48);
            signOutBtn.Click += (s, e) => SignOut();
            card.Controls.Add(signOutBtn);

            Controls.Add(card);
            card.BringToFront();
        }

        private void SignOut()
        {
            SessionUser.Clear();
            var main = FindForm();
            main?.Hide();

            using var login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
                Application.Restart();
            else
                main?.Close();
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        private static string IconKeyFor(string moduleKey) => moduleKey switch
        {
            "View Dashboard" => "dashboard",
            "Analytics" => "analytics",
            "View Reports" => "reports",
            "Manage Users" => "users",
            "Manage Customers" => "customers",
            "Manage Services" => "services",
            "Manage Service Requests" => "requests",
            "Follow-Ups / Reminders" => "reminders",
            "Manage Admin Accounts" => "admin",
            _ => "dashboard"
        };

        // ================================================================
        // GROUP LABEL
        // ================================================================
        private int AddGroupLabel(string text, int y)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(28, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            Controls.Add(lbl);
            return y + 30;
        }

        // ================================================================
        // MENU ITEM — always creates the accent bar, just toggles visibility
        // ================================================================
        private int AddItem(string label, string iconKey, int y, string? key = null)
        {
            bool isActive = key != null && key == _activeModule;

            var btn = new Button
            {
                Text = "        " + label,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = TextMain,
                BackColor = isActive ? NavyActive : NavyBg,
                Width = 240,
                Height = 42,
                Left = 20,
                Top = y,
                Cursor = Cursors.Hand,
                Padding = new Padding(20, 0, 0, 0)
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = isActive ? NavyActive : NavyHover;

            // Accent bar — always created, just hidden when inactive
            var accent = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(4, 42),
                BackColor = AccentBlue,
                Visible = isActive
            };
            btn.Controls.Add(accent);

            var icon = new Panel
            {
                Location = new Point(20, 11),
                Size = new Size(20, 20),
                BackColor = Color.Transparent
            };
            icon.Paint += (s, e) => DrawIcon(e.Graphics, iconKey, isActive ? AccentBlue : TextDim);
            btn.Controls.Add(icon);

            if (key != null)
            {
                var capturedKey = key;
                btn.Click += (s, e) => ModuleSelected?.Invoke(this, capturedKey);
                _moduleButtons[capturedKey] = btn;
                _moduleAccents[capturedKey] = accent;
            }

            Controls.Add(btn);
            return y + 46;
        }

        // ================================================================
        // ICON DRAWING
        // ================================================================
        private static void DrawIcon(Graphics g, string key, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, 1.6f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            using var brush = new SolidBrush(color);

            switch (key)
            {
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
                    g.FillRectangle(brush, 15, 4, 3, 14);
                    break;
                case "reports":
                    g.DrawRectangle(pen, 3, 2, 14, 17);
                    g.DrawLine(pen, 6, 7, 14, 7);
                    g.DrawLine(pen, 6, 11, 14, 11);
                    g.DrawLine(pen, 6, 15, 12, 15);
                    break;
                case "users":
                    g.DrawEllipse(pen, 2, 3, 6, 6);
                    g.DrawArc(pen, 0, 9, 10, 10, 180, 180);
                    g.DrawEllipse(pen, 13, 9, 5, 5);
                    g.DrawLine(pen, 15, 14, 15, 19);
                    g.DrawLine(pen, 15, 17, 18, 17);
                    break;
                case "customers":
                    g.DrawEllipse(pen, 3, 2, 5, 5);
                    g.DrawArc(pen, 1, 8, 9, 9, 180, 180);
                    g.DrawEllipse(pen, 12, 3, 5, 5);
                    g.DrawArc(pen, 10, 9, 9, 9, 180, 180);
                    break;
                case "services":
                    g.DrawArc(pen, 1, 1, 10, 10, 30, 300);
                    g.DrawLine(pen, 9, 9, 17, 17);
                    g.DrawEllipse(pen, 15, 15, 4, 4);
                    break;
                case "requests":
                    g.DrawRectangle(pen, 1, 8, 11, 8);
                    g.DrawLine(pen, 12, 10, 18, 10);
                    g.DrawLine(pen, 18, 10, 18, 16);
                    g.DrawEllipse(pen, 3, 14, 4, 4);
                    g.DrawEllipse(pen, 13, 14, 4, 4);
                    break;
                case "reminders":
                    g.DrawArc(pen, 4, 3, 12, 12, 180, 180);
                    g.DrawLine(pen, 4, 11, 4, 15);
                    g.DrawLine(pen, 16, 11, 16, 15);
                    g.DrawLine(pen, 3, 15, 17, 15);
                    g.DrawEllipse(pen, 8, 16, 4, 3);
                    break;
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
                default:
                    g.DrawEllipse(pen, 4, 4, 12, 12);
                    break;
            }
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
    }
}