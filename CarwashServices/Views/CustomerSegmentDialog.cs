using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CarwashServices.Views
{
    public class CustomerSegmentDialog : Form
    {
        private readonly List<SegmentCustomerDto> _rows;
        private readonly string _title;

        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);

        public List<int> SelectedCustomerIds { get; private set; } = new();

        public CustomerSegmentDialog(string title, List<SegmentCustomerDto> rows)
        {
            _title = title;
            _rows = rows ?? new();

            Text = title;
            Size = new Size(820, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Header
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(30, 0, 30, 0)
            };
            header.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(30, 22),
                AutoSize = true
            });
            Controls.Add(header);

            // Grid
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = BorderSoft,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xF7, 0xFA, 0xFD),
                    ForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0)
                },
                ColumnHeadersHeight = 44,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 62 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(16, 0, 0, 0)
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Customer", Width = 200 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Contact", HeaderText = "Contact", Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Vehicle", HeaderText = "Vehicle", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastVisit", HeaderText = "Last visit", Width = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Days", HeaderText = "Days since", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });

            foreach (var r in _rows.OrderByDescending(r => r.DaysSince))
            {
                var statusText = r.Segment == "Lost"
                    ? $"Lost ({r.DaysSince}d)"
                    : $"{r.DaysLeft} days left";

                grid.Rows.Add(
                    r.Name,
                    string.IsNullOrWhiteSpace(r.Phone) ? r.Email : r.Phone,
                    r.Vehicle ?? "—",
                    r.LastVisit == default ? "—" : r.LastVisit.ToString("yyyy-MM-dd"),
                    r.DaysSince >= 9999 ? "—" : r.DaysSince.ToString(),
                    statusText);
            }
            Controls.Add(grid);
            grid.BringToFront();

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White
            };
            Controls.Add(footer);

            var closeBtn = new Button
            {
                Text = "Close",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted,
                BackColor = Color.White,
                Size = new Size(120, 42),
                Location = new Point(footer.Width - 150, 14),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            closeBtn.FlatAppearance.BorderColor = BorderSoft;
            closeBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(closeBtn);

            var followUpBtn = new Button
            {
                Text = "Follow Up Selected",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0x1E, 0x88, 0xE5),
                Size = new Size(200, 42),
                Location = new Point(footer.Width - 360, 14),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            followUpBtn.FlatAppearance.BorderSize = 0;
            followUpBtn.Click += (s, e) =>
            {
                // For now, follow up ALL customers shown
                SelectedCustomerIds = _rows.Select(r => r.CustomerId).ToList();
                DialogResult = DialogResult.OK;
                Close();
            };
            footer.Controls.Add(followUpBtn);
            footer.BringToFront();
        }
    }
}