using System;
using System.Drawing;
using System.Windows.Forms;
using CarwashServices.Auth;

namespace CarwashServices.Dialogs
{
    public class SyncConfirmDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color BorderSoft = Color.FromArgb(0xD1, 0xD9, 0xE6);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color BlueHover = Color.FromArgb(0x1E, 0x40, 0xAF);

        private readonly string _target;
        private readonly string _sourceDb;
        private readonly string _destDb;
        private readonly string _server;

        public SyncConfirmDialog(string target)
        {
            _target = target;
            (_sourceDb, _destDb, _server) = target switch
            {
                "AquaShine" => ("aquaShine_db", "db70860", "MonsterASP (db70860.databaseasp.net)"),
                "SparkleRide" => ("sparkleRide_db", "db70861", "MonsterASP (db70861.databaseasp.net)"),
                "CleanRide" => ("cleanRide_db", "db70862", "MonsterASP (db70862.databaseasp.net)"),
                "MasterERP" => ("CarwashMasterERP", "db67193", "MonsterASP (db67193.databaseasp.net)"),
                _ => ("All Local Databases (4)", "All Cloud Databases (4)", "MonsterASP Cloud SQL Servers")
            };

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = $"Cloud Synchronization - {_target}";
            Size = new Size(580, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            // Header
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Navy
            };
            var titleLbl = new Label
            {
                Text = "SYNCHRONIZE LOCAL DATA TO MONSTERASP?",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(24, 14),
                AutoSize = true
            };
            var subLbl = new Label
            {
                Text = "Safe Record-Level Synchronization • Local → Cloud",
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(24, 38),
                AutoSize = true
            };
            header.Controls.AddRange(new Control[] { titleLbl, subLbl });

            // Body
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 16, 24, 16),
                AutoScroll = true
            };

            int y = 14;

            // Mapping card
            var mapCard = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(514, 100),
                BackColor = Color.White
            };
            mapCard.Paint += (s, e) =>
            {
                using var p = new Pen(BorderSoft, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, mapCard.Width - 1, mapCard.Height - 1);
            };

            var srcTitle = new Label
            {
                Text = "SOURCE DATABASE:",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(16, 12),
                AutoSize = true
            };
            var srcVal = new Label
            {
                Text = _sourceDb,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11f),
                Location = new Point(16, 28),
                AutoSize = true
            };

            var arrow = new Label
            {
                Text = "➜",
                ForeColor = Blue,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Location = new Point(220, 26),
                AutoSize = true
            };

            var dstTitle = new Label
            {
                Text = "DESTINATION DATABASE:",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(270, 12),
                AutoSize = true
            };
            var dstVal = new Label
            {
                Text = _destDb,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11f),
                Location = new Point(270, 28),
                AutoSize = true
            };
            var srvVal = new Label
            {
                Text = $"Server: {_server}",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(16, 68),
                AutoSize = true
            };

            mapCard.Controls.AddRange(new Control[] { srcTitle, srcVal, arrow, dstTitle, dstVal, srvVal });
            body.Controls.Add(mapCard);
            y += 114;

            // Explanation box
            var infoBox = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(514, 160),
                BackColor = Color.FromArgb(0xF0, 0xF7, 0xFF)
            };
            infoBox.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(0xBF, 0xDB, 0xFE), 1f);
                e.Graphics.DrawRectangle(p, 0, 0, infoBox.Width - 1, infoBox.Height - 1);
            };

            var infoTitle = new Label
            {
                Text = "SAFETY & ISOLATION GUARANTEES",
                ForeColor = Blue,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Location = new Point(16, 12),
                AutoSize = true
            };

            var explanation = new Label
            {
                Text = "• Record-Level Sync: If a local record does not exist in the cloud, it will be INSERTED.\n" +
                       "• Changed Records: If a local record exists in the cloud and has changed, it will be UPDATED.\n" +
                       "• Identical Records: If a local record is unchanged, it will be SKIPPED.\n" +
                       "• Cloud Deletion Protected: Records existing only in the cloud will NEVER be deleted.\n" +
                       "• Tenant & Branch Isolation: Strict 1-to-1 database mapping; CompanyId and BranchId are preserved.",
                ForeColor = Navy,
                Font = new Font("Segoe UI", 8.8f),
                Location = new Point(16, 36),
                Size = new Size(480, 110)
            };

            infoBox.Controls.AddRange(new Control[] { infoTitle, explanation });
            body.Controls.Add(infoBox);
            y += 174;

            var nextStepLbl = new Label
            {
                Text = "Clicking 'Preview Sync' will inspect local and cloud records without making any modifications.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(24, y),
                Size = new Size(514, 30)
            };
            body.Controls.Add(nextStepLbl);

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(0xEA, 0xF0, 0xF8)
            };

            var cancelBtn = new Button
            {
                Text = "Cancel",
                Size = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Cursor = Cursors.Hand
            };
            cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var syncBtn = new Button
            {
                Text = "Preview Sync",
                Size = new Size(130, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            syncBtn.FlatAppearance.BorderSize = 0;
            syncBtn.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            void PlaceButtons()
            {
                syncBtn.Location = new Point(footer.ClientSize.Width - 24 - syncBtn.Width, 12);
                cancelBtn.Location = new Point(syncBtn.Left - 12 - cancelBtn.Width, 12);
            }
            footer.Resize += (s, e) => PlaceButtons();
            PlaceButtons();

            footer.Controls.AddRange(new Control[] { cancelBtn, syncBtn });

            CancelButton = cancelBtn;
            AcceptButton = syncBtn;

            Controls.Add(header);
            Controls.Add(footer);
            Controls.Add(body);
            body.BringToFront();
        }
    }
}
