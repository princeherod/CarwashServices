using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    public class BusinessViewDialog : Form
    {
        // Palette
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color CardBg = Color.FromArgb(0xF8, 0xFA, 0xFD);

        private readonly CompanyListItemDto _company;

        public bool WasEdited { get; private set; }

        public BusinessViewDialog(CompanyListItemDto company)
        {
            _company = company ?? throw new ArgumentNullException(nameof(company));
            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InitializeForm()
        {
            Text = $"Business Profile — {_company.CompanyName}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 680);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 12)
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = _company.CompanyName,
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Navy,
                Location = new Point(32, 14),
                AutoSize = true
            };
            header.Controls.Add(titleLbl);

            var codeSubtitleLbl = new Label
            {
                Text = $"Company ID: {_company.CompanyId}   •   Code: {_company.CompanyCode}   •   Registered: {_company.CreatedAt:yyyy-MM-dd}",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Muted,
                Location = new Point(32, 46),
                AutoSize = true
            };
            header.Controls.Add(codeSubtitleLbl);

            // Status Pill in header
            var statusPill = new Label
            {
                Text = _company.IsActive ? "● Active" : "● Inactive",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = _company.IsActive ? Green : Red,
                BackColor = _company.IsActive ? GreenSoft : RedSoft,
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true,
                Location = new Point(ClientSize.Width - 110, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            void RoundPill()
            {
                using var p = RoundedRect(new Rectangle(0, 0, statusPill.Width, statusPill.Height), statusPill.Height / 2);
                statusPill.Region = new Region(p);
            }
            statusPill.SizeChanged += (s, e) => RoundPill();
            RoundPill();
            header.Controls.Add(statusPill);

            Controls.Add(header);

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Color.FromArgb(0xFA, 0xFB, 0xFD)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            var closeBtn = new Button
            {
                Text = "Close",
                Size = new Size(95, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Location = new Point(footer.ClientSize.Width - 32 - 95, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeBtn.FlatAppearance.BorderColor = BorderSoft;
            closeBtn.Click += (s, e) => { DialogResult = WasEdited ? DialogResult.OK : DialogResult.Cancel; Close(); };
            footer.Controls.Add(closeBtn);

            var editBtn = new Button
            {
                Text = "Edit Business",
                Size = new Size(125, 38),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Accent,
                Cursor = Cursors.Hand,
                Location = new Point(closeBtn.Left - 12 - 125, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            editBtn.FlatAppearance.BorderSize = 0;
            editBtn.Click += (s, e) =>
            {
                using var dlg = new BusinessEditDialog(_company);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    WasEdited = true;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            footer.Controls.Add(editBtn);

            void RoundFooterBtn(Button btn)
            {
                using var p = RoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 6);
                btn.Region = new Region(p);
            }
            RoundFooterBtn(closeBtn);
            RoundFooterBtn(editBtn);

            Controls.Add(footer);

            // ---- Body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(32, 20, 32, 20)
            };

            int y = 16;
            int cardW = 576;

            // Card 1: Contact Information
            body.Controls.Add(CreateSectionCard("CONTACT INFORMATION", new[]
            {
                ("Contact Phone", string.IsNullOrWhiteSpace(_company.ContactPhone) ? "Not specified" : _company.ContactPhone),
                ("Contact Email", string.IsNullOrWhiteSpace(_company.ContactEmail) ? "Not specified" : _company.ContactEmail)
            }, 32, ref y, cardW));

            y += 16;

            // Card 2: Physical Address & Location
            body.Controls.Add(CreateSectionCard("LOCATION & ADDRESS", new[]
            {
                ("Address Line", string.IsNullOrWhiteSpace(_company.AddressLine) ? "Not specified" : _company.AddressLine),
                ("City", string.IsNullOrWhiteSpace(_company.City) ? "Not specified" : _company.City),
                ("Province / State", string.IsNullOrWhiteSpace(_company.Province) ? (string.IsNullOrWhiteSpace(_company.State) ? "Not specified" : _company.State) : _company.Province),
                ("Postal Code", string.IsNullOrWhiteSpace(_company.PostalCode) ? "Not specified" : _company.PostalCode),
                ("Country", string.IsNullOrWhiteSpace(_company.Country) ? "Not specified" : _company.Country)
            }, 32, ref y, cardW));

            y += 16;

            // Card 3: Associated Administrator
            body.Controls.Add(CreateSectionCard("ASSOCIATED ADMINISTRATOR", new[]
            {
                ("Administrator Name", string.IsNullOrWhiteSpace(_company.AdminUser) ? "Unassigned" : _company.AdminUser),
                ("Email Address", string.IsNullOrWhiteSpace(_company.AdminEmail) ? "—" : _company.AdminEmail),
                ("Assigned Role", "Administrator (Tenant Admin)")
            }, 32, ref y, cardW));

            y += 16;

            // Card 4: Tenant Database Configuration
            body.Controls.Add(CreateSectionCard("TENANT DATABASE CONFIGURATION", new[]
            {
                ("Database Server", string.IsNullOrWhiteSpace(_company.DatabaseServer) ? "(localdb)\\MSSQLLocalDB" : _company.DatabaseServer),
                ("Tenant Database Name", string.IsNullOrWhiteSpace(_company.DatabaseName) ? $"Tenant_{_company.CompanyCode}" : _company.DatabaseName),
                ("Multi-Tenancy Mode", "Database-per-tenant (Isolated Storage)")
            }, 32, ref y, cardW));

            Controls.Add(body);
            body.BringToFront();
        }

        private static Panel CreateSectionCard(string sectionTitle, (string Label, string Value)[] rows, int x, ref int y, int w)
        {
            int rowH = 26;
            int headerH = 34;
            int totalH = headerH + (rows.Length * rowH) + 12;

            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, totalH),
                BackColor = CardBg
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                using var p = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
                e.Graphics.DrawPath(pen, p);
            };

            var secHeader = new Label
            {
                Text = sectionTitle,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Muted,
                Location = new Point(16, 10),
                AutoSize = true
            };
            card.Controls.Add(secHeader);

            int ry = headerH;
            foreach (var (lbl, val) in rows)
            {
                var labelCtrl = new Label
                {
                    Text = lbl,
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Muted,
                    Location = new Point(16, ry),
                    Size = new Size(160, 20),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                card.Controls.Add(labelCtrl);

                var valCtrl = new Label
                {
                    Text = val,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    ForeColor = Navy,
                    Location = new Point(180, ry),
                    Size = new Size(w - 196, 20),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                card.Controls.Add(valCtrl);

                ry += rowH;
            }

            y += totalH;
            return card;
        }
    }
}
