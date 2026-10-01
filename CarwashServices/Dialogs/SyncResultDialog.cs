using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class SyncResultDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color BorderSoft = Color.FromArgb(0xD1, 0xD9, 0xE6);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color Green = Color.FromArgb(0x16, 0xA3, 0x4A);
        private static readonly Color Red = Color.FromArgb(0xDC, 0x26, 0x26);

        private readonly MultiSyncResultDto _result;
        private readonly string _target;

        public SyncResultDialog(string target, MultiSyncResultDto result)
        {
            _target = target;
            _result = result;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = $"Sync Results - {_target}";
            Size = new Size(840, 680);
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
                Text = _result.Success
                    ? $"SYNCHRONIZATION COMPLETE — {_target.ToUpperInvariant()}"
                    : $"SYNCHRONIZATION COMPLETED WITH ISSUES — {_target.ToUpperInvariant()}",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(24, 14),
                AutoSize = true
            };
            var subLbl = new Label
            {
                Text = $"Duration: {_result.TotalDuration} • Local → MonsterASP Cloud Sync Execution",
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

            // Summary totals
            int totalInserted = _result.Databases.Sum(d => d.TotalInserted);
            int totalUpdated = _result.Databases.Sum(d => d.TotalUpdated);
            int totalSkipped = _result.Databases.Sum(d => d.TotalSkipped);
            int totalFailed = _result.Databases.Sum(d => d.TotalFailed);

            var statPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(774, 76),
                BackColor = Color.White
            };
            statPanel.Paint += (s, e) =>
            {
                using var p = new Pen(BorderSoft, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, statPanel.Width - 1, statPanel.Height - 1);
            };

            int colW = 185;
            statPanel.Controls.Add(CreateStatBox("TOTAL INSERTED", totalInserted.ToString("N0"), Green, 16, 12, colW));
            statPanel.Controls.Add(CreateStatBox("TOTAL UPDATED", totalUpdated.ToString("N0"), Blue, 16 + colW, 12, colW));
            statPanel.Controls.Add(CreateStatBox("TOTAL SKIPPED", totalSkipped.ToString("N0"), Muted, 16 + colW * 2, 12, colW));
            statPanel.Controls.Add(CreateStatBox("TOTAL FAILED", totalFailed.ToString("N0"), totalFailed > 0 ? Red : Green, 16 + colW * 3, 12, colW));

            body.Controls.Add(statPanel);
            y += 88;

            // Database Overview Cards
            var dbCardsPanel = new FlowLayoutPanel
            {
                Location = new Point(24, y),
                Size = new Size(774, 56),
                BackColor = Color.Transparent,
                WrapContents = false,
                AutoScroll = false
            };

            foreach (var db in _result.Databases)
            {
                var card = new Panel
                {
                    Size = new Size(185, 48),
                    BackColor = db.Success ? Color.FromArgb(0xF0, 0xFD, 0xF4) : Color.FromArgb(0xFE, 0xF2, 0xF2),
                    Margin = new Padding(0, 0, 8, 0)
                };
                card.Paint += (s, e) =>
                {
                    using var p = new Pen(db.Success ? Color.FromArgb(0x86, 0xEF, 0xAC) : Color.FromArgb(0xFC, 0xA5, 0xA5), 1f);
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                };

                var statusIcon = new Label
                {
                    Text = db.Success ? "✅" : "❌",
                    Location = new Point(8, 12),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 11f)
                };
                var nameLbl = new Label
                {
                    Text = db.Target,
                    ForeColor = Navy,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Location = new Point(34, 6),
                    AutoSize = true
                };
                var detailLbl = new Label
                {
                    Text = db.Success ? $"{db.TotalInserted} ins, {db.TotalUpdated} upd ({db.Duration})" : "Failed",
                    ForeColor = db.Success ? Muted : Red,
                    Font = new Font("Segoe UI", 7.8f),
                    Location = new Point(34, 24),
                    AutoSize = true
                };
                card.Controls.AddRange(new Control[] { statusIcon, nameLbl, detailLbl });
                dbCardsPanel.Controls.Add(card);
            }

            body.Controls.Add(dbCardsPanel);
            y += 64;

            // DataGridView for Table breakdown
            var grid = new DataGridView
            {
                Location = new Point(24, y),
                Size = new Size(774, 300),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(0xF1, 0xF5, 0xF9),
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9f),
                Padding = new Padding(8, 0, 8, 0),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 30;
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9f),
                ForeColor = Navy,
                SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                SelectionForeColor = Navy,
                Padding = new Padding(8, 0, 8, 0)
            };

            grid.Columns.Add("Database", "Database Mapping");
            grid.Columns.Add("Table", "Table Name");
            grid.Columns.Add("Inserted", "Inserted");
            grid.Columns.Add("Updated", "Updated");
            grid.Columns.Add("Skipped", "Skipped");
            grid.Columns.Add("Failed", "Failed");
            grid.Columns.Add("Status", "Status");

            grid.Columns[0].FillWeight = 22;
            grid.Columns[1].FillWeight = 22;
            grid.Columns[2].FillWeight = 12;
            grid.Columns[3].FillWeight = 12;
            grid.Columns[4].FillWeight = 12;
            grid.Columns[5].FillWeight = 10;
            grid.Columns[6].FillWeight = 10;

            foreach (var db in _result.Databases)
            {
                string mapping = $"{db.SourceDatabase} ➜ {db.DestinationDatabase}";
                if (!db.Success && db.Tables.Count == 0)
                {
                    grid.Rows.Add(mapping, "ALL TABLES", "0", "0", "0", "1", "FAILED: " + (db.ErrorMessage ?? "Error"));
                    continue;
                }

                foreach (var t in db.Tables)
                {
                    grid.Rows.Add(
                        mapping,
                        t.TableName,
                        t.Inserted > 0 ? $"+{t.Inserted}" : "0",
                        t.Updated > 0 ? $"~{t.Updated}" : "0",
                        t.Skipped.ToString(),
                        t.Failed > 0 ? t.Failed.ToString() : "0",
                        t.Failed > 0 ? "Error" : "Synced");
                }
            }

            body.Controls.Add(grid);
            y += 310;

            // Error alert box if any error
            var failedDbs = _result.Databases.Where(d => !d.Success).ToList();
            if (failedDbs.Count > 0)
            {
                var errBox = new Panel
                {
                    Location = new Point(24, y),
                    Size = new Size(774, 50),
                    BackColor = Color.FromArgb(0xFE, 0xF2, 0xF2)
                };
                errBox.Paint += (s, e) =>
                {
                    using var p = new Pen(Color.FromArgb(0xF8, 0x71, 0x71), 1f);
                    e.Graphics.DrawRectangle(p, 0, 0, errBox.Width - 1, errBox.Height - 1);
                };

                var errText = new Label
                {
                    Text = "Errors occurred: " + string.Join("; ", failedDbs.Select(d => $"{d.Target}: {d.ErrorMessage}")),
                    ForeColor = Red,
                    Font = new Font("Segoe UI Semibold", 8.8f),
                    Location = new Point(12, 10),
                    Size = new Size(750, 30)
                };
                errBox.Controls.Add(errText);
                body.Controls.Add(errBox);
                y += 56;
            }

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(0xEA, 0xF0, 0xF8)
            };

            var closeBtn = new Button
            {
                Text = "Close",
                Size = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => Close();

            void PlaceButtons()
            {
                closeBtn.Location = new Point(footer.ClientSize.Width - 24 - closeBtn.Width, 12);
            }
            footer.Resize += (s, e) => PlaceButtons();
            PlaceButtons();

            footer.Controls.Add(closeBtn);
            CancelButton = closeBtn;
            AcceptButton = closeBtn;

            Controls.Add(header);
            Controls.Add(footer);
            Controls.Add(body);
            body.BringToFront();
        }

        private static Panel CreateStatBox(string caption, string value, Color valColor, int x, int y, int w)
        {
            var p = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 52),
                BackColor = Color.Transparent
            };

            var cap = new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 7.8f),
                Location = new Point(0, 4),
                AutoSize = true
            };

            var val = new Label
            {
                Text = value,
                ForeColor = valColor,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(0, 22),
                AutoSize = true
            };

            p.Controls.AddRange(new Control[] { cap, val });
            return p;
        }
    }
}
