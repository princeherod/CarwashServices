#nullable disable

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
using CarwashServices.Shell;

namespace CarwashServices.Roles
{
   
    public class AnalyticsView : UserControl
    {
        private HttpClient _http;

        // ---- data ----
        private AnalyticsSummaryDto _summary = new();
        private List<RetentionPointDto> _retention = new();
        private SegmentCountsDto _segments = new();
        private RevenueResponseDto _revenue = new();
        private List<ServiceRequestDto> _recent = new();
        private List<TenantCustomerDto> _customers = new();

        private List<(string Label, double Value)> _washFrequency = new List<(string Label, double Value)>
        {
            ("New", 1), ("Occasional", 2.8), ("Regular", 5), ("Loyal", 12.1)
        };

        // ---- UI ----
        private Panel _root;
        private TableLayoutPanel _page;
        private PageHeader _header;
        private KpiCard[] _kpis;
        private ChartCard _retentionCard, _washCard, _recentCard, _statusCard, _revenueCard, _actionsCard;
        private AreaLineChart _retentionChart;
        private ColumnChart _washChart, _revenueChart;
        private RingChart _donut;
        private SegmentLegend _legend;
        private ActivityList _activity;
        private ActionRow _rowAtRisk, _rowLost, _rowRetention;
        private bool _loading;

        public AnalyticsView()
        {
            Dock = DockStyle.Fill;
            BackColor = Ui.PageBg;
            Font = new Font("Segoe UI", 9.5f);

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5180/"),
                Timeout = TimeSpan.FromSeconds(15)
            };

            Load += async (s, e) =>
            {
                BuildUi();
                await ReloadAsync();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _http != null) _http.Dispose();
            base.Dispose(disposing);
        }

        private int S(double px) { return Ui.S(this, px); }

        // ================================================================
        //  BUILD UI (runs once)
        // ================================================================
        private void BuildUi()
        {
            SuspendLayout();

            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Ui.PageBg,
                AutoScroll = true,
                Padding = new Padding(S(32), S(20), S(32), S(28))
            };
            _root.AutoScrollMinSize = new Size(S(1020), 0);
            Controls.Add(_root);

            int hHeader = S(100), hKpi = S(142), hRow1 = S(378), hRow2 = S(358), hActions = S(412);

            _page = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Ui.PageBg,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Height = hHeader + hKpi + hRow1 + hRow2 + hActions
            };
            _page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, hHeader));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, hKpi));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, hRow1));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, hRow2));
            _page.RowStyles.Add(new RowStyle(SizeType.Absolute, hActions));
            _root.Controls.Add(_page);

            _header = new PageHeader { Dock = DockStyle.Fill, Margin = Padding.Empty };
            _header.RefreshButton.Click += async (s, e) => await ReloadAsync();
            _page.Controls.Add(_header, 0, 0);

            var kpiGrid = MakeGrid(16.66f, 16.66f, 16.66f, 16.66f, 16.66f, 16.7f);
            _kpis = new[]
            {
                new KpiCard { Title = "Total Customers",        Icon = IconKind.Users,     IconBg = Color.FromArgb(0xE1, 0xEC, 0xFD), IconFg = Ui.Accent },
                new KpiCard { Title = "Returning Customers",    Icon = IconKind.Repeat,    IconBg = Color.FromArgb(0xE1, 0xF7, 0xEC), IconFg = Ui.Green },
                new KpiCard { Title = "Churn Rate",             Icon = IconKind.TrendDown, IconBg = Color.FromArgb(0xFD, 0xE4, 0xE4), IconFg = Ui.Red },
                new KpiCard { Title = "Avg Spend per Customer", Icon = IconKind.Card,      IconBg = Color.FromArgb(0xEC, 0xE7, 0xFD), IconFg = Ui.Purple },
                new KpiCard { Title = "Cars Washed This Month", Icon = IconKind.Car,       IconBg = Color.FromArgb(0xFD, 0xF2, 0xD9), IconFg = Color.FromArgb(0xD9, 0x8A, 0x06) },
                new KpiCard { Title = "Revenue This Month",     Icon = IconKind.Coin,      IconBg = Color.FromArgb(0xDD, 0xF5, 0xF0), IconFg = Ui.Teal }
            };
            for (int i = 0; i < _kpis.Length; i++) AddCell(kpiGrid, _kpis[i], i, i == _kpis.Length - 1);
            _page.Controls.Add(kpiGrid, 0, 1);

            var row1 = MakeGrid(36f, 28f, 36f);

            _retentionCard = new ChartCard { Title = "Customer Retention Rate", Subtitle = "Monthly retention — active visitors / total seen" };
            _retentionChart = new AreaLineChart { Dock = DockStyle.Fill, LineColor = Ui.Accent };
            _retentionCard.Controls.Add(_retentionChart);
            AddCell(row1, _retentionCard, 0, false);

            _washCard = new ChartCard { Title = "Wash Frequency by Customer Segment", Subtitle = "Average washes per month by loyalty tier" };
            _washChart = new ColumnChart
            {
                Dock = DockStyle.Fill,
                ShowValueLabels = true,
                BarColors = new[] { Ui.Accent, Ui.Green, Ui.Purple, Ui.Orange },
                Format = v => v.ToString("0.#"),
                EmptyMax = 4
            };
            _washCard.Controls.Add(_washChart);
            AddCell(row1, _washCard, 1, false);

            _recentCard = new ChartCard { Title = "Recent Activity", Subtitle = "Latest service requests" };
            _activity = new ActivityList { Dock = DockStyle.Fill };
            _recentCard.Controls.Add(_activity);
            AddCell(row1, _recentCard, 2, true);
            _page.Controls.Add(row1, 0, 2);

            var row2 = MakeGrid(42f, 58f);

            _statusCard = new ChartCard
            {
                Title = "Customer Status",
                Subtitle = "Based on days since last visit",
                FooterNote = "Active = visited ≤60d  ·  At Risk = 61–120d  ·  Lost = 120d+"
            };
            var statusBody = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            statusBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(210)));
            statusBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            statusBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _donut = new RingChart { Dock = DockStyle.Fill, Margin = Padding.Empty };
            _legend = new SegmentLegend { Dock = DockStyle.Fill, Margin = new Padding(S(28), 0, 0, 0) };
            statusBody.Controls.Add(_donut, 0, 0);
            statusBody.Controls.Add(_legend, 1, 0);
            _statusCard.Controls.Add(statusBody);
            AddCell(row2, _statusCard, 0, false);

            _revenueCard = new ChartCard
            {
                Title = "Monthly Revenue",
                Subtitle = "Completed & paid transactions",
                FooterLeftLabel = "YTD Total:",
                FooterRightLabel = "Best month:"
            };
            _revenueChart = new ColumnChart
            {
                Dock = DockStyle.Fill,
                BarColor = Color.FromArgb(0x3B, 0x82, 0xF6),
                HighlightColor = Ui.Navy,
                Format = v => v >= 1000 ? "₱" + (v / 1000).ToString("0.#") + "k" : "₱" + v.ToString("0"),
                HoverFormat = v => "₱" + v.ToString("N0"),
                EmptyMax = 4000,
                EmptyText = "No revenue recorded yet"
            };
            _revenueCard.Controls.Add(_revenueChart);
            AddCell(row2, _revenueCard, 1, true);
            _page.Controls.Add(row2, 0, 3);

            _actionsCard = new ChartCard
            {
                Title = "Customer Retention Actions",
                Subtitle = "Actions generated from current customer analytics",
                FooterNote = "Based on days since last visit  ·  Active ≤60d  ·  At Risk 61–120d  ·  Lost 120d+",
                TextInset = 24,
                BodyPad = 0,
                HeaderRule = true,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            var rows = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 3; i++) rows.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            _rowAtRisk = new ActionRow { Tone = Ui.Yellow, Icon = IconKind.Alert, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _rowAtRisk.Ghost.Text = "View Customers";
            _rowAtRisk.Solid.Text = "Follow Up";
            _rowAtRisk.Ghost.Click += (s, e) => ShowSegmentDialog("At-Risk Customers", "AtRisk");
            _rowAtRisk.Solid.Click += (s, e) => FollowUpSegment("AtRisk");

            _rowLost = new ActionRow { Tone = Ui.Red, Icon = IconKind.UserX, ShowTopBorder = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _rowLost.Ghost.Text = "View Customers";
            _rowLost.Solid.Text = "Create Offer";
            _rowLost.Ghost.Click += (s, e) => ShowSegmentDialog("Lost Customers", "Lost");
            _rowLost.Solid.Click += (s, e) => FollowUpSegment("Lost");

            _rowRetention = new ActionRow { Tone = Ui.Accent, Icon = IconKind.TrendDown, ShowTopBorder = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            _rowRetention.Ghost.Text = "View Analysis";
            _rowRetention.Solid.Text = "Take Action";
            _rowRetention.Ghost.Click += (s, e) => { _root.AutoScrollPosition = new Point(0, 0); };
            _rowRetention.Solid.Click += (s, e) => FollowUpSegment("AtRisk");

            rows.Controls.Add(_rowAtRisk, 0, 0);
            rows.Controls.Add(_rowLost, 0, 1);
            rows.Controls.Add(_rowRetention, 0, 2);
            _actionsCard.Controls.Add(rows);
            _page.Controls.Add(_actionsCard, 0, 4);

            foreach (var card in new[] { _retentionCard, _washCard, _recentCard, _statusCard, _revenueCard, _actionsCard })
                card.ApplyPadding();

            HookWheelFocus(_root);
            ResumeLayout(true);
        }

        private TableLayoutPanel MakeGrid(params float[] columnPercents)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columnPercents.Length,
                RowCount = 1,
                BackColor = Ui.PageBg,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            foreach (var p in columnPercents) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, p));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            return t;
        }

        private void AddCell(TableLayoutPanel grid, Control c, int col, bool last)
        {
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 0, last ? 0 : S(16), S(18));
            grid.Controls.Add(c, col, 0);
        }

        private void HookWheelFocus(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                child.MouseEnter += (s, e) => { if (_root != null && !_root.ContainsFocus) _root.Focus(); };
                HookWheelFocus(child);
            }
        }

        // ================================================================
        //  DATA → UI
        // ================================================================
        private void ApplyData()
        {
            if (_kpis == null) return;

            string[] values =
            {
                _summary.TotalCustomers.ToString(),
                _summary.ReturningCustomers.ToString(),
                Convert.ToDouble(_summary.ChurnRate).ToString("0.#") + "%",
                "₱" + Convert.ToDouble(_summary.AvgSpendPerCustomer).ToString("N0"),
                _summary.CarsWashedThisMonth.ToString(),
                "₱" + Convert.ToDouble(_summary.RevenueThisMonth).ToString("N0")
            };
            for (int i = 0; i < _kpis.Length; i++)
            {
                _kpis[i].Value = values[i];
                _kpis[i].DeltaText = "–";
                _kpis[i].DeltaColor = Ui.Muted;
            }

            _retentionChart.Points = _retention.Select(p => (p.Month, Convert.ToDouble(p.Value))).ToList();
            _washChart.Bars = _washFrequency;
            _revenueChart.Bars = _revenue.Months.Select(m => (m.Label, Convert.ToDouble(m.Value))).ToList();
            _revenueChart.HighlightIndex = _revenueChart.Bars.Count - 1;

            _revenueCard.Subtitle = "Jan – " + DateTime.Today.ToString("MMM yyyy") + " · Completed & paid transactions";
            _revenueCard.FooterLeftValue = "₱" + Convert.ToDouble(_revenue.Ytd).ToString("N0");
            _revenueCard.FooterRightValue = _revenue.BestMonth + " — ₱" + Convert.ToDouble(_revenue.BestValue).ToString("N0");

            int total = Convert.ToInt32(_segments.Total);
            int active = Convert.ToInt32(_segments.Active);
            int atRisk = Convert.ToInt32(_segments.AtRisk);
            int lost = Convert.ToInt32(_segments.Lost);
            double activePct = Convert.ToDouble(_segments.ActivePct);
            double atRiskPct = Convert.ToDouble(_segments.AtRiskPct);
            double lostPct = Convert.ToDouble(_segments.LostPct);

            _donut.CenterTop = total.ToString();
            _donut.CenterBottom = "Total";
            _donut.Segments = new List<(string Label, double Value, Color Color)>
            {
                ("Active", active, Ui.Accent),
                ("At Risk", atRisk, Ui.Yellow),
                ("Lost", lost, Ui.Red)
            };
            _legend.Items = new List<(string Label, int Count, double Pct, Color Color)>
            {
                ("Active", active, activePct, Ui.Accent),
                ("At Risk", atRisk, atRiskPct, Ui.Yellow),
                ("Lost", lost, lostPct, Ui.Red)
            };

            _activity.Items = _recent.Take(8).Select(req =>
            {
                var cust = _customers.FirstOrDefault(c => c.TenantCustomerId == req.CustomerId);
                string who = cust != null ? cust.CustomerName : "Customer #" + req.CustomerId;
                DateTime when = req.CompletedDate ?? req.RequestedDate;
                bool done = req.Status == "Completed";
                return new ActivityItem
                {
                    Title = done ? "Service completed" : "Service request created",
                    Subtitle = who + " · #" + req.RequestId.ToString("0000"),
                    When = Ago(when),
                    Completed = done
                };
            }).ToList();

            double avgSpend = Convert.ToDouble(_summary.AvgSpendPerCustomer);

            _rowAtRisk.Title = atRisk + " At-Risk Customers";
            _rowAtRisk.ShareText = "(" + atRiskPct.ToString("0.#") + "%)";
            _rowAtRisk.Basis = "No visit for 61–120 days.";
            _rowAtRisk.Action = "Send a return-customer follow-up.";
            _rowAtRisk.Impact = atRisk > 0 && avgSpend > 0
                ? "Est. ₱" + (atRisk * avgSpend).ToString("N0") + " in spend at risk (" + atRisk + " × ₱" + avgSpend.ToString("N0") + ")"
                : "";
            _rowAtRisk.Meter = atRiskPct;
            _rowAtRisk.MeterCaption = atRiskPct.ToString("0.#") + "% of " + total + " customers";

            _rowLost.Title = lost + " Lost Customers";
            _rowLost.ShareText = "(" + lostPct.ToString("0.#") + "%)";
            _rowLost.Basis = "No visit for 120+ days.";
            _rowLost.Action = "Send a reactivation offer.";
            _rowLost.Impact = lost > 0 && avgSpend > 0
                ? "Est. ₱" + (lost * avgSpend).ToString("N0") + " in spend lost (" + lost + " × ₱" + avgSpend.ToString("N0") + ")"
                : "";
            _rowLost.Meter = lostPct;
            _rowLost.MeterCaption = lostPct.ToString("0.#") + "% of " + total + " customers";

            int rc = _retention.Count;
            double cur = rc > 0 ? Convert.ToDouble(_retention[rc - 1].Value) : 0;
            bool hasPrev = rc >= 2;
            double prev = hasPrev ? Convert.ToDouble(_retention[rc - 2].Value) : 0;
            string prevMonth = hasPrev ? _retention[rc - 2].Month : "";
            double diff = cur - prev;

            _rowRetention.Title = "Retention Rate: " + cur.ToString("0.#") + "%";
            _rowRetention.Basis = "Share of customers who returned within 90 days.";
            _rowRetention.Action = "Launch a customer re-engagement campaign.";
            _rowRetention.Meter = cur;
            if (hasPrev)
            {
                _rowRetention.ShareText = (diff < 0 ? "↓ " : "↑ ") + Math.Abs(diff).ToString("0.#") + " pts";
                _rowRetention.ShareColor = diff < 0 ? Ui.Red : Ui.Green;
                _rowRetention.Impact = (diff < 0 ? "Down " : "Up ") + Math.Abs(diff).ToString("0.#") + " pts from " + prevMonth + " (" + prev.ToString("0.#") + "%)";
                _rowRetention.Tick = prev;
                _rowRetention.MeterCaption = "Now " + cur.ToString("0.#") + "%  ·  marker = " + prevMonth;
            }
            else
            {
                _rowRetention.ShareText = "";
                _rowRetention.Impact = "";
                _rowRetention.Tick = null;
                _rowRetention.MeterCaption = "";
            }

            _root.Invalidate(true);
        }

        private static string Ago(DateTime when)
        {
            var d = DateTime.Now - when;
            if (d.TotalSeconds < 0) return when.ToString("MMM d");
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + "m ago";
            if (d.TotalHours < 24) return (int)d.TotalHours + "h ago";
            if (d.TotalDays < 7) return (int)d.TotalDays + "d ago";
            return when.ToString("MMM d");
        }

        // ================================================================
        //  LOAD
        // ================================================================
        private async Task ReloadAsync()
        {
            if (_loading || _header == null) return;
            _loading = true;
            _header.RefreshButton.Enabled = false;
            _header.RefreshButton.Text = "Refreshing…";

            bool ok = await LoadAsync();
            ApplyData();

            if (ok) _header.Status = "Updated " + DateTime.Now.ToString("h:mm tt");
            _header.RefreshButton.Text = "Refresh";
            _header.RefreshButton.Enabled = true;
            _header.Invalidate();
            _loading = false;
        }

        private async Task<bool> LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var summaryT = _http.GetFromJsonAsync<AnalyticsSummaryDto>("api/analytics/summary?companyId=1");
                var retentionT = _http.GetFromJsonAsync<List<RetentionPointDto>>("api/analytics/retention?companyId=1");
                var segmentsT = _http.GetFromJsonAsync<SegmentCountsDto>("api/analytics/segments?companyId=1");
                var revenueT = _http.GetFromJsonAsync<RevenueResponseDto>("api/analytics/revenue?companyId=1");
                var recentT = _http.GetFromJsonAsync<List<ServiceRequestDto>>("api/analytics/recent");
                var customersT = _http.GetFromJsonAsync<List<TenantCustomerDto>>("api/tenant/1/tenant-customers");

                await Task.WhenAll(summaryT, retentionT, segmentsT, revenueT, recentT, customersT);

                _summary = summaryT.Result ?? new AnalyticsSummaryDto();
                _retention = retentionT.Result ?? new List<RetentionPointDto>();
                _segments = segmentsT.Result ?? new SegmentCountsDto();
                _revenue = revenueT.Result ?? new RevenueResponseDto();
                _recent = recentT.Result ?? new List<ServiceRequestDto>();
                _customers = customersT.Result ?? new List<TenantCustomerDto>();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load analytics.\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ================================================================
        //  SEGMENT ACTIONS
        // ================================================================
        private async void ShowSegmentDialog(string title, string segment)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var rows = await _http.GetFromJsonAsync<List<SegmentCustomerDto>>(
                    $"api/analytics/segment-customers?companyId=1&segment={segment}")
                    ?? new List<SegmentCustomerDto>();

                Cursor = Cursors.Default;

                using var dlg = new CustomerSegmentDialog(title, rows);
                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                    JumpToFollowUp(dlg.SelectedCustomerIds);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(
                    $"Failed to load segment customers.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void FollowUpSegment(string segment)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var rows = await _http.GetFromJsonAsync<List<SegmentCustomerDto>>(
                    $"api/analytics/segment-customers?companyId=1&segment={segment}")
                    ?? new List<SegmentCustomerDto>();

                Cursor = Cursors.Default;

                // Only send customers who don't already have an open follow-up.
                var eligible = rows.Where(r => !r.HasOpenFollowUp).ToList();

                if (eligible.Count == 0)
                {
                    MessageBox.Show(
                        "Everyone in this segment already has an open follow-up.\n\n" +
                        "Wait for them to respond, or wait for the offers to expire.",
                        "Nothing to do",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var ids = eligible.Select(r => r.CustomerId).ToList();
                JumpToFollowUp(ids);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(
                    $"Failed to load segment.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void JumpToFollowUp(List<int> customerIds)
        {
            if (customerIds == null || customerIds.Count == 0)
            {
                MessageBox.Show("No customers in this segment.",
                    "Follow Up", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var mainForm = this.FindForm() as MainForm;
            if (mainForm == null) return;

            mainForm.NavigateToFollowUpsWithCustomers(customerIds);
        }

        // ================================================================
        //  DRAWING CODE
        // ================================================================
        private enum IconKind { Users, Repeat, TrendDown, Card, Car, Coin, Check, Alert, UserX }

        private sealed class ActivityItem
        {
            public string Title, Subtitle, When;
            public bool Completed;
        }

        private static class Ui
        {
            public static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
            public static readonly Color Body = Color.FromArgb(0x4A, 0x5A, 0x78);
            public static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
            public static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
            public static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
            public static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
            public static readonly Color Line = Color.FromArgb(0xEC, 0xF0, 0xF6);
            public static readonly Color Grid = Color.FromArgb(0xEE, 0xF2, 0xF8);
            public static readonly Color GridStrong = Color.FromArgb(0xD9, 0xE1, 0xEE);
            public static readonly Color Track = Color.FromArgb(0xEC, 0xF0, 0xF7);
            public static readonly Color FieldBorder = Color.FromArgb(0xD5, 0xDD, 0xEB);
            public static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
            public static readonly Color AccentHover = Color.FromArgb(0x19, 0x76, 0xD2);
            public static readonly Color AccentDark = Color.FromArgb(0x15, 0x65, 0xC0);
            public static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
            public static readonly Color Red = Color.FromArgb(0xE5, 0x39, 0x35);
            public static readonly Color Yellow = Color.FromArgb(0xF5, 0xB0, 0x2E);
            public static readonly Color Purple = Color.FromArgb(0x8B, 0x5C, 0xF6);
            public static readonly Color Orange = Color.FromArgb(0xF5, 0x7C, 0x00);
            public static readonly Color Teal = Color.FromArgb(0x14, 0xA3, 0x8B);

            public static readonly Font FTitle = new Font("Segoe UI Semibold", 22f);
            public static readonly Font FCardTitle = new Font("Segoe UI Semibold", 12f);
            public static readonly Font FRowTitle = new Font("Segoe UI Semibold", 11.5f);
            public static readonly Font FKpi = new Font("Segoe UI Semibold", 20f);
            public static readonly Font FBody = new Font("Segoe UI", 10f);
            public static readonly Font FSub = new Font("Segoe UI", 9f);
            public static readonly Font FSmall = new Font("Segoe UI", 8.5f);
            public static readonly Font FTick = new Font("Segoe UI", 8.5f);
            public static readonly Font FSemi9 = new Font("Segoe UI Semibold", 9f);
            public static readonly Font FSemi10 = new Font("Segoe UI Semibold", 10f);
            public static readonly Font FBtn = new Font("Segoe UI Semibold", 9.5f);

            public const TextFormatFlags LeftF = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            public const TextFormatFlags RightF = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            public const TextFormatFlags CenterF = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

            public static int S(Control c, double px)
            {
                return (int)Math.Round(px * c.DeviceDpi / 96.0);
            }

            public static Color Tint(Color c, double amount)
            {
                int r = (int)Math.Round(255 - (255 - c.R) * amount);
                int g = (int)Math.Round(255 - (255 - c.G) * amount);
                int b = (int)Math.Round(255 - (255 - c.B) * amount);
                return Color.FromArgb(r, g, b);
            }

            public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags = LeftF)
            {
                if (string.IsNullOrEmpty(s)) return;
                TextRenderer.DrawText(g, s, f, r, c,
                    flags | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            public static Size Measure(Graphics g, string s, Font f)
            {
                if (string.IsNullOrEmpty(s)) return Size.Empty;
                return TextRenderer.MeasureText(g, s, f, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }

            public static GraphicsPath Round(RectangleF r, float radius)
            {
                float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
                var p = new GraphicsPath();
                if (d <= 0f) { p.AddRectangle(r); return p; }
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }

            public static GraphicsPath RoundTop(RectangleF r, float radius)
            {
                float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height * 2f));
                var p = new GraphicsPath();
                if (d <= 0f) { p.AddRectangle(r); return p; }
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
                p.CloseFigure();
                return p;
            }

            public static void Pill(Graphics g, Control c, string text, float cx, float bottomY, Rectangle bounds)
            {
                Size sz = Measure(g, text, FSemi9);
                int padX = S(c, 10), padY = S(c, 5);
                var r = new RectangleF(cx - (sz.Width + padX * 2) / 2f, bottomY - sz.Height - padY * 2, sz.Width + padX * 2, sz.Height + padY * 2);
                if (r.Right > bounds.Right) r.X = bounds.Right - r.Width;
                if (r.Left < bounds.Left) r.X = bounds.Left;
                if (r.Top < bounds.Top) r.Y = bounds.Top;
                using (var path = Round(r, r.Height / 2f))
                using (var br = new SolidBrush(Navy))
                    g.FillPath(br, path);
                TextRenderer.DrawText(g, text, FSemi9, Rectangle.Round(r), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }

            public static void NiceScale(double dataMax, double emptyMax, out double max, out double step)
            {
                if (dataMax <= 0)
                {
                    max = emptyMax;
                    step = emptyMax / 4.0;
                    return;
                }
                double raw = dataMax / 4.0;
                double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
                double norm = raw / mag;
                double nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10;
                step = nice * mag;
                max = Math.Ceiling(dataMax / step - 1e-9) * step;
            }
        }

        private static class Icons
        {
            public static void Draw(Graphics g, IconKind kind, RectangleF b, Color fg, Color bg)
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float x = b.X, y = b.Y, w = b.Width, h = b.Height;
                float lw = Math.Max(1.6f, w * 0.10f);
                Func<float, float, PointF> P = (px, py) => new PointF(x + px * w, y + py * h);

                using (var pen = new Pen(fg, lw) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var br = new SolidBrush(fg))
                {
                    switch (kind)
                    {
                        case IconKind.Users:
                            g.FillEllipse(br, x + 0.14f * w, y + 0.14f * h, 0.30f * w, 0.30f * h);
                            g.FillPie(br, x + 0.04f * w, y + 0.50f * h, 0.50f * w, 0.60f * h, 180, 180);
                            g.FillEllipse(br, x + 0.54f * w, y + 0.22f * h, 0.26f * w, 0.26f * h);
                            g.FillPie(br, x + 0.50f * w, y + 0.54f * h, 0.44f * w, 0.52f * h, 180, 180);
                            break;
                        case IconKind.Repeat:
                            using (var cap = new AdjustableArrowCap(3f, 3f))
                            using (var ap = new Pen(fg, lw) { StartCap = LineCap.Round })
                            {
                                ap.CustomEndCap = cap;
                                var rc = new RectangleF(x + 0.16f * w, y + 0.16f * h, 0.68f * w, 0.68f * h);
                                g.DrawArc(ap, rc, 200, 140);
                                g.DrawArc(ap, rc, 20, 140);
                            }
                            break;
                        case IconKind.TrendDown:
                            g.DrawLines(pen, new[] { P(0.10f, 0.28f), P(0.38f, 0.56f), P(0.56f, 0.40f), P(0.90f, 0.74f) });
                            g.DrawLines(pen, new[] { P(0.90f, 0.50f), P(0.90f, 0.74f), P(0.66f, 0.74f) });
                            break;
                        case IconKind.Card:
                            using (var path = Ui.Round(new RectangleF(x + 0.08f * w, y + 0.22f * h, 0.84f * w, 0.56f * h), w * 0.10f))
                                g.DrawPath(pen, path);
                            g.FillRectangle(br, x + 0.08f * w, y + 0.38f * h, 0.84f * w, 0.12f * h);
                            break;
                        case IconKind.Car:
                            g.FillPolygon(br, new[] { P(0.22f, 0.46f), P(0.32f, 0.26f), P(0.68f, 0.26f), P(0.78f, 0.46f) });
                            using (var body = Ui.Round(new RectangleF(x + 0.06f * w, y + 0.42f * h, 0.88f * w, 0.30f * h), w * 0.08f))
                                g.FillPath(br, body);
                            using (var wb = new SolidBrush(bg))
                            {
                                g.FillEllipse(wb, x + 0.16f * w, y + 0.60f * h, 0.26f * w, 0.26f * h);
                                g.FillEllipse(wb, x + 0.58f * w, y + 0.60f * h, 0.26f * w, 0.26f * h);
                            }
                            g.FillEllipse(br, x + 0.20f * w, y + 0.64f * h, 0.18f * w, 0.18f * h);
                            g.FillEllipse(br, x + 0.62f * w, y + 0.64f * h, 0.18f * w, 0.18f * h);
                            break;
                        case IconKind.Coin:
                            g.DrawEllipse(pen, x + 0.10f * w, y + 0.10f * h, 0.80f * w, 0.80f * h);
                            using (var f = new Font("Segoe UI", w * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString("₱", f, br, new RectangleF(x, y, w, h), sf);
                            break;
                        case IconKind.Check:
                            g.FillEllipse(br, x + 0.06f * w, y + 0.06f * h, 0.88f * w, 0.88f * h);
                            using (var wp = new Pen(bg, lw) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                                g.DrawLines(wp, new[] { P(0.30f, 0.52f), P(0.45f, 0.66f), P(0.72f, 0.36f) });
                            break;
                        case IconKind.Alert:
                            g.DrawPolygon(pen, new[] { P(0.50f, 0.12f), P(0.92f, 0.84f), P(0.08f, 0.84f) });
                            g.DrawLine(pen, P(0.50f, 0.40f), P(0.50f, 0.60f));
                            g.FillEllipse(br, x + 0.50f * w - lw * 0.5f, y + 0.72f * h - lw * 0.5f, lw, lw);
                            break;
                        case IconKind.UserX:
                            g.FillEllipse(br, x + 0.14f * w, y + 0.14f * h, 0.34f * w, 0.34f * h);
                            g.FillPie(br, x + 0.04f * w, y + 0.52f * h, 0.54f * w, 0.66f * h, 180, 180);
                            g.DrawLine(pen, P(0.66f, 0.30f), P(0.90f, 0.54f));
                            g.DrawLine(pen, P(0.90f, 0.30f), P(0.66f, 0.54f));
                            break;
                    }
                }

                g.SmoothingMode = old;
            }
        }

        private abstract class BufferedControl : Control
        {
            protected BufferedControl()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }

            protected int S(double px) { return Ui.S(this, px); }
        }

        private class RoundedCard : Panel
        {
            public RoundedCard()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }

            protected int S(double px) { return Ui.S(this, px); }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.Clear(Ui.PageBg);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float radius = S(14);
                var rect = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2.5f);

                using (var sp = Ui.Round(new RectangleF(rect.X, rect.Y + 1.5f, rect.Width, rect.Height), radius))
                using (var sb = new SolidBrush(Color.FromArgb(14, 10, 22, 51)))
                    g.FillPath(sb, sp);

                using (var path = Ui.Round(rect, radius))
                {
                    using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);
                    using (var pen = new Pen(Ui.CardBorder)) g.DrawPath(pen, path);
                }

                PaintCard(g);
            }

            protected virtual void PaintCard(Graphics g) { }
        }

        private sealed class ChartCard : RoundedCard
        {
            public string Title = "", Subtitle = "";
            public string FooterNote = "";
            public string FooterLeftLabel = "", FooterLeftValue = "", FooterRightLabel = "", FooterRightValue = "";
            public int TextInset = 20;
            public int BodyPad = 20;
            public bool HeaderRule;

            public void ApplyPadding()
            {
                Padding = new Padding(S(BodyPad), S(74), S(BodyPad), HasFooter() ? S(52) : S(18));
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                ApplyPadding();
            }

            private bool HasFooter()
            {
                return !string.IsNullOrEmpty(FooterNote) || !string.IsNullOrEmpty(FooterLeftLabel) || !string.IsNullOrEmpty(FooterRightLabel);
            }

            protected override void PaintCard(Graphics g)
            {
                int pad = S(TextInset);
                Ui.Text(g, Title, Ui.FCardTitle, Ui.Navy, new Rectangle(pad, S(16), Width - pad * 2, S(26)));
                Ui.Text(g, Subtitle, Ui.FSub, Ui.Muted, new Rectangle(pad, S(42), Width - pad * 2, S(20)));

                if (HeaderRule)
                    using (var pen = new Pen(Ui.Line)) g.DrawLine(pen, pad, S(72), Width - pad, S(72));

                if (HasFooter())
                {
                    int fy = Height - S(50);
                    using (var pen = new Pen(Ui.Line)) g.DrawLine(pen, pad, fy, Width - pad, fy);
                    var fr = new Rectangle(pad, fy, Width - pad * 2, S(48));
                    Ui.Text(g, FooterNote, Ui.FSmall, Ui.Muted, fr);
                    DrawPair(g, FooterLeftLabel, FooterLeftValue, fr, false);
                    DrawPair(g, FooterRightLabel, FooterRightValue, fr, true);
                }
            }

            private void DrawPair(Graphics g, string label, string value, Rectangle fr, bool alignRight)
            {
                if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(value)) return;
                int lw = Ui.Measure(g, label, Ui.FSub).Width;
                int vw = Ui.Measure(g, value, Ui.FSemi10).Width;
                int gap = S(6);
                int total = lw + gap + vw;
                int x = alignRight ? fr.Right - total : fr.Left;
                Ui.Text(g, label, Ui.FSub, Ui.Muted, new Rectangle(x, fr.Y, lw + S(4), fr.Height));
                Ui.Text(g, value, Ui.FSemi10, Ui.Navy, new Rectangle(x + lw + gap, fr.Y, vw + S(6), fr.Height));
            }
        }

        private sealed class RoundedButton : Button
        {
            public bool Solid;
            private bool _hover, _down;

            public RoundedButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
                BackColor = Color.White;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
            protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

            protected override void OnPaint(PaintEventArgs pe)
            {
                var g = pe.Graphics;
                g.Clear(Parent != null ? Parent.BackColor : Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color fill, border, fore;
                if (!Enabled)
                {
                    fill = Color.FromArgb(0xF4, 0xF6, 0xFA); border = Ui.Line; fore = Ui.Faint;
                }
                else if (Solid)
                {
                    fill = _down ? Ui.AccentDark : (_hover ? Ui.AccentHover : Ui.Accent);
                    border = fill; fore = Color.White;
                }
                else
                {
                    fill = _down ? Color.FromArgb(0xE6, 0xEE, 0xF9) : (_hover ? Color.FromArgb(0xF4, 0xF7, 0xFC) : Color.White);
                    border = Ui.FieldBorder; fore = Ui.Navy;
                }

                var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                using (var path = Ui.Round(r, Ui.S(this, 10)))
                {
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var pen = new Pen(border)) g.DrawPath(pen, path);
                }

                TextRenderer.DrawText(g, Text, Ui.FBtn, new Rectangle(0, 0, Width, Height), fore,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                if (Focused && ShowFocusCues)
                    using (var fp = new Pen(Ui.Accent, 2f))
                    using (var fpath = Ui.Round(new RectangleF(2f, 2f, Width - 5f, Height - 5f), Ui.S(this, 8)))
                        g.DrawPath(fp, fpath);
            }
        }

        private sealed class PageHeader : BufferedControl
        {
            public readonly RoundedButton RefreshButton;
            public string Status = "";

            public PageHeader()
            {
                BackColor = Ui.PageBg;
                RefreshButton = new RoundedButton { Text = "Refresh" };
                Controls.Add(RefreshButton);
            }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                int w = S(112), h = S(38);
                RefreshButton.SetBounds(Width - w, S(32), w, h);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Ui.PageBg);
                Ui.Text(g, "Overview  ›  Analytics", Ui.FSub, Ui.Muted, new Rectangle(0, 0, Width, S(20)));
                Ui.Text(g, "Analytics", Ui.FTitle, Ui.Navy, new Rectangle(0, S(20), Width - S(300), S(42)));
                Ui.Text(g, "Track performance and customer insights across your carwash business.", Ui.FBody, Ui.Muted,
                    new Rectangle(0, S(64), Width - S(300), S(24)));
                if (!string.IsNullOrEmpty(Status))
                    Ui.Text(g, Status, Ui.FSmall, Ui.Faint, new Rectangle(0, S(32), Width - S(112) - S(14), S(38)), Ui.RightF);
            }
        }

        private sealed class KpiCard : RoundedCard
        {
            public string Title = "", Value = "–", DeltaText = "–";
            public Color DeltaColor = Ui.Muted, IconBg = Color.White, IconFg = Ui.Accent;
            public IconKind Icon;

            protected override void PaintCard(Graphics g)
            {
                int pad = S(18), tile = S(40);
                var tileRect = new RectangleF(Width - pad - tile + S(2), pad - S(2), tile, tile);
                using (var path = Ui.Round(tileRect, S(11)))
                using (var br = new SolidBrush(IconBg))
                    g.FillPath(br, path);
                float inset = tile * 0.24f;
                Icons.Draw(g, Icon, new RectangleF(tileRect.X + inset, tileRect.Y + inset, tile - inset * 2, tile - inset * 2), IconFg, IconBg);

                Ui.Text(g, Title, Ui.FSub, Ui.Muted,
                    new Rectangle(pad, pad - S(2), Math.Max(10, Width - pad * 2 - tile - S(6)), S(36)),
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);

                Ui.Text(g, Value, Ui.FKpi, Ui.Navy, new Rectangle(pad, Height - S(78), Width - pad * 2, S(40)));

                int dy = Height - S(34);
                int dw = Ui.Measure(g, DeltaText, Ui.FSmall).Width;
                Ui.Text(g, DeltaText, Ui.FSmall, DeltaColor, new Rectangle(pad, dy, dw + S(4), S(18)));
                Ui.Text(g, "vs previous period", Ui.FSmall, Ui.Faint,
                    new Rectangle(pad + dw + S(8), dy, Math.Max(10, Width - pad * 2 - dw - S(8)), S(18)));
            }
        }

        private sealed class AreaLineChart : BufferedControl
        {
            public List<(string Label, double Value)> Points = new List<(string Label, double Value)>();
            public Color LineColor = Ui.Accent;
            private int _hover = -1;

            public AreaLineChart() { SetStyle(ControlStyles.Selectable, false); }

            private Rectangle PlotRect()
            {
                int l = S(46), t = S(16), r = S(14), b = S(30);
                return new Rectangle(l, t, Math.Max(10, Width - l - r), Math.Max(10, Height - t - b));
            }

            private PointF[] Coords(Rectangle plot)
            {
                int n = Points.Count;
                var arr = new PointF[n];
                float inset = S(16);
                for (int i = 0; i < n; i++)
                {
                    float fx = n == 1 ? plot.Left + plot.Width / 2f : plot.Left + inset + i * (plot.Width - 2 * inset) / (n - 1);
                    float v = (float)Math.Max(0, Math.Min(100, Points[i].Value));
                    arr[i] = new PointF(fx, plot.Bottom - v / 100f * plot.Height);
                }
                return arr;
            }

            private int HitIndex(int mx)
            {
                if (Points.Count == 0) return -1;
                var pts = Coords(PlotRect());
                int best = -1;
                float bd = S(28);
                for (int i = 0; i < pts.Length; i++)
                {
                    float d = Math.Abs(pts[i].X - mx);
                    if (d < bd) { bd = d; best = i; }
                }
                return best;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = HitIndex(e.X);
                if (idx != _hover) { _hover = idx; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_hover != -1) { _hover = -1; Invalidate(); }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var plot = PlotRect();

                for (int v = 0; v <= 100; v += 25)
                {
                    float y = plot.Bottom - v / 100f * plot.Height;
                    using (var pen = new Pen(v == 0 ? Ui.GridStrong : Ui.Grid, 1f)) g.DrawLine(pen, plot.Left, y, plot.Right, y);
                    Ui.Text(g, v + "%", Ui.FTick, Ui.Muted, new Rectangle(0, (int)y - S(9), plot.Left - S(10), S(18)), Ui.RightF);
                }

                int n = Points.Count;
                if (n == 0)
                {
                    Ui.Text(g, "No data yet", Ui.FSub, Ui.Muted, plot, Ui.CenterF);
                    return;
                }

                var pts = Coords(plot);

                if (n >= 2)
                {
                    g.SetClip(new Rectangle(plot.Left - S(4), plot.Top - S(6), plot.Width + S(8), plot.Height + S(6)));
                    using (var curve = new GraphicsPath())
                    {
                        curve.AddCurve(pts, 0.35f);
                        using (var area = (GraphicsPath)curve.Clone())
                        {
                            area.AddLine(pts[n - 1].X, plot.Bottom, pts[0].X, plot.Bottom);
                            area.CloseFigure();
                            using (var lg = new LinearGradientBrush(new Rectangle(plot.Left, plot.Top, plot.Width, plot.Height + 1),
                                       Color.FromArgb(70, LineColor), Color.FromArgb(0, LineColor), 90f))
                                g.FillPath(lg, area);
                        }
                        using (var pen = new Pen(LineColor, S(3)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                            g.DrawPath(pen, curve);
                    }
                    g.ResetClip();
                }

                for (int i = 0; i < n; i++)
                {
                    bool last = i == n - 1;
                    bool focus = i == _hover;
                    float r = S(focus ? 6 : (last ? 5 : 4));
                    var p = pts[i];
                    using (var fill = new SolidBrush(last ? LineColor : Color.White))
                        g.FillEllipse(fill, p.X - r, p.Y - r, r * 2, r * 2);
                    using (var pen = new Pen(LineColor, S(2)))
                        g.DrawEllipse(pen, p.X - r, p.Y - r, r * 2, r * 2);
                }

                int step = n > 12 ? 2 : 1;
                for (int i = 0; i < n; i += step)
                    Ui.Text(g, Points[i].Label, Ui.FTick, Ui.Muted,
                        new Rectangle((int)pts[i].X - S(24), plot.Bottom + S(8), S(48), S(18)), Ui.CenterF);

                int fi = _hover >= 0 ? _hover : n - 1;
                Ui.Pill(g, this, Points[fi].Label + "  " + Points[fi].Value.ToString("0.#") + "%", pts[fi].X, pts[fi].Y - S(10), ClientRectangle);
            }
        }

        private sealed class ColumnChart : BufferedControl
        {
            public List<(string Label, double Value)> Bars = new List<(string Label, double Value)>();
            public Color[] BarColors;
            public Color BarColor = Ui.Accent;
            public int HighlightIndex = -1;
            public Color HighlightColor = Ui.Navy;
            public Func<double, string> Format = v => v.ToString("0.#");
            public Func<double, string> HoverFormat;
            public bool ShowValueLabels;
            public double EmptyMax = 4;
            public string EmptyText = "No data yet";

            private int _hover = -1;
            private Rectangle _plot;

            public ColumnChart() { SetStyle(ControlStyles.Selectable, false); }

            private int HitIndex(int mx)
            {
                if (Bars.Count == 0 || _plot.Width <= 0) return -1;
                if (mx < _plot.Left || mx > _plot.Right) return -1;
                float slot = (float)_plot.Width / Bars.Count;
                int i = (int)((mx - _plot.Left) / slot);
                return (i >= 0 && i < Bars.Count) ? i : -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = HitIndex(e.X);
                if (idx != _hover) { _hover = idx; Invalidate(); }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (_hover != -1) { _hover = -1; Invalidate(); }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                double dataMax = Bars.Count == 0 ? 0 : Bars.Max(b => b.Value);
                double max, step;
                Ui.NiceScale(dataMax, EmptyMax, out max, out step);
                int ticks = Math.Max(1, (int)Math.Round(max / step));

                int labW = 0;
                for (int i = 0; i <= ticks; i++)
                    labW = Math.Max(labW, Ui.Measure(g, Format(i * step), Ui.FTick).Width);

                int left = labW + S(14), top = S(ShowValueLabels ? 30 : 14), bottom = S(30), right = S(8);
                _plot = new Rectangle(left, top, Math.Max(10, Width - left - right), Math.Max(10, Height - top - bottom));
                var plot = _plot;

                for (int i = 0; i <= ticks; i++)
                {
                    float y = plot.Bottom - (float)(i * step / max) * plot.Height;
                    using (var pen = new Pen(i == 0 ? Ui.GridStrong : Ui.Grid, 1f)) g.DrawLine(pen, plot.Left, y, plot.Right, y);
                    Ui.Text(g, Format(i * step), Ui.FTick, Ui.Muted, new Rectangle(0, (int)y - S(9), plot.Left - S(8), S(18)), Ui.RightF);
                }

                int n = Bars.Count;
                if (n == 0 || dataMax <= 0)
                    Ui.Text(g, EmptyText, Ui.FSub, Ui.Muted, plot, Ui.CenterF);

                float slot = n == 0 ? 0f : (float)plot.Width / n;
                for (int i = 0; i < n; i++)
                {
                    float cx = plot.Left + slot * i + slot / 2f;
                    Ui.Text(g, Bars[i].Label, Ui.FTick, Ui.Muted,
                        new Rectangle((int)(cx - slot / 2f), plot.Bottom + S(8), (int)slot, S(18)), Ui.CenterF);

                    double v = Bars[i].Value;
                    if (v <= 0) continue;

                    float bw = Math.Min(S(56), slot * 0.56f);
                    float bh = Math.Max(S(3), (float)(v / max) * plot.Height);
                    var rect = new RectangleF(cx - bw / 2f, plot.Bottom - bh, bw, bh);

                    Color c = i == HighlightIndex ? HighlightColor
                            : (BarColors != null && i < BarColors.Length ? BarColors[i] : BarColor);
                    if (i == _hover) c = ControlPaint.Light(c, 0.2f);

                    using (var path = Ui.RoundTop(rect, S(6)))
                    using (var br = new SolidBrush(c))
                        g.FillPath(br, path);

                    if (ShowValueLabels)
                        Ui.Text(g, Format(v), Ui.FSemi10, Ui.Navy,
                            new Rectangle((int)(cx - S(30)), (int)rect.Y - S(22), S(60), S(18)), Ui.CenterF);
                }

                if (!ShowValueLabels && _hover >= 0 && _hover < n)
                {
                    double hv = Bars[_hover].Value;
                    float cx = plot.Left + slot * _hover + slot / 2f;
                    float bh = hv <= 0 ? 0f : Math.Max(S(3), (float)(hv / max) * plot.Height);
                    string t = Bars[_hover].Label + ": " + (HoverFormat ?? Format)(hv);
                    Ui.Pill(g, this, t, cx, plot.Bottom - bh - S(6), ClientRectangle);
                }
            }
        }

        private sealed class RingChart : BufferedControl
        {
            public List<(string Label, double Value, Color Color)> Segments = new List<(string Label, double Value, Color Color)>();
            public string CenterTop = "0", CenterBottom = "Total";

            public RingChart() { SetStyle(ControlStyles.Selectable, false); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int size = Math.Min(Width, Height) - S(8);
                if (size < 40) return;
                var rect = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
                float thick = size * 0.17f;
                var arcRect = RectangleF.Inflate(rect, -thick / 2f, -thick / 2f);

                double total = Segments.Sum(s => s.Value);
                if (total <= 0)
                {
                    using (var pen = new Pen(Ui.Track, thick)) g.DrawEllipse(pen, arcRect);
                }
                else
                {
                    int nonZero = Segments.Count(s => s.Value > 0);
                    float gap = nonZero > 1 ? 3f : 0f;
                    float start = -90f;
                    foreach (var seg in Segments)
                    {
                        if (seg.Value <= 0) continue;
                        float sweep = (float)(seg.Value / total * 360.0);
                        using (var pen = new Pen(seg.Color, thick))
                        {
                            if (nonZero == 1) g.DrawEllipse(pen, arcRect);
                            else if (sweep - gap > 0.5f) g.DrawArc(pen, arcRect, start + gap / 2f, sweep - gap);
                        }
                        start += sweep;
                    }
                }

                var cy = rect.Y + size / 2f;
                Ui.Text(g, CenterTop, Ui.FTitle, Ui.Navy, new Rectangle((int)rect.X, (int)cy - S(26), size, S(34)), Ui.CenterF);
                Ui.Text(g, CenterBottom, Ui.FSub, Ui.Muted, new Rectangle((int)rect.X, (int)cy + S(8), size, S(18)), Ui.CenterF);
            }
        }

        private sealed class SegmentLegend : BufferedControl
        {
            public List<(string Label, int Count, double Pct, Color Color)> Items = new List<(string Label, int Count, double Pct, Color Color)>();

            public SegmentLegend() { SetStyle(ControlStyles.Selectable, false); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (Items.Count == 0) return;

                int rowH = Math.Min(S(58), Height / Items.Count);
                int y0 = (Height - rowH * Items.Count) / 2;

                for (int i = 0; i < Items.Count; i++)
                {
                    var it = Items[i];
                    int y = y0 + i * rowH;
                    int dot = S(10);

                    using (var br = new SolidBrush(it.Color))
                        g.FillEllipse(br, 0, y + S(9), dot, dot);
                    Ui.Text(g, it.Label, Ui.FBody, Ui.Navy, new Rectangle(S(20), y, Width / 2, S(28)));

                    string pctText = "(" + it.Pct.ToString("0.#") + "%)";
                    string cntText = it.Count.ToString();
                    int pw = Ui.Measure(g, pctText, Ui.FSub).Width;
                    int cw = Ui.Measure(g, cntText, Ui.FSemi10).Width;
                    Ui.Text(g, pctText, Ui.FSub, Ui.Muted, new Rectangle(Width - pw - S(4), y, pw + S(4), S(28)), Ui.RightF);
                    Ui.Text(g, cntText, Ui.FSemi10, Ui.Navy, new Rectangle(Width - pw - S(10) - cw - S(4), y, cw + S(4), S(28)), Ui.RightF);

                    var track = new RectangleF(0, y + S(32), Width, S(6));
                    using (var tp = Ui.Round(track, S(3))) using (var tb = new SolidBrush(Ui.Track)) g.FillPath(tb, tp);
                    float fw = (float)(Width * Math.Max(0, Math.Min(100, it.Pct)) / 100.0);
                    if (it.Pct > 0) fw = Math.Max(fw, S(6));
                    if (fw > 0)
                        using (var fp = Ui.Round(new RectangleF(0, track.Y, fw, track.Height), S(3)))
                        using (var fb = new SolidBrush(it.Color))
                            g.FillPath(fb, fp);
                }
            }
        }

        private sealed class ActivityList : BufferedControl
        {
            public List<ActivityItem> Items = new List<ActivityItem>();

            public ActivityList() { SetStyle(ControlStyles.Selectable, false); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (Items.Count == 0)
                {
                    Ui.Text(g, "No recent activity.", Ui.FSub, Ui.Muted, new Rectangle(0, 0, Width, S(28)));
                    return;
                }

                int rowH = S(52);
                int visible = Math.Max(1, Math.Min(Items.Count, Height / rowH));
                for (int i = 0; i < visible; i++)
                {
                    var it = Items[i];
                    int y = i * rowH;

                    if (i > 0)
                        using (var pen = new Pen(Ui.Line)) g.DrawLine(pen, 0, y, Width, y);

                    int d = S(34);
                    Color tone = it.Completed ? Ui.Green : Ui.Accent;
                    Color tint = Ui.Tint(tone, 0.14);
                    using (var br = new SolidBrush(tint))
                        g.FillEllipse(br, 0, y + (rowH - d) / 2, d, d);
                    int gi = S(18);
                    Icons.Draw(g, it.Completed ? IconKind.Check : IconKind.Car,
                        new RectangleF((d - gi) / 2f, y + (rowH - gi) / 2f, gi, gi), tone, tint);

                    int tx = d + S(12);
                    int timeW = S(64);
                    Ui.Text(g, it.Title, Ui.FSemi10, Ui.Navy, new Rectangle(tx, y + S(8), Width - tx - timeW, S(20)));
                    Ui.Text(g, it.Subtitle, Ui.FSmall, Ui.Muted, new Rectangle(tx, y + S(28), Width - tx - timeW, S(18)));
                    Ui.Text(g, it.When, Ui.FSmall, Ui.Faint, new Rectangle(Width - timeW, y + S(8), timeW, S(20)), Ui.RightF);
                }
            }
        }

        private sealed class ActionRow : BufferedControl
        {
            public Color Tone = Ui.Accent;
            public IconKind Icon;
            public string Title = "", ShareText = "", Basis = "", Action = "", Impact = "", MeterCaption = "";
            public Color ShareColor = Ui.Muted;
            public double Meter;
            public double? Tick;
            public bool ShowTopBorder;
            public readonly RoundedButton Ghost, Solid;

            public ActionRow()
            {
                Ghost = new RoundedButton();
                Solid = new RoundedButton { Solid = true };
                Controls.Add(Ghost);
                Controls.Add(Solid);
            }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                int bw = S(138), bh = S(38), gap = S(10), pad = S(24);
                int by = (Height - bh) / 2;
                Solid.SetBounds(Width - pad - bw, by, bw, bh);
                Ghost.SetBounds(Width - pad - bw * 2 - gap, by, bw, bh);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int pad = S(24);
                if (ShowTopBorder)
                    using (var pen = new Pen(Ui.Line)) g.DrawLine(pen, pad, 0, Width - pad, 0);

                int tile = S(46);
                int ty = (Height - tile) / 2;
                Color tint = Ui.Tint(Tone, 0.16);
                using (var path = Ui.Round(new RectangleF(pad, ty, tile, tile), S(12)))
                using (var br = new SolidBrush(tint))
                    g.FillPath(br, path);
                int gi = S(22);
                Icons.Draw(g, Icon, new RectangleF(pad + (tile - gi) / 2f, ty + (tile - gi) / 2f, gi, gi), Tone, tint);

                int tx = pad + tile + S(18);
                int meterW = S(210);
                bool showMeter = Width >= S(940);
                int meterLeft = Ghost.Left - S(28) - meterW;
                int textRight = (showMeter ? meterLeft : Ghost.Left) - S(20);
                int textW = Math.Max(40, textRight - tx);

                bool hasImpact = !string.IsNullOrEmpty(Impact);
                int lineH = S(20);
                int titleH = S(26);
                int lines = 2 + (hasImpact ? 1 : 0);
                int y = (Height - (titleH + lines * lineH)) / 2;

                Ui.Text(g, Title, Ui.FRowTitle, Ui.Navy, new Rectangle(tx, y, textW, titleH));
                if (!string.IsNullOrEmpty(ShareText))
                {
                    int tw = Ui.Measure(g, Title, Ui.FRowTitle).Width;
                    Ui.Text(g, ShareText, Ui.FSub, ShareColor, new Rectangle(tx + tw + S(10), y, Math.Max(10, textW - tw - S(10)), titleH));
                }
                y += titleH;

                int labelW = S(56);
                Ui.Text(g, "Basis", Ui.FSub, Ui.Faint, new Rectangle(tx, y, labelW, lineH));
                Ui.Text(g, Basis, Ui.FSub, Ui.Body, new Rectangle(tx + labelW, y, Math.Max(10, textW - labelW), lineH));
                y += lineH;
                Ui.Text(g, "Action", Ui.FSub, Ui.Faint, new Rectangle(tx, y, labelW, lineH));
                Ui.Text(g, Action, Ui.FSub, Ui.Body, new Rectangle(tx + labelW, y, Math.Max(10, textW - labelW), lineH));
                y += lineH;
                if (hasImpact)
                {
                    Ui.Text(g, "Impact", Ui.FSub, Ui.Faint, new Rectangle(tx, y, labelW, lineH));
                    Ui.Text(g, Impact, Ui.FSub, Ui.Body, new Rectangle(tx + labelW, y, Math.Max(10, textW - labelW), lineH));
                }

                if (showMeter)
                {
                    int my = Height / 2 - S(10);
                    var track = new RectangleF(meterLeft, my, meterW, S(6));
                    using (var tp = Ui.Round(track, S(3))) using (var tb = new SolidBrush(Ui.Track)) g.FillPath(tb, tp);

                    float fw = (float)(meterW * Math.Max(0, Math.Min(100, Meter)) / 100.0);
                    if (Meter > 0) fw = Math.Max(fw, S(6));
                    if (fw > 0)
                        using (var fp = Ui.Round(new RectangleF(meterLeft, my, fw, track.Height), S(3)))
                        using (var fb = new SolidBrush(Tone))
                            g.FillPath(fb, fp);

                    if (Tick.HasValue)
                    {
                        float tkx = meterLeft + (float)(meterW * Math.Max(0, Math.Min(100, Tick.Value)) / 100.0);
                        using (var tkp = new Pen(Ui.Navy, 2f)) g.DrawLine(tkp, tkx, my - S(4), tkx, my + S(10));
                    }

                    Ui.Text(g, MeterCaption, Ui.FSmall, Ui.Muted, new Rectangle(meterLeft, my + S(14), meterW, S(18)));
                }
            }
        }
    }
}