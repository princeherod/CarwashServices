using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles
{
    /// <summary>
    /// Manager → Monitor Service Status.
    ///
    ///   [ Service Status ]  — one row per active ServiceRequest, same
    ///                          columns as Manage Service Requests
    ///   [ Service History ] — every ServiceStatusLog row, newest first
    ///
    /// Read-only. No edit / delete / status-change actions.
    /// </summary>
    public class MonitorServiceStatusView : UserControl
    {
        // ---- Palette ----
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
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);
        private static readonly Color Slate = Color.FromArgb(0x39, 0x49, 0xAB);
        private static readonly Color SlateSoft = Color.FromArgb(0xE8, 0xEA, 0xF6);
        private static readonly Color NeutralSoft = Color.FromArgb(0xEE, 0xF1, 0xF6);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);

        private static readonly Font FontLine1 = new("Segoe UI Semibold", 9.5f);
        private static readonly Font FontLine2 = new("Segoe UI", 8.5f);
        private static readonly Font FontTag = new("Segoe UI Semibold", 9.5f);

        // ---- Layout ----
        private const int PadX = 40;
        private const int KpiTop = 160;
        private const int KpiHeight = 100;
        private const int PanelTop = 280;
        private const int PanelHeight = 40;
        private const int GridTop = 330;
        private const int PageBottom = 24;
        private const int RowHeight = 62;

        // ---- Data ----
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        private ServiceStatusResponseDto _data = new();

        // ---- UI ----
        private Panel _contentPanel = null!;

        private Label _kpiPending = null!;
        private Label _kpiInProgress = null!;
        private Label _kpiCompleted = null!;
        private Label _kpiCancelled = null!;

        private Label _panelTabStatus = null!;
        private Label _panelTabHistory = null!;
        private Panel _panelUnderline = null!;

        private Label _statusSubtitle = null!;
        private Label _subtitleHistory = null!;

        private Panel _filterBar = null!;
        private Button _chipAll = null!;
        private Button _chipPending = null!;
        private Button _chipInProgress = null!;
        private Button _chipCompleted = null!;
        private Button _chipCancelled = null!;
        private string _statusFilter = "All";

        private Panel _gridHost = null!;
        private DataGridView _grid = null!;
        private Label _emptyLbl = null!;

        private enum PanelTab { Status, History }
        private PanelTab _tab = PanelTab.Status;

        public MonitorServiceStatusView()
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
        //  UI
        // ================================================================
        private void InitializeUI()
        {
            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Controls.Add(_contentPanel);

            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Monitor Service Status",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 12),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Monitor Service Status",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(PadX, 34),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "Live view of service requests and their status history.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 80),
                AutoSize = true
            });

            // ---- KPI tiles ----
            _kpiPending = AddKpi("PENDING", Amber);
            _kpiInProgress = AddKpi("IN PROGRESS", Blue);
            _kpiCompleted = AddKpi("COMPLETED", Green);
            _kpiCancelled = AddKpi("CANCELLED", Red);

            // ---- Panel toggle strip ----
            var panelBar = new Panel
            {
                Location = new Point(PadX, PanelTop),
                Height = PanelHeight,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            panelBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, panelBar.Height - 1, panelBar.Width, panelBar.Height - 1);
            };
            _contentPanel.Controls.Add(panelBar);

            _panelTabStatus = MakePanelTab("Service Status", 0);
            _panelTabHistory = MakePanelTab("Service History", 170);
            panelBar.Controls.Add(_panelTabStatus);
            panelBar.Controls.Add(_panelTabHistory);

            _panelUnderline = new Panel
            {
                Height = 2,
                Width = 160,
                BackColor = Blue,
                Location = new Point(_panelTabStatus.Left, PanelHeight - 2)
            };
            panelBar.Controls.Add(_panelUnderline);

            _panelTabStatus.Click += (s, e) => SwitchTab(PanelTab.Status);
            _panelTabHistory.Click += (s, e) => SwitchTab(PanelTab.History);

            // ---- Subtitle ----
            _statusSubtitle = new Label
            {
                Text = "Current status of every active service request.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, GridTop - 28),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_statusSubtitle);

            _subtitleHistory = new Label
            {
                Text = "Every status change recorded, newest first. Read-only.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, GridTop - 28),
                AutoSize = true,
                Visible = false
            };
            _contentPanel.Controls.Add(_subtitleHistory);

            // ---- Status filter chips ----
            _filterBar = new Panel
            {
                Location = new Point(PadX, PanelTop + PanelHeight + 8),
                Height = 36,
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_filterBar);

            _chipAll = MakeChip("All", 0);
            _chipPending = MakeChip("Pending", 115);
            _chipInProgress = MakeChip("In Progress", 230);
            _chipCompleted = MakeChip("Completed", 345);
            _chipCancelled = MakeChip("Cancelled", 460);

            _chipAll.Click += (s, e) => SetFilter("All");
            _chipPending.Click += (s, e) => SetFilter("Pending");
            _chipInProgress.Click += (s, e) => SetFilter("InProgress");
            _chipCompleted.Click += (s, e) => SetFilter("Completed");
            _chipCancelled.Click += (s, e) => SetFilter("Cancelled");

            _filterBar.Controls.Add(_chipAll);
            _filterBar.Controls.Add(_chipPending);
            _filterBar.Controls.Add(_chipInProgress);
            _filterBar.Controls.Add(_chipCompleted);
            _filterBar.Controls.Add(_chipCancelled);

            // ---- Grid ----
            _gridHost = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, GridTop),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_gridHost);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Vertical,
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
                RowTemplate = { Height = RowHeight },
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

            _grid.CellPainting += Grid_CellPainting;

            _gridHost.Controls.Add(_grid);

            _emptyLbl = new Label
            {
                Text = "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                BackColor = Color.White
            };
            _gridHost.Controls.Add(_emptyLbl);
            _emptyLbl.BringToFront();

            _contentPanel.Resize += (s, e) => LayoutContent();
            LayoutContent();

            RefreshPanelChrome();
        }

        private Label AddKpi(string caption, Color fg)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                Height = KpiHeight,
                Tag = "kpi"
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                using var accent = new SolidBrush(fg);
                e.Graphics.FillRectangle(accent, 0, 0, 4, card.Height);
            };

            card.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(22, 14),
                AutoSize = true,
                BackColor = Color.White
            });

            var val = new Label
            {
                Text = "0",
                ForeColor = fg,
                Font = new Font("Segoe UI Semibold", 24f),
                Location = new Point(20, 40),
                AutoSize = true,
                BackColor = Color.White
            };
            card.Controls.Add(val);

            _contentPanel.Controls.Add(card);
            return val;
        }

        private Label MakePanelTab(string text, int x) => new()
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 11f),
            ForeColor = Muted,
            AutoSize = false,
            Size = new Size(160, PanelHeight),
            Location = new Point(x, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.White
        };

        private Button MakeChip(string text, int x)
        {
            var chip = new Button
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9.5f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 32),
                Location = new Point(x, 0),
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            chip.FlatAppearance.BorderSize = 1;
            return chip;
        }

        private void SwitchTab(PanelTab tab)
        {
            if (_tab == tab)
            {
                RefreshPanelChrome();
                return;
            }

            _tab = tab;
            RefreshPanelChrome();
            PopulateGrid();
        }

        private void RefreshPanelChrome()
        {
            bool status = _tab == PanelTab.Status;

            _panelTabStatus.ForeColor = status ? Navy : Muted;
            _panelTabHistory.ForeColor = status ? Muted : Navy;
            _panelUnderline.Left = status ? _panelTabStatus.Left : _panelTabHistory.Left;
            _panelUnderline.Width = status ? _panelTabStatus.Width : _panelTabHistory.Width;

            _statusSubtitle.Visible = status;
            _subtitleHistory.Visible = !status;
            _filterBar.Visible = status;

            RebuildChips();
        }

        private void RebuildChips()
        {
            foreach (var chip in new[] { _chipAll, _chipPending, _chipInProgress, _chipCompleted, _chipCancelled })
            {
                bool active = chip.Text == DisplayFilter(_statusFilter);
                chip.BackColor = active ? Navy : Color.White;
                chip.ForeColor = active ? Color.White : Navy;
                chip.FlatAppearance.BorderColor = active ? Navy : CardBorder;
                chip.FlatAppearance.MouseOverBackColor = active ? Navy : Color.FromArgb(0xF5, 0xF7, 0xFA);
            }
        }

        private void SetFilter(string filter)
        {
            _statusFilter = filter;
            RebuildChips();
            PopulateGrid();
        }

        private static string DisplayFilter(string f) => f switch
        {
            "InProgress" => "In Progress",
            _ => f
        };

        private void LayoutContent()
        {
            if (_contentPanel == null) return;
            int w = _contentPanel.ClientSize.Width;
            int h = _contentPanel.ClientSize.Height;
            if (w < 400) return;

            int contentW = w - 2 * PadX;

            var kpis = _contentPanel.Controls.OfType<Panel>()
                .Where(p => (p.Tag as string) == "kpi").ToList();
            int kpiGap = 20;
            int kpiW = (contentW - kpiGap * 3) / 4;
            for (int i = 0; i < kpis.Count; i++)
                kpis[i].SetBounds(PadX + i * (kpiW + kpiGap), KpiTop, kpiW, KpiHeight);

            var panelBar = _contentPanel.Controls.OfType<Panel>()
                .FirstOrDefault(p => p.Controls.Contains(_panelTabStatus));
            panelBar?.SetBounds(PadX, PanelTop, contentW, PanelHeight);

            _filterBar.SetBounds(PadX, PanelTop + PanelHeight + 8, contentW, 36);
            _gridHost.SetBounds(PadX, GridTop, contentW,
                Math.Max(0, h - GridTop - PageBottom));
        }

        // ================================================================
        //  DATA
        // ================================================================
        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                _data = await _http.GetFromJsonAsync<ServiceStatusResponseDto>(
                    $"api/service-status?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}") ?? new();

                _kpiPending.Text = _data.Pending.ToString();
                _kpiInProgress.Text = _data.InProgress.ToString();
                _kpiCompleted.Text = _data.Completed.ToString();
                _kpiCancelled.Text = _data.Cancelled.ToString();

                PopulateGrid();
            }
            catch (Exception ex)
            {
                // Show the error inline so the empty panel isn't mistaken for "no data".
                _data = new ServiceStatusResponseDto();

                _kpiPending.Text = "—";
                _kpiInProgress.Text = "—";
                _kpiCompleted.Text = "—";
                _kpiCancelled.Text = "—";

                _grid.SuspendLayout();
                _grid.Columns.Clear();
                _grid.Rows.Clear();
                _grid.ResumeLayout();

                _emptyLbl.Text =
                    $"Couldn't load service status.\n\n{ex.Message}";
                _emptyLbl.Visible = true;
                _emptyLbl.ForeColor = Red;
                _emptyLbl.SetBounds(
                    Math.Max(0, (_gridHost.ClientSize.Width - 500) / 2),
                    Math.Max(60, _gridHost.ClientSize.Height / 2 - 30),
                    500, 60);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void PopulateGrid()
        {
            _grid.SuspendLayout();
            _grid.Columns.Clear();
            _grid.Rows.Clear();

            if (_tab == PanelTab.Status)
                BuildStatusPanel();
            else
                BuildHistoryPanel();

            _grid.ClearSelection();
            _grid.ResumeLayout();

            // Restore the label to its normal colour — a previous load may
            // have set it to Red when it was reporting an error.
            _emptyLbl.ForeColor = Muted;

            _emptyLbl.Text = _grid.Rows.Count == 0
                ? (_tab == PanelTab.Status
                    ? (_statusFilter == "All"
                        ? "No service requests to monitor."
                        : $"No requests with status '{DisplayFilter(_statusFilter)}'.")
                    : "No status history recorded yet.")
                : "";
            _emptyLbl.Visible = _grid.Rows.Count == 0;

            if (_emptyLbl.Visible)
            {
                _emptyLbl.SetBounds(
                    Math.Max(0, (_gridHost.ClientSize.Width - 400) / 2),
                    Math.Max(60, _gridHost.ClientSize.Height / 2 - 20),
                    400, 40);
            }
        }

        // ---- Panel 1: one row per active request ----
        private void BuildStatusPanel()
        {
            AddColumn("Request", 90);
            AddColumn("Customer", 220, fill: true, weight: 100);
            AddColumn("Service", 200, fill: true, weight: 90);
            AddColumn("Scheduled Date", 160);
            AddColumn("Assigned Staff", 150);
            AddColumn("Priority", 100);
            AddColumn("Status", 120);
            AddColumn("Last Updated", 160);
            AddColumn("Updated By", 150);

            IEnumerable<ServiceStatusRowDto> rows = _data.Current;

            if (_statusFilter != "All")
            {
                rows = rows.Where(r =>
                    string.Equals(r.Status ?? "", _statusFilter, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var r in rows)
            {
                var custCell = r.Customer;
                if (!string.IsNullOrWhiteSpace(r.Plate))
                    custCell += "\n" + r.Plate;

                var svcCell = r.Service;
                if (r.ServicePrice > 0)
                    svcCell += $"\n₱{r.ServicePrice:N0}";

                _grid.Rows.Add(
                    $"#{r.RequestId}",
                    custCell,
                    svcCell,
                    string.IsNullOrWhiteSpace(r.ScheduledDate) ? "—" : r.ScheduledDate,
                    string.IsNullOrWhiteSpace(r.AssignedStaff) ? "Unassigned" : r.AssignedStaff,
                    string.IsNullOrWhiteSpace(r.Priority) ? "Normal" : r.Priority,
                    r.Status,
                    string.IsNullOrWhiteSpace(r.LastUpdated) ? "—" : r.LastUpdated,
                    string.IsNullOrWhiteSpace(r.UpdatedBy) ? "—" : r.UpdatedBy);
            }
        }

        // ---- Panel 2: every log row ----
        private void BuildHistoryPanel()
        {
            AddColumn("Request", 90);
            AddColumn("Customer", 200, fill: true, weight: 90);
            AddColumn("Service", 180, fill: true, weight: 80);
            AddColumn("Status", 120);
            AddColumn("Updated At", 170);
            AddColumn("Updated By", 150);
            AddColumn("Notes", 260, fill: true, weight: 120);

            foreach (var h in _data.History)
            {
                _grid.Rows.Add(
                    $"#{h.RequestId}",
                    string.IsNullOrWhiteSpace(h.Customer) ? "—" : h.Customer,
                    string.IsNullOrWhiteSpace(h.Service) ? "—" : h.Service,
                    h.Status,
                    h.UpdatedAt,
                    h.UpdatedBy,
                    string.IsNullOrWhiteSpace(h.Notes) ? "" : h.Notes);
            }
        }

        private void AddColumn(string header, int width, bool fill = false, int weight = 100)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Name = header
            };
            if (fill)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                col.MinimumWidth = width;
                col.FillWeight = weight;
            }
            else
            {
                col.Width = width;
            }
            _grid.Columns.Add(col);
        }

        // ================================================================
        //  CELL PAINTING
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var header = _grid.Columns[e.ColumnIndex].HeaderText;

            if (header == "Status")
            {
                var text = Convert.ToString(e.Value) ?? "";
                var (bg, fg) = StatusColors(text);
                PaintPill(e, text, fg, bg);
                return;
            }

            if (header == "Priority")
            {
                var text = Convert.ToString(e.Value) ?? "Normal";
                var (bg, fg) = PriorityColors(text);
                PaintPill(e, text, fg, bg);
                return;
            }

            if (header == "Customer" || header == "Service" || header == "Assigned Staff")
            {
                PaintTwoLine(e);
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

        private static void PaintPill(DataGridViewCellPaintingEventArgs e, string text, Color fg, Color bg)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            if (string.IsNullOrEmpty(text)) { e.Handled = true; return; }

            var b = e.CellBounds;
            var size = TextRenderer.MeasureText(e.Graphics, text, FontTag,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int x = b.X + 16;
            int maxW = Math.Max(10, b.Width - 24);
            int y = b.Y + (b.Height - size.Height) / 2;

            TextRenderer.DrawText(e.Graphics, text, FontTag,
                new Rectangle(x, y, maxW, size.Height), fg,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

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
    }
}