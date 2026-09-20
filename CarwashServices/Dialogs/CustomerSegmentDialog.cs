using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class CustomerSegmentDialog : Form
    {
        private readonly List<SegmentCustomerDto> _rows;
        private readonly string _title;
        private DataGridView _grid = null!;

        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Faint = Color.FromArgb(0xB4, 0xBE, 0xD2);

        // Column layout — weights out of 1000 (relative widths, not pixels)
        private static readonly (string Name, string Header, int Weight)[] Columns =
        {
            ("Name",         "Customer",   150),
            ("Contact",      "Contact",    130),
            ("Vehicle",      "Vehicle",     80),
            ("LastVisit",    "Last visit",  95),
            ("Days",         "Days since",  75),
            ("Status",       "Status",     120),
            ("LastFollowUp", "Follow-up",  180),
            ("Action",       "",           120),
        };

        public List<int> SelectedCustomerIds { get; private set; } = new();

        public CustomerSegmentDialog(string title, List<SegmentCustomerDto> rows)
        {
            _title = title;
            _rows = rows ?? new();

            // ---- Window ----
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.Sizable;    // was FixedDialog — now resizable
            MaximizeBox = true;
            MinimizeBox = false;
            MinimumSize = new Size(1000, 500);

            // Size relative to the parent screen — bigger than before
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1400, 900);
            ClientSize = new Size(
                Math.Min(1200, wa.Width - 80),
                Math.Min(700, wa.Height - 80));

            // ---- Header ----
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

            // ---- Grid ----
            var orderedRows = _rows.OrderByDescending(r => r.DaysSince).ToList();

            _grid = new DataGridView
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
                    SelectionBackColor = Color.FromArgb(0xF7, 0xFA, 0xFD),
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersHeight = 44,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 64 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(12, 0, 6, 0),
                    WrapMode = DataGridViewTriState.False
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                AllowUserToResizeColumns = true,
                AllowUserToOrderColumns = false
            };

            // Build columns with placeholder widths; we'll set actual widths in LayoutGrid()
            foreach (var c in Columns)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = c.Name,
                    HeaderText = c.Header,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Resizable = DataGridViewTriState.True
                });
            }

            foreach (var r in orderedRows)
            {
                var statusText = r.Segment == "Lost"
                    ? $"Lost ({r.DaysSince}d)"
                    : $"{r.DaysLeft} days left";

                _grid.Rows.Add(
                    r.Name,
                    string.IsNullOrWhiteSpace(r.Phone) ? r.Email ?? "" : r.Phone,
                    r.Vehicle ?? "—",
                    r.LastVisit == default ? "—" : r.LastVisit.ToString("yyyy-MM-dd"),
                    r.DaysSince >= 9999 ? "—" : r.DaysSince.ToString(),
                    statusText,
                    BuildFollowUpText(r),
                    r.HasOpenFollowUp ? "Contacted" : "Follow Up");
            }

            _grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "Action") return;

                var row = orderedRows[e.RowIndex];
                PaintActionButton(e, row.HasOpenFollowUp);
            };

            _grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "Action") return;

                var row = orderedRows[e.RowIndex];

                if (row.HasOpenFollowUp)
                {
                    var when = row.LastFollowUpDate.HasValue
                        ? $" ({row.LastFollowUpDate.Value:yyyy-MM-dd})"
                        : "";
                    MessageBox.Show(
                        $"{row.Name} was already contacted.\n\n" +
                        $"Latest follow-up: {row.LastFollowUpType ?? "Follow-up"} — {row.LastFollowUpStatus}{when}.\n\n" +
                        "Wait for a response or try a new campaign later.",
                        "Already Contacted",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SelectedCustomerIds = new List<int> { row.CustomerId };
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(_grid);
            _grid.BringToFront();

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White
            };
            Controls.Add(footer);
            footer.BringToFront();

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
                Text = "Follow Up All Pending",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Accent,
                Size = new Size(210, 42),
                Location = new Point(footer.Width - 380, 14),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            followUpBtn.FlatAppearance.BorderColor = Accent;
            followUpBtn.FlatAppearance.BorderSize = 0;
            followUpBtn.Click += (s, e) =>
            {
                SelectedCustomerIds = _rows
                    .Where(r => !r.HasOpenFollowUp)
                    .Select(r => r.CustomerId)
                    .ToList();

                if (SelectedCustomerIds.Count == 0)
                {
                    MessageBox.Show(
                        "Everyone here has already been contacted.",
                        "Nothing to do",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            footer.Controls.Add(followUpBtn);

            // ---- Auto-layout on resize ----
            void LayoutGrid()
            {
                if (_grid == null || _grid.IsDisposed) return;
                if (_grid.ClientSize.Width < 200) return;

                int totalWeight = Columns.Sum(c => c.Weight);
                int usable = _grid.ClientSize.Width - 4;   // small fudge for borders
                int assigned = 0;

                for (int i = 0; i < Columns.Length - 1; i++)
                {
                    int w = (int)Math.Round(usable * (double)Columns[i].Weight / totalWeight);
                    _grid.Columns[Columns[i].Name].Width = w;
                    assigned += w;
                }
                // last column absorbs rounding remainder
                _grid.Columns[Columns[^1].Name].Width = Math.Max(80, usable - assigned);
            }

            _grid.Resize += (s, e) => LayoutGrid();
            Load += (s, e) => LayoutGrid();
        }

        private static string BuildFollowUpText(SegmentCustomerDto r)
        {
            if (string.IsNullOrWhiteSpace(r.LastFollowUpStatus)) return "—";

            var when = r.LastFollowUpDate.HasValue
                ? r.LastFollowUpDate.Value.ToString("MMM dd")
                : "";

            var type = r.LastFollowUpType ?? "Follow-up";

            return $"{type} · {r.LastFollowUpStatus} · {when}";
        }

        // ---- Custom-drawn pill button (blue = active, grey = disabled) ----
        private static void PaintActionButton(DataGridViewCellPaintingEventArgs e, bool isContacted)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = isContacted ? "Contacted" : "Follow Up";
            bool enabled = !isContacted;

            var bounds = e.CellBounds;
            int btnH = 34;
            int btnW = Math.Min(110, bounds.Width - 16);
            var rect = new Rectangle(
                bounds.X + (bounds.Width - btnW) / 2,
                bounds.Y + (bounds.Height - btnH) / 2,
                btnW, btnH);

            using var path = RoundedRect(rect, 8);
            using var fill = new SolidBrush(enabled ? Accent : Color.White);
            using var border = new Pen(enabled ? Accent : Faint, 1);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            TextRenderer.DrawText(
                e.Graphics, text,
                new Font("Segoe UI Semibold", 9f),
                rect,
                enabled ? Color.White : Faint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            e.Handled = true;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}