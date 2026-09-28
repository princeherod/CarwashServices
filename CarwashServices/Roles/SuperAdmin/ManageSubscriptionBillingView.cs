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
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Super Admin Module: Manage Subscription & Billing.
    /// Features:
    /// - 3 summary tiles (Total Paid green, Outstanding red, Active Subscriptions blue) via GET /api/billing/summary
    /// - Segmented Tab Control: SUBSCRIPTION_PLANS / CUSTOMER_SUBSCRIPTIONS / BILLING_TRANSACTIONS
    /// - Each tab backed by its own GET endpoint and rendered in the existing grid style
    /// - Custom pills for billing_cycle and status/payment_status columns
    /// </summary>
    public class ManageSubscriptionBillingView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color NavyHover = Color.FromArgb(0x16, 0x2A, 0x5C);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Blue = Color.FromArgb(0x02, 0x84, 0xC7);
        private static readonly Color BlueSoft = Color.FromArgb(0xE0, 0xF2, 0xFE);
        private static readonly Color Purple = Color.FromArgb(0x7C, 0x3A, 0xED);
        private static readonly Color PurpleSoft = Color.FromArgb(0xF3, 0xE8, 0xFF);
        private static readonly Color Green = Color.FromArgb(0x16, 0xA3, 0x4A);
        private static readonly Color GreenSoft = Color.FromArgb(0xDC, 0xFC, 0xE7);
        private static readonly Color Amber = Color.FromArgb(0xB4, 0x53, 0x09);
        private static readonly Color AmberSoft = Color.FromArgb(0xFE, 0xF3, 0xC7);
        private static readonly Color Red = Color.FromArgb(0xDC, 0x26, 0x26);
        private static readonly Color RedSoft = Color.FromArgb(0xFE, 0xE2, 0xE2);

        // ================================================================
        //  Fonts
        // ================================================================
        private static readonly Font FontAvatar = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontName = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontSub = new("Segoe UI", 8.5f);
        private static readonly Font FontPill = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);

        // ================================================================
        //  HTTP
        // ================================================================
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(20)
        };

        // ================================================================
        //  Controls & Layout
        // ================================================================
        private Panel _contentPanel = null!;
        private Panel _tileTotalPaid = null!;
        private Panel _tileOutstanding = null!;
        private Panel _tileActiveSubs = null!;
        private Label _lblTotalPaidVal = null!;
        private Label _lblOutstandingVal = null!;
        private Label _lblActiveSubsVal = null!;

        private Panel _tabBarPanel = null!;
        private Button _tabPlansBtn = null!;
        private Button _tabSubsBtn = null!;
        private Button _tabTxsBtn = null!;

        private DataGridView _grid = null!;

        public enum TabMode { Plans, CustomerSubscriptions, Transactions }
        private TabMode _currentTab = TabMode.Plans;

        // Data caches
        private List<SubscriptionPlanItemDto> _plans = new();
        private List<CustomerSubscriptionItemDto> _customerSubs = new();
        private List<BillingTransactionItemDto> _transactions = new();

        public ManageSubscriptionBillingView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            if (SessionUser.RoleId != 4)
            {
                Controls.Add(new AccessDeniedView("Manage Subscription / Billing", "Super Admin (Role 4)"));
                return;
            }

            InitializeUI();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) =>
            {
                await LoadSummaryAsync();
                await SwitchTabAsync(TabMode.Plans);
            };
        }

        private void InitializeUI()
        {
            SuspendLayout();

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                AutoScroll = true
            };
            Controls.Add(_contentPanel);

            const int padX = 36;

            // ---- Breadcrumb ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Super Admin Modules  ›  Manage Subscription & Billing",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(padX, 12),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Title ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Subscription & Billing Management",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(padX, 34),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Subtitle ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "COMMERCIAL TIERS & INVOICING — subscription plans, recurring billing cycles, tenant subscriptions, and ledger transactions",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(padX, 80),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- 3 Summary Tiles ----
            _tileTotalPaid = CreateTile("Total Paid", "✓ Collected", Green, GreenSoft, out _lblTotalPaidVal);
            _tileOutstanding = CreateTile("Outstanding", "⚠ Pending", Red, RedSoft, out _lblOutstandingVal);
            _tileActiveSubs = CreateTile("Active Subscriptions", "● Enrolled", Blue, BlueSoft, out _lblActiveSubsVal);

            _contentPanel.Controls.Add(_tileTotalPaid);
            _contentPanel.Controls.Add(_tileOutstanding);
            _contentPanel.Controls.Add(_tileActiveSubs);

            // ---- Segmented Tab Control ----
            _tabBarPanel = new Panel
            {
                Location = new Point(padX, 224),
                Height = 44,
                BackColor = Color.White
            };
            _tabBarPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, _tabBarPanel.Width - 1, _tabBarPanel.Height - 1), 8);
                using var borderPen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            };
            _contentPanel.Controls.Add(_tabBarPanel);

            _tabPlansBtn = CreateTabButton("Subscription Plans", 4, 190);
            _tabSubsBtn = CreateTabButton("Customer Subscriptions", 198, 210);
            _tabTxsBtn = CreateTabButton("Billing Transactions", 412, 200);

            _tabPlansBtn.Click += async (s, e) => await SwitchTabAsync(TabMode.Plans);
            _tabSubsBtn.Click += async (s, e) => await SwitchTabAsync(TabMode.CustomerSubscriptions);
            _tabTxsBtn.Click += async (s, e) => await SwitchTabAsync(TabMode.Transactions);

            _tabBarPanel.Controls.Add(_tabPlansBtn);
            _tabBarPanel.Controls.Add(_tabSubsBtn);
            _tabBarPanel.Controls.Add(_tabTxsBtn);

            // ---- DataGridView Grid ----
            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                Location = new Point(padX, 280),
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderBg,
                    ForeColor = Muted,
                    SelectionBackColor = HeaderBg,
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersHeight = 44,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 64 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = FontCell,
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(12, 0, 8, 0),
                    WrapMode = DataGridViewTriState.False
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Vertical,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _grid.CellPainting += Grid_CellPainting;
            _contentPanel.Controls.Add(_grid);

            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();

            ResumeLayout(true);
        }

        private static Panel CreateTile(string caption, string badge, Color fgColor, Color bgColor, out Label valLabel)
        {
            var p = new Panel
            {
                BackColor = Color.White,
                Height = 96
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 10);
                using var borderPen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            };

            // Top caption
            var capLbl = new Label
            {
                Text = caption,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = Muted,
                Location = new Point(18, 16),
                AutoSize = true,
                UseMnemonic = false
            };
            p.Controls.Add(capLbl);

            // Badge Pill
            var badgeLbl = new Label
            {
                Text = badge,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = fgColor,
                BackColor = bgColor,
                Location = new Point(p.Width - 90, 14),
                Padding = new Padding(8, 2, 8, 2),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                UseMnemonic = false
            };
            void Round()
            {
                using var path = RoundedRect(new Rectangle(0, 0, badgeLbl.Width, badgeLbl.Height), badgeLbl.Height / 2);
                badgeLbl.Region = new Region(path);
            }
            badgeLbl.SizeChanged += (s, e) => Round();
            Round();
            p.Controls.Add(badgeLbl);

            // Value Label
            valLabel = new Label
            {
                Text = "...",
                Font = new Font("Segoe UI Bold", 19f),
                ForeColor = fgColor,
                Location = new Point(18, 44),
                AutoSize = true,
                UseMnemonic = false
            };
            p.Controls.Add(valLabel);

            return p;
        }

        private static Button CreateTabButton(string text, int x, int width = 210)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9f),
                Size = new Size(width, 36),
                Location = new Point(x, 4),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                ForeColor = Muted
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void Relayout()
        {
            const int padX = 36;
            int totalW = Math.Max(700, _contentPanel.ClientSize.Width - (padX * 2));
            int gap = 16;
            int tileW = (totalW - (gap * 2)) / 3;

            _tileTotalPaid.SetBounds(padX, 114, tileW, 96);
            _tileOutstanding.SetBounds(padX + tileW + gap, 114, tileW, 96);
            _tileActiveSubs.SetBounds(padX + (tileW + gap) * 2, 114, tileW, 96);

            _tabBarPanel.SetBounds(padX, 224, totalW, 44);

            int gridTop = 280;
            int gridH = Math.Max(200, _contentPanel.ClientSize.Height - gridTop - 24);
            _grid.SetBounds(padX, gridTop, totalW, gridH);
        }

        // ================================================================
        //  DATA LOADING & TAB SWITCHING
        // ================================================================
        public async Task LoadSummaryAsync()
        {
            try
            {
                var summary = await _http.GetFromJsonAsync<BillingSummaryDto>("api/billing/summary");
                if (summary != null)
                {
                    _lblTotalPaidVal.Text = summary.TotalPaidFormatted;
                    _lblOutstandingVal.Text = summary.OutstandingFormatted;
                    _lblActiveSubsVal.Text = summary.ActiveSubscriptions.ToString();
                }
            }
            catch (Exception ex)
            {
                _lblTotalPaidVal.Text = "Error";
                _lblOutstandingVal.Text = "Error";
                _lblActiveSubsVal.Text = "!";
                Console.WriteLine($"Failed to load billing summary: {ex.Message}");
            }
        }

        public async Task SwitchTabAsync(TabMode tab)
        {
            _currentTab = tab;

            // Highlight active tab button
            SetTabBtnState(_tabPlansBtn, tab == TabMode.Plans);
            SetTabBtnState(_tabSubsBtn, tab == TabMode.CustomerSubscriptions);
            SetTabBtnState(_tabTxsBtn, tab == TabMode.Transactions);

            Cursor = Cursors.WaitCursor;
            try
            {
                switch (tab)
                {
                    case TabMode.Plans:
                        await LoadPlansAsync();
                        break;
                    case TabMode.CustomerSubscriptions:
                        await LoadCustomerSubscriptionsAsync();
                        break;
                    case TabMode.Transactions:
                        await LoadTransactionsAsync();
                        break;
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private static void SetTabBtnState(Button btn, bool active)
        {
            if (active)
            {
                btn.BackColor = Navy;
                btn.ForeColor = Color.White;
            }
            else
            {
                btn.BackColor = Color.Transparent;
                btn.ForeColor = Muted;
            }
            void Round()
            {
                using var path = RoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), 6);
                btn.Region = new Region(path);
            }
            btn.SizeChanged += (s, e) => Round();
            Round();
        }

        private async Task LoadPlansAsync()
        {
            _plans = await _http.GetFromJsonAsync<List<SubscriptionPlanItemDto>>("api/billing/plans")
                     ?? new List<SubscriptionPlanItemDto>();

            _grid.SuspendLayout();
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PlanId", HeaderText = "Plan ID", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PlanName",
                HeaderText = "Plan Name & Details",
                Width = 240
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Price", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillingCycle", HeaderText = "Billing Cycle", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActiveMembers", HeaderText = "Enrolled Tenants", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = "Description",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 200
            });

            foreach (var p in _plans)
            {
                _grid.Rows.Add(
                    p.PlanId,
                    p.PlanName,
                    p.PriceFormatted,
                    p.BillingCycle,
                    $"{p.ActiveSubscribers} active",
                    p.Description);
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        private async Task LoadCustomerSubscriptionsAsync()
        {
            _customerSubs = await _http.GetFromJsonAsync<List<CustomerSubscriptionItemDto>>("api/billing/customer-subscriptions")
                            ?? new List<CustomerSubscriptionItemDto>();

            _grid.SuspendLayout();
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SubId", HeaderText = "Sub ID", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Company",
                HeaderText = "Tenant Company",
                Width = 200
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Admin",
                HeaderText = "Admin User",
                Width = 190
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Plan", HeaderText = "Subscription", Width = 145 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillingCycle", HeaderText = "Billing Cycle", Width = 115 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartDate", HeaderText = "Start Date", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", HeaderText = "Renewal Date", Width = 105 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 90
            });

            foreach (var s in _customerSubs)
            {
                var compCell = $"{s.TenantCompany}\n{s.CompanyCode}";
                var adminCell = $"{s.AdminUser}\n{s.AdminEmail}";
                var planCell = $"{s.PlanName}\n{s.PriceFormatted}";

                _grid.Rows.Add(
                    s.SubscriptionId,
                    compCell,
                    adminCell,
                    planCell,
                    s.BillingCycle,
                    s.StartDate.ToString("yyyy-MM-dd"),
                    s.EndDate.HasValue ? s.EndDate.Value.ToString("yyyy-MM-dd") : "—",
                    s.Status);
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        private async Task LoadTransactionsAsync()
        {
            _transactions = await _http.GetFromJsonAsync<List<BillingTransactionItemDto>>("api/billing/transactions")
                            ?? new List<BillingTransactionItemDto>();

            _grid.SuspendLayout();
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TxnId", HeaderText = "Txn ID", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Company",
                HeaderText = "Tenant Company",
                Width = 210
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Plan", HeaderText = "Subscription", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reference", HeaderText = "Reference Number", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Transaction Date", Width = 135 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PaymentStatus",
                HeaderText = "Payment Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 110
            });

            foreach (var t in _transactions)
            {
                var compCell = $"{t.TenantCompany}\n{t.TenantCompanyCode}";

                _grid.Rows.Add(
                    t.TransactionId,
                    compCell,
                    t.PlanName,
                    t.ReferenceNumber,
                    t.AmountFormatted,
                    t.TransactionDate.ToString("yyyy-MM-dd HH:mm"),
                    t.PaymentStatus);
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        // ================================================================
        //  CELL PAINTING (Pills & Avatars)
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "BillingCycle":
                    PaintBillingCyclePill(e);
                    break;
                case "Status":
                case "PaymentStatus":
                    PaintStatusPill(e);
                    break;
                case "Company":
                    PaintCompanyCell(e);
                    break;
                case "Admin":
                    PaintAdminCell(e);
                    break;
                case "Plan":
                    PaintPlanCell(e);
                    break;
            }
        }

        private void PaintPlanCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var name = parts[0];
            var price = parts.Length > 1 ? parts[1] : "";

            var b = e.CellBounds;
            if (b.Width <= 4 || b.Height <= 4)
            {
                e.Handled = true;
                return;
            }

            int x = b.X + 12;
            int tw = Math.Max(10, b.Right - x - 8);

            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine;

            int h1 = FontName.Height;
            int h2 = FontSub.Height;
            int gap = 2;
            int top = b.Y + (b.Height - (h1 + (string.IsNullOrEmpty(price) ? 0 : h2 + gap))) / 2;

            TextRenderer.DrawText(e.Graphics, name, FontName,
                new Rectangle(x, top, tw, h1), Navy, flags);

            if (!string.IsNullOrEmpty(price))
            {
                TextRenderer.DrawText(e.Graphics, price, FontSub,
                    new Rectangle(x, top + h1 + gap, tw, h2), Muted, flags);
            }

            e.Handled = true;
        }

        private void PaintBillingCyclePill(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            Color bg, fg;
            if (text.IndexOf("Month", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bg = PurpleSoft;
                fg = Purple;
            }
            else if (text.IndexOf("Quarter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bg = BlueSoft;
                fg = Blue;
            }
            else
            {
                bg = GreenSoft;
                fg = Green;
            }

            var b = e.CellBounds;
            using var font = FontPill;
            var size = TextRenderer.MeasureText(e.Graphics, text, font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int pillW = size.Width + 22;
            int pillH = size.Height + 8;
            var pill = new Rectangle(
                b.X + 12,
                b.Y + (b.Height - pillH) / 2,
                pillW, pillH);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(pill, pillH / 2))
            using (var brush = new SolidBrush(bg))
                e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(e.Graphics, text, font, pill, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private void PaintStatusPill(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            Color bg, fg;
            if (string.Equals(text, "Active", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                bg = GreenSoft;
                fg = Green;
            }
            else if (string.Equals(text, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                bg = AmberSoft;
                fg = Amber;
            }
            else
            {
                bg = RedSoft;
                fg = Red;
            }

            var b = e.CellBounds;
            using var font = FontPill;
            var size = TextRenderer.MeasureText(e.Graphics, text, font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int pillW = size.Width + 22;
            int pillH = size.Height + 8;
            var pill = new Rectangle(
                b.X + 12,
                b.Y + (b.Height - pillH) / 2,
                pillW, pillH);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(pill, pillH / 2))
            using (var brush = new SolidBrush(bg))
                e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(e.Graphics, text, font, pill, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private void PaintCompanyCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var name = parts[0];
            var code = parts.Length > 1 ? parts[1] : "";

            var b = e.CellBounds;
            if (b.Width <= 4 || b.Height <= 4)
            {
                e.Handled = true;
                return;
            }

            int avatarSize = Math.Min(34, Math.Max(16, b.Height - 12));
            int ax = b.X + 12;
            int ay = b.Y + (b.Height - avatarSize) / 2;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Blue))
                e.Graphics.FillEllipse(bg, ax, ay, avatarSize, avatarSize);

            string initials = Initials(name);
            var avatarRect = new Rectangle(ax, ay, avatarSize, avatarSize);

            TextRenderer.DrawText(
                e.Graphics,
                initials,
                FontAvatar,
                avatarRect,
                Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);

            int tx = ax + avatarSize + 12;
            int tw = Math.Max(10, b.Right - tx - 12);

            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine;

            int h1 = FontName.Height;
            int h2 = FontSub.Height;
            int gap = 2;
            int top = b.Y + (b.Height - (h1 + (string.IsNullOrEmpty(code) ? 0 : h2 + gap))) / 2;

            TextRenderer.DrawText(e.Graphics, name, FontName,
                new Rectangle(tx, top, tw, h1), Navy, flags);

            if (!string.IsNullOrEmpty(code))
            {
                TextRenderer.DrawText(e.Graphics, code, FontSub,
                    new Rectangle(tx, top + h1 + gap, tw, h2), Muted, flags);
            }

            e.Handled = true;
        }

        private void PaintAdminCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var name = parts[0];
            var email = parts.Length > 1 ? parts[1] : "";

            var b = e.CellBounds;
            if (b.Width <= 4 || b.Height <= 4)
            {
                e.Handled = true;
                return;
            }

            int avatarSize = Math.Min(34, Math.Max(16, b.Height - 12));
            int ax = b.X + 12;
            int ay = b.Y + (b.Height - avatarSize) / 2;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Navy))
                e.Graphics.FillEllipse(bg, ax, ay, avatarSize, avatarSize);

            string initials = Initials(name);
            var avatarRect = new Rectangle(ax, ay, avatarSize, avatarSize);

            TextRenderer.DrawText(
                e.Graphics,
                initials,
                FontAvatar,
                avatarRect,
                Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);

            int tx = ax + avatarSize + 12;
            int tw = Math.Max(10, b.Right - tx - 12);

            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine;

            int h1 = FontName.Height;
            int h2 = FontSub.Height;
            int gap = 2;
            int top = b.Y + (b.Height - (h1 + (string.IsNullOrEmpty(email) ? 0 : h2 + gap))) / 2;

            TextRenderer.DrawText(e.Graphics, name, FontName,
                new Rectangle(tx, top, tw, h1), Navy, flags);

            if (!string.IsNullOrEmpty(email))
            {
                TextRenderer.DrawText(e.Graphics, email, FontSub,
                    new Rectangle(tx, top + h1 + gap, tw, h2), Muted, flags);
            }

            e.Handled = true;
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
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
    }
}
