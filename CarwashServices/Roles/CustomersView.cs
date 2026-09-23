using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dialogs;
using CarwashServices.Dtos;

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

        // -----------------------------------------------------------------
        //  State
        // -----------------------------------------------------------------
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<TenantCustomerDto> _allCustomers = new();
        private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 300 };

        // Pagination
        private int _page = 1;
        private int _pageSize = 6;
        private const int RowHeight = 92;
        private const int RowGap = 12;

        // Root — swapped between list / detail
        private Panel _root;

        // List-screen controls
        private Panel _listScreen;

        // Detail-screen controls
        private Panel _detailScreen;
        private TenantCustomerDto? _currentCustomer;

        // -----------------------------------------------------------------
        //  Ctor
        // -----------------------------------------------------------------
        public CustomersView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _searchDebounce.Tick += (s, e) => { _searchDebounce.Stop(); ApplyFilter(); };

            BuildRoot();
            ShowList();

            Load += async (s, e) => await LoadCustomersAsync();
        }

        // -----------------------------------------------------------------
        //  Root container
        // -----------------------------------------------------------------
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

            _listScreen = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            _root.Controls.Add(_listScreen);

            // ---- Breadcrumb ----
            _listScreen.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Customers",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            // ---- Header ----
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

            // ---- New Customer button ----
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

            // ---- Filter card (search only) ----
            var filterCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(0, 118),
                Height = 76
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

            // Bordered wrapper + borderless TextBox = a clearly visible search field
            var searchWrap = new Panel
            {
                Location = new Point(16, 28),
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
                UseVisualStyleBackColor = false
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
                Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderColor = CardBorder;
            StyleOutlineButton(refreshBtn);
            refreshBtn.Click += async (s, e) => await LoadCustomersAsync();
            filterCard.Controls.Add(refreshBtn);

            // ---- List panel (no scroll) + pager ----
            var listPanel = new Panel
            {
                BackColor = PageBg,
                AutoScroll = false
            };
            _listScreen.Controls.Add(listPanel);

            var pager = new Panel { BackColor = PageBg };
            _listScreen.Controls.Add(pager);

            listPanel.Tag = new ListScreenState
            {
                SearchBox = searchBox,
                ListPanel = listPanel,
                Pager = pager
            };

            const int listTop = 206;
            const int pagerH = 44;

            void Relayout()
            {
                var w = _listScreen.ClientSize.Width;
                var h = _listScreen.ClientSize.Height;

                newCustomerBtn.Location = new Point(w - newCustomerBtn.Width, 30);
                filterCard.Width = w;

                refreshBtn.Location = new Point(w - 16 - refreshBtn.Width, 28);
                searchBtn.Location = new Point(refreshBtn.Left - 10 - searchBtn.Width, 28);
                searchWrap.Width = Math.Max(120, searchBtn.Left - 12 - searchWrap.Left);

                pager.SetBounds(0, Math.Max(listTop, h - pagerH), w, pagerH);
                listPanel.SetBounds(0, listTop, w, Math.Max(0, h - listTop - pagerH - 6));

                ApplyFilter(false);   // page size depends on available height
            }
            _listScreen.Resize += (s, e) => Relayout();
            Relayout();
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
        //  DATA LOAD
        // =================================================================
        private async Task LoadCustomersAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var list = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers");
                _allCustomers = list ?? new List<TenantCustomerDto>();
                ApplyFilter(false);   // stay on the current page
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

        private void ApplyFilter(bool resetPage = true)
        {
            var st = ListState;
            if (st == null) return;

            var search = st.SearchBox.Text?.Trim().ToLower() ?? "";

            var filtered = _allCustomers.Where(c =>
                string.IsNullOrEmpty(search) ||
                (c.CustomerName?.ToLower().Contains(search) ?? false) ||
                (c.EmailAddress?.ToLower().Contains(search) ?? false) ||
                (c.ContactNumber?.ToLower().Contains(search) ?? false) ||
                (c.PlateNumber?.ToLower().Contains(search) ?? false) ||
                (c.CustomerCode?.ToLower().Contains(search) ?? false)
            ).ToList();

            _pageSize = Math.Max(1, (st.ListPanel.ClientSize.Height + RowGap) / (RowHeight + RowGap));

            int totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)_pageSize));
            if (resetPage) _page = 1;
            _page = Math.Min(Math.Max(1, _page), totalPages);

            var pageItems = filtered.Skip((_page - 1) * _pageSize).Take(_pageSize).ToList();

            st.ListPanel.SuspendLayout();
            st.ListPanel.Controls.Clear();

            int y = 0;
            foreach (var c in pageItems)
            {
                var row = BuildCustomerRow(c);
                row.Location = new Point(0, y);
                row.Width = st.ListPanel.ClientSize.Width;
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                st.ListPanel.Controls.Add(row);
                y += row.Height + RowGap;
            }

            if (filtered.Count == 0)
            {
                st.ListPanel.Controls.Add(new Label
                {
                    Text = "No customers match your search.",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 10f),
                    AutoSize = true,
                    Location = new Point(10, 20)
                });
            }

            st.ListPanel.ResumeLayout();
            RenderPager(st, filtered.Count, totalPages);
        }

        // ---- Pager ----
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
                for (int p = start; p <= end; p++)
                    items.Add((p.ToString(), p, p == _page, true));
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

        // ---- One row of the customer list ----
        private Panel BuildCustomerRow(TenantCustomerDto c)
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

            var initials = Initials(c.CustomerName);
            var avatar = new Label
            {
                Text = initials,
                ForeColor = Accent,
                BackColor = BlueSoft,
                Font = new Font("Segoe UI Semibold", 11f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(48, 48),
                Location = new Point(20, 22)
            };
            row.Controls.Add(avatar);

            var name = new Label
            {
                Text = c.CustomerName ?? "",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11.5f),
                Location = new Point(88, 16),
                AutoSize = true
            };
            row.Controls.Add(name);

            var email = new Label
            {
                Text = c.EmailAddress ?? "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(88, 40),
                AutoSize = true
            };
            row.Controls.Add(email);

            var meta = string.Join(" · ", new[]
            {
                c.ContactNumber,
                c.PlateNumber,
                c.CustomerCode
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            var metaLbl = new Label
            {
                Text = meta,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(88, 62),
                AutoSize = true
            };
            row.Controls.Add(metaLbl);

            var statusLbl = new Label
            {
                Text = c.IsActive ? "Active" : "Inactive",
                ForeColor = c.IsActive ? Green : Red,
                Font = new Font("Segoe UI Semibold", 9.5f),
                TextAlign = ContentAlignment.MiddleRight,
                Size = new Size(90, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            row.Controls.Add(statusLbl);
            row.Resize += (s, e) => statusLbl.Location = new Point(row.Width - 110, 34);
            statusLbl.Location = new Point(row.Width - 110, 34);

            void OpenDetail(object? s, EventArgs e) => ShowDetail(c);
            row.Click += OpenDetail;
            foreach (Control child in row.Controls)
                child.Click += OpenDetail;

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
        //  SCREEN 2 — DETAIL (Profile / Interactions)
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

            // ---- Back to list ----
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

            // ---- Edit Profile ----
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

            // ---- Profile header card ----
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

            // ---- Tab strip ----
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
                Location = new Point(100, 0),
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

            // ---- Tab content host ----
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

        // =================================================================
        //  SHARED HELPERS
        // =================================================================
        private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

        private static void StyleOutlineButton(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            b.BackColor = Color.White;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE9, 0xEE, 0xF6);
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

        // =================================================================
        //  PROFILE TAB
        // =================================================================
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

            // CONTACT
            card.Controls.Add(SectionHeader("CONTACT", left, 22));
            int y = 52;
            y = Field(card, "Email", Dash(c.EmailAddress), left, y, colW);
            y = Field(card, "Phone", Dash(c.ContactNumber), left, y, colW);
            y = Field(card, "Address", Dash(c.Address), left, y, colW);

            // VEHICLE
            card.Controls.Add(SectionHeader("VEHICLE", right, 22));
            int y2 = 52;
            y2 = Field(card, "Plate number", Dash(c.PlateNumber), right, y2, colW);
            y2 = Field(card, "Make", Dash(c.VehicleMake), right, y2, colW);
            y2 = Field(card, "Model", Dash(c.VehicleModel), right, y2, colW);
            y2 = Field(card, "Year", c.VehicleYear?.ToString() ?? "—", right, y2, colW);
            y2 = Field(card, "Color", Dash(c.VehicleColor), right, y2, colW);
            y2 = Field(card, "Type", Dash(c.VehicleType), right, y2, colW);

            // OTHER
            int bottom = Math.Max(y, y2) + 14;
            card.Controls.Add(SectionHeader("OTHER", left, bottom));
            int y3 = bottom + 30;
            y3 = Field(card, "Source", Dash(c.Source), left, y3, colW);
            y3 = Field(card, "Registered", c.CreatedAt.ToString("yyyy-MM-dd"), left, y3, colW);

            card.Height = y3 + 16;
            return card;
        }

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

        private Label SectionHeader(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        // =================================================================
        //  INTERACTIONS TAB
        // =================================================================
        private Control BuildInteractionsTab(TenantCustomerDto c)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };

            // ---- Action row (red / green) ----
            var recordComplaint = ActionButton("+  Record Complaint", Red, 0);
            var recordFeedback = ActionButton("+  Record Feedback", Green, 200);
            host.Controls.Add(recordComplaint);
            host.Controls.Add(recordFeedback);

            // ---- Heading ----
            var heading = new Label
            {
                Text = "Interactions",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(0, 58),
                AutoSize = true
            };
            host.Controls.Add(heading);

            // ---- Scrollable list ----
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
                        $"api/tenant/1/customer-interactions?customerId={c.TenantCustomerId}")
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

        // ---- One interaction card (with optional inline Edit button) ----
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

            // ---- Status pill ----
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

            // ---- Inline Edit button (only when the record is still Open) ----
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