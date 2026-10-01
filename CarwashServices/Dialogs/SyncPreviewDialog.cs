using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class SyncPreviewDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color BorderSoft = Color.FromArgb(0xD1, 0xD9, 0xE6);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color Green = Color.FromArgb(0x16, 0xA3, 0x4A);
        private static readonly Color Amber = Color.FromArgb(0xD9, 0x77, 0x06);

        private readonly List<SyncDatabasePreviewDto> _previews;
        private readonly string _target;

        public SyncPreviewDialog(string target, List<SyncDatabasePreviewDto> previews)
        {
            _target = target;
            _previews = previews;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = $"Sync Preview - {_target}";
            Size = new Size(820, 640);
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
                Text = $"SYNCHRONIZATION PREVIEW — {_target.ToUpperInvariant()}",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(24, 14),
                AutoSize = true
            };
            var subLbl = new Label
            {
                Text = "Review pending record changes before committing to MonsterASP cloud databases",
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

            // Summary statistics card
            int totalNew = _previews.Sum(p => p.TotalNew);
            int totalUpdated = _previews.Sum(p => p.TotalUpdated);
            int totalUnchanged = _previews.Sum(p => p.TotalUnchanged);
            int totalCloudOnly = _previews.Sum(p => p.TotalCloudOnly);

            var summaryPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(754, 76),
                BackColor = Color.White
            };
            summaryPanel.Paint += (s, e) =>
            {
                using var p = new Pen(BorderSoft, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, summaryPanel.Width - 1, summaryPanel.Height - 1);
            };

            int colW = 180;
            summaryPanel.Controls.Add(CreateStatBox("NEW TO INSERT", totalNew.ToString("N0"), Green, 16, 12, colW));
            summaryPanel.Controls.Add(CreateStatBox("CHANGED TO UPDATE", totalUpdated.ToString("N0"), Blue, 16 + colW, 12, colW));
            summaryPanel.Controls.Add(CreateStatBox("UNCHANGED (SKIP)", totalUnchanged.ToString("N0"), Muted, 16 + colW * 2, 12, colW));
            summaryPanel.Controls.Add(CreateStatBox("CLOUD-ONLY (KEEP)", totalCloudOnly.ToString("N0"), Amber, 16 + colW * 3, 12, colW));

            body.Controls.Add(summaryPanel);
            y += 88;

            // Details tab or list per database
            var grid = new DataGridView
            {
                Location = new Point(24, y),
                Size = new Size(754, 360),
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
            grid.RowTemplate.Height = 32;
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
            grid.Columns.Add("New", "New (Insert)");
            grid.Columns.Add("Updated", "Changed (Update)");
            grid.Columns.Add("Unchanged", "Unchanged (Skip)");
            grid.Columns.Add("CloudOnly", "Cloud-Only (Preserved)");

            grid.Columns[0].FillWeight = 24;
            grid.Columns[1].FillWeight = 22;
            grid.Columns[2].FillWeight = 13;
            grid.Columns[3].FillWeight = 13;
            grid.Columns[4].FillWeight = 14;
            grid.Columns[5].FillWeight = 14;

            foreach (var p in _previews)
            {
                string mapping = $"{p.SourceDatabase} ➜ {p.DestinationDatabase}";
                if (!p.IsCloudOnline)
                {
                    grid.Rows.Add(mapping, "OFFLINE", "—", "—", "—", p.ConnectionError ?? "Unreachable");
                    continue;
                }

                foreach (var t in p.Tables)
                {
                    grid.Rows.Add(
                        mapping,
                        t.TableName,
                        t.NewCount > 0 ? $"+{t.NewCount}" : "0",
                        t.UpdatedCount > 0 ? $"~{t.UpdatedCount}" : "0",
                        t.UnchangedCount.ToString(),
                        t.CloudOnlyCount.ToString());
                }
            }

            body.Controls.Add(grid);
            y += 370;

            var safetyLbl = new Label
            {
                Text = "Note: Cloud-only records will remain untouched in MonsterASP. No cloud data will be deleted during this operation.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(24, y),
                Size = new Size(754, 24)
            };
            body.Controls.Add(safetyLbl);

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

            var proceedBtn = new Button
            {
                Text = "Proceed with Sync",
                Size = new Size(150, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            proceedBtn.FlatAppearance.BorderSize = 0;
            proceedBtn.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            void PlaceButtons()
            {
                proceedBtn.Location = new Point(footer.ClientSize.Width - 24 - proceedBtn.Width, 12);
                cancelBtn.Location = new Point(proceedBtn.Left - 12 - cancelBtn.Width, 12);
            }
            footer.Resize += (s, e) => PlaceButtons();
            PlaceButtons();

            footer.Controls.AddRange(new Control[] { cancelBtn, proceedBtn });

            CancelButton = cancelBtn;
            AcceptButton = proceedBtn;

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
