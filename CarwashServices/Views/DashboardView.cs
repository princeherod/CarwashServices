using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CarwashServices.Views
{
    public class DashboardView : UserControl
    {
        private HttpClient _http;
        private DashboardResponseDto _data = new();

        private Panel _root;
        private Panel _kpiRow;
        private Panel _middleRow;
        private Panel _bottomRow;

        // KPI labels
        private Label _kpiCustomers, _kpiCustomersSub;
        private Label _kpiToday, _kpiTodaySub;
        private Label _kpiInProgress, _kpiInProgressSub;
        private Label _kpiRevenue, _kpiRevenueSub;

        // Panels
        private Panel _recentCard;
        private DataGridView _recentGrid;

        private Panel _followUpCard;
        private Panel _followUpList;

        private Panel _staffCard;
        private Panel _staffList;

        private Panel _logsCard;
        private DataGridView _logsGrid;

        // Palette
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

        private const int MarginX = 40;
        private const int TopMargin = 20;
        private const int SectionGap = 20;
        private const int KpiHeight = 130;

        public DashboardView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5180/"),
                Timeout = TimeSpan.FromSeconds(15)
            };

            InitializeUI();
        }

        // ================================================================
        //  UI
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

            // ---- KPI row ----
            _kpiRow = new Panel { Height = KpiHeight, BackColor = Color.Transparent };
            _root.Controls.Add(_kpiRow);

            _kpiCustomers = AddKpiCard(_kpiRow, 0, "TOTAL CUSTOMERS", out _kpiCustomersSub);
            _kpiToday = AddKpiCard(_kpiRow, 1, "TODAY'S JOBS", out _kpiTodaySub);
            _kpiInProgress = AddKpiCard(_kpiRow, 2, "IN PROGRESS", out _kpiInProgressSub);
            _kpiRevenue = AddKpiCard(_kpiRow, 3, "REVENUE (THIS MONTH)", out _kpiRevenueSub);

            // ---- Middle row ----
            _middleRow = new Panel { BackColor = Color.Transparent };
            _root.Controls.Add(_middleRow);

            // Recent Service Requests (left, wide)
            _recentCard = MakeCard(_middleRow, "Recent Service Requests");
            _recentGrid = MakeGrid();
            _recentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "REQUEST_ID", Width = 90 });
            _recentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "CUSTOMER", Width = 180 });
            _recentGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "SERVICE",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });
            _recentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "SCHEDULED_DATE", Width = 130 });
            _recentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Staff", HeaderText = "ASSIGNED_STAFF", Width = 140 });
            _recentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 120 });
            _recentGrid.CellPainting += RecentGrid_CellPainting;
            _recentCard.Controls.Add(_recentGrid);
            Inset(_recentCard, _recentGrid, 20, 50, 20, 20);

            // Follow-Up Queue (top right)
            _followUpCard = MakeCard(_middleRow, "Follow-Up Queue");
            _followUpList = new Panel { BackColor = Color.White };
            _followUpCard.Controls.Add(_followUpList);
            Inset(_followUpCard, _followUpList, 20, 50, 20, 20);

            // Staff (bottom right)
            _staffCard = MakeCard(_middleRow, "Service Staff on Duty");
            _staffList = new Panel { BackColor = Color.White };
            _staffCard.Controls.Add(_staffList);
            Inset(_staffCard, _staffList, 20, 50, 20, 20);

            // ---- Bottom row: Status Logs ----
            _bottomRow = new Panel { BackColor = Color.Transparent };
            _root.Controls.Add(_bottomRow);

            _logsCard = MakeCard(_bottomRow, "Recent SERVICE_STATUS_LOGS");
            _logsGrid = MakeGrid();
            _logsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LogId", HeaderText = "LOG_ID", Width = 90 });
            _logsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestId", HeaderText = "REQUEST_ID", Width = 120 });
            _logsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 140 });
            _logsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedBy", HeaderText = "UPDATED_BY", Width = 180 });
            _logsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedAt", HeaderText = "UPDATED_AT", Width = 190 });
            _logsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Notes",
                HeaderText = "NOTES",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });
            _logsGrid.CellPainting += LogsGrid_CellPainting;
            _logsCard.Controls.Add(_logsGrid);
            Inset(_logsCard, _logsGrid, 20, 50, 20, 20);

            // ---- Layout ----
            _root.ClientSizeChanged += (s, e) => Relayout();
            Load += async (s, e) =>
            {
                Relayout();
                await LoadAsync();
                BeginInvoke(new Action(Relayout));
            };
        }

        private void Relayout()
        {
            if (_root == null) return;
            int fullW = _root.ClientSize.Width;
            int w = fullW - _root.Padding.Horizontal;
            if (w < 400) return;

            int L = _root.Padding.Left;
            int y = _root.Padding.Top;

            // KPI row
            _kpiRow.SetBounds(L, y, w, KpiHeight);
            LayoutKpis(w);
            y += KpiHeight + SectionGap;

            // Middle row: 60% recent | 40% follow-up + staff
            int middleH = 480;
            _middleRow.SetBounds(L, y, w, middleH);
            int leftW = (int)(w * 0.60);
            int rightW = w - leftW - SectionGap;

            _recentCard.SetBounds(0, 0, leftW, middleH);

            int followH = (middleH - SectionGap) / 2;
            _followUpCard.SetBounds(leftW + SectionGap, 0, rightW, followH);
            _staffCard.SetBounds(leftW + SectionGap, followH + SectionGap, rightW, middleH - followH - SectionGap);

            y += middleH + SectionGap;

            // Bottom row: status logs
            int logH = 320;
            _bottomRow.SetBounds(L, y, w, logH);
            _logsCard.SetBounds(0, 0, w, logH);
        }

        private void LayoutKpis(int totalWidth)
        {
            var cards = _kpiRow.Controls.OfType<Panel>().ToList();
            int gap = SectionGap;
            int cardW = (totalWidth - gap * (cards.Count - 1)) / Math.Max(1, cards.Count);
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetBounds(i * (cardW + gap), 0, cardW, KpiHeight);
        }

        // ================================================================
        //  KPI CARD
        // ================================================================
        private Label AddKpiCard(Panel parent, int index, string title, out Label subtitle)
        {
            var card = new CardPanel { Tag = index };
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
                Location = new Point(20, 96),
                AutoSize = true
            };
            card.Controls.Add(subtitle);

            return val;
        }

        // ================================================================
        //  HELPERS
        // ================================================================
        private Panel MakeCard(Panel parent, string title)
        {
            var card = new CardPanel();
            parent.Controls.Add(card);

            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(20, 16),
                AutoSize = true
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
                    Font = new Font("Segoe UI Semibold", 8.5f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0)
                },
                ColumnHeadersHeight = 38,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 48 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(12, 0, 0, 0)
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ScrollBars = ScrollBars.Vertical
            };
            return g;
        }

        private static void Inset(Control parent, Control child, int l, int t, int r, int b)
        {
            void Apply() => child.SetBounds(l, t,
                Math.Max(0, parent.ClientSize.Width - l - r),
                Math.Max(0, parent.ClientSize.Height - t - b));
            parent.Resize += (s, e) => Apply();
            Apply();
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
        //  DATA LOAD
        // ================================================================
        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var resp = await _http.GetFromJsonAsync<DashboardResponseDto>(
                    "api/dashboard?companyId=1") ?? new DashboardResponseDto();
                _data = resp;
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

            // ---- Recent service requests ----
            _recentGrid.SuspendLayout();
            _recentGrid.Rows.Clear();
            foreach (var r in _data.RecentRequests)
            {
                var custCell = r.Customer;
                if (!string.IsNullOrWhiteSpace(r.Plate))
                    custCell += $"\n{r.Plate}";

                _recentGrid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    r.Service,
                    r.ScheduledDate,
                    r.AssignedStaff,
                    r.Status);
            }
            _recentGrid.ResumeLayout();

            // ---- Follow-up queue ----
            BuildFollowUpList();

            // ---- Service staff ----
            BuildStaffList();

            // ---- Logs ----
            _logsGrid.SuspendLayout();
            _logsGrid.Rows.Clear();
            foreach (var l in _data.RecentLogs)
            {
                _logsGrid.Rows.Add(
                    l.LogId,
                    $"#{l.RequestId}",
                    l.Status,
                    l.UpdatedBy,
                    l.UpdatedAt,
                    l.Notes);
            }
            _logsGrid.ResumeLayout();
        }

        private void BuildFollowUpList()
        {
            _followUpList.Controls.Clear();
            _followUpList.AutoScroll = true;

            if (_data.FollowUpQueue.Count == 0)
            {
                _followUpList.Controls.Add(new Label
                {
                    Text = "No pending follow-ups.",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 9.5f),
                    Location = new Point(0, 0),
                    AutoSize = true
                });
                return;
            }

            int y = 0;
            foreach (var f in _data.FollowUpQueue)
            {
                var row = new Panel
                {
                    Location = new Point(0, y),
                    Height = 60,
                    BackColor = Color.White,
                    Width = _followUpList.ClientSize.Width
                };

                row.Controls.Add(new Label
                {
                    Text = f.Customer,
                    ForeColor = Navy,
                    Font = new Font("Segoe UI Semibold", 10.5f),
                    Location = new Point(0, 4),
                    AutoSize = true
                });

                row.Controls.Add(new Label
                {
                    Text = $"{f.Type} · {f.ScheduledDate}",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 8.5f),
                    Location = new Point(0, 26),
                    AutoSize = true
                });

                var badge = MakePill(f.Status);
                badge.Location = new Point(row.Width - badge.Width - 6, 12);
                badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                row.Controls.Add(badge);

                _followUpList.Controls.Add(row);
                y += 66;
            }
        }

        private void BuildStaffList()
        {
            _staffList.Controls.Clear();
            _staffList.AutoScroll = true;

            if (_data.ServiceStaff.Count == 0)
            {
                _staffList.Controls.Add(new Label
                {
                    Text = "No staff assigned.",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 9.5f),
                    Location = new Point(0, 0),
                    AutoSize = true
                });
                return;
            }

            int y = 0;
            foreach (var s in _data.ServiceStaff)
            {
                var row = new Panel
                {
                    Location = new Point(0, y),
                    Height = 56,
                    BackColor = Color.White,
                    Width = _staffList.ClientSize.Width
                };

                var avatar = new Label
                {
                    Text = Initials(s.FullName),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    BackColor = Navy,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(36, 36),
                    Location = new Point(0, 10)
                };
                row.Controls.Add(avatar);

                row.Controls.Add(new Label
                {
                    Text = s.FullName,
                    ForeColor = Navy,
                    Font = new Font("Segoe UI Semibold", 10.5f),
                    Location = new Point(48, 8),
                    AutoSize = true
                });

                row.Controls.Add(new Label
                {
                    Text = $"user_id: {s.UserId}",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 8.5f),
                    Location = new Point(48, 28),
                    AutoSize = true
                });

                var dot = new Panel
                {
                    Size = new Size(10, 10),
                    BackColor = s.IsOnDuty ? Green : Muted,
                    Location = new Point(row.Width - 20, 23),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                dot.Paint += (sp, ev) =>
                {
                    ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var brush = new SolidBrush(((Panel)sp).BackColor);
                    ev.Graphics.FillEllipse(brush, 0, 0, 10, 10);
                };
                row.Controls.Add(dot);

                _staffList.Controls.Add(row);
                y += 62;
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

        private static Label MakePill(string status)
        {
            var (bg, fg) = StatusColors(status);
            var text = status;
            var size = TextRenderer.MeasureText(text, new Font("Segoe UI Semibold", 8.5f));
            return new Label
            {
                Text = text,
                BackColor = bg,
                ForeColor = fg,
                Font = new Font("Segoe UI Semibold", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(size.Width + 20, 24)
            };
        }

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Completed" => (GreenSoft, Green),
            "Pending" => (AmberSoft, Amber),
            "InProgress" or "In Progress" or "Assigned" => (BlueSoft, Blue),
            "Cancelled" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            "Redeemed" or "Contacted" => (GreenSoft, Green),
            "Scheduled" => (Color.FromArgb(0xE8, 0xEA, 0xF6), Color.FromArgb(0x39, 0x49, 0xAB)),
            "Expired" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            "Due today" => (AmberSoft, Amber),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        private void RecentGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var name = _recentGrid.Columns[e.ColumnIndex].Name;

            if (name == "Status")
                PaintBadge(e);
            else if (name == "Customer")
                PaintTwoLine(e);
        }

        private void LogsGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (_logsGrid.Columns[e.ColumnIndex].Name == "Status")
                PaintBadge(e);
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            var parts = text.Split('\n');
            var line1 = parts[0];
            var line2 = parts.Length > 1 ? parts[1] : null;

            var b = e.CellBounds;
            int x = b.X + 12;
            int w = Math.Max(10, b.Width - 20);

            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

            if (line2 == null)
            {
                TextRenderer.DrawText(e.Graphics, line1,
                    new Font("Segoe UI Semibold", 9.5f),
                    new Rectangle(x, b.Y, w, b.Height),
                    Navy, flags);
            }
            else
            {
                int h1 = 16, h2 = 14, gap = 2;
                int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;

                TextRenderer.DrawText(e.Graphics, line1,
                    new Font("Segoe UI Semibold", 9.5f),
                    new Rectangle(x, top, w, h1),
                    Navy, flags);

                TextRenderer.DrawText(e.Graphics, line2,
                    new Font("Segoe UI", 8.5f),
                    new Rectangle(x, top + h1 + gap, w, h2),
                    Muted, flags);
            }

            e.Handled = true;
        }

        private static void PaintBadge(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (bg, fg) = StatusColors(text);
            var font = new Font("Segoe UI Semibold", 8.5f);
            var size = TextRenderer.MeasureText(e.Graphics, text, font,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

            int h = 24;
            int w = Math.Min(size.Width + 22, Math.Max(20, e.CellBounds.Width - 24));
            var rect = new Rectangle(
                e.CellBounds.X + 12,
                e.CellBounds.Y + (e.CellBounds.Height - h) / 2,
                w, h);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1), h / 2))
            using (var br = new SolidBrush(bg))
            {
                e.Graphics.FillPath(br, path);
            }

            TextRenderer.DrawText(e.Graphics, text, font, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            e.Handled = true;
        }
    }
}