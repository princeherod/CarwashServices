using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Controls.Charts;
using CarwashServices.Controls.Reports;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles
{
    public class ReportsView : UserControl
    {
        public static string ApiBaseUrl { get; set; } = "http://localhost:5180/";
        public static int CompanyId { get; set; } = 1;

        private readonly HttpClient _http;
        private readonly CancellationTokenSource _cts = new();
        private ReportsResponseDto _data = new();
        private string _lastSubtitle = "";

        // ---- Layout ----
        private SmoothFlowPanel _flow;
        private CardPanel _filterCard;
        private Panel _kpiRow;
        private Panel _chartsRow;
        private CardPanel _tableCard;
        private readonly List<Control> _kpiCards = new();

        // ---- Filters ----
        private ComboBox _reportTypeCombo;
        private ComboBox _dateRangeCombo;
        private ComboBox _serviceCombo;
        private ComboBox _vehicleCombo;
        private Button _runBtn;
        private Button _csvBtn;
        private Button _pdfBtn;
        private Button _modeChartTableBtn;
        private Button _modeChartBtn;
        private Button _modeTableBtn;

        // ---- Revenue KPIs ----
        private Label _kpiTxn, _kpiTxnSub;
        private Label _kpiRev, _kpiRevSub;
        private Label _kpiTicket, _kpiTicketSub;
        private Label _kpiPending, _kpiPendingSub;

        // ---- Revenue charts + table ----
        private ReportLineChart _lineChart;
        private ReportBarChart _barChart;
        private DataGridView _grid;
        private Label _tableHeaderLbl;
        private Label _emptyLbl;

        // ---- Drill-down filter state ----
        private string _activeGridFilter = "";
        private Panel _filterChipBar;
        private Panel _filterChip;
        private Label _filterChipLabel;

        // ---- Sub-view hosting ----
        private Panel _extraHost;
        private UserControl _currentExtra;
        private string _currentReportType = "Service & Revenue Report";
        private readonly List<Control> _revenueWidgets = new();

        private const string RevenueReport = "Service & Revenue Report";
        private const string ComplaintReport = "Complaint & Feedback Report";
        private const string CustomerActivityReport = "Customer Activity Report";
        private const string RetentionReport = "Retention Summary Report";

        private enum ViewMode { ChartAndTable, ChartOnly, TableOnly }
        private ViewMode _mode = ViewMode.ChartAndTable;

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);

        // ---- Fonts ----
        private static readonly Font FBody = new("Segoe UI", 9.5f);
        private static readonly Font FCombo = new("Segoe UI", 10f);
        private static readonly Font FCaption = new("Segoe UI Semibold", 8f);
        private static readonly Font FTitle = new("Segoe UI Semibold", 12f);
        private static readonly Font FKpiValue = new("Segoe UI Semibold", 22f);
        private static readonly Font FSmall = new("Segoe UI", 8.5f);
        private static readonly Font FBtnBold = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FItalic = new("Segoe UI", 9.5f, FontStyle.Italic);
        private static readonly Font FStatus = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FGridHeader = new("Segoe UI Semibold", 9f);

        private static readonly bool TintBehindText = false;

        private const int MarginX = 40;
        private const int TopMargin = 20;
        private const int SectionGap = 20;
        private const int KpiRowHeight = 130;

        public ReportsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = FBody;
            DoubleBuffered = true;

            _http = new HttpClient
            {
                BaseAddress = new Uri(ApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };

            InitializeUI();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                _http.Dispose();
                _currentExtra?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ================================================================
        //  NAVIGATION
        // ================================================================
        private void Navigate(string moduleKey)
        {
            (FindForm() as MainForm)?.NavigateToModule(moduleKey);
        }

        private static void BindClick(Control root, Action onClick)
        {
            if (root == null || onClick == null) return;

            root.Cursor = Cursors.Hand;
            root.Click += (s, e) => onClick();

            foreach (Control child in root.Controls)
                BindClick(child, onClick);
        }

        // ================================================================
        //  UI CONSTRUCTION
        // ================================================================
        private void InitializeUI()
        {
            _flow = new SmoothFlowPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(MarginX, TopMargin, MarginX, TopMargin)
            };
            Controls.Add(_flow);
            _flow.ClientSizeChanged += (s, e) =>
            {
                FitSections();
                ResizeExtraHost();
            };

            BuildFilterCard();
            BuildKpiRow();
            BuildChartsRow();
            BuildTableCard();
            BuildExtraHost();

            SetMode(ViewMode.ChartAndTable);

            Load += async (s, e) =>
            {
                if (DesignMode) return;
                FitSections();
                await LoadAsync();
            };
        }

        private T Section<T>(T control, int height) where T : Control
        {
            control.Height = height;
            control.Margin = new Padding(0, 0, 0, SectionGap);
            _flow.Controls.Add(control);
            return control;
        }

        private void FitSections()
        {
            int w = Math.Max(760, _flow.ClientSize.Width - _flow.Padding.Horizontal);
            foreach (Control c in _flow.Controls)
                if (c.Width != w) c.Width = w;
        }

        private static void Inset(Control parent, Control child, int l, int t, int r, int b)
        {
            void Apply() => child.SetBounds(l, t,
                Math.Max(0, parent.ClientSize.Width - l - r),
                Math.Max(0, parent.ClientSize.Height - t - b));
            parent.Resize += (s, e) => Apply();
            Apply();
        }

        // ---------------- Filters ----------------
        private void BuildFilterCard()
        {
            _filterCard = Section(new CardPanel(), 150);

            _reportTypeCombo = AddFilter("REPORT TYPE", 20, 320,
                RevenueReport,
                ComplaintReport,
                CustomerActivityReport,
                RetentionReport);
            _reportTypeCombo.SelectedIndexChanged += (s, e) => SwitchReportType();

            _dateRangeCombo = AddFilter("DATE RANGE", 360, 180,
                "This Week", "This Month", "Last Month", "This Year", "Last Year", "All Time");
            _dateRangeCombo.SelectedIndex = 3; // This Year

            _serviceCombo = AddFilter("SERVICE", 560, 180, "All Services");
            _vehicleCombo = AddFilter("VEHICLE TYPE", 760, 180, "All Types");

            _runBtn = MakePrimaryButton("▶  Run Report", 20, 90);
            _runBtn.Click += async (s, e) => await RunReportAsync();
            _filterCard.Controls.Add(_runBtn);

            _csvBtn = MakeSecondaryButton("⬇  Export CSV", 170, 90);
            _csvBtn.Click += (s, e) => ExportCsv();
            _filterCard.Controls.Add(_csvBtn);

            _pdfBtn = MakeSecondaryButton("⬇  Export PDF", 320, 90);
            _pdfBtn.Click += (s, e) => ExportPdf();
            _filterCard.Controls.Add(_pdfBtn);

            _modeChartTableBtn = MakeModeButton("Chart + Table", ViewMode.ChartAndTable);
            _modeChartBtn = MakeModeButton("Chart Only", ViewMode.ChartOnly);
            _modeTableBtn = MakeModeButton("Table Only", ViewMode.TableOnly);
            _filterCard.Controls.Add(_modeChartTableBtn);
            _filterCard.Controls.Add(_modeChartBtn);
            _filterCard.Controls.Add(_modeTableBtn);

            _filterCard.Resize += (s, e) =>
            {
                int bx = _filterCard.ClientSize.Width - 20;
                foreach (var b in new[] { _modeTableBtn, _modeChartBtn, _modeChartTableBtn })
                {
                    b.Location = new Point(bx - b.Width, 90);
                    bx -= b.Width + 6;
                }
            };
        }

        private ComboBox AddFilter(string caption, int x, int width, params object[] items)
        {
            _filterCard.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = FCaption,
                Location = new Point(x, 14),
                AutoSize = true
            });
            var cb = new ComboBox
            {
                Location = new Point(x, 34),
                Width = width,
                Font = FCombo,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            cb.Items.AddRange(items);
            cb.SelectedIndex = 0;
            _filterCard.Controls.Add(cb);
            return cb;
        }

        // ---------------- KPIs ----------------
        private void BuildKpiRow()
        {
            _kpiRow = Section(new Panel { BackColor = PageBg }, KpiRowHeight);

            _kpiTxn = AddKpiCard("Total Transactions", out _kpiTxnSub, "NavigateToServiceRequests");
            _kpiRev = AddKpiCard("Total Revenue", out _kpiRevSub, "Paid");
            _kpiTicket = AddKpiCard("Avg. Ticket Size", out _kpiTicketSub, "Completed");
            _kpiPending = AddKpiCard("Pending / Cancelled", out _kpiPendingSub, "NotCompleted");

            _kpiRow.Resize += (s, e) =>
            {
                int n = _kpiCards.Count;
                if (n == 0) return;
                int cardW = Math.Max(120, (_kpiRow.Width - SectionGap * (n - 1)) / n);
                for (int i = 0; i < n; i++)
                    _kpiCards[i].SetBounds(i * (cardW + SectionGap), 0, cardW, _kpiRow.Height);
            };
        }

        private Label AddKpiCard(string title, out Label subtitle, string filterKey)
        {
            var card = new CardPanel();
            _kpiRow.Controls.Add(card);
            _kpiCards.Add(card);

            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Muted,
                Font = FCombo,
                Location = new Point(20, 16),
                AutoSize = true
            });

            var val = new Label
            {
                Text = "0",
                ForeColor = Navy,
                Font = FKpiValue,
                Location = new Point(20, 42),
                AutoSize = true
            };
            card.Controls.Add(val);

            subtitle = new Label
            {
                Text = "",
                ForeColor = Muted,
                Font = FSmall,
                Location = new Point(20, 96),
                AutoSize = true
            };
            card.Controls.Add(subtitle);

            if (!string.IsNullOrEmpty(filterKey))
            {
                string captured = filterKey;
                BindClick(card, () => ApplyGridFilter(captured));
            }

            return val;
        }

        // ---------------- Charts ----------------
        private void BuildChartsRow()
        {
            _chartsRow = Section(new Panel { BackColor = PageBg }, 340);

            _lineChart = new ReportLineChart { BackColor = Color.White, LineColor = Blue };
            _barChart = new ReportBarChart { BackColor = Color.White, BarColor = Color.FromArgb(0x22, 0xA0, 0x66) };

            var lineCard = MakeChartCard("Revenue by Month", _lineChart, "View Reports");
            var barCard = MakeChartCard("Revenue by Service", _barChart, "Manage Services");

            _chartsRow.Resize += (s, e) =>
            {
                int leftW = (_chartsRow.Width - SectionGap) / 2;
                lineCard.SetBounds(0, 0, leftW, _chartsRow.Height);
                barCard.SetBounds(leftW + SectionGap, 0, _chartsRow.Width - SectionGap - leftW, _chartsRow.Height);
            };
        }

        private CardPanel MakeChartCard(string title, Control chart, string clickModule)
        {
            var card = new CardPanel();
            _chartsRow.Controls.Add(card);
            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Navy,
                Font = FTitle,
                Location = new Point(20, 16),
                AutoSize = true
            });
            card.Controls.Add(chart);
            Inset(card, chart, 20, 50, 20, 16);

            if (!string.IsNullOrEmpty(clickModule))
            {
                string captured = clickModule;
                BindClick(card, () => Navigate(captured));
            }

            return card;
        }

        // ---------------- Table ----------------
        private void BuildTableCard()
        {
            _tableCard = Section(new CardPanel(), 320);

            _tableHeaderLbl = new Label
            {
                Text = "Service & Revenue Report",
                ForeColor = Muted,
                Font = FItalic,
                Location = new Point(20, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            _tableCard.Controls.Add(_tableHeaderLbl);

            // ---- Drill-down filter chip (hidden until a filter is set) ----
            _filterChipBar = new Panel
            {
                Location = new Point(20, 40),
                Height = 34,
                BackColor = Color.White,
                Visible = false
            };
            _tableCard.Controls.Add(_filterChipBar);

            _filterChip = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(260, 30),
                BackColor = Color.FromArgb(0xE3, 0xF1, 0xFD),
                Cursor = Cursors.Hand
            };
            _filterChip.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(
                    new Rectangle(0, 0, _filterChip.Width - 1, _filterChip.Height - 1), 15);
                using var fill = new SolidBrush(_filterChip.BackColor);
                e.Graphics.FillPath(fill, path);
            };
            _filterChip.Click += (s, e) => ClearGridFilter();

            _filterChipLabel = new Label
            {
                Text = "",
                ForeColor = Blue,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9f),
                AutoSize = false,
                Location = new Point(14, 0),
                Size = new Size(210, 30),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _filterChip.Controls.Add(_filterChipLabel);

            var clearLbl = new Label
            {
                Text = "×",
                ForeColor = Blue,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 12f),
                AutoSize = false,
                Location = new Point(230, 0),
                Size = new Size(24, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            clearLbl.Click += (s, e) => ClearGridFilter();
            _filterChip.Controls.Add(clearLbl);

            _filterChipBar.Controls.Add(_filterChip);

            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xF7, 0xFA, 0xFD),
                    ForeColor = Muted,
                    Font = FGridHeader,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0)
                },
                ColumnHeadersHeight = 40,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 44 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = FBody,
                    ForeColor = Navy,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(12, 0, 0, 0)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xFA, 0xFC, 0xFF),
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ScrollBars = ScrollBars.Vertical
            };
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(_grid, true);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Txn", HeaderText = "Txn #", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "Customer", Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Vehicle", HeaderText = "Vehicle", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "Service",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });

            var amountCol = new DataGridViewTextBoxColumn
            {
                Name = "Amount",
                HeaderText = "Amount",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "₱#,##0",
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Padding = new Padding(0, 0, 16, 0)
                }
            };
            amountCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            amountCol.HeaderCell.Style.Padding = new Padding(0, 0, 16, 0);
            _grid.Columns.Add(amountCol);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Payment", HeaderText = "Payment", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 120 });

            _grid.CellPainting += Grid_CellPainting;
            _grid.SortCompare += Grid_SortCompare;

            // Click a row → jump to the customer that transaction belongs to.
            _grid.CellMouseClick += Grid_CellMouseClick;

            _tableCard.Controls.Add(_grid);

            _emptyLbl = new Label
            {
                Text = "No transactions match these filters.\nTry a wider date range or reset the service and vehicle filters.",
                ForeColor = Muted,
                Font = FCombo,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            _tableCard.Controls.Add(_emptyLbl);
            _emptyLbl.BringToFront();

            LayoutTableCard();
            _tableCard.Resize += (s, e) => LayoutTableCard();
        }

        private void LayoutTableCard()
        {
            if (_tableCard == null || _grid == null) return;

            int top = _filterChipBar != null && _filterChipBar.Visible ? 80 : 44;
            _grid.SetBounds(20, top,
                Math.Max(0, _tableCard.ClientSize.Width - 40),
                Math.Max(0, _tableCard.ClientSize.Height - top - 20));

            if (_emptyLbl != null)
            {
                _emptyLbl.Location = new Point(
                    Math.Max(0, (_tableCard.Width - _emptyLbl.Width) / 2),
                    Math.Max(top + 40, (_tableCard.Height - _emptyLbl.Height) / 2 + 20));
            }
        }

        // ---------------- Extra host ----------------
        private void BuildExtraHost()
        {
            _extraHost = new Panel
            {
                BackColor = PageBg,
                Margin = new Padding(0, 0, 0, SectionGap),
                Visible = false,
                AutoScroll = false
            };
            _flow.Controls.Add(_extraHost);
        }

        private void ResizeExtraHost()
        {
            if (_extraHost == null || !_extraHost.Visible) return;
            int w = Math.Max(760, _flow.ClientSize.Width - _flow.Padding.Horizontal);
            int h = Math.Max(200, _flow.ClientSize.Height - _flow.Padding.Vertical - 40);
            if (_extraHost.Width != w) _extraHost.Width = w;
            if (_extraHost.Height != h) _extraHost.Height = h;
        }

        // ================================================================
        //  REPORT TYPE SWITCHING
        // ================================================================
        private void SwitchReportType()
        {
            var selected = _reportTypeCombo.SelectedItem?.ToString() ?? RevenueReport;
            if (selected == _currentReportType) return;

            _currentReportType = selected;
            bool revenueMode = selected == RevenueReport;

            if (_revenueWidgets.Count == 0)
            {
                _revenueWidgets.Add(_kpiRow);
                _revenueWidgets.Add(_chartsRow);
                _revenueWidgets.Add(_tableCard);
            }

            if (_currentExtra != null)
            {
                _extraHost.Controls.Remove(_currentExtra);
                _currentExtra.Dispose();
                _currentExtra = null;
            }

            if (revenueMode)
            {
                _flow.Controls.Remove(_extraHost);
                _extraHost.Visible = false;

                int idx = _flow.Controls.IndexOf(_filterCard) + 1;
                foreach (var w in _revenueWidgets)
                {
                    if (!_flow.Controls.Contains(w))
                    {
                        _flow.Controls.Add(w);
                        _flow.Controls.SetChildIndex(w, idx++);
                    }
                }

                SetMode(_mode);
                _ = LoadAsync();
                return;
            }

            foreach (var w in _revenueWidgets)
                _flow.Controls.Remove(w);

            if (!_flow.Controls.Contains(_extraHost))
            {
                _flow.Controls.Add(_extraHost);
                _flow.Controls.SetChildIndex(_extraHost, _flow.Controls.IndexOf(_filterCard) + 1);
            }
            _extraHost.Visible = true;

            UserControl sub = selected switch
            {
                ComplaintReport => new ComplaintFeedbackReportView(),
                CustomerActivityReport => new CustomerActivityReportView(),
                RetentionReport => new RetentionSummaryReportView(),
                _ => null
            };

            if (sub != null)
            {
                sub.Dock = DockStyle.Fill;
                _extraHost.Controls.Add(sub);
                _currentExtra = sub;

                if (sub is IReportView rv)
                {
                    rv.ApplyFilters(
                        _dateRangeCombo.SelectedItem?.ToString() ?? "This Year",
                        _serviceCombo.SelectedItem?.ToString() ?? "All Services",
                        _vehicleCombo.SelectedItem?.ToString() ?? "All Types");

                    switch (_mode)
                    {
                        case ViewMode.ChartAndTable: rv.ShowChartAndTable(); break;
                        case ViewMode.ChartOnly: rv.ShowChartOnly(); break;
                        case ViewMode.TableOnly: rv.ShowTableOnly(); break;
                    }
                }
            }

            FitSections();
            ResizeExtraHost();
        }

        private async Task RunReportAsync()
        {
            if (_currentReportType == RevenueReport)
            {
                await LoadAsync();
                return;
            }

            if (_currentExtra is IReportView rv)
            {
                rv.ApplyFilters(
                    _dateRangeCombo.SelectedItem?.ToString() ?? "This Year",
                    _serviceCombo.SelectedItem?.ToString() ?? "All Services",
                    _vehicleCombo.SelectedItem?.ToString() ?? "All Types");
            }
        }

        // ================================================================
        //  HELPERS
        // ================================================================
        private Button MakePrimaryButton(string text, int x, int y) => new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(140, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Navy,
            ForeColor = Color.White,
            Font = FBtnBold,
            Cursor = Cursors.Hand
        }.Also(b => b.FlatAppearance.BorderSize = 0);

        private Button MakeSecondaryButton(string text, int x, int y) => new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(140, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Navy,
            Font = FBody,
            Cursor = Cursors.Hand
        }.Also(b => b.FlatAppearance.BorderColor = CardBorder);

        private Button MakeModeButton(string text, ViewMode mode)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Font = FBody,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = CardBorder;
            b.Click += (s, e) => SetMode(mode);
            return b;
        }

        private void SetMode(ViewMode m)
        {
            _mode = m;
            HighlightMode(_modeChartTableBtn, m == ViewMode.ChartAndTable);
            HighlightMode(_modeChartBtn, m == ViewMode.ChartOnly);
            HighlightMode(_modeTableBtn, m == ViewMode.TableOnly);

            if (_currentReportType == RevenueReport)
            {
                _chartsRow.Visible = m != ViewMode.TableOnly;
                _tableCard.Visible = m != ViewMode.ChartOnly;
            }
            else if (_currentExtra is IReportView rv)
            {
                switch (m)
                {
                    case ViewMode.ChartAndTable: rv.ShowChartAndTable(); break;
                    case ViewMode.ChartOnly: rv.ShowChartOnly(); break;
                    case ViewMode.TableOnly: rv.ShowTableOnly(); break;
                }
            }
        }

        private static void HighlightMode(Button b, bool active)
        {
            b.BackColor = active ? Color.FromArgb(0xE3, 0xF1, 0xFD) : Color.White;
            b.ForeColor = active ? Blue : Navy;
            b.FlatAppearance.BorderColor = active ? Blue : CardBorder;
        }

        // ================================================================
        //  GRID FILTER (DRILL-DOWN)
        // ================================================================
        private void ApplyGridFilter(string filterKey)
        {
            if (_filterChipBar == null) return;

            // -- Keys that navigate to another module instead of filtering the grid --
            switch (filterKey)
            {
                case "NavigateToServiceRequests":
                    // Preserve the current report's service and vehicle filters
                    // so Service Requests opens with the same scope.
                    string svc = _serviceCombo.SelectedIndex > 0
                        ? _serviceCombo.SelectedItem?.ToString()
                        : null;
                    string veh = _vehicleCombo.SelectedIndex > 0
                        ? _vehicleCombo.SelectedItem?.ToString()
                        : null;

                    (FindForm() as MainForm)?.NavigateToServiceRequests(
                        status: "All",
                        service: svc,
                        vehicle: veh,
                        source: "reports");
                    return;
            }

            // -- Normal in-grid filter path --
            if (_activeGridFilter == filterKey)
            {
                ClearGridFilter();
                return;
            }

            _activeGridFilter = filterKey ?? "";

            bool hasFilter = !string.IsNullOrEmpty(_activeGridFilter);
            _filterChipBar.Visible = hasFilter;

            if (hasFilter)
                _filterChipLabel.Text = "Filtered by: " + DisplayFilterName(_activeGridFilter);

            LayoutTableCard();
            RebindGridRows();
            _filterChipBar.Invalidate(true);
        }

        private void ClearGridFilter()
        {
            _activeGridFilter = "";
            if (_filterChipBar != null) _filterChipBar.Visible = false;
            LayoutTableCard();
            RebindGridRows();
        }

        private static string DisplayFilterName(string key) => key switch
        {
            "Paid" => "Paid",
            "Completed" => "Completed",
            "NotCompleted" => "Pending / Cancelled",
            _ => key
        };

        private IEnumerable<ReportTxnDto> FilteredTransactions()
        {
            IEnumerable<ReportTxnDto> rows = _data?.Transactions ?? new List<ReportTxnDto>();

            switch (_activeGridFilter)
            {
                case "Paid":
                    rows = rows.Where(t => t.Payment == "Paid");
                    break;
                case "Completed":
                    rows = rows.Where(t => t.Status == "Completed");
                    break;
                case "NotCompleted":
                    rows = rows.Where(t => t.Status == "Pending"
                                        || t.Status == "Cancelled"
                                        || t.Status == "InProgress");
                    break;
                case "":
                default:
                    break;
            }

            return rows;
        }

        private void RebindGridRows()
        {
            if (_grid == null || _data == null) return;

            var list = FilteredTransactions().ToList();

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            foreach (var t in list)
            {
                int idx = _grid.Rows.Add(
                    t.Txn, t.Date, t.Customer, t.Vehicle, t.Service,
                    t.Amount, t.Payment, t.Status);

                // Store the tenant customer id on the row so the click handler
                // can drill straight to the right customer.
                _grid.Rows[idx].Tag = t.CustomerId;
            }
            _grid.ClearSelection();
            _grid.ResumeLayout();

            if (_emptyLbl != null) _emptyLbl.Visible = list.Count == 0;

            if (_tableCard != null)
            {
                int topPad = _filterChipBar != null && _filterChipBar.Visible ? 80 : 44;
                _tableCard.Height = Math.Clamp(topPad + 40 + list.Count * 44 + 24, 260, 640);
            }

            LayoutTableCard();

            if (_tableHeaderLbl != null)
            {
                string filterTag = string.IsNullOrEmpty(_activeGridFilter)
                    ? ""
                    : " · Filtered: " + DisplayFilterName(_activeGridFilter);
                _tableHeaderLbl.Text =
                    $"Service & Revenue Report · {_lastSubtitle}{filterTag} · {list.Count:N0} rows";
            }
        }

        // ================================================================
        //  DATA LOAD
        // ================================================================
        private async Task LoadAsync()
        {
            var ct = _cts.Token;
            try
            {
                Cursor = Cursors.WaitCursor;
                _runBtn.Enabled = false;
                _runBtn.Text = "⟳  Running…";

                var range = (_dateRangeCombo.SelectedItem?.ToString() ?? "This Year").Replace(" ", "");
                var service = _serviceCombo.SelectedItem?.ToString() ?? "All Services";
                if (service == "All Services") service = "All";
                var vehicle = _vehicleCombo.SelectedItem?.ToString() ?? "All Types";
                if (vehicle == "All Types") vehicle = "All";

                var url = $"api/reports/service-revenue?companyId={CompanyId}" +
                          $"&range={Uri.EscapeDataString(range)}" +
                          $"&service={Uri.EscapeDataString(service)}" +
                          $"&vehicle={Uri.EscapeDataString(vehicle)}";

                var resp = await _http.GetFromJsonAsync<ReportsResponseDto>(url, ct)
                           ?? new ReportsResponseDto();
                if (IsDisposed) return;
                _data = resp;

                if (_serviceCombo.Items.Count <= 1 && _data.ServiceOptions.Count > 1)
                {
                    _serviceCombo.Items.Clear();
                    _serviceCombo.Items.Add("All Services");
                    foreach (var s in _data.ServiceOptions.Where(x => x != "All"))
                        _serviceCombo.Items.Add(s);
                    _serviceCombo.SelectedIndex = 0;
                }
                if (_vehicleCombo.Items.Count <= 1 && _data.VehicleOptions.Count > 1)
                {
                    _vehicleCombo.Items.Clear();
                    _vehicleCombo.Items.Add("All Types");
                    foreach (var v in _data.VehicleOptions.Where(x => x != "All"))
                        _vehicleCombo.Items.Add(v);
                    _vehicleCombo.SelectedIndex = 0;
                }

                BindData();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                var msg = ex is TaskCanceledException
                    ? "The server took too long to respond. Check that the API is running and try again."
                    : ex.Message;
                MessageBox.Show($"Failed to load report.\n\n{msg}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    Cursor = Cursors.Default;
                    _runBtn.Enabled = true;
                    _runBtn.Text = "▶  Run Report";
                }
            }
        }

        private void BindData()
        {
            double total = Convert.ToDouble(_data.TotalTransactions);
            double done = Convert.ToDouble(_data.Completed);
            double pending = Convert.ToDouble(_data.PendingOrCancelled);
            var months = _data.Months.ToList();

            _kpiTxn.Text = $"{_data.TotalTransactions:N0}";
            _kpiTxnSub.Text = total > 0
                ? $"{_data.Completed:N0} completed ({done / total:P0})"
                : "No transactions";

            _kpiRev.Text = $"₱{_data.TotalRevenue:N0}";
            string revSub = "Paid & completed";
            if (months.Count > 0)
            {
                var peak = months.OrderByDescending(m => m.Value).First();
                if (peak.Value > 0) revSub = $"Peak: {peak.Label} ({ChartUtil.Money(peak.Value)})";
            }
            _kpiRevSub.Text = revSub;

            _kpiTicket.Text = $"₱{_data.AvgTicket:N0}";
            _kpiTicketSub.Text = "Per completed job";

            _kpiPending.Text = $"{_data.PendingOrCancelled:N0}";
            _kpiPending.ForeColor = pending > 0 ? Amber : Navy;
            _kpiPendingSub.Text = total > 0
                ? $"{pending / total:P0} of all jobs not completed"
                : "Jobs not completed";

            _lineChart.Points = months.Select(m => (m.Label, m.Value)).ToList();
            _barChart.Bars = _data.ByService.Select(s => (s.Label, s.Value)).ToList();

            var rangeLabel = _dateRangeCombo.SelectedItem?.ToString() ?? "This Year";
            var filters = new List<string>();
            if (_serviceCombo.SelectedIndex > 0) filters.Add(_serviceCombo.SelectedItem?.ToString() ?? "");
            if (_vehicleCombo.SelectedIndex > 0) filters.Add(_vehicleCombo.SelectedItem?.ToString() ?? "");
            var filterText = filters.Count > 0 ? " · " + string.Join(", ", filters) : "";

            _lastSubtitle = $"{rangeLabel}{filterText} · Generated at {_data.GeneratedAt}";

            RebindGridRows();
        }

        // ================================================================
        //  GRID paint + sort + drill-down click
        // ================================================================
        private void Grid_SortCompare(object? sender, DataGridViewSortCompareEventArgs e)
        {
            if (_grid.Columns[e.Column.Index].Name != "Date") return;
            if (DateTime.TryParse(Convert.ToString(e.CellValue1), out var d1) &&
                DateTime.TryParse(Convert.ToString(e.CellValue2), out var d2))
            {
                e.SortResult = d1.CompareTo(d2);
                e.Handled = true;
            }
        }

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            // Only the Customer column drills into the customer record —
            // other columns don't have a meaningful single target.
            if (col != "Customer") return;

            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is not int customerId || customerId <= 0) return;

            (FindForm() as MainForm)?.NavigateToCustomers(
                segment: "All",
                focusCustomerId: customerId,
                source: "reports");
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;
            if (col != "Payment" && col != "Status") return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (bg, fg) = col == "Payment" ? PaymentColors(text) : StatusColors(text);

            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FStatus,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 12;
            int maxW = Math.Max(10, b.Width - 20);
            int y = b.Y + (b.Height - size.Height) / 2;

            if (TintBehindText)
            {
                int textW = Math.Min(size.Width, maxW);
                using var br = new SolidBrush(bg);
                e.Graphics.FillRectangle(br, new Rectangle(x - 6, y - 3, textW + 12, size.Height + 6));
            }

            TextRenderer.DrawText(e.Graphics, text, FStatus,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private static (Color bg, Color fg) PaymentColors(string s) => s switch
        {
            "Paid" => (Color.FromArgb(0xE4, 0xF5, 0xE8), Color.FromArgb(0x1E, 0x7A, 0x34)),
            "Pending" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Color.FromArgb(0x9A, 0x6A, 0x00)),
            "Partial" => (Color.FromArgb(0xFF, 0xEE, 0xD9), Color.FromArgb(0xC8, 0x6D, 0x00)),
            "Cancelled" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Completed" => (Color.FromArgb(0xE4, 0xF5, 0xE8), Color.FromArgb(0x1E, 0x7A, 0x34)),
            "Pending" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Color.FromArgb(0x9A, 0x6A, 0x00)),
            "Cancelled" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            "InProgress" => (Color.FromArgb(0xE3, 0xF1, 0xFD), Color.FromArgb(0x15, 0x65, 0xC0)),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        // ================================================================
        //  EXPORT
        // ================================================================
        private bool HasRows
        {
            get
            {
                if (_currentReportType == RevenueReport) return FilteredTransactions().Any();
                if (_currentExtra is IExportableReport er) return er.HasData;
                return false;
            }
        }

        private void ExportCsv()
        {
            if (!HasRows)
            {
                MessageBox.Show("There is nothing to export. Run a report that returns data first.",
                    "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = MakeFileName("csv")
                };
                if (sfd.ShowDialog() != DialogResult.OK) return;

                var sb = new StringBuilder();

                if (_currentReportType == RevenueReport)
                {
                    var inv = CultureInfo.InvariantCulture;
                    sb.AppendLine("Txn #,Date,Customer,Vehicle,Service,Amount,Payment,Status");
                    foreach (var t in FilteredTransactions())
                    {
                        sb.AppendLine(
                            $"{Csv(t.Txn)},{Csv(t.Date)},{Csv(t.Customer)},{Csv(t.Vehicle)}," +
                            $"{Csv(t.Service)},{t.Amount.ToString("0.##", inv)},{Csv(t.Payment)},{Csv(t.Status)}");
                    }
                }
                else if (_currentExtra is IExportableReport er)
                {
                    sb.Append(er.BuildCsv());
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Report exported.", "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string Csv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if ("=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private string MakeFileName(string ext)
        {
            string tag = _currentReportType.Replace(" ", "");
            return $"{tag}_{DateTime.Now:yyyyMMdd_HHmm}.{ext}";
        }

        private void ExportPdf()
        {
            if (!HasRows)
            {
                MessageBox.Show("There is nothing to export. Run a report that returns data first.",
                    "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                PrintDocument doc;
                if (_currentReportType == RevenueReport)
                    doc = BuildPrintDocument();
                else if (_currentExtra is IExportableReport er)
                    doc = er.BuildPrintDocument();
                else
                    return;

                doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";

                if (!doc.PrinterSettings.IsValid)
                {
                    MessageBox.Show(
                        "The \"Microsoft Print to PDF\" printer isn't available on this PC, so Print Preview will open instead. " +
                        "From there you can print to any PDF printer.",
                        "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    doc.PrinterSettings.PrinterName = new PrinterSettings().PrinterName;
                    using var preview = new PrintPreviewDialog { Document = doc, Width = 1100, Height = 800 };
                    preview.ShowDialog(this);
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = MakeFileName("pdf")
                };
                if (sfd.ShowDialog() != DialogResult.OK) return;

                doc.PrinterSettings.PrintToFile = true;
                doc.PrinterSettings.PrintFileName = sfd.FileName;
                doc.Print();

                MessageBox.Show("PDF saved.", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private PrintDocument BuildPrintDocument()
        {
            var rows = FilteredTransactions().ToList();
            string[] heads = { "Txn #", "Date", "Customer", "Vehicle", "Service", "Amount", "Payment", "Status" };
            float[] weights = { 0.9f, 1.1f, 2.0f, 1.0f, 2.2f, 1.1f, 1.0f, 1.0f };
            const int amountCol = 5;
            const float rowH = 22f;
            int next = 0, page = 0;

            decimal filteredRevenue = rows.Where(t => t.Status == "Completed").Sum(t => t.Amount);
            int filteredCompleted = rows.Count(t => t.Status == "Completed");
            int filteredPending = rows.Count(t => t.Status == "Pending"
                                              || t.Status == "Cancelled"
                                              || t.Status == "InProgress");
            decimal filteredAvg = filteredCompleted > 0
                ? filteredRevenue / filteredCompleted
                : 0m;

            string filterSuffix = string.IsNullOrEmpty(_activeGridFilter)
                ? ""
                : " · Filtered: " + DisplayFilterName(_activeGridFilter);

            var doc = new PrintDocument { DocumentName = "Service & Revenue Report" };
            doc.DefaultPageSettings.Landscape = true;
            doc.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
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
                using var fmtRight = new StringFormat(fmt) { Alignment = StringAlignment.Far };

                g.DrawString("Service & Revenue Report", page == 1 ? titleFont : headFont, ink, area.Left, y);
                y += (page == 1 ? titleFont : headFont).GetHeight(g) + 2;

                if (page == 1)
                {
                    g.DrawString(_lastSubtitle + filterSuffix, subFont, gray, area.Left, y);
                    y += subFont.GetHeight(g) + 6;

                    var summary =
                        $"Transactions: {rows.Count:N0}     Revenue: ₱{filteredRevenue:N0}     " +
                        $"Avg. ticket: ₱{filteredAvg:N0}     Pending / cancelled: {filteredPending:N0}";
                    g.DrawString(summary, headFont, ink, area.Left, y);
                    y += headFont.GetHeight(g) + 14;
                }
                else y += 8;

                float sum = weights.Sum();
                var xs = new float[weights.Length + 1];
                xs[0] = area.Left;
                for (int i = 0; i < weights.Length; i++)
                    xs[i + 1] = xs[i] + area.Width * weights[i] / sum;

                void DrawCells(IList<string> cells, Font f, Brush b, float top)
                {
                    for (int i = 0; i < cells.Count; i++)
                    {
                        var r = new RectangleF(xs[i] + 6, top, xs[i + 1] - xs[i] - 12, rowH);
                        g.DrawString(cells[i], f, b, r, i == amountCol ? fmtRight : fmt);
                    }
                }

                g.FillRectangle(headBg, area.Left, y, area.Width, rowH);
                DrawCells(heads, headFont, gray, y);
                y += rowH;

                float bottom = area.Bottom - 24f;
                while (next < rows.Count && y + rowH <= bottom)
                {
                    var t = rows[next];
                    DrawCells(new[]
                    {
                        t.Txn ?? "", t.Date ?? "", t.Customer ?? "", t.Vehicle ?? "", t.Service ?? "",
                        $"₱{t.Amount:N0}", t.Payment ?? "", t.Status ?? ""
                    }, bodyFont, ink, y);
                    g.DrawLine(linePen, area.Left, y + rowH, area.Right, y + rowH);
                    y += rowH;
                    next++;
                }

                g.DrawString($"Page {page}", subFont, gray,
                    new RectangleF(area.Left, area.Bottom - 14f, area.Width, 16f),
                    fmtRight);

                e.HasMorePages = next < rows.Count;
            };

            return doc;
        }

        // ================================================================
        //  Rounded-rect helper
        // ================================================================
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

    internal static class ControlExtensions
    {
        public static T Also<T>(this T self, Action<T> action)
        {
            action(self);
            return self;
        }
    }
}