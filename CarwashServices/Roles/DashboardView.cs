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
    public class DashboardView : UserControl
    {
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private DashboardResponseDto _data = new();

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
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

        private static readonly Font FontCustName = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontCustPlate = new Font("Segoe UI", 8.5f);
        private static readonly Font FontStatus = new Font("Segoe UI Semibold", 9.5f);

        private static readonly bool TintBehindText = false;

        private const int MarginX = 32;
        private const int TopMargin = 20;
        private const int SectionGap = 16;
        private const int KpiHeight = 120;
        private const int MiddleRowHeight = 460;
        private const int BottomRowHeight = 320;
        private const int CardTitleStrip = 48;
        private const int CardPad = 16;

        private Panel _root = null!;
        private Panel _kpiRow = null!;
        private Panel _middleRow = null!;
        private Panel _bottomRow = null!;

        private Label _kpiCustomers = null!, _kpiCustomersSub = null!;
        private Label _kpiToday = null!, _kpiTodaySub = null!;
        private Label _kpiInProgress = null!, _kpiInProgressSub = null!;
        private Label _kpiRevenue = null!, _kpiRevenueSub = null!;

        private DataGridView _recentGrid = null!;
        private Panel _followUpList = null!;
        private Panel _staffList = null!;
        private DataGridView _logsGrid = null!;
        private Label _logsEmpty = null!;

        public DashboardView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            InitializeUI();

            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadAsync();
        }

        // ================================================================
        //  NAVIGATION HELPERS
        // ================================================================
        private void NavigateServiceRequests(string status)
        {
            (FindForm() as MainForm)?.NavigateToServiceRequests(
                status: status,
                source: "dashboard");
        }

        private void NavigateServiceRequestsFocused(string status, int requestId)
        {
            (FindForm() as MainForm)?.NavigateToServiceRequests(
                status: status,
                focusRequestId: requestId,
                source: "dashboard");
        }

        private void NavigateCustomers(string segment)
        {
            (FindForm() as MainForm)?.NavigateToCustomers(
                segment: segment,
                source: "dashboard");
        }

        private void NavigateCustomerFocused(int customerId)
        {
            (FindForm() as MainForm)?.NavigateToCustomers(
                segment: "All",
                focusCustomerId: customerId,
                source: "dashboard");
        }

        private void NavigateFollowUps(string status)
        {
            (FindForm() as MainForm)?.NavigateToFollowUps(
                status: status,
                source: "dashboard");
        }

        private void NavigateFollowUpFocused(string status, int followUpId)
        {
            (FindForm() as MainForm)?.NavigateToFollowUps(
                status: status,
                focusFollowUpId: followUpId,
                source: "dashboard");
        }

        private void NavigateReports()
        {
            (FindForm() as MainForm)?.NavigateToModule("View Reports");
        }

        // ================================================================
        //  UI CONSTRUCTION
        // ================================================================
        private void InitializeUI()
        {
            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                AutoScroll = true,
                Padding = new Padding(MarginX, TopMargin, MarginX, TopMargin)
            };
            Controls.Add(_root);
            _root.ClientSizeChanged += (s, e) => Relayout();

            // -------- KPI row --------
            _kpiRow = new Panel { Height = KpiHeight, BackColor = Color.Transparent };
            _root.Controls.Add(_kpiRow);

            _kpiCustomers = AddKpiCard(_kpiRow, "TOTAL CUSTOMERS", out _kpiCustomersSub,
                () => NavigateCustomers("All"));

            _kpiToday = AddKpiCard(_kpiRow, "TODAY'S JOBS", out _kpiTodaySub,
                () => NavigateServiceRequests("All"));

            _kpiInProgress = AddKpiCard(_kpiRow, "IN PROGRESS", out _kpiInProgressSub,
                () => NavigateServiceRequests("InProgress"));

            _kpiRevenue = AddKpiCard(_kpiRow, "REVENUE (THIS MONTH)", out _kpiRevenueSub,
                () => NavigateReports());

            // -------- Middle row --------
            _middleRow = new Panel { BackColor = Color.Transparent };
            _root.Controls.Add(_middleRow);

            // LEFT: Recent Service Requests
            var recentCard = MakeCard(_middleRow, "Recent Service Requests");
            _recentGrid = MakeGrid();
            _recentGrid.RowTemplate.Height = 56;
            AddColumns(_recentGrid,
                ("Id", "ID", 70),
                ("Customer", "CUSTOMER", 200),
                ("Service", "SERVICE", -1),
                ("Scheduled", "SCHEDULED", 120),
                ("Staff", "STAFF", 140),
                ("Status", "STATUS", 120));
            _recentGrid.CellPainting += RecentGrid_CellPainting;
            _recentGrid.CellMouseClick += RecentGrid_CellMouseClick;
            recentCard.Controls.Add(_recentGrid);
            Inset(recentCard, _recentGrid, CardPad, CardTitleStrip, CardPad, CardPad);

            // RIGHT TOP: Follow-Up Queue
            var followCard = MakeCard(_middleRow, "Follow-Up Queue");
            _followUpList = new Panel { BackColor = Color.White, AutoScroll = true };
            followCard.Controls.Add(_followUpList);
            Inset(followCard, _followUpList, CardPad, CardTitleStrip, CardPad, CardPad);

            // RIGHT BOTTOM: Service Staff
            var staffCard = MakeCard(_middleRow, "Service Staff on Duty");
            _staffList = new Panel { BackColor = Color.White, AutoScroll = true };
            staffCard.Controls.Add(_staffList);
            Inset(staffCard, _staffList, CardPad, CardTitleStrip, CardPad, CardPad);

            // -------- Bottom row: Status Logs --------
            _bottomRow = new Panel { BackColor = Color.Transparent };
            _root.Controls.Add(_bottomRow);

            var logsCard = MakeCard(_bottomRow, "Recent SERVICE_STATUS_LOGS");
            _logsGrid = MakeGrid();
            AddColumns(_logsGrid,
                ("LogId", "LOG_ID", 90),
                ("RequestId", "REQUEST_ID", 130),
                ("Status", "STATUS", 140),
                ("UpdatedBy", "UPDATED_BY", 160),
                ("UpdatedAt", "UPDATED_AT", 170),
                ("Notes", "NOTES", -1));
            _logsGrid.CellPainting += LogsGrid_CellPainting;
            _logsGrid.CellMouseClick += LogsGrid_CellMouseClick;
            logsCard.Controls.Add(_logsGrid);
            Inset(logsCard, _logsGrid, CardPad, CardTitleStrip, CardPad, CardPad);

            _logsEmpty = new Label
            {
                Text = "No status changes recorded yet.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                BackColor = Color.White,
                Visible = false
            };
            logsCard.Controls.Add(_logsEmpty);

            _middleRow.Tag = new[] { recentCard, followCard, staffCard };
            _bottomRow.Tag = logsCard;

            Relayout();
        }

        private Label AddKpiCard(Panel parent, string title, out Label subtitle, Action onClick = null)
        {
            var card = new BorderPanel
            {
                BackColor = Color.White,
                Tag = title
            };
            parent.Controls.Add(card);

            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(20, 16),
                AutoSize = true
            });

            var val = new Label
            {
                Text = "0",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(20, 42),
                AutoSize = true
            };
            card.Controls.Add(val);

            subtitle = new Label
            {
                Text = "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(20, 88),
                AutoSize = true
            };
            card.Controls.Add(subtitle);

            if (onClick != null)
            {
                card.Cursor = Cursors.Hand;
                card.Click += (s, e) => onClick();

                foreach (Control child in card.Controls)
                {
                    child.Cursor = Cursors.Hand;
                    child.Click += (s, e) => onClick();
                }
            }

            return val;
        }

        private BorderPanel MakeCard(Panel parent, string title)
        {
            var card = new BorderPanel { BackColor = Color.White };
            parent.Controls.Add(card);
            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(CardPad, 14),
                AutoSize = true,
                BackColor = Color.White
            });
            return card;
        }

        private DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Muted,
                    SelectionBackColor = Color.White,
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 8.5f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0)
                },
                ColumnHeadersHeight = 36,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 44 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ScrollBars = ScrollBars.Vertical,
                AllowUserToOrderColumns = false,
                AllowUserToResizeColumns = false
            };
            return g;
        }

        private static void AddColumns(DataGridView g, params (string Name, string Header, int Width)[] cols)
        {
            foreach (var c in cols)
            {
                if (c.Width < 0)
                {
                    g.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        Name = c.Name,
                        HeaderText = c.Header,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                        MinimumWidth = 100,
                        FillWeight = 100
                    });
                }
                else
                {
                    g.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        Name = c.Name,
                        HeaderText = c.Header,
                        Width = c.Width
                    });
                }
            }
        }

        private static void Inset(Control parent, Control child, int l, int t, int r, int b)
        {
            void Apply() => child.SetBounds(l, t,
                Math.Max(0, parent.ClientSize.Width - l - r),
                Math.Max(0, parent.ClientSize.Height - t - b));
            parent.Resize += (s, e) => Apply();
            Apply();
        }

        // ================================================================
        //  RELAYOUT
        // ================================================================
        private void Relayout()
        {
            if (_root == null) return;
            int w = _root.ClientSize.Width - _root.Padding.Horizontal;
            if (w < 400) return;

            int L = _root.Padding.Left;
            int y = _root.Padding.Top;

            _kpiRow.SetBounds(L, y, w, KpiHeight);
            LayoutKpis(w);
            y += KpiHeight + SectionGap;

            _middleRow.SetBounds(L, y, w, MiddleRowHeight);
            if (_middleRow.Tag is BorderPanel[] cards && cards.Length == 3)
            {
                int leftW = (int)(w * 0.60);
                int rightW = w - leftW - SectionGap;
                cards[0].SetBounds(0, 0, leftW, MiddleRowHeight);
                int halfH = (MiddleRowHeight - SectionGap) / 2;
                cards[1].SetBounds(leftW + SectionGap, 0, rightW, halfH);
                cards[2].SetBounds(leftW + SectionGap, halfH + SectionGap, rightW,
                                   MiddleRowHeight - halfH - SectionGap);
            }
            y += MiddleRowHeight + SectionGap;

            _bottomRow.SetBounds(L, y, w, BottomRowHeight);
            if (_bottomRow.Tag is BorderPanel logsCard)
                logsCard.SetBounds(0, 0, w, BottomRowHeight);
        }

        private void LayoutKpis(int totalWidth)
        {
            var cards = _kpiRow.Controls.OfType<BorderPanel>().ToList();
            int gap = SectionGap;
            if (cards.Count == 0) return;
            int cardW = (totalWidth - gap * (cards.Count - 1)) / cards.Count;
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetBounds(i * (cardW + gap), 0, cardW, KpiHeight);
        }

        // ================================================================
        //  DATA LOAD
        // ================================================================
        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _data = await _http.GetFromJsonAsync<DashboardResponseDto>(
                    $"api/dashboard?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}") ?? new DashboardResponseDto();
                BindData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load dashboard.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void BindData()
        {
            // ---- KPIs ----
            _kpiCustomers.Text = _data.TotalCustomers.ToString();
            _kpiCustomersSub.Text = "Registered";

            _kpiToday.Text = _data.TodayJobs.ToString();
            _kpiTodaySub.Text = "Scheduled today";

            _kpiInProgress.Text = _data.InProgress.ToString();
            _kpiInProgressSub.Text = $"{_data.Pending} pending";

            _kpiRevenue.Text = $"₱{_data.RevenueThisMonth:N0}";
            _kpiRevenue.ForeColor = Green;
            _kpiRevenueSub.Text = "Paid transactions";

            // ---- Recent Service Requests ----
            _recentGrid.SuspendLayout();
            _recentGrid.Rows.Clear();
            foreach (var r in _data.RecentRequests)
            {
                var custCell = r.Customer;
                if (!string.IsNullOrWhiteSpace(r.Plate))
                    custCell += $"\n{r.Plate}";

                int idx = _recentGrid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    r.Service,
                    r.ScheduledDate,
                    string.IsNullOrWhiteSpace(r.AssignedStaff) ? "Unassigned" : r.AssignedStaff,
                    r.Status);

                // Store ids on the row for click-through.
                _recentGrid.Rows[idx].Tag = new RowContext
                {
                    RequestId = r.RequestId,
                    CustomerId = r.CustomerId,
                    Status = r.Status
                };
            }
            _recentGrid.ClearSelection();
            _recentGrid.ResumeLayout();

            BuildFollowUpList();
            BuildStaffList();
            BuildLogs();
        }

        private static void ClearList(Panel p)
        {
            p.SuspendLayout();
            foreach (var c in p.Controls.Cast<Control>().ToList())
            {
                p.Controls.Remove(c);
                c.Dispose();
            }
            p.ResumeLayout();
            p.AutoScrollPosition = new Point(0, 0);
        }

        private void BuildFollowUpList()
        {
            ClearList(_followUpList);

            var items = _data.FollowUpQueue ?? new List<DashboardFollowUpDto>();

            if (items.Count == 0)
            {
                _followUpList.Controls.Add(new Label
                {
                    Text = "No pending follow-ups.",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 9.5f),
                    Location = new Point(4, 4),
                    AutoSize = true
                });
                return;
            }

            int listW = Math.Max(200, _followUpList.ClientSize.Width - 2);
            int y = 0;
            foreach (var f in items)
            {
                var row = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(listW, 46),
                    BackColor = Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Cursor = Cursors.Hand
                };

                var status = MakeStatusLabel(f.Status);
                status.Location = new Point(row.Width - status.Width - 2, (row.Height - status.Height) / 2);
                status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                row.Controls.Add(status);

                int textW = Math.Max(60, row.Width - status.Width - 16);

                var nameLbl = new Label
                {
                    Text = f.Customer,
                    ForeColor = Navy,
                    Font = new Font("Segoe UI Semibold", 10.5f),
                    Location = new Point(0, 2),
                    AutoSize = false,
                    AutoEllipsis = true,
                    Size = new Size(textW, 20),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Cursor = Cursors.Hand
                };
                row.Controls.Add(nameLbl);

                var metaLbl = new Label
                {
                    Text = $"{f.Type} · {f.ScheduledDate}",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 8.5f),
                    Location = new Point(0, 25),
                    AutoSize = false,
                    AutoEllipsis = true,
                    Size = new Size(textW, 16),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Cursor = Cursors.Hand
                };
                row.Controls.Add(metaLbl);

                // Clicking the row → Follow-Ups filtered by that status,
                // focused on the specific follow-up row.
                string capturedStatus = f.Status;
                int capturedFollowUpId = f.FollowUpId;
                row.Click += (s, e) => NavigateFollowUpFocused(capturedStatus, capturedFollowUpId);
                nameLbl.Click += (s, e) => NavigateFollowUpFocused(capturedStatus, capturedFollowUpId);
                metaLbl.Click += (s, e) => NavigateFollowUpFocused(capturedStatus, capturedFollowUpId);
                status.Click += (s, e) => NavigateFollowUpFocused(capturedStatus, capturedFollowUpId);

                _followUpList.Controls.Add(row);
                y += 52;
            }
        }

        private void BuildStaffList()
        {
            ClearList(_staffList);

            var items = _data.ServiceStaff ?? new List<DashboardStaffDto>();

            if (items.Count == 0)
            {
                _staffList.Controls.Add(new Label
                {
                    Text = "No service staff on duty.",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 9.5f),
                    Location = new Point(4, 4),
                    AutoSize = true
                });
                return;
            }

            int listW = Math.Max(200, _staffList.ClientSize.Width - 2);
            int y = 0;
            foreach (var s in items)
            {
                var row = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(listW, 46),
                    BackColor = Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Cursor = Cursors.Hand
                };

                row.Controls.Add(new Label
                {
                    Text = Initials(s.FullName),
                    ForeColor = Color.White,
                    BackColor = Navy,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(34, 34),
                    Location = new Point(0, 6)
                });

                int textW = Math.Max(60, row.Width - 48 - 28);

                row.Controls.Add(new Label
                {
                    Text = s.FullName,
                    ForeColor = Navy,
                    Font = new Font("Segoe UI Semibold", 10.5f),
                    Location = new Point(46, 4),
                    AutoSize = false,
                    AutoEllipsis = true,
                    Size = new Size(textW, 20),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                });

                row.Controls.Add(new Label
                {
                    Text = $"user_id: {s.UserId}",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 8.5f),
                    Location = new Point(46, 24),
                    AutoSize = false,
                    AutoEllipsis = true,
                    Size = new Size(textW, 16),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                });

                var dot = new Panel
                {
                    Size = new Size(10, 10),
                    BackColor = s.IsOnDuty ? Green : Muted,
                    Location = new Point(row.Width - 18, 18),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                dot.Paint += (sp, ev) =>
                {
                    ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var brush = new SolidBrush(((Panel)sp!).BackColor);
                    ev.Graphics.FillEllipse(brush, 0, 0, 9, 9);
                };
                row.Controls.Add(dot);

                // Staff rows route to Manage Users (unchanged behavior).
                row.Click += (s, e) =>
                    (FindForm() as MainForm)?.NavigateToModule("Manage Users");

                _staffList.Controls.Add(row);
                y += 50;
            }
        }

        private void BuildLogs()
        {
            _logsGrid.SuspendLayout();
            _logsGrid.Rows.Clear();

            var items = _data.RecentLogs ?? new List<DashboardLogDto>();

            foreach (var l in items)
            {
                int idx = _logsGrid.Rows.Add(
                    l.LogId,
                    $"#{l.RequestId}",
                    l.Status,
                    l.UpdatedBy,
                    l.UpdatedAt,
                    l.Notes);

                // Store ids on the row for click-through.
                _logsGrid.Rows[idx].Tag = new RowContext
                {
                    RequestId = l.RequestId,
                    CustomerId = l.CustomerId,
                    Status = l.Status
                };
            }

            _logsGrid.ClearSelection();
            _logsGrid.ResumeLayout();

            _logsEmpty.Visible = items.Count == 0;
            if (_logsEmpty.Visible)
            {
                _logsEmpty.Location = new Point(
                    _logsGrid.Left + 12,
                    _logsGrid.Top + _logsGrid.ColumnHeadersHeight + 20);
                _logsEmpty.BringToFront();
            }
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        private static Label MakeStatusLabel(string status)
        {
            var (bg, fg) = StatusColors(status);
            var size = TextRenderer.MeasureText(status, FontStatus,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int padX = TintBehindText ? 6 : 0;
            int padY = TintBehindText ? 3 : 0;

            return new Label
            {
                Text = status,
                ForeColor = fg,
                BackColor = TintBehindText ? bg : Color.White,
                Font = FontStatus,
                TextAlign = TintBehindText ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleRight,
                AutoSize = false,
                Cursor = Cursors.Hand,
                Size = new Size(size.Width + 12 + 2 * padX, size.Height + 6 + 2 * padY)
            };
        }

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Completed" => (GreenSoft, Green),
            "Pending" => (AmberSoft, Amber),
            "InProgress" or "In Progress" or "Assigned" => (BlueSoft, Blue),
            "Cancelled" => (RedSoft, Red),
            "Redeemed" or "Contacted" => (GreenSoft, Green),
            "Scheduled" => (SlateSoft, Slate),
            "Expired" => (RedSoft, Red),
            "Due today" => (AmberSoft, Amber),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        // ================================================================
        //  CELL PAINTING + CLICK
        // ================================================================
        private void RecentGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var name = _recentGrid.Columns[e.ColumnIndex].Name;

            if (name == "Status")
                PaintStatusText(e);
            else if (name == "Customer")
                PaintTwoLine(e, Navy, Muted);
        }

        private void RecentGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var row = _recentGrid.Rows[e.RowIndex];
            if (row.Tag is not RowContext ctx) return;

            var col = _recentGrid.Columns[e.ColumnIndex].Name;

            // Clicking the Customer cell → drill into that customer.
            if (col == "Customer" && ctx.CustomerId > 0)
            {
                NavigateCustomerFocused(ctx.CustomerId);
                return;
            }

            // Any other cell → Service Requests filtered by the row's status
            // and focused on the specific request.
            NavigateServiceRequestsFocused(
                status: string.IsNullOrEmpty(ctx.Status) ? "All" : ctx.Status,
                requestId: ctx.RequestId);
        }

        private void LogsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (_logsGrid.Columns[e.ColumnIndex].Name == "Status")
                PaintStatusText(e);
        }

        private void LogsGrid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var row = _logsGrid.Rows[e.RowIndex];
            if (row.Tag is not RowContext ctx) return;

            var col = _logsGrid.Columns[e.ColumnIndex].Name;

            if (col == "UpdatedBy" && ctx.CustomerId > 0)
            {
                NavigateCustomerFocused(ctx.CustomerId);
                return;
            }

            NavigateServiceRequestsFocused(
                status: string.IsNullOrEmpty(ctx.Status) ? "All" : ctx.Status,
                requestId: ctx.RequestId);
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e, Color topColor, Color bottomColor)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            var parts = text.Split('\n');
            var l1 = parts[0];
            var l2 = parts.Length > 1 ? parts[1] : null;

            var b = e.CellBounds;
            int x = b.X + 12;
            int w = Math.Max(10, b.Width - 20);

            var flags = TextFormatFlags.Left | TextFormatFlags.Top |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                        TextFormatFlags.NoPadding;

            int h1 = FontCustName.Height;
            int h2 = FontCustPlate.Height;

            if (l2 == null)
            {
                int top = b.Y + (b.Height - h1) / 2;
                TextRenderer.DrawText(e.Graphics, l1, FontCustName,
                    new Rectangle(x, top, w, h1), topColor, flags);
            }
            else
            {
                const int gap = 2;
                int top = b.Y + (b.Height - (h1 + gap + h2)) / 2;

                TextRenderer.DrawText(e.Graphics, l1, FontCustName,
                    new Rectangle(x, top, w, h1), topColor, flags);

                TextRenderer.DrawText(e.Graphics, l2, FontCustPlate,
                    new Rectangle(x, top + h1 + gap, w, h2), bottomColor, flags);
            }

            e.Handled = true;
        }

        private static void PaintStatusText(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (bg, fg) = StatusColors(text);

            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontStatus,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 12;
            int maxW = Math.Max(10, b.Width - 20);
            int y = b.Y + (b.Height - size.Height) / 2;

            if (TintBehindText)
            {
                int textW = Math.Min(size.Width, maxW);
                using var br = new SolidBrush(bg);
                e.Graphics.FillRectangle(br, new Rectangle(x - 6, y - 3, textW + 12, size.Height + 6));
            }

            TextRenderer.DrawText(e.Graphics, text, FontStatus,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        // ================================================================
        //  Helper types
        // ================================================================
        private sealed class RowContext
        {
            public int RequestId;
            public int CustomerId;
            public string Status = "";
        }

        private sealed class BorderPanel : Panel
        {
            public BorderPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                BackColor = Color.White;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}