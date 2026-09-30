using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Dialogs;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles
{
    public class ServiceRequestsView : UserControl
    {
        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color AmberSoft = Color.FromArgb(0xFF, 0xF4, 0xDB);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color Slate = Color.FromArgb(0x39, 0x49, 0xAB);
        private static readonly Color SlateSoft = Color.FromArgb(0xE8, 0xEA, 0xF6);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color NeutralSoft = Color.FromArgb(0xEE, 0xF1, 0xF6);
        private static readonly Color HighlightTint = Color.FromArgb(0xFF, 0xF5, 0xCC);

        private static readonly Font FontLine1 = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new Font("Segoe UI", 8.5f);
        private static readonly Font FontPlain = new Font("Segoe UI", 9.5f);
        private static readonly Font FontTag = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontButton = new Font("Segoe UI Semibold", 9f);

        private static readonly bool TintBehindText = false;

        // ---- Layout ----
        private const int PadX = 40;
        private const int TabTop = 114;
        private const int TabH = 44;
        private const int ChipTop = 166;
        private const int ChipH = 40;
        private const int ChipGap = 8;
        private const int DrillDownChipH = 36;
        private const int FilterTop = 214;
        private const int FilterH = 96;
        private const int PagerH = 48;
        private const int PageBottom = 24;

        private const int RowTemplateHeight = 62;

        private const int ActionBtnH = 30;
        private const int DotsBtnW = 40;
        private const int MenuItemH = 34;

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<ServiceRequestDto> _all = new();
        private List<ServiceRequestDto> _filtered = new();
        private List<TenantCustomerDto> _tenantCustomers = new();
        private List<ProductDto> _tenantProducts = new();
        private List<UserDto> _users = new();

        private List<CustomerDto> _customerLookup = new();
        private List<ServiceDto> _serviceLookup = new();

        private enum ListTab { Active, Archived }
        private ListTab _tab = ListTab.Active;

        private string _activeStatus = "All";
        private string _serviceFilter = "";
        private string _vehicleFilter = "";
        private int? _focusRequestId;
        private string _source = "";
        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;
        private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
        private readonly System.Windows.Forms.Timer _highlightTimer = new() { Interval = 3000 };

        private int _page = 1;
        private int _pageSize = 8;
        private bool _relayouting;

        // ---- Drill-down ----
        private Panel _drillDownChipBar = null!;
        private Panel _drillDownChip = null!;
        private Label _drillDownChipLabel = null!;
        private string _drillDownFilter = "";

        // ---- UI ----
        private Panel _contentPanel = null!;
        private Panel _gridHost = null!;
        private DataGridView _grid = null!;
        private Panel _pager = null!;
        private TextBox _searchBox = null!;
        private Button _newRequestBtn = null!;
        private Panel _chipBar = null!;
        private Label _tabActive = null!;
        private Label _tabArchived = null!;
        private Panel _tabUnderline = null!;
        private Panel _tabBar = null!;
        private Panel _filterCard = null!;

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Admin" : SessionUser.FullName;

        public ServiceRequestsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _debounce.Tick += (s, e) => { _debounce.Stop(); ApplyFilter(resetPage: true); };
            _highlightTimer.Tick += (s, e) =>
            {
                _highlightTimer.Stop();
                _focusRequestId = null;
                if (_grid != null) _grid.Invalidate();
            };

            InitializeUI();

            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };

            SessionUser.BranchChanged += () =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    Invoke(async () =>
                    {
                        await LoadLookupsAsync();
                        await LoadRequestsAsync();
                    });
                }
            };

            LostFocus += (s, e) => CloseActiveMenu();
        }

        // ================================================================
        //  UI
        // ================================================================
        private void InitializeUI()
        {
            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Controls.Add(_contentPanel);

            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Service Requests",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 12),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Manage Service Requests",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(PadX, 34),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "SERVICE_REQUESTS — request_id · customer_id · service_id · assigned_staff_id · created_by · status · scheduled_date",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 80),
                AutoSize = true
            });

            _newRequestBtn = new Button
            {
                Text = "+  New Request",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(170, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _newRequestBtn.FlatAppearance.BorderSize = 0;
            _newRequestBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _newRequestBtn.Click += (s, e) => OpenNewRequestDialog();
            _contentPanel.Controls.Add(_newRequestBtn);

            _tabBar = new Panel
            {
                Location = new Point(PadX, TabTop),
                Height = TabH,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _tabBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, _tabBar.Height - 1, _tabBar.Width, _tabBar.Height - 1);
            };
            _contentPanel.Controls.Add(_tabBar);

            const int TabWidth = 170;
            const int TabGap = 40;

            _tabActive = MakeTab("Active Requests", 0, TabWidth);
            _tabArchived = MakeTab("Archived Requests", TabWidth + TabGap, TabWidth);
            _tabBar.Controls.Add(_tabActive);
            _tabBar.Controls.Add(_tabArchived);

            _tabUnderline = new Panel
            {
                Height = 2,
                Width = TabWidth,
                BackColor = Blue,
                Location = new Point(_tabActive.Left, TabH - 2)
            };
            _tabBar.Controls.Add(_tabUnderline);

            _tabActive.Click += async (s, e) =>
            {
                if (_tab == ListTab.Active) return;
                _tab = ListTab.Active;
                StyleTabs();
                await LoadRequestsAsync();
            };
            _tabArchived.Click += async (s, e) =>
            {
                if (_tab == ListTab.Archived) return;
                _tab = ListTab.Archived;
                StyleTabs();
                await LoadRequestsAsync();
            };

            _chipBar = new Panel
            {
                Location = new Point(PadX, ChipTop),
                Height = ChipH,
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            _drillDownChipBar = new Panel
            {
                Location = new Point(PadX, ChipTop + ChipH + ChipGap),
                Height = DrillDownChipH,
                BackColor = PageBg,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_drillDownChipBar);

            _drillDownChip = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(280, 32),
                BackColor = BlueSoft,
                Cursor = Cursors.Hand
            };
            _drillDownChip.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, _drillDownChip.Width - 1, _drillDownChip.Height - 1);
                int radius = 16, d = radius * 2;
                using var path = new GraphicsPath();
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using var fill = new SolidBrush(_drillDownChip.BackColor);
                e.Graphics.FillPath(fill, path);
            };
            _drillDownChip.Click += (s, e) => ClearDrillDown();

            _drillDownChipLabel = new Label
            {
                Text = "",
                ForeColor = Blue,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9f),
                AutoSize = false,
                Location = new Point(14, 0),
                Size = new Size(230, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _drillDownChip.Controls.Add(_drillDownChipLabel);

            var clearLbl = new Label
            {
                Text = "×",
                ForeColor = Blue,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 12f),
                AutoSize = false,
                Location = new Point(250, 0),
                Size = new Size(24, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            clearLbl.Click += (s, e) => ClearDrillDown();
            _drillDownChip.Controls.Add(clearLbl);

            _drillDownChipBar.Controls.Add(_drillDownChip);

            _filterCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, FilterTop),
                Height = FilterH
            };
            _filterCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _filterCard.Width - 1, _filterCard.Height - 1);
            };
            _contentPanel.Controls.Add(_filterCard);

            _filterCard.Controls.Add(new Label
            {
                Text = "Search",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(16, 8),
                AutoSize = true
            });

            var searchWrap = new Panel
            {
                Location = new Point(16, 38),
                Size = new Size(400, 36),
                BackColor = Color.White,
                Padding = new Padding(10, 7, 10, 0)
            };
            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "Search by customer, plate, service or staff",
                Dock = DockStyle.Top
            };
            searchWrap.Controls.Add(_searchBox);

            bool focused = false;
            _searchBox.Enter += (s, e) => { focused = true; searchWrap.Invalidate(); };
            _searchBox.Leave += (s, e) => { focused = false; searchWrap.Invalidate(); };
            searchWrap.Resize += (s, e) => searchWrap.Invalidate();
            searchWrap.Paint += (s, e) =>
            {
                using var pen = new Pen(focused ? Blue : Faint);
                e.Graphics.DrawRectangle(pen, 0, 0, searchWrap.Width - 1, searchWrap.Height - 1);
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _debounce.Stop();
                _debounce.Start();
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _debounce.Stop();
                    ApplyFilter(resetPage: true);
                }
            };
            _filterCard.Controls.Add(searchWrap);

            var searchBtn = new Button
            {
                Text = "Search",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                BackColor = Navy,
                ForeColor = Color.White,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            searchBtn.FlatAppearance.BorderSize = 0;
            searchBtn.Click += (s, e) => ApplyFilter(resetPage: true);
            _filterCard.Controls.Add(searchBtn);

            var refreshBtn = new Button
            {
                Text = "Refresh",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Navy,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            refreshBtn.FlatAppearance.BorderColor = CardBorder;
            StyleOutlineButton(refreshBtn);
            refreshBtn.Click += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
            _filterCard.Controls.Add(refreshBtn);

            _gridHost = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, FilterTop + FilterH + 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_gridHost);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.None,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderBg,
                    ForeColor = Muted,
                    SelectionBackColor = HeaderBg,
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersHeight = 46,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = RowTemplateHeight },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(16, 0, 8, 0),
                    WrapMode = DataGridViewTriState.False
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestId", HeaderText = "ID", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "CUSTOMER",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 200,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "SERVICE",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180,
                FillWeight = 90
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "SCHEDULED", Width = 170, MinimumWidth = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Staff", HeaderText = "STAFF", Width = 150, MinimumWidth = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "PRIORITY", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "ACTIONS",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    Padding = new Padding(0),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseLeave += (s, e) => SetHover(-1);
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.Scroll += (s, e) => CloseActiveMenu();
            _grid.Resize += (s, e) => CloseActiveMenu();
            _grid.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) CloseActiveMenu();
            };
            _gridHost.Controls.Add(_grid);

            _pager = new Panel
            {
                BackColor = Color.White,
                Height = PagerH,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _pager.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, _pager.Width, 0);
            };
            _contentPanel.Controls.Add(_pager);

            _contentPanel.Resize += (s, e) => RelayoutUI();
            RelayoutUI();

            StyleTabs();
        }

        private Label MakeTab(string text, int x, int width) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 10.5f),
            ForeColor = Muted,
            AutoSize = false,
            Size = new Size(width, TabH),
            Location = new Point(x, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.White
        };

        private void StyleTabs()
        {
            bool activeIsActive = _tab == ListTab.Active;

            _tabActive.ForeColor = activeIsActive ? Navy : Muted;
            _tabArchived.ForeColor = activeIsActive ? Muted : Navy;

            if (_tabUnderline != null)
            {
                _tabUnderline.Left = activeIsActive ? _tabActive.Left : _tabArchived.Left;
                _tabUnderline.Width = activeIsActive ? _tabActive.Width : _tabArchived.Width;
            }

            if (_chipBar != null) _chipBar.Visible = activeIsActive;
            if (_newRequestBtn != null) _newRequestBtn.Visible = activeIsActive;
        }

        private static void StyleOutlineButton(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            b.BackColor = Color.White;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE9, 0xEE, 0xF6);
        }

        // ================================================================
        //  STATUS CHIPS
        // ================================================================
        private void BuildChips()
        {
            if (_chipBar == null) return;

            _chipBar.Controls.Clear();

            if (string.IsNullOrWhiteSpace(_activeStatus))
                _activeStatus = "All";

            var statuses = new[] { "All", "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
            int x = 0;

            foreach (var st in statuses)
            {
                var chip = new Button
                {
                    Text = st,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(120, 36),
                    Location = new Point(x, 0),
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    UseVisualStyleBackColor = false
                };

                bool active = st == _activeStatus;
                chip.BackColor = active ? Navy : Color.White;
                chip.ForeColor = active ? Color.White : Navy;
                chip.FlatAppearance.BorderColor = active ? Navy : CardBorder;
                chip.FlatAppearance.BorderSize = 1;

                var captured = st;
                chip.Click += (s, e) =>
                {
                    _activeStatus = captured;
                    _drillDownFilter = "";
                    if (_drillDownChipBar != null) _drillDownChipBar.Visible = false;
                    _serviceFilter = "";
                    _vehicleFilter = "";

                    BuildChips();
                    RelayoutUI();
                    ApplyFilter(resetPage: true);
                };

                _chipBar.Controls.Add(chip);
                x += 130;
            }
        }

        // ================================================================
        //  DRILL-DOWN
        // ================================================================
        public void ApplyDrillDown(string status,
                                   string service = null,
                                   string vehicle = null,
                                   int? focusRequestId = null,
                                   string source = null)
        {
            _drillDownFilter = string.IsNullOrWhiteSpace(status) || status == "All"
                ? ""
                : status;
            _serviceFilter = service ?? "";
            _vehicleFilter = vehicle ?? "";
            _focusRequestId = focusRequestId;
            _source = source ?? "";

            if (!string.IsNullOrEmpty(_drillDownFilter))
            {
                _activeStatus = _drillDownFilter;
                if (_drillDownChipLabel != null)
                    _drillDownChipLabel.Text = "Status: " + _drillDownFilter;
                if (_drillDownChipBar != null)
                    _drillDownChipBar.Visible = _tab == ListTab.Active;
            }
            else
            {
                _activeStatus = "All";
                if (_drillDownChipLabel != null) _drillDownChipLabel.Text = "";
                if (_drillDownChipBar != null) _drillDownChipBar.Visible = false;
            }

            if (!string.IsNullOrEmpty(_drillDownChipLabel?.Text))
            {
                var extra = new List<string>();
                if (!string.IsNullOrEmpty(_serviceFilter)) extra.Add("Service: " + _serviceFilter);
                if (!string.IsNullOrEmpty(_vehicleFilter)) extra.Add("Vehicle: " + _vehicleFilter);
                if (!string.IsNullOrEmpty(_source)) extra.Add("from " + DisplaySource(_source));
                if (extra.Count > 0)
                    _drillDownChipLabel.Text += "  ·  " + string.Join("  ·  ", extra);
            }

            _page = 1;
            BuildChips();
            RelayoutUI();
            ApplyFilter(resetPage: true);
        }

        private static string DisplaySource(string src) => src switch
        {
            "dashboard" => "Dashboard",
            "analytics" => "Analytics",
            "reports" => "Reports",
            _ => src
        };

        public void ClearDrillDown()
        {
            ApplyDrillDown("All");
        }

        // ================================================================
        //  LAYOUT
        // ================================================================
        private void RelayoutUI()
        {
            if (_contentPanel == null || _relayouting) return;
            _relayouting = true;
            try
            {
                int prevW = -1;
                for (int pass = 0; pass < 2; pass++)
                {
                    int before = _contentPanel.ClientSize.Width;
                    if (before == prevW) break;
                    prevW = before;
                    ApplyLayout();
                }
            }
            finally
            {
                _relayouting = false;
            }
        }

        private void ApplyLayout()
        {
            int fullW = _contentPanel.ClientSize.Width;
            int h = _contentPanel.ClientSize.Height;
            if (fullW < 300) return;

            int contentW = Math.Max(0, fullW - 2 * PadX);
            int w = fullW;

            _contentPanel.SuspendLayout();
            try
            {
                _newRequestBtn.Location = new Point(w - _newRequestBtn.Width - PadX, 30);

                _tabBar.SetBounds(PadX, TabTop, contentW, TabH);
                _chipBar.SetBounds(PadX, ChipTop, contentW, ChipH);

                bool showDrill = _drillDownChipBar != null && _drillDownChipBar.Visible;
                int drillAreaH = showDrill ? DrillDownChipH + ChipGap : 0;

                if (_drillDownChipBar != null)
                {
                    _drillDownChipBar.SetBounds(PadX, ChipTop + ChipH + ChipGap,
                        contentW, DrillDownChipH);
                    _drillDownChipBar.Visible = showDrill;
                }

                int filterY = FilterTop + drillAreaH;
                _filterCard.SetBounds(PadX, filterY, contentW, FilterH);

                var refreshBtn = _filterCard.Controls.OfType<Button>()
                    .FirstOrDefault(b => b.Text == "Refresh");
                var searchBtn = _filterCard.Controls.OfType<Button>()
                    .FirstOrDefault(b => b.Text == "Search");
                var searchWrap = _filterCard.Controls.OfType<Panel>()
                    .FirstOrDefault(p => p.Controls.OfType<TextBox>().Any());

                if (refreshBtn != null)
                    refreshBtn.Location = new Point(contentW - 16 - refreshBtn.Width, 38);

                if (searchBtn != null && refreshBtn != null)
                    searchBtn.Location = new Point(refreshBtn.Left - 10 - searchBtn.Width, 38);

                if (searchWrap != null && searchBtn != null)
                    searchWrap.Width = Math.Max(120, searchBtn.Left - 12 - searchWrap.Left);

                int gridTop = filterY + FilterH + 12;

                _pager.SetBounds(PadX, h - PageBottom - PagerH, contentW, PagerH);
                _gridHost.SetBounds(PadX, gridTop, contentW,
                    Math.Max(0, (h - PageBottom - PagerH) - gridTop));
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
                _contentPanel.PerformLayout();
            }

            RecomputePageSize();
            RenderCurrentPage();
            RenderPager(CurrentTotalPages());
        }

        // ================================================================
        //  DATA
        // ================================================================
        private async Task LoadLookupsAsync()
        {
            var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
            var branchId = CarwashServices.Auth.SessionUser.CurrentBranchId;
            var branchQuery = branchId.HasValue && branchId.Value > 0 ? $"?branchId={branchId.Value}" : "";
            var userBranchQuery = branchId.HasValue && branchId.Value > 0 ? $"&branchId={branchId.Value}" : "";

            try
            {
                _tenantCustomers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    $"api/tenant/{companyId}/tenant-customers{branchQuery}") ?? new();

                _customerLookup = _tenantCustomers
                    .Select(c => new CustomerDto
                    {
                        CustomerId = c.TenantCustomerId,
                        BranchId = c.BranchId,
                        FullName = c.CustomerName,
                        Phone = c.ContactNumber,
                        Email = c.EmailAddress,
                        Address = c.Address
                    })
                    .ToList();
            }
            catch { _tenantCustomers = new(); _customerLookup = new(); }

            try
            {
                _tenantProducts = await _http.GetFromJsonAsync<List<ProductDto>>(
                    $"api/tenant/{companyId}/products{branchQuery}") ?? new();

                _serviceLookup = _tenantProducts
                    .Select(p => new ServiceDto
                    {
                        ServiceId = p.ProductId,
                        ServiceName = p.ProductName,
                        Price = p.UnitPrice,
                        DurationMinutes = p.DurationMinutes,
                        BranchId = p.BranchId
                    })
                    .ToList();
            }
            catch { _tenantProducts = new(); _serviceLookup = new(); }

            try { _users = await _http.GetFromJsonAsync<List<UserDto>>($"api/users?companyId={companyId}{userBranchQuery}") ?? new(); }
            catch { _users = new(); }
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                _newRequestBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var url = _tab == ListTab.Active
                    ? $"api/service-requests?companyId={companyId}"
                    : $"api/service-requests/archived?companyId={companyId}";

                if (SessionUser.CurrentBranchId.HasValue && SessionUser.CurrentBranchId.Value > 0)
                {
                    url += $"&branchId={SessionUser.CurrentBranchId.Value}";
                }

                _all = await _http.GetFromJsonAsync<List<ServiceRequestDto>>(url) ?? new();

                BuildChips();
                ApplyFilter(resetPage: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load service requests.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newRequestBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        // ================================================================
        //  FILTER + PAGINATION
        // ================================================================
        private void ApplyFilter(bool resetPage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ApplyFilter(resetPage)));
                return;
            }

            var search = _searchBox.Text?.Trim().ToLower() ?? "";

            var effectiveStatus = !string.IsNullOrWhiteSpace(_drillDownFilter)
                ? _drillDownFilter
                : _activeStatus;

            _filtered = _all.Where(r =>
            {
                if (_tab == ListTab.Active &&
                    !string.IsNullOrWhiteSpace(effectiveStatus) &&
                    effectiveStatus != "All" &&
                    !string.Equals(r.Status, effectiveStatus, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.IsNullOrEmpty(_serviceFilter))
                {
                    var svc = _serviceLookup.FirstOrDefault(s => s.ServiceId == r.ServiceId);
                    if (svc == null || !string.Equals(svc.ServiceName, _serviceFilter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                if (!string.IsNullOrEmpty(_vehicleFilter))
                {
                    var tenantCust = _tenantCustomers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                    if (tenantCust == null || !string.Equals(tenantCust.VehicleType, _vehicleFilter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                if (string.IsNullOrEmpty(search)) return true;

                var cust = _tenantCustomers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                var sv = _serviceLookup.FirstOrDefault(s => s.ServiceId == r.ServiceId);
                var staff = r.AssignedStaffId.HasValue
                    ? _users.FirstOrDefault(u => u.UserId == r.AssignedStaffId.Value)
                    : null;

                var blob = $"{cust?.CustomerName} {cust?.PlateNumber} " +
                           $"{sv?.ServiceName} {staff?.FullName} " +
                           $"{r.Status} {r.Priority}".ToLower();
                return blob.Contains(search);
            }).ToList();

            if (resetPage) _page = 1;

            if (_focusRequestId.HasValue && _tab == ListTab.Active)
            {
                int idx = _filtered.FindIndex(r => r.RequestId == _focusRequestId.Value);
                if (idx >= 0) _page = idx / Math.Max(1, _pageSize) + 1;
            }

            int totalPages = CurrentTotalPages();
            if (_page > totalPages) _page = totalPages;
            if (_page < 1) _page = 1;

            RenderCurrentPage();
            RenderPager(totalPages);
        }

        private int CurrentTotalPages()
        {
            if (_filtered == null) return 1;
            return Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)_pageSize));
        }

        private void RecomputePageSize()
        {
            int headerH = _grid.ColumnHeadersHeight;
            int available = _gridHost.ClientSize.Height - headerH;
            if (available < RowTemplateHeight) available = RowTemplateHeight;
            _pageSize = Math.Max(1, available / RowTemplateHeight);
        }

        private void RenderCurrentPage()
        {
            if (_filtered == null) return;

            _hoverAction = -1;
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            int from = (_page - 1) * _pageSize;
            int take = Math.Min(_pageSize, Math.Max(0, _filtered.Count - from));

            for (int i = from; i < from + take; i++)
            {
                var r = _filtered[i];

                var tenantCust = _tenantCustomers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                var svc = _serviceLookup.FirstOrDefault(s => s.ServiceId == r.ServiceId);
                var staff = r.AssignedStaffId.HasValue
                    ? _users.FirstOrDefault(u => u.UserId == r.AssignedStaffId.Value)
                    : null;

                string custCell;
                if (tenantCust != null)
                {
                    custCell = tenantCust.CustomerName;
                    if (!string.IsNullOrWhiteSpace(tenantCust.EmailAddress))
                        custCell += "\n" + tenantCust.EmailAddress;
                    else if (!string.IsNullOrWhiteSpace(tenantCust.ContactNumber))
                        custCell += "\n" + tenantCust.ContactNumber;
                }
                else
                {
                    custCell = $"id:{r.CustomerId}";
                }

                string svcCell = svc != null
                    ? $"{svc.ServiceName}\nid:{svc.ServiceId}  ·  ₱{svc.Price:N0}"
                    : $"id:{r.ServiceId}";

                string staffCell = staff != null
                    ? $"{staff.FullName}\nid:{staff.UserId}"
                    : (r.AssignedStaffId.HasValue ? $"id:{r.AssignedStaffId}" : "Unassigned");

                int rowIdx = _grid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    svcCell,
                    r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "—",
                    staffCell,
                    string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority,
                    r.Status,
                    "");

                _grid.Rows[rowIdx].Tag = r.RequestId;

                if (_focusRequestId.HasValue && r.RequestId == _focusRequestId.Value)
                {
                    var row = _grid.Rows[rowIdx];
                    row.DefaultCellStyle.BackColor = HighlightTint;
                    row.DefaultCellStyle.SelectionBackColor = HighlightTint;
                    _highlightTimer.Stop();
                    _highlightTimer.Start();
                }
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();

            if (_focusRequestId.HasValue)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Tag is int id && id == _focusRequestId.Value)
                    {
                        _grid.FirstDisplayedScrollingRowIndex = row.Index;
                        break;
                    }
                }
            }
        }

        private void RenderPager(int totalPages)
        {
            _pager.SuspendLayout();
            foreach (Control c in _pager.Controls.OfType<Control>().ToList())
            {
                if (c is Button || c is Label) { _pager.Controls.Remove(c); c.Dispose(); }
            }

            int total = _filtered?.Count ?? 0;
            int from = total == 0 ? 0 : (_page - 1) * _pageSize + 1;
            int to = Math.Min(_page * _pageSize, total);

            _pager.Controls.Add(new Label
            {
                Text = $"Showing {from}–{to} of {total}",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Location = new Point(16, 15)
            });

            if (totalPages > 1)
            {
                int start = Math.Max(1, _page - 2);
                int end = Math.Min(totalPages, start + 4);
                start = Math.Max(1, end - 4);

                var items = new List<(string Text, int Page, bool Active, bool Enabled)>
                {
                    ("‹", _page - 1, false, _page > 1)
                };
                for (int p = start; p <= end; p++) items.Add((p.ToString(), p, p == _page, true));
                items.Add(("›", _page + 1, false, _page < totalPages));

                const int bw = 36, bh = 34, gap = 6;
                int pagerW = _pager.ClientSize.Width;
                int totalButtonsW = items.Count * bw + (items.Count - 1) * gap;
                int x = Math.Max(16, pagerW - 16 - totalButtonsW);

                foreach (var it in items)
                {
                    var b = PagerButton(it.Text, it.Active, it.Enabled);
                    b.SetBounds(x, 7, bw, bh);
                    int target = it.Page;
                    b.Click += (s, e) =>
                    {
                        _page = target;
                        RenderCurrentPage();
                        RenderPager(CurrentTotalPages());
                    };
                    _pager.Controls.Add(b);
                    x += bw + gap;
                }
            }

            _pager.ResumeLayout();
        }

        private static Button PagerButton(string text, bool active, bool enabled)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                BackColor = active ? Navy : Color.White,
                ForeColor = active ? Color.White : Navy,
                Enabled = enabled,
                Cursor = enabled ? Cursors.Hand : Cursors.Default,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = active ? Navy : CardBorder;
            b.FlatAppearance.MouseOverBackColor = active ? Navy : Color.FromArgb(0xF5, 0xF7, 0xFA);
            return b;
        }

        // ================================================================
        //  CELL PAINTING
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "Customer":
                case "Service":
                case "Staff":
                    PaintTwoLine(e, col == "Staff");
                    break;

                case "Status":
                    {
                        var text = Convert.ToString(e.Value) ?? "";
                        var (bg, fg) = StatusColors(text);
                        PaintColoredText(e, text, fg, bg);
                        break;
                    }

                case "Priority":
                    {
                        var text = Convert.ToString(e.Value) ?? "Normal";
                        var (bg, fg) = PriorityColors(text);
                        PaintColoredText(e, text, fg, bg);
                        break;
                    }

                case "Actions":
                    PaintActionsCell(e);
                    break;
            }
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e, bool mutedSecondLine)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            var parts = text.Split('\n');
            var l1 = parts[0];
            var l2 = parts.Length > 1 ? parts[1] : null;

            var b = e.CellBounds;
            int x = b.X + 16;
            int w = Math.Max(10, b.Width - 24);
            var flags = TextFormatFlags.Left | TextFormatFlags.Top |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                        TextFormatFlags.NoPadding;

            if (l2 == null)
            {
                bool unassigned = l1 == "Unassigned";
                var font = unassigned ? FontPlain : FontLine1;
                int top = b.Y + (b.Height - font.Height) / 2;
                TextRenderer.DrawText(e.Graphics, l1, font,
                    new Rectangle(x, top, w, font.Height),
                    unassigned ? Muted : Navy, flags);
            }
            else
            {
                const int gap = 2;
                int h1 = FontLine1.Height;
                int h2 = FontLine2.Height;
                int top = b.Y + (b.Height - (h1 + gap + h2)) / 2;

                TextRenderer.DrawText(e.Graphics, l1, FontLine1,
                    new Rectangle(x, top, w, h1), Navy, flags);

                TextRenderer.DrawText(e.Graphics, l2, FontLine2,
                    new Rectangle(x, top + h1 + gap, w, h2),
                    mutedSecondLine ? Faint : Muted, flags);
            }

            e.Handled = true;
        }

        private static void PaintColoredText(DataGridViewCellPaintingEventArgs e, string text, Color fg, Color bg)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            if (text.Length == 0) { e.Handled = true; return; }

            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontTag,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 16;
            int maxW = Math.Max(10, b.Width - 24);
            int y = b.Y + (b.Height - size.Height) / 2;

            if (TintBehindText)
            {
                int textW = Math.Min(size.Width, maxW);
                using var br = new SolidBrush(bg);
                e.Graphics.FillRectangle(br, new Rectangle(x - 6, y - 3, textW + 12, size.Height + 6));
            }

            TextRenderer.DrawText(e.Graphics, text, FontTag,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var rect = DotsRect(e.CellBounds);
            bool hover = _hoverAction == (e.RowIndex << 2);

            DrawDotsButton(e.Graphics, rect, hover);
            e.Handled = true;
        }

        private static Rectangle DotsRect(Rectangle cell)
        {
            int x = cell.X + (cell.Width - DotsBtnW) / 2;
            int y = cell.Y + (cell.Height - ActionBtnH) / 2;
            return new Rectangle(x, y, DotsBtnW, ActionBtnH);
        }

        private static void DrawDotsButton(Graphics g, Rectangle rect, bool hover)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = hover ? Color.FromArgb(0xEA, 0xF2, 0xFD) : Color.White;
            Color border = hover ? Blue : CardBorder;
            Color dotColor = hover ? Blue : Navy;

            using (var path = RoundedRect(rect, 6))
            using (var fillBrush = new SolidBrush(fill))
            using (var pen = new Pen(border, 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(pen, path);
            }

            const int dotSize = 3;
            int cx = rect.X + rect.Width / 2 - dotSize / 2;
            int cy = rect.Y + rect.Height / 2;
            using var dotBrush = new SolidBrush(dotColor);
            g.FillEllipse(dotBrush, cx, cy - 8, dotSize, dotSize);
            g.FillEllipse(dotBrush, cx, cy - dotSize / 2, dotSize, dotSize);
            g.FillEllipse(dotBrush, cx, cy + 5, dotSize, dotSize);
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

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Completed" => (GreenSoft, Green),
            "Pending" => (AmberSoft, Amber),
            "InProgress" or "In Progress" or "Assigned" => (BlueSoft, Blue),
            "Cancelled" => (RedSoft, Red),
            _ => (NeutralSoft, Muted)
        };

        private static (Color bg, Color fg) PriorityColors(string s) => s switch
        {
            "High" => (RedSoft, Red),
            "VIP" => (SlateSoft, Slate),
            _ => (NeutralSoft, Muted)
        };

        // ================================================================
        //  HOVER + CLICK
        // ================================================================
        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _grid.Columns[e.ColumnIndex].Name != "Actions")
            {
                SetHover(-1);
                return;
            }
            SetHover(HitTestActions(e.RowIndex, e.Location));
        }

        private int HitTestActions(int rowIndex, Point local)
        {
            var cellBounds = _grid.GetCellDisplayRectangle(
                _grid.Columns["Actions"].Index, rowIndex, false);

            var absolute = new Point(cellBounds.X + local.X, cellBounds.Y + local.Y);

            if (DotsRect(cellBounds).Contains(absolute))
                return rowIndex << 2;

            return -1;
        }

        private void SetHover(int encoded)
        {
            if (encoded == _hoverAction) return;

            int old = _hoverAction;
            _hoverAction = encoded;

            var col = _grid.Columns["Actions"];
            if (col != null)
            {
                if (old >= 0) _grid.InvalidateCell(col.Index, old >> 2);
                if (encoded >= 0) _grid.InvalidateCell(col.Index, encoded >> 2);
            }
            _grid.Cursor = encoded >= 0 ? Cursors.Hand : Cursors.Default;
        }

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (_grid.Columns[e.ColumnIndex].Name == "Actions")
            {
                int hit = HitTestActions(e.RowIndex, e.Location);
                if (hit < 0) return;

                var idText = _grid.Rows[e.RowIndex].Cells["RequestId"].Value?.ToString() ?? "";
                if (!int.TryParse(idText.TrimStart('#'), out var id)) return;

                var req = _all.FirstOrDefault(r => r.RequestId == id);
                if (req == null) return;

                ShowActionsMenu(e.RowIndex, req);
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "Customer")
            {
                var idText = _grid.Rows[e.RowIndex].Cells["RequestId"].Value?.ToString() ?? "";
                if (!int.TryParse(idText.TrimStart('#'), out var reqId)) return;

                var req = _all.FirstOrDefault(r => r.RequestId == reqId);
                if (req == null) return;

                (FindForm() as MainForm)?.NavigateToCustomers(
                    segment: "All",
                    focusCustomerId: req.CustomerId,
                    source: "dashboard");
            }
        }

        // ================================================================
        //  ACTIONS MENU
        // ================================================================
        private void ShowActionsMenu(int rowIndex, ServiceRequestDto req)
        {
            CloseActiveMenu();

            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                Padding = new Padding(4),
                Renderer = new ToolStripProfessionalRenderer(new MenuColors())
            };

            if (_tab == ListTab.Active)
            {
                AddMenuItem(menu, "Edit", () => OpenEditRequestDialog(req.RequestId));
                AddMenuItem(menu, "Archive", () => ArchiveRequestAsync(req), isDanger: true);
            }
            else
            {
                AddMenuItem(menu, "Restore", () => RestoreRequestAsync(req));
            }

            _activeMenu = menu;
            _menuRow = rowIndex;

            menu.Closed += (s, e) =>
            {
                if (ReferenceEquals(_activeMenu, menu))
                {
                    _activeMenu = null;
                    _menuRow = -1;
                    _grid.InvalidateCell(_grid.Columns["Actions"].Index, rowIndex);
                }
            };

            var cellBounds = _grid.GetCellDisplayRectangle(
                _grid.Columns["Actions"].Index, rowIndex, false);

            menu.Show(_grid,
                new Point(cellBounds.Right - 8, cellBounds.Bottom - 4),
                ToolStripDropDownDirection.BelowLeft);
        }

        private static void AddMenuItem(ContextMenuStrip menu, string text, Action onClick, bool isDanger = false)
        {
            var item = new ToolStripMenuItem(text)
            {
                ForeColor = isDanger ? Red : Navy,
                AutoSize = false,
                Height = MenuItemH,
                Padding = new Padding(12, 0, 12, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 140
            };
            item.Click += (s, e) =>
            {
                menu.Close();
                onClick();
            };
            menu.Items.Add(item);
        }

        private void CloseActiveMenu()
        {
            if (_activeMenu == null) return;
            var m = _activeMenu;
            _activeMenu = null;
            m.Close();
            if (_menuRow >= 0)
            {
                _grid.InvalidateCell(_grid.Columns["Actions"].Index, _menuRow);
                _menuRow = -1;
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(0xEA, 0xF2, 0xFD);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0xEA, 0xF2, 0xFD);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0xEA, 0xF2, 0xFD);
            public override Color MenuItemBorder => CardBorder;
            public override Color MenuBorder => CardBorder;
        }

        // ================================================================
        //  ACTIONS
        // ================================================================
        private async void OpenNewRequestDialog()
        {
            var staffList = _users.Where(u => u.RoleId == 4).ToList();
            using var dlg = new ServiceRequestEditDialog(null, _customerLookup, _serviceLookup, staffList);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            }
        }

        private async void OpenEditRequestDialog(int id)
        {
            var staffList = _users.Where(u => u.RoleId == 4).ToList();
            using var dlg = new ServiceRequestEditDialog(id, _customerLookup, _serviceLookup, staffList);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            }
        }

        private async void ArchiveRequestAsync(ServiceRequestDto req)
        {
            if (!ArchiveConfirmDialog.ConfirmArchive("service request")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsJsonAsync(
                    $"api/service-requests/{req.RequestId}/archive?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}",
                    new { archivedBy = CurrentUserName });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Archive failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Archive failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private async void RestoreRequestAsync(ServiceRequestDto req)
        {
            if (!ArchiveConfirmDialog.ConfirmRestore("service request")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsync(
                    $"api/service-requests/{req.RequestId}/restore?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}", null);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Restore failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }
    }
}