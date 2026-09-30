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
    /// Super Admin Module: Business Intelligence (Platform-Level SaaS Analytics).
    /// Displays platform overview, tenant growth, subscription lifecycle, and CRM recurring revenue.
    /// Strictly excludes carwash operational details (washes, customer visits, menu services).
    /// </summary>
    public class SuperAdminBusinessIntelligenceView : UserControl
    {
        // Colors & Palette matching CRM UI
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBg = Color.White;
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color AccentBlue = Color.FromArgb(0x02, 0x84, 0xC7);
        private static readonly Color Green = Color.FromArgb(0x10, 0xB9, 0x81);
        private static readonly Color GreenBg = Color.FromArgb(0xEC, 0xFD, 0xF5);
        private static readonly Color GreenBorder = Color.FromArgb(0xA7, 0xF3, 0xD0);
        private static readonly Color Indigo = Color.FromArgb(0x63, 0x66, 0xF1);
        private static readonly Color Purple = Color.FromArgb(0x8B, 0x5C, 0xF6);
        private static readonly Color Amber = Color.FromArgb(0xF5, 0x9E, 0x0B);
        private static readonly Color Red = Color.FromArgb(0xEF, 0x44, 0x44);

        // Fonts
        private static readonly Font FontPageTitle = new("Segoe UI Semibold", 18f);
        private static readonly Font FontSubtitle = new("Segoe UI", 9.2f);
        private static readonly Font FontCardTitle = new("Segoe UI Semibold", 11.5f);
        private static readonly Font FontCardSub = new("Segoe UI", 8.8f);
        private static readonly Font FontKpiValue = new("Segoe UI Semibold", 19f);
        private static readonly Font FontKpiSub = new("Segoe UI", 8.5f);
        private static readonly Font FontSectionTag = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontGridHeader = new("Segoe UI Semibold", 9f);
        private static readonly Font FontGridCell = new("Segoe UI", 9.2f);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private PlatformAnalyticsDto _data = new();
        private bool _loading;

        // Top UI Controls
        private Panel _rootScrollPanel = null!;
        private Label _statusLbl = null!;
        private Button _refreshBtn = null!;

        // KPI Panels
        private KpiTile _kpiTenants = null!;
        private KpiTile _kpiSubscriptions = null!;
        private KpiTile _kpiRevenue = null!;
        private KpiTile _kpiUsers = null!;
        private KpiTile _kpiBranches = null!;

        // Tenant & Subscription Analytics Panels
        private TenantAnalyticsCard _tenantCard = null!;
        private SubscriptionAnalyticsCard _subscriptionCard = null!;

        // Billing Analytics Panels
        private BillingAnalyticsCard _billingCard = null!;

        // Recent Activity Grid
        private DataGridView _activityGrid = null!;

        public SuperAdminBusinessIntelligenceView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            InitializeComponent();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await ReloadAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _http.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // Root Scrollable Panel
            _rootScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PageBg,
                Padding = new Padding(28, 20, 28, 30)
            };
            Controls.Add(_rootScrollPanel);

            int curY = 16;
            const int ContentWidth = 1140;

            // ============================================================
            // 1. HEADER ROW (Title + Subtitle + Refresh)
            // ============================================================
            var headerRow = new Panel
            {
                Location = new Point(28, curY),
                Size = new Size(ContentWidth, 68),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var titleLbl = new Label
            {
                Text = "Business Intelligence",
                Font = FontPageTitle,
                ForeColor = Navy,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            headerRow.Controls.Add(titleLbl);

            var subtitleLbl = new Label
            {
                Text = "Platform-level SaaS analytics: tenant growth, active subscriptions, and recurring subscription revenue.",
                Font = FontSubtitle,
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(2, 34)
            };
            headerRow.Controls.Add(subtitleLbl);

            _statusLbl = new Label
            {
                Text = "Loading platform metrics...",
                Font = FontSubtitle,
                ForeColor = Muted,
                AutoSize = false,
                Size = new Size(220, 28),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerRow.Width - 340, 16)
            };
            headerRow.Controls.Add(_statusLbl);

            _refreshBtn = new Button
            {
                Text = "⟳ Refresh",
                Font = new Font("Segoe UI Semibold", 9.2f),
                ForeColor = AccentBlue,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(headerRow.Width - 105, 12)
            };
            _refreshBtn.FlatAppearance.BorderColor = CardBorder;
            _refreshBtn.FlatAppearance.BorderSize = 1;
            _refreshBtn.Click += async (s, e) => await ReloadAsync();
            headerRow.Controls.Add(_refreshBtn);

            _rootScrollPanel.Controls.Add(headerRow);
            curY += 76;

            // ============================================================
            // 2. OVERVIEW KPI CARDS (5 Horizontal Cards)
            // ============================================================
            var kpiRow = new TableLayoutPanel
            {
                Location = new Point(28, curY),
                Size = new Size(ContentWidth, 100),
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            for (int i = 0; i < 5; i++)
            {
                kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            }

            _kpiTenants = new KpiTile("Total Tenants", "0", "Active: 0 · Inactive: 0", AccentBlue, "🏢");
            _kpiSubscriptions = new KpiTile("Active Subscriptions", "0", "0 expiring soon", Green, "✓");
            _kpiRevenue = new KpiTile("Subscription Revenue", "₱0.00", "0 paid transactions", Green, "₱");
            _kpiUsers = new KpiTile("Platform Users", "0", "Across all tenants", Indigo, "👥");
            _kpiBranches = new KpiTile("Total Branches", "0", "Multi-branch locations", Purple, "📍");

            kpiRow.Controls.Add(_kpiTenants, 0, 0);
            kpiRow.Controls.Add(_kpiSubscriptions, 1, 0);
            kpiRow.Controls.Add(_kpiRevenue, 2, 0);
            kpiRow.Controls.Add(_kpiUsers, 3, 0);
            kpiRow.Controls.Add(_kpiBranches, 4, 0);

            _rootScrollPanel.Controls.Add(kpiRow);
            curY += 114;

            // ============================================================
            // 3. ROW 2: TENANT ANALYTICS & SUBSCRIPTION ANALYTICS (2 COLUMNS)
            // ============================================================
            var row2 = new TableLayoutPanel
            {
                Location = new Point(28, curY),
                Size = new Size(ContentWidth, 360),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            _tenantCard = new TenantAnalyticsCard();
            _subscriptionCard = new SubscriptionAnalyticsCard();

            row2.Controls.Add(_tenantCard, 0, 0);
            row2.Controls.Add(_subscriptionCard, 1, 0);

            _rootScrollPanel.Controls.Add(row2);
            curY += 374;

            // ============================================================
            // 4. ROW 3: BILLING & REVENUE ANALYTICS
            // ============================================================
            _billingCard = new BillingAnalyticsCard
            {
                Location = new Point(28, curY),
                Size = new Size(ContentWidth, 380),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _rootScrollPanel.Controls.Add(_billingCard);
            curY += 396;

            // ============================================================
            // 5. ROW 4: RECENT SUBSCRIPTION ACTIVITY TABLE
            // ============================================================
            var activityCard = new Panel
            {
                Location = new Point(28, curY),
                Size = new Size(ContentWidth, 320),
                BackColor = CardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(20)
            };
            activityCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var p = new Pen(CardBorder, 1f);
                using var path = RoundedRect(new Rectangle(0, 0, activityCard.Width - 1, activityCard.Height - 1), 10);
                e.Graphics.DrawPath(p, path);
            };

            var activityTitle = new Label
            {
                Text = "Recent Subscription Activity",
                Font = FontCardTitle,
                ForeColor = Navy,
                AutoSize = true,
                Location = new Point(20, 16)
            };
            activityCard.Controls.Add(activityTitle);

            var activitySub = new Label
            {
                Text = "Latest tenant subscription billing transactions and renewals.",
                Font = FontCardSub,
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(22, 38)
            };
            activityCard.Controls.Add(activitySub);

            _activityGrid = new DataGridView
            {
                Location = new Point(20, 68),
                Size = new Size(activityCard.Width - 40, activityCard.Height - 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(0xEE, 0xF2, 0xF6),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 36,
                RowTemplate = { Height = 40 }
            };

            _activityGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
                ForeColor = Muted,
                Font = FontGridHeader,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            _activityGrid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Navy,
                Font = FontGridCell,
                SelectionBackColor = Color.FromArgb(0xF0, 0xF9, 0xFF),
                SelectionForeColor = Navy,
                Padding = new Padding(12, 0, 0, 0)
            };

            SetupActivityGridColumns();
            activityCard.Controls.Add(_activityGrid);

            _rootScrollPanel.Controls.Add(activityCard);
            curY += 340;

            // Extra bottom spacer
            var spacer = new Panel
            {
                Location = new Point(28, curY),
                Size = new Size(ContentWidth, 20),
                BackColor = Color.Transparent
            };
            _rootScrollPanel.Controls.Add(spacer);

            ResumeLayout(false);
        }

        private void SetupActivityGridColumns()
        {
            _activityGrid.Columns.Clear();
            _activityGrid.AutoGenerateColumns = false;

            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "TX ID",
                DataPropertyName = "TransactionId",
                Width = 80
            });
            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "TENANT BUSINESS",
                DataPropertyName = "CompanyName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 26f
            });
            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "PLAN",
                DataPropertyName = "PlanName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 24f
            });
            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "AMOUNT",
                DataPropertyName = "AmountFormatted",
                Width = 130
            });
            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "PAYMENT METHOD",
                DataPropertyName = "PaymentMethod",
                Width = 150
            });
            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "STATUS",
                DataPropertyName = "PaymentStatus",
                Width = 120
            });
            _activityGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "DATE",
                DataPropertyName = "DateFormatted",
                Width = 140
            });

            _activityGrid.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 5 && e.Value != null)
                {
                    e.PaintBackground(e.ClipBounds, true);
                    string status = e.Value.ToString() ?? "";
                    Color bg = status is "Completed" or "Paid" ? Color.FromArgb(0xDC, 0xFC, 0xE7) : Color.FromArgb(0xFE, 0xF3, 0xC7);
                    Color fg = status is "Completed" or "Paid" ? Color.FromArgb(0x16, 0x65, 0x34) : Color.FromArgb(0x92, 0x40, 0x0E);

                    var pillRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + (e.CellBounds.Height - 22) / 2, 88, 22);
                    using var pBrush = new SolidBrush(bg);
                    using var path = RoundedRect(pillRect, 6);
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(pBrush, path);

                    TextRenderer.DrawText(e.Graphics, status, FontSectionTag, pillRect, fg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    e.Handled = true;
                }
            };
        }

        // ================================================================
        //  DATA RELOAD
        // ================================================================
        public async Task ReloadAsync()
        {
            if (_loading) return;
            _loading = true;

            try
            {
                _refreshBtn.Enabled = false;
                _refreshBtn.Text = "Loading...";
                _statusLbl.Text = "Fetching platform analytics...";

                var result = await _http.GetFromJsonAsync<PlatformAnalyticsDto>("api/analytics/platform");
                if (result != null)
                {
                    _data = result;
                    RenderData();
                    _statusLbl.Text = $"Updated {DateTime.Now:h:mm tt}";
                }
                else
                {
                    _statusLbl.Text = "No data returned";
                }
            }
            catch (Exception ex)
            {
                _statusLbl.Text = "Error loading metrics";
                MessageBox.Show($"Failed to load platform analytics:\n{ex.Message}", "Platform Analytics Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _loading = false;
                _refreshBtn.Enabled = true;
                _refreshBtn.Text = "⟳ Refresh";
            }
        }

        private void RenderData()
        {
            // 1. Overview KPIs
            var ov = _data.PlatformOverview;
            _kpiTenants.SetValue(ov.TotalTenants.ToString(), $"Active: {ov.ActiveTenants} · Inactive: {ov.InactiveTenants}");
            _kpiSubscriptions.SetValue(ov.TotalActiveSubscriptions.ToString(), $"{_data.SubscriptionAnalytics.ExpiringSubscriptions} expiring soon");
            _kpiRevenue.SetValue(ov.TotalRevenueFormatted, $"{_data.BillingAnalytics.PaidBillingTransactions} paid transactions");
            _kpiUsers.SetValue(ov.TotalRegisteredUsers.ToString(), "Across all tenants");
            _kpiBranches.SetValue(ov.TotalBranches.ToString(), "Multi-branch locations");

            // 2. Tenant Analytics Card
            _tenantCard.SetData(_data.TenantAnalytics);

            // 3. Subscription Analytics Card
            _subscriptionCard.SetData(_data.SubscriptionAnalytics);

            // 4. Billing Analytics Card
            _billingCard.SetData(_data.BillingAnalytics);

            // 5. Activity Grid
            _activityGrid.DataSource = null;
            if (_data.RecentActivity.Count > 0)
            {
                _activityGrid.DataSource = _data.RecentActivity;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y + bounds.Height - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Y + bounds.Height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ================================================================
        //  SUB-CONTROLS
        // ================================================================

        /// <summary>
        /// Single KPI Tile
        /// </summary>
        private sealed class KpiTile : Panel
        {
            private readonly string _title;
            private string _value;
            private string _sub;
            private readonly Color _accent;
            private readonly string _iconText;

            public KpiTile(string title, string value, string sub, Color accent, string iconText)
            {
                _title = title;
                _value = value;
                _sub = sub;
                _accent = accent;
                _iconText = iconText;

                Dock = DockStyle.Fill;
                Margin = new Padding(0, 0, 14, 0);
                DoubleBuffered = true;
            }

            public void SetValue(string value, string sub)
            {
                _value = value;
                _sub = sub;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var bgBrush = new SolidBrush(CardBg))
                using (var path = RoundedRect(bounds, 10))
                {
                    g.FillPath(bgBrush, path);
                    using var p = new Pen(CardBorder, 1f);
                    g.DrawPath(p, path);
                }

                // Accent top line
                using (var ab = new SolidBrush(_accent))
                {
                    g.FillRectangle(ab, 12, 1, Math.Max(20, Width - 24), 3);
                }

                // Title
                TextRenderer.DrawText(g, _title, FontCardSub, new Point(14, 14), Muted, TextFormatFlags.NoPadding);

                // Value
                TextRenderer.DrawText(g, _value, FontKpiValue, new Point(13, 34), Navy, TextFormatFlags.NoPadding);

                // Sub
                TextRenderer.DrawText(g, _sub, FontKpiSub, new Point(14, 68), Muted, TextFormatFlags.NoPadding);

                // Icon circle right
                int iconD = 32;
                var iconRect = new Rectangle(Width - iconD - 14, 18, iconD, iconD);
                using (var circleBrush = new SolidBrush(Color.FromArgb(0xF0, 0xF5, 0xFA)))
                {
                    g.FillEllipse(circleBrush, iconRect);
                }
                using (var iconFont = new Font("Segoe UI Semibold", 11f))
                {
                    TextRenderer.DrawText(g, _iconText, iconFont, iconRect, _accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        /// <summary>
        /// Tenant Analytics Card: Total, Active, Inactive, New, Growth chart
        /// </summary>
        private sealed class TenantAnalyticsCard : Panel
        {
            private TenantAnalyticsDto _data = new();

            public TenantAnalyticsCard()
            {
                Dock = DockStyle.Fill;
                Margin = new Padding(0, 0, 10, 0);
                BackColor = CardBg;
                DoubleBuffered = true;
            }

            public void SetData(TenantAnalyticsDto data)
            {
                _data = data;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var bgBrush = new SolidBrush(CardBg))
                using (var path = RoundedRect(bounds, 10))
                {
                    g.FillPath(bgBrush, path);
                    using var p = new Pen(CardBorder, 1f);
                    g.DrawPath(p, path);
                }

                // Header
                TextRenderer.DrawText(g, "Tenant Analytics", FontCardTitle, new Point(20, 16), Navy, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "Tenant portfolio health and onboarding growth across the platform.", FontCardSub, new Point(21, 38), Muted, TextFormatFlags.NoPadding);

                // 4 Sub-metrics pills
                int tileW = (Width - 40 - 24) / 4;
                int tileY = 64;
                int tileH = 58;

                DrawMetricPill(g, new Rectangle(20, tileY, tileW, tileH), "Total Tenants", _data.TotalTenants.ToString(), AccentBlue);
                DrawMetricPill(g, new Rectangle(20 + tileW + 8, tileY, tileW, tileH), "Active", _data.ActiveTenants.ToString(), Green);
                DrawMetricPill(g, new Rectangle(20 + (tileW + 8) * 2, tileY, tileW, tileH), "Inactive", _data.InactiveTenants.ToString(), _data.InactiveTenants > 0 ? Red : Muted);
                DrawMetricPill(g, new Rectangle(20 + (tileW + 8) * 3, tileY, tileW, tileH), "New (30d)", _data.NewlyRegisteredTenants.ToString(), Indigo);

                // Tenant Status ratio bar
                int barY = 138;
                TextRenderer.DrawText(g, "Tenant Status Distribution", FontSectionTag, new Point(20, barY), Navy, TextFormatFlags.NoPadding);

                int barW = Width - 40;
                int barH = 14;
                int barTop = barY + 22;

                int total = Math.Max(1, _data.TotalTenants);
                float activeRatio = (float)_data.ActiveTenants / total;
                int activeW = (int)(barW * activeRatio);

                using (var bgBar = new SolidBrush(Color.FromArgb(0xEE, 0xF2, 0xF6)))
                using (var path = RoundedRect(new Rectangle(20, barTop, barW, barH), 4))
                {
                    g.FillPath(bgBar, path);
                }

                if (activeW > 0)
                {
                    using (var actBar = new SolidBrush(Green))
                    using (var path = RoundedRect(new Rectangle(20, barTop, activeW, barH), 4))
                    {
                        g.FillPath(actBar, path);
                    }
                }

                TextRenderer.DrawText(g, $"Active: {_data.ActiveTenants} ({activeRatio * 100:0.#}%)", FontKpiSub, new Point(20, barTop + 18), Green, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, $"Inactive: {_data.InactiveTenants}", FontKpiSub, new Point(Width - 110, barTop + 18), Muted, TextFormatFlags.NoPadding);

                // Tenant Growth Chart
                int chartY = barTop + 46;
                TextRenderer.DrawText(g, "Tenant Growth by Registration Period", FontSectionTag, new Point(20, chartY), Navy, TextFormatFlags.NoPadding);

                var chartRect = new Rectangle(20, chartY + 22, Width - 40, Height - chartY - 34);
                DrawBarChart(g, chartRect, _data.TenantGrowth.Select(x => (x.Period, (double)x.Count)).ToList(), AccentBlue, "tenants");
            }

            private static void DrawMetricPill(Graphics g, Rectangle r, string label, string val, Color col)
            {
                using var b = new SolidBrush(Color.FromArgb(0xF8, 0xFA, 0xFD));
                using var p = new Pen(CardBorder, 1f);
                using var path = RoundedRect(r, 6);
                g.FillPath(b, path);
                g.DrawPath(p, path);

                TextRenderer.DrawText(g, label, FontKpiSub, new Point(r.X + 8, r.Y + 8), Muted, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, val, FontKpiValue, new Point(r.X + 8, r.Y + 24), col, TextFormatFlags.NoPadding);
            }
        }

        /// <summary>
        /// Subscription Analytics Card: Plan distribution, status distribution, lifecycle
        /// </summary>
        private sealed class SubscriptionAnalyticsCard : Panel
        {
            private SubscriptionAnalyticsDto _data = new();

            public SubscriptionAnalyticsCard()
            {
                Dock = DockStyle.Fill;
                Margin = new Padding(10, 0, 0, 0);
                BackColor = CardBg;
                DoubleBuffered = true;
            }

            public void SetData(SubscriptionAnalyticsDto data)
            {
                _data = data;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var bgBrush = new SolidBrush(CardBg))
                using (var path = RoundedRect(bounds, 10))
                {
                    g.FillPath(bgBrush, path);
                    using var p = new Pen(CardBorder, 1f);
                    g.DrawPath(p, path);
                }

                // Header
                TextRenderer.DrawText(g, "Subscription Analytics", FontCardTitle, new Point(20, 16), Navy, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "Active plan distribution, renewal lifecycle, and subscription status.", FontCardSub, new Point(21, 38), Muted, TextFormatFlags.NoPadding);

                // 3 Sub-metrics pills
                int tileW = (Width - 40 - 16) / 3;
                int tileY = 64;
                int tileH = 58;

                DrawMetricPill(g, new Rectangle(20, tileY, tileW, tileH), "Active Subscriptions", _data.TotalActiveSubscriptions.ToString(), Green);
                DrawMetricPill(g, new Rectangle(20 + tileW + 8, tileY, tileW, tileH), "Expiring (<30 Days)", _data.ExpiringSubscriptions.ToString(), _data.ExpiringSubscriptions > 0 ? Amber : Muted);
                DrawMetricPill(g, new Rectangle(20 + (tileW + 8) * 2, tileY, tileW, tileH), "Expired", _data.ExpiredSubscriptions.ToString(), _data.ExpiredSubscriptions > 0 ? Red : Muted);

                // Subscriptions by Plan (Horizontal bars)
                int listY = 138;
                TextRenderer.DrawText(g, "Subscriptions by Plan", FontSectionTag, new Point(20, listY), Navy, TextFormatFlags.NoPadding);

                int curY = listY + 26;
                int totalActive = Math.Max(1, _data.TotalActiveSubscriptions);
                Color[] planColors = { AccentBlue, Indigo, Purple, Green, Amber };

                int idx = 0;
                foreach (var plan in _data.SubscriptionsByPlan)
                {
                    if (curY + 36 > Height - 10) break;

                    Color c = planColors[idx % planColors.Length];
                    float ratio = (float)plan.Count / totalActive;

                    // Plan name + price
                    TextRenderer.DrawText(g, plan.PlanName, FontGridCell, new Point(20, curY), Navy, TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, $"{plan.Count} active ({plan.PriceFormatted}/{plan.BillingCycle})", FontKpiSub,
                        new Point(Width - 190, curY + 2), Muted, TextFormatFlags.NoPadding);

                    // Bar
                    int maxBarW = Width - 40;
                    int barW = Math.Max(6, (int)(maxBarW * ratio));
                    using (var barBg = new SolidBrush(Color.FromArgb(0xEE, 0xF2, 0xF6)))
                    using (var pathBg = RoundedRect(new Rectangle(20, curY + 18, maxBarW, 8), 3))
                    {
                        g.FillPath(barBg, pathBg);
                    }
                    using (var barFg = new SolidBrush(c))
                    using (var pathFg = RoundedRect(new Rectangle(20, curY + 18, barW, 8), 3))
                    {
                        g.FillPath(barFg, pathFg);
                    }

                    curY += 36;
                    idx++;
                }

                if (_data.SubscriptionsByPlan.Count == 0)
                {
                    TextRenderer.DrawText(g, "No active plans configured.", FontCardSub, new Point(20, curY + 10), Muted, TextFormatFlags.NoPadding);
                }
            }

            private static void DrawMetricPill(Graphics g, Rectangle r, string label, string val, Color col)
            {
                using var b = new SolidBrush(Color.FromArgb(0xF8, 0xFA, 0xFD));
                using var p = new Pen(CardBorder, 1f);
                using var path = RoundedRect(r, 6);
                g.FillPath(b, path);
                g.DrawPath(p, path);

                TextRenderer.DrawText(g, label, FontKpiSub, new Point(r.X + 8, r.Y + 8), Muted, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, val, FontKpiValue, new Point(r.X + 8, r.Y + 24), col, TextFormatFlags.NoPadding);
            }
        }

        /// <summary>
        /// Billing Analytics Card: Total Revenue, Paid, Outstanding, Overdue, Revenue by Month, Revenue by Plan
        /// </summary>
        private sealed class BillingAnalyticsCard : Panel
        {
            private BillingAnalyticsDto _data = new();

            public BillingAnalyticsCard()
            {
                BackColor = CardBg;
                DoubleBuffered = true;
            }

            public void SetData(BillingAnalyticsDto data)
            {
                _data = data;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var bgBrush = new SolidBrush(CardBg))
                using (var path = RoundedRect(bounds, 10))
                {
                    g.FillPath(bgBrush, path);
                    using var p = new Pen(CardBorder, 1f);
                    g.DrawPath(p, path);
                }

                // Header
                TextRenderer.DrawText(g, "Subscription Billing & Revenue Analytics", FontCardTitle, new Point(20, 16), Navy, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "Platform subscription revenue performance, payment settlements, and plan revenue contribution.",
                    FontCardSub, new Point(21, 38), Muted, TextFormatFlags.NoPadding);

                // 4 Sub-metrics pills
                int tileW = (Width - 40 - 24) / 4;
                int tileY = 64;
                int tileH = 58;

                DrawMetricPill(g, new Rectangle(20, tileY, tileW, tileH), "Total Revenue", _data.TotalSubscriptionRevenueFormatted, Green);
                DrawMetricPill(g, new Rectangle(20 + tileW + 8, tileY, tileW, tileH), "Paid Transactions", _data.PaidBillingTransactions.ToString(), AccentBlue);
                DrawMetricPill(g, new Rectangle(20 + (tileW + 8) * 2, tileY, tileW, tileH), "Outstanding", _data.OutstandingBillingAmountFormatted, _data.OutstandingBillingAmount > 0 ? Amber : Muted);
                DrawMetricPill(g, new Rectangle(20 + (tileW + 8) * 3, tileY, tileW, tileH), "Overdue Amount", _data.OverdueBillingAmountFormatted, _data.OverdueBillingAmount > 0 ? Red : Muted);

                // Two columns below: Left = Revenue by Month (Bar chart), Right = Revenue by Plan (Breakdown)
                int sectionY = 138;
                int colW = (Width - 40 - 24) / 2;

                // Left Column: Revenue by Month
                TextRenderer.DrawText(g, "Revenue by Month", FontSectionTag, new Point(20, sectionY), Navy, TextFormatFlags.NoPadding);
                var monthChartRect = new Rectangle(20, sectionY + 24, colW, Height - sectionY - 38);
                DrawBarChart(g, monthChartRect, _data.RevenueByMonth.Select(m => (m.Month, (double)m.Amount)).ToList(), Green, "currency");

                // Right Column: Revenue by Subscription Plan
                int rightX = 20 + colW + 24;
                TextRenderer.DrawText(g, "Revenue by Subscription Plan", FontSectionTag, new Point(rightX, sectionY), Navy, TextFormatFlags.NoPadding);

                int curY = sectionY + 26;
                decimal totalRev = Math.Max(1m, _data.TotalSubscriptionRevenue);
                Color[] planColors = { Green, AccentBlue, Indigo, Purple, Amber };

                int pIdx = 0;
                foreach (var plan in _data.RevenueByPlan)
                {
                    if (curY + 38 > Height - 10) break;

                    Color c = planColors[pIdx % planColors.Length];
                    float ratio = (float)(plan.Amount / totalRev);

                    TextRenderer.DrawText(g, plan.PlanName, FontGridCell, new Point(rightX, curY), Navy, TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, $"{plan.Formatted} ({plan.Percentage:0.#}%)", FontKpiSub,
                        new Point(rightX + colW - 140, curY + 2), Muted, TextFormatFlags.NoPadding);

                    int maxBarW = colW;
                    int barW = Math.Max(6, (int)(maxBarW * ratio));
                    using (var barBg = new SolidBrush(Color.FromArgb(0xEE, 0xF2, 0xF6)))
                    using (var pathBg = RoundedRect(new Rectangle(rightX, curY + 20, maxBarW, 8), 3))
                    {
                        g.FillPath(barBg, pathBg);
                    }
                    using (var barFg = new SolidBrush(c))
                    using (var pathFg = RoundedRect(new Rectangle(rightX, curY + 20, barW, 8), 3))
                    {
                        g.FillPath(barFg, pathFg);
                    }

                    curY += 40;
                    pIdx++;
                }

                if (_data.RevenueByPlan.Count == 0)
                {
                    TextRenderer.DrawText(g, "No billing transaction history recorded.", FontCardSub, new Point(rightX, curY + 10), Muted, TextFormatFlags.NoPadding);
                }
            }

            private static void DrawMetricPill(Graphics g, Rectangle r, string label, string val, Color col)
            {
                using var b = new SolidBrush(Color.FromArgb(0xF8, 0xFA, 0xFD));
                using var p = new Pen(CardBorder, 1f);
                using var path = RoundedRect(r, 6);
                g.FillPath(b, path);
                g.DrawPath(p, path);

                TextRenderer.DrawText(g, label, FontKpiSub, new Point(r.X + 8, r.Y + 8), Muted, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, val, FontKpiValue, new Point(r.X + 8, r.Y + 24), col, TextFormatFlags.NoPadding);
            }
        }

        /// <summary>
        /// Generic GDI+ Bar Chart for Growth and Revenue trends
        /// </summary>
        private static void DrawBarChart(Graphics g, Rectangle r, List<(string Label, double Value)> items, Color barColor, string type)
        {
            if (items.Count == 0)
            {
                TextRenderer.DrawText(g, "No data available for the period.", FontCardSub,
                    new Point(r.X + 10, r.Y + r.Height / 2 - 10), Muted, TextFormatFlags.NoPadding);
                return;
            }

            double maxVal = items.Max(x => x.Value);
            if (maxVal <= 0) maxVal = 1;

            int bottomAxisY = r.Bottom - 24;
            int chartH = bottomAxisY - r.Top;

            // Baseline
            using (var p = new Pen(CardBorder, 1f))
            {
                g.DrawLine(p, r.Left, bottomAxisY, r.Right, bottomAxisY);
            }

            int count = items.Count;
            int slotW = r.Width / Math.Max(1, count);
            int barW = Math.Min(48, Math.Max(24, slotW - 20));

            for (int i = 0; i < count; i++)
            {
                var (lbl, val) = items[i];
                int centerX = r.Left + i * slotW + slotW / 2;
                int barH = (int)((val / maxVal) * (chartH - 24));
                if (barH < 6 && val > 0) barH = 6;

                int barX = centerX - barW / 2;
                int barY = bottomAxisY - barH;

                // Draw Bar
                var barRect = new Rectangle(barX, barY, barW, barH);
                using (var br = new SolidBrush(barColor))
                using (var path = RoundedRect(barRect, 4))
                {
                    g.FillPath(br, path);
                }

                // Value Label above bar
                string valStr = type == "currency" ? $"₱{val:N0}" : val.ToString("0");
                TextRenderer.DrawText(g, valStr, FontKpiSub,
                    new Rectangle(barX - 10, barY - 18, barW + 20, 16), Navy,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // X-axis label
                TextRenderer.DrawText(g, lbl, FontKpiSub,
                    new Rectangle(centerX - slotW / 2, bottomAxisY + 4, slotW, 18), Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
