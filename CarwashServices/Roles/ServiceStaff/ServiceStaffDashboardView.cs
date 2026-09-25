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
using CarwashServices.Shell;

namespace CarwashServices.Roles.ServiceStaff
{
    /// <summary>
    /// Personalised Service Staff dashboard.
    ///
    /// Layout matches the reference design:
    ///   - Header with title, sign-in line, and day badge
    ///   - 4 KPI tiles (Assigned / In Progress / Completed / Pending Follow-Ups)
    ///   - Today's Assigned Services (empty-state aware)
    ///   - My Customers (empty-state aware)
    ///   - Recent Service Activity (empty-state aware)
    ///   - Follow-Up Approval Status (Pending / Approved / Rejected)
    ///
    /// Every query on the API side filters by AssignedStaffId == staffId.
    /// </summary>
    public class ServiceStaffDashboardView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE4, 0xE9, 0xF1);
        private static readonly Color HeaderBg = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Divider = Color.FromArgb(0xEE, 0xF1, 0xF6);

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
        private static readonly Font FontTitle = new("Segoe UI Semibold", 20f);
        private static readonly Font FontSubtitle = new("Segoe UI", 9.5f);
        private static readonly Font FontBadge = new("Segoe UI", 9f);
        private static readonly Font FontKpiLabel = new("Segoe UI Semibold", 9f);
        private static readonly Font FontKpiValue = new("Segoe UI Semibold", 28f);
        private static readonly Font FontCardTitle = new("Segoe UI Semibold", 11.5f);
        private static readonly Font FontCardHint = new("Segoe UI", 8.5f);
        private static readonly Font FontGridHeader = new("Segoe UI Semibold", 8f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontCellBold = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontCellSub = new("Segoe UI", 8.5f);
        private static readonly Font FontPill = new("Segoe UI Semibold", 9f);
        private static readonly Font FontEmptyTitle = new("Segoe UI Semibold", 10.5f);
        private static readonly Font FontEmptyBody = new("Segoe UI", 9f);

        // ================================================================
        //  Layout
        // ================================================================
        private const int PadX = 24;
        private const int PadTop = 16;
        private const int SectionGap = 16;
        private const int CardPad = 20;
        private const int KpiHeight = 96;
        private const int GridHeaderHeight = 38;
        private const int RowHeight = 52;
        private const int EmptyCardMinHeight = 180;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ================================================================
        //  Fields
        // ================================================================
        private Panel _root = null!;
        private Panel _headerPanel = null!;
        private Label _greeting = null!;
        private Label _subGreeting = null!;
        private Panel _dayBadge = null!;
        private Label _dayBadgeLabel = null!;

        private Panel _kpiRow = null!;
        private Label _kpiAssigned = null!;
        private Label _kpiInProgress = null!;
        private Label _kpiCompleted = null!;
        private Label _kpiFollowUp = null!;

        private Panel _todayCard = null!;
        private Panel _todayHost = null!;
        private DataGridView _todayGrid = null!;
        private EmptyState _todayEmpty = null!;

        private Panel _customersCard = null!;
        private Panel _customersHost = null!;
        private DataGridView _customersGrid = null!;
        private EmptyState _customersEmpty = null!;

        private Panel _activityCard = null!;
        private Panel _activityHost = null!;
        private DataGridView _activityGrid = null!;
        private EmptyState _activityEmpty = null!;

        private Panel _approvalCard = null!;
        private Label _approvalPending = null!;
        private Label _approvalApproved = null!;
        private Label _approvalRejected = null!;

        private bool _loading;

        // ================================================================
        //  Ctor
        // ================================================================
        public ServiceStaffDashboardView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            BuildUi();

            Sidebar.EnableDoubleBuffering(this);

            Resize += (s, e) => LayoutAll();

            Load += async (s, e) => await ReloadAsync();
        }

        // ================================================================
        //  UI
        // ================================================================
        private void BuildUi()
        {
            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                AutoScroll = true,
                Padding = new Padding(PadX, PadTop, PadX, PadTop)
            };
            Controls.Add(_root);

            // ---------- Header ----------
            _headerPanel = new Panel { BackColor = PageBg, Height = 72 };
            _root.Controls.Add(_headerPanel);

            _greeting = new Label
            {
                Text = "My Dashboard",
                ForeColor = Navy,
                Font = FontTitle,
                Location = new Point(0, 0),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _headerPanel.Controls.Add(_greeting);

            _subGreeting = new Label
            {
                Text = "Signed in as — showing only your assigned work.",
                ForeColor = Muted,
                Font = FontSubtitle,
                Location = new Point(2, 40),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _headerPanel.Controls.Add(_subGreeting);

            _dayBadge = new Panel
            {
                BackColor = Color.White,
                Size = new Size(120, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _dayBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, _dayBadge.Width - 1, _dayBadge.Height - 1), 16);
                using var fill = new SolidBrush(Color.White);
                using var pen = new Pen(CardBorder);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            };
            _headerPanel.Controls.Add(_dayBadge);

            _dayBadgeLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = DateTime.Now.ToString("ddd, MMM d"),
                ForeColor = Navy,
                Font = FontBadge,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            _dayBadge.Controls.Add(_dayBadgeLabel);

            // ---------- KPI row ----------
            _kpiRow = new Panel { BackColor = PageBg, Height = KpiHeight };
            _root.Controls.Add(_kpiRow);

            _kpiAssigned = AddKpiTile(_kpiRow, "MY ASSIGNED REQUESTS", Accent, AccentSoft, KpiIconKind.Clipboard,
    () => (FindForm() as MainForm)?.NavigateToAssignedRequests("All statuses"));

            _kpiInProgress = AddKpiTile(_kpiRow, "IN PROGRESS", Amber, AmberSoft, KpiIconKind.Clock,
                () => (FindForm() as MainForm)?.NavigateToAssignedRequests("InProgress"));

            _kpiCompleted = AddKpiTile(_kpiRow, "COMPLETED", Green, GreenSoft, KpiIconKind.Check,
                () => (FindForm() as MainForm)?.NavigateToAssignedRequests("Completed"));

            _kpiFollowUp = AddKpiTile(_kpiRow, "PENDING FOLLOW-UPS", Slate, SlateSoft, KpiIconKind.Mail,
                () => (FindForm() as MainForm)?.NavigateToModule("Follow-Ups / Reminders"));

            // ---------- Today's Assigned Services ----------
            _todayCard = MakeCard("Today's Assigned Services", hasHint: true);
            _root.Controls.Add(_todayCard);

            _todayHost = new Panel { BackColor = Color.White };
            _todayCard.Controls.Add(_todayHost);

            _todayGrid = MakeGrid();
            AddGridColumns(_todayGrid,
                ("Customer", "CUSTOMER", 260, true),
                ("Service", "SERVICE", 220, true),
                ("Scheduled", "SCHEDULED", 160, false),
                ("Priority", "PRIORITY", 110, false),
                ("Status", "STATUS", 130, false));
            _todayGrid.CellPainting += Grid_CellPainting;
            _todayHost.Controls.Add(_todayGrid);

            _todayEmpty = new EmptyState(
                KpiIconKind.Calendar,
                "Nothing scheduled for today",
                "New assignments from your manager will show up here automatically.");
            _todayHost.Controls.Add(_todayEmpty);
            _todayEmpty.BringToFront();

            // ---------- My Customers ----------
            _customersCard = MakeCard("My Customers");
            _root.Controls.Add(_customersCard);

            _customersHost = new Panel { BackColor = Color.White };
            _customersCard.Controls.Add(_customersHost);

            _customersGrid = MakeGrid();
            AddGridColumns(_customersGrid,
                ("Customer", "CUSTOMER", 300, true),
                ("Service", "SERVICE", 260, true),
                ("Scheduled", "DATE", 180, false),
                ("Status", "STATUS", 130, false));
            _customersGrid.CellPainting += Grid_CellPainting;
            _customersHost.Controls.Add(_customersGrid);

            _customersEmpty = new EmptyState(
                KpiIconKind.Users,
                "No customers assigned yet",
                "Customers you've serviced will build up your list here.");
            _customersHost.Controls.Add(_customersEmpty);
            _customersEmpty.BringToFront();

            // ---------- Recent Service Activity ----------
            _activityCard = MakeCard("Recent Service Activity");
            _root.Controls.Add(_activityCard);

            _activityHost = new Panel { BackColor = Color.White };
            _activityCard.Controls.Add(_activityHost);

            _activityGrid = MakeGrid();
            AddGridColumns(_activityGrid,
                ("Customer", "CUSTOMER", 260, true),
                ("Service", "SERVICE", 220, true),
                ("Status", "STATUS", 160, false),
                ("Updated", "UPDATED AT", 180, false));
            _activityGrid.CellPainting += Grid_CellPainting;
            _activityHost.Controls.Add(_activityGrid);

            _activityEmpty = new EmptyState(
                KpiIconKind.Clock,
                "No recent activity",
                "Status updates you log on service requests will appear here.");
            _activityHost.Controls.Add(_activityEmpty);
            _activityEmpty.BringToFront();

            // ---------- Follow-Up Approval Status ----------
            _approvalCard = MakeCard("Follow-Up Approval Status");
            _root.Controls.Add(_approvalCard);

            _approvalPending = AddApprovalTile(_approvalCard, "PENDING", Amber, 20);
            _approvalApproved = AddApprovalTile(_approvalCard, "APPROVED", Green, 220);
            _approvalRejected = AddApprovalTile(_approvalCard, "REJECTED", Red, 420);
        }

        // ================================================================
        //  KPI tile
        // ================================================================
        private enum KpiIconKind { Clipboard, Clock, Check, Mail, Calendar, Users }

        private Label AddKpiTile(Panel parent, string title, Color accent, Color soft, KpiIconKind icon,
                                  Action onClick = null)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
            };

            bool hover = false;

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Color bg = hover && onClick != null
                    ? Color.FromArgb(0xF7, 0xFB, 0xFF)
                    : Color.White;

                using (var path = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (var fill = new SolidBrush(bg))
                using (var pen = new Pen(hover && onClick != null ? accent : CardBorder,
                                         hover && onClick != null ? 1.5f : 1f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }

                using (var stripe = new SolidBrush(accent))
                {
                    var r = new Rectangle(0, 0, 4, card.Height);
                    e.Graphics.FillRectangle(stripe, r);
                }
            };
            parent.Controls.Add(card);

            // Title
            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Muted,
                Font = FontKpiLabel,
                Location = new Point(20, 14),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            // Icon chip
            var chip = new Panel
            {
                Size = new Size(30, 30),
                BackColor = soft,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            chip.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, chip.Width - 1, chip.Height - 1), 8);
                using var fill = new SolidBrush(soft);
                e.Graphics.FillPath(fill, path);

                DrawIcon(e.Graphics, icon,
                    new RectangleF(7, 7, 16, 16),
                    accent);
            };
            card.Controls.Add(chip);

            chip.Location = new Point(card.Width - chip.Width - 16, 14);
            card.Resize += (s, e) => chip.Location = new Point(card.Width - chip.Width - 16, 14);

            // Value
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

            // ---- Hover / click wiring ----
            if (onClick != null)
            {
                void Enter(object? s, EventArgs e) { hover = true; card.Invalidate(); }
                void Leave(object? s, EventArgs e) { hover = false; card.Invalidate(); }
                void Click(object? s, EventArgs e) => onClick();

                card.MouseEnter += Enter;
                card.MouseLeave += Leave;
                card.Click += Click;

                // Forward events from child controls
                void Bind(Control c)
                {
                    c.Cursor = Cursors.Hand;
                    c.MouseEnter += Enter;
                    c.MouseLeave += Leave;
                    c.Click += Click;
                    foreach (Control inner in c.Controls) Bind(inner);
                }
                foreach (Control child in card.Controls) Bind(child);
            }

            return val;
        }

        // ================================================================
        //  Cards
        // ================================================================
        private Panel MakeCard(string title, bool hasHint = false)
        {
            var card = new Panel { BackColor = Color.White, Height = EmptyCardMinHeight };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10);
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawPath(pen, path);
            };

            card.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Navy,
                Font = FontCardTitle,
                Location = new Point(CardPad, 14),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            if (hasHint)
            {
                var hint = new Label
                {
                    Text = "Auto-refreshes",
                    ForeColor = Faint,
                    Font = FontCardHint,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                card.Controls.Add(hint);
                hint.Location = new Point(card.Width - hint.PreferredWidth - CardPad, 16);
                card.Resize += (s, e) =>
                    hint.Location = new Point(card.Width - hint.PreferredWidth - CardPad, 16);
            }

            return card;
        }

        private DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Divider,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderBg,
                    ForeColor = Muted,
                    SelectionBackColor = HeaderBg,
                    SelectionForeColor = Muted,
                    Font = FontGridHeader,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0)
                },
                ColumnHeadersHeight = GridHeaderHeight,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = RowHeight },
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ScrollBars = ScrollBars.Vertical
            };
            return g;
        }

        private static void AddGridColumns(DataGridView g,
            params (string Name, string Header, int Width, bool Fill)[] cols)
        {
            foreach (var c in cols)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = c.Name,
                    HeaderText = c.Header
                };
                if (c.Fill)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    col.MinimumWidth = c.Width;
                    col.FillWeight = c.Width;
                }
                else
                {
                    col.Width = c.Width;
                }
                g.Columns.Add(col);
            }
        }

        private Label AddApprovalTile(Panel parent, string caption, Color accent, int left)
        {
            var tile = new Panel
            {
                Location = new Point(left, 46),
                Size = new Size(180, 74),
                BackColor = NeutralSoft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            tile.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, tile.Width - 1, tile.Height - 1), 10);
                using var fill = new SolidBrush(tile.BackColor);
                e.Graphics.FillPath(fill, path);
            };
            parent.Controls.Add(tile);

            tile.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(14, 10),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            // Coloured dot on the right of the caption
            var dot = new Panel
            {
                Size = new Size(10, 10),
                Location = new Point(14, 34),
                BackColor = Color.Transparent,
                Tag = accent
            };
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var b = new SolidBrush((Color)((Panel)s!).Tag!);
                e.Graphics.FillEllipse(b, 0, 0, 9, 9);
            };
            tile.Controls.Add(dot);

            var val = new Label
            {
                Text = "0",
                ForeColor = accent,
                Font = new Font("Segoe UI Semibold", 18f),
                Location = new Point(30, 26),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            tile.Controls.Add(val);

            return val;
        }

        // ================================================================
        //  Icons
        // ================================================================
        private static void DrawIcon(Graphics g, KpiIconKind kind, RectangleF b, Color color)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(b.X, b.Y);
            g.ScaleTransform(b.Width / 20f, b.Height / 20f);

            using var pen = new Pen(color, 1.7f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            switch (kind)
            {
                case KpiIconKind.Clipboard:
                    g.DrawRectangle(pen, 4, 3, 12, 15);
                    g.DrawRectangle(pen, 7, 1, 6, 3);
                    g.DrawLine(pen, 7, 9, 13, 9);
                    g.DrawLine(pen, 7, 13, 13, 13);
                    break;

                case KpiIconKind.Clock:
                    g.DrawEllipse(pen, 2, 2, 16, 16);
                    g.DrawLine(pen, 10, 5, 10, 10);
                    g.DrawLine(pen, 10, 10, 13, 12);
                    break;

                case KpiIconKind.Check:
                    g.DrawEllipse(pen, 2, 2, 16, 16);
                    g.DrawLines(pen, new[]
                    {
                        new PointF(6, 10),
                        new PointF(9, 13),
                        new PointF(14, 7)
                    });
                    break;

                case KpiIconKind.Mail:
                    g.DrawRectangle(pen, 2, 4, 16, 12);
                    g.DrawLines(pen, new[]
                    {
                        new PointF(2.5f, 5.5f),
                        new PointF(10, 11),
                        new PointF(17.5f, 5.5f)
                    });
                    break;

                case KpiIconKind.Calendar:
                    g.DrawRectangle(pen, 2, 3, 16, 15);
                    g.DrawLine(pen, 2, 7, 18, 7);
                    g.DrawLine(pen, 6, 1, 6, 4);
                    g.DrawLine(pen, 14, 1, 14, 4);
                    break;

                case KpiIconKind.Users:
                    g.DrawEllipse(pen, 3, 3, 6, 6);
                    g.DrawArc(pen, 1, 9, 10, 9, 180, 180);
                    g.DrawEllipse(pen, 11, 3, 6, 6);
                    g.DrawArc(pen, 9, 9, 10, 9, 180, 180);
                    break;
            }

            g.Restore(state);
        }

        // ================================================================
        //  Empty-state card overlay
        // ================================================================
        private sealed class EmptyState : Panel
        {
            private readonly KpiIconKind _icon;
            private readonly string _title;
            private readonly string _body;

            public EmptyState(KpiIconKind icon, string title, string body)
            {
                _icon = icon;
                _title = title;
                _body = body;

                Dock = DockStyle.Fill;
                BackColor = Color.White;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int iconSize = 42;
                int titleH = FontEmptyTitle.Height + 4;
                int bodyH = FontEmptyBody.Height * 2 + 4;
                int totalH = iconSize + 12 + titleH + 6 + bodyH;

                int top = Math.Max(6, (Height - totalH) / 2);
                int iconLeft = (Width - iconSize) / 2;

                // Soft circular background behind the icon
                using (var circle = new SolidBrush(Color.FromArgb(0xF1, 0xF4, 0xF9)))
                    g.FillEllipse(circle, iconLeft - 10, top - 10, iconSize + 20, iconSize + 20);

                DrawIcon(g, _icon,
                    new RectangleF(iconLeft, top, iconSize, iconSize),
                    Faint);

                var titleRect = new Rectangle(0, top + iconSize + 12, Width, titleH);
                TextRenderer.DrawText(g, _title, FontEmptyTitle, titleRect, Navy,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

                var bodyRect = new Rectangle(40, top + iconSize + 12 + titleH + 2, Width - 80, bodyH);
                TextRenderer.DrawText(g, _body, FontEmptyBody, bodyRect, Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        // ================================================================
        //  Layout
        // ================================================================
        private void LayoutAll()
        {
            if (_root == null || _root.ClientSize.Width <= 0) return;

            int w = _root.ClientSize.Width - _root.Padding.Horizontal;
            int L = _root.Padding.Left;
            int y = _root.Padding.Top;

            _headerPanel.SetBounds(L, y, w, 72);
            _dayBadge.Location = new Point(w - _dayBadge.Width, 8);
            y += 72 + 8;

            _kpiRow.SetBounds(L, y, w, KpiHeight);
            LayoutKpis();
            y += KpiHeight + SectionGap;

            LayoutCard(_todayCard, _todayHost, _todayGrid, _todayEmpty, L, y, w, EmptyCardMinHeight);
            y += _todayCard.Height + SectionGap;

            LayoutCard(_customersCard, _customersHost, _customersGrid, _customersEmpty, L, y, w, EmptyCardMinHeight);
            y += _customersCard.Height + SectionGap;

            LayoutCard(_activityCard, _activityHost, _activityGrid, _activityEmpty, L, y, w, EmptyCardMinHeight);
            y += _activityCard.Height + SectionGap;

            // Approval card: fixed height
            _approvalCard.SetBounds(L, y, w, 148);
            LayoutApprovalTiles(w);
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

        private void LayoutCard(
            Panel card, Panel host, DataGridView grid, EmptyState empty,
            int left, int top, int width, int minHeight)
        {
            int contentTop = 46;
            int contentH = Math.Max(1, card.Height - contentTop - 16);

            // Compute needed height based on row count
            int rowCount = grid.Rows.Count;
            int needed = contentTop
                       + grid.ColumnHeadersHeight
                       + Math.Max(2, rowCount) * RowHeight
                       + 24;
            int h = Math.Max(minHeight, Math.Min(needed, 420));

            card.SetBounds(left, top, width, h);

            host.SetBounds(CardPad - 4, contentTop, width - (CardPad - 4) * 2, h - contentTop - 12);

            grid.SetBounds(0, 0, host.ClientSize.Width, host.ClientSize.Height);
            empty.SetBounds(0, 0, host.ClientSize.Width, host.ClientSize.Height);

            bool isEmpty = rowCount == 0;
            empty.Visible = isEmpty;
            grid.Visible = !isEmpty;
        }

        private void LayoutApprovalTiles(int cardWidth)
        {
            // Keep the three tiles left-aligned; no stretching.
            // Their positions were set in AddApprovalTile.
        }

        // ================================================================
        //  Load
        // ================================================================
        private async Task ReloadAsync()
        {
            if (_loading) return;
            _loading = true;

            try
            {
                int staffId = SessionUser.UserId;
                if (staffId <= 0)
                {
                    _subGreeting.Text = "No signed-in user. Please log in again.";
                    return;
                }

                _subGreeting.Text =
                    $"Signed in as {SessionUser.FullName} — showing only your assigned work.";

                Cursor = Cursors.WaitCursor;

                var resp = await _http.GetFromJsonAsync<StaffDashboardResponse>(
                    $"api/dashboard/staff?companyId=1&staffId={staffId}");

                if (resp is null) return;

                ApplyData(resp);
                LayoutAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load your dashboard.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _loading = false;
            }
        }

        private void ApplyData(StaffDashboardResponse data)
        {
            _kpiAssigned.Text = data.AssignedRequests.ToString();
            _kpiInProgress.Text = data.InProgress.ToString();
            _kpiCompleted.Text = data.Completed.ToString();
            _kpiFollowUp.Text = data.FollowUpApproval.Pending.ToString();

            // ---- Today's services ----
            _todayGrid.SuspendLayout();
            _todayGrid.Rows.Clear();
            foreach (var r in data.TodaysRequests)
            {
                var cell = string.IsNullOrWhiteSpace(r.Plate)
                    ? r.Customer
                    : $"{r.Customer}\n{r.Plate}";

                _todayGrid.Rows.Add(cell, r.Service, r.ScheduledDate, r.Priority, r.Status);
            }
            _todayGrid.ResumeLayout();

            // ---- My customers ----
            _customersGrid.SuspendLayout();
            _customersGrid.Rows.Clear();
            foreach (var c in data.MyCustomers)
            {
                var cell = string.IsNullOrWhiteSpace(c.Plate)
                    ? c.Customer
                    : $"{c.Customer}\n{c.Plate}";

                _customersGrid.Rows.Add(cell, c.Service, c.ScheduledDate, c.Status);
            }
            _customersGrid.ResumeLayout();

            // ---- Recent activity ----
            _activityGrid.SuspendLayout();
            _activityGrid.Rows.Clear();
            foreach (var a in data.RecentActivity)
            {
                _activityGrid.Rows.Add(a.Customer, a.Service, a.Status, a.UpdatedAt);
            }
            _activityGrid.ResumeLayout();

            // ---- Follow-up approval ----
            _approvalPending.Text = data.FollowUpApproval.Pending.ToString();
            _approvalApproved.Text = data.FollowUpApproval.Approved.ToString();
            _approvalRejected.Text = data.FollowUpApproval.Rejected.ToString();

            // ---- Day badge ----
            _dayBadgeLabel.Text = DateTime.Now.ToString("ddd, MMM d");
        }

        // ================================================================
        //  Grid painting
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = (DataGridView)sender!;
            var colName = grid.Columns[e.ColumnIndex].Name;

            switch (colName)
            {
                case "Customer":
                    PaintTwoLine(e);
                    break;
                case "Status":
                    PaintStatusPill(e, Convert.ToString(e.Value) ?? "");
                    break;
                case "Priority":
                    PaintPriorityPill(e, Convert.ToString(e.Value) ?? "Normal");
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
            int x = b.X + 12;
            int w = Math.Max(10, b.Width - 20);
            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

            if (l2 == null)
            {
                TextRenderer.DrawText(e.Graphics, l1, FontCellBold,
                    new Rectangle(x, b.Y, w, b.Height), Navy, flags);
            }
            else
            {
                int h1 = FontCellBold.Height, h2 = FontCellSub.Height, gap = 2;
                int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;
                TextRenderer.DrawText(e.Graphics, l1, FontCellBold,
                    new Rectangle(x, top, w, h1), Navy, flags);
                TextRenderer.DrawText(e.Graphics, l2, FontCellSub,
                    new Rectangle(x, top + h1 + gap, w, h2), Muted, flags);
            }

            e.Handled = true;
        }

        private static void PaintStatusPill(DataGridViewCellPaintingEventArgs e, string text)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            if (string.IsNullOrWhiteSpace(text)) { e.Handled = true; return; }

            var (_, fg) = StatusColors(text);
            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontPill,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 12;
            int maxW = Math.Max(10, b.Width - 20);
            int y = b.Y + (b.Height - size.Height) / 2;

            TextRenderer.DrawText(e.Graphics, text, FontPill,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private static void PaintPriorityPill(DataGridViewCellPaintingEventArgs e, string text)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            if (string.IsNullOrWhiteSpace(text)) { e.Handled = true; return; }

            var (_, fg) = PriorityColors(text);
            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontPill,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 12;
            int maxW = Math.Max(10, b.Width - 20);
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

        // ================================================================
        //  Geometry helper
        // ================================================================
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return path;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ================================================================
        //  DTOs (mirror the API JSON exactly)
        // ================================================================
        public class StaffDashboardResponse
        {
            public int StaffId { get; set; }
            public int AssignedRequests { get; set; }
            public int InProgress { get; set; }
            public int Completed { get; set; }
            public List<RequestRow> PendingRequests { get; set; } = new();
            public List<RequestRow> TodaysRequests { get; set; } = new();
            public List<CustomerRow> MyCustomers { get; set; } = new();
            public List<ActivityRow> RecentActivity { get; set; } = new();
            public FollowUpApproval FollowUpApproval { get; set; } = new();
            public string GeneratedAt { get; set; } = "";
        }

        public class RequestRow
        {
            public int RequestId { get; set; }
            public int CustomerId { get; set; }
            public string Customer { get; set; } = "";
            public string Plate { get; set; } = "";
            public int ServiceId { get; set; }
            public string Service { get; set; } = "";
            public decimal ServicePrice { get; set; }
            public string ScheduledDate { get; set; } = "";
            public string CompletedDate { get; set; } = "";
            public string Priority { get; set; } = "Normal";
            public string Status { get; set; } = "Pending";
            public string Notes { get; set; } = "";
        }

        public class CustomerRow
        {
            public int CustomerId { get; set; }
            public string Customer { get; set; } = "";
            public string Plate { get; set; } = "";
            public string Service { get; set; } = "";
            public string ScheduledDate { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public class ActivityRow
        {
            public int LogId { get; set; }
            public int RequestId { get; set; }
            public string Customer { get; set; } = "";
            public string Service { get; set; } = "";
            public string Status { get; set; } = "";
            public string UpdatedBy { get; set; } = "";
            public string UpdatedAt { get; set; } = "";
            public string Notes { get; set; } = "";
        }

        public class FollowUpApproval
        {
            public int Pending { get; set; }
            public int Approved { get; set; }
            public int Rejected { get; set; }
        }
    }
}