using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Controls.Reports
{
    public class RetentionSummaryReportView : UserControl, IReportView
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color Purple = Color.FromArgb(0x8B, 0x5C, 0xF6);

        private static readonly Font PillFont = new("Segoe UI Semibold", 9f);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly Label _kRetention = new() { AutoSize = true };
        private readonly Label _kRetentionSub = new() { AutoSize = true };
        private readonly Label _kAtRisk = new() { AutoSize = true };
        private readonly Label _kAtRiskSub = new() { AutoSize = true };
        private readonly Label _kChurned = new() { AutoSize = true };
        private readonly Label _kChurnedSub = new() { AutoSize = true };
        private readonly Label _kUnresolved = new() { AutoSize = true };
        private readonly Label _kUnresolvedSub = new() { AutoSize = true };

        // No Dock here on purpose: MakeCard() docks these under the card title.
        // Docking them here too made them cover the whole card and slide
        // underneath the title.
        private readonly DonutChartSimple _donut = new();
        private readonly BarChartSimple _topLtv = new() { BarColor = Purple };
        private readonly DataGridView _grid = new();
        private readonly Label _subtitle = new();

        public RetentionSummaryReportView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            DoubleBuffered = true;

            // If the window gets very small, scroll instead of squashing the cards.
            AutoScroll = true;
            AutoScrollMinSize = new Size(900, 130 + 340 + 420);

            BuildUi();
        }

        public void ApplyFilters(string dateRange, string service, string vehicleType)
            => _ = LoadAsync(dateRange);

        // ------------------------------------------------------------------
        //  Layout
        // ------------------------------------------------------------------
        private void BuildUi()
        {
            // KPI row (fixed) / charts row (fixed) / table row (takes the rest)
            var page = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = PageBg,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(page);

            // KPI cards
            var kpiGrid = MakeGrid(25f, 25f, 25f, 25f);
            AddKpi(kpiGrid, 0, "Retention Rate", "Active within 60 days", _kRetention, _kRetentionSub, Green);
            AddKpi(kpiGrid, 1, "At Risk", "61–120 days inactive", _kAtRisk, _kAtRiskSub, Amber);
            AddKpi(kpiGrid, 2, "Churned", "120d+ no activity", _kChurned, _kChurnedSub, Red);
            AddKpi(kpiGrid, 3, "Unresolved Complaints", "Affecting retention", _kUnresolved, _kUnresolvedSub, Purple);
            page.Controls.Add(kpiGrid, 0, 0);

            // Charts
            var chartsGrid = MakeGrid(45f, 55f);
            chartsGrid.Controls.Add(
                MakeCard("Customer Health Segmentation", _donut, new Padding(0, 0, 8, 16)), 0, 0);
            chartsGrid.Controls.Add(
                MakeCard("Top Customers by Lifetime Value", _topLtv, new Padding(8, 0, 0, 16)), 1, 0);
            page.Controls.Add(chartsGrid, 0, 1);

            // Table
            page.Controls.Add(MakeTableCard(), 0, 2);
        }

        /// <summary>White bordered card: title on top, content fills the rest.</summary>
        private static Card MakeCard(string title, Control content, Padding margin)
        {
            var card = new Card
            {
                Margin = margin,
                Padding = new Padding(20, 14, 20, 20)
            };

            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);              // Fill control must be added first...
            card.Controls.Add(new Label              // ...so the Top-docked title is laid out before it.
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 12f)
            });
            return card;
        }

        private Card MakeTableCard()
        {
            var card = new Card
            {
                Margin = Padding.Empty,
                Padding = new Padding(20, 12, 20, 20)
            };

            ConfigureGrid();
            _grid.Dock = DockStyle.Fill;
            card.Controls.Add(_grid);

            _subtitle.AutoSize = false;
            _subtitle.Dock = DockStyle.Top;
            _subtitle.Height = 30;
            _subtitle.TextAlign = ContentAlignment.MiddleLeft;
            _subtitle.ForeColor = Muted;
            _subtitle.Font = new Font("Segoe UI", 9f);
            _subtitle.UseMnemonic = false;
            card.Controls.Add(_subtitle);

            return card;
        }

        private void ConfigureGrid()
        {
            var headerBg = Color.FromArgb(0xF7, 0xFA, 0xFD);

            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.None;
            _grid.GridColor = CardBorder;
            _grid.EnableHeadersVisualStyles = false;

            _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = headerBg,
                ForeColor = Muted,
                SelectionBackColor = headerBg,   // no blue flash when a header is clicked
                SelectionForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 9f),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = 40;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _grid.RowTemplate.Height = 44;
            _grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Navy,
                SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                SelectionForeColor = Navy,
                Padding = new Padding(12, 0, 0, 0)
            };
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;

            // Columns share the full width: no dead space on the right, no
            // horizontal scrollbar, and "Days Since" is no longer cut off.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            AddCol("Customer", "Customer", 17, bold: true);
            AddCol("LastVisit", "Last Visit", 11);
            AddCol("Days", "Days Since", 9, right: true);
            AddCol("Visits", "Total Visits", 10, right: true);
            AddCol("LTV", "LTV", 11, right: true, format: "₱#,##0");
            AddCol("Rating", "Avg Rating", 9, right: true);
            AddCol("Complaint", "Complaint", 11);
            AddCol("Segment", "Segment", 11);
            AddCol("Action", "Retention Action", 16);

            _grid.CellPainting += Grid_CellPainting;
        }

        private void AddCol(string name, string header, float weight,
                            bool right = false, bool bold = false, string? format = null)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = weight,
                MinimumWidth = 80
            };

            var style = new DataGridViewCellStyle();
            if (bold) style.Font = new Font("Segoe UI Semibold", 9.5f);
            if (format != null) style.Format = format;
            if (right)
            {
                style.Alignment = DataGridViewContentAlignment.MiddleRight;
                style.Padding = new Padding(0, 0, 16, 0);

                col.HeaderCell.Style = new DataGridViewCellStyle(_grid.ColumnHeadersDefaultCellStyle)
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Padding = new Padding(0, 0, 16, 0)
                };
            }
            col.DefaultCellStyle = style;

            _grid.Columns.Add(col);
        }

        private static TableLayoutPanel MakeGrid(params float[] percents)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = percents.Length,
                RowCount = 1,
                BackColor = PageBg,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            foreach (var p in percents) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, p));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            return t;
        }

        private void AddKpi(TableLayoutPanel grid, int col, string title, string sub,
                            Label valueLbl, Label subLbl, Color accent)
        {
            // Card is Dock = Fill, so it stretches to its column. Before, each
            // card kept its default 200x100 size and left big gaps.
            var count = grid.ColumnCount;
            var card = new Card
            {
                Margin = new Padding(col == 0 ? 0 : 8, 0, col == count - 1 ? 0 : 8, 16)
            };

            // thin accent stripe on the left edge
            card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });

            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(22, 14),
                AutoSize = true,
                BackColor = Color.White
            });

            valueLbl.Text = "0";
            valueLbl.ForeColor = Navy;
            valueLbl.Font = new Font("Segoe UI Semibold", 22f);
            valueLbl.Location = new Point(20, 36);
            valueLbl.BackColor = Color.White;
            card.Controls.Add(valueLbl);

            subLbl.Text = sub;
            subLbl.ForeColor = Muted;
            subLbl.Font = new Font("Segoe UI", 8.5f);
            subLbl.Location = new Point(22, 82);
            subLbl.BackColor = Color.White;
            card.Controls.Add(subLbl);

            grid.Controls.Add(card, col, 0);
        }

        // ------------------------------------------------------------------
        //  Data
        // ------------------------------------------------------------------
        private async Task LoadAsync(string range)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var displayRange = range ?? "This Year";
                var r = displayRange.Replace(" ", "");
                var data = await _http.GetFromJsonAsync<RetentionSummaryReportDto>(
                    $"api/reports/retention-summary?companyId=1&range={Uri.EscapeDataString(r)}")
                    ?? new RetentionSummaryReportDto();

                // KPIs
                _kRetention.Text = data.RetentionRate.ToString("0") + "%";
                _kRetentionSub.Text = "Active within 60 days";
                _kAtRisk.Text = data.AtRisk.ToString();
                _kAtRiskSub.Text = "61–120 days inactive";
                _kChurned.Text = data.Churned.ToString();
                _kChurnedSub.Text = "120d+ no activity";
                _kUnresolved.Text = data.Unresolved.ToString();
                _kUnresolvedSub.Text = "Affecting retention";

                // Charts
                _donut.Segments = new()
                {
                    ("At Risk", data.Segmentation.AtRisk, Amber),
                    ("Churned", data.Segmentation.Churned, Red),
                    ("Healthy", data.Segmentation.Healthy, Green)
                };
                _donut.CenterTop = data.Segmentation.Total.ToString();
                _donut.CenterBottom = "Customers";

                _topLtv.Items = data.TopByLtv
                    .Select(x => (x.Label, (double)x.Value))
                    .ToList();

                // Table
                _grid.SuspendLayout();
                _grid.Rows.Clear();
                foreach (var row in data.Rows)
                {
                    var idx = _grid.Rows.Add(
                        row.Customer,
                        Or(row.LastVisit),
                        row.DaysSince,
                        row.TotalVisits,
                        row.Ltv,              // formatted by the column style (₱#,##0)
                        row.AvgRating,
                        Or(row.Complaint),
                        row.Segment,          // drawn as a pill in Grid_CellPainting
                        Or(row.RetentionAction));

                    var cells = _grid.Rows[idx].Cells;

                    // Complaint: unresolved stands out, "None" fades back
                    var complaint = cells["Complaint"].Value?.ToString();
                    if (complaint == "Unresolved")
                    {
                        cells["Complaint"].Style.ForeColor = Red;
                        cells["Complaint"].Style.SelectionForeColor = Red;
                        cells["Complaint"].Style.Font = new Font("Segoe UI Semibold", 9.5f);
                    }
                    else if (complaint == "None" || complaint == "—")
                    {
                        cells["Complaint"].Style.ForeColor = Muted;
                        cells["Complaint"].Style.SelectionForeColor = Muted;
                    }

                    // grey out "—" placeholders everywhere else
                    foreach (DataGridViewCell cell in cells)
                    {
                        if (cell.Value as string == "—")
                        {
                            cell.Style.ForeColor = Muted;
                            cell.Style.SelectionForeColor = Muted;
                        }
                    }
                }
                _grid.ClearSelection();
                _grid.ResumeLayout();

                _subtitle.Text =
                    $"Retention Summary Report · {displayRange} · Generated at {data.GeneratedAt} · {data.Rows.Count} rows";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load report.\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private static string Or(object? value)
        {
            var s = value?.ToString();
            return string.IsNullOrWhiteSpace(s) ? "—" : s;
        }

        private static (Color bg, Color fg) SegmentColors(string segment) => segment switch
        {
            "Churned" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Red),
            "At Risk" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Amber),
            "Healthy" => (Color.FromArgb(0xE4, 0xF5, 0xE8), Green),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        // ------------------------------------------------------------------
        //  Segment pill
        //  (before, the whole cell was filled with the segment colour)
        // ------------------------------------------------------------------
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Segment") return;

            // background, borders and selection highlight — but not the text
            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

            var text = e.FormattedValue?.ToString() ?? "";
            if (text.Length > 0)
            {
                var (bg, fg) = SegmentColors(text);

                var textSize = TextRenderer.MeasureText(e.Graphics, text, PillFont);
                int h = 24;
                var pill = new Rectangle(
                    e.CellBounds.X + 12,
                    e.CellBounds.Y + (e.CellBounds.Height - h) / 2,
                    textSize.Width + 20,
                    h);

                var oldMode = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(pill, h / 2))
                using (var brush = new SolidBrush(bg))
                    e.Graphics.FillPath(brush, path);
                e.Graphics.SmoothingMode = oldMode;

                TextRenderer.DrawText(e.Graphics, text, PillFont, pill, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

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

        // ------------------------------------------------------------------
        //  Card panel: white, 1px border, repaints cleanly on resize
        // ------------------------------------------------------------------
        private sealed class Card : Panel
        {
            public Card()
            {
                BackColor = Color.White;
                Dock = DockStyle.Fill;
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}