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

namespace CarwashServices.Roles.ServiceStaff
{
    /// <summary>
    /// Service Staff → Update Service Status.
    ///
    /// Left column lists the staff member's active assigned requests.
    /// Right column shows details, progress timeline, update form, and
    /// the full status history for the selected request.
    /// </summary>
    public class UpdateServiceStatusView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Divider = Color.FromArgb(0xEE, 0xF1, 0xF6);

        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color AmberSoft = Color.FromArgb(0xFF, 0xF4, 0xDB);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color NeutralSoft = Color.FromArgb(0xEE, 0xF1, 0xF6);
        private static readonly Color StepDone = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color StepCurrent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color StepUpcoming = Color.FromArgb(0xEC, 0xF1, 0xF8);
        private static readonly Color StepConnector = Color.FromArgb(0xE6, 0xEC, 0xF3);

        private static readonly Font FontSectionTitle = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontBigName = new("Segoe UI Semibold", 15f);
        private static readonly Font FontBody = new("Segoe UI", 9.5f);
        private static readonly Font FontBodyBold = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontSmall = new("Segoe UI", 8.5f);
        private static readonly Font FontSmallBold = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontPill = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontStepLabel = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontPrice = new("Segoe UI Semibold", 14f);

        // ================================================================
        //  Layout
        // ================================================================
        private const int PadX = 24;
        private const int TopMargin = 16;
        private const int SectionGap = 14;

        // Header Y positions
        private const int BreadcrumbY = 0;
        private const int TitleY = 24;
        private const int SubtitleY = 66;
        private const int LayoutTopY = 112;

        // Left column — widened so the full date/time line fits on each card
        private const int LeftColWidth = 480;
        private const int RequestCardHeight = 100;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ---- Data ----
        private List<ServiceRequestDto> _myRequests = new();
        private ServiceRequestDto? _selected;
        private List<ServiceStatusHistoryRowDto> _history = new();
        private List<TenantCustomerDto> _customers = new();
        private List<ProductDto> _services = new();
        private List<UserDto> _users = new();

        private Dictionary<int, TenantCustomerDto> _custById = new();
        private Dictionary<int, ProductDto> _svcById = new();
        private Dictionary<int, UserDto> _userById = new();

        // ---- UI ----
        private Panel _contentPanel = null!;
        private TableLayoutPanel _layout = null!;
        private FlowLayoutPanel _requestsPanel = null!;
        private Panel _detailHost = null!;
        private Panel _emptyDetail = null!;

        // Detail controls (rebuilt each time a request is selected)
        private ComboBox _statusCombo = null!;
        private TextBox _notesBox = null!;
        private Button _saveBtn = null!;
        private Label _validationLbl = null!;
        private DataGridView _historyGrid = null!;

        public UpdateServiceStatusView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            BuildUi();

            Sidebar.EnableDoubleBuffering(this);

            CarwashServices.Auth.SessionUser.BranchChanged += () =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    Invoke(async () => await LoadAllAsync());
                }
            };

            Load += async (s, e) => await LoadAllAsync();
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
                Padding = new Padding(PadX, TopMargin, PadX, TopMargin)
            };
            Controls.Add(_contentPanel);

            // Header
            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Update Service Status",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, BreadcrumbY),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Update Service Status",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, TitleY),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Update the progress of services assigned to you.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(0, SubtitleY),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            // ---- Two-column body ----
            _layout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LeftColWidth));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _contentPanel.Controls.Add(_layout);

            // ---- Left card: My Assigned Services ----
            var leftCard = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, SectionGap, 0)
            };
            leftCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, leftCard.Width - 1, leftCard.Height - 1);
            };
            _layout.Controls.Add(leftCard, 0, 0);

            leftCard.Controls.Add(new Label
            {
                Text = "My Assigned Services",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11f),
                Location = new Point(16, 14),
                AutoSize = true,
                BackColor = Color.White
            });

            var requestsHost = new Panel
            {
                Location = new Point(0, 46),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            leftCard.Controls.Add(requestsHost);

            _requestsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(8, 0, 8, 8)
            };
            requestsHost.Controls.Add(_requestsPanel);

            requestsHost.Resize += (s, e) =>
            {
                _requestsPanel.Width = requestsHost.ClientSize.Width;
                // Resize each card so it spans the new column width.
                foreach (Control c in _requestsPanel.Controls)
                {
                    if (c is Panel card)
                        card.Width = Math.Max(200, _requestsPanel.ClientSize.Width - 24);
                }
            };

            // ---- Right column: detail host ----
            _detailHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = true
            };
            _layout.Controls.Add(_detailHost, 1, 0);

            _emptyDetail = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            _emptyDetail.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _emptyDetail.Width - 1, _emptyDetail.Height - 1);
            };
            _emptyDetail.Controls.Add(new Label
            {
                Text = "Select a service from the list to view and update it.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            _detailHost.Controls.Add(_emptyDetail);

            _contentPanel.Resize += (s, e) => ApplyLayout();
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (_contentPanel == null || _layout == null) return;
            int w = _contentPanel.ClientSize.Width - _contentPanel.Padding.Horizontal;
            int h = _contentPanel.ClientSize.Height - _contentPanel.Padding.Vertical;
            if (w < 300) return;
            _layout.SetBounds(0, LayoutTopY, w, Math.Max(200, h - LayoutTopY));
        }

        // ================================================================
        //  Load
        // ================================================================
        private async Task LoadAllAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                int staffId = SessionUser.UserId;
                if (staffId <= 0)
                {
                    MessageBox.Show("No signed-in user.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var companyId = SessionUser.CurrentCompanyId;
                var branchId = SessionUser.CurrentBranchId;
                var branchQuery = branchId.HasValue && branchId.Value > 0 ? $"?branchId={branchId.Value}" : "";
                var userBranchQuery = branchId.HasValue && branchId.Value > 0 ? $"&branchId={branchId.Value}" : "";
                var reqBranchQuery = branchId.HasValue && branchId.Value > 0 ? $"&branchId={branchId.Value}" : "";

                var reqsT = _http.GetFromJsonAsync<List<ServiceRequestDto>>($"api/service-requests?companyId={companyId}{reqBranchQuery}&assignedStaffId={staffId}");
                var custsT = _http.GetFromJsonAsync<List<TenantCustomerDto>>($"api/tenant/{companyId}/tenant-customers{branchQuery}");
                var svcsT = _http.GetFromJsonAsync<List<ProductDto>>($"api/tenant/{companyId}/products{branchQuery}");
                var usersT = _http.GetFromJsonAsync<List<UserDto>>($"api/users?companyId={companyId}{userBranchQuery}");

                await Task.WhenAll(reqsT, custsT, svcsT, usersT);

                var allRequests = reqsT.Result ?? new();
                _customers = custsT.Result ?? new();
                _services = svcsT.Result ?? new();
                _users = usersT.Result ?? new();

                _custById = _customers.ToDictionary(c => c.TenantCustomerId);
                _svcById = _services.ToDictionary(s => s.ProductId);
                _userById = _users.ToDictionary(u => u.UserId);

                _myRequests = allRequests
                    .Where(r => !r.IsArchived && r.AssignedStaffId == staffId)
                    .Where(r => !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.ScheduledDate ?? r.RequestedDate)
                    .ToList();

                RenderRequestsList();

                if (_selected != null &&
                    !_myRequests.Any(r => r.RequestId == _selected.RequestId))
                {
                    _selected = null;
                    _detailHost.Controls.Clear();
                    _detailHost.Controls.Add(_emptyDetail);
                    _emptyDetail.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load your assigned services.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void RenderRequestsList()
        {
            _requestsPanel.SuspendLayout();
            _requestsPanel.Controls.Clear();

            if (_myRequests.Count == 0)
            {
                _requestsPanel.Controls.Add(new Label
                {
                    Text = "You have no active assigned services.",
                    ForeColor = Muted,
                    Font = new Font("Segoe UI", 10f),
                    AutoSize = false,
                    Width = Math.Max(120, _requestsPanel.ClientSize.Width - 16),
                    Height = 60,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0, 20, 0, 0)
                });
                _requestsPanel.ResumeLayout();
                return;
            }

            foreach (var r in _myRequests)
            {
                var card = BuildRequestCard(r);
                card.Margin = new Padding(0, 0, 0, 8);
                card.Width = Math.Max(200, _requestsPanel.ClientSize.Width - 24);
                _requestsPanel.Controls.Add(card);
            }

            _requestsPanel.ResumeLayout();
        }

        private Panel BuildRequestCard(ServiceRequestDto r)
        {
            var cust = _custById.TryGetValue(r.CustomerId, out var c) ? c : null;
            var svc = _svcById.TryGetValue(r.ServiceId, out var s) ? s : null;

            bool isSelected = _selected != null && _selected.RequestId == r.RequestId;
            Color borderColor = isSelected ? Accent : CardBorder;
            Color bgColor = isSelected ? AccentSoft : Color.White;

            var card = new Panel
            {
                Height = RequestCardHeight,
                BackColor = bgColor,
                Cursor = Cursors.Hand,
                Tag = r
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
                using var fill = new SolidBrush(bgColor);
                using var pen = new Pen(borderColor, isSelected ? 2f : 1f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            };

            // ---- Row 1: #ID and status pill ----
            card.Controls.Add(new Label
            {
                Text = $"#{r.RequestId}",
                ForeColor = Navy,
                Font = FontBodyBold,
                Location = new Point(14, 12),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            var (_, fg) = StatusColors(r.Status ?? "Pending");
            var (bg, _) = StatusColors(r.Status ?? "Pending");
            var pill = new Label
            {
                Text = r.Status ?? "Pending",
                ForeColor = fg,
                BackColor = bg,
                Font = FontPill,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(96, 22),
                Location = new Point(card.Width - 118, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            card.Controls.Add(pill);

            // ---- Row 2: customer name ----
            card.Controls.Add(new Label
            {
                Text = cust?.CustomerName ?? $"id:{r.CustomerId}",
                ForeColor = Navy,
                Font = FontBodyBold,
                Location = new Point(14, 36),
                AutoSize = false,
                Width = Math.Max(80, card.Width - 28),
                Height = 18,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            });

            // ---- Row 3: service · priority ----
            card.Controls.Add(new Label
            {
                Text = $"{svc?.ProductName ?? $"Service {r.ServiceId}"} · {r.Priority ?? "Normal"}",
                ForeColor = Muted,
                Font = FontSmall,
                Location = new Point(14, 56),
                AutoSize = false,
                Width = Math.Max(80, card.Width - 28),
                Height = 16,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            });

            // ---- Row 4: date — with wider card, the full timestamp fits ----
            card.Controls.Add(new Label
            {
                Text = r.ScheduledDate?.ToString("MMM d, yyyy — h:mm tt") ?? "Not scheduled",
                ForeColor = Faint,
                Font = FontSmall,
                Location = new Point(14, 74),
                AutoSize = false,
                Width = Math.Max(80, card.Width - 28),
                Height = 16,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            });

            void OnClick(object? s, EventArgs e) => _ = SelectRequestAsync(r);
            card.Click += OnClick;
            foreach (Control child in card.Controls) child.Click += OnClick;

            return card;
        }

        private async Task SelectRequestAsync(ServiceRequestDto r)
        {
            _selected = r;
            RenderRequestsList();

            _detailHost.SuspendLayout();
            _detailHost.Controls.Clear();
            _detailHost.ResumeLayout();

            await LoadHistoryAsync(r.RequestId);
            BuildDetailPanel(r);
        }

        private async Task LoadHistoryAsync(int requestId)
        {
            try
            {
                var branchId = SessionUser.CurrentBranchId;
                var branchQuery = branchId.HasValue && branchId.Value > 0 ? $"&branchId={branchId.Value}" : "";
                var all = await _http.GetFromJsonAsync<ServiceStatusResponseDto>(
                    $"api/service-status?companyId={SessionUser.CurrentCompanyId}{branchQuery}") ?? new();

                _history = all.History
                    .Where(h => h.RequestId == requestId)
                    .OrderByDescending(h => h.UpdatedAt)
                    .ToList();
            }
            catch
            {
                _history = new();
            }
        }

        // ================================================================
        //  Detail panel construction (right column)
        // ================================================================
        private void BuildDetailPanel(ServiceRequestDto r)
        {
            var cust = _custById.TryGetValue(r.CustomerId, out var c) ? c : null;
            var svc = _svcById.TryGetValue(r.ServiceId, out var s) ? s : null;

            int W = Math.Max(360, _detailHost.ClientSize.Width - 4);
            int y = 0;

            // Summary card
            var summary = MakeCard(W, y, 220);
            BuildSummary(summary, r, cust, svc, W);
            _detailHost.Controls.Add(summary);
            y = summary.Bottom + SectionGap;

            // Progress timeline
            var progress = MakeCard(W, y, 170);
            BuildProgress(progress, r, W);
            _detailHost.Controls.Add(progress);
            y = progress.Bottom + SectionGap;

            // Update status form
            var update = MakeCard(W, y, 300);
            BuildUpdateStatus(update, r, W);
            _detailHost.Controls.Add(update);
            y = update.Bottom + SectionGap;

            // Status history grid
            var history = MakeCard(W, y, 300);
            BuildHistory(history, W);
            _detailHost.Controls.Add(history);

            _detailHost.PerformLayout();
        }

        // ---- Summary card ----
        private void BuildSummary(Panel card, ServiceRequestDto r,
                                   TenantCustomerDto? cust, ProductDto? svc, int W)
        {
            AddSectionHeader(card, "REQUEST DETAILS");

            var avatar = new Label
            {
                Text = Initials(cust?.CustomerName ?? "?"),
                ForeColor = Accent,
                BackColor = AccentSoft,
                Font = new Font("Segoe UI Semibold", 13f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(56, 56),
                Location = new Point(20, 48)
            };
            card.Controls.Add(avatar);

            card.Controls.Add(new Label
            {
                Text = cust?.CustomerName ?? $"id:{r.CustomerId}",
                ForeColor = Navy,
                Font = FontBigName,
                Location = new Point(88, 46),
                AutoSize = true
            });

            var contact = string.Join("  ·  ",
                new[] { cust?.ContactNumber, cust?.EmailAddress }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            card.Controls.Add(new Label
            {
                Text = contact,
                ForeColor = Muted,
                Font = FontBody,
                Location = new Point(88, 76),
                AutoSize = true
            });

            var vehicle = string.Join("  ·  ",
                new[] {
                    cust?.PlateNumber,
                    cust?.VehicleMake,
                    cust?.VehicleModel,
                    cust?.VehicleType
                }.Where(x => !string.IsNullOrWhiteSpace(x)));

            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(vehicle) ? "—" : vehicle,
                ForeColor = Muted,
                Font = FontBody,
                Location = new Point(88, 98),
                AutoSize = true
            });

            card.Controls.Add(new Label
            {
                Text = $"₱{svc?.UnitPrice ?? 0m:N0}",
                ForeColor = Navy,
                Font = FontPrice,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Size = new Size(200, 26),
                Location = new Point(W - 220, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            });

            card.Controls.Add(new Label
            {
                Text = $"{svc?.ProductName ?? $"Service {r.ServiceId}"}  ·  {svc?.DurationMinutes ?? 0} min",
                ForeColor = Muted,
                Font = FontSmall,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Size = new Size(200, 18),
                Location = new Point(W - 220, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            });

            card.Controls.Add(new Panel
            {
                Location = new Point(20, 148),
                Size = new Size(W - 40, 1),
                BackColor = Divider,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });

            int colWidth = (W - 40) / 3;

            AddMetaBlock(card, "REQUEST ID", $"#{r.RequestId}", 20, 162);
            AddMetaBlock(card, "PRIORITY", r.Priority ?? "Normal", 20 + colWidth, 162);
            AddMetaBlock(card, "REQUESTED",
                r.RequestedDate.ToString("MMM d, yyyy — h:mm tt"),
                20 + colWidth * 2, 162);
            AddMetaBlock(card, "SCHEDULED",
                r.ScheduledDate?.ToString("MMM d, yyyy — h:mm tt") ?? "—",
                20, 196);
            AddMetaBlock(card, "ASSIGNED SERVICE STAFF",
                GetStaffName(r.AssignedStaffId) ?? "—",
                20 + colWidth, 196);
        }

        private void AddMetaBlock(Panel parent, string caption, string value, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = FontSmallBold,
                Location = new Point(x, y),
                AutoSize = true
            });
            parent.Controls.Add(new Label
            {
                Text = value,
                ForeColor = Navy,
                Font = FontBodyBold,
                Location = new Point(x, y + 18),
                AutoSize = false,
                Width = 220,
                Height = 18,
                AutoEllipsis = true
            });
        }

        // ---- Service progress ----
        private void BuildProgress(Panel card, ServiceRequestDto r, int W)
        {
            AddSectionHeader(card, "SERVICE PROGRESS");

            var timeline = new Panel
            {
                Location = new Point(20, 54),
                Size = new Size(W - 40, 100),
                BackColor = Color.Transparent
            };
            string currentStatus = r.Status ?? "Pending";
            timeline.Paint += (s, e) =>
                PaintTimeline(e.Graphics, timeline.ClientSize, currentStatus);
            card.Controls.Add(timeline);
        }

        private static void PaintTimeline(Graphics g, Size size, string currentStatus)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            bool cancelled = string.Equals(currentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);

            if (cancelled)
            {
                var rect = new RectangleF((size.Width - 220) / 2f, size.Height / 2f - 22, 220, 44);
                using (var path = RoundedRect(Rectangle.Round(rect), 10))
                using (var fill = new SolidBrush(RedSoft))
                using (var pen = new Pen(Red, 1.5f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, "Cancelled",
                    new Font("Segoe UI Semibold", 12f),
                    Rectangle.Round(rect), Red,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var steps = new[] { "Pending", "InProgress", "Completed" };
            var labels = new[] { "Pending", "In Progress", "Completed" };

            int stepCount = steps.Length;
            int radius = 20;
            int usable = size.Width - radius * 2;
            int spacing = usable / (stepCount - 1);
            int cy = size.Height / 2 - 6;

            int currentIdx = Array.FindIndex(steps,
                s => string.Equals(s, currentStatus, StringComparison.OrdinalIgnoreCase));
            if (currentIdx < 0) currentIdx = 0;

            for (int i = 0; i < stepCount - 1; i++)
            {
                int x1 = radius + spacing * i;
                int x2 = radius + spacing * (i + 1);

                using var pen = new Pen(StepConnector, 3f);
                g.DrawLine(pen, x1 + radius, cy, x2 - radius, cy);
            }

            for (int i = 0; i < currentIdx; i++)
            {
                int x1 = radius + spacing * i;
                int x2 = radius + spacing * (i + 1);

                using var pen = new Pen(StepDone, 3f);
                g.DrawLine(pen, x1 + radius, cy, x2 - radius, cy);
            }

            for (int i = 0; i < stepCount; i++)
            {
                int cx = radius + spacing * i;
                var dot = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);

                bool done = i < currentIdx;
                bool current = i == currentIdx;

                Color fill = current ? StepCurrent
                          : done ? StepDone
                          : StepUpcoming;

                using (var fillBr = new SolidBrush(fill))
                    g.FillEllipse(fillBr, dot);

                if (done)
                {
                    using var check = new Pen(Color.White, 3f)
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round
                    };
                    g.DrawLines(check, new[]
                    {
                        new PointF(cx - 7, cy),
                        new PointF(cx - 1, cy + 6),
                        new PointF(cx + 8, cy - 7)
                    });
                }
                else if (current)
                {
                    using var inner = new SolidBrush(Color.White);
                    g.FillEllipse(inner, cx - 6, cy - 6, 12, 12);
                }

                var labelRect = new Rectangle(cx - 70, cy + radius + 6, 140, 22);
                TextRenderer.DrawText(g, labels[i],
                    FontStepLabel,
                    labelRect,
                    current ? Accent : Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix);
            }
        }

        // ---- Update status form ----
        private void BuildUpdateStatus(Panel card, ServiceRequestDto r, int W)
        {
            AddSectionHeader(card, "UPDATE STATUS");

            card.Controls.Add(new Label
            {
                Text = "NEW STATUS",
                ForeColor = Muted,
                Font = FontSmallBold,
                Location = new Point(20, 54),
                AutoSize = true
            });

            _statusCombo = new ComboBox
            {
                Location = new Point(20, 74),
                Width = W - 40,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_statusCombo);
            PopulateStatusCombo(r.Status ?? "Pending");

            card.Controls.Add(new Label
            {
                Text = "NOTES",
                ForeColor = Muted,
                Font = FontSmallBold,
                Location = new Point(20, 118),
                AutoSize = true
            });

            _notesBox = new TextBox
            {
                Location = new Point(20, 138),
                Width = W - 40,
                Height = 72,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                PlaceholderText = "Optional notes about this update...",
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Controls.Add(_notesBox);

            _validationLbl = new Label
            {
                ForeColor = Red,
                Font = FontSmall,
                Location = new Point(20, 216),
                AutoSize = true,
                Visible = false
            };
            card.Controls.Add(_validationLbl);

            _saveBtn = new Button
            {
                Text = "Save Status",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Size = new Size(150, 44),
                Location = new Point(20, 240),
                Cursor = Cursors.Hand
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _saveBtn.Click += async (s, e) => await SaveStatusAsync();
            card.Controls.Add(_saveBtn);

            PreSelectNextStatus(r.Status ?? "Pending");
        }

        // ---- Status history ----
        private void BuildHistory(Panel card, int W)
        {
            AddSectionHeader(card, "STATUS HISTORY");

            _historyGrid = MakeHistoryGrid();
            _historyGrid.Location = new Point(20, 54);
            _historyGrid.Size = new Size(W - 40, 210);
            _historyGrid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Request", HeaderText = "REQUEST", Width = 90 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 120 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UpdatedBy",
                HeaderText = "UPDATED BY",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100,
                MinimumWidth = 130
            });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedAt", HeaderText = "UPDATED AT", Width = 160 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Notes",
                HeaderText = "NOTES",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 130,
                MinimumWidth = 180
            });

            _historyGrid.CellPainting += HistoryGrid_CellPainting;
            card.Controls.Add(_historyGrid);

            PopulateHistoryGrid();
        }

        private void PopulateHistoryGrid()
        {
            if (_historyGrid == null) return;
            _historyGrid.SuspendLayout();
            _historyGrid.Rows.Clear();

            foreach (var h in _history)
            {
                int idx = _historyGrid.Rows.Add(
                    $"#{h.RequestId}",
                    h.Status,
                    h.UpdatedBy,
                    h.UpdatedAt,
                    string.IsNullOrWhiteSpace(h.Notes) ? "—" : h.Notes);

                _historyGrid.Rows[idx].Tag = h;
            }

            _historyGrid.ClearSelection();
            _historyGrid.ResumeLayout();
        }

        private DataGridView MakeHistoryGrid()
        {
            var g = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Vertical,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Muted,
                    SelectionBackColor = Color.White,
                    SelectionForeColor = Muted,
                    Font = FontSmallBold,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0)
                },
                ColumnHeadersHeight = 40,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 44 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = FontBody,
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
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };
            return g;
        }

        // ================================================================
        //  Card helpers
        // ================================================================
        private Panel MakeCard(int width, int top, int height)
        {
            var p = new Panel
            {
                Location = new Point(0, top),
                Width = width,
                Height = height,
                BackColor = Color.White
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };
            return p;
        }

        private void AddSectionHeader(Panel parent, string text)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                ForeColor = Muted,
                Font = FontSectionTitle,
                Location = new Point(20, 16),
                AutoSize = true
            });
            parent.Controls.Add(new Panel
            {
                Location = new Point(20, 36),
                Size = new Size(parent.Width - 40, 1),
                BackColor = Divider,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        private string? GetStaffName(int? staffId)
        {
            if (!staffId.HasValue) return null;
            return _userById.TryGetValue(staffId.Value, out var u) ? u.FullName : null;
        }

        // ================================================================
        //  Status combo + transitions
        // ================================================================
        private void PopulateStatusCombo(string currentStatus)
        {
            _statusCombo.Items.Clear();

            var all = new[] { "Pending", "InProgress", "Completed", "Cancelled" };
            foreach (var s in all)
            {
                if (string.Equals(s, currentStatus, StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsValidTransition(currentStatus, s)) continue;
                _statusCombo.Items.Add(s);
            }

            if (_statusCombo.Items.Count == 0)
            {
                _statusCombo.Items.Add(currentStatus);
                _statusCombo.Enabled = false;
                _saveBtn.Enabled = false;
                _validationLbl.Text = "This status is terminal and cannot be changed.";
                _validationLbl.Visible = true;
            }

            _statusCombo.SelectedIndex = 0;
        }

        private void PreSelectNextStatus(string currentStatus)
        {
            if (_statusCombo == null || !_statusCombo.Enabled) return;

            var preferred = currentStatus switch
            {
                "Pending" or "Assigned" => "InProgress",
                "InProgress" => "Completed",
                _ => null
            };

            if (preferred == null) return;
            var idx = _statusCombo.Items.IndexOf(preferred);
            if (idx >= 0) _statusCombo.SelectedIndex = idx;
        }

        private static bool IsValidTransition(string from, string to)
        {
            from = from?.Trim() ?? "Pending";
            to = to?.Trim() ?? "Pending";

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return true;

            if (string.Equals(from, "Completed", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(from, "Cancelled", StringComparison.OrdinalIgnoreCase)) return false;

            if (string.Equals(to, "Cancelled", StringComparison.OrdinalIgnoreCase)) return true;

            if ((string.Equals(from, "Pending", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(from, "Assigned", StringComparison.OrdinalIgnoreCase))
                && string.Equals(to, "InProgress", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(from, "InProgress", StringComparison.OrdinalIgnoreCase)
                && string.Equals(to, "Completed", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        // ================================================================
        //  Save
        // ================================================================
        private async Task SaveStatusAsync()
        {
            if (_selected == null || _statusCombo == null) return;

            var newStatus = _statusCombo.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(newStatus))
            {
                ShowValidation("Please pick a new status.");
                return;
            }

            if (!IsValidTransition(_selected.Status ?? "Pending", newStatus))
            {
                ShowValidation($"Invalid transition from {_selected.Status} to {newStatus}.");
                return;
            }

            ClearValidation();

            var confirm = MessageBox.Show(
                $"Change status from {_selected.Status} to {newStatus}?",
                "Update Status",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (confirm != DialogResult.OK) return;

            try
            {
                Cursor = Cursors.WaitCursor;
                _saveBtn.Enabled = false;
                _saveBtn.Text = "Saving...";

                var payload = new
                {
                    staffId = SessionUser.UserId,
                    status = newStatus,
                    notes = _notesBox?.Text?.Trim()
                };

                var resp = await _http.PutAsJsonAsync(
                    $"api/service-requests/{_selected.RequestId}/staff-status?companyId={SessionUser.CurrentCompanyId}", payload);

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        $"Status updated to {newStatus}.",
                        "Saved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    int selId = _selected.RequestId;
                    await LoadAllAsync();

                    var refreshed = _myRequests.FirstOrDefault(r => r.RequestId == selId);
                    if (refreshed != null)
                    {
                        await SelectRequestAsync(refreshed);
                    }
                    else
                    {
                        _selected = null;
                        _detailHost.Controls.Clear();
                        _detailHost.Controls.Add(_emptyDetail);
                        _emptyDetail.Visible = true;
                    }
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Save failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _saveBtn.Text = "Save Status";
                _saveBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ShowValidation(string message)
        {
            if (_validationLbl == null) return;
            _validationLbl.Text = message;
            _validationLbl.Visible = true;
        }

        private void ClearValidation()
        {
            if (_validationLbl == null) return;
            _validationLbl.Text = "";
            _validationLbl.Visible = false;
        }

        // ================================================================
        //  Cell paint + colours
        // ================================================================
        private void HistoryGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_historyGrid.Columns[e.ColumnIndex].Name != "Status") return;

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

        // ================================================================
        //  Geometry
        // ================================================================
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
    }
}