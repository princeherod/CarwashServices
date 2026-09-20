using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dialogs;
using CarwashServices.Dtos;

namespace CarwashServices.Roles
{
    public class ServiceRequestsView : UserControl
    {
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

        // ---- Fonts (created once instead of on every cell paint) ----
        private static readonly Font FontLine1 = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new Font("Segoe UI", 8.5f);
        private static readonly Font FontPlain = new Font("Segoe UI", 9.5f);
        private static readonly Font FontTag = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontButton = new Font("Segoe UI Semibold", 9f);

        // Status / Priority are drawn as coloured words only (no pill).
        // Set to true if you also want a light square highlight behind the words.
        private static readonly bool TintBehindText = false;

        // ---- Layout ----
        private const int PadX = 40;             // left / right page margin (Padding does not move absolutely-placed children)
        private const int FilterTop = 166;
        private const int FilterH = 76;
        private const int GridTop = FilterTop + FilterH + 12;   // 254
        private const int PageBottom = 24;

        // Edit "button" drawn inside the Edit column
        private const int EditBtnW = 64;
        private const int EditBtnH = 30;

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<ServiceRequestDto> _all = new();
        private List<TenantCustomerDto> _tenantCustomers = new();
        private List<ProductDto> _tenantProducts = new();
        private List<UserDto> _users = new();

        private List<CustomerDto> _customerLookup = new();
        private List<ServiceDto> _serviceLookup = new();

        private string _activeStatus = "All";
        private int _hoverEditRow = -1;
        private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };

        private Panel _contentPanel = null!;
        private DataGridView _grid = null!;
        private TextBox _searchBox = null!;
        private Button _newRequestBtn = null!;
        private Panel _chipBar = null!;

        public ServiceRequestsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _debounce.Tick += (s, e) => { _debounce.Stop(); ApplyFilter(); };

            InitializeUI();

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
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            Controls.Add(_contentPanel);

            // ---- Breadcrumb ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Service Requests",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 12),
                AutoSize = true
            });

            // ---- Header ----
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
                Location = new Point(PadX, 80),      // was 68: the title (22pt) ran into it
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

            // ---- Status chips ----
            _chipBar = new Panel
            {
                Location = new Point(PadX, 114),
                Height = 40,
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            // ---- Filter card (same look as Manage Customers / Manage Services) ----
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

            // Bordered wrapper + borderless TextBox = a clearly visible search field
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
            refreshBtn.Click += async (s, e) =>
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
            filterCard.Controls.Add(refreshBtn);

            // ---- Grid ----
            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                Location = new Point(PadX, GridTop),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
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
                RowTemplate = { Height = 62 },
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

            // Header text shortened where it was being cut off (REQUEST_ID -> ID, PRIORITY widened)
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestId", HeaderText = "ID", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "CUSTOMER",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "SERVICE",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 160,
                FillWeight = 90
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "SCHEDULED", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Staff", HeaderText = "STAFF", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "PRIORITY", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 120 });

            // Edit is now a plain text column that we paint as a real button (see PaintEditButton)
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Edit", HeaderText = "", Width = 100 });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseLeave += (s, e) => SetEditHover(-1);
            _grid.MouseLeave += (s, e) => SetEditHover(-1);
            _grid.CellMouseClick += Grid_CellMouseClick;
            _contentPanel.Controls.Add(_grid);

            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;
                int contentW = Math.Max(0, w - 2 * PadX);

                _newRequestBtn.Location = new Point(w - _newRequestBtn.Width - PadX, 30);
                _chipBar.Width = contentW;

                filterCard.Width = contentW;
                refreshBtn.Location = new Point(contentW - 16 - refreshBtn.Width, 28);
                searchBtn.Location = new Point(refreshBtn.Left - 10 - searchBtn.Width, 28);
                searchWrap.Width = Math.Max(120, searchBtn.Left - 12 - searchWrap.Left);

                _grid.SetBounds(PadX, GridTop, contentW, Math.Max(0, h - GridTop - PageBottom));
            }
            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();
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
                chip.Click += (s, e) => { _activeStatus = captured; BuildChips(); ApplyFilter(); };
                _chipBar.Controls.Add(chip);
                x += 130;
            }
        }

        // ================================================================
        //  DATA
        // ================================================================
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
            catch
            {
                _tenantCustomers = new();
                _customerLookup = new();
            }

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
            catch
            {
                _tenantProducts = new();
                _serviceLookup = new();
            }

            try
            {
                _users = await _http.GetFromJsonAsync<List<UserDto>>("api/users") ?? new();
            }
            catch
            {
                _users = new();
            }
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                _newRequestBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                _all = await _http.GetFromJsonAsync<List<ServiceRequestDto>>("api/service-requests")
                       ?? new();

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
                _newRequestBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyFilter));
                return;
            }

            var search = _searchBox.Text?.Trim().ToLower() ?? "";

            _hoverEditRow = -1;
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var r in _all)
            {
                if (_activeStatus != "All" &&
                    !string.Equals(r.Status, _activeStatus, StringComparison.OrdinalIgnoreCase))
                    continue;

                var tenantCust = _tenantCustomers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);
                var svc = _serviceLookup.FirstOrDefault(s => s.ServiceId == r.ServiceId);
                var staff = r.AssignedStaffId.HasValue
                    ? _users.FirstOrDefault(u => u.UserId == r.AssignedStaffId.Value)
                    : null;

                // Customer cell: name\nemail
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

                // Service cell: name\nid · price
                string svcCell = svc != null
                    ? $"{svc.ServiceName}\nid:{svc.ServiceId}  ·  ₱{svc.Price:N0}"
                    : $"id:{r.ServiceId}";

                // Staff cell: name\nid
                string staffCell = staff != null
                    ? $"{staff.FullName}\nid:{staff.UserId}"
                    : (r.AssignedStaffId.HasValue ? $"id:{r.AssignedStaffId}" : "Unassigned");

                if (!string.IsNullOrEmpty(search))
                {
                    // Plate number is included so the "plate" hint in the search box actually works
                    var blob = $"{custCell} {tenantCust?.PlateNumber} {svcCell} {staffCell} {r.Status} {r.Priority}".ToLower();
                    if (!blob.Contains(search)) continue;
                }

                _grid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    svcCell,
                    r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "—",
                    staffCell,
                    string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority,
                    r.Status,
                    "Edit");
            }

            _grid.ClearSelection();     // don't leave the first row highlighted
            _grid.ResumeLayout();
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

                case "Edit":
                    PaintEditButton(e, e.RowIndex == _hoverEditRow);
                    break;
            }
        }

        // Line heights come from the fonts (Font.Height). The old hard-coded 16px / 14px boxes
        // were smaller than the text, which is why the second line looked cut off.
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
                // Single line (e.g. "Unassigned"): muted + regular so it doesn't shout
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

        // Status / Priority: just the words, in colour. No pill, no circle.
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

        // Edit: a real button — rounded outline, blue text, fills blue on hover.
        private static void PaintEditButton(DataGridViewCellPaintingEventArgs e, bool hover)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var r = EditButtonRect(e.CellBounds.Width, e.CellBounds.Height);
            r.Offset(e.CellBounds.X, e.CellBounds.Y);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 5))
            using (var fill = new SolidBrush(hover ? Blue : Color.White))
            using (var pen = new Pen(Blue, 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(e.Graphics, "Edit", FontButton, r,
                hover ? Color.White : Blue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        // Button rectangle in CELL-LOCAL coordinates (used for painting, hover and click)
        private static Rectangle EditButtonRect(int cellW, int cellH) =>
            new Rectangle((cellW - EditBtnW) / 2, (cellH - EditBtnH) / 2, EditBtnW, EditBtnH);

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
        //  EDIT BUTTON — hover + click
        // ================================================================
        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Edit")
            {
                SetEditHover(-1);
                return;
            }

            var rect = EditButtonRect(_grid.Columns[e.ColumnIndex].Width, _grid.Rows[e.RowIndex].Height);
            SetEditHover(rect.Contains(e.Location) ? e.RowIndex : -1);
        }

        private void SetEditHover(int row)
        {
            if (row == _hoverEditRow) return;

            int old = _hoverEditRow;
            _hoverEditRow = row;

            var editCol = _grid.Columns["Edit"];
            if (editCol != null)
            {
                if (old >= 0 && old < _grid.RowCount) _grid.InvalidateCell(editCol.Index, old);
                if (row >= 0 && row < _grid.RowCount) _grid.InvalidateCell(editCol.Index, row);
            }

            _grid.Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
        }

        // Only clicks that land on the drawn button count (not the empty part of the cell)
        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Edit") return;

            var rect = EditButtonRect(_grid.Columns[e.ColumnIndex].Width, _grid.Rows[e.RowIndex].Height);
            if (!rect.Contains(e.Location)) return;

            var idText = _grid.Rows[e.RowIndex].Cells["RequestId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText.TrimStart('#'), out var id)) return;

            OpenEditRequestDialog(id);
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
    }
}