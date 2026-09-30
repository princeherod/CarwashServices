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
    public class FollowUpsView : UserControl
    {
        private HttpClient _http;

        private List<FollowUpDto> _all = new();
        private List<TenantCustomerDto> _customers = new();
        private List<UserDto> _users = new();
        private Dictionary<int, TenantCustomerDto> _custById = new();
        private Dictionary<int, UserDto> _userById = new();

        private int _pageSize = 8;
        private int _page = 1;
        private FollowUpStatsDto _stats = new();
        private string _search = "";
        private string _statusFilter = "All statuses";

        private enum ListTab { Active, Archived }
        private ListTab _tab = ListTab.Active;

        // ---- Drill-down ----
        private string _drillDownFilter = "";
        private int? _focusFollowUpId;
        private string _source = "";
        private Panel _drillDownHost = null!;
        private Panel _drillDownChip = null!;
        private Label _drillDownChipLabel = null!;
        private readonly System.Windows.Forms.Timer _highlightTimer = new() { Interval = 3000 };

        private Panel _contentPanel = null!;
        private Panel _statsBar = null!;
        private DataGridView _grid = null!;
        private Panel _pagerBar = null!;
        private DataGridView _logGrid = null!;
        private Label _showingLbl = null!;
        private Panel _gridCard = null!;
        private Panel _logCard = null!;
        private Panel _header = null!;
        private Button _addBtn = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterCombo = null!;

        private Label _tabActive = null!;
        private Label _tabArchived = null!;
        private Panel _tabUnderline = null!;
        private Panel _tabBar = null!;

        private Label _breadcrumb = null!;
        private Label _sectionLbl = null!;
        private Label _logTitleLbl = null!;
        private Label _logSubLbl = null!;
        private bool _relayouting;
        private int _statsLayoutWidth = -1;
        private int _logRowCount = 0;

        private bool _uiReady = false;

        private const int MarginX = 40;
        private const int TopMargin = 20;

        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);

        private static readonly Color GreenDot = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color YellowDot = Color.FromArgb(0xF5, 0xB0, 0x2E);
        private static readonly Color BlueDot = Color.FromArgb(0x42, 0xA5, 0xF5);
        private static readonly Color RedDot = Color.FromArgb(0xE5, 0x39, 0x35);

        private static readonly Color HighlightTint = Color.FromArgb(0xFF, 0xF5, 0xCC);

        private static readonly Font FontStrong = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontNormal = new Font("Segoe UI", 9.5f);
        private static readonly Font FontSub = new Font("Segoe UI", 8.5f);
        private static readonly Font FontPill = new Font("Segoe UI Semibold", 8.5f);
        private static readonly Font FontAction = new Font("Segoe UI Semibold", 8.5f);
        private static readonly Font FontStatTitle = new Font("Segoe UI", 10f);
        private static readonly Font FontStatValue = new Font("Segoe UI Semibold", 24f);

        private static readonly bool TintBehindText = false;

        private const int MinCustomer = 180;
        private const int MinCreatedBy = 130;
        private const int MinScheduled = 120;
        private const int MinReason = 180;
        private const int MinDiscount = 130;
        private const int MinSendVia = 80;
        private const int MinStatus = 110;
        private const int MinActions = 70;
        private const int MinArchived = 130;

        private const float WCustomer = 22f;
        private const float WCreatedBy = 14f;
        private const float WScheduled = 15f;
        private const float WReason = 18f;
        private const float WDiscount = 17f;
        private const float WSendVia = 8f;
        private const float WStatus = 10f;
        private const float WActions = 6f;

        private const int LogMinNum = 60;
        private const int LogMinCustomer = 160;
        private const int LogMinType = 140;
        private const int LogMinMethod = 130;
        private const int LogMinScheduled = 110;
        private const int LogMinStatus = 110;
        private const int LogMinNotes = 180;

        private const float LogWNum = 6f;
        private const float LogWCustomer = 18f;
        private const float LogWType = 16f;
        private const float LogWMethod = 14f;
        private const float LogWScheduled = 12f;
        private const float LogWStatus = 12f;
        private const float LogWNotes = 22f;

        private const int ActionBtnH = 28;
        private const int DotsBtnW = 40;
        private const int MenuItemH = 34;

        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Admin" : SessionUser.FullName;

        public FollowUpsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5180/"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            _highlightTimer.Tick += (s, e) =>
            {
                _highlightTimer.Stop();
                _focusFollowUpId = null;
                if (_grid != null) _grid.Invalidate();
            };

            InitializeUI();

            Sidebar.EnableDoubleBuffering(this);

            _uiReady = true;

            LostFocus += (s, e) => CloseActiveMenu();
        }

        private sealed class BufferedGrid : DataGridView
        {
            public BufferedGrid() { DoubleBuffered = true; }
        }

        private sealed class RoundedPanel : Panel
        {
            public RoundedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(PageBg);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = RoundedRect(r, 10);
                using var fill = new SolidBrush(Color.White);
                using var pen = new Pen(CardBorder, 1);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private DataGridView CreateGrid(int rowHeight)
        {
            var g = new BufferedGrid
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
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
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 46,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
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
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ScrollBars = ScrollBars.Vertical
            };
            g.RowTemplate.Height = rowHeight;
            return g;
        }

        private void InitializeUI()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(0, 0, 0, 24),
                AutoScroll = true
            };
            Controls.Add(_contentPanel);

            _breadcrumb = new Label
            {
                Text = "Modules  ›  Follow-Ups / Reminders",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_breadcrumb);

            _header = new Panel { Height = 92, BackColor = Color.Transparent };
            _contentPanel.Controls.Add(_header);

            _header.Controls.Add(new Label
            {
                Text = "Follow-ups / Reminders",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            _header.Controls.Add(new Label
            {
                Text = "Send discount offers and reminders to keep customers coming back.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(2, 50),
                AutoSize = true
            });

            _addBtn = new Button
            {
                Text = "+  Add Follow-up",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(190, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _addBtn.FlatAppearance.BorderSize = 0;
            _addBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _addBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x06, 0x0E, 0x22);
            _addBtn.Click += (s, e) => OpenAddDialog();
            _header.Controls.Add(_addBtn);

            _tabBar = new Panel { Height = 44, BackColor = Color.White };
            _tabBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, _tabBar.Height - 1, _tabBar.Width, _tabBar.Height - 1);
            };
            _contentPanel.Controls.Add(_tabBar);

            const int TabWidth = 180;
            const int TabGap = 40;

            _tabActive = MakeTab("Active Follow-Ups", 0, TabWidth);
            _tabArchived = MakeTab("Archived", TabWidth + TabGap, TabWidth);
            _tabBar.Controls.Add(_tabActive);
            _tabBar.Controls.Add(_tabArchived);

            _tabUnderline = new Panel
            {
                Height = 2,
                Width = TabWidth,
                BackColor = Navy,
                Location = new Point(_tabActive.Left, 42)
            };
            _tabBar.Controls.Add(_tabUnderline);

            _tabActive.Click += async (s, e) =>
            {
                if (_tab == ListTab.Active) return;
                _tab = ListTab.Active;
                StyleTabs();
                await LoadAsync();
            };
            _tabArchived.Click += async (s, e) =>
            {
                if (_tab == ListTab.Archived) return;
                _tab = ListTab.Archived;
                StyleTabs();
                await LoadAsync();
            };

            _drillDownHost = new Panel { Height = 36, BackColor = PageBg, Visible = false };
            _contentPanel.Controls.Add(_drillDownHost);

            _drillDownChip = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(300, 32),
                BackColor = BlueSoft,
                Cursor = Cursors.Hand
            };
            _drillDownChip.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(
                    new Rectangle(0, 0, _drillDownChip.Width - 1, _drillDownChip.Height - 1), 16);
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
                Size = new Size(250, 32),
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
                Location = new Point(270, 0),
                Size = new Size(24, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            clearLbl.Click += (s, e) => ClearDrillDown();
            _drillDownChip.Controls.Add(clearLbl);

            _drillDownHost.Controls.Add(_drillDownChip);

            _statsBar = new Panel { Height = 100, BackColor = Color.Transparent };
            _contentPanel.Controls.Add(_statsBar);

            _sectionLbl = new Label
            {
                Text = "Customers to follow up",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_sectionLbl);

            _searchBox = new TextBox
            {
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Search customer..."
            };
            _contentPanel.Controls.Add(_searchBox);
            _searchBox.TextChanged += (s, e) =>
            {
                if (!_uiReady) return;
                _search = _searchBox.Text?.Trim().ToLower() ?? "";
                _page = 1;
                ApplyFilter();
            };

            _statusFilterCombo = new ComboBox
            {
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterCombo.Items.AddRange(new object[]
            {
                "All statuses", "Draft", "Pending Approval", "Approved", "Scheduled",
                "Due today", "Sent", "Redeemed", "Rejected", "Expired"
            });

            _statusFilterCombo.SelectedIndexChanged += (s, e) =>
            {
                if (!_uiReady) return;
                _statusFilter = _statusFilterCombo.SelectedItem?.ToString() ?? "All statuses";

                if (!string.IsNullOrEmpty(_drillDownFilter))
                {
                    _drillDownFilter = "";
                    if (_drillDownHost != null) _drillDownHost.Visible = false;
                    RelayoutUI();
                }

                _page = 1;
                ApplyFilter();
            };
            _statusFilterCombo.SelectedIndex = 0;

            _contentPanel.Controls.Add(_statusFilterCombo);

            _gridCard = new Panel
            {
                BackColor = CardBorder,
                Padding = new Padding(1)
            };
            _contentPanel.Controls.Add(_gridCard);

            _grid = CreateGrid(62);
            BuildGridColumns();

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellMouseLeave += (s, e) => SetHover(-1);
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.Scroll += (s, e) => CloseActiveMenu();
            _grid.Resize += (s, e) => CloseActiveMenu();
            _grid.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) CloseActiveMenu(); };
            _gridCard.Controls.Add(_grid);

            _pagerBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 8)
            };
            _pagerBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, _pagerBar.Width, 0);
            };
            _gridCard.Controls.Add(_pagerBar);
            _pagerBar.SendToBack();

            _showingLbl = new Label
            {
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                Location = new Point(16, 18)
            };
            _pagerBar.Controls.Add(_showingLbl);

            _logTitleLbl = new Label
            {
                Text = "Follow-up Log",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_logTitleLbl);

            _logSubLbl = new Label
            {
                Text = "View and manage all customer follow-up activities.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_logSubLbl);

            _logCard = new Panel
            {
                BackColor = CardBorder,
                Padding = new Padding(1)
            };
            _contentPanel.Controls.Add(_logCard);

            _logGrid = CreateGrid(54);
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Num",
                HeaderText = "#",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinNum,
                FillWeight = LogWNum
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "Customer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinCustomer,
                FillWeight = LogWCustomer
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Type",
                HeaderText = "Type",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinType,
                FillWeight = LogWType
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Method",
                HeaderText = "Contact Method",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinMethod,
                FillWeight = LogWMethod
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Scheduled",
                HeaderText = "Scheduled",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinScheduled,
                FillWeight = LogWScheduled
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinStatus,
                FillWeight = LogWStatus
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Notes",
                HeaderText = "Notes",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = LogMinNotes,
                FillWeight = LogWNotes
            });

            _logGrid.CellPainting += LogGrid_CellPainting;
            _logCard.Controls.Add(_logGrid);

            _contentPanel.Resize += (s, e) => RelayoutUI();

            Load += async (s, e) =>
            {
                RelayoutUI();
                await LoadLookupsAsync();
                await LoadAsync();
                BeginInvoke(new Action(RelayoutUI));
            };
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FollowUpId", Visible = false });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "Customer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinCustomer,
                FillWeight = WCustomer
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedBy",
                HeaderText = "Created by",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinCreatedBy,
                FillWeight = WCreatedBy
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Scheduled",
                HeaderText = "Scheduled / Send On",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinScheduled,
                FillWeight = WScheduled
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Reason",
                HeaderText = "Reason",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinReason,
                FillWeight = WReason
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Discount",
                HeaderText = "Discount offer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinDiscount,
                FillWeight = WDiscount
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SendVia",
                HeaderText = "Send via",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinSendVia,
                FillWeight = WSendVia
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinStatus,
                FillWeight = WStatus
            });

            if (_tab == ListTab.Archived)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "ArchivedInfo",
                    HeaderText = "Archived",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = MinArchived,
                    FillWeight = 14f
                });
            }

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "Actions",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinActions,
                FillWeight = WActions,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Navy,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(0),
                    WrapMode = DataGridViewTriState.False
                }
            });
        }

        private Label MakeTab(string text, int x, int width) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 10.5f),
            ForeColor = Muted,
            AutoSize = false,
            Size = new Size(width, 42),
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

            if (_addBtn != null) _addBtn.Visible = activeIsActive;
            if (_statsBar != null) _statsBar.Visible = activeIsActive;
            if (_drillDownHost != null)
                _drillDownHost.Visible = activeIsActive && !string.IsNullOrEmpty(_drillDownFilter);
        }

        // ================================================================
        //  DRILL-DOWN
        // ================================================================
        public void ApplyDrillDown(string status, int? focusFollowUpId = null, string source = null)
        {
            _drillDownFilter = string.IsNullOrWhiteSpace(status) || status == "All" ? "" : status;
            _focusFollowUpId = focusFollowUpId;
            _source = source ?? "";

            if (!string.IsNullOrEmpty(_drillDownFilter))
            {
                var chipText = "Status: " + _drillDownFilter;
                if (!string.IsNullOrEmpty(_source))
                    chipText += "  ·  from " + DisplaySource(_source);
                _drillDownChipLabel.Text = chipText;
                _drillDownHost.Visible = _tab == ListTab.Active;

                var match = _statusFilterCombo.Items.Cast<object>()
                    .FirstOrDefault(x => string.Equals(x.ToString(), _drillDownFilter,
                                                       StringComparison.OrdinalIgnoreCase));
                if (match != null) _statusFilterCombo.SelectedItem = match;
            }
            else
            {
                _drillDownChipLabel.Text = "";
                _drillDownHost.Visible = false;
                _statusFilterCombo.SelectedItem = "All statuses";
            }

            _statusFilter = string.IsNullOrEmpty(_drillDownFilter) ? "All statuses" : _drillDownFilter;
            _page = 1;
            RelayoutUI();
            ApplyFilter();
        }

        private static string DisplaySource(string src) => src switch
        {
            "dashboard" => "Dashboard",
            "analytics" => "Analytics",
            "reports" => "Reports",
            _ => src
        };

        public void ClearDrillDown() { ApplyDrillDown("All"); }

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
            finally { _relayouting = false; }
        }

        private void ApplyLayout()
        {
            int fullW = _contentPanel.ClientSize.Width;
            int w = fullW - MarginX * 2;
            if (w < 300) return;

            _contentPanel.SuspendLayout();
            try
            {
                var off = _contentPanel.AutoScrollPosition;
                int L = MarginX + off.X;
                int y = TopMargin + off.Y;

                _breadcrumb.Location = new Point(L, y);
                y += 28;

                _header.SetBounds(L, y, w, 92);
                _addBtn.Location = new Point(w - _addBtn.Width, 14);
                y += 92 + 8;

                _tabBar.SetBounds(L, y, w, 44);
                y += 44 + 20;

                bool showDrill = _drillDownHost != null && _drillDownHost.Visible;
                if (_drillDownHost != null)
                {
                    _drillDownHost.SetBounds(L, y, w, showDrill ? 36 : 0);
                    _drillDownHost.Visible = showDrill;
                }
                if (showDrill) y += 36 + 12;

                _statsBar.SetBounds(L, y, w, 100);
                if (_statsLayoutWidth != w) BuildStats();
                y += 100 + 34;

                _sectionLbl.Location = new Point(L, y);
                _statusFilterCombo.Width = 190;
                _statusFilterCombo.Location = new Point(L + w - 190, y + 1);
                _searchBox.Width = 260;
                _searchBox.Location = new Point(L + w - 190 - 12 - 260, y + 1);
                y += 44;

                int gridH = 2 + _grid.ColumnHeadersHeight + _pageSize * _grid.RowTemplate.Height + _pagerBar.Height + 2;
                _gridCard.SetBounds(L, y, w, gridH);
                y += gridH + 36;

                _logTitleLbl.Location = new Point(L, y);
                y += 34;
                _logSubLbl.Location = new Point(L + 2, y);
                y += 32;

                int logH = 2 + _logGrid.ColumnHeadersHeight + Math.Max(3, _logRowCount) * _logGrid.RowTemplate.Height + 2;
                _logCard.SetBounds(L, y, w, logH);
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
                _contentPanel.PerformLayout();
            }
        }

        // ================================================================
        //  STATS
        // ================================================================
        private void BuildStats()
        {
            if (_statsBar == null) return;

            foreach (var old in _statsBar.Controls.Cast<Control>().ToList())
            {
                _statsBar.Controls.Remove(old);
                old.Dispose();
            }

            int gap = 20;
            int cardW = (_statsBar.Width - gap * 3) / 4;
            if (cardW < 100) cardW = 100;

            string[] titles = { "Due today", "Offers sent", "Discounts redeemed", "Expired" };
            int[] values = { _stats.DueToday, _stats.OffersSent, _stats.Redeemed, _stats.Expired };
            Color[] dots = { YellowDot, BlueDot, GreenDot, RedDot };
            string[] filterTargets = { "Due today", "Sent", "Redeemed", "Expired" };

            for (int i = 0; i < 4; i++)
            {
                var card = new RoundedPanel
                {
                    Location = new Point(i * (cardW + gap), 0),
                    Size = new Size(cardW, 100),
                    Cursor = Cursors.Hand
                };

                string target = filterTargets[i];
                card.Click += (s, e) => ApplyDrillDown(target);
                foreach (Control child in card.Controls)
                    child.Click += (s, e) => ApplyDrillDown(target);

                var dot = new Panel
                {
                    Location = new Point(24, 27),
                    Size = new Size(10, 10),
                    BackColor = Color.White,
                    Tag = dots[i]
                };
                dot.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var br = new SolidBrush((Color)((Panel)s).Tag);
                    e.Graphics.FillEllipse(br, 0, 0, 9, 9);
                };
                card.Controls.Add(dot);

                var titleLbl = new Label
                {
                    Text = titles[i],
                    ForeColor = Muted,
                    BackColor = Color.White,
                    Font = FontStatTitle,
                    Location = new Point(42, 21),
                    AutoSize = true
                };
                card.Controls.Add(titleLbl);

                var valueLbl = new Label
                {
                    Text = values[i].ToString(),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    Font = FontStatValue,
                    Location = new Point(22, 42),
                    AutoSize = true
                };
                card.Controls.Add(valueLbl);

                titleLbl.Click += (s, e) => ApplyDrillDown(target);
                valueLbl.Click += (s, e) => ApplyDrillDown(target);

                _statsBar.Controls.Add(card);
            }

            _statsLayoutWidth = _statsBar.Width;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ================================================================
        //  CELL PAINTING
        // ================================================================
        private const DataGridViewPaintParts BaseParts =
            DataGridViewPaintParts.Background |
            DataGridViewPaintParts.Border |
            DataGridViewPaintParts.SelectionBackground;

        private static void PaintHeader(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.All);
            using var pen = new Pen(CardBorder);
            e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                e.CellBounds.Right, e.CellBounds.Bottom - 1);
            e.Handled = true;
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e,
            Font first, Color firstColor, Font second, Color secondColor)
        {
            e.Paint(e.CellBounds, BaseParts);

            var text = Convert.ToString(e.Value) ?? "";
            var parts = text.Split('\n');
            var l1 = parts[0];
            var l2 = parts.Length > 1 ? parts[1] : null;

            var b = e.CellBounds;
            int x = b.X + 16;
            int w = Math.Max(10, b.Width - 24);
            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

            if (l2 == null)
            {
                TextRenderer.DrawText(e.Graphics, l1, first,
                    new Rectangle(x, b.Y, w, b.Height), firstColor, flags);
            }
            else
            {
                int h1 = first.Height, h2 = second.Height, gap = 2;
                int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;
                TextRenderer.DrawText(e.Graphics, l1, first,
                    new Rectangle(x, top, w, h1), firstColor, flags);
                TextRenderer.DrawText(e.Graphics, l2, second,
                    new Rectangle(x, top + h1 + gap, w, h2), secondColor, flags);
            }

            e.Handled = true;
        }

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Draft" => (Color.FromArgb(0xF1, 0xF4, 0xF9), Muted),
            "Pending Approval" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Color.FromArgb(0x9A, 0x6A, 0x00)),
            "Approved" => (BlueSoft, Color.FromArgb(0x15, 0x65, 0xC0)),
            "Scheduled" => (Color.FromArgb(0xE8, 0xEA, 0xF6), Color.FromArgb(0x39, 0x49, 0xAB)),
            "Due today" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Color.FromArgb(0x9A, 0x6A, 0x00)),
            "Sent" or "Contacted" => (BlueSoft, Color.FromArgb(0x15, 0x65, 0xC0)),
            "Redeemed" => (Color.FromArgb(0xE4, 0xF5, 0xE8), Color.FromArgb(0x1E, 0x7A, 0x34)),
            "Rejected" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            "Expired" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        private static void PaintStatusPill(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, BaseParts);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (bg, fg) = StatusColors(text);

            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontStrong,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 16;
            int maxW = Math.Max(10, b.Width - 24);
            int y = b.Y + (b.Height - size.Height) / 2;

            if (TintBehindText)
            {
                int textW = Math.Min(size.Width, maxW);
                using var br = new SolidBrush(bg);
                e.Graphics.FillRectangle(br, new Rectangle(x - 6, y - 3, textW + 12, size.Height + 6));
            }

            TextRenderer.DrawText(e.Graphics, text, FontStrong,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, BaseParts);

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

            Color fill = hover ? BlueSoft : Color.White;
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

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;

            if (e.RowIndex == -1) { PaintHeader(e); return; }

            var colName = _grid.Columns[e.ColumnIndex].Name;

            switch (colName)
            {
                case "Customer":
                    PaintTwoLine(e, FontStrong, Navy, FontSub, Muted);
                    break;

                case "CreatedBy":
                    PaintTwoLine(e, FontNormal, Navy, FontSub, Muted);
                    break;

                case "Scheduled":
                    {
                        var raw = Convert.ToString(e.Value) ?? "";
                        var secondColor = raw.Contains("overdue") ? RedDot : Muted;
                        PaintTwoLine(e, FontNormal, Navy, FontSub, secondColor);
                        break;
                    }

                case "Reason":
                    PaintTwoLine(e, FontNormal, Navy, FontSub, Muted);
                    break;

                case "Status":
                    PaintStatusPill(e);
                    break;

                case "ArchivedInfo":
                    PaintTwoLine(e, FontNormal, Navy, FontSub, Muted);
                    break;

                case "Actions":
                    PaintActionsCell(e);
                    break;
            }
        }

        private void LogGrid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            if (e.RowIndex == -1) { PaintHeader(e); return; }

            if (_logGrid.Columns[e.ColumnIndex].Name == "Status")
                PaintStatusPill(e);
        }

        // ================================================================
        //  HOVER + CLICK
        // ================================================================
        private void Grid_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _grid.Columns[e.ColumnIndex].Name != "Actions")
            {
                SetHover(-1);
                return;
            }
            SetHover(HitTestActions(e.RowIndex, e.Location));
        }

        private void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (_grid.Columns[e.ColumnIndex].Name == "Actions")
            {
                int hit = HitTestActions(e.RowIndex, e.Location);
                if (hit < 0) return;

                var idText = _grid.Rows[e.RowIndex].Cells["FollowUpId"].Value?.ToString() ?? "";
                if (!int.TryParse(idText, out var rowId)) return;

                var dto = _all.FirstOrDefault(f => f.FollowUpId == rowId);
                if (dto == null) return;

                ShowActionsMenu(e.RowIndex, dto);
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "Customer")
            {
                var idText = _grid.Rows[e.RowIndex].Cells["FollowUpId"].Value?.ToString() ?? "";
                if (!int.TryParse(idText, out var rowId)) return;

                var dto = _all.FirstOrDefault(f => f.FollowUpId == rowId);
                if (dto == null) return;

                (FindForm() as MainForm)?.NavigateToCustomers(
                    segment: "All",
                    focusCustomerId: dto.CustomerId,
                    source: "dashboard");
            }
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

        // ================================================================
        //  ACTION MENU — status-aware
        // ================================================================
        private void ShowActionsMenu(int rowIndex, FollowUpDto f)
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

            // Every state gets a View.
            AddMenuItem(menu, "View", () => ShowViewDialog(f));

            // Approval-gated rows get Approve / Reject for Admin/Manager only.
            bool canApprove = SessionUser.Role == UserRole.Admin
                           || SessionUser.Role == UserRole.SuperAdmin
                           || SessionUser.Role == UserRole.Manager;

            if (f.ApprovalStatus == "Pending" && canApprove)
            {
                AddMenuItem(menu, "Approve", () => ApproveAsync(f));
                AddMenuItem(menu, "Reject", () => RejectAsync(f), isDanger: true);
            }
            else if (f.ApprovalStatus == "Pending" && !canApprove)
            {
                // Service Staff shouldn't even reach this view, but guard anyway.
                // No Approve / Reject items are added.
            }
            else
            {
                // Non-approval rows keep the existing actions.
                AddMenuItem(menu, "Edit", () => OpenEditDialog(f));
                AddMenuItem(menu, "Archive", () => ArchiveAsync(f), isDanger: true);
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
                Width = 180
            };
            item.Click += (s, e) => { menu.Close(); onClick(); };
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
            public override Color MenuItemSelected => BlueSoft;
            public override Color MenuItemSelectedGradientBegin => BlueSoft;
            public override Color MenuItemSelectedGradientEnd => BlueSoft;
            public override Color MenuItemBorder => CardBorder;
            public override Color MenuBorder => CardBorder;
        }

        // ================================================================
        //  LOAD
        // ================================================================
        private async Task LoadLookupsAsync()
        {
            var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
            try
            {
                _customers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    $"api/tenant/{companyId}/tenant-customers") ?? new();
                _custById = _customers.ToDictionary(c => c.TenantCustomerId);
            }
            catch { _customers = new(); _custById = new(); }

            try
            {
                _users = await _http.GetFromJsonAsync<List<UserDto>>($"api/users?companyId={companyId}") ?? new();
                _userById = _users.ToDictionary(u => u.UserId);
            }
            catch { _users = new(); _userById = new(); }
        }

        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var url = _tab == ListTab.Active ? $"api/follow-ups?companyId={companyId}" : $"api/follow-ups/archived?companyId={companyId}";
                _all = await _http.GetFromJsonAsync<List<FollowUpDto>>(url) ?? new();

                if (_tab == ListTab.Active)
                {
                    _stats = await _http.GetFromJsonAsync<FollowUpStatsDto>($"api/follow-ups/stats?companyId={companyId}")
                                ?? new FollowUpStatsDto();
                    BuildStats();
                }

                BuildGridColumns();
                ApplyFilter();
                BuildLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load follow-ups.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        // ================================================================
        //  GRID + PAGINATION
        // ================================================================
        private void ApplyFilter()
        {
            if (InvokeRequired) { Invoke(new Action(ApplyFilter)); return; }
            if (_grid == null || _pagerBar == null || _showingLbl == null) return;

            var list = _all.OrderByDescending(f => f.FollowUpId).ToList();

            if (!string.IsNullOrEmpty(_search))
            {
                list = list.Where(f =>
                {
                    var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;
                    var creator = f.CreatedBy.HasValue && _userById.TryGetValue(f.CreatedBy.Value, out var u)
                        ? u.FullName : "";
                    var name = cust?.CustomerName?.ToLower() ?? "";
                    var creatorLower = creator?.ToLower() ?? "";
                    return name.Contains(_search)
                        || creatorLower.Contains(_search)
                        || (f.DiscountOffer ?? "").ToLower().Contains(_search)
                        || (f.Reason ?? "").ToLower().Contains(_search)
                        || (f.Notes ?? "").ToLower().Contains(_search);
                }).ToList();
            }

            var effectiveFilter = !string.IsNullOrEmpty(_drillDownFilter)
                ? _drillDownFilter
                : _statusFilter;

            if (!string.IsNullOrWhiteSpace(effectiveFilter)
                && effectiveFilter != "All"
                && effectiveFilter != "All statuses")
            {
                list = list.Where(f => string.Equals(DisplayStatus(f), effectiveFilter,
                                                      StringComparison.OrdinalIgnoreCase))
                           .ToList();
            }

            var total = list.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));

            if (_focusFollowUpId.HasValue && _tab == ListTab.Active)
            {
                int idx = list.FindIndex(f => f.FollowUpId == _focusFollowUpId.Value);
                if (idx >= 0) _page = idx / Math.Max(1, _pageSize) + 1;
            }

            if (_page > totalPages) _page = totalPages;
            if (_page < 1) _page = 1;

            var start = (_page - 1) * _pageSize;
            var pageItems = list.Skip(start).Take(_pageSize).ToList();

            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var f in pageItems)
            {
                var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;

                var custCell = cust?.CustomerName ?? $"id:{f.CustomerId}";
                var secondLine = string.Join("  ·  ",
                    new[] { cust?.ContactNumber, cust?.VehicleType }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(secondLine))
                    custCell += $"\n{secondLine}";

                // Created By cell: staff name + role line
                string creatorCell = "—";
                if (f.CreatedBy.HasValue && _userById.TryGetValue(f.CreatedBy.Value, out var creator))
                {
                    creatorCell = creator.FullName;
                    var roleLabel = creator.RoleId switch
                    {
                        1 => "Super Admin",
                        2 => "Admin",
                        3 => "Manager",
                        4 => "Service Staff",
                        _ => "User"
                    };
                    creatorCell += $"\n{roleLabel}";
                }

                var scheduled = f.ScheduledDate.ToString("yyyy-MM-dd");
                var isOverdue = f.ScheduledDate.Date < DateTime.Today
                                && f.Status != "Sent"
                                && f.Status != "Contacted"
                                && f.Status != "Redeemed"
                                && f.Status != "Expired";
                if (isOverdue) scheduled += "\noverdue";

                var reasonCell = string.IsNullOrWhiteSpace(f.Reason) ? "—" : f.Reason!;

                var method = f.ContactMethod switch
                {
                    "Facebook Messenger" => "FB Messenger",
                    _ => f.ContactMethod
                };

                var displayStatus = DisplayStatus(f);

                int rowIdx;
                if (_tab == ListTab.Active)
                {
                    rowIdx = _grid.Rows.Add(
                        f.FollowUpId,
                        custCell,
                        creatorCell,
                        scheduled,
                        reasonCell,
                        string.IsNullOrWhiteSpace(f.DiscountOffer) ? "—" : f.DiscountOffer,
                        method,
                        displayStatus,
                        "");
                }
                else
                {
                    var archivedCell = "";
                    if (f.ArchivedAt.HasValue)
                        archivedCell = f.ArchivedAt.Value.ToString("yyyy-MM-dd");
                    if (!string.IsNullOrWhiteSpace(f.ArchivedBy))
                        archivedCell += "\nby " + f.ArchivedBy;

                    rowIdx = _grid.Rows.Add(
                        f.FollowUpId,
                        custCell,
                        creatorCell,
                        scheduled,
                        reasonCell,
                        string.IsNullOrWhiteSpace(f.DiscountOffer) ? "—" : f.DiscountOffer,
                        method,
                        displayStatus,
                        string.IsNullOrWhiteSpace(archivedCell) ? "—" : archivedCell,
                        "");
                }

                _grid.Rows[rowIdx].Tag = f.FollowUpId;

                if (_focusFollowUpId.HasValue && f.FollowUpId == _focusFollowUpId.Value)
                {
                    var row = _grid.Rows[rowIdx];
                    row.DefaultCellStyle.BackColor = HighlightTint;
                    row.DefaultCellStyle.SelectionBackColor = HighlightTint;
                }
            }

            _grid.ResumeLayout();
            _grid.PerformLayout();

            if (_focusFollowUpId.HasValue)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Tag is int id && id == _focusFollowUpId.Value)
                    {
                        try { _grid.FirstDisplayedScrollingRowIndex = row.Index; } catch { }
                        _highlightTimer.Stop();
                        _highlightTimer.Start();
                        break;
                    }
                }
            }

            var from = total == 0 ? 0 : start + 1;
            var to = Math.Min(start + _pageSize, total);
            _showingLbl.Text = _tab == ListTab.Active
                ? $"Showing {from}–{to} of {total} follow-ups"
                : $"Showing {from}–{to} of {total} archived follow-ups";

            RebuildPager(totalPages);
        }

        /// <summary>
        /// Display status shown in the grid. Approval state overrides
        /// the underlying lifecycle status when they disagree.
        /// </summary>
        private static string DisplayStatus(FollowUpDto f)
        {
            if (f.ApprovalStatus == "Pending") return "Pending Approval";
            if (f.ApprovalStatus == "Rejected") return "Rejected";
            if (f.ApprovalStatus == "Approved" && f.Status == "Scheduled") return "Approved";
            if (f.ApprovalStatus == "Approved" && f.Status == "Sent") return "Sent";
            return f.Status ?? "Draft";
        }

        // ================================================================
        //  PAGER
        // ================================================================
        private void RebuildPager(int totalPages)
        {
            foreach (var btn in _pagerBar.Controls.OfType<Button>().ToList())
                btn.Dispose();

            int btnSize = 36, gap = 6, rightPad = 16;

            var next = new Button
            {
                Text = "›",
                Size = new Size(btnSize, btnSize),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                UseVisualStyleBackColor = false,
                Font = new Font("Segoe UI Semibold", 12f),
                Cursor = _page < totalPages ? Cursors.Hand : Cursors.Default,
                Enabled = _page < totalPages,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            next.FlatAppearance.BorderColor = CardBorder;
            next.Click += (s, e) => { if (_page < totalPages) { _page++; ApplyFilter(); } };
            next.Location = new Point(_pagerBar.Width - btnSize - rightPad, 10);
            _pagerBar.Controls.Add(next);
            rightPad += btnSize + gap;

            var startPage = Math.Max(1, totalPages - 3);
            for (int p = totalPages; p >= startPage; p--)
            {
                var pb = new Button
                {
                    Text = p.ToString(),
                    Size = new Size(btnSize, btnSize),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = p == _page ? Navy : Color.White,
                    ForeColor = p == _page ? Color.White : Navy,
                    UseVisualStyleBackColor = false,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                pb.FlatAppearance.BorderColor = p == _page ? Navy : CardBorder;
                var captured = p;
                pb.Click += (s, e) => { _page = captured; ApplyFilter(); };
                pb.Location = new Point(_pagerBar.Width - btnSize - rightPad, 10);
                _pagerBar.Controls.Add(pb);
                rightPad += btnSize + gap;
            }

            var prev = new Button
            {
                Text = "‹",
                Size = new Size(btnSize, btnSize),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                UseVisualStyleBackColor = false,
                Font = new Font("Segoe UI Semibold", 12f),
                Cursor = _page > 1 ? Cursors.Hand : Cursors.Default,
                Enabled = _page > 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            prev.FlatAppearance.BorderColor = CardBorder;
            prev.Click += (s, e) => { if (_page > 1) { _page--; ApplyFilter(); } };
            prev.Location = new Point(_pagerBar.Width - btnSize - rightPad, 10);
            _pagerBar.Controls.Add(prev);
        }

        // ================================================================
        //  LOG GRID
        // ================================================================
        private void BuildLog()
        {
            _logGrid.SuspendLayout();
            _logGrid.Rows.Clear();

            var recent = _all.OrderByDescending(f => f.ScheduledDate).Take(10).ToList();
            foreach (var f in recent)
            {
                var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;
                var idx = _logGrid.Rows.Add(
                    $"#{f.FollowUpId}",
                    cust?.CustomerName ?? $"id:{f.CustomerId}",
                    f.Type,
                    f.ContactMethod,
                    f.ScheduledDate.ToString("yyyy-MM-dd"),
                    DisplayStatus(f),
                    f.Notes ?? "");
                _logGrid.Rows[idx].Cells["Notes"].ToolTipText = f.Notes ?? "";
            }
            _logGrid.ResumeLayout();

            _logRowCount = recent.Count;
            RelayoutUI();
        }

        // ================================================================
        //  VIEW DIALOG
        // ================================================================
        private void ShowViewDialog(FollowUpDto f)
        {
            var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;

            string creatorText = "—";
            if (f.CreatedBy.HasValue && _userById.TryGetValue(f.CreatedBy.Value, out var u))
            {
                var roleLabel = u.RoleId switch
                {
                    1 => "Super Admin",
                    2 => "Admin",
                    3 => "Manager",
                    4 => "Service Staff",
                    _ => "User"
                };
                creatorText = $"{u.FullName} ({roleLabel})";
            }

            using var dlg = new FollowUpViewDialog(
                followUpId: f.FollowUpId,
                customerName: cust?.CustomerName ?? $"id:{f.CustomerId}",
                customerEmail: cust?.EmailAddress ?? "—",
                createdBy: creatorText,
                reason: f.Reason ?? "—",
                discountOffer: f.DiscountOffer ?? "—",
                validUntil: f.ValidUntil,
                sendVia: f.ContactMethod ?? "Email",
                sendOn: f.ScheduledDate,
                messagePreview: f.Notes ?? "",
                status: DisplayStatus(f),
                approvalStatus: f.ApprovalStatus,
                rejectionReason: f.RejectionReason,
                approvedByName: f.ApprovedBy.HasValue && _userById.TryGetValue(f.ApprovedBy.Value, out var ap)
                    ? ap.FullName : null,
                approvedAt: f.ApprovedAt);

            dlg.ShowDialog(FindForm());
        }

        // ================================================================
        //  APPROVE
        // ================================================================
        private async void ApproveAsync(FollowUpDto f)
        {
            var isSendNow = f.ScheduledDate <= DateTime.Now;

            var confirm = MessageBox.Show(
                isSendNow
                    ? "Approve this follow-up?\n\nThe email will be sent immediately because it was submitted as Send Now."
                    : "Approve this follow-up?\n\nThe email will be sent when the scheduled time is reached.",
                "Approve Follow-Up",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (confirm != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var resp = await _http.PostAsJsonAsync(
                    $"api/follow-ups/{f.FollowUpId}/approve?companyId={companyId}",
                    new
                    {
                        approvedBy = SessionUser.UserId,
                        sendNow = isSendNow
                    });

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        isSendNow
                            ? "Follow-up approved and sent."
                            : "Follow-up approved. It will be sent when the scheduled time is reached.",
                        "Approved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    await LoadAsync();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Approve failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Approve failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        // ================================================================
        //  REJECT
        // ================================================================
        private async void RejectAsync(FollowUpDto f)
        {
            using var dlg = new RejectFollowUpDialog();
            if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var resp = await _http.PostAsJsonAsync(
                    $"api/follow-ups/{f.FollowUpId}/reject?companyId={companyId}",
                    new
                    {
                        rejectedBy = SessionUser.UserId,
                        reason = dlg.RejectionReason
                    });

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        "Follow-up rejected. The Service Staff will see the reason.",
                        "Rejected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    await LoadAsync();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Reject failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Reject failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        // ================================================================
        //  EDIT / ARCHIVE / RESTORE / ADD
        // ================================================================
        private void OpenEditDialog(FollowUpDto dto)
        {
            using var dlg = new FollowUpEditDialog(_customers, dto);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = ReloadAllAsync();
            }
        }

        private async void ArchiveAsync(FollowUpDto f)
        {
            if (!ArchiveConfirmDialog.ConfirmArchive("follow-up")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsJsonAsync(
                    $"api/follow-ups/{f.FollowUpId}/archive?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}",
                    new { archivedBy = CurrentUserName });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Archive failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Archive failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private async void RestoreAsync(FollowUpDto f)
        {
            if (!ArchiveConfirmDialog.ConfirmRestore("follow-up")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsync(
                    $"api/follow-ups/{f.FollowUpId}/restore?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}", null);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Restore failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private async void OpenAddDialog()
        {
            if (_customers == null || _customers.Count == 0)
            {
                MessageBox.Show(
                    "No customers found. Add customers in Manage Customers first.",
                    "No Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new FollowUpEditDialog(_customers);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _page = 1;
                await ReloadAllAsync();
            }
        }

        public async void OpenAddDialogWithCustomers(List<int> preselectedCustomerIds)
        {
            await LoadLookupsAsync();

            if (_customers == null || _customers.Count == 0)
            {
                MessageBox.Show(
                    "No customers found. Add customers in Manage Customers first.",
                    "No Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new FollowUpEditDialog(_customers, preselectedCustomerIds);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _page = 1;
                await ReloadAllAsync();
            }
        }

        public void RefreshData() { _ = ReloadAllAsync(); }

        private async Task ReloadAllAsync()
        {
            await LoadLookupsAsync();
            await LoadAsync();
        }
    }
}