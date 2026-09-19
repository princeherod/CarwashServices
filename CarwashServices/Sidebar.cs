using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CarwashServices
{
    public class Sidebar : Panel
    {
        public event EventHandler<string>? ModuleSelected;

        private string _activeModule = "Manage Customers";

        public string ActiveModuleKey => _activeModule;

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

            Build();
        }

        public void SetActiveModule(string moduleKey)
        {
            _activeModule = moduleKey;
            Controls.Clear();
            Build();
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

                // Blue rounded square background
                using var bg = new SolidBrush(Color.FromArgb(0x1E, 0x88, 0xE5));
                using var path = RoundedRect(new Rectangle(0, 0, 38, 38), 10);
                g.FillPath(bg, path);

                // Paper plane glyph
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
            // ADMIN PILL
            // ============================================================
            var adminPill = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(92, 30),
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
                Text = "Admin",
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
            // OVERVIEW
            // ============================================================
            y = AddGroupLabel("OVERVIEW", y);
            y = AddItem("View Dashboard", "dashboard", y, "View Dashboard");
            y = AddItem("Analytics", "analytics", y, "Analytics");
            y = AddItem("View Reports", "reports", y, "View Reports");

            y += 22;

            // ============================================================
            // ADMIN MODULES
            // ============================================================
            y = AddGroupLabel("ADMIN MODULES", y);
            y = AddItem("Manage Users", "users", y, "Manage Users");
            y = AddItem("Manage Customers", "customers", y, "Manage Customers");
            y = AddItem("Manage Services", "services", y, "Manage Services");
            y = AddItem("Manage Service Requests", "requests", y, "Manage Service Requests");
            y = AddItem("Follow-Ups / Reminders", "reminders", y, "Follow-Ups / Reminders");
            y = AddItem("Manage Admin Accounts", "admin", y, "Manage Admin Accounts");

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
                Text = "LP",
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
                Text = "Lena Park",
                ForeColor = TextMain,
                Font = new Font("Segoe UI Semibold", 10f),
                Location = new Point(58, 16),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            card.Controls.Add(new Label
            {
                Text = "Admin",
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
            card.Controls.Add(signOutBtn);

            Controls.Add(card);
            card.BringToFront();
        }

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
        // MENU ITEM (with drawn icon)
        // ================================================================
        private int AddItem(string label, string iconKey, int y, string? key = null)
        {
            bool isActive = key != null && key == _activeModule;

            var btn = new Button
            {
                Text = "        " + label,                       // indent for the icon
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

            if (isActive)
            {
                var accent = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(4, 42),
                    BackColor = AccentBlue
                };
                btn.Controls.Add(accent);
            }

            // Draw icon on top of the button
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
            }

            Controls.Add(btn);
            return y + 46;
        }

        // ================================================================
        // ICON DRAWING (no emojis — pure GDI+)
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
                // ---- Dashboard: 4-square grid ----
                case "dashboard":
                    g.DrawRectangle(pen, 2, 2, 7, 7);
                    g.DrawRectangle(pen, 11, 2, 7, 7);
                    g.DrawRectangle(pen, 2, 11, 7, 7);
                    g.DrawRectangle(pen, 11, 11, 7, 7);
                    break;

                // ---- Analytics: bar chart ----
                case "analytics":
                    g.DrawLine(pen, 2, 18, 2, 3);   // y-axis
                    g.DrawLine(pen, 2, 18, 18, 18); // x-axis
                    g.FillRectangle(brush, 5, 11, 3, 7);
                    g.FillRectangle(brush, 10, 7, 3, 11);
                    g.FillRectangle(brush, 15, 4, 3, 14);
                    break;

                // ---- Reports: document with lines ----
                case "reports":
                    g.DrawRectangle(pen, 3, 2, 14, 17);
                    g.DrawLine(pen, 6, 7, 14, 7);
                    g.DrawLine(pen, 6, 11, 14, 11);
                    g.DrawLine(pen, 6, 15, 12, 15);
                    break;

                // ---- Users: person + key ----
                case "users":
                    g.DrawEllipse(pen, 2, 3, 6, 6);
                    g.DrawArc(pen, 0, 9, 10, 10, 180, 180);
                    g.DrawEllipse(pen, 13, 9, 5, 5);
                    g.DrawLine(pen, 15, 14, 15, 19);
                    g.DrawLine(pen, 15, 17, 18, 17);
                    break;

                // ---- Customers: group of people ----
                case "customers":
                    g.DrawEllipse(pen, 3, 2, 5, 5);
                    g.DrawArc(pen, 1, 8, 9, 9, 180, 180);
                    g.DrawEllipse(pen, 12, 3, 5, 5);
                    g.DrawArc(pen, 10, 9, 9, 9, 180, 180);
                    break;

                // ---- Services: wrench ----
                case "services":
                    g.DrawArc(pen, 1, 1, 10, 10, 30, 300);
                    g.DrawLine(pen, 9, 9, 17, 17);
                    g.DrawEllipse(pen, 15, 15, 4, 4);
                    break;

                // ---- Service Requests: truck ----
                case "requests":
                    g.DrawRectangle(pen, 1, 8, 11, 8);
                    g.DrawLine(pen, 12, 10, 18, 10);
                    g.DrawLine(pen, 18, 10, 18, 16);
                    g.DrawEllipse(pen, 3, 14, 4, 4);
                    g.DrawEllipse(pen, 13, 14, 4, 4);
                    break;

                // ---- Follow-Ups: bell ----
                case "reminders":
                    g.DrawArc(pen, 4, 3, 12, 12, 180, 180);
                    g.DrawLine(pen, 4, 11, 4, 15);
                    g.DrawLine(pen, 16, 11, 16, 15);
                    g.DrawLine(pen, 3, 15, 17, 15);
                    g.DrawEllipse(pen, 8, 16, 4, 3);
                    break;

                // ---- Admin: shield ----
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

        // ================================================================
        // ROUNDED RECTANGLE HELPER
        // ================================================================
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