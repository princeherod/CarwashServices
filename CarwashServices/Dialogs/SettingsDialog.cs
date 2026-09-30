using System;
using System.Drawing;
using System.Windows.Forms;
using CarwashServices.Auth;

namespace CarwashServices.Dialogs
{
    public class SettingsDialog : Form
    {
        public SettingsDialog()
        {
            Text = "Settings & Profile";
            Size = new Size(520, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC);
            Font = new Font("Segoe UI", 9.5f);

            InitializeUI();
        }

        private void InitializeUI()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(0x07, 0x12, 0x26),
                Padding = new Padding(24, 0, 24, 0)
            };
            var titleLbl = new Label
            {
                Text = "System & User Settings",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13f),
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            header.Controls.Add(titleLbl);
            Controls.Add(header);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 16, 24, 16)
            };
            Controls.Add(body);

            int y = 16;
            void AddRow(string label, string val, Color? valColor = null)
            {
                var lbl = new Label
                {
                    Text = label,
                    ForeColor = Color.FromArgb(0x64, 0x74, 0x8B),
                    Font = new Font("Segoe UI Semibold", 9f),
                    Location = new Point(24, y),
                    Size = new Size(160, 24)
                };
                body.Controls.Add(lbl);

                var txt = new Label
                {
                    Text = val,
                    ForeColor = valColor ?? Color.FromArgb(0x0F, 0x17, 0x2A),
                    Font = new Font("Segoe UI", 9.5f),
                    Location = new Point(190, y),
                    Size = new Size(280, 24)
                };
                body.Controls.Add(txt);
                y += 36;
            }

            AddRow("Full Name", string.IsNullOrWhiteSpace(SessionUser.FullName) ? "N/A" : SessionUser.FullName);
            AddRow("Email Address", string.IsNullOrWhiteSpace(SessionUser.Email) ? "N/A" : SessionUser.Email);
            AddRow("User Role", SessionUser.Role.ToString());
            AddRow("Organization", $"{SessionUser.CompanyName} ({SessionUser.CompanyCode})");
            AddRow("Active Branch", string.IsNullOrWhiteSpace(SessionUser.CurrentBranchName) ? "All Branches" : SessionUser.CurrentBranchName);
            AddRow("API Endpoint", "http://localhost:5180/", Color.FromArgb(0x25, 0x63, 0xEB));
            AddRow("Connection Status", "● Connected & Active", Color.FromArgb(0x16, 0xA3, 0x4A));
            AddRow("Application Suite", "Carwash CRM v2.0 (WinForms)");

            var closeBtn = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(110, 36),
                Location = new Point(360, y + 16),
                BackColor = Color.FromArgb(0x1D, 0x4E, 0xD8),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            body.Controls.Add(closeBtn);
        }
    }
}
