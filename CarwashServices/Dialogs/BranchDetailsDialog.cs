using System;
using System.Drawing;
using System.Windows.Forms;
using CarwashServices.Auth;
using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class BranchDetailsDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color CardBg = Color.White;
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color BlueSoft = Color.FromArgb(0xDB, 0xEA, 0xFE);
        private static readonly Color SlateMuted = Color.FromArgb(0x64, 0x74, 0x8B);

        private readonly BranchDto _b;

        public BranchDetailsDialog(BranchDto branch)
        {
            _b = branch;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = $"Branch Details - {_b.BranchName}";
            Size = new Size(520, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Navy
            };
            var titleLbl = new Label
            {
                Text = _b.BranchName.ToUpperInvariant(),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13f),
                Location = new Point(24, 14),
                AutoSize = true
            };
            var subLbl = new Label
            {
                Text = $"Company: {SessionUser.CompanyName}  •  Code: {_b.BranchCode}",
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(24, 40),
                AutoSize = true
            };
            header.Controls.AddRange(new Control[] { titleLbl, subLbl });
            Controls.Add(header);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 16, 24, 16),
                AutoScroll = true
            };

            int y = 14;

            // Stats Cards
            var statsRow = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(456, 76),
                BackColor = Color.Transparent
            };

            var reqCard = CreateStatCard("SERVICE REQUESTS", _b.ServiceRequestsCount.ToString("N0"), 0, 0, 220);
            var custCard = CreateStatCard("CUSTOMERS", _b.CustomersCount.ToString("N0"), 236, 0, 220);
            statsRow.Controls.AddRange(new Control[] { reqCard, custCard });
            body.Controls.Add(statsRow);
            y += 92;

            // Details card
            var detailsCard = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(456, 294),
                BackColor = CardBg
            };
            detailsCard.Paint += (s, e) =>
            {
                using var p = new Pen(CardBorder);
                e.Graphics.DrawRectangle(p, 0, 0, detailsCard.Width - 1, detailsCard.Height - 1);
            };

            int dy = 16;
            AddDetailRow(detailsCard, "Branch Status", _b.IsArchived ? "Archived" : (_b.IsActive ? "Active" : "Inactive"), dy);
            dy += 34;
            AddDetailRow(detailsCard, "Branch Type", _b.IsMainBranch ? "Primary / Main Branch" : "Secondary Branch", dy);
            dy += 34;
            string adminText = !string.IsNullOrWhiteSpace(_b.AssignedAdminName)
                ? (!string.IsNullOrWhiteSpace(_b.AssignedAdminEmail) ? $"{_b.AssignedAdminName} ({_b.AssignedAdminEmail})" : _b.AssignedAdminName)
                : "Unassigned";
            AddDetailRow(detailsCard, "Branch Admin", adminText, dy);
            dy += 34;
            AddDetailRow(detailsCard, "City / Municipality", string.IsNullOrWhiteSpace(_b.City) ? "—" : _b.City, dy);
            dy += 34;
            AddDetailRow(detailsCard, "Street Address", string.IsNullOrWhiteSpace(_b.Address) ? "—" : _b.Address, dy);
            dy += 34;
            AddDetailRow(detailsCard, "Contact Number", string.IsNullOrWhiteSpace(_b.ContactNumber) ? "—" : _b.ContactNumber, dy);
            dy += 34;
            AddDetailRow(detailsCard, "Email Address", string.IsNullOrWhiteSpace(_b.Email) ? "—" : _b.Email, dy);
            dy += 34;
            AddDetailRow(detailsCard, "Created Date", _b.CreatedAt.ToString("MMM dd, yyyy"), dy);

            body.Controls.Add(detailsCard);
            Controls.Add(body);

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(0xEA, 0xF0, 0xF8)
            };

            var switchBtn = new Button
            {
                Text = "Set Active Branch",
                Size = new Size(130, 36),
                Location = new Point(24, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand,
                Enabled = !SessionUser.IsSingleBranchUser
            };
            switchBtn.FlatAppearance.BorderSize = 0;
            switchBtn.Click += (s, e) =>
            {
                SessionUser.SetBranch(_b.BranchId, _b.BranchName);
                MessageBox.Show($"Active branch set to '{_b.BranchName}'. Operational views will now filter to this branch.", "Active Branch Changed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            };

            var assignBtn = new Button
            {
                Text = "Assign Admin",
                Size = new Size(120, 36),
                Location = new Point(164, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand
            };
            assignBtn.FlatAppearance.BorderColor = CardBorder;
            assignBtn.Click += (s, e) =>
            {
                using var dlg = new AssignBranchAdminDialog(_b);
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            var closeBtn = new Button
            {
                Text = "Close",
                Size = new Size(90, 36),
                Location = new Point(Width - 124, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderColor = CardBorder;
            closeBtn.Click += (s, e) => Close();

            footer.Controls.AddRange(new Control[] { switchBtn, assignBtn, closeBtn });
            Controls.Add(footer);
        }

        private static Panel CreateStatCard(string caption, string val, int x, int y, int w)
        {
            var p = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 76),
                BackColor = CardBg
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };

            var cap = new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(14, 10),
                AutoSize = true
            };
            var v = new Label
            {
                Text = val,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 18f),
                Location = new Point(12, 28),
                AutoSize = true
            };
            p.Controls.AddRange(new Control[] { cap, v });
            return p;
        }

        private static void AddDetailRow(Panel parent, string label, string val, int y)
        {
            var lbl = new Label
            {
                Text = label,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(16, y),
                Size = new Size(140, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var valLbl = new Label
            {
                Text = val,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Location = new Point(160, y),
                Size = new Size(280, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            parent.Controls.AddRange(new Control[] { lbl, valLbl });
        }
    }
}
