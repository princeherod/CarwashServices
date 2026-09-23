using System;
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
using CarwashServices.Shell;

namespace CarwashServices.Controls.Reports
{
    public class ComplaintFeedbackReportView : UserControl, IReportView, IExportableReport
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color Slate = Color.FromArgb(0xB0, 0xBE, 0xD0);

        private static readonly Font PillFont = new("Segoe UI Semibold", 9f);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private ComplaintsFeedbackReportDto _data = new();
        private string _currentRange = "This Year";

        private readonly Label _kTotalFeedback = new() { AutoSize = true };
        private readonly Label _kTotalFeedbackSub = new() { AutoSize = true };
        private readonly Label _kAvgRating = new() { AutoSize = true };
        private readonly Label _kAvgRatingSub = new() { AutoSize = true };
        private readonly Label _kTotalComplaints = new() { AutoSize = true };
        private readonly Label _kTotalComplaintsSub = new() { AutoSize = true };
        private readonly Label _kResolution = new() { AutoSize = true };
        private readonly Label _kResolutionSub = new() { AutoSize = true };

        private readonly DonutChartSimple _donut = new();
        private readonly BarChartSimple _bar = new() { BarColor = Red };
        private readonly DataGridView _grid = new();
        private readonly Label _subtitle = new();

        private TableLayoutPanel _page;
        private Panel _chartsRowPanel;
        private Panel _tableRowPanel;

        public ComplaintFeedbackReportView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            DoubleBuffered = true;
            AutoScroll = true;
            AutoScrollMinSize = new Size(900, 100 + 130 + 340 + 400);
            BuildUi();
        }

        public void ApplyFilters(string dateRange, string service, string vehicleType)
        {
            _currentRange = dateRange ?? "This Year";
            _ = LoadAsync(_currentRange);
        }

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

        // ================================================================
        //  NAVIGATION
        // ================================================================
        private static void BindClick(Control root, Action onClick)
        {
            if (root == null || onClick == null) return;
            root.Cursor = Cursors.Hand;
            root.Click += (s, e) => onClick();
            foreach (Control child in root.Controls)
                BindClick(child, onClick);
        }

        private void Navigate(string moduleKey)
        {
            (FindParentForm() as MainForm)?.NavigateToModule(moduleKey);
        }

        private Form FindParentForm() => FindForm();

        // ================================================================
        //  LAYOUT
        // ================================================================
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
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));
            _page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(_page);

            var header = MakePageHeader(
                "Complaint & Feedback Report",
                "Customer complaints and feedback recorded against service visits.");
            header.Dock = DockStyle.Fill;
            header.Margin = Padding.Empty;
            _page.Controls.Add(header, 0, 0);

            // KPI row
            var kpiGrid = MakeGrid(25f, 25f, 25f, 25f);
            AddKpi(kpiGrid, 0, "Total Feedback", "", _kTotalFeedback, _kTotalFeedbackSub, Blue);
            AddKpi(kpiGrid, 1, "Avg. Rating", "Customer satisfaction", _kAvgRating, _kAvgRatingSub, Amber);
            AddKpi(kpiGrid, 2, "Total Complaints", "", _kTotalComplaints, _kTotalComplaintsSub, Red);
            AddKpi(kpiGrid, 3, "Resolution Rate", "", _kResolution, _kResolutionSub, Green);
            _page.Controls.Add(MakeRowHost(kpiGrid), 0, 1);

            // Charts row
            var chartsGrid = MakeGrid(40f, 60f);
            var donutCard = MakeCard("Feedback Sentiment", _donut, new Padding(0, 0, 8, 16));
            var barCard = MakeCard("Complaints by Category", _bar, new Padding(8, 0, 0, 16));
            BindClick(donutCard, () => Navigate("Manage Customers"));
            BindClick(barCard, () => Navigate("Manage Customers"));
            chartsGrid.Controls.Add(donutCard, 0, 0);
            chartsGrid.Controls.Add(barCard, 1, 0);
            _chartsRowPanel = MakeRowHost(chartsGrid);
            _page.Controls.Add(_chartsRowPanel, 0, 2);

            // Table row
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

        internal static Control MakePageHeader(string title, string subtitle)
        {
            var header = new Panel { BackColor = PageBg };
            header.Controls.Add(new Label
            {
                Text = "Overview  ›  View Reports",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 22),
                AutoSize = true
            });
            header.Controls.Add(new Label
            {
                Text = subtitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(0, 66),
                AutoSize = true
            });
            return header;
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

            AddCol("Type", "Type", 11);
            AddCol("Date", "Date", 11);
            AddCol("Customer", "Customer", 16, bold: true);
            AddCol("Details", "Details", 30);
            AddCol("Cat", "Rating / Category", 15);
            AddCol("Status", "Status", 12);

            _grid.CellPainting += Grid_CellPainting;

            // Click a complaint/feedback row -> jump to the customer's record.
            _grid.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                Navigate("Manage Customers");
            };
        }

        private void AddCol(string name, string header, float weight, bool bold = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = weight,
                MinimumWidth = 90
            };
            if (bold)
                col.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI Semibold", 9.5f) };
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

            // Complaints and feedback route back to the customer record.
            BindClick(card, () => Navigate("Manage Customers"));

            grid.Controls.Add(card, col, 0);
        }

        // ================================================================
        //  DATA
        // ================================================================
        private async Task LoadAsync(string range)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var displayRange = range ?? "This Year";
                var r = displayRange.Replace(" ", "");
                var data = await _http.GetFromJsonAsync<ComplaintsFeedbackReportDto>(
                    $"api/reports/complaints-feedback?companyId=1&range={Uri.EscapeDataString(r)}")
                    ?? new ComplaintsFeedbackReportDto();
                _data = data;

                _kTotalFeedback.Text = data.TotalFeedback.ToString();
                _kTotalFeedbackSub.Text = $"{data.Sentiment.Positive} positive";

                if (data.AvgRating > 0)
                {
                    _kAvgRating.Text = data.AvgRating.ToString("0.#") + " / 5";
                    _kAvgRatingSub.Text = "Customer satisfaction";
                }
                else
                {
                    _kAvgRating.Text = "—";
                    _kAvgRatingSub.Text = "No ratings yet";
                }

                _kTotalComplaints.Text = data.TotalComplaints.ToString();
                _kTotalComplaintsSub.Text = $"{data.TotalComplaints - data.ResolvedComplaints} open";

                _kResolution.Text = data.ResolutionRate.ToString("0") + "%";
                _kResolutionSub.Text = $"{data.ResolvedComplaints} of {data.TotalComplaints} resolved";

                _donut.Segments = new()
                {
                    ("Negative", data.Sentiment.Negative, Red),
                    ("Neutral",  data.Sentiment.Neutral,  Slate),
                    ("Positive", data.Sentiment.Positive, Green)
                };
                _donut.CenterTop = data.Sentiment.Total.ToString();
                _donut.CenterBottom = "Total";

                _bar.Items = data.ComplaintsByCategory
                    .Select(x => (x.Label, (double)x.Value))
                    .ToList();

                _grid.SuspendLayout();
                _grid.Rows.Clear();
                foreach (var row in data.Rows)
                {
                    var idx = _grid.Rows.Add(
                        row.Type,
                        Or(row.Date),
                        Or(row.Customer),
                        Or(row.Details),
                        Or(row.RatingOrCategory),
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
                    $"Complaint & Feedback Report · {displayRange} · Generated at {data.GeneratedAt} · {data.Rows.Count} rows";
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

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _grid.Columns[e.ColumnIndex].Name;

            if (col == "Type")
            {
                var text = Convert.ToString(e.Value) ?? "";
                var isComplaint = text == "Complaint";
                var fg = isComplaint ? Red : Green;
                var bg = isComplaint ? Color.FromArgb(0xFD, 0xE7, 0xE6) : Color.FromArgb(0xE4, 0xF5, 0xE8);
                PaintPill(e, text, fg, bg);
            }
            else if (col == "Status")
            {
                var text = Convert.ToString(e.Value) ?? "";
                var (bg, fg) = text == "Resolved"
                    ? (Color.FromArgb(0xE4, 0xF5, 0xE8), Green)
                    : (Color.FromArgb(0xFF, 0xF4, 0xDB), Amber);
                PaintPill(e, text, fg, bg);
            }
        }

        private static void PaintPill(DataGridViewCellPaintingEventArgs e, string text, Color fg, Color bg)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
            if (!string.IsNullOrEmpty(text))
            {
                const TextFormatFlags measureFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                var textSize = TextRenderer.MeasureText(e.Graphics, text, PillFont,
                    new Size(int.MaxValue, int.MaxValue), measureFlags);
                int h = 24;
                int w = Math.Min(textSize.Width + 20, Math.Max(20, e.CellBounds.Width - 20));
                var pill = new Rectangle(e.CellBounds.X + 12,
                    e.CellBounds.Y + (e.CellBounds.Height - h) / 2, w, h);
                var oldMode = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(pill, h / 2))
                using (var brush = new SolidBrush(bg))
                    e.Graphics.FillPath(brush, path);
                e.Graphics.SmoothingMode = oldMode;
                TextRenderer.DrawText(e.Graphics, text, PillFont, pill, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | measureFlags);
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

        public bool HasData => _data.Rows.Count > 0;

        public string BuildCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Type,Date,Customer,Details,Rating / Category,Status");
            foreach (var row in _data.Rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv(row.Type), Csv(row.Date), Csv(row.Customer),
                    Csv(row.Details), Csv(row.RatingOrCategory), Csv(row.Status)));
            }
            return sb.ToString();
        }

        public PrintDocument BuildPrintDocument()
        {
            var rows = _data.Rows;
            var rangeLabel = _currentRange;
            var generated = _data.GeneratedAt;

            var doc = new PrintDocument { DocumentName = "Complaint & Feedback Report" };
            doc.DefaultPageSettings.Landscape = true;
            doc.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
            const float rowH = 22f;
            string[] heads = { "Type", "Date", "Customer", "Details", "Rating / Category", "Status" };
            float[] weights = { 1.0f, 1.0f, 1.8f, 3.4f, 1.6f, 1.2f };
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

                g.DrawString("Complaint & Feedback Report", page == 1 ? titleFont : headFont, ink, area.Left, y);
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
                        r.Type ?? "", r.Date ?? "", r.Customer ?? "",
                        r.Details ?? "", r.RatingOrCategory ?? "", r.Status ?? ""
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