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
    public class FollowUpsView : UserControl
    {
        private HttpClient _http;

        private List<FollowUpDto> _all = new();
        private List<TenantCustomerDto> _customers = new();
        private Dictionary<int, TenantCustomerDto> _custById = new();

        private int _pageSize = 8;
        private int _page = 1;
        private FollowUpStatsDto _stats = new();
        private string _search = "";
        private string _statusFilter = "All statuses";

        private int? _editingRowId = null;
        private int _editingRowIndex = -1;

        private Panel _contentPanel;
        private Panel _statsBar;
        private DataGridView _grid;
        private Panel _pagerBar;
        private DataGridView _logGrid;
        private Label _showingLbl;
        private Panel _gridCard;
        private Panel _logCard;
        private Panel _header;
        private Button _addBtn;
        private TextBox _searchBox;
        private ComboBox _statusFilterCombo;

        private Label _breadcrumb;
        private Label _sectionLbl;
        private Label _logTitleLbl;
        private Label _logSubLbl;
        private bool _relayouting;
        private int _statsLayoutWidth = -1;
        private int _logRowCount = 0;

        // FIX: a flag that tells us the UI is ready before we try to touch the grid
        private bool _uiReady = false;

        private const int MarginX = 40;
        private const int TopMargin = 20;
        private const int ActionsColWidth = 190;

        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color ButtonBorder = Color.FromArgb(0xC9, 0xD3, 0xE3);

        private static readonly Color GreenDot = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color YellowDot = Color.FromArgb(0xF5, 0xB0, 0x2E);
        private static readonly Color BlueDot = Color.FromArgb(0x42, 0xA5, 0xF5);
        private static readonly Color RedDot = Color.FromArgb(0xE5, 0x39, 0x35);

        private static readonly Font FontStrong = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font FontNormal = new Font("Segoe UI", 9.5f);
        private static readonly Font FontSub = new Font("Segoe UI", 8.5f);
        private static readonly Font FontPill = new Font("Segoe UI Semibold", 8.5f);
        private static readonly Font FontAction = new Font("Segoe UI Semibold", 9f);
        private static readonly Font FontStatTitle = new Font("Segoe UI", 10f);
        private static readonly Font FontStatValue = new Font("Segoe UI Semibold", 24f);

        private static readonly string[] StatusOptions =
        {
            "Scheduled", "Due today", "Sent", "Redeemed", "Expired"
        };

        public FollowUpsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5180/"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            InitializeUI();

            // FIX: UI is built — safe to process filter changes now
            _uiReady = true;
        }

        // ================================================================
        //  Small helper controls
        // ================================================================
        private sealed class BufferedGrid : DataGridView
        {
            public BufferedGrid() { DoubleBuffered = true; }
        }

        private sealed class RoundedPanel : Panel
        {
            public RoundedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(PageBg);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = RoundedRect(r, 10);
                using var fill = new SolidBrush(Color.White);
                using var pen = new Pen(CardBorder, 1);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        // ================================================================
        //  UI
        // ================================================================
        private DataGridView CreateGrid(int rowHeight)
        {
            var g = new BufferedGrid
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderBg,
                    ForeColor = Muted,
                    SelectionBackColor = HeaderBg,
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0)
                },
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 44,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 8, 0)
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
            g.RowTemplate.Height = rowHeight;
            return g;
        }

        private void InitializeUI()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(0, 0, 0, 24),
                AutoScroll = true
            };
            Controls.Add(_contentPanel);

            _breadcrumb = new Label
            {
                Text = "Modules  ›  Follow-Ups / Reminders",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_breadcrumb);

            _header = new Panel
            {
                Height = 92,
                BackColor = Color.Transparent
            };
            _contentPanel.Controls.Add(_header);

            _header.Controls.Add(new Label
            {
                Text = "Follow-ups / Reminders",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            _header.Controls.Add(new Label
            {
                Text = "Send discount offers and reminders to keep customers coming back.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(2, 50),
                AutoSize = true
            });

            _addBtn = new Button
            {
                Text = "+  Add Follow-up",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(190, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _addBtn.FlatAppearance.BorderSize = 0;
            _addBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _addBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x06, 0x0E, 0x22);
            _addBtn.Click += (s, e) => OpenAddDialog();
            _header.Controls.Add(_addBtn);

            _statsBar = new Panel
            {
                Height = 100,
                BackColor = Color.Transparent
            };
            _contentPanel.Controls.Add(_statsBar);

            _sectionLbl = new Label
            {
                Text = "Customers to follow up",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_sectionLbl);

            _searchBox = new TextBox
            {
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Search customer..."
            };
            _contentPanel.Controls.Add(_searchBox);
            _searchBox.TextChanged += (s, e) =>
            {
                if (!_uiReady) return;
                _search = _searchBox.Text?.Trim().ToLower() ?? "";
                _page = 1;
                ApplyFilter();
            };

            _statusFilterCombo = new ComboBox
            {
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterCombo.Items.AddRange(new object[]
            {
                "All statuses", "Scheduled", "Due today", "Sent", "Redeemed", "Expired"
            });

            // Attach handler BEFORE setting SelectedIndex — the guard inside
            // prevents it from running ApplyFilter before the grid exists
            _statusFilterCombo.SelectedIndexChanged += (s, e) =>
            {
                if (!_uiReady) return;
                _statusFilter = _statusFilterCombo.SelectedItem?.ToString() ?? "All statuses";
                _page = 1;
                ApplyFilter();
            };
            _statusFilterCombo.SelectedIndex = 0;

            _contentPanel.Controls.Add(_statusFilterCombo);

            _gridCard = new Panel
            {
                BackColor = CardBorder,
                Padding = new Padding(1)
            };
            _contentPanel.Controls.Add(_gridCard);

            _grid = CreateGrid(62);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FollowUpId", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "Customer", Width = 230 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "Scheduled", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Reason",
                HeaderText = "Reason",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 110,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Discount",
                HeaderText = "Discount offer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 110,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SendVia", HeaderText = "Send via", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 140 });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Actions",
                HeaderText = "",
                UseColumnTextForButtonValue = false,
                FlatStyle = FlatStyle.Flat,
                Width = ActionsColWidth,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Navy,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(12, 15, ActionsColWidth - 12 - 80, 15)
                }
            });

            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellPainting += Grid_CellPainting;
            _grid.SizeChanged += (s, e) => RepositionEditOverlays();
            _grid.Scroll += (s, e) => RepositionEditOverlays();
            _gridCard.Controls.Add(_grid);

            _pagerBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 8)
            };
            _pagerBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, _pagerBar.Width, 0);
            };
            _gridCard.Controls.Add(_pagerBar);
            _pagerBar.SendToBack();

            _showingLbl = new Label
            {
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                Location = new Point(16, 18)
            };
            _pagerBar.Controls.Add(_showingLbl);

            _logTitleLbl = new Label
            {
                Text = "Follow-up Log",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_logTitleLbl);

            _logSubLbl = new Label
            {
                Text = "View and manage all customer follow-up activities.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true
            };
            _contentPanel.Controls.Add(_logSubLbl);

            _logCard = new Panel
            {
                BackColor = CardBorder,
                Padding = new Padding(1)
            };
            _contentPanel.Controls.Add(_logCard);

            _logGrid = CreateGrid(54);
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Num", HeaderText = "#", Width = 70 });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "Customer", Width = 190 });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", Width = 180 });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Method",
                HeaderText = "Contact Method",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 110,
                FillWeight = 100
            });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "Scheduled", Width = 130 });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 140 });
            _logGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Notes",
                HeaderText = "Notes",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 140,
                FillWeight = 120
            });
            _logGrid.CellPainting += LogGrid_CellPainting;
            _logCard.Controls.Add(_logGrid);

            _contentPanel.ClientSizeChanged += (s, e) => RelayoutUI();

            Load += async (s, e) =>
            {
                RelayoutUI();
                await LoadLookupsAsync();
                await LoadAsync();
                BeginInvoke(new Action(RelayoutUI));
            };
        }

        // ================================================================
        //  LAYOUT
        // ================================================================
        private void RelayoutUI()
        {
            if (_contentPanel == null || _relayouting) return;
            _relayouting = true;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    int before = _contentPanel.ClientSize.Width;
                    ApplyLayout();
                    if (_contentPanel.ClientSize.Width == before) break;
                }
            }
            finally
            {
                _relayouting = false;
            }
        }

        private void ApplyLayout()
        {
            int fullW = _contentPanel.ClientSize.Width;
            int w = fullW - MarginX * 2;
            if (w < 300) return;

            var off = _contentPanel.AutoScrollPosition;
            int L = MarginX + off.X;
            int y = TopMargin + off.Y;

            _breadcrumb.Location = new Point(L, y);
            y += 28;

            _header.SetBounds(L, y, w, 92);
            _addBtn.Location = new Point(w - _addBtn.Width, 14);
            y += 92 + 8;

            _statsBar.SetBounds(L, y, w, 100);
            if (_statsLayoutWidth != w) BuildStats();
            y += 100 + 34;

            _sectionLbl.Location = new Point(L, y);
            _statusFilterCombo.Width = 190;
            _statusFilterCombo.Location = new Point(L + w - 190, y + 1);
            _searchBox.Width = 260;
            _searchBox.Location = new Point(L + w - 190 - 12 - 260, y + 1);
            y += 44;

            int gridH = 2 + _grid.ColumnHeadersHeight + _pageSize * _grid.RowTemplate.Height + _pagerBar.Height + 2;
            _gridCard.SetBounds(L, y, w, gridH);
            y += gridH + 36;

            _logTitleLbl.Location = new Point(L, y);
            y += 34;
            _logSubLbl.Location = new Point(L + 2, y);
            y += 32;

            int logH = 2 + _logGrid.ColumnHeadersHeight + Math.Max(3, _logRowCount) * _logGrid.RowTemplate.Height + 2;
            _logCard.SetBounds(L, y, w, logH);
        }

        // ================================================================
        //  STATS
        // ================================================================
        private void BuildStats()
        {
            if (_statsBar == null) return;

            foreach (var old in _statsBar.Controls.Cast<Control>().ToList())
            {
                _statsBar.Controls.Remove(old);
                old.Dispose();
            }

            int gap = 20;
            int cardW = (_statsBar.Width - gap * 3) / 4;
            if (cardW < 100) cardW = 100;

            string[] titles = { "Due today", "Offers sent", "Discounts redeemed", "Expired" };
            int[] values = { _stats.DueToday, _stats.OffersSent, _stats.Redeemed, _stats.Expired };
            Color[] dots = { YellowDot, BlueDot, GreenDot, RedDot };

            for (int i = 0; i < 4; i++)
            {
                var card = new RoundedPanel
                {
                    Location = new Point(i * (cardW + gap), 0),
                    Size = new Size(cardW, 100)
                };

                var dot = new Panel
                {
                    Location = new Point(24, 27),
                    Size = new Size(10, 10),
                    BackColor = Color.White,
                    Tag = dots[i]
                };
                dot.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var br = new SolidBrush((Color)((Panel)s).Tag);
                    e.Graphics.FillEllipse(br, 0, 0, 9, 9);
                };
                card.Controls.Add(dot);

                card.Controls.Add(new Label
                {
                    Text = titles[i],
                    ForeColor = Muted,
                    BackColor = Color.White,
                    Font = FontStatTitle,
                    Location = new Point(42, 21),
                    AutoSize = true
                });

                card.Controls.Add(new Label
                {
                    Text = values[i].ToString(),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    Font = FontStatValue,
                    Location = new Point(22, 42),
                    AutoSize = true
                });

                _statsBar.Controls.Add(card);
            }

            _statsLayoutWidth = _statsBar.Width;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ================================================================
        //  CUSTOM CELL PAINTING
        // ================================================================
        private const DataGridViewPaintParts BaseParts =
            DataGridViewPaintParts.Background |
            DataGridViewPaintParts.Border |
            DataGridViewPaintParts.SelectionBackground;

        private static void PaintHeader(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.All);
            using var pen = new Pen(CardBorder);
            e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                e.CellBounds.Right, e.CellBounds.Bottom - 1);
            e.Handled = true;
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e,
            Font first, Color firstColor, Font second, Color secondColor)
        {
            e.Paint(e.CellBounds, BaseParts);

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
                TextRenderer.DrawText(e.Graphics, l1, first,
                    new Rectangle(x, b.Y, w, b.Height), firstColor, flags);
            }
            else
            {
                int h1 = first.Height, h2 = second.Height, gap = 2;
                int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;
                TextRenderer.DrawText(e.Graphics, l1, first,
                    new Rectangle(x, top, w, h1), firstColor, flags);
                TextRenderer.DrawText(e.Graphics, l2, second,
                    new Rectangle(x, top + h1 + gap, w, h2), secondColor, flags);
            }

            e.Handled = true;
        }

        private static (Color bg, Color fg) StatusColors(string s) => s switch
        {
            "Scheduled" => (Color.FromArgb(0xE8, 0xEA, 0xF6), Color.FromArgb(0x39, 0x49, 0xAB)),
            "Due today" => (Color.FromArgb(0xFF, 0xF4, 0xDB), Color.FromArgb(0x9A, 0x6A, 0x00)),
            "Sent" or "Contacted" => (Color.FromArgb(0xE3, 0xF1, 0xFD), Color.FromArgb(0x15, 0x65, 0xC0)),
            "Redeemed" => (Color.FromArgb(0xE4, 0xF5, 0xE8), Color.FromArgb(0x1E, 0x7A, 0x34)),
            "Expired" => (Color.FromArgb(0xFD, 0xE7, 0xE6), Color.FromArgb(0xC6, 0x28, 0x28)),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        private static void PaintStatusPill(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, BaseParts);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            var (bg, fg) = StatusColors(text);
            var size = TextRenderer.MeasureText(e.Graphics, text, FontPill,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int h = 26;
            int w = Math.Min(size.Width + 26, Math.Max(20, e.CellBounds.Width - 24));
            var rect = new Rectangle(
                e.CellBounds.X + 16,
                e.CellBounds.Y + (e.CellBounds.Height - h) / 2,
                w, h);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1), h / 2))
            using (var br = new SolidBrush(bg))
            {
                e.Graphics.FillPath(br, path);
            }

            TextRenderer.DrawText(e.Graphics, text, FontPill, rect, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            e.Handled = true;
        }

        private static void PaintActionButton(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, BaseParts);

            var text = Convert.ToString(e.Value) ?? "";
            bool primary = text == "Save";

            var rect = new Rectangle(e.CellBounds.X + 12, e.CellBounds.Y + 15, 80, e.CellBounds.Height - 30);
            var pathRect = new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(pathRect, 8))
            using (var fill = new SolidBrush(primary ? Navy : Color.White))
            using (var pen = new Pen(primary ? Navy : ButtonBorder, 1))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(e.Graphics, text, FontAction, rect,
                primary ? Color.White : Navy,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            e.Handled = true;
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;

            if (e.RowIndex == -1)
            {
                PaintHeader(e);
                return;
            }

            switch (_grid.Columns[e.ColumnIndex].Name)
            {
                case "Customer":
                    PaintTwoLine(e, FontStrong, Navy, FontSub, Muted);
                    break;

                case "Scheduled":
                    {
                        var raw = Convert.ToString(e.Value) ?? "";
                        var secondColor = raw.Contains("overdue") ? RedDot : Muted;
                        PaintTwoLine(e, FontNormal, Navy, FontSub, secondColor);
                        break;
                    }

                case "Status":
                    if (e.RowIndex != _editingRowIndex)
                        PaintStatusPill(e);
                    break;

                case "Actions":
                    PaintActionButton(e);
                    break;
            }
        }

        private void LogGrid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0) return;

            if (e.RowIndex == -1)
            {
                PaintHeader(e);
                return;
            }

            if (_logGrid.Columns[e.ColumnIndex].Name == "Status")
                PaintStatusPill(e);
        }

        // ================================================================
        //  LOAD
        // ================================================================
        private async Task LoadLookupsAsync()
        {
            try
            {
                _customers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers") ?? new();
                _custById = _customers.ToDictionary(c => c.TenantCustomerId);
            }
            catch
            {
                _customers = new();
                _custById = new();
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                _all = await _http.GetFromJsonAsync<List<FollowUpDto>>("api/follow-ups") ?? new();
                _stats = await _http.GetFromJsonAsync<FollowUpStatsDto>("api/follow-ups/stats")
                            ?? new FollowUpStatsDto();

                BuildStats();
                ApplyFilter();
                BuildLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load follow-ups.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // ================================================================
        //  GRID + PAGINATION
        // ================================================================
        private void ApplyFilter()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyFilter));
                return;
            }

            // Guard: bail out if the UI hasn't been built yet.
            if (_grid == null || _pagerBar == null || _showingLbl == null)
                return;

            RemoveEditOverlays();

            var list = _all.OrderByDescending(f => f.FollowUpId).ToList();

            if (!string.IsNullOrEmpty(_search))
            {
                list = list.Where(f =>
                {
                    var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;
                    var name = cust?.CustomerName?.ToLower() ?? "";
                    return name.Contains(_search)
                        || (f.Reason ?? "").ToLower().Contains(_search)
                        || (f.DiscountOffer ?? "").ToLower().Contains(_search);
                }).ToList();
            }

            // Treat both "All" and "All statuses" as no filter
            if (!string.IsNullOrWhiteSpace(_statusFilter)
                && _statusFilter != "All"
                && _statusFilter != "All statuses")
            {
                list = list.Where(f => f.Status == _statusFilter).ToList();
            }

            var total = list.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
            if (_page > totalPages) _page = totalPages;
            if (_page < 1) _page = 1;

            var start = (_page - 1) * _pageSize;
            var pageItems = list.Skip(start).Take(_pageSize).ToList();

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            _editingRowId = null;
            _editingRowIndex = -1;

            foreach (var f in pageItems)
            {
                var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;

                var custCell = cust?.CustomerName ?? $"id:{f.CustomerId}";
                var secondLine = string.Join("  ·  ",
                    new[] { cust?.ContactNumber, cust?.VehicleType }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                if (!string.IsNullOrWhiteSpace(secondLine))
                    custCell += $"\n{secondLine}";

                var scheduled = f.ScheduledDate.ToString("yyyy-MM-dd");
                var isOverdue = f.ScheduledDate.Date < DateTime.Today
                                && f.Status != "Sent"
                                && f.Status != "Contacted"
                                && f.Status != "Redeemed";
                if (isOverdue) scheduled += "\noverdue";

                var method = f.ContactMethod switch
                {
                    "Facebook Messenger" => "FB Messenger",
                    _ => f.ContactMethod
                };

                _grid.Rows.Add(
                    f.FollowUpId,
                    custCell,
                    scheduled,
                    f.Reason ?? "",
                    string.IsNullOrWhiteSpace(f.DiscountOffer) ? "—" : f.DiscountOffer,
                    method,
                    f.Status,
                    "Edit");
            }

            _grid.ResumeLayout();

            var from = total == 0 ? 0 : start + 1;
            var to = Math.Min(start + _pageSize, total);
            _showingLbl.Text = $"Showing {from}–{to} of {total} customers";

            RebuildPager(totalPages);
        }

        // ================================================================
        //  EDIT MODE
        // ================================================================
        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex];

            if (col.Name == "Actions")
            {
                var rowId = Convert.ToInt32(_grid.Rows[e.RowIndex].Cells["FollowUpId"].Value);

                if (_editingRowId == rowId)
                {
                    var btnText = _grid.Rows[e.RowIndex].Cells["Actions"].Value?.ToString() ?? "";
                    if (btnText == "Save")
                        _ = SaveEditAsync(e.RowIndex, rowId);
                }
                else
                {
                    EnterEditMode(e.RowIndex, rowId);
                }
            }
        }

        private void EnterEditMode(int rowIndex, int rowId)
        {
            RemoveEditOverlays();

            _editingRowId = rowId;
            _editingRowIndex = rowIndex;

            _grid.Rows[rowIndex].Cells["Actions"].Value = "Save";

            var cell = _grid.Rows[rowIndex].Cells["Status"];

            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Name = "statusCombo"
            };
            combo.Items.AddRange(StatusOptions);
            var current = cell.Value?.ToString() ?? "Sent";
            if (!StatusOptions.Contains(current))
                combo.Items.Add(current);
            combo.SelectedItem = current;

            var cancelBtn = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White,
                ForeColor = Navy,
                UseVisualStyleBackColor = false,
                Size = new Size(76, 32),
                Cursor = Cursors.Hand,
                Name = "cancelBtn"
            };
            cancelBtn.FlatAppearance.BorderColor = ButtonBorder;
            cancelBtn.FlatAppearance.MouseOverBackColor = PageBg;
            cancelBtn.Click += (s, e) => ExitEditMode();

            _grid.Controls.Add(combo);
            _grid.Controls.Add(cancelBtn);
            combo.BringToFront();
            cancelBtn.BringToFront();

            combo.SelectedIndexChanged += (s, e) =>
            {
                cell.Value = combo.SelectedItem?.ToString() ?? current;
            };

            RepositionEditOverlays();
        }

        private void RepositionEditOverlays()
        {
            // Guard against early calls during construction
            if (_grid == null) return;
            if (_editingRowIndex < 0 || _editingRowIndex >= _grid.RowCount) return;

            var st = _grid.GetCellDisplayRectangle(_grid.Columns["Status"].Index, _editingRowIndex, false);
            var ac = _grid.GetCellDisplayRectangle(_grid.Columns["Actions"].Index, _editingRowIndex, false);

            foreach (Control c in _grid.Controls)
            {
                if (c is ComboBox cb && cb.Name == "statusCombo")
                {
                    cb.Visible = st.Width > 0;
                    cb.Width = Math.Max(40, st.Width - 24);
                    cb.Location = new Point(st.X + 12, st.Y + (st.Height - cb.Height) / 2);
                }
                else if (c is Button btn && btn.Name == "cancelBtn")
                {
                    btn.Visible = ac.Width > 0;
                    btn.Location = new Point(ac.X + 100, ac.Y + 15);
                }
            }
        }

        private void RemoveEditOverlays()
        {
            // FIX: guard against a null grid (this method may be called
            // before the grid has been created)
            if (_grid == null) return;

            var toRemove = new List<Control>();
            foreach (Control c in _grid.Controls)
            {
                if (c is ComboBox cb && cb.Name == "statusCombo")
                    toRemove.Add(c);
                else if (c is Button b && b.Name == "cancelBtn")
                    toRemove.Add(c);
            }
            foreach (var c in toRemove)
            {
                _grid.Controls.Remove(c);
                c.Dispose();
            }
        }

        private void ExitEditMode()
        {
            RemoveEditOverlays();
            _editingRowId = null;
            _editingRowIndex = -1;
            ApplyFilter();
        }

        private async Task SaveEditAsync(int rowIndex, int rowId)
        {
            var dto = _all.FirstOrDefault(f => f.FollowUpId == rowId);
            if (dto == null) { ExitEditMode(); return; }

            string newStatus = dto.Status;
            foreach (Control c in _grid.Controls)
            {
                if (c is ComboBox cb && cb.Name == "statusCombo")
                {
                    newStatus = cb.SelectedItem?.ToString() ?? dto.Status;
                    break;
                }
            }

            var body = new
            {
                followUpId = dto.FollowUpId,
                customerId = dto.CustomerId,
                type = dto.Type,
                contactMethod = dto.ContactMethod,
                reason = dto.Reason,
                discountOffer = dto.DiscountOffer,
                notes = dto.Notes,
                scheduledDate = dto.ScheduledDate,
                validUntil = dto.ValidUntil,
                status = newStatus
            };

            try
            {
                var resp = await _http.PutAsJsonAsync($"api/follow-ups/{rowId}", body);

                if (resp.IsSuccessStatusCode)
                {
                    RemoveEditOverlays();
                    _editingRowId = null;
                    _editingRowIndex = -1;

                    await LoadLookupsAsync();
                    await LoadAsync();
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Save failed.\n\n{resp.StatusCode}\n\n{err}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Save failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        //  PAGER
        // ================================================================
        private void RebuildPager(int totalPages)
        {
            foreach (var btn in _pagerBar.Controls.OfType<Button>().ToList())
                btn.Dispose();

            int btnSize = 36;
            int gap = 6;
            int rightPad = 16;

            var next = new Button
            {
                Text = "›",
                Size = new Size(btnSize, btnSize),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                UseVisualStyleBackColor = false,
                Font = new Font("Segoe UI Semibold", 12f),
                Cursor = _page < totalPages ? Cursors.Hand : Cursors.Default,
                Enabled = _page < totalPages,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            next.FlatAppearance.BorderColor = CardBorder;
            next.FlatAppearance.MouseOverBackColor = PageBg;
            next.Click += (s, e) => { if (_page < totalPages) { _page++; ApplyFilter(); } };
            next.Location = new Point(_pagerBar.Width - btnSize - rightPad, 10);
            _pagerBar.Controls.Add(next);
            rightPad += btnSize + gap;

            var startPage = Math.Max(1, totalPages - 3);
            for (int p = totalPages; p >= startPage; p--)
            {
                var pb = new Button
                {
                    Text = p.ToString(),
                    Size = new Size(btnSize, btnSize),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = p == _page ? Navy : Color.White,
                    ForeColor = p == _page ? Color.White : Navy,
                    UseVisualStyleBackColor = false,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                pb.FlatAppearance.BorderColor = p == _page ? Navy : CardBorder;
                if (p != _page) pb.FlatAppearance.MouseOverBackColor = PageBg;
                var captured = p;
                pb.Click += (s, e) => { _page = captured; ApplyFilter(); };
                pb.Location = new Point(_pagerBar.Width - btnSize - rightPad, 10);
                _pagerBar.Controls.Add(pb);
                rightPad += btnSize + gap;
            }

            var prev = new Button
            {
                Text = "‹",
                Size = new Size(btnSize, btnSize),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                UseVisualStyleBackColor = false,
                Font = new Font("Segoe UI Semibold", 12f),
                Cursor = _page > 1 ? Cursors.Hand : Cursors.Default,
                Enabled = _page > 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            prev.FlatAppearance.BorderColor = CardBorder;
            prev.FlatAppearance.MouseOverBackColor = PageBg;
            prev.Click += (s, e) => { if (_page > 1) { _page--; ApplyFilter(); } };
            prev.Location = new Point(_pagerBar.Width - btnSize - rightPad, 10);
            _pagerBar.Controls.Add(prev);
        }

        // ================================================================
        //  LOG GRID
        // ================================================================
        private void BuildLog()
        {
            _logGrid.SuspendLayout();
            _logGrid.Rows.Clear();

            var recent = _all.OrderByDescending(f => f.ScheduledDate).Take(10).ToList();

            foreach (var f in recent)
            {
                var cust = _custById.TryGetValue(f.CustomerId, out var c) ? c : null;

                var idx = _logGrid.Rows.Add(
                    $"#{f.FollowUpId}",
                    cust?.CustomerName ?? $"id:{f.CustomerId}",
                    f.Type,
                    f.ContactMethod,
                    f.ScheduledDate.ToString("yyyy-MM-dd"),
                    f.Status,
                    f.Notes ?? "");

                _logGrid.Rows[idx].Cells["Notes"].ToolTipText = f.Notes ?? "";
            }

            _logGrid.ResumeLayout();

            _logRowCount = recent.Count;
            RelayoutUI();
        }

        // ================================================================
        //  OPEN ADD DIALOG
        // ================================================================
        private async void OpenAddDialog()
        {
            if (_customers == null || _customers.Count == 0)
            {
                MessageBox.Show(
                    "No customers found. Add customers in Manage Customers first.",
                    "No Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new FollowUpEditDialog(_customers);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _page = 1;
                await LoadLookupsAsync();
                await LoadAsync();
            }
        }

        public async void OpenAddDialogWithCustomers(List<int> preselectedCustomerIds)
        {
            await LoadLookupsAsync();

            if (_customers == null || _customers.Count == 0)
            {
                MessageBox.Show(
                    "No customers found. Add customers in Manage Customers first.",
                    "No Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new FollowUpEditDialog(_customers, preselectedCustomerIds);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                _page = 1;
                await LoadLookupsAsync();
                await LoadAsync();
            }
        }

        // ================================================================
        //  PUBLIC — refresh from outside
        // ================================================================
        public void RefreshData()
        {
            _ = ReloadAllAsync();
        }

        private async Task ReloadAllAsync()
        {
            await LoadLookupsAsync();
            await LoadAsync();
        }
    }
}