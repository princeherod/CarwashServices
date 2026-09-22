using System;
using System.Collections.Generic;
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
    public class CustomerActivityReportView : UserControl, IReportView
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Purple = Color.FromArgb(0x8B, 0x5C, 0xF6);
        private static readonly Color Orange = Color.FromArgb(0xF5, 0x7C, 0x00);
        private static readonly Color Unspecified = Color.FromArgb(0xC9, 0xD2, 0xE3);

        private static readonly Font PillFont = new("Segoe UI Semibold", 9f);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly Label _kTotal = new() { AutoSize = true };
        private readonly Label _kTotalSub = new() { AutoSize = true };
        private readonly Label _kActive = new() { AutoSize = true };
        private readonly Label _kActiveSub = new() { AutoSize = true };
        private readonly Label _kReturning = new() { AutoSize = true };
        private readonly Label _kReturningSub = new() { AutoSize = true };
        private readonly Label _kAvg = new() { AutoSize = true };
        private readonly Label _kAvgSub = new() { AutoSize = true };

        // NOTE: no Dock here on purpose. MakeCard() docks the charts inside the
        // card's padded body, below the title. Docking them here as well made
        // them cover the whole card and slide underneath the title.
        private readonly BarChartSimple _bySource = new() { BarColor = Blue };
        private readonly DonutChartSimple _byVehicle = new();
        private readonly DataGridView _grid = new();
        private readonly Label _subtitle = new();

        public CustomerActivityReportView()
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
            AddKpi(kpiGrid, 0, "Total Customers", "", _kTotal, _kTotalSub, Blue);
            AddKpi(kpiGrid, 1, "Active Customers", "Visited within 60 days", _kActive, _kActiveSub, Green);
            AddKpi(kpiGrid, 2, "Returning Rate", "2+ visits", _kReturning, _kReturningSub, Purple);
            AddKpi(kpiGrid, 3, "Avg Services / Customer", "", _kAvg, _kAvgSub, Orange);
            page.Controls.Add(kpiGrid, 0, 0);

            // Charts
            var chartsGrid = MakeGrid(50f, 50f);
            chartsGrid.Controls.Add(
                MakeCard("Customers by Acquisition Source", _bySource, new Padding(0, 0, 8, 16)), 0, 0);
            chartsGrid.Controls.Add(
                MakeCard("Customers by Vehicle Type", _byVehicle, new Padding(8, 0, 0, 16)), 1, 0);
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

            // Columns share the full width, so there is no dead space on the right
            // and no horizontal scrollbar.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            AddCol("Customer", "Customer", 19, bold: true);
            AddCol("Phone", "Phone", 14);
            AddCol("Vehicle", "Vehicle Type", 11);
            AddCol("Source", "Source", 11);
            AddCol("Visits", "Total Visits", 10, right: true);
            AddCol("LastVisit", "Last Visit", 11);
            AddCol("LTV", "Lifetime Value", 12, right: true, format: "₱#,##0");
            AddCol("Avg", "Avg Spend", 10, right: true, format: "₱#,##0");
            AddCol("Status", "Status", 11);

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
            // Dock = Fill (via Card) makes the card stretch to its column.
            // Before, each card kept its default 200x100 size and left big gaps.
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
                var data = await _http.GetFromJsonAsync<CustomerActivityReportDto>(
                    $"api/reports/customer-activity?companyId=1&range={Uri.EscapeDataString(r)}")
                    ?? new CustomerActivityReportDto();

                // KPIs
                _kTotal.Text = data.TotalCustomers.ToString();
                _kTotalSub.Text = $"{data.Rows.Count} listed below";
                _kActive.Text = data.ActiveCustomers.ToString();
                _kActiveSub.Text = "Visited within 60 days";
                _kReturning.Text = data.ReturningRate.ToString("0") + "%";
                _kReturningSub.Text = "2+ visits";
                _kAvg.Text = data.AvgServicesPerCustomer.ToString("0.#");
                _kAvgSub.Text = "Per customer";

                // Bar chart. Customers with no source are added as "Not specified" so
                // the chart adds up to the Total Customers KPI.
                var sourceItems = data.BySource
                    .Select(x => (x.Label, (double)x.Value))
                    .ToList();
                var noSource = data.TotalCustomers - data.BySource.Sum(x => (double)x.Value);
                if (noSource > 0) sourceItems.Add(("Not specified", noSource));
                _bySource.Items = sourceItems;

                // Donut chart (same idea for vehicle type)
                var palette = new[]
                {
                    Blue, Green, Orange, Purple,
                    Color.FromArgb(0x14, 0xA3, 0x8B), Color.FromArgb(0xE5, 0x39, 0x35),
                    Color.FromArgb(0xF5, 0xB0, 0x2E), Muted
                };
                var segments = new List<(string, double, Color)>();
                for (int i = 0; i < data.ByVehicle.Count; i++)
                    segments.Add((data.ByVehicle[i].Label, (double)data.ByVehicle[i].Value, palette[i % palette.Length]));

                var noVehicle = data.TotalCustomers - data.ByVehicle.Sum(x => (double)x.Value);
                if (noVehicle > 0) segments.Add(("Not specified", noVehicle, Unspecified));

                _byVehicle.Segments = segments;
                _byVehicle.CenterTop = data.TotalCustomers.ToString();
                _byVehicle.CenterBottom = "Customers";

                // Table
                _grid.SuspendLayout();
                _grid.Rows.Clear();
                foreach (var row in data.Rows)
                {
                    var idx = _grid.Rows.Add(
                        row.Customer,
                        Or(row.Phone),
                        Or(row.VehicleType),
                        Or(row.Source),
                        row.TotalVisits,
                        Or(row.LastVisit),
                        row.LifetimeValue,   // formatted by the column style (₱#,##0)
                        row.AvgSpend,
                        row.Status);

                    // grey out "—" placeholders
                    foreach (DataGridViewCell cell in _grid.Rows[idx].Cells)
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
                    $"Customer Activity Report · {displayRange} · Generated at {data.GeneratedAt} · {data.Rows.Count} rows";
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

        private static string Or(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value;

        // ------------------------------------------------------------------
        //  Status pill
        // ------------------------------------------------------------------
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Status") return;

            // background, borders and selection highlight — but not the text
            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

            var text = e.FormattedValue?.ToString() ?? "";
            if (text.Length > 0)
            {
                bool active = text == "Active";
                var fg = active ? Green : Muted;
                var bg = active ? Color.FromArgb(0xE6, 0xF4, 0xEA) : Color.FromArgb(0xEE, 0xF1, 0xF6);

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