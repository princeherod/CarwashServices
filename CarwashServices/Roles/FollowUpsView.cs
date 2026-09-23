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

namespace CarwashServices.Roles
{
    public class FollowUpsView : UserControl
    {
        private HttpClient _http;

        private List<FollowUpDto> _all = new();
        private List<TenantCustomerDto> _customers = new();
        private Dictionary<int, TenantCustomerDto> _custById = new();

        private int _pageSize = 8;
        private int _page = 1;
        private FollowUpStatsDto _stats = new();
        private string _search = "";
        private string _statusFilter = "All statuses";

        private enum ListTab { Active, Archived }
        private ListTab _tab = ListTab.Active;

        private Panel _contentPanel;
        private Panel _statsBar;
        private DataGridView _grid;
        private Panel _pagerBar;
        private DataGridView _logGrid;
        private Label _showingLbl;
        private Panel _gridCard;
        private Panel _logCard;
        private Panel _header;
        private Button _addBtn;
        private TextBox _searchBox;
        private ComboBox _statusFilterCombo;

        private Label _tabActive;
        private Label _tabArchived;
        private Panel _tabUnderline;
        private Panel _tabBar;

        private Label _breadcrumb;
        private Label _sectionLbl;
        private Label _logTitleLbl;
        private Label _logSubLbl;
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
        private static readonly Color ButtonBorder = Color.FromArgb(0xC9, 0xD3, 0xE3);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);

        private static readonly Color GreenDot = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color YellowDot = Color.FromArgb(0xF5, 0xB0, 0x2E);
        private static readonly Color BlueDot = Color.FromArgb(0x42, 0xA5, 0xF5);
        private static readonly Color RedDot = Color.FromArgb(0xE5, 0x39, 0x35);

        private static readonly Font FontStrong = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontNormal = new Font("Segoe UI", 9.5f);
        private static readonly Font FontSub = new Font("Segoe UI", 8.5f);
        private static readonly Font FontPill = new Font("Segoe UI Semibold", 8.5f);
        private static readonly Font FontAction = new Font("Segoe UI Semibold", 8.5f);
        private static readonly Font FontStatTitle = new Font("Segoe UI", 10f);
        private static readonly Font FontStatValue = new Font("Segoe UI Semibold", 24f);

        private static readonly bool TintBehindText = false;

        // ============================================================
        //  Main grid column weights
        // ============================================================
        private const int MinCustomer = 180;
        private const int MinScheduled = 120;
        private const int MinDiscount = 130;
        private const int MinSendVia = 80;
        private const int MinStatus = 110;
        private const int MinActions = 130;
        private const int MinArchived = 130;

        private const float WCustomer = 24f;
        private const float WScheduled = 16f;
        private const float WDiscount = 18f;
        private const float WSendVia = 9f;
        private const float WStatus = 11f;
        private const float WActions = 22f;

        // ============================================================
        //  Log grid column weights
        // ============================================================
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

        // Actions cell button geometry
        private const int EditBtnW = 72;
        private const int ArcBtnW = 78;
        private const int ResBtnW = 82;
        private const int ActionBtnH = 28;
        private const int ActionBtnGap = 6;

        private int _hoverAction = -1;   // (row << 2) | buttonIndex

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

            InitializeUI();
            _uiReady = true;
        }

        // ================================================================
        //  Small helper controls
        // ================================================================
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

        // ================================================================
        //  UI
        // ================================================================
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

            _header = new Panel
            {
                Height = 92,
                BackColor = Color.Transparent
            };
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

            // ---- Tab strip ----
            _tabBar = new Panel
            {
                Height = 44,
                BackColor = Color.White
            };
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

            _statsBar = new Panel
            {
                Height = 100,
                BackColor = Color.Transparent
            };
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
                "All statuses", "Scheduled", "Due today", "Sent", "Redeemed", "Expired"
            });

            _statusFilterCombo.SelectedIndexChanged += (s, e) =>
            {
                if (!_uiReady) return;
                _statusFilter = _statusFilterCombo.SelectedItem?.ToString() ?? "All statuses";
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

            _contentPanel.ClientSizeChanged += (s, e) => RelayoutUI();

            Load += async (s, e) =>
            {
                RelayoutUI();
                await LoadLookupsAsync();
                await LoadAsync();
                BeginInvoke(new Action(RelayoutUI));
            };
        }

        // ================================================================
        //  GRID COLUMNS — depend on the active tab
        // ================================================================
        private void BuildGridColumns()
        {
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "FollowUpId",
                Visible = false
            });

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
                Name = "Scheduled",
                HeaderText = "Scheduled / Send On",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = MinScheduled,
                FillWeight = WScheduled
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
                for (int pass = 0; pass < 3; pass++)
                {
                    int before = _contentPanel.ClientSize.Width;
                    ApplyLayout();
                    if (_contentPanel.ClientSize.Width == before) break;
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
            int w = fullW - MarginX * 2;
            if (w < 300) return;

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

            for (int i = 0; i < 4; i++)
            {
                var card = new RoundedPanel
                {
                    Location = new Point(i * (cardW + gap), 0),
                    Size = new Size(cardW, 100)
                };

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

                card.Controls.Add(new Label
                {
                    Text = titles[i],
                    ForeColor = Muted,
                    BackColor = Color.White,
                    Font = FontStatTitle,
                    Location = new Point(42, 21),
                    AutoSize = true
                });

                card.Controls.Add(new Label
                {
                    Text = values[i].ToString(),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    Font = FontStatValue,
                    Location = new Point(22, 42),
                    AutoSize = true
                });

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
        //  CUSTOM CELL PAINTING
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
            "Scheduled" => (Color.FromArgb(0xE8, 0xEA, 0xF6), Color.FromArgb(0x39, 0x49, 0xAB)),
            "Due today" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Color.FromArgb(0x9A, 0x6A, 0x00)),
            "Sent" or "Contacted" => (Color.FromArgb(0xE3, 0xF1, 0xFD), Color.FromArgb(0x15, 0x65, 0xC0)),
            "Redeemed" => (Color.FromArgb(0xE4, 0xF5, 0xE8), Color.FromArgb(0x1E, 0x7A, 0x34)),
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

        // ---- Actions cell painter ----
        // Uses the cell's text value as a "|"-delimited spec:
        //   Active tab  → "Edit|Archive"   or "Archive" for terminal rows
        //   Archived tab → "Restore"
        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, BaseParts);

            var spec = Convert.ToString(e.Value) ?? "";
            if (string.IsNullOrEmpty(spec)) { e.Handled = true; return; }

            var parts = spec.Split('|');

            int totalW = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                totalW += parts[i] switch
                {
                    "Edit" => EditBtnW,
                    "Archive" => ArcBtnW,
                    "Restore" => ResBtnW,
                    _ => ArcBtnW
                };
                if (i > 0) totalW += ActionBtnGap;
            }

            int x0 = e.CellBounds.X + (e.CellBounds.Width - totalW) / 2;
            int y0 = e.CellBounds.Y + (e.CellBounds.Height - ActionBtnH) / 2;

            for (int i = 0; i < parts.Length; i++)
            {
                int w = parts[i] switch
                {
                    "Edit" => EditBtnW,
                    "Archive" => ArcBtnW,
                    "Restore" => ResBtnW,
                    _ => ArcBtnW
                };

                var rect = new Rectangle(x0, y0, w, ActionBtnH);
                bool hover = _hoverAction == (e.RowIndex << 2) + i;

                switch (parts[i])
                {
                    case "Edit":
                        PaintButton(e.Graphics, rect, "Edit", hover, Navy);
                        break;
                    case "Archive":
                        PaintButton(e.Graphics, rect, "Archive", hover, Red);
                        break;
                    case "Restore":
                        PaintButton(e.Graphics, rect, "Restore", hover, Green);
                        break;
                }

                x0 += w + ActionBtnGap;
            }

            e.Handled = true;
        }

        private static void PaintButton(Graphics g, Rectangle rect, string text,
            bool hover, Color tone)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = hover ? tone : Color.White;
            Color fore = hover ? Color.White : tone;

            using (var path = RoundedRect(rect, 6))
            using (var fillBrush = new SolidBrush(fill))
            using (var pen = new Pen(tone, 1))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, text, FontAction, rect, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;

            if (e.RowIndex == -1)
            {
                PaintHeader(e);
                return;
            }

            var colName = _grid.Columns[e.ColumnIndex].Name;

            switch (colName)
            {
                case "Customer":
                    PaintTwoLine(e, FontStrong, Navy, FontSub, Muted);
                    break;

                case "Scheduled":
                    {
                        var raw = Convert.ToString(e.Value) ?? "";
                        var secondColor = raw.Contains("overdue") ? RedDot : Muted;
                        PaintTwoLine(e, FontNormal, Navy, FontSub, secondColor);
                        break;
                    }

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

            if (e.RowIndex == -1)
            {
                PaintHeader(e);
                return;
            }

            if (_logGrid.Columns[e.ColumnIndex].Name == "Status")
                PaintStatusPill(e);
        }

        // ================================================================
        //  HOVER + CLICK for the Actions column
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
            if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

            int hit = HitTestActions(e.RowIndex, e.Location);
            if (hit < 0) return;

            int buttonIndex = hit & 0b11;

            var spec = _grid.Rows[e.RowIndex].Cells["Actions"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(spec)) return;

            var parts = spec.Split('|');
            if (buttonIndex >= parts.Length) return;

            string action = parts[buttonIndex];

            var idText = _grid.Rows[e.RowIndex].Cells["FollowUpId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var rowId)) return;

            var dto = _all.FirstOrDefault(f => f.FollowUpId == rowId);
            if (dto == null) return;

            switch (action)
            {
                case "Edit": OpenEditDialog(dto); break;
                case "Archive": ArchiveAsync(dto); break;
                case "Restore": RestoreAsync(dto); break;
            }
        }

        private int HitTestActions(int rowIndex, Point local)
        {
            var cellBounds = _grid.GetCellDisplayRectangle(
                _grid.Columns["Actions"].Index, rowIndex, false);

            var absolute = new Point(cellBounds.X + local.X, cellBounds.Y + local.Y);

            var spec = _grid.Rows[rowIndex].Cells["Actions"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(spec)) return -1;

            var parts = spec.Split('|');

            int totalW = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                totalW += parts[i] switch
                {
                    "Edit" => EditBtnW,
                    "Archive" => ArcBtnW,
                    "Restore" => ResBtnW,
                    _ => ArcBtnW
                };
                if (i > 0) totalW += ActionBtnGap;
            }

            int x0 = cellBounds.X + (cellBounds.Width - totalW) / 2;
            int y0 = cellBounds.Y + (cellBounds.Height - ActionBtnH) / 2;

            for (int i = 0; i < parts.Length; i++)
            {
                int w = parts[i] switch
                {
                    "Edit" => EditBtnW,
                    "Archive" => ArcBtnW,
                    "Restore" => ResBtnW,
                    _ => ArcBtnW
                };

                var rect = new Rectangle(x0, y0, w, ActionBtnH);
                if (rect.Contains(absolute)) return (rowIndex << 2) + i;

                x0 += w + ActionBtnGap;
            }

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
        //  LOAD
        // ================================================================
        private async Task LoadLookupsAsync()
        {
            try
            {
                _customers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers") ?? new();
                _custById = _customers.ToDictionary(c => c.TenantCustomerId);
            }
            catch
            {
                _customers = new();
                _custById = new();
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var url = _tab == ListTab.Active
                    ? "api/follow-ups"
                    : "api/follow-ups/archived";

                _all = await _http.GetFromJsonAsync<List<FollowUpDto>>(url) ?? new();

                if (_tab == ListTab.Active)
                {
                    _stats = await _http.GetFromJsonAsync<FollowUpStatsDto>("api/follow-ups/stats")
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
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ================================================================
        //  GRID + PAGINATION
        // ================================================================
        private void ApplyFilter()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyFilter));
                return;
            }

            if (_grid == null || _pagerBar == null || _showingLbl == null)
                return;

            var list = _all.OrderByDescending(f => f.FollowUpId).ToList();

            if (!string.IsNullOrEmpty(_search))
            {
                list = list.Where(f =>
                {
                    var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;
                    var name = cust?.CustomerName?.ToLower() ?? "";
                    return name.Contains(_search)
                        || (f.DiscountOffer ?? "").ToLower().Contains(_search)
                        || (f.Notes ?? "").ToLower().Contains(_search);
                }).ToList();
            }

            if (!string.IsNullOrWhiteSpace(_statusFilter)
                && _statusFilter != "All"
                && _statusFilter != "All statuses")
            {
                list = list.Where(f => f.Status == _statusFilter).ToList();
            }

            var total = list.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
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

                var scheduled = f.ScheduledDate.ToString("yyyy-MM-dd");
                var isOverdue = f.ScheduledDate.Date < DateTime.Today
                                && f.Status != "Sent"
                                && f.Status != "Contacted"
                                && f.Status != "Redeemed"
                                && f.Status != "Expired";
                if (isOverdue) scheduled += "\noverdue";

                var method = f.ContactMethod switch
                {
                    "Facebook Messenger" => "FB Messenger",
                    _ => f.ContactMethod
                };

                if (_tab == ListTab.Active)
                {
                    bool isTerminal = f.Status == "Redeemed" || f.Status == "Expired";
                    string actions = isTerminal ? "Archive" : "Edit|Archive";

                    _grid.Rows.Add(
                        f.FollowUpId,
                        custCell,
                        scheduled,
                        string.IsNullOrWhiteSpace(f.DiscountOffer) ? "—" : f.DiscountOffer,
                        method,
                        f.Status,
                        actions);
                }
                else
                {
                    var archivedCell = "";
                    if (f.ArchivedAt.HasValue)
                        archivedCell = f.ArchivedAt.Value.ToString("yyyy-MM-dd");
                    if (!string.IsNullOrWhiteSpace(f.ArchivedBy))
                        archivedCell += "\nby " + f.ArchivedBy;

                    _grid.Rows.Add(
                        f.FollowUpId,
                        custCell,
                        scheduled,
                        string.IsNullOrWhiteSpace(f.DiscountOffer) ? "—" : f.DiscountOffer,
                        method,
                        f.Status,
                        string.IsNullOrWhiteSpace(archivedCell) ? "—" : archivedCell,
                        "Restore");
                }
            }

            _grid.ResumeLayout();
            _grid.PerformLayout();

            var from = total == 0 ? 0 : start + 1;
            var to = Math.Min(start + _pageSize, total);
            _showingLbl.Text = _tab == ListTab.Active
                ? $"Showing {from}–{to} of {total} follow-ups"
                : $"Showing {from}–{to} of {total} archived follow-ups";

            RebuildPager(totalPages);
        }

        // ================================================================
        //  ARCHIVE / RESTORE
        // ================================================================
        private async void ArchiveAsync(FollowUpDto f)
        {
            if (!ArchiveConfirmDialog.ConfirmArchive("follow-up")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsJsonAsync(
                    $"api/follow-ups/{f.FollowUpId}/archive",
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
                    $"api/follow-ups/{f.FollowUpId}/restore", null);

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

        // ================================================================
        //  EDIT
        // ================================================================
        private void OpenEditDialog(FollowUpDto dto)
        {
            using var dlg = new FollowUpEditDialog(_customers, dto);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = ReloadAllAsync();
            }
        }

        // ================================================================
        //  PAGER
        // ================================================================
        private void RebuildPager(int totalPages)
        {
            foreach (var btn in _pagerBar.Controls.OfType<Button>().ToList())
                btn.Dispose();

            int btnSize = 36;
            int gap = 6;
            int rightPad = 16;

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
            next.FlatAppearance.MouseOverBackColor = PageBg;
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
                if (p != _page) pb.FlatAppearance.MouseOverBackColor = PageBg;
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
            prev.FlatAppearance.MouseOverBackColor = PageBg;
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
                    f.Status,
                    f.Notes ?? "");

                _logGrid.Rows[idx].Cells["Notes"].ToolTipText = f.Notes ?? "";
            }

            _logGrid.ResumeLayout();

            _logRowCount = recent.Count;
            RelayoutUI();
        }

        // ================================================================
        //  OPEN ADD DIALOG
        // ================================================================
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

        public void RefreshData()
        {
            _ = ReloadAllAsync();
        }

        private async Task ReloadAllAsync()
        {
            await LoadLookupsAsync();
            await LoadAsync();
        }
    }
}