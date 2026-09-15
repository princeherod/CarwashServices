using System;
using System.Drawing;
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

        public Sidebar()
        {
            Dock = DockStyle.Left;
            Width = 280;
            BackColor = NavyBg;
            Padding = new Padding(0);

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

            // ---- Brand ----
            var brandTitle = new Label
            {
                Text = "CARWASH SERVICES",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(24, y),
                AutoSize = true
            };
            Controls.Add(brandTitle);

            var brandSubtitle = new Label
            {
                Text = "CRM System",
                ForeColor = TextDim,
                Font = new Font("Segoe UI Semibold", 11f),
                Location = new Point(24, y + 20),
                AutoSize = true
            };
            Controls.Add(brandSubtitle);

            y += 76;

            // ---- Admin pill ----
            var adminPill = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(84, 28),
                BackColor = Color.FromArgb(0x1A, 0x2A, 0x48)
            };
            var adminDot = new Label
            {
                Text = "●",
                ForeColor = AccentBlue,
                Font = new Font("Segoe UI", 8f),
                Location = new Point(10, 6),
                AutoSize = true
            };
            adminPill.Controls.Add(adminDot);

            var adminLabel = new Label
            {
                Text = "Admin",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(26, 5),
                AutoSize = true
            };
            adminPill.Controls.Add(adminLabel);
            Controls.Add(adminPill);

            y += 56;

            // ---- OVERVIEW ----
            y = AddGroupLabel("OVERVIEW", y);
            y = AddItem("View Dashboard", y, key: "View Dashboard");
            y = AddItem("View Reports", y, key: "View Reports");

            y += 24;

            // ---- ADMIN MODULES ----
            y = AddGroupLabel("ADMIN MODULES", y);
            y = AddItem("Manage Users", y, key: "Manage Users");
            y = AddItem("Manage Customers", y, key: "Manage Customers");
            y = AddItem("Manage Services", y, key: "Manage Services");
            y = AddItem("Manage Service Requests", y, key: "Manage Service Requests");
            y = AddItem("Follow-Ups / Reminders", y, key: "Follow-Ups / Reminders");
            y = AddItem("Manage Admin Accounts", y, key: "Manage Admin Accounts");

            // ---- Bottom user card ----
            var card = new Panel
            {
                Location = new Point(20, Height - 130),
                Size = new Size(240, 110),
                BackColor = Color.FromArgb(0x0F, 0x1A, 0x2E),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };

            var avatar = new Panel
            {
                Location = new Point(12, 12),
                Size = new Size(36, 36),
                BackColor = Color.FromArgb(0x1E, 0x88, 0xE5)
            };
            var avatarLbl = new Label
            {
                Text = "LP",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            avatar.Controls.Add(avatarLbl);
            card.Controls.Add(avatar);

            card.Controls.Add(new Label
            {
                Text = "Lena Park",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10f),
                Location = new Point(58, 16),
                AutoSize = true
            });

            card.Controls.Add(new Label
            {
                Text = "Admin",
                ForeColor = TextDim,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(58, 34),
                AutoSize = true
            });

            var signOutBtn = new Button
            {
                Text = "Sign Out",
                ForeColor = Color.White,
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

        private int AddGroupLabel(string text, int y)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(28, y),
                AutoSize = true
            };
            Controls.Add(lbl);
            return y + 28;     // a bit more space before items
        }

        private int AddItem(string label, int y, string? key = null)
        {
            bool isActive = key != null && key == _activeModule;

            var btn = new Button
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.White,
                BackColor = isActive ? NavyActive : NavyBg,
                Width = 240,
                Height = 42,           // slightly taller
                Left = 20,
                Top = y,
                Cursor = Cursors.Hand,
                Padding = new Padding(20, 0, 0, 0)   // more indent
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = isActive ? NavyActive : NavyHover;

            if (isActive)
            {
                var accent = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(4, 42),            // thicker accent
                    BackColor = AccentBlue
                };
                btn.Controls.Add(accent);
            }

            if (key != null)
            {
                var capturedKey = key;
                btn.Click += (s, e) => ModuleSelected?.Invoke(this, capturedKey);
            }

            Controls.Add(btn);
            return y + 46;      // more breathing room between items
        }
    }
}