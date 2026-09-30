using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net;
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
    public class CustomersView : UserControl
    {
        // -----------------------------------------------------------------
        //  Palette
        // -----------------------------------------------------------------
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF5, 0xF7, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE5, 0xE8, 0xEE);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color AmberSoft = Color.FromArgb(0xFF, 0xF4, 0xDB);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color HighlightTint = Color.FromArgb(0xFF, 0xF5, 0xCC);

        // -----------------------------------------------------------------
        //  State
        // -----------------------------------------------------------------
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<TenantCustomerDto> _allCustomers = new();
        private List<TenantCustomerDto> _archivedCustomers = new();
        private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 300 };

        private enum ListTab { Active, Archived }
        private ListTab _tab = ListTab.Active;

        private int _page = 1;
        private int _pageSize = 6;
        private const int RowHeight = 92;
        private const int RowGap = 12;

        private const int TabWidth = 170;
        private const int TabGap = 40;

        // ---- Drill-down ----
        private string _drillDownSegment = "";
        private HashSet<int>? _drillDownCustomerIds;
        private int? _focusCustomerId;
        private string _source = "";
        private Panel _drillDownHost = null!;
        private Panel _drillDownChip = null!;
        private Label _drillDownChipLabel = null!;
        private readonly System.Windows.Forms.Timer _highlightTimer = new() { Interval = 3000 };

        // -----------------------------------------------------------------
        //  UI
        // -----------------------------------------------------------------
        private Panel _root = null!;
        private Panel _listScreen = null!;
        private Label _tabActive = null!;
        private Label _tabArchived = null!;
        private Panel _tabUnderline = null!;

        private Panel? _detailScreen;
        private TenantCustomerDto? _currentCustomer;

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Admin" : SessionUser.FullName;

        public CustomersView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _searchDebounce.Tick += (s, e) => { _searchDebounce.Stop(); ApplyFilter(); };
            _highlightTimer.Tick += (s, e) =>
            {
                _highlightTimer.Stop();
                _focusCustomerId = null;
                ClearRowHighlights();
            };

            BuildRoot();
            ShowList();

            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadCustomersAsync();
        }

        private void BuildRoot()
        {
            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(30, 20, 30, 20)
            };
            Controls.Add(_root);
        }

        // =================================================================
        //  SCREEN 1 — LIST
        // =================================================================
        private void ShowList()
        {
            _root.Controls.Clear();

            _listScreen = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            _root.Controls.Add(_listScreen);

            _listScreen.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Customers",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            _listScreen.Controls.Add(new Label
            {
                Text = "Manage Customers",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 20f),
                Location = new Point(0, 30),
                AutoSize = true
            });

            _listScreen.Controls.Add(new Label
            {
                Text = "Customers · vehicles · interactions",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 76),
                AutoSize = true
            });

            var newCustomerBtn = new Button
            {
                Text = "+  New Customer",
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(180, 44),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            newCustomerBtn.FlatAppearance.BorderSize = 0;
            newCustomerBtn.Click += async (s, e) =>
            {
                using var dlg = new CustomerEditDialog(null);
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                    await LoadCustomersAsync();
            };
            _listScreen.Controls.Add(newCustomerBtn);

            // ---- Tab strip ----
            var tabBar = new Panel
            {
                Location = new Point(0, 118),
                Height = 44,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);
            };
            _listScreen.Controls.Add(tabBar);

            _tabActive = MakeTab("Active Customers", 0);
            _tabArchived = MakeTab("Archived Customers", TabWidth + TabGap);
            tabBar.Controls.Add(_tabActive);
            tabBar.Controls.Add(_tabArchived);

            _tabUnderline = new Panel
            {
                Height = 2,
                Width = TabWidth,
                BackColor = Accent,
                Location = new Point(_tabActive.Left, 42)
            };
            tabBar.Controls.Add(_tabUnderline);

            _tabActive.Click += async (s, e) =>
            {
                if (_tab == ListTab.Active) return;
                _tab = ListTab.Active;
                StyleTabs();
                await LoadCustomersAsync();
            };
            _tabArchived.Click += async (s, e) =>
            {
                if (_tab == ListTab.Archived) return;
                _tab = ListTab.Archived;
                StyleTabs();
                await LoadCustomersAsync();
            };

            // ---- Drill-down chip ----
            _drillDownHost = new Panel
            {
                Location = new Point(0, 172),
                Height = 36,
                BackColor = PageBg,
                Visible = !string.IsNullOrEmpty(_drillDownSegment),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _listScreen.Controls.Add(_drillDownHost);

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
                ForeColor = Accent,
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
                ForeColor = Accent,
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

            _drillDownHost.Controls.Add(_drillDownChip);

            // ---- Filter card (96 px tall) ----
            var filterCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(0, 216),
                Height = 96
            };
            filterCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, filterCard.Width - 1, filterCard.Height - 1);
            };
            _listScreen.Controls.Add(filterCard);

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
                Location = new Point(16, 38),
                Size = new Size(400, 36),
                BackColor = Color.White,
                Padding = new Padding(10, 7, 10, 0)
            };
            var searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "Search by name, plate, phone or email",
                Dock = DockStyle.Top
            };
            searchWrap.Controls.Add(searchBox);

            bool focused = false;
            searchBox.Enter += (s, e) => { focused = true; searchWrap.Invalidate(); };
            searchBox.Leave += (s, e) => { focused = false; searchWrap.Invalidate(); };
            searchWrap.Resize += (s, e) => searchWrap.Invalidate();
            searchWrap.Paint += (s, e) =>
            {
                using var pen = new Pen(focused ? Accent : Faint);
                e.Graphics.DrawRectangle(pen, 0, 0, searchWrap.Width - 1, searchWrap.Height - 1);
            };
            searchBox.TextChanged += (s, e) =>
            {
                _searchDebounce.Stop();
                _searchDebounce.Start();
            };
            searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _searchDebounce.Stop();
                    ApplyFilter();
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
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            searchBtn.FlatAppearance.BorderSize = 0;
            searchBtn.Click += (s, e) => ApplyFilter();
            filterCard.Controls.Add(searchBtn);

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
            refreshBtn.Click += async (s, e) => await LoadCustomersAsync();
            filterCard.Controls.Add(refreshBtn);

            var listPanel = new Panel { BackColor = PageBg, AutoScroll = false };
            _listScreen.Controls.Add(listPanel);

            var pager = new Panel { BackColor = PageBg };
            _listScreen.Controls.Add(pager);

            listPanel.Tag = new ListScreenState
            {
                SearchBox = searchBox,
                ListPanel = listPanel,
                Pager = pager
            };

            void Relayout()
            {
                var w = _listScreen.ClientSize.Width;
                var h = _listScreen.ClientSize.Height;

                if (w < 100 || h < 100) return;

                newCustomerBtn.Location = new Point(w - newCustomerBtn.Width, 30);
                filterCard.Width = w;
                tabBar.Width = w;
                _drillDownHost.Width = w;

                refreshBtn.Location = new Point(w - 16 - refreshBtn.Width, 38);
                searchBtn.Location = new Point(refreshBtn.Left - 10 - searchBtn.Width, 38);
                searchWrap.Width = Math.Max(120, searchBtn.Left - 12 - searchWrap.Left);

                int extra = _drillDownHost.Visible ? 36 : 0;
                filterCard.Location = new Point(0, 216 + extra);

                const int baseListTop = 324;
                int listTop = baseListTop + extra;
                const int pagerH = 44;

                pager.SetBounds(0, Math.Max(listTop, h - pagerH), w, pagerH);
                listPanel.SetBounds(0, listTop, w,
                    Math.Max(0, h - listTop - pagerH - 6));

                ApplyFilter(false);
            }
            _listScreen.Resize += (s, e) => Relayout();
            _listScreen.HandleCreated += (s, e) => Relayout();
            _listScreen.SizeChanged += (s, e) => Relayout();
            Relayout();

            StyleTabs();
        }

        private Label MakeTab(string text, int x) => new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 10.5f),
            ForeColor = Muted,
            AutoSize = false,
            Size = new Size(TabWidth, 42),
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
                _tabUnderline.Width = TabWidth;
            }
        }

        private sealed class ListScreenState
        {
            public TextBox SearchBox = null!;
            public Panel ListPanel = null!;
            public Panel Pager = null!;
        }

        private ListScreenState? ListState => _listScreen.Controls
            .OfType<Panel>()
            .FirstOrDefault(p => p.Tag is ListScreenState)?.Tag as ListScreenState;

        // =================================================================
        //  DRILL-DOWN
        // =================================================================
        private static string DisplaySegment(string seg) => seg switch
        {
            "AtRisk" => "At Risk",
            "Returning" => "Returning",
            "Active" => "Active",
            "Lost" => "Lost",
            "New" => "New",
            "Occasional" => "Occasional",
            "Regular" => "Regular",
            "Loyal" => "Loyal",
            _ => seg
        };

        public async void ApplyDrillDown(string segment, int? focusCustomerId = null, string source = null)
        {
            _drillDownSegment = segment ?? "";
            _focusCustomerId = focusCustomerId;
            _source = source ?? "";

            if (string.IsNullOrEmpty(_drillDownSegment) || _drillDownSegment == "All")
            {
                _drillDownSegment = "";
                _drillDownCustomerIds = null;
                if (_drillDownChipLabel != null) _drillDownChipLabel.Text = "";
                if (_drillDownHost != null) _drillDownHost.Visible = false;
            }
            else
            {
                var labelText = "Segment: " + DisplaySegment(_drillDownSegment);
                if (!string.IsNullOrEmpty(_source))
                    labelText += "  ·  from " + DisplaySource(_source);
                if (_drillDownChipLabel != null) _drillDownChipLabel.Text = labelText;
                if (_drillDownHost != null) _drillDownHost.Visible = true;

                await LoadDrillDownIdsAsync(_drillDownSegment);
            }

            // If we're currently showing the detail screen, switch back to list.
            if (_detailScreen != null)
            {
                ShowList();
            }
            else if (_drillDownHost != null)
            {
                _listScreen.PerformLayout();
            }

            _page = 1;
            await LoadCustomersAsync();
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
            ApplyDrillDown("", null, null);
        }

        private async Task LoadDrillDownIdsAsync(string segment)
        {
            try
            {
                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                if (segment == "AtRisk" || segment == "Active" || segment == "Lost")
                {
                    var rows = await _http.GetFromJsonAsync<List<SegmentCustomerDto>>(
                        $"api/analytics/segment-customers?companyId={companyId}&segment={Uri.EscapeDataString(segment)}")
                        ?? new List<SegmentCustomerDto>();

                    _drillDownCustomerIds = rows.Select(r => r.CustomerId).ToHashSet();
                }
                else if (segment == "New" || segment == "Occasional"
                      || segment == "Regular" || segment == "Loyal")
                {
                    // Same tier classifier the Wash Frequency chart uses.
                    var customers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                        $"api/tenant/{companyId}/tenant-customers") ?? new List<TenantCustomerDto>();
                    var requests = await _http.GetFromJsonAsync<List<ServiceRequestDto>>(
                        $"api/service-requests?companyId={companyId}") ?? new List<ServiceRequestDto>();

                    var ids = new HashSet<int>();
                    foreach (var c in customers)
                    {
                        int visits = requests.Count(r =>
                            r.CustomerId == c.TenantCustomerId &&
                            r.Status == "Completed" &&
                            !r.IsArchived);

                        string tier = visits <= 1 ? "New"
                                    : visits <= 4 ? "Occasional"
                                    : visits <= 9 ? "Regular"
                                    : "Loyal";

                        if (tier == segment) ids.Add(c.TenantCustomerId);
                    }
                    _drillDownCustomerIds = ids;
                }
                else if (segment == "Returning")
                {
                    // Returning = 2+ completed visits ever.
                    var customers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                        $"api/tenant/{companyId}/tenant-customers") ?? new List<TenantCustomerDto>();
                    var requests = await _http.GetFromJsonAsync<List<ServiceRequestDto>>(
                        $"api/service-requests?companyId={companyId}") ?? new List<ServiceRequestDto>();

                    var ids = new HashSet<int>();
                    foreach (var c in customers)
                    {
                        int visits = requests.Count(r =>
                            r.CustomerId == c.TenantCustomerId &&
                            r.Status == "Completed" &&
                            !r.IsArchived);
                        if (visits >= 2) ids.Add(c.TenantCustomerId);
                    }
                    _drillDownCustomerIds = ids;
                }
                else
                {
                    _drillDownCustomerIds = null;
                }
            }
            catch
            {
                _drillDownCustomerIds = null;
            }
        }

        // =================================================================
        //  DATA LOAD
        // =================================================================
        private async Task LoadCustomersAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                if (_tab == ListTab.Active)
                {
                    var list = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                        $"api/tenant/{companyId}/tenant-customers");
                    _allCustomers = list ?? new List<TenantCustomerDto>();
                }
                else
                {
                    var list = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                        $"api/tenant/{companyId}/tenant-customers/archived");
                    _archivedCustomers = list ?? new List<TenantCustomerDto>();
                }

                ApplyFilter(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customers.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private List<TenantCustomerDto> CurrentSource =>
            _tab == ListTab.Active ? _allCustomers : _archivedCustomers;

        private void ApplyFilter(bool resetPage = true)
        {
            var st = ListState;
            if (st == null) return;

            var search = st.SearchBox.Text?.Trim().ToLower() ?? "";

            var filtered = CurrentSource.Where(c =>
                string.IsNullOrEmpty(search) ||
                (c.CustomerName?.ToLower().Contains(search) ?? false) ||
                (c.EmailAddress?.ToLower().Contains(search) ?? false) ||
                (c.ContactNumber?.ToLower().Contains(search) ?? false) ||
                (c.PlateNumber?.ToLower().Contains(search) ?? false) ||
                (c.CustomerCode?.ToLower().Contains(search) ?? false)
            ).ToList();

            if (_drillDownCustomerIds != null)
            {
                filtered = filtered.Where(c => _drillDownCustomerIds.Contains(c.TenantCustomerId)).ToList();
            }

            _pageSize = Math.Max(1, (st.ListPanel.ClientSize.Height + RowGap) / (RowHeight + RowGap));
            int totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)_pageSize));
            if (resetPage) _page = 1;
            _page = Math.Min(Math.Max(1, _page), totalPages);

            // If we're focusing a specific customer, jump to the page containing them.
            if (_focusCustomerId.HasValue && _tab == ListTab.Active)
            {
                int idx = filtered.FindIndex(c => c.TenantCustomerId == _focusCustomerId.Value);
                if (idx >= 0) _page = idx / _pageSize + 1;
            }

            var pageItems = filtered.Skip((_page - 1) * _pageSize).Take(_pageSize).ToList();

            st.ListPanel.SuspendLayout();
            st.ListPanel.Controls.Clear();

            int y = 0;
            foreach (var c in pageItems)
            {
                var row = _tab == ListTab.Active
                    ? BuildActiveCustomerRow(c)
                    : BuildArchivedCustomerRow(c);

                row.Tag = c.TenantCustomerId;
                row.Location = new Point(0, y);
                row.Width = st.ListPanel.ClientSize.Width;
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                st.ListPanel.Controls.Add(row);
                y += row.Height + RowGap;
            }

            if (filtered.Count == 0)
            {
                var emptyText = _tab == ListTab.Active
                    ? "No active customers."
                    : "No archived customers.";

                if (!string.IsNullOrEmpty(_drillDownSegment))
                    emptyText = "No customers match this segment.";

                st.ListPanel.Controls.Add(new Label
                {
                    Text = emptyText,
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 10f),
                    AutoSize = true,
                    Location = new Point(10, 20)
                });
            }

            st.ListPanel.ResumeLayout();
            RenderPager(st, filtered.Count, totalPages);

            // Apply highlight + scroll AFTER layout so positions are real.
            if (_focusCustomerId.HasValue && _tab == ListTab.Active)
                BeginInvoke(new Action(() => HighlightAndScrollTo(_focusCustomerId.Value)));
        }

        private void RenderPager(ListScreenState st, int total, int totalPages)
        {
            var pager = st.Pager;
            pager.SuspendLayout();
            pager.Controls.Clear();

            int from = total == 0 ? 0 : (_page - 1) * _pageSize + 1;
            int to = Math.Min(_page * _pageSize, total);

            pager.Controls.Add(new Label
            {
                Text = $"Showing {from}–{to} of {total}",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Location = new Point(0, 13)
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
                int x = pager.Width - (items.Count * bw + (items.Count - 1) * gap);

                foreach (var it in items)
                {
                    var b = PagerButton(it.Text, it.Active, it.Enabled);
                    b.SetBounds(x, 5, bw, bh);
                    int target = it.Page;
                    b.Click += (s, e) => { _page = target; ApplyFilter(false); };
                    pager.Controls.Add(b);
                    x += bw + gap;
                }
            }

            pager.ResumeLayout();
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

        // =================================================================
        //  HIGHLIGHT + SCROLL
        // =================================================================
        private void HighlightAndScrollTo(int customerId)
        {
            var st = ListState;
            if (st == null) return;

            Control? target = null;
            foreach (Control c in st.ListPanel.Controls)
            {
                if (c.Tag is int id && id == customerId)
                {
                    target = c;
                    break;
                }
            }

            if (target == null) return;

            st.ListPanel.ScrollControlIntoView(target);

            var original = target.BackColor;
            target.BackColor = HighlightTint;
            target.Invalidate();

            foreach (Control child in target.Controls)
            {
                if (child is Label lbl && child is not Button)
                {
                    // leave labels alone — they're transparent over the panel
                }
            }

            _highlightTimer.Stop();
            _highlightTimer.Start();
        }

        private void ClearRowHighlights()
        {
            var st = ListState;
            if (st == null) return;

            foreach (Control c in st.ListPanel.Controls)
            {
                if (c.BackColor == HighlightTint)
                {
                    c.BackColor = Color.White;
                    c.Invalidate();
                }
            }
        }

        // =================================================================
        //  ROW BUILDERS
        // =================================================================
        private Panel BuildActiveCustomerRow(TenantCustomerDto c)
        {
            var row = new Panel
            {
                Height = RowHeight,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            row.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
            };

            var avatar = new Label
            {
                Text = Initials(c.CustomerName),
                ForeColor = Accent,
                BackColor = BlueSoft,
                Font = new Font("Segoe UI Semibold", 11f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(48, 48),
                Location = new Point(20, 22)
            };
            row.Controls.Add(avatar);

            row.Controls.Add(new Label
            {
                Text = c.CustomerName ?? "",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11.5f),
                Location = new Point(88, 16),
                AutoSize = true
            });

            row.Controls.Add(new Label
            {
                Text = c.EmailAddress ?? "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(88, 40),
                AutoSize = true
            });

            var meta = string.Join(" · ", new[]
            {
                c.ContactNumber, c.PlateNumber, c.CustomerCode
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            row.Controls.Add(new Label
            {
                Text = meta,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(88, 62),
                AutoSize = true
            });

            var statusLbl = new Label
            {
                Text = c.IsActive ? "Active" : "Inactive",
                ForeColor = c.IsActive ? Green : Red,
                Font = new Font("Segoe UI Semibold", 9.5f),
                TextAlign = ContentAlignment.MiddleRight,
                Size = new Size(100, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(0, 12)
            };
            row.Controls.Add(statusLbl);

            var archiveBtn = new Button
            {
                Text = "Archive",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Red,
                BackColor = Color.White,
                Size = new Size(100, 32),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(0, 40)
            };
            archiveBtn.FlatAppearance.BorderColor = Red;
            archiveBtn.FlatAppearance.MouseOverBackColor = RedSoft;
            archiveBtn.Click += async (s, e) => await ArchiveCustomerAsync(c);
            row.Controls.Add(archiveBtn);

            void PositionRightCluster()
            {
                int right = row.Width - 20;
                statusLbl.Location = new Point(right - statusLbl.Width, 12);
                archiveBtn.Location = new Point(right - archiveBtn.Width, 40);
            }
            row.Resize += (s, e) => PositionRightCluster();
            PositionRightCluster();

            void OpenDetail(object? s, EventArgs e) => ShowDetail(c);
            row.Click += OpenDetail;
            avatar.Click += OpenDetail;
            foreach (Control child in row.Controls)
            {
                if (child is Label) child.Click += OpenDetail;
            }

            return row;
        }

        private Panel BuildArchivedCustomerRow(TenantCustomerDto c)
        {
            var row = new Panel
            {
                Height = RowHeight,
                BackColor = Color.White
            };
            row.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
                using var bar = new SolidBrush(Amber);
                e.Graphics.FillRectangle(bar, 0, 0, 4, row.Height);
            };

            var avatar = new Label
            {
                Text = Initials(c.CustomerName),
                ForeColor = Faint,
                BackColor = Color.FromArgb(0xF1, 0xF4, 0xF9),
                Font = new Font("Segoe UI Semibold", 11f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(48, 48),
                Location = new Point(20, 22)
            };
            row.Controls.Add(avatar);

            row.Controls.Add(new Label
            {
                Text = c.CustomerName ?? "",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11.5f),
                Location = new Point(88, 16),
                AutoSize = true
            });

            row.Controls.Add(new Label
            {
                Text = c.EmailAddress ?? "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(88, 40),
                AutoSize = true
            });

            var archivedAt = c.ArchivedAt?.ToString("MMM d, yyyy h:mm tt") ?? "—";
            var archivedBy = string.IsNullOrWhiteSpace(c.ArchivedBy) ? "—" : c.ArchivedBy;

            row.Controls.Add(new Label
            {
                Text = $"Archived {archivedAt}  ·  by {archivedBy}",
                ForeColor = Amber,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(88, 62),
                AutoSize = true
            });

            var restoreBtn = new Button
            {
                Text = "Restore",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Green,
                BackColor = Color.White,
                Size = new Size(100, 32),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(0, 30)
            };
            restoreBtn.FlatAppearance.BorderColor = Green;
            restoreBtn.FlatAppearance.MouseOverBackColor = GreenSoft;
            restoreBtn.Click += async (s, e) => await RestoreCustomerAsync(c);
            row.Controls.Add(restoreBtn);

            void PositionRestore()
            {
                int right = row.Width - 20;
                restoreBtn.Location = new Point(right - restoreBtn.Width, (RowHeight - restoreBtn.Height) / 2);
            }
            row.Resize += (s, e) => PositionRestore();
            PositionRestore();

            return row;
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        // =================================================================
        //  ARCHIVE / RESTORE
        // =================================================================
        private async Task ArchiveCustomerAsync(TenantCustomerDto c)
        {
            if (!ArchiveConfirmDialog.ConfirmArchive("customer")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsJsonAsync(
                    $"api/tenant/{CarwashServices.Auth.SessionUser.CurrentCompanyId}/tenant-customers/{c.TenantCustomerId}/archive",
                    new { archivedBy = CurrentUserName });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Archive failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Archive failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private async Task RestoreCustomerAsync(TenantCustomerDto c)
        {
            if (!ArchiveConfirmDialog.ConfirmRestore("customer")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsync(
                    $"api/tenant/{CarwashServices.Auth.SessionUser.CurrentCompanyId}/tenant-customers/{c.TenantCustomerId}/restore", null);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Restore failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        // =================================================================
        //  SCREEN 2 — DETAIL  (unchanged)
        // =================================================================
        private void ShowDetail(TenantCustomerDto customer)
        {
            _currentCustomer = customer;
            _root.Controls.Clear();

            _detailScreen = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            _root.Controls.Add(_detailScreen);

            var backBtn = new Button
            {
                Text = "‹  Back to list",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                Size = new Size(150, 40),
                Location = new Point(0, 0),
                Cursor = Cursors.Hand
            };
            backBtn.FlatAppearance.BorderColor = CardBorder;
            backBtn.Click += (s, e) => ShowList();
            _detailScreen.Controls.Add(backBtn);

            var editBtn = new Button
            {
                Text = "Edit Profile",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                Size = new Size(140, 40),
                Location = new Point(600, 0),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            editBtn.FlatAppearance.BorderColor = CardBorder;
            editBtn.Click += async (s, e) =>
            {
                using var dlg = new CustomerEditDialog(customer.TenantCustomerId);
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    await LoadCustomersAsync();
                    var refreshed = _allCustomers.FirstOrDefault(x => x.TenantCustomerId == customer.TenantCustomerId);
                    if (refreshed != null) ShowDetail(refreshed);
                }
            };
            _detailScreen.Controls.Add(editBtn);

            StyleOutlineButton(backBtn);
            StyleOutlineButton(editBtn);

            void PositionEditBtn()
            {
                var x = Math.Max(0, _detailScreen.ClientSize.Width - editBtn.Width);
                editBtn.Location = new Point(x, 0);
            }
            _detailScreen.Resize += (s, e) => PositionEditBtn();
            _detailScreen.HandleCreated += (s, e) => PositionEditBtn();
            PositionEditBtn();

            var header = new Panel
            {
                Location = new Point(0, 60),
                Height = 110,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, header.Width - 1, header.Height - 1);
            };
            _detailScreen.Controls.Add(header);

            var avatar = new Label
            {
                Text = Initials(customer.CustomerName),
                ForeColor = Accent,
                BackColor = BlueSoft,
                Font = new Font("Segoe UI Semibold", 16f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(64, 64),
                Location = new Point(24, 22)
            };
            header.Controls.Add(avatar);

            var nameLbl = new Label
            {
                Text = customer.CustomerName ?? "",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(108, 22),
                AutoSize = true
            };
            header.Controls.Add(nameLbl);

            var subLbl = new Label
            {
                Text = $"{customer.CustomerCode}  |  {customer.Source ?? "—"}",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(108, 56),
                AutoSize = true
            };
            header.Controls.Add(subLbl);

            var pill = new Label
            {
                Text = customer.IsActive ? "Active" : "Inactive",
                ForeColor = customer.IsActive ? Green : Red,
                Font = new Font("Segoe UI Semibold", 9.5f),
                TextAlign = ContentAlignment.MiddleRight,
                Size = new Size(90, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(header.Width - 114, 34)
            };
            header.Controls.Add(pill);
            header.Resize += (s, e) => pill.Location = new Point(header.Width - 114, 34);

            var tabBar = new Panel
            {
                Location = new Point(0, 180),
                Height = 44,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            tabBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);
            };
            _detailScreen.Controls.Add(tabBar);

            var tabProfile = new Label
            {
                Text = "Profile",
                Font = new Font("Segoe UI Semibold", 10.5f),
                ForeColor = Navy,
                AutoSize = false,
                Size = new Size(100, 42),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            tabBar.Controls.Add(tabProfile);

            var tabInteractions = new Label
            {
                Text = "Interactions",
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = Muted,
                AutoSize = false,
                Size = new Size(130, 42),
                Location = new Point(140, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            tabBar.Controls.Add(tabInteractions);

            var underline = new Panel
            {
                Height = 2,
                Width = 100,
                BackColor = Accent,
                Location = new Point(0, 42)
            };
            tabBar.Controls.Add(underline);

            var tabContent = new Panel
            {
                Location = new Point(0, 238),
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _detailScreen.Controls.Add(tabContent);

            void LayoutDetail()
            {
                header.Width = _detailScreen.ClientSize.Width;
                tabBar.Width = _detailScreen.ClientSize.Width;
                tabContent.Size = new Size(
                    _detailScreen.ClientSize.Width,
                    _detailScreen.ClientSize.Height - tabContent.Top);
            }
            _detailScreen.Resize += (s, e) => LayoutDetail();
            LayoutDetail();

            void SelectProfile()
            {
                tabProfile.ForeColor = Navy;
                tabProfile.Font = new Font("Segoe UI Semibold", 10.5f);
                tabInteractions.ForeColor = Muted;
                tabInteractions.Font = new Font("Segoe UI", 10.5f);
                underline.Location = new Point(tabProfile.Left, 42);
                underline.Width = tabProfile.Width;
                tabContent.Controls.Clear();
                tabContent.Controls.Add(BuildProfileTab(customer));
            }

            void SelectInteractions()
            {
                tabInteractions.ForeColor = Navy;
                tabInteractions.Font = new Font("Segoe UI Semibold", 10.5f);
                tabProfile.ForeColor = Muted;
                tabProfile.Font = new Font("Segoe UI", 10.5f);
                underline.Location = new Point(tabInteractions.Left, 42);
                underline.Width = tabInteractions.Width;
                tabContent.Controls.Clear();
                tabContent.Controls.Add(BuildInteractionsTab(customer));
            }

            tabProfile.Click += (s, e) => SelectProfile();
            tabInteractions.Click += (s, e) => SelectInteractions();

            SelectProfile();
        }

        private static void StyleOutlineButton(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            b.BackColor = Color.White;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE9, 0xEE, 0xF6);
        }

        private Control BuildProfileTab(TenantCustomerDto c)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                Height = 340,
                BackColor = Color.White
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            const int colW = 340;
            int left = 28;
            int right = left + colW + 40;

            card.Controls.Add(SectionHeader("CONTACT", left, 22));
            int y = 52;
            y = Field(card, "Email", Dash(c.EmailAddress), left, y, colW);
            y = Field(card, "Phone", Dash(c.ContactNumber), left, y, colW);
            y = Field(card, "Address", Dash(c.Address), left, y, colW);

            card.Controls.Add(SectionHeader("VEHICLE", right, 22));
            int y2 = 52;
            y2 = Field(card, "Plate number", Dash(c.PlateNumber), right, y2, colW);
            y2 = Field(card, "Make", Dash(c.VehicleMake), right, y2, colW);
            y2 = Field(card, "Model", Dash(c.VehicleModel), right, y2, colW);
            y2 = Field(card, "Year", c.VehicleYear?.ToString() ?? "—", right, y2, colW);
            y2 = Field(card, "Color", Dash(c.VehicleColor), right, y2, colW);
            y2 = Field(card, "Type", Dash(c.VehicleType), right, y2, colW);

            int bottom = Math.Max(y, y2) + 14;
            card.Controls.Add(SectionHeader("OTHER", left, bottom));
            int y3 = bottom + 30;
            y3 = Field(card, "Source", Dash(c.Source), left, y3, colW);
            y3 = Field(card, "Registered", c.CreatedAt.ToString("yyyy-MM-dd"), left, y3, colW);

            card.Height = y3 + 16;
            return card;
        }

        private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

        private static Label SectionHeader(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private int Field(Control parent, string label, string value, int x, int y, int colW)
        {
            parent.Controls.Add(new Label
            {
                Text = label,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            });

            parent.Controls.Add(new Label
            {
                Text = value,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Location = new Point(x + 130, y),
                AutoSize = false,
                AutoEllipsis = true,
                Size = new Size(colW - 130, 20)
            });

            return y + 28;
        }

        private Button ActionButton(string text, Color back, int x)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = back,
                Size = new Size(190, 40),
                Location = new Point(x, 0),
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(back, 0.08f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.15f);
            return b;
        }

        private Control BuildInteractionsTab(TenantCustomerDto c)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };

            var recordComplaint = ActionButton("+  Record Complaint", Red, 0);
            var recordFeedback = ActionButton("+  Record Feedback", Green, 200);
            host.Controls.Add(recordComplaint);
            host.Controls.Add(recordFeedback);

            var heading = new Label
            {
                Text = "Interactions",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(0, 58),
                AutoSize = true
            };
            host.Controls.Add(heading);

            var listPanel = new Panel
            {
                Location = new Point(0, 94),
                BackColor = PageBg,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            host.Controls.Add(listPanel);

            void LayoutTab()
            {
                listPanel.Size = new Size(
                    host.ClientSize.Width,
                    Math.Max(0, host.ClientSize.Height - listPanel.Top));
            }
            host.Resize += (s, e) => LayoutTab();
            LayoutTab();

            async Task LoadAsync()
            {
                List<CustomerInteractionDto> items;
                try
                {
                    items = await _http.GetFromJsonAsync<List<CustomerInteractionDto>>(
                        $"api/tenant/{CarwashServices.Auth.SessionUser.CurrentCompanyId}/customer-interactions?customerId={c.TenantCustomerId}")
                        ?? new List<CustomerInteractionDto>();
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    items = new List<CustomerInteractionDto>();
                }
                catch
                {
                    items = new List<CustomerInteractionDto>();
                }

                heading.Text = $"Interactions ({items.Count})";

                listPanel.SuspendLayout();
                listPanel.Controls.Clear();

                int y = 0;
                foreach (var it in items.OrderByDescending(x => x.CreatedAt))
                {
                    var row = BuildInteractionRow(it, LoadAsync);
                    row.Location = new Point(0, y);
                    row.Width = listPanel.ClientSize.Width - 4;
                    row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    listPanel.Controls.Add(row);
                    y += row.Height + 10;
                }

                if (items.Count == 0)
                {
                    listPanel.Controls.Add(new Label
                    {
                        Text = "No interactions yet. Use the buttons above to record one.",
                        ForeColor = Muted,
                        Font = new Font("Segoe UI", 9.5f),
                        AutoSize = true,
                        Location = new Point(2, 8)
                    });
                }

                listPanel.ResumeLayout();
            }

            recordComplaint.Click += async (s, e) =>
            {
                using var dlg = new InteractionEditDialog(c.TenantCustomerId, "Complaint");
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                    await LoadAsync();
            };
            recordFeedback.Click += async (s, e) =>
            {
                using var dlg = new InteractionEditDialog(c.TenantCustomerId, "Feedback");
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                    await LoadAsync();
            };

            _ = LoadAsync();

            return host;
        }

        private Panel BuildInteractionRow(CustomerInteractionDto it, Func<Task> reload)
        {
            bool isComplaint = string.Equals(it.Kind, "Complaint", StringComparison.OrdinalIgnoreCase);
            bool resolved = string.Equals(it.Status, "Resolved", StringComparison.OrdinalIgnoreCase);
            Color barColor = isComplaint ? Red : Green;
            Color kindBg = isComplaint ? RedSoft : GreenSoft;
            Color kindFg = isComplaint ? Red : Green;

            var row = new Panel
            {
                Height = 118,
                BackColor = Color.White
            };
            row.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
                using var bar = new SolidBrush(barColor);
                e.Graphics.FillRectangle(bar, 0, 0, 4, row.Height);
            };

            var kind = new Label
            {
                Text = it.Kind.ToUpperInvariant(),
                ForeColor = kindFg,
                BackColor = kindBg,
                Font = new Font("Segoe UI Semibold", 8f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(90, 20),
                Location = new Point(20, 14)
            };
            row.Controls.Add(kind);

            var sev = new Label
            {
                Text = it.Severity ?? "Normal",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(118, 14),
                AutoSize = true
            };
            row.Controls.Add(sev);

            var statusPill = new Label
            {
                Text = it.Status ?? "Open",
                ForeColor = resolved ? Green : Amber,
                BackColor = resolved ? GreenSoft : AmberSoft,
                Font = new Font("Segoe UI Semibold", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(80, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(row.Width - 100, 12)
            };
            row.Controls.Add(statusPill);
            row.Resize += (s, e) => statusPill.Location = new Point(row.Width - 100, 12);

            if (!resolved)
            {
                var editBtn = new Button
                {
                    Text = "Edit",
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI Semibold", 9f),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    Size = new Size(80, 30),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Location = new Point(row.Width - 100, 46)
                };
                editBtn.FlatAppearance.BorderColor = CardBorder;
                editBtn.Click += async (s, e) =>
                {
                    using var dlg = new InteractionEditDialog(it.CustomerId, it.Kind, it.InteractionId);
                    if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                        await reload();
                };
                row.Controls.Add(editBtn);
                row.Resize += (s, e) => editBtn.Location = new Point(row.Width - 100, 46);
            }

            var title = new Label
            {
                Text = it.Title ?? "",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11f),
                Location = new Point(20, 44),
                AutoSize = false,
                AutoEllipsis = true,
                Size = new Size(row.Width - 130, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row.Controls.Add(title);
            row.Resize += (s, e) => title.Width = Math.Max(80, row.Width - 130);

            var details = new Label
            {
                Text = it.Details ?? "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(20, 68),
                AutoSize = false,
                AutoEllipsis = true,
                Size = new Size(row.Width - 40, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row.Controls.Add(details);
            row.Resize += (s, e) => details.Width = Math.Max(80, row.Width - 40);

            var date = new Label
            {
                Text = it.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                ForeColor = Faint,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(20, 92),
                AutoSize = true
            };
            row.Controls.Add(date);

            return row;
        }
    }
}