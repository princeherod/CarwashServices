using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles
{
    /// <summary>
    /// Manager module: queue of PENDING service requests that have no
    /// AssignedStaffId. Rows whose status is Completed or Cancelled are
    /// excluded — they never need assignment.
    ///
    /// The picker and Assign button are always visible on the active row.
    /// </summary>
    public class AssignServiceStaffView : UserControl
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
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color NeutralSoft = Color.FromArgb(0xEE, 0xF1, 0xF6);

        private static readonly Font FontLine1 = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new("Segoe UI", 8.5f);
        private static readonly Font FontTag = new("Segoe UI Semibold", 9.5f);

        // ---- Layout ----
        private const int PadX = 40;
        private const int FilterTop = 200;
        private const int FilterH = 76;
        private const int GridTop = 296;
        private const int PageBottom = 24;
        private const int RowHeight = 62;

        private const int AssignComboW = 190;
        private const int AssignBtnW = 78;
        private const int AssignBtnH = 30;
        private const int AssignGap = 8;

        // ---- Data ----
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<ServiceRequestDto> _unassigned = new();
        private List<TenantCustomerDto> _customers = new();
        private List<ProductDto> _services = new();
        private List<UserDto> _staff = new();

        private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };

        // ---- UI ----
        private Panel _contentPanel = null!;
        private Panel _filterCard = null!;
        private TextBox _searchBox = null!;
        private Button _refreshBtn = null!;
        private Panel _gridHost = null!;
        private DataGridView _grid = null!;
        private Label _statusLbl = null!;

        private ComboBox _staffCombo = null!;
        private Button _assignBtn = null!;
        private int _activeRow = -1;
        private int _hoverAssign = -1;

        public AssignServiceStaffView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _debounce.Tick += (s, e) => { _debounce.Stop(); ApplyFilter(); };

            InitializeUI();

            Sidebar.EnableDoubleBuffering(this);

            CarwashServices.Auth.SessionUser.BranchChanged += () =>
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

            Load += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
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
                Text = "Modules  ›  Assign Service Staff",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 12),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Assign Service Staff",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(PadX, 34),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Pending service requests waiting for a Service Staff. Pick a staff member to assign the job.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 80),
                AutoSize = true
            });

            _statusLbl = new Label
            {
                Text = "",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 10f),
                Location = new Point(PadX, 112),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_statusLbl);

            // ---- Filter card ----
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
                PlaceholderText = "Search by customer, plate, service or priority",
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
            _filterCard.Controls.Add(searchWrap);

            _refreshBtn = new Button
            {
                Text = "Refresh",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Navy,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                UseVisualStyleBackColor = false
            };
            _refreshBtn.FlatAppearance.BorderColor = CardBorder;
            _refreshBtn.Click += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
            _filterCard.Controls.Add(_refreshBtn);

            _filterCard.Resize += (s, e) =>
            {
                _refreshBtn.Location = new Point(_filterCard.Width - 16 - _refreshBtn.Width, 28);
                searchWrap.Width = Math.Max(120, _refreshBtn.Left - 12 - searchWrap.Left);
            };

            // ---- Grid host ----
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
                ScrollBars = ScrollBars.Vertical,
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestId", HeaderText = "Request", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "Customer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 200,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "Service",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180,
                FillWeight = 90
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "Scheduled", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "Priority", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Assign",
                HeaderText = "Assign to",
                Width = 280,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    Padding = new Padding(0)
                }
            });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellMouseEnter += Grid_CellMouseEnter;

            _staffCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Visible = true,
                Width = AssignComboW,
                Height = 28
            };
            _staffCombo.SelectedIndexChanged += (s, e) => UpdateAssignButtonState();
            _grid.Controls.Add(_staffCombo);

            _assignBtn = new Button
            {
                Text = "Assign",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Color.White,
                BackColor = Blue,
                Size = new Size(AssignBtnW, AssignBtnH),
                Cursor = Cursors.Hand,
                Visible = true,
                UseVisualStyleBackColor = false
            };
            _assignBtn.FlatAppearance.BorderSize = 0;
            _assignBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x19, 0x76, 0xD2);
            _assignBtn.Click += async (s, e) => await AssignAsync();
            _grid.Controls.Add(_assignBtn);

            _gridHost.Controls.Add(_grid);

            _contentPanel.Resize += (s, e) => LayoutContent();
            LayoutContent();
        }

        private void LayoutContent()
        {
            if (_contentPanel == null || _gridHost == null || _filterCard == null) return;
            int fullW = _contentPanel.ClientSize.Width;
            int fullH = _contentPanel.ClientSize.Height;
            if (fullW < 300) return;

            int contentW = fullW - 2 * PadX;

            _filterCard.SetBounds(PadX, FilterTop, contentW, FilterH);
            _gridHost.SetBounds(PadX, GridTop, contentW,
                Math.Max(0, fullH - GridTop - PageBottom));
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
                _customers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    $"api/tenant/{companyId}/tenant-customers{branchQuery}") ?? new();
            }
            catch { _customers = new(); }

            try
            {
                _services = await _http.GetFromJsonAsync<List<ProductDto>>(
                    $"api/tenant/{companyId}/products{branchQuery}") ?? new();
            }
            catch { _services = new(); }

            try
            {
                var all = await _http.GetFromJsonAsync<List<UserDto>>($"api/users?companyId={companyId}{userBranchQuery}") ?? new();
                _staff = all.Where(u => u.RoleId == 4).ToList();
            }
            catch { _staff = new(); }

            _staffCombo.Items.Clear();
            foreach (var s in _staff.OrderBy(u => u.FullName))
                _staffCombo.Items.Add(new ComboItem(s.UserId, s.FullName));
        }

        /// <summary>
        /// A request belongs in the assign queue ONLY when:
        ///   • it is not archived
        ///   • it has no AssignedStaffId
        ///   • its status is Pending (or Assigned, defensively, if the API
        ///     ever leaves an "Assigned" row without a staff id)
        /// Completed, Cancelled, InProgress, and everything else are excluded.
        /// </summary>
        private static bool IsAwaitingAssignment(ServiceRequestDto r)
        {
            if (r.IsArchived) return false;
            if (r.AssignedStaffId.HasValue) return false;

            var status = (r.Status ?? "").Trim();
            return status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Assigned", StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var branchId = CarwashServices.Auth.SessionUser.CurrentBranchId;
                var branchQuery = branchId.HasValue && branchId.Value > 0 ? $"&branchId={branchId.Value}" : "";

                var all = await _http.GetFromJsonAsync<List<ServiceRequestDto>>(
                    $"api/service-requests?companyId={companyId}{branchQuery}") ?? new();

                _unassigned = all
                    .Where(IsAwaitingAssignment)
                    .OrderBy(r => r.ScheduledDate ?? r.RequestedDate)
                    .ToList();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load service requests.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            var term = _searchBox.Text?.Trim().ToLower() ?? "";

            var list = _unassigned;

            // Defensive re-check — keeps the queue correct even if the
            // source list somehow contains a stale row.
            list = list.Where(IsAwaitingAssignment).ToList();

            if (!string.IsNullOrEmpty(term))
            {
                list = list.Where(r =>
                {
                    var cust = _customers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                    var svc = _services.FirstOrDefault(s => s.ProductId == r.ServiceId);
                    var blob = $"{cust?.CustomerName} {cust?.PlateNumber} {svc?.ProductName} {r.Priority}".ToLower();
                    return blob.Contains(term);
                }).ToList();
            }

            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var r in list)
            {
                var cust = _customers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                var svc = _services.FirstOrDefault(s => s.ProductId == r.ServiceId);

                string custCell = cust?.CustomerName ?? $"id:{r.CustomerId}";
                if (!string.IsNullOrWhiteSpace(cust?.PlateNumber))
                    custCell += "\n" + cust.PlateNumber;

                string svcCell = svc?.ProductName ?? $"id:{r.ServiceId}";
                svcCell += $"\n₱{svc?.UnitPrice:N0}";

                _grid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    svcCell,
                    r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "—",
                    string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority,
                    r.Status,
                    "");
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();

            _statusLbl.Text = list.Count == 0
                ? "No pending requests waiting for assignment."
                : $"{list.Count} pending request{(list.Count == 1 ? "" : "s")} waiting for assignment.";

            if (_grid.Rows.Count > 0)
            {
                ShowAssignControls(0);
            }
            else
            {
                _activeRow = -1;
                _staffCombo.Visible = false;
                _assignBtn.Visible = false;
            }
        }

        // ================================================================
        //  CELL PAINTING
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;

            if (e.RowIndex < 0)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                    e.CellBounds.Right, e.CellBounds.Bottom - 1);
                e.Handled = true;
                return;
            }

            var col = _grid.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "Customer":
                case "Service":
                    PaintTwoLine(e);
                    break;

                case "Priority":
                    {
                        var text = Convert.ToString(e.Value) ?? "Normal";
                        var (bg, fg) = PriorityColors(text);
                        PaintColoredText(e, text, fg, bg);
                        break;
                    }

                case "Status":
                    {
                        var text = Convert.ToString(e.Value) ?? "";
                        var (bg, fg) = StatusColors(text);
                        PaintColoredText(e, text, fg, bg);
                        break;
                    }

                case "Assign":
                    e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                          DataGridViewPaintParts.Border |
                                          DataGridViewPaintParts.SelectionBackground);
                    e.Handled = true;
                    break;
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
            int y = b.Y + (b.Height - size.Height) / 2;

            TextRenderer.DrawText(e.Graphics, text, FontTag,
                new Rectangle(x, y, Math.Max(10, b.Width - 24), size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
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
            "VIP" => (Color.FromArgb(0xE8, 0xEA, 0xF6), Color.FromArgb(0x39, 0x49, 0xAB)),
            _ => (NeutralSoft, Muted)
        };

        // ================================================================
        //  HOVER / CLICK
        // ================================================================
        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _grid.Columns[e.ColumnIndex].Name != "Assign")
            {
                return;
            }
            SetHover(e.RowIndex);
        }

        private void Grid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            ShowAssignControls(e.RowIndex);
        }

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Assign") return;
            ShowAssignControls(e.RowIndex);
        }

        private void SetHover(int row)
        {
            if (_hoverAssign == row) return;
            int old = _hoverAssign;
            _hoverAssign = row;
            var col = _grid.Columns["Assign"];
            if (col != null)
            {
                if (old >= 0) _grid.InvalidateCell(col.Index, old);
                if (row >= 0) _grid.InvalidateCell(col.Index, row);
            }
        }

        private void ShowAssignControls(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;
            _activeRow = rowIndex;

            var cellRect = _grid.GetCellDisplayRectangle(
                _grid.Columns["Assign"].Index, rowIndex, false);
            if (cellRect.Width <= 0 || cellRect.Height <= 0) return;

            int comboX = cellRect.X + 12;
            int comboY = cellRect.Y + (cellRect.Height - 28) / 2;
            int btnX = comboX + AssignComboW + AssignGap;
            int btnY = cellRect.Y + (cellRect.Height - AssignBtnH) / 2;

            _staffCombo.SetBounds(comboX, comboY, AssignComboW, 28);
            _assignBtn.SetBounds(btnX, btnY, AssignBtnW, AssignBtnH);

            var idText = _grid.Rows[rowIndex].Cells["RequestId"].Value?.ToString() ?? "";
            int.TryParse(idText.TrimStart('#'), out var reqId);
            var req = _unassigned.FirstOrDefault(x => x.RequestId == reqId);

            var eligible = _staff.Where(s =>
                !s.BranchId.HasValue ||
                req == null ||
                !req.BranchId.HasValue ||
                s.BranchId.Value == req.BranchId.Value
            ).OrderBy(s => s.FullName).ToList();

            var previousSelectedId = (_staffCombo.SelectedItem as ComboItem)?.Id;

            _staffCombo.Items.Clear();
            int selectedIdx = -1;
            for (int i = 0; i < eligible.Count; i++)
            {
                var s = eligible[i];
                string branchTag = !string.IsNullOrWhiteSpace(s.BranchName) ? $" ({s.BranchName})" : "";
                _staffCombo.Items.Add(new ComboItem(s.UserId, $"{s.FullName}{branchTag}"));
                if (previousSelectedId.HasValue && s.UserId == previousSelectedId.Value)
                {
                    selectedIdx = i;
                }
            }

            if (eligible.Count > 0)
            {
                _staffCombo.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
            }
            else
            {
                _staffCombo.Items.Add(new ComboItem(null, "No staff for this branch"));
                _staffCombo.SelectedIndex = 0;
            }

            _staffCombo.Visible = true;
            _assignBtn.Visible = true;
            _staffCombo.BringToFront();
            _assignBtn.BringToFront();

            UpdateAssignButtonState();
        }

        private void UpdateAssignButtonState()
        {
            bool hasStaff = _staffCombo.SelectedItem is ComboItem ci && ci.Id.HasValue;
            _assignBtn.Enabled = hasStaff && _activeRow >= 0;
        }

        // ================================================================
        //  ASSIGN ACTION
        // ================================================================
        private async Task AssignAsync()
        {
            if (_activeRow < 0 || _activeRow >= _grid.Rows.Count) return;
            if (_staffCombo.SelectedItem is not ComboItem picked || picked.Id == null) return;

            var idText = _grid.Rows[_activeRow].Cells["RequestId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText.TrimStart('#'), out var requestId)) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                _assignBtn.Enabled = false;
                _assignBtn.Text = "Saving…";

                var resp = await _http.PutAsJsonAsync(
                    $"api/service-requests/{requestId}/assign?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}",
                    new {
                        assignedStaffId = picked.Id.Value,
                        updatedBy = CarwashServices.Auth.SessionUser.UserId
                    });

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Assign failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadRequestsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Assign failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _assignBtn.Text = "Assign";
                UpdateAssignButtonState();
                Cursor = Cursors.Default;
            }
        }
    }
}