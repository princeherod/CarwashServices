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
    /// Service Staff → Follow-Ups / Reminders.
    ///
    /// Scoped strictly to the signed-in staff member:
    ///   - Only rows they created are shown.
    ///   - Only customers with an assigned request appear in the picker.
    ///   - Every submission lands in "Pending Approval".
    ///   - No Send button, no Approve button.
    /// </summary>
    public class ServiceStaffFollowUpsView : UserControl
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color AmberSoft = Color.FromArgb(0xFF, 0xF4, 0xDB);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color Slate = Color.FromArgb(0x39, 0x49, 0xAB);
        private static readonly Color SlateSoft = Color.FromArgb(0xE8, 0xEA, 0xF6);
        private static readonly Color NeutralSoft = Color.FromArgb(0xEE, 0xF1, 0xF6);

        private static readonly Font FontLine1 = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new("Segoe UI", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontPill = new("Segoe UI Semibold", 9f);
        private static readonly Font FontMenu = new("Segoe UI", 9.5f);

        private const int PadX = 32;
        private const int RowTemplateHeight = 62;
        private const int ActionBtnH = 28;
        private const int DotsBtnW = 40;
        private const int MenuItemH = 34;

        // ---- Header layout (fixes title/subtitle overlap) ----
        private const int BreadcrumbY = 0;
        private const int TitleY = 22;
        private const int SubtitleY = 76;
        private const int ToolbarY = 118;
        private const int GridTop = 166;

        // Column order (left → right): Customer, Status, Scheduled, Send Via,
        // Discount Offer, Reason, Actions. Identity + status first, the
        // internal-only "Reason" note last since it's the least glanced-at.
        private const int ColCustomerMin = 220;
        private const int ColStatusW = 170;
        private const int ColScheduledW = 160;
        private const int ColSendViaW = 110;
        private const int ColDiscountW = 190;   // was 160 — was clipping "DISCOUNT OFFER"
        private const int ColReasonMin = 200;
        private const int ColActionsW = 96;     // was 80 — was clipping "ACTIONS"

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private List<FollowUpDto> _all = new();
        private List<TenantCustomerDto> _myCustomers = new();
        private Dictionary<int, TenantCustomerDto> _custById = new();

        private Panel _contentPanel = null!;
        private DataGridView _grid = null!;
        private TextBox _searchBox = null!;
        private Button _searchBtn = null!;
        private ComboBox _statusFilter = null!;
        private Button _addBtn = null!;
        private Label _showingLbl = null!;
        private Panel _gridCard = null!;
        private Panel _pager = null!;

        private int _page = 1;
        private int _pageSize = 8;

        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;

        private string CurrentUserName =>
            string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Staff" : SessionUser.FullName;

        public ServiceStaffFollowUpsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            BuildUi();

            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) =>
            {
                await LoadMyCustomersAsync();
                await LoadAsync();
            };

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
                Padding = new Padding(PadX, 20, PadX, 20)
            };
            Controls.Add(_contentPanel);

            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Follow-Ups / Reminders",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, BreadcrumbY),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Follow-Ups / Reminders",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, TitleY),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Create follow-up requests for your assigned customers. Every submission needs Admin approval before it can be sent.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(0, SubtitleY),
                AutoSize = true
            });

            _addBtn = new Button
            {
                Text = "+  New Follow-Up",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(200, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _addBtn.FlatAppearance.BorderSize = 0;
            _addBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _addBtn.Click += (s, e) => OpenAddDialog();
            _contentPanel.Controls.Add(_addBtn);

            // ---- Search box + explicit search button ----
            _searchBox = new TextBox
            {
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Search by customer...",
                Location = new Point(0, ToolbarY),
                Width = 320
            };
            _contentPanel.Controls.Add(_searchBox);
            _searchBox.TextChanged += (s, e) => { _page = 1; RenderGrid(); };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _page = 1;
                    RenderGrid();
                }
            };

            _searchBtn = new Button
            {
                Size = new Size(38, 27),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop = false,
                Text = ""
            };
            _searchBtn.FlatAppearance.BorderColor = CardBorder;
            _searchBtn.FlatAppearance.MouseOverBackColor = BlueSoft;
            _searchBtn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool hover = _searchBtn.ClientRectangle.Contains(_searchBtn.PointToClient(Cursor.Position));
                int d = 13;
                var iconRect = new Rectangle(
                    (_searchBtn.Width - d) / 2 - 2,
                    (_searchBtn.Height - d) / 2 - 2,
                    d, d);
                DrawSearchIcon(e.Graphics, iconRect, hover ? Blue : Navy);
            };
            _searchBtn.Click += (s, e) => { _page = 1; RenderGrid(); };
            _searchBtn.MouseEnter += (s, e) => _searchBtn.Invalidate();
            _searchBtn.MouseLeave += (s, e) => _searchBtn.Invalidate();
            _contentPanel.Controls.Add(_searchBtn);

            _statusFilter = new ComboBox
            {
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                Location = new Point(336, ToolbarY),
                Width = 200
            };
            _statusFilter.Items.AddRange(new object[]
            {
                "All statuses",
                "Draft",
                "Pending Approval",
                "Approved",
                "Scheduled",
                "Sent",
                "Rejected",
                "Expired"
            });
            _statusFilter.SelectedIndex = 0;
            _statusFilter.SelectedIndexChanged += (s, e) => { _page = 1; RenderGrid(); };
            _contentPanel.Controls.Add(_statusFilter);

            _gridCard = new Panel
            {
                Location = new Point(0, GridTop),
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

                // Horizontal scrollbar appears when the sum of column widths
                // exceeds the available width. Vertical scrollbar stays for rows.
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

            // ---- Columns, rearranged: Customer, Status, Scheduled, Send Via,
            //      Discount Offer, Reason, Actions ----
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FollowUpId", Visible = false });

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
                Name = "Status",
                HeaderText = "STATUS",
                Width = ColStatusW,
                MinimumWidth = ColStatusW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Scheduled",
                HeaderText = "SCHEDULED",
                Width = ColScheduledW,
                MinimumWidth = ColScheduledW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SendVia",
                HeaderText = "SEND VIA",
                Width = ColSendViaW,
                MinimumWidth = ColSendViaW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Discount",
                HeaderText = "DISCOUNT OFFER",
                Width = ColDiscountW,
                MinimumWidth = ColDiscountW
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Reason",
                HeaderText = "REASON",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = ColReasonMin,
                FillWeight = 70
            });

            var actionsCol = new DataGridViewTextBoxColumn
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
            };
            actionsCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            actionsCol.HeaderCell.Style.Padding = new Padding(0);
            _grid.Columns.Add(actionsCol);

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellMouseLeave += (s, e) => SetHover(-1);
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.Scroll += (s, e) => CloseActiveMenu();
            _grid.Resize += (s, e) => CloseActiveMenu();
            _grid.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) CloseActiveMenu(); };

            _gridCard.Controls.Add(_grid);

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

        private static void DrawSearchIcon(Graphics g, Rectangle r, Color color)
        {
            using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int d = (int)(r.Width * 0.78);
            var circle = new Rectangle(r.X, r.Y, d, d);
            g.DrawEllipse(pen, circle);
            g.DrawLine(pen, circle.Right - 2, circle.Bottom - 2, r.Right + 2, r.Bottom + 2);
        }

        private void LayoutAll()
        {
            if (_contentPanel == null) return;
            int w = _contentPanel.ClientSize.Width - _contentPanel.Padding.Horizontal;
            int h = _contentPanel.ClientSize.Height - _contentPanel.Padding.Vertical;
            if (w < 200) return;

            _addBtn.Location = new Point(w - _addBtn.Width, 20);

            _statusFilter.Location = new Point(w - _statusFilter.Width, ToolbarY);
            _searchBtn.Location = new Point(_statusFilter.Left - 8 - _searchBtn.Width, ToolbarY - 1);
            _searchBox.Width = Math.Max(160, _searchBtn.Left - 8);
            _searchBox.Location = new Point(0, ToolbarY);

            int pagerH = 48;
            _pager.SetBounds(0, h - pagerH, w, pagerH);
            _gridCard.SetBounds(0, GridTop, w, Math.Max(0, h - GridTop - pagerH));

            RecomputePageSize();
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
        private async Task LoadMyCustomersAsync()
        {
            try
            {
                var staffId = SessionUser.UserId;
                if (staffId <= 0) return;

                var list = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    $"api/follow-ups/my-customers?staffId={staffId}&companyId=1") ?? new();

                _myCustomers = list;
                _custById = list.ToDictionary(c => c.TenantCustomerId);
            }
            catch
            {
                _myCustomers = new();
                _custById = new();
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var staffId = SessionUser.UserId;
                if (staffId <= 0)
                {
                    MessageBox.Show("No signed-in user.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _all = await _http.GetFromJsonAsync<List<FollowUpDto>>(
                    $"api/follow-ups/mine?staffId={staffId}") ?? new();

                RenderGrid();
                RenderPager(CurrentTotalPages());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load your follow-ups.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ================================================================
        //  Grid rendering
        // ================================================================
        private List<FollowUpDto> FilteredList()
        {
            var search = _searchBox.Text?.Trim().ToLower() ?? "";
            var status = _statusFilter.SelectedItem?.ToString() ?? "All statuses";

            return _all
                .Where(f =>
                {
                    if (status != "All statuses")
                    {
                        var matches =
                            (status == "Draft" && f.Status == "Draft") ||
                            (status == "Pending Approval" && f.ApprovalStatus == "Pending") ||
                            (status == "Approved" && f.ApprovalStatus == "Approved") ||
                            (status == "Scheduled" && f.Status == "Scheduled") ||
                            (status == "Sent" && f.Status == "Sent") ||
                            (status == "Rejected" && f.ApprovalStatus == "Rejected") ||
                            (status == "Expired" && f.Status == "Expired");
                        if (!matches) return false;
                    }

                    if (string.IsNullOrEmpty(search)) return true;

                    var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;
                    var name = cust?.CustomerName?.ToLower() ?? "";
                    return name.Contains(search)
                        || (f.Reason ?? "").ToLower().Contains(search)
                        || (f.DiscountOffer ?? "").ToLower().Contains(search);
                })
                .OrderByDescending(f => f.FollowUpId)
                .ToList();
        }

        private int CurrentTotalPages()
        {
            var list = FilteredList();
            return Math.Max(1, (int)Math.Ceiling(list.Count / (double)_pageSize));
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

            foreach (var f in pageItems)
            {
                var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;

                string custCell = cust?.CustomerName ?? $"id:{f.CustomerId}";
                var second = string.Join("  ·  ",
                    new[] { cust?.ContactNumber, cust?.VehicleType }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(second))
                    custCell += "\n" + second;

                var scheduled = f.ScheduledDate.ToString("MMM d, yyyy  h:mm tt");
                var reason = string.IsNullOrWhiteSpace(f.Reason) ? "—" : f.Reason!;
                var discount = string.IsNullOrWhiteSpace(f.DiscountOffer) ? "—" : f.DiscountOffer;
                var method = string.IsNullOrWhiteSpace(f.ContactMethod) ? "Email" : f.ContactMethod;

                string displayStatus = f.ApprovalStatus switch
                {
                    "Pending" => "Pending Approval",
                    "Rejected" => "Rejected",
                    "Approved" when f.Status == "Sent" => "Sent",
                    "Approved" when f.Status == "Scheduled" => "Scheduled",
                    _ => f.Status ?? "Draft"
                };

                // Values supplied in column order: FollowUpId, Customer, Status,
                // Scheduled, SendVia, Discount, Reason, Actions.
                int idx = _grid.Rows.Add(
                    f.FollowUpId,
                    custCell,
                    displayStatus,
                    scheduled,
                    method,
                    discount,
                    reason,
                    "");

                _grid.Rows[idx].Tag = f.FollowUpId;
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
            _grid.PerformLayout();

            var from = list.Count == 0 ? 0 : start + 1;
            var to = Math.Min(start + _pageSize, list.Count);
            _showingLbl.Text = $"Showing {from}–{to} of {list.Count} follow-ups";

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

            if (col == "Customer" || col == "Reason")
            {
                PaintTwoLine(e);
                return;
            }

            if (col == "Status")
            {
                PaintStatusPill(e);
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

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Draft" => (NeutralSoft, Muted),
            "Pending Approval" or "Pending" => (AmberSoft, Amber),
            "Approved" => (BlueSoft, Blue),
            "Scheduled" => (SlateSoft, Slate),
            "Sent" or "Contacted" => (BlueSoft, Blue),
            "Redeemed" => (GreenSoft, Green),
            "Rejected" => (RedSoft, Red),
            "Expired" => (RedSoft, Red),
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

            Color fill = hover ? BlueSoft : Color.White;
            Color border = hover ? Blue : CardBorder;
            Color dot = hover ? Blue : Navy;

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
        //  Hover / Click
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

            var idText = _grid.Rows[e.RowIndex].Cells["FollowUpId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            var dto = _all.FirstOrDefault(f => f.FollowUpId == id);
            if (dto == null) return;

            ShowActionsMenu(e.RowIndex, dto);
        }

        // ================================================================
        //  Action menu — status-aware
        // ================================================================
        private void ShowActionsMenu(int rowIndex, FollowUpDto f)
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

            AddMenuItem(menu, "View", () => ShowDetailDialog(f));

            if (f.ApprovalStatus == "Pending")
            {
                AddMenuItem(menu, "Edit", () => OpenEditDialog(f));
                AddMenuItem(menu, "Cancel", () => ArchiveAsync(f), isDanger: true);
            }
            else if (f.ApprovalStatus == "Approved")
            {
                AddMenuItem(menu, "Archive", () => ArchiveAsync(f), isDanger: true);
            }
            else if (f.ApprovalStatus == "Rejected")
            {
                AddMenuItem(menu, "Edit", () => OpenEditDialog(f));
                AddMenuItem(menu, "Submit for Approval", () => SubmitForApprovalAsync(f));
                AddMenuItem(menu, "Archive", () => ArchiveAsync(f), isDanger: true);
            }
            else if (string.Equals(f.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                AddMenuItem(menu, "Edit", () => OpenEditDialog(f));
                AddMenuItem(menu, "Submit for Approval", () => SubmitForApprovalAsync(f));
                AddMenuItem(menu, "Archive", () => ArchiveAsync(f), isDanger: true);
            }
            else
            {
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
                Width = 200
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
        //  Actions
        // ================================================================
        private async void OpenAddDialog()
        {
            await LoadMyCustomersAsync();

            if (_myCustomers.Count == 0)
            {
                MessageBox.Show(
                    "You don't have any assigned customers yet.\n\n" +
                    "Customers become available once a manager assigns you a service request.",
                    "No Assigned Customers",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new ServiceStaffFollowUpDialog(_myCustomers, SessionUser.UserId);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _page = 1;
                await LoadAsync();
            }
        }

        private void OpenEditDialog(FollowUpDto f)
        {
            using var dlg = new FollowUpEditDialog(_myCustomers, f);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = LoadAsync();
            }
        }

        private void ShowDetailDialog(FollowUpDto f)
        {
            var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;

            MessageBox.Show(
                $"Follow-Up #{f.FollowUpId}\n\n" +
                $"Customer       : {cust?.CustomerName ?? $"id:{f.CustomerId}"}\n" +
                $"Send Via       : {f.ContactMethod}\n" +
                $"Scheduled      : {f.ScheduledDate:yyyy-MM-dd HH:mm}\n" +
                $"Valid Until    : {(f.ValidUntil.HasValue ? f.ValidUntil.Value.ToString("yyyy-MM-dd") : "—")}\n" +
                $"Reason         : {f.Reason ?? "—"}\n" +
                $"Discount Offer : {f.DiscountOffer ?? "—"}\n" +
                $"Status         : {f.Status}\n" +
                $"Approval       : {f.ApprovalStatus}\n" +
                $"Sent At        : {(f.SentAt.HasValue ? f.SentAt.Value.ToString("yyyy-MM-dd HH:mm") : "—")}\n" +
                (string.IsNullOrWhiteSpace(f.RejectionReason) ? "" : $"\nRejection Reason:\n{f.RejectionReason}\n") +
                $"\nMessage:\n{f.Notes ?? "(none)"}",
                "Follow-Up Details",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void SubmitForApprovalAsync(FollowUpDto f)
        {
            var confirm = MessageBox.Show(
                "Submit this follow-up for Admin approval?\n\n" +
                "The Admin will review it before it can be sent to the customer.",
                "Submit for Approval",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;

                var resp = await _http.PostAsJsonAsync(
                    $"api/follow-ups/{f.FollowUpId}/submit-for-approval",
                    new
                    {
                        submittedBy = SessionUser.UserId,
                        sendNow = f.ScheduledDate <= DateTime.Now,
                        scheduledDate = (DateTime?)f.ScheduledDate
                    });

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        "Submitted. It's now waiting for Admin approval.",
                        "Submitted",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadAsync();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Submit failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Submit failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private async void ArchiveAsync(FollowUpDto f)
        {
            if (!ArchiveConfirmDialog.ConfirmArchive("follow-up")) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.PutAsJsonAsync(
                    $"api/follow-ups/{f.FollowUpId}/archive",
                    new { archivedBy = CurrentUserName });

                if (resp.IsSuccessStatusCode)
                {
                    await LoadAsync();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Archive failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Archive failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }
    }
}