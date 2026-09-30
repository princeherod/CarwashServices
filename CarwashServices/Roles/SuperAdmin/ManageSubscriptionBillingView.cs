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

namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Super Admin Module: Manage Subscription & Billing.
    /// Features:
    /// - 3 summary tiles (Total Paid green, Outstanding red, Active Subscriptions blue) via GET /api/billing/summary
    /// - Segmented Tab Control: SUBSCRIPTION_PLANS / CUSTOMER_SUBSCRIPTIONS / BILLING_TRANSACTIONS
    /// - SaaS multi-tenant plan management: Create, Edit, Activate/Deactivate, Archive/Restore
    /// - Tenant plan assignment and payment settlement dialog integrations
    /// - Custom pills for billing cycle and status/payment status columns
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
        private static readonly Color DeepNavy = Color.FromArgb(0x0C, 0x4A, 0x6E);
        private static readonly Color DeepNavySoft = Color.FromArgb(0xEA, 0xF2, 0xFD);
        private static readonly Color SlateBlue = Color.FromArgb(0x47, 0x55, 0x69);
        private static readonly Color SlateBlueSoft = Color.FromArgb(0xF1, 0xF5, 0xF9);
        private static readonly Color RoyalBlue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color RoyalBlueSoft = Color.FromArgb(0xDB, 0xEA, 0xFE);

        // ================================================================
        //  Fonts
        // ================================================================
        private static readonly Font FontAvatar = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontName = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontSub = new("Segoe UI", 8.5f);
        private static readonly Font FontPill = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontAction = new("Segoe UI Semibold", 8.5f);

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
        private Button _actionBtn = null!;

        private DataGridView _grid = null!;

        public enum TabMode { Plans, CustomerSubscriptions, Transactions }
        private TabMode _currentTab = TabMode.Plans;

        // Data caches
        private List<SubscriptionPlanItemDto> _plans = new();
        private List<CustomerSubscriptionItemDto> _customerSubs = new();
        private List<BillingTransactionItemDto> _transactions = new();

        // Actions Three-Dot Menu & Hover
        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;

        public ManageSubscriptionBillingView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            if (SessionUser.RoleId != 1 && SessionUser.Role != UserRole.SuperAdmin)
            {
                Controls.Add(new AccessDeniedView("Manage Subscription / Billing", "Super Admin (Role 1)"));
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

            // ---- Title ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Subscriptions",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(padX, 20),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Subtitle ----
            _contentPanel.Controls.Add(new Label
            {
                Text = SuperAdminLabels.ManageSubscriptionBillingSubtitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(padX, 64),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- 3 Summary Tiles (Clickable KPIs) ----
            _tileTotalPaid = CreateTile(SuperAdminLabels.TileTotalPaid, SuperAdminLabels.BadgeCollected, DeepNavy, DeepNavySoft, out _lblTotalPaidVal, () =>
            {
                _ = SwitchTabAsync(TabMode.Transactions);
            });
            _tileOutstanding = CreateTile(SuperAdminLabels.TileOutstanding, SuperAdminLabels.BadgePending, SlateBlue, SlateBlueSoft, out _lblOutstandingVal, () =>
            {
                _ = SwitchTabAsync(TabMode.Transactions);
            });
            _tileActiveSubs = CreateTile(SuperAdminLabels.TileActiveSubscriptions, SuperAdminLabels.BadgeEnrolled, RoyalBlue, RoyalBlueSoft, out _lblActiveSubsVal, () =>
            {
                _ = SwitchTabAsync(TabMode.CustomerSubscriptions);
            });

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

            _tabPlansBtn = CreateTabButton(SuperAdminLabels.TabSubscriptionPlans, 4, 180);
            _tabSubsBtn = CreateTabButton(SuperAdminLabels.TabCustomerSubscriptions, 188, 200);
            _tabTxsBtn = CreateTabButton(SuperAdminLabels.TabBillingTransactions, 392, 190);

            _tabPlansBtn.Click += async (s, e) => await SwitchTabAsync(TabMode.Plans);
            _tabSubsBtn.Click += async (s, e) => await SwitchTabAsync(TabMode.CustomerSubscriptions);
            _tabTxsBtn.Click += async (s, e) => await SwitchTabAsync(TabMode.Transactions);

            _tabBarPanel.Controls.Add(_tabPlansBtn);
            _tabBarPanel.Controls.Add(_tabSubsBtn);
            _tabBarPanel.Controls.Add(_tabTxsBtn);

            // Action button anchored to top-right of tab bar
            _actionBtn = new Button
            {
                Text = "+ Create Subscription Plan",
                Font = new Font("Segoe UI Semibold", 9f),
                Size = new Size(220, 34),
                Location = new Point(_tabBarPanel.Width - 226, 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White
            };
            _actionBtn.FlatAppearance.BorderSize = 0;
            void RoundActionBtn()
            {
                using var path = RoundedRect(new Rectangle(0, 0, _actionBtn.Width, _actionBtn.Height), 6);
                _actionBtn.Region = new Region(path);
            }
            _actionBtn.SizeChanged += (s, e) => RoundActionBtn();
            RoundActionBtn();
            _actionBtn.Click += async (s, e) => await HandleTopActionAsync();
            _tabBarPanel.Controls.Add(_actionBtn);

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
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.Scroll += (s, e) => { CloseActiveMenu(); SetHover(-1); };
            _contentPanel.Controls.Add(_grid);

            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();

            ResumeLayout(true);
        }

        private static Panel CreateTile(string caption, string badge, Color fgColor, Color bgColor, out Label valLabel, Action? onClick = null)
        {
            var p = new Panel
            {
                BackColor = Color.White,
                Height = 96,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
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
                UseMnemonic = false,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
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
                UseMnemonic = false,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
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
                UseMnemonic = false,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
            };
            p.Controls.Add(valLabel);

            if (onClick != null)
            {
                p.Click += (s, e) => onClick();
                capLbl.Click += (s, e) => onClick();
                badgeLbl.Click += (s, e) => onClick();
                valLabel.Click += (s, e) => onClick();
            }

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
            if (_actionBtn != null)
            {
                _actionBtn.Location = new Point(_tabBarPanel.Width - _actionBtn.Width - 6, 5);
            }

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
            CloseActiveMenu();
            SetHover(-1);
            _currentTab = tab;

            // Highlight active tab button
            SetTabBtnState(_tabPlansBtn, tab == TabMode.Plans);
            SetTabBtnState(_tabSubsBtn, tab == TabMode.CustomerSubscriptions);
            SetTabBtnState(_tabTxsBtn, tab == TabMode.Transactions);

            // Update top action button
            switch (tab)
            {
                case TabMode.Plans:
                    _actionBtn.Text = "+ Create Subscription Plan";
                    _actionBtn.Width = 220;
                    _actionBtn.Visible = true;
                    break;
                case TabMode.CustomerSubscriptions:
                    _actionBtn.Text = "+ Assign / Update Plan";
                    _actionBtn.Width = 200;
                    _actionBtn.Visible = true;
                    break;
                case TabMode.Transactions:
                    _actionBtn.Text = "+ Record Payment";
                    _actionBtn.Width = 180;
                    _actionBtn.Visible = true;
                    break;
            }
            _actionBtn.Location = new Point(_tabBarPanel.Width - _actionBtn.Width - 6, 5);

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
            try
            {
                _plans = await _http.GetFromJsonAsync<List<SubscriptionPlanItemDto>>("api/billing/plans")
                         ?? new List<SubscriptionPlanItemDto>();

                _grid.SuspendLayout();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PlanId", HeaderText = "ID", Width = 55 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "PlanName",
                    HeaderText = "PLAN NAME & DETAILS",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = 180,
                    FillWeight = 120
                });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "PRICE (PHP)", Width = 115 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillingCycle", HeaderText = "BILLING CYCLE", Width = 130 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Limits", HeaderText = "LIMITS & BRANCHES", Width = 210 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 95 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActiveMembers", HeaderText = "ENROLLED", Width = 100 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Actions",
                    HeaderText = "ACTIONS",
                    Width = SuperAdminActionMenuHelper.ActionsColWidth,
                    MinimumWidth = 70,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = Color.White,
                        SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                        Padding = new Padding(0),
                        Alignment = DataGridViewContentAlignment.MiddleCenter
                    }
                });

                foreach (var p in _plans)
                {
                    var planDetails = $"{p.PlanName}\n{(string.IsNullOrWhiteSpace(p.Description) ? "Standard SaaS Tenant Plan" : p.Description)}";
                    var limits = $"{p.MaxUsers} Users • {(p.MultiBranchEnabled ? "Multi-Branch" : "Single Branch")}\nMax {p.MaxCustomers:N0} Customers";
                    var status = p.IsArchived ? "Archived" : (p.IsActive ? "Active" : "Inactive");

                    _grid.Rows.Add(
                        p.PlanId,
                        planDetails,
                        p.PriceFormatted,
                        p.BillingCycle,
                        limits,
                        status,
                        $"{p.ActiveSubscribers} active",
                        "");
                }

                _grid.ClearSelection();
                _grid.ResumeLayout();
            }
            catch (Exception ex)
            {
                _grid.ResumeLayout();
                MessageBox.Show($"Failed to load subscription plans: {ex.Message}", "Error Loading Plans", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadCustomerSubscriptionsAsync()
        {
            try
            {
                _customerSubs = await _http.GetFromJsonAsync<List<CustomerSubscriptionItemDto>>("api/billing/customer-subscriptions")
                                ?? new List<CustomerSubscriptionItemDto>();

                _grid.SuspendLayout();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SubId", HeaderText = "SUB ID", Width = 80 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Company",
                    HeaderText = "TENANT BUSINESS",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = 180
                });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Admin",
                    HeaderText = "ADMIN CONTACT",
                    Width = 190
                });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Plan", HeaderText = "CURRENT PLAN", Width = 150 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BillingCycle", HeaderText = "CYCLE", Width = 110 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StartDate", HeaderText = "START DATE", Width = 100 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EndDate", HeaderText = "RENEWAL DATE", Width = 105 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 95 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Actions",
                    HeaderText = "ACTIONS",
                    Width = SuperAdminActionMenuHelper.ActionsColWidth,
                    MinimumWidth = 70,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = Color.White,
                        SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                        Padding = new Padding(0),
                        Alignment = DataGridViewContentAlignment.MiddleCenter
                    }
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
                        s.Status,
                        "");
                }

                _grid.ClearSelection();
                _grid.ResumeLayout();
            }
            catch (Exception ex)
            {
                _grid.ResumeLayout();
                MessageBox.Show($"Failed to load tenant subscriptions: {ex.Message}", "Error Loading Subscriptions", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadTransactionsAsync()
        {
            try
            {
                _transactions = await _http.GetFromJsonAsync<List<BillingTransactionItemDto>>("api/billing/transactions")
                                ?? new List<BillingTransactionItemDto>();

                _grid.SuspendLayout();
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TxnId", HeaderText = "TXN ID", Width = 80 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Company",
                    HeaderText = "TENANT BUSINESS",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = 180
                });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Plan", HeaderText = "PLAN", Width = 140 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reference", HeaderText = "REFERENCE #", Width = 150 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "AMOUNT", Width = 110 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "DATE", Width = 135 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaymentStatus", HeaderText = "STATUS", Width = 100 });
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Actions",
                    HeaderText = "ACTIONS",
                    Width = SuperAdminActionMenuHelper.ActionsColWidth,
                    MinimumWidth = 70,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        BackColor = Color.White,
                        SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                        Padding = new Padding(0),
                        Alignment = DataGridViewContentAlignment.MiddleCenter
                    }
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
                        t.PaymentStatus,
                        "");
                }

                _grid.ClearSelection();
                _grid.ResumeLayout();
            }
            catch (Exception ex)
            {
                _grid.ResumeLayout();
                MessageBox.Show($"Failed to load billing transactions: {ex.Message}", "Error Loading Transactions", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        //  TOP ACTION & ROW CLICKS
        // ================================================================
        private async Task HandleTopActionAsync()
        {
            switch (_currentTab)
            {
                case TabMode.Plans:
                    OpenCreatePlanDialog();
                    break;
                case TabMode.CustomerSubscriptions:
                    OpenAssignPlanDialog();
                    break;
                case TabMode.Transactions:
                    OpenRecordPaymentDialog();
                    break;
            }
            await Task.CompletedTask;
        }

        private void OpenCreatePlanDialog()
        {
            using var dlg = new CreateTenantPlanDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = LoadSummaryAsync();
                _ = LoadPlansAsync();
            }
        }

        private void OpenAssignPlanDialog(CustomerSubscriptionItemDto? specificItem = null)
        {
            CustomerSubscriptionItemDto? target = specificItem;
            if (target == null && _grid.CurrentRow != null && _grid.CurrentRow.Index >= 0)
            {
                var idVal = _grid.CurrentRow.Cells["SubId"]?.Value?.ToString();
                if (int.TryParse(idVal, out var subId))
                {
                    target = _customerSubs.FirstOrDefault(s => s.SubscriptionId == subId);
                }
            }

            if (target == null && _customerSubs.Count > 0)
            {
                target = _customerSubs[0];
            }

            if (target == null)
            {
                MessageBox.Show("No tenant companies found to assign a plan to.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new AssignTenantPlanDialog(target);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = LoadSummaryAsync();
                _ = LoadCustomerSubscriptionsAsync();
            }
        }

        private void OpenRecordPaymentDialog(BillingTransactionItemDto? specificTx = null)
        {
            BillingTransactionItemDto? targetTx = specificTx;
            if (targetTx == null && _grid.CurrentRow != null && _grid.CurrentRow.Index >= 0)
            {
                var txVal = _grid.CurrentRow.Cells["TxnId"]?.Value?.ToString();
                if (int.TryParse(txVal, out var txId))
                {
                    targetTx = _transactions.FirstOrDefault(t => t.TransactionId == txId);
                }
            }

            if (targetTx == null && _transactions.Count > 0)
            {
                targetTx = _transactions.FirstOrDefault(t => !t.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                           ?? _transactions[0];
            }

            if (targetTx == null)
            {
                MessageBox.Show("No billing transactions found.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (targetTx.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"Transaction #{targetTx.TransactionId} is already settled as Paid.", "Already Settled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new MarkPaidDialog(targetTx);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = LoadSummaryAsync();
                _ = LoadTransactionsAsync();
            }
        }

        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                _grid.Columns[e.ColumnIndex].Name != "Actions")
            {
                SetHover(-1);
                return;
            }

            SetHover(SuperAdminActionMenuHelper.HitTestActions(_grid, e.RowIndex, e.Location));
        }

        private void SetHover(int encoded)
        {
            if (encoded == _hoverAction) return;

            int old = _hoverAction;
            _hoverAction = encoded;

            var col = _grid.Columns["Actions"];
            if (col != null)
            {
                if (old >= 0 && (old >> 2) < _grid.RowCount) _grid.InvalidateCell(col.Index, old >> 2);
                if (encoded >= 0 && (encoded >> 2) < _grid.RowCount) _grid.InvalidateCell(col.Index, encoded >> 2);
            }

            _grid.Cursor = encoded >= 0 ? Cursors.Hand : Cursors.Default;
        }

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

            int hit = SuperAdminActionMenuHelper.HitTestActions(_grid, e.RowIndex, e.Location);
            if (hit < 0) return;

            if (_currentTab == TabMode.Plans)
            {
                var idVal = _grid.Rows[e.RowIndex].Cells["PlanId"].Value?.ToString();
                if (!int.TryParse(idVal, out var planId)) return;
                var plan = _plans.FirstOrDefault(p => p.PlanId == planId);
                if (plan == null) return;

                ShowPlanActionsMenu(e.RowIndex, plan);
            }
            else if (_currentTab == TabMode.CustomerSubscriptions)
            {
                var idVal = _grid.Rows[e.RowIndex].Cells["SubId"].Value?.ToString();
                if (!int.TryParse(idVal, out var subId)) return;
                var sub = _customerSubs.FirstOrDefault(s => s.SubscriptionId == subId);
                if (sub == null) return;

                ShowCustomerSubActionsMenu(e.RowIndex, sub);
            }
            else if (_currentTab == TabMode.Transactions)
            {
                var idVal = _grid.Rows[e.RowIndex].Cells["TxnId"].Value?.ToString();
                if (!int.TryParse(idVal, out var txId)) return;
                var tx = _transactions.FirstOrDefault(t => t.TransactionId == txId);
                if (tx == null) return;

                ShowTransactionActionsMenu(e.RowIndex, tx);
            }
        }

        private void ShowPlanActionsMenu(int rowIndex, SubscriptionPlanItemDto plan)
        {
            CloseActiveMenu();

            var menu = SuperAdminActionMenuHelper.CreateMenu();

            SuperAdminActionMenuHelper.AddMenuItem(menu, "Edit", () =>
            {
                using var dlg = new CreateTenantPlanDialog(plan);
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _ = LoadSummaryAsync();
                    _ = LoadPlansAsync();
                }
            });

            string toggleText = plan.IsActive ? "Deactivate" : "Activate";
            SuperAdminActionMenuHelper.AddMenuItem(
                menu,
                toggleText,
                () => { _ = TogglePlanStatusAsync(plan); },
                isDanger: plan.IsActive);

            string archText = plan.IsArchived ? "Restore" : "Archive";
            SuperAdminActionMenuHelper.AddMenuItem(
                menu,
                archText,
                () => { _ = TogglePlanArchiveAsync(plan); },
                isDanger: !plan.IsArchived);

            _activeMenu = menu;
            _menuRow = rowIndex;

            menu.Closed += (s, e) =>
            {
                if (ReferenceEquals(_activeMenu, menu))
                {
                    _activeMenu = null;
                    _menuRow = -1;
                    var col = _grid.Columns["Actions"];
                    if (col != null && rowIndex >= 0 && rowIndex < _grid.RowCount)
                        _grid.InvalidateCell(col.Index, rowIndex);
                }
            };

            SuperAdminActionMenuHelper.ShowMenu(menu, _grid, rowIndex);
        }

        private void ShowCustomerSubActionsMenu(int rowIndex, CustomerSubscriptionItemDto sub)
        {
            CloseActiveMenu();

            var menu = SuperAdminActionMenuHelper.CreateMenu();

            SuperAdminActionMenuHelper.AddMenuItem(menu, "Change Plan", () => OpenAssignPlanDialog(sub));

            _activeMenu = menu;
            _menuRow = rowIndex;

            menu.Closed += (s, e) =>
            {
                if (ReferenceEquals(_activeMenu, menu))
                {
                    _activeMenu = null;
                    _menuRow = -1;
                    var col = _grid.Columns["Actions"];
                    if (col != null && rowIndex >= 0 && rowIndex < _grid.RowCount)
                        _grid.InvalidateCell(col.Index, rowIndex);
                }
            };

            SuperAdminActionMenuHelper.ShowMenu(menu, _grid, rowIndex);
        }

        private void ShowTransactionActionsMenu(int rowIndex, BillingTransactionItemDto tx)
        {
            CloseActiveMenu();

            var menu = SuperAdminActionMenuHelper.CreateMenu();

            bool isPaid = tx.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase);
            if (!isPaid)
            {
                SuperAdminActionMenuHelper.AddMenuItem(menu, "Record Payment", () => OpenRecordPaymentDialog(tx));
            }
            else
            {
                SuperAdminActionMenuHelper.AddMenuItem(menu, "Settled ✓", () => { }, isEnabled: false);
            }

            _activeMenu = menu;
            _menuRow = rowIndex;

            menu.Closed += (s, e) =>
            {
                if (ReferenceEquals(_activeMenu, menu))
                {
                    _activeMenu = null;
                    _menuRow = -1;
                    var col = _grid.Columns["Actions"];
                    if (col != null && rowIndex >= 0 && rowIndex < _grid.RowCount)
                        _grid.InvalidateCell(col.Index, rowIndex);
                }
            };

            SuperAdminActionMenuHelper.ShowMenu(menu, _grid, rowIndex);
        }

        private void CloseActiveMenu()
        {
            if (_activeMenu == null) return;
            var m = _activeMenu;
            _activeMenu = null;
            m.Close();
            if (_menuRow >= 0)
            {
                var col = _grid.Columns["Actions"];
                if (col != null && _menuRow < _grid.RowCount)
                    _grid.InvalidateCell(col.Index, _menuRow);
                _menuRow = -1;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseActiveMenu();
            }
            base.Dispose(disposing);
        }

        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (_currentTab == TabMode.Plans)
            {
                var idVal = _grid.Rows[e.RowIndex].Cells["PlanId"].Value?.ToString();
                if (int.TryParse(idVal, out var planId))
                {
                    var plan = _plans.FirstOrDefault(p => p.PlanId == planId);
                    if (plan != null)
                    {
                        using var dlg = new CreateTenantPlanDialog(plan);
                        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                        {
                            _ = LoadSummaryAsync();
                            _ = LoadPlansAsync();
                        }
                    }
                }
            }
            else if (_currentTab == TabMode.CustomerSubscriptions)
            {
                var idVal = _grid.Rows[e.RowIndex].Cells["SubId"].Value?.ToString();
                if (int.TryParse(idVal, out var subId))
                {
                    var sub = _customerSubs.FirstOrDefault(s => s.SubscriptionId == subId);
                    if (sub != null) OpenAssignPlanDialog(sub);
                }
            }
            else if (_currentTab == TabMode.Transactions)
            {
                var idVal = _grid.Rows[e.RowIndex].Cells["TxnId"].Value?.ToString();
                if (int.TryParse(idVal, out var txId))
                {
                    var tx = _transactions.FirstOrDefault(t => t.TransactionId == txId);
                    if (tx != null && !tx.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenRecordPaymentDialog(tx);
                    }
                }
            }
        }

        private async Task TogglePlanStatusAsync(SubscriptionPlanItemDto plan)
        {
            if (plan.IsArchived)
            {
                MessageBox.Show("Archived plans cannot be toggled. Please restore the plan first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string action = plan.IsActive ? "deactivate" : "activate";
            var confirm = MessageBox.Show(
                $"Are you sure you want to {action} the plan '{plan.PlanName}'?\n\n" +
                (plan.IsActive
                    ? "New businesses will not be able to choose this plan while it is inactive."
                    : "This plan will become immediately available for new and existing tenants."),
                $"{char.ToUpper(action[0]) + action.Substring(1)} Plan",
                MessageBoxButtons.YesNo,
                plan.IsActive ? MessageBoxIcon.Warning : MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var res = await _http.PutAsJsonAsync($"api/billing/plans/{plan.PlanId}/status", new { isActive = !plan.IsActive });
                if (res.IsSuccessStatusCode)
                {
                    await LoadSummaryAsync();
                    await LoadPlansAsync();
                }
                else
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to update plan status: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Network error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task TogglePlanArchiveAsync(SubscriptionPlanItemDto plan)
        {
            string action = plan.IsArchived ? "restore" : "archive";
            var confirm = MessageBox.Show(
                $"Are you sure you want to {action} the plan '{plan.PlanName}'?\n\n" +
                (plan.IsArchived
                    ? "This will unarchive the plan so it can be managed and activated."
                    : "Archived plans are retired and automatically deactivated. Existing subscriptions remain intact."),
                $"{char.ToUpper(action[0]) + action.Substring(1)} Plan",
                MessageBoxButtons.YesNo,
                plan.IsArchived ? MessageBoxIcon.Question : MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var res = await _http.PutAsync($"api/billing/plans/{plan.PlanId}/archive", null);
                if (res.IsSuccessStatusCode)
                {
                    await LoadSummaryAsync();
                    await LoadPlansAsync();
                }
                else
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to update archive status: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Network error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        //  CELL PAINTING (Pills, Avatars, Actions & Limits)
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
                case "PlanName":
                    PaintPlanCell(e);
                    break;
                case "Limits":
                    PaintLimitsCell(e);
                    break;
                case "Actions":
                    bool hover = _hoverAction == (e.RowIndex << 2);
                    SuperAdminActionMenuHelper.PaintActionsCell(e, hover);
                    break;
            }
        }

        private void PaintLimitsCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var line1 = parts[0];
            var line2 = parts.Length > 1 ? parts[1] : "";

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
            int top = b.Y + (b.Height - (h1 + (string.IsNullOrEmpty(line2) ? 0 : h2 + gap))) / 2;

            TextRenderer.DrawText(e.Graphics, line1, FontName,
                new Rectangle(x, top, tw, h1), Navy, flags);

            if (!string.IsNullOrEmpty(line2))
            {
                TextRenderer.DrawText(e.Graphics, line2, FontSub,
                    new Rectangle(x, top + h1 + gap, tw, h2), Muted, flags);
            }

            e.Handled = true;
        }

        private void PaintPlanCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var name = parts[0];
            var priceOrDesc = parts.Length > 1 ? parts[1] : "";

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
            int top = b.Y + (b.Height - (h1 + (string.IsNullOrEmpty(priceOrDesc) ? 0 : h2 + gap))) / 2;

            TextRenderer.DrawText(e.Graphics, name, FontName,
                new Rectangle(x, top, tw, h1), Navy, flags);

            if (!string.IsNullOrEmpty(priceOrDesc))
            {
                TextRenderer.DrawText(e.Graphics, priceOrDesc, FontSub,
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
                bg = RoyalBlueSoft;
                fg = RoyalBlue;
            }
            else if (text.IndexOf("Quarter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bg = DeepNavySoft;
                fg = DeepNavy;
            }
            else
            {
                bg = BlueSoft;
                fg = Blue;
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
                string.Equals(text, "Paid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                bg = BlueSoft;
                fg = DeepNavy;
            }
            else if (string.Equals(text, "Pending", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(text, "Archived", StringComparison.OrdinalIgnoreCase))
            {
                bg = SlateBlueSoft;
                fg = SlateBlue;
            }
            else
            {
                bg = RoyalBlueSoft;
                fg = RoyalBlue;
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
