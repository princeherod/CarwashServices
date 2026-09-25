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

namespace CarwashServices.Roles.ServiceStaff
{
    /// <summary>
    /// Service Staff → View Assigned Requests.
    ///
    /// Read-only. Every query is scoped to SessionUser.UserId via
    /// AssignedStaffId. Nothing in the UI lets the user edit or change
    /// status — that belongs in the "Update Service Status" module.
    /// </summary>
    public class ServiceStaffAssignedRequestsView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);

        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color AmberSoft = Color.FromArgb(0xFF, 0xF4, 0xDB);
        private static readonly Color Slate = Color.FromArgb(0x39, 0x49, 0xAB);
        private static readonly Color SlateSoft = Color.FromArgb(0xE8, 0xEA, 0xF6);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color NeutralSoft = Color.FromArgb(0xEE, 0xF1, 0xF6);

        // ================================================================
        //  Fonts
        // ================================================================
        private static readonly Font FontLine1 = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new("Segoe UI", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontPill = new("Segoe UI Semibold", 9f);
        private static readonly Font FontMenu = new("Segoe UI", 9.5f);
        private static readonly Font FontKpiLabel = new("Segoe UI Semibold", 9f);
        private static readonly Font FontKpiValue = new("Segoe UI Semibold", 24f);

        // ================================================================
        //  Layout constants
        // ================================================================
        private const int PadX = 32;
        private const int TopMargin = 20;
        private const int RowTemplateHeight = 62;
        private const int ActionBtnH = 28;
        private const int DotsBtnW = 40;
        private const int MenuItemH = 34;
        private const int KpiHeight = 96;

        // Grid column widths — fixed minimums so nothing gets crushed
        private const int ColRequestW = 90;
        private const int ColCustomerMin = 200;
        private const int ColServiceMin = 180;
        private const int ColScheduledW = 160;
        private const int ColPriorityW = 110;
        private const int ColStatusW = 140;
        private const int ColActionsW = 80;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ---- Data ----
        private List<ServiceRequestDto> _myRequests = new();
        private List<TenantCustomerDto> _customers = new();
        private List<ProductDto> _services = new();
        private List<UserDto> _users = new();

        private Dictionary<int, TenantCustomerDto> _custById = new();
        private Dictionary<int, ProductDto> _svcById = new();
        private Dictionary<int, UserDto> _userById = new();

        // ---- UI ----
        private Panel _contentPanel = null!;
        private Panel _kpiRow = null!;
        private Label _kpiAssigned = null!;
        private Label _kpiPending = null!;
        private Label _kpiInProgress = null!;
        private Label _kpiCompleted = null!;

        private TextBox _searchBox = null!;
        private ComboBox _statusFilter = null!;
        private ComboBox _priorityFilter = null!;
        private ComboBox _dateFilter = null!;

        private Panel _gridCard = null!;
        private DataGridView _grid = null!;
        private Label _showingLbl = null!;

        private Panel _pager = null!;
        private int _page = 1;
        private int _pageSize = 8;

        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;

        // ================================================================
        //  Constructor
        // ================================================================
        public ServiceStaffAssignedRequestsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            BuildUi();

            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadAllAsync();

            LostFocus += (s, e) => CloseActiveMenu();
        }

        // ================================================================
        //  UI
        // ================================================================
        private void BuildUi()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(PadX, TopMargin, PadX, TopMargin),
                AutoScroll = false
            };
            Controls.Add(_contentPanel);

            // ---- Header ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  View Assigned Requests",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "View Assigned Requests",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 26),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Read-only view of every service request assigned to you.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(0, 68),
                AutoSize = true
            });

            // ---- KPI row ----
            _kpiRow = new Panel
            {
                Location = new Point(0, 108),
                Height = KpiHeight,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_kpiRow);

            _kpiAssigned = AddKpiTile(_kpiRow, "ASSIGNED REQUESTS", Accent);
            _kpiPending = AddKpiTile(_kpiRow, "PENDING", Amber);
            _kpiInProgress = AddKpiTile(_kpiRow, "IN PROGRESS", Accent);
            _kpiCompleted = AddKpiTile(_kpiRow, "COMPLETED", Green);

            // ---- Filter row ----
            var filterPanel = new Panel
            {
                Location = new Point(0, 220),
                Height = 46,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(filterPanel);

            _searchBox = new TextBox
            {
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Search by request ID, customer, or service...",
                Location = new Point(0, 6),
                Width = 360,
                Height = 34
            };
            filterPanel.Controls.Add(_searchBox);
            _searchBox.TextChanged += (s, e) => { _page = 1; RenderGrid(); };

            _statusFilter = AddFilterCombo(filterPanel, 376, "All statuses",
                "All statuses", "Pending", "Assigned", "InProgress", "Completed", "Cancelled");
            _statusFilter.SelectedIndexChanged += (s, e) => { _page = 1; RenderGrid(); };

            _priorityFilter = AddFilterCombo(filterPanel, 556, "All priorities",
                "All priorities", "Normal", "High", "VIP");
            _priorityFilter.SelectedIndexChanged += (s, e) => { _page = 1; RenderGrid(); };

            _dateFilter = AddFilterCombo(filterPanel, 736, "All dates",
                "All dates", "Today", "Upcoming", "Past");
            _dateFilter.SelectedIndexChanged += (s, e) => { _page = 1; RenderGrid(); };

            // ---- Grid card ----
            _gridCard = new Panel
            {
                Location = new Point(0, 282),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _gridCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _gridCard.Width - 1, _gridCard.Height - 1);
            };
            _contentPanel.Controls.Add(_gridCard);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,

                // Both bars — the sum of fixed minimums can exceed the width.
                ScrollBars = ScrollBars.Both,

                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderBg,
                    ForeColor = Muted,
                    SelectionBackColor = HeaderBg,
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 12, 0),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersHeight = 46,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = RowTemplateHeight },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = FontCell,
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(16, 0, 12, 0),
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

            // ---- Columns ----
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestId", Visible = false });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Request",
                HeaderText = "REQUEST",
                Width = ColRequestW,
                MinimumWidth = ColRequestW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "CUSTOMER",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = ColCustomerMin,
                FillWeight = 100
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "SERVICE",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = ColServiceMin,
                FillWeight = 90
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Scheduled",
                HeaderText = "SCHEDULED DATE",
                Width = ColScheduledW,
                MinimumWidth = ColScheduledW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Priority",
                HeaderText = "PRIORITY",
                Width = ColPriorityW,
                MinimumWidth = ColPriorityW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "STATUS",
                Width = ColStatusW,
                MinimumWidth = ColStatusW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "ACTIONS",
                Width = ColActionsW,
                MinimumWidth = ColActionsW,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(0)
                }
            });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellMouseLeave += (s, e) => SetHover(-1);
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.Scroll += (s, e) => CloseActiveMenu();
            _grid.Resize += (s, e) => CloseActiveMenu();
            _grid.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) CloseActiveMenu(); };

            _gridCard.Controls.Add(_grid);

            // ---- Pager ----
            _pager = new Panel
            {
                BackColor = Color.White,
                Height = 48,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _pager.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, _pager.Width, 0);
            };
            _contentPanel.Controls.Add(_pager);

            _showingLbl = new Label
            {
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                Location = new Point(16, 15)
            };
            _pager.Controls.Add(_showingLbl);

            _contentPanel.Resize += (s, e) => LayoutAll();
            LayoutAll();
        }

        private ComboBox AddFilterCombo(Panel parent, int left, string firstItem, params string[] items)
        {
            var cb = new ComboBox
            {
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                Location = new Point(left, 6),
                Width = 170,
                Height = 34
            };
            cb.Items.AddRange(items);
            cb.SelectedIndex = 0;
            parent.Controls.Add(cb);
            return cb;
        }

        private Label AddKpiTile(Panel parent, string title, Color accent)
        {
            var card = new Panel { BackColor = Color.White };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (var fill = new SolidBrush(Color.White))
                using (var pen = new Pen(CardBorder))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }
                using (var stripe = new SolidBrush(accent))
                    e.Graphics.FillRectangle(stripe, 0, 0, 4, card.Height);
            };
            parent.Controls.Add(card);

            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Muted,
                Font = FontKpiLabel,
                Location = new Point(20, 14),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var val = new Label
            {
                Text = "0",
                ForeColor = Navy,
                Font = FontKpiValue,
                Location = new Point(18, 38),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(val);

            return val;
        }

        private void LayoutAll()
        {
            if (_contentPanel == null) return;

            int w = _contentPanel.ClientSize.Width - _contentPanel.Padding.Horizontal;
            int h = _contentPanel.ClientSize.Height - _contentPanel.Padding.Vertical;
            if (w < 200) return;

            _kpiRow.Width = w;
            LayoutKpis();

            // Filter row
            int filterRight = _contentPanel.ClientSize.Width - _contentPanel.Padding.Right;
            _dateFilter.Left = Math.Max(736, filterRight - 170);
            _priorityFilter.Left = _dateFilter.Left - 180;
            _statusFilter.Left = _priorityFilter.Left - 180;

            int pagerH = 48;
            _pager.SetBounds(0, h - pagerH, w, pagerH);
            _gridCard.SetBounds(0, 282, w, Math.Max(0, h - 282 - pagerH));

            RecomputePageSize();
        }

        private void LayoutKpis()
        {
            var tiles = _kpiRow.Controls.OfType<Panel>().ToList();
            if (tiles.Count == 0) return;

            int gap = 16;
            int total = _kpiRow.ClientSize.Width;
            int tileW = (total - gap * (tiles.Count - 1)) / tiles.Count;

            for (int i = 0; i < tiles.Count; i++)
                tiles[i].SetBounds(i * (tileW + gap), 0, tileW, KpiHeight);
        }

        private void RecomputePageSize()
        {
            int headerH = _grid.ColumnHeadersHeight;
            int available = _gridCard.ClientSize.Height - headerH;
            if (available < RowTemplateHeight) available = RowTemplateHeight;
            _pageSize = Math.Max(1, available / RowTemplateHeight);
        }

        // ================================================================
        //  Load
        // ================================================================
        private async Task LoadAllAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                int staffId = SessionUser.UserId;
                if (staffId <= 0)
                {
                    MessageBox.Show("No signed-in user.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Load everything the view needs in parallel.
                var reqsT = _http.GetFromJsonAsync<List<ServiceRequestDto>>("api/service-requests");
                var custsT = _http.GetFromJsonAsync<List<TenantCustomerDto>>("api/tenant/1/tenant-customers");
                var svcsT = _http.GetFromJsonAsync<List<ProductDto>>("api/tenant/1/products");
                var usersT = _http.GetFromJsonAsync<List<UserDto>>("api/users");

                await Task.WhenAll(reqsT, custsT, svcsT, usersT);

                var allRequests = reqsT.Result ?? new();
                _customers = custsT.Result ?? new();
                _services = svcsT.Result ?? new();
                _users = usersT.Result ?? new();

                _custById = _customers.ToDictionary(c => c.TenantCustomerId);
                _svcById = _services.ToDictionary(s => s.ProductId);
                _userById = _users.ToDictionary(u => u.UserId);

                // Scope: only this Service Staff's assigned requests.
                _myRequests = allRequests
                    .Where(r => !r.IsArchived && r.AssignedStaffId == staffId)
                    .OrderByDescending(r => r.RequestId)
                    .ToList();

                UpdateKpis();
                RenderGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load your assigned requests.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void UpdateKpis()
        {
            _kpiAssigned.Text = _myRequests.Count.ToString();
            _kpiPending.Text = _myRequests.Count(r =>
                string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase)).ToString();
            _kpiInProgress.Text = _myRequests.Count(r =>
                string.Equals(r.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Status, "Assigned", StringComparison.OrdinalIgnoreCase)).ToString();
            _kpiCompleted.Text = _myRequests.Count(r =>
                string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase)).ToString();
        }

        // ================================================================
        //  Filtering + render
        // ================================================================
        private List<ServiceRequestDto> FilteredList()
        {
            var search = _searchBox.Text?.Trim().ToLower() ?? "";
            var status = _statusFilter.SelectedItem?.ToString() ?? "All statuses";
            var priority = _priorityFilter.SelectedItem?.ToString() ?? "All priorities";
            var date = _dateFilter.SelectedItem?.ToString() ?? "All dates";

            var today = DateTime.Today;

            return _myRequests.Where(r =>
            {
                // ---- Search ----
                if (!string.IsNullOrEmpty(search))
                {
                    var cust = _custById.TryGetValue(r.CustomerId, out var c) ? c : null;
                    var svc = _svcById.TryGetValue(r.ServiceId, out var s) ? s : null;

                    var blob = $"#{r.RequestId} {r.RequestId} " +
                               $"{cust?.CustomerName} " +
                               $"{svc?.ProductName}".ToLower();
                    if (!blob.Contains(search)) return false;
                }

                // ---- Status ----
                if (status != "All statuses")
                {
                    if (!string.Equals(r.Status ?? "", status, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // ---- Priority ----
                if (priority != "All priorities")
                {
                    var p = string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority;
                    if (!string.Equals(p, priority, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // ---- Date ----
                if (date != "All dates")
                {
                    var when = r.ScheduledDate ?? r.RequestedDate;
                    switch (date)
                    {
                        case "Today":
                            if (when.Date != today) return false;
                            break;
                        case "Upcoming":
                            if (when.Date <= today) return false;
                            break;
                        case "Past":
                            if (when.Date >= today) return false;
                            break;
                    }
                }

                return true;
            })
            .OrderByDescending(r => r.RequestId)
            .ToList();
        }

        private void RenderGrid()
        {
            RecomputePageSize();

            var list = FilteredList();
            int totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)_pageSize));
            if (_page > totalPages) _page = totalPages;
            if (_page < 1) _page = 1;

            var start = (_page - 1) * _pageSize;
            var pageItems = list.Skip(start).Take(_pageSize).ToList();

            _hoverAction = -1;
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var r in pageItems)
            {
                var cust = _custById.TryGetValue(r.CustomerId, out var c) ? c : null;
                var svc = _svcById.TryGetValue(r.ServiceId, out var s) ? s : null;

                var custCell = cust?.CustomerName ?? $"id:{r.CustomerId}";
                if (!string.IsNullOrWhiteSpace(cust?.PlateNumber))
                    custCell += "\n" + cust.PlateNumber;

                var svcCell = svc?.ProductName ?? $"Service {r.ServiceId}";
                if (svc != null && svc.UnitPrice > 0)
                    svcCell += $"\n₱{svc.UnitPrice:N0}";

                var scheduled = r.ScheduledDate?.ToString("MMM d, yyyy  h:mm tt") ?? "—";

                int idx = _grid.Rows.Add(
                    r.RequestId,
                    $"#{r.RequestId}",
                    custCell,
                    svcCell,
                    scheduled,
                    string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority,
                    r.Status ?? "Pending",
                    "");

                _grid.Rows[idx].Tag = r.RequestId;
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
            _grid.PerformLayout();

            var from = list.Count == 0 ? 0 : start + 1;
            var to = Math.Min(start + _pageSize, list.Count);
            _showingLbl.Text = $"Showing {from}–{to} of {list.Count} assigned requests";

            RenderPager(totalPages);
        }

        private void RenderPager(int totalPages)
        {
            foreach (var c in _pager.Controls.OfType<Button>().ToList())
            {
                _pager.Controls.Remove(c);
                c.Dispose();
            }

            const int bw = 36, bh = 34, gap = 6;
            int right = _pager.ClientSize.Width - 16;

            var next = MakePagerBtn("›", _page < totalPages);
            next.Location = new Point(right - bw, 7);
            next.Click += (s, e) => { if (_page < totalPages) { _page++; RenderGrid(); } };
            _pager.Controls.Add(next);
            right -= bw + gap;

            int start = Math.Max(1, _page - 3);
            for (int p = Math.Min(totalPages, start + 4); p >= start; p--)
            {
                var b = MakePagerBtn(p.ToString(), true);
                b.BackColor = p == _page ? Navy : Color.White;
                b.ForeColor = p == _page ? Color.White : Navy;
                b.FlatAppearance.BorderColor = p == _page ? Navy : CardBorder;
                int target = p;
                b.Click += (s, e) => { _page = target; RenderGrid(); };
                b.Location = new Point(right - bw, 7);
                _pager.Controls.Add(b);
                right -= bw + gap;
            }

            var prev = MakePagerBtn("‹", _page > 1);
            prev.Location = new Point(right - bw, 7);
            prev.Click += (s, e) => { if (_page > 1) { _page--; RenderGrid(); } };
            _pager.Controls.Add(prev);
        }

        private static Button MakePagerBtn(string text, bool enabled)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(36, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                Enabled = enabled,
                Cursor = enabled ? Cursors.Hand : Cursors.Default,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = CardBorder;
            return b;
        }

        // ================================================================
        //  Cell painting
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            if (col == "Customer" || col == "Service")
            {
                PaintTwoLine(e);
                return;
            }

            if (col == "Status")
            {
                PaintStatusPill(e);
                return;
            }

            if (col == "Priority")
            {
                PaintPriorityPill(e);
                return;
            }

            if (col == "Actions")
            {
                PaintActionsCell(e);
                return;
            }
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e)
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
            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

            if (l2 == null)
            {
                TextRenderer.DrawText(e.Graphics, l1, FontLine1,
                    new Rectangle(x, b.Y, w, b.Height), Navy, flags);
            }
            else
            {
                int h1 = FontLine1.Height, h2 = FontLine2.Height, gap = 2;
                int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;
                TextRenderer.DrawText(e.Graphics, l1, FontLine1,
                    new Rectangle(x, top, w, h1), Navy, flags);
                TextRenderer.DrawText(e.Graphics, l2, FontLine2,
                    new Rectangle(x, top + h1 + gap, w, h2), Muted, flags);
            }
            e.Handled = true;
        }

        private static void PaintStatusPill(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (_, fg) = StatusColors(text);
            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontPill,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 16;
            int maxW = Math.Max(10, b.Width - 24);
            int y = b.Y + (b.Height - size.Height) / 2;

            TextRenderer.DrawText(e.Graphics, text, FontPill,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private static void PaintPriorityPill(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (_, fg) = PriorityColors(text);
            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontPill,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 16;
            int maxW = Math.Max(10, b.Width - 24);
            int y = b.Y + (b.Height - size.Height) / 2;

            TextRenderer.DrawText(e.Graphics, text, FontPill,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Completed" => (GreenSoft, Green),
            "Pending" => (AmberSoft, Amber),
            "InProgress" or "In Progress" or "Assigned" => (AccentSoft, Accent),
            "Cancelled" => (RedSoft, Red),
            _ => (NeutralSoft, Muted)
        };

        private static (Color bg, Color fg) PriorityColors(string s) => s switch
        {
            "High" => (RedSoft, Red),
            "VIP" => (SlateSoft, Slate),
            _ => (NeutralSoft, Muted)
        };

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

            Color fill = hover ? AccentSoft : Color.White;
            Color border = hover ? Accent : CardBorder;
            Color dot = hover ? Accent : Navy;

            using (var path = RoundedRect(rect, 6))
            using (var b = new SolidBrush(fill))
            using (var p = new Pen(border, 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }

            const int dotSize = 3;
            int cx = rect.X + rect.Width / 2 - dotSize / 2;
            int cy = rect.Y + rect.Height / 2;
            using var db = new SolidBrush(dot);
            g.FillEllipse(db, cx, cy - 8, dotSize, dotSize);
            g.FillEllipse(db, cx, cy - dotSize / 2, dotSize, dotSize);
            g.FillEllipse(db, cx, cy + 5, dotSize, dotSize);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return p;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ================================================================
        //  Hover + click
        // ================================================================
        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _grid.Columns[e.ColumnIndex].Name != "Actions")
            {
                SetHover(-1);
                return;
            }
            SetHover(HitTest(e.RowIndex, e.Location));
        }

        private int HitTest(int rowIndex, Point local)
        {
            var cb = _grid.GetCellDisplayRectangle(_grid.Columns["Actions"].Index, rowIndex, false);
            var abs = new Point(cb.X + local.X, cb.Y + local.Y);
            if (DotsRect(cb).Contains(abs)) return rowIndex << 2;
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

            if (HitTest(e.RowIndex, e.Location) < 0) return;

            var idText = _grid.Rows[e.RowIndex].Cells["RequestId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            var dto = _myRequests.FirstOrDefault(r => r.RequestId == id);
            if (dto == null) return;

            ShowActionsMenu(e.RowIndex, dto);
        }

        // ================================================================
        //  Menu — only View Details for now
        // ================================================================
        private void ShowActionsMenu(int rowIndex, ServiceRequestDto r)
        {
            CloseActiveMenu();

            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Font = FontMenu,
                BackColor = Color.White,
                ForeColor = Navy,
                Padding = new Padding(4),
                Renderer = new ToolStripProfessionalRenderer(new MenuColors())
            };

            AddMenuItem(menu, "View Details", () => ShowViewDetails(r));

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

            var cb = _grid.GetCellDisplayRectangle(_grid.Columns["Actions"].Index, rowIndex, false);
            menu.Show(_grid, new Point(cb.Right - 8, cb.Bottom - 4), ToolStripDropDownDirection.BelowLeft);
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
            public override Color MenuItemSelected => AccentSoft;
            public override Color MenuItemSelectedGradientBegin => AccentSoft;
            public override Color MenuItemSelectedGradientEnd => AccentSoft;
            public override Color MenuItemBorder => CardBorder;
            public override Color MenuBorder => CardBorder;
        }

        // ================================================================
        //  View Details
        // ================================================================
        private void ShowViewDetails(ServiceRequestDto r)
        {
            var cust = _custById.TryGetValue(r.CustomerId, out var c) ? c : null;
            var svc = _svcById.TryGetValue(r.ServiceId, out var s) ? s : null;

            string staff = "—";
            if (r.AssignedStaffId.HasValue &&
                _userById.TryGetValue(r.AssignedStaffId.Value, out var u))
            {
                staff = u.FullName;
            }

            string createdBy = "—";
            if (_userById.TryGetValue(r.CreatedBy, out var creator))
                createdBy = creator.FullName;

            using var dlg = new ServiceRequestViewDialog(
                requestId: r.RequestId,
                requestedDate: r.RequestedDate,
                scheduledDate: r.ScheduledDate,
                completedDate: r.CompletedDate,
                priority: r.Priority ?? "Normal",
                status: r.Status ?? "Pending",
                notes: r.Notes ?? "",
                customerName: cust?.CustomerName ?? $"id:{r.CustomerId}",
                customerPhone: cust?.ContactNumber ?? "—",
                customerEmail: cust?.EmailAddress ?? "—",
                customerAddress: cust?.Address ?? "—",
                serviceName: svc?.ProductName ?? $"Service {r.ServiceId}",
                serviceDescription: svc?.Description ?? "",
                servicePrice: svc?.UnitPrice ?? 0m,
                serviceDuration: svc?.DurationMinutes ?? 0,
                assignedStaff: staff,
                createdBy: createdBy,
                createdAt: r.RequestedDate);

            dlg.ShowDialog(FindForm());
        }
    }
}