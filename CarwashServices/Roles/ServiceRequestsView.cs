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

        private static readonly Font FontLine1 = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new Font("Segoe UI", 8.5f);
        private static readonly Font FontPlain = new Font("Segoe UI", 9.5f);
        private static readonly Font FontTag = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontButton = new Font("Segoe UI Semibold", 9f);

        private static readonly bool TintBehindText = false;

        private const int PadX = 40;
        private const int TabTop = 114;
        private const int TabH = 44;
        private const int FilterTop = 178;
        private const int FilterH = 76;
        private const int GridTop = FilterTop + FilterH + 12;
        private const int PagerH = 48;
        private const int PageBottom = 24;

        private const int RowTemplateHeight = 62;

        private const int EditBtnW = 72;
        private const int ArcBtnW = 88;
        private const int ResBtnW = 88;
        private const int ActionBtnH = 30;
        private const int ActionGap = 8;

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
        private int _hoverAction = -1;
        private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };

        private int _page = 1;
        private int _pageSize = 8;

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

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Admin" : SessionUser.FullName;

        public ServiceRequestsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _debounce.Tick += (s, e) => { _debounce.Stop(); ApplyFilter(resetPage: true); };

            InitializeUI();

            Load += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
        }

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

            var tabBar = new Panel
            {
                Location = new Point(PadX, TabTop),
                Height = TabH,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);
            };
            _contentPanel.Controls.Add(tabBar);

            const int TabWidth = 170;
            const int TabGap = 40;

            _tabActive = MakeTab("Active Requests", 0, TabWidth);
            _tabArchived = MakeTab("Archived Requests", TabWidth + TabGap, TabWidth);
            tabBar.Controls.Add(_tabActive);
            tabBar.Controls.Add(_tabArchived);

            _tabUnderline = new Panel
            {
                Height = 2,
                Width = TabWidth,
                BackColor = Blue,
                Location = new Point(_tabActive.Left, TabH - 2)
            };
            tabBar.Controls.Add(_tabUnderline);

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
                Location = new Point(PadX, FilterTop - 56),
                Height = 40,
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            var filterCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, FilterTop),
                Height = FilterH
            };
            filterCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, filterCard.Width - 1, filterCard.Height - 1);
            };
            _contentPanel.Controls.Add(filterCard);

            filterCard.Controls.Add(new Label
            {
                Text = "Search",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(16, 8),
                AutoSize = true
            });

            var searchWrap = new Panel
            {
                Location = new Point(16, 28),
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
            filterCard.Controls.Add(searchWrap);

            var searchBtn = new Button
            {
                Text = "Search",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                BackColor = Navy,
                ForeColor = Color.White,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            searchBtn.FlatAppearance.BorderSize = 0;
            searchBtn.Click += (s, e) => ApplyFilter(resetPage: true);
            filterCard.Controls.Add(searchBtn);

            var refreshBtn = new Button
            {
                Text = "Refresh",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Navy,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderColor = CardBorder;
            StyleOutlineButton(refreshBtn);
            refreshBtn.Click += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
            filterCard.Controls.Add(refreshBtn);

            _gridHost = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, GridTop),
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

            // Column sizing: fixed columns keep their widths so timestamps and
            // names never ellipsise; two Fill columns absorb the remaining space.
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RequestId",
                HeaderText = "ID",
                Width = 70
            });
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
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Scheduled",
                HeaderText = "SCHEDULED",
                Width = 170,
                MinimumWidth = 170
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Staff",
                HeaderText = "STAFF",
                Width = 150,
                MinimumWidth = 150
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Priority",
                HeaderText = "PRIORITY",
                Width = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "STATUS",
                Width = 120
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "ACTIONS",
                Width = 200,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    Padding = new Padding(0)
                }
            });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseLeave += (s, e) => SetHover(-1);
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.CellMouseClick += Grid_CellMouseClick;
            _gridHost.Controls.Add(_grid);

            _pager = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, 0),
                Height = PagerH,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _pager.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, _pager.Width, 0);
            };
            _contentPanel.Controls.Add(_pager);

            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;
                int contentW = Math.Max(0, w - 2 * PadX);

                _newRequestBtn.Location = new Point(w - _newRequestBtn.Width - PadX, 30);
                tabBar.Width = contentW;
                _chipBar.Width = contentW;

                filterCard.Width = contentW;
                refreshBtn.Location = new Point(contentW - 16 - refreshBtn.Width, 28);
                searchBtn.Location = new Point(refreshBtn.Left - 10 - searchBtn.Width, 28);
                searchWrap.Width = Math.Max(120, searchBtn.Left - 12 - searchWrap.Left);

                _pager.SetBounds(PadX, h - PageBottom - PagerH, contentW, PagerH);
                _gridHost.SetBounds(PadX, GridTop, contentW,
                    Math.Max(0, (h - PageBottom - PagerH) - GridTop));

                RecomputePageSize();
                RenderCurrentPage();
                RenderPager(CurrentTotalPages());
            }
            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();

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

        private void BuildChips()
        {
            _chipBar.Controls.Clear();
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
                    BuildChips();
                    ApplyFilter(resetPage: true);
                };
                _chipBar.Controls.Add(chip);
                x += 130;
            }
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                _tenantCustomers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers") ?? new();

                _customerLookup = _tenantCustomers
                    .Select(c => new CustomerDto
                    {
                        CustomerId = c.TenantCustomerId,
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
                    "api/tenant/1/products") ?? new();

                _serviceLookup = _tenantProducts
                    .Select(p => new ServiceDto
                    {
                        ServiceId = p.ProductId,
                        ServiceName = p.ProductName,
                        Price = p.UnitPrice,
                        DurationMinutes = p.DurationMinutes
                    })
                    .ToList();
            }
            catch { _tenantProducts = new(); _serviceLookup = new(); }

            try { _users = await _http.GetFromJsonAsync<List<UserDto>>("api/users") ?? new(); }
            catch { _users = new(); }
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                _newRequestBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var url = _tab == ListTab.Active
                    ? "api/service-requests"
                    : "api/service-requests/archived";

                _all = await _http.GetFromJsonAsync<List<ServiceRequestDto>>(url) ?? new();

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

        private void ApplyFilter(bool resetPage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ApplyFilter(resetPage)));
                return;
            }

            var search = _searchBox.Text?.Trim().ToLower() ?? "";

            _filtered = _all.Where(r =>
            {
                if (_tab == ListTab.Active &&
                    _activeStatus != "All" &&
                    !string.Equals(r.Status, _activeStatus, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (string.IsNullOrEmpty(search)) return true;

                var tenantCust = _tenantCustomers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                var svc = _serviceLookup.FirstOrDefault(s => s.ServiceId == r.ServiceId);
                var staff = r.AssignedStaffId.HasValue
                    ? _users.FirstOrDefault(u => u.UserId == r.AssignedStaffId.Value)
                    : null;

                var blob = $"{tenantCust?.CustomerName} {tenantCust?.PlateNumber} " +
                           $"{svc?.ServiceName} {staff?.FullName} " +
                           $"{r.Status} {r.Priority}".ToLower();
                return blob.Contains(search);
            }).ToList();

            if (resetPage) _page = 1;

            int totalPages = CurrentTotalPages();
            if (_page > totalPages) _page = totalPages;

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

                _grid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    svcCell,
                    r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "—",
                    staffCell,
                    string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority,
                    r.Status,
                    "");
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
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
                int x = _pager.Width - 16 - (items.Count * bw + (items.Count - 1) * gap);

                foreach (var it in items)
                {
                    var b = PagerButton(it.Text, it.Active, it.Enabled);
                    b.SetBounds(x, 7, bw, bh);
                    int target = it.Page;
                    b.Click += (s, e) =>
                    {
                        _page = target;
                        RenderCurrentPage();
                        RenderPager(totalPages);
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

            if (_tab == ListTab.Active)
            {
                var (editRect, arcRect) = ActiveButtonRects(e.CellBounds);
                PaintOutlineButton(e.Graphics, editRect, "Edit",
                    _hoverAction == (e.RowIndex << 2) + 0, Blue);
                PaintOutlineButton(e.Graphics, arcRect, "Archive",
                    _hoverAction == (e.RowIndex << 2) + 1, Red);
            }
            else
            {
                var restoreRect = RestoreButtonRect(e.CellBounds);
                PaintOutlineButton(e.Graphics, restoreRect, "Restore",
                    _hoverAction == (e.RowIndex << 2) + 1, Green);
            }

            e.Handled = true;
        }

        private static (Rectangle edit, Rectangle archive) ActiveButtonRects(Rectangle cellBounds)
        {
            int totalW = EditBtnW + ArcBtnW + ActionGap;
            int x0 = cellBounds.X + (cellBounds.Width - totalW) / 2;
            int y0 = cellBounds.Y + (cellBounds.Height - ActionBtnH) / 2;
            return (
                new Rectangle(x0, y0, EditBtnW, ActionBtnH),
                new Rectangle(x0 + EditBtnW + ActionGap, y0, ArcBtnW, ActionBtnH)
            );
        }

        private static Rectangle RestoreButtonRect(Rectangle cellBounds)
        {
            int x0 = cellBounds.X + (cellBounds.Width - ResBtnW) / 2;
            int y0 = cellBounds.Y + (cellBounds.Height - ActionBtnH) / 2;
            return new Rectangle(x0, y0, ResBtnW, ActionBtnH);
        }

        private static void PaintOutlineButton(Graphics g, Rectangle rect, string text, bool hover, Color tone)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = hover ? tone : Color.White;
            Color fore = hover ? Color.White : tone;

            using (var path = RoundedRect(rect, 6))
            using (var fillBrush = new SolidBrush(fill))
            using (var pen = new Pen(tone, 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, text, FontButton, rect, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
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

            if (_tab == ListTab.Active)
            {
                var (editRect, arcRect) = ActiveButtonRects(cellBounds);
                if (editRect.Contains(absolute)) return (rowIndex << 2) + 0;
                if (arcRect.Contains(absolute)) return (rowIndex << 2) + 1;
            }
            else
            {
                var restoreRect = RestoreButtonRect(cellBounds);
                if (restoreRect.Contains(absolute)) return (rowIndex << 2) + 1;
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

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

            int hit = HitTestActions(e.RowIndex, e.Location);
            if (hit < 0) return;

            int buttonIndex = hit & 0b11;

            var idText = _grid.Rows[e.RowIndex].Cells["RequestId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText.TrimStart('#'), out var id)) return;

            var req = _all.FirstOrDefault(r => r.RequestId == id);
            if (req == null) return;

            if (_tab == ListTab.Active)
            {
                switch (buttonIndex)
                {
                    case 0: OpenEditRequestDialog(id); break;
                    case 1: ArchiveRequestAsync(req); break;
                }
            }
            else
            {
                if (buttonIndex == 1) RestoreRequestAsync(req);
            }
        }

        private async void OpenNewRequestDialog()
        {
            using var dlg = new ServiceRequestEditDialog(null, _customerLookup, _serviceLookup);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            }
        }

        private async void OpenEditRequestDialog(int id)
        {
            using var dlg = new ServiceRequestEditDialog(id, _customerLookup, _serviceLookup);
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
                    $"api/service-requests/{req.RequestId}/archive",
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
                    $"api/service-requests/{req.RequestId}/restore", null);

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