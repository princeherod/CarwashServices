using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Controls.Reports
{
    public class CustomerActivityReportView : UserControl, IReportView, IExportableReport
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

        private CustomerActivityReportDto _data = new();
        private string _currentRange = "This Year";

        private readonly Label _kTotal = new() { AutoSize = true };
        private readonly Label _kTotalSub = new() { AutoSize = true };
        private readonly Label _kActive = new() { AutoSize = true };
        private readonly Label _kActiveSub = new() { AutoSize = true };
        private readonly Label _kReturning = new() { AutoSize = true };
        private readonly Label _kReturningSub = new() { AutoSize = true };
        private readonly Label _kAvg = new() { AutoSize = true };
        private readonly Label _kAvgSub = new() { AutoSize = true };

        private readonly BarChartSimple _bySource = new() { BarColor = Blue };
        private readonly DonutChartSimple _byVehicle = new();
        private readonly DataGridView _grid = new();
        private readonly Label _subtitle = new();

        // ---- Layout rows ----
        private TableLayoutPanel _page;
        private Panel _chartsRowPanel;    // row 2
        private Panel _tableRowPanel;     // row 3

        public CustomerActivityReportView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            DoubleBuffered = true;

            AutoScroll = true;
            AutoScrollMinSize = new Size(900, 100 + 130 + 340 + 420);

            BuildUi();
        }

        public void ApplyFilters(string dateRange, string service, string vehicleType)
        {
            _currentRange = dateRange ?? "This Year";
            _ = LoadAsync(_currentRange);
        }

        // ---- View modes ----
        // Row layout: 0=header (always)  1=KPI (always)  2=charts  3=table
        public void ShowChartOnly()
        {
            if (_page == null) return;
            _page.RowStyles[2] = new RowStyle(SizeType.Absolute, 340);
            _page.RowStyles[3] = new RowStyle(SizeType.Absolute, 0);

            if (_chartsRowPanel != null) _chartsRowPanel.Visible = true;
            if (_tableRowPanel != null) _tableRowPanel.Visible = false;

            _page.PerformLayout();
        }

        public void ShowTableOnly()
        {
            if (_page == null) return;
            _page.RowStyles[2] = new RowStyle(SizeType.Absolute, 0);
            _page.RowStyles[3] = new RowStyle(SizeType.Percent, 100f);

            if (_chartsRowPanel != null) _chartsRowPanel.Visible = false;
            if (_tableRowPanel != null) _tableRowPanel.Visible = true;

            _page.PerformLayout();
        }

        public void ShowChartAndTable()
        {
            if (_page == null) return;
            _page.RowStyles[2] = new RowStyle(SizeType.Absolute, 340);
            _page.RowStyles[3] = new RowStyle(SizeType.Percent, 100f);

            if (_chartsRowPanel != null) _chartsRowPanel.Visible = true;
            if (_tableRowPanel != null) _tableRowPanel.Visible = true;

            _page.PerformLayout();
        }

        private void BuildUi()
        {
            _page = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = PageBg,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));   // row 0: header
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));   // row 1: KPI
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));   // row 2: charts
            _page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // row 3: table
            Controls.Add(_page);

            // ---- Row 0: header ----
            var header = ComplaintFeedbackReportView.MakePageHeader(
                "Customer Activity Report",
                "How often each customer visits and how much they spend.");
            header.Dock = DockStyle.Fill;
            header.Margin = Padding.Empty;
            _page.Controls.Add(header, 0, 0);

            // ---- Row 1: KPI ----
            var kpiGrid = MakeGrid(25f, 25f, 25f, 25f);
            AddKpi(kpiGrid, 0, "Total Customers", "", _kTotal, _kTotalSub, Blue);
            AddKpi(kpiGrid, 1, "Active Customers", "Visited within 60 days", _kActive, _kActiveSub, Green);
            AddKpi(kpiGrid, 2, "Returning Rate", "2+ visits", _kReturning, _kReturningSub, Purple);
            AddKpi(kpiGrid, 3, "Avg Services / Customer", "", _kAvg, _kAvgSub, Orange);
            _page.Controls.Add(MakeRowHost(kpiGrid), 0, 1);

            // ---- Row 2: charts ----
            var chartsGrid = MakeGrid(50f, 50f);
            chartsGrid.Controls.Add(
                MakeCard("Customers by Acquisition Source", _bySource, new Padding(0, 0, 8, 16)), 0, 0);
            chartsGrid.Controls.Add(
                MakeCard("Customers by Vehicle Type", _byVehicle, new Padding(8, 0, 0, 16)), 1, 0);
            _chartsRowPanel = MakeRowHost(chartsGrid);
            _page.Controls.Add(_chartsRowPanel, 0, 2);

            // ---- Row 3: table ----
            _tableRowPanel = MakeRowHost(MakeTableCard());
            _page.Controls.Add(_tableRowPanel, 0, 3);
        }

        private static Panel MakeRowHost(Control content)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Margin = Padding.Empty
            };
            content.Dock = DockStyle.Fill;
            host.Controls.Add(content);
            return host;
        }

        private static Card MakeCard(string title, Control content, Padding margin)
        {
            var card = new Card
            {
                Margin = margin,
                Padding = new Padding(20, 14, 20, 20)
            };

            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            card.Controls.Add(new Label
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
                SelectionBackColor = headerBg,
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
            var count = grid.ColumnCount;
            var card = new Card
            {
                Margin = new Padding(col == 0 ? 0 : 8, 0, col == count - 1 ? 0 : 8, 16)
            };

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

                _data = data;

                _kTotal.Text = data.TotalCustomers.ToString();
                _kTotalSub.Text = $"{data.Rows.Count} listed below";
                _kActive.Text = data.ActiveCustomers.ToString();
                _kActiveSub.Text = "Visited within 60 days";
                _kReturning.Text = data.ReturningRate.ToString("0") + "%";
                _kReturningSub.Text = "2+ visits";
                _kAvg.Text = data.AvgServicesPerCustomer.ToString("0.#");
                _kAvgSub.Text = "Per customer";

                var sourceItems = data.BySource
                    .Select(x => (x.Label, (double)x.Value))
                    .ToList();
                var noSource = data.TotalCustomers - data.BySource.Sum(x => (double)x.Value);
                if (noSource > 0) sourceItems.Add(("Not specified", noSource));
                _bySource.Items = sourceItems;

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
                        row.LifetimeValue,
                        row.AvgSpend,
                        row.Status);

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

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Status") return;

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
                    textSize.Width + 20, h);

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
        //  Export
        // ------------------------------------------------------------------
        public bool HasData => _data.Rows.Count > 0;

        public string BuildCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Customer,Phone,Vehicle Type,Source,Total Visits,Last Visit,Lifetime Value,Avg Spend,Status");
            var inv = CultureInfo.InvariantCulture;
            foreach (var row in _data.Rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv(row.Customer), Csv(row.Phone), Csv(row.VehicleType), Csv(row.Source),
                    row.TotalVisits.ToString(inv),
                    Csv(row.LastVisit),
                    row.LifetimeValue.ToString("0.##", inv),
                    row.AvgSpend.ToString("0.##", inv),
                    Csv(row.Status)));
            }
            return sb.ToString();
        }

        public PrintDocument BuildPrintDocument()
        {
            var rows = _data.Rows;
            var rangeLabel = _currentRange;
            var generated = _data.GeneratedAt;

            var doc = new PrintDocument { DocumentName = "Customer Activity Report" };
            doc.DefaultPageSettings.Landscape = true;
            doc.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);

            const float rowH = 22f;
            string[] heads = { "Customer", "Phone", "Vehicle", "Source", "Visits", "Last Visit", "LTV", "Avg Spend", "Status" };
            float[] weights = { 1.6f, 1.3f, 1.0f, 1.0f, 0.8f, 1.0f, 1.1f, 1.0f, 0.9f };
            int next = 0, page = 0;

            doc.BeginPrint += (s, e) => { next = 0; page = 0; };

            doc.PrintPage += (s, e) =>
            {
                var g = e.Graphics!;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                var area = e.MarginBounds;
                float y = area.Top;
                page++;

                using var titleFont = new Font("Segoe UI Semibold", 16f);
                using var subFont = new Font("Segoe UI", 9f);
                using var headFont = new Font("Segoe UI Semibold", 9f);
                using var bodyFont = new Font("Segoe UI", 9f);
                using var ink = new SolidBrush(Navy);
                using var gray = new SolidBrush(Muted);
                using var headBg = new SolidBrush(Color.FromArgb(0xF3, 0xF6, 0xFB));
                using var linePen = new Pen(CardBorder);
                using var fmt = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                    LineAlignment = StringAlignment.Center
                };

                g.DrawString("Customer Activity Report", page == 1 ? titleFont : headFont, ink, area.Left, y);
                y += (page == 1 ? titleFont : headFont).GetHeight(g) + 2;

                if (page == 1)
                {
                    g.DrawString($"{rangeLabel} · Generated at {generated}", subFont, gray, area.Left, y);
                    y += subFont.GetHeight(g) + 8;
                }
                else y += 8;

                float sum = weights.Sum();
                var xs = new float[weights.Length + 1];
                xs[0] = area.Left;
                for (int i = 0; i < weights.Length; i++)
                    xs[i + 1] = xs[i] + area.Width * weights[i] / sum;

                void DrawCells(string[] cells, Font f, Brush b, float top)
                {
                    for (int i = 0; i < cells.Length; i++)
                    {
                        var r = new RectangleF(xs[i] + 6, top, xs[i + 1] - xs[i] - 12, rowH);
                        g.DrawString(cells[i], f, b, r, fmt);
                    }
                }

                g.FillRectangle(headBg, area.Left, y, area.Width, rowH);
                DrawCells(heads, headFont, gray, y);
                y += rowH;

                float bottom = area.Bottom - 24f;
                while (next < rows.Count && y + rowH <= bottom)
                {
                    var r = rows[next];
                    DrawCells(new[]
                    {
                        r.Customer ?? "", r.Phone ?? "", r.VehicleType ?? "", r.Source ?? "",
                        r.TotalVisits.ToString(),
                        r.LastVisit ?? "",
                        "₱" + r.LifetimeValue.ToString("N0"),
                        "₱" + r.AvgSpend.ToString("N0"),
                        r.Status ?? ""
                    }, bodyFont, ink, y);
                    g.DrawLine(linePen, area.Left, y + rowH, area.Right, y + rowH);
                    y += rowH;
                    next++;
                }

                g.DrawString($"Page {page}", subFont, gray,
                    new RectangleF(area.Left, area.Bottom - 14f, area.Width, 16f),
                    new StringFormat { Alignment = StringAlignment.Far });

                e.HasMorePages = next < rows.Count;
            };

            return doc;
        }

        private static string Csv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if ("=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

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