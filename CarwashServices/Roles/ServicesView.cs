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
    public class ServicesView : UserControl
    {
        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF5, 0xF7, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE5, 0xE8, 0xEE);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);

        private static readonly Font NameFont = new Font("Segoe UI Semibold", 10f);
        private static readonly Font StatusFont = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font ButtonFont = new Font("Segoe UI Semibold", 8.5f);

        private const int ActionsColW = 280;
        private const int ActionBtnW = 78;
        private const int ActionBtnH = 30;
        private const int ActionBtnGap = 8;
        private const int ArchiveBtnW = 92;

        // ---- Layout ----
        private const int MarginX = 30;
        private const int TopMargin = 20;
        private const int RowHeight = 56;
        private const int PagerH = 48;
        private const int PageBottom = 24;
        private const int GridTop = 310;

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private enum ListTab { Active, Archived }
        private ListTab _tab = ListTab.Active;

        private List<ProductDto> _allServices = new();
        private List<ProductDto> _filtered = new();
        private string _activeCategory = "All";
        private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 300 };

        private int _page = 1;
        private int _pageSize = 8;
        private bool _relayouting;

        private Panel _root = null!;
        private Panel _contentPanel = null!;
        private Panel _gridHost = null!;
        private DataGridView _grid = null!;
        private Panel _pager = null!;
        private TextBox _searchBox = null!;
        private Button _addBtn = null!;
        private Panel _chipBar = null!;
        private Panel _filterCard = null!;
        private Panel _searchWrap = null!;
        private Button _searchBtn = null!;
        private Button _refreshBtn = null!;
        private Panel _tabBar = null!;

        private Label _tabActive = null!;
        private Label _tabArchived = null!;
        private Panel _tabUnderline = null!;

        private int _hoverAction = -1;

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Admin" : SessionUser.FullName;

        public ServicesView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _searchDebounce.Tick += (s, e) => { _searchDebounce.Stop(); ApplyFilter(resetPage: true); };

            InitializeUI();

            // FIX: turn on double-buffering for every child control.
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadServicesAsync();
        }

        private void InitializeUI()
        {
            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(MarginX, TopMargin, MarginX, TopMargin)
            };
            Controls.Add(_root);

            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            _root.Controls.Add(_contentPanel);

            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Services",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Manage Services",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 30),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Manage your carwash services · pricing · duration · category",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 82),
                AutoSize = true
            });

            _addBtn = new Button
            {
                Text = "+  Add Service",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(170, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _addBtn.FlatAppearance.BorderSize = 0;
            _addBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _addBtn.Click += (s, e) => OpenAddServiceDialog();
            _contentPanel.Controls.Add(_addBtn);

            // ---- Tab strip ----
            _tabBar = new Panel
            {
                Location = new Point(0, 118),
                Height = 44,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _tabBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, _tabBar.Height - 1, _tabBar.Width, _tabBar.Height - 1);
            };
            _contentPanel.Controls.Add(_tabBar);

            const int TabWidth = 160;
            const int TabGap = 40;

            _tabActive = MakeTab("Active Services", 0, TabWidth);
            _tabArchived = MakeTab("Archived Services", TabWidth + TabGap, TabWidth);
            _tabBar.Controls.Add(_tabActive);
            _tabBar.Controls.Add(_tabArchived);

            _tabUnderline = new Panel
            {
                Height = 2,
                Width = TabWidth,
                BackColor = Accent,
                Location = new Point(_tabActive.Left, 42)
            };
            _tabBar.Controls.Add(_tabUnderline);

            _tabActive.Click += async (s, e) =>
            {
                if (_tab == ListTab.Active) return;
                _tab = ListTab.Active;
                StyleTabs();
                await LoadServicesAsync();
            };
            _tabArchived.Click += async (s, e) =>
            {
                if (_tab == ListTab.Archived) return;
                _tab = ListTab.Archived;
                StyleTabs();
                await LoadServicesAsync();
            };

            // ---- Category chips ----
            _chipBar = new Panel
            {
                Location = new Point(0, 172),
                Height = 40,
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            // ---- Filter card ----
            _filterCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(0, 222),
                Height = 76,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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

            _searchWrap = new Panel
            {
                Location = new Point(16, 28),
                Size = new Size(400, 36),
                BackColor = Color.White,
                Padding = new Padding(10, 7, 10, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "Search by service name or category",
                Dock = DockStyle.Top
            };
            _searchWrap.Controls.Add(_searchBox);

            bool focused = false;
            _searchBox.Enter += (s, e) => { focused = true; _searchWrap.Invalidate(); };
            _searchBox.Leave += (s, e) => { focused = false; _searchWrap.Invalidate(); };
            _searchWrap.Resize += (s, e) => _searchWrap.Invalidate();
            _searchWrap.Paint += (s, e) =>
            {
                using var pen = new Pen(focused ? Accent : Faint);
                e.Graphics.DrawRectangle(pen, 0, 0, _searchWrap.Width - 1, _searchWrap.Height - 1);
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _searchDebounce.Stop();
                _searchDebounce.Start();
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _searchDebounce.Stop();
                    ApplyFilter(resetPage: true);
                }
            };
            _filterCard.Controls.Add(_searchWrap);

            _searchBtn = new Button
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
            _searchBtn.FlatAppearance.BorderSize = 0;
            _searchBtn.Click += (s, e) => ApplyFilter(resetPage: true);
            _filterCard.Controls.Add(_searchBtn);

            _refreshBtn = new Button
            {
                Text = "Refresh",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Navy,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _refreshBtn.FlatAppearance.BorderColor = CardBorder;
            StyleOutlineButton(_refreshBtn);
            _refreshBtn.Click += async (s, e) => await LoadServicesAsync();
            _filterCard.Controls.Add(_refreshBtn);

            // ---- Grid host + pager ----
            _gridHost = new Panel
            {
                BackColor = Color.White,
                Location = new Point(0, GridTop),
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
                RowTemplate = { Height = RowHeight },
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ServiceId",
                HeaderText = "Service ID",
                Width = 100
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Service Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 200,
                FillWeight = 60
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Price",
                HeaderText = "Price",
                Width = 110
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Duration",
                HeaderText = "Duration",
                Width = 110
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Category",
                HeaderText = "Category",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 120,
                FillWeight = 40
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IsActive",
                HeaderText = "Status",
                Width = 110
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "Actions",
                Width = ActionsColW,
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
                Height = PagerH,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _pager.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, _pager.Width, 0);
            };
            _contentPanel.Controls.Add(_pager);

            // FIX: swap ClientSizeChanged for Resize.
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

            if (_chipBar != null) _chipBar.Visible = activeIsActive;
            if (_addBtn != null) _addBtn.Visible = activeIsActive;
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
            var categories = new[] { "All", "Exterior", "Interior", "Full Service", "Specialty" };
            int x = 0;

            foreach (var cat in categories)
            {
                var chip = new Button
                {
                    Text = cat,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(120, 36),
                    Location = new Point(x, 0),
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    UseVisualStyleBackColor = false
                };

                bool isActive = cat == _activeCategory;
                chip.BackColor = isActive ? Navy : Color.White;
                chip.ForeColor = isActive ? Color.White : Navy;
                chip.FlatAppearance.BorderColor = isActive ? Navy : CardBorder;
                chip.FlatAppearance.BorderSize = 1;

                var captured = cat;
                chip.Click += (s, e) =>
                {
                    _activeCategory = captured;
                    BuildChips();
                    ApplyFilter(resetPage: true);
                };

                _chipBar.Controls.Add(chip);
                x += 130;
            }
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

            _contentPanel.SuspendLayout();
            try
            {
                int w = fullW;

                _addBtn.Location = new Point(w - _addBtn.Width, 30);

                _tabBar.Width = w;
                _chipBar.Width = w;
                _filterCard.Width = w;

                _refreshBtn.Location = new Point(w - 16 - _refreshBtn.Width, 28);
                _searchBtn.Location = new Point(_refreshBtn.Left - 10 - _searchBtn.Width, 28);
                _searchWrap.Width = Math.Max(120, _searchBtn.Left - 12 - _searchWrap.Left);

                _pager.SetBounds(0, h - PageBottom - PagerH, w, PagerH);
                _gridHost.SetBounds(0, GridTop, w,
                    Math.Max(0, (h - PageBottom - PagerH) - GridTop));

                RecomputePageSize();
                RenderCurrentPage();
                RenderPager(CurrentTotalPages());
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
                _contentPanel.PerformLayout();
            }
        }

        // ================================================================
        //  DATA        // ================================================================
        private async Task LoadServicesAsync()
        {
            try
            {
                _addBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var url = _tab == ListTab.Active
                    ? "api/tenant/1/products"
                    : "api/tenant/1/products/archived";

                var list = await _http.GetFromJsonAsync<List<ProductDto>>(url);
                _allServices = list ?? new List<ProductDto>();
                ApplyFilter(resetPage: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load services.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _addBtn.Enabled = true;
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

            _filtered = _allServices.Where(s =>
            {
                if (_tab == ListTab.Active &&
                    _activeCategory != "All" &&
                    !string.Equals(s.Category, _activeCategory, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (string.IsNullOrEmpty(search)) return true;

                return (s.ProductName?.ToLower().Contains(search) ?? false)
                    || (s.Description?.ToLower().Contains(search) ?? false)
                    || (s.Category?.ToLower().Contains(search) ?? false);
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
            if (available < RowHeight) available = RowHeight;
            _pageSize = Math.Max(1, available / RowHeight);
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
                var s = _filtered[i];

                string statusText;
                Color statusColor;
                if (_tab == ListTab.Active)
                {
                    statusText = s.IsActive ? "Active" : "Inactive";
                    statusColor = s.IsActive ? Green : Red;
                }
                else
                {
                    statusText = "Archived";
                    statusColor = Amber;
                }

                int idx = _grid.Rows.Add(
                    s.ProductId,
                    s.ProductName ?? "",
                    $"₱{s.UnitPrice:N0}",
                    $"{s.DurationMinutes} min",
                    string.IsNullOrWhiteSpace(s.Category) ? "—" : s.Category,
                    statusText,
                    "");

                var statusCell = _grid.Rows[idx].Cells["IsActive"];
                statusCell.Style.ForeColor = statusColor;
                statusCell.Style.SelectionForeColor = statusColor;
                statusCell.Style.Font = StatusFont;
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        private void RenderPager(int totalPages)
        {
            _pager.SuspendLayout();
            foreach (Control c in _pager.Controls.OfType<Control>().ToList())
            {
                if (c is Button || c is Label)
                {
                    _pager.Controls.Remove(c);
                    c.Dispose();
                }
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

        // ================================================================
        //  CELL PAINTING
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name == "Actions")
                PaintActionsCell(e);
        }

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            if (_tab == ListTab.Active)
            {
                var (viewRect, editRect, arcRect) = ActiveButtonRects(e.CellBounds);
                PaintOutlineButton(e.Graphics, viewRect, "View",
                    _hoverAction == (e.RowIndex << 2) + 0, Accent);
                PaintOutlineButton(e.Graphics, editRect, "Edit",
                    _hoverAction == (e.RowIndex << 2) + 1, Accent);
                PaintOutlineButton(e.Graphics, arcRect, "Archive",
                    _hoverAction == (e.RowIndex << 2) + 2, Red);
            }
            else
            {
                var restoreRect = RestoreButtonRect(e.CellBounds);
                PaintOutlineButton(e.Graphics, restoreRect, "Restore",
                    _hoverAction == (e.RowIndex << 2) + 2, Green);
            }

            e.Handled = true;
        }

        private static (Rectangle view, Rectangle edit, Rectangle archive) ActiveButtonRects(Rectangle cell)
        {
            int totalW = ActionBtnW * 2 + ArchiveBtnW + ActionBtnGap * 2;
            int x0 = cell.X + (cell.Width - totalW) / 2;
            int y0 = cell.Y + (cell.Height - ActionBtnH) / 2;

            return (
                new Rectangle(x0, y0, ActionBtnW, ActionBtnH),
                new Rectangle(x0 + ActionBtnW + ActionBtnGap, y0, ActionBtnW, ActionBtnH),
                new Rectangle(x0 + ActionBtnW * 2 + ActionBtnGap * 2, y0, ArchiveBtnW, ActionBtnH)
            );
        }

        private static Rectangle RestoreButtonRect(Rectangle cell)
        {
            int w = 110;
            int x0 = cell.X + (cell.Width - w) / 2;
            int y0 = cell.Y + (cell.Height - ActionBtnH) / 2;
            return new Rectangle(x0, y0, w, ActionBtnH);
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

            TextRenderer.DrawText(g, text, ButtonFont, rect, fore,
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

            if (_tab == ListTab.Active)
            {
                var (viewRect, editRect, arcRect) = ActiveButtonRects(cellBounds);
                if (viewRect.Contains(absolute)) return (rowIndex << 2) + 0;
                if (editRect.Contains(absolute)) return (rowIndex << 2) + 1;
                if (arcRect.Contains(absolute)) return (rowIndex << 2) + 2;
            }
            else
            {
                var restoreRect = RestoreButtonRect(cellBounds);
                if (restoreRect.Contains(absolute)) return (rowIndex << 2) + 2;
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

            var idText = _grid.Rows[e.RowIndex].Cells["ServiceId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            var svc = _allServices.FirstOrDefault(x => x.ProductId == id);
            if (svc == null) return;

            if (_tab == ListTab.Active)
            {
                switch (buttonIndex)
                {
                    case 0: OpenViewServiceDialog(svc); break;
                    case 1: OpenEditServiceDialog(id); break;
                    case 2: ArchiveServiceAsync(svc); break;
                }
            }
            else
            {
                if (buttonIndex == 2) RestoreServiceAsync(svc);
            }
        }

        // ================================================================
        //  ACTIONS
        // ================================================================
        private async void OpenAddServiceDialog()
        {
            using var dlg = new ServiceEditDialog(null);
            dlg.ShowDialog(FindForm());
            await LoadServicesAsync();
        }

        private async void OpenEditServiceDialog(int serviceId)
        {
            using var dlg = new ServiceEditDialog(serviceId);
            dlg.ShowDialog(FindForm());
            await LoadServicesAsync();
        }

        private void OpenViewServiceDialog(ProductDto svc)
        {
            MessageBox.Show(
                $"Service ID       : {svc.ProductId}\n" +
                $"Code             : {svc.ProductCode}\n" +
                $"Name             : {svc.ProductName}\n" +
                $"Category         : {(string.IsNullOrWhiteSpace(svc.Category) ? "—" : svc.Category)}\n" +
                $"Price            : ₱{svc.UnitPrice:N0}\n" +
                $"Duration         : {svc.DurationMinutes} min\n" +
                $"Status           : {(svc.IsActive ? "Active" : "Inactive")}\n" +
                $"Created          : {svc.CreatedAt:yyyy-MM-dd}\n\n" +
                $"Description:\n{(string.IsNullOrWhiteSpace(svc.Description) ? "(none)" : svc.Description)}",
                $"Service — {svc.ProductName}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void ArchiveServiceAsync(ProductDto svc)
        {
            if (!ArchiveConfirmDialog.ConfirmArchive("service")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsJsonAsync(
                    $"api/tenant/1/products/{svc.ProductId}/archive",
                    new { archivedBy = CurrentUserName });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Archive failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadServicesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Archive failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private async void RestoreServiceAsync(ProductDto svc)
        {
            if (!ArchiveConfirmDialog.ConfirmRestore("service")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsync(
                    $"api/tenant/1/products/{svc.ProductId}/restore", null);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Restore failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadServicesAsync();
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