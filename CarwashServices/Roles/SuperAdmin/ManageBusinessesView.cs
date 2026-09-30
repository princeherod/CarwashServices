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
    /// Super Admin Module: Manage Businesses (Tenants).
    /// Lists tenant companies with visible ID, Code, Name, Contact, Location, Associated Admin,
    /// Status, Created Date, and actions: View, Edit, and Activate/Deactivate.
    /// </summary>
    public class ManageBusinessesView : UserControl
    {
        // Palette
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color DeepNavy = Color.FromArgb(0x0C, 0x4A, 0x6E);
        private static readonly Color SlateBlue = Color.FromArgb(0x47, 0x55, 0x69);
        private static readonly Color SlateBlueSoft = Color.FromArgb(0xF1, 0xF5, 0xF9);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);

        // Fonts
        private static readonly Font FontTitle = new("Segoe UI Semibold", 22f);
        private static readonly Font FontSubtitle = new("Segoe UI", 9.5f);
        private static readonly Font FontKpiNum = new("Segoe UI Semibold", 20f);
        private static readonly Font FontKpiLbl = new("Segoe UI", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontTag = new("Segoe UI Semibold", 8.5f);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<CompanyListItemDto> _companies = new();
        private Panel _contentPanel = null!;
        private DataGridView _grid = null!;
        private TextBox _searchTxt = null!;
        private ComboBox _statusFilterCombo = null!;
        private Button _registerBtn = null!;

        // Actions Three-Dot Menu & Hover
        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;

        // Layout controls
        private Panel _totalCard = null!;
        private Panel _activeCard = null!;
        private Panel _inactiveCard = null!;
        private Panel _filterBar = null!;
        private Button _refreshBtn = null!;

        // KPI labels
        private Label _kpiTotalNum = null!;
        private Label _kpiActiveNum = null!;
        private Label _kpiInactiveNum = null!;

        private const int PadX = 36;
        private const int KpiY = 106;
        private const int KpiH = 80;
        private const int FilterY = 200;
        private const int FilterH = 44;
        private const int GridY = 256;
        private const int Gap = 16;

        public ManageBusinessesView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            if (SessionUser.RoleId != 4 && SessionUser.Role != UserRole.SuperAdmin)
            {
                Controls.Add(new AccessDeniedView("Manage Businesses", "Super Admin (Role 4)"));
                return;
            }

            InitializeUI();
            Sidebar.EnableDoubleBuffering(this);
            Load += async (s, e) => await LoadCompaniesAsync();
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

        private void InitializeUI()
        {
            SuspendLayout();

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                AutoScroll = false
            };
            Controls.Add(_contentPanel);

            // ---- Top Header Row ----
            var titleLbl = new Label
            {
                Text = "Manage Businesses",
                Font = FontTitle,
                ForeColor = Navy,
                Location = new Point(PadX, 20),
                AutoSize = true
            };
            var subtitleLbl = new Label
            {
                Text = "Onboard new businesses, configure tenant details, and manage platform access.",
                Font = FontSubtitle,
                ForeColor = Muted,
                Location = new Point(PadX, 64),
                AutoSize = true
            };
            _contentPanel.Controls.Add(titleLbl);
            _contentPanel.Controls.Add(subtitleLbl);

            _registerBtn = new Button
            {
                Text = "+ Register New Business",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Blue,
                Size = new Size(205, 38),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _registerBtn.FlatAppearance.BorderSize = 0;
            using (var p = RoundedRect(new Rectangle(0, 0, _registerBtn.Width, _registerBtn.Height), 6))
                _registerBtn.Region = new Region(p);

            _registerBtn.Click += (s, e) => OpenRegisterDialog();
            _contentPanel.Controls.Add(_registerBtn);

            // ---- KPI Cards Row (Clickable) ----
            _totalCard = CreateKpiCard("TOTAL BUSINESSES", out _kpiTotalNum, Navy, () =>
            {
                _statusFilterCombo.SelectedIndex = 0;
            });
            _activeCard = CreateKpiCard("ACTIVE TENANTS", out _kpiActiveNum, DeepNavy, () =>
            {
                _statusFilterCombo.SelectedItem = "Active";
            });
            _inactiveCard = CreateKpiCard("INACTIVE TENANTS", out _kpiInactiveNum, SlateBlue, () =>
            {
                _statusFilterCombo.SelectedItem = "Inactive";
            });

            _contentPanel.Controls.Add(_totalCard);
            _contentPanel.Controls.Add(_activeCard);
            _contentPanel.Controls.Add(_inactiveCard);

            // ---- Filter & Search Bar ----
            _filterBar = new Panel
            {
                BackColor = Color.White
            };
            _filterBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(CardBorder, 1);
                using var p = RoundedRect(new Rectangle(0, 0, _filterBar.Width - 1, _filterBar.Height - 1), 8);
                e.Graphics.DrawPath(pen, p);
            };
            _filterBar.Resize += (s, e) => _filterBar.Invalidate();

            var searchIcon = new Label
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 9f),
                Location = new Point(12, 12),
                AutoSize = true,
                ForeColor = Muted
            };
            _filterBar.Controls.Add(searchIcon);

            _searchTxt = new TextBox
            {
                Location = new Point(36, 11),
                Size = new Size(260, 26),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                ForeColor = Navy
            };
            _searchTxt.TextChanged += (s, e) => ApplyFilter();
            _filterBar.Controls.Add(_searchTxt);

            var filterLbl = new Label
            {
                Text = "Status:",
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Muted,
                Location = new Point(320, 12),
                AutoSize = true
            };
            _filterBar.Controls.Add(filterLbl);

            _statusFilterCombo = new ComboBox
            {
                Location = new Point(370, 8),
                Size = new Size(130, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _statusFilterCombo.Items.AddRange(new object[] { "All Statuses", "Active", "Inactive" });
            _statusFilterCombo.SelectedIndex = 0;
            _statusFilterCombo.SelectedIndexChanged += (s, e) => ApplyFilter();
            _filterBar.Controls.Add(_statusFilterCombo);

            _refreshBtn = new Button
            {
                Text = "Refresh",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = Navy,
                BackColor = Color.FromArgb(0xF1, 0xF5, 0xF9),
                Size = new Size(76, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _refreshBtn.FlatAppearance.BorderColor = CardBorder;
            _refreshBtn.Click += async (s, e) => await LoadCompaniesAsync();
            _filterBar.Controls.Add(_refreshBtn);

            _contentPanel.Controls.Add(_filterBar);

            // ---- DataGridView ----
            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = CardBorder,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 52 },
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Both
            };

            _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = HeaderBg,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            _grid.ColumnHeadersHeight = 40;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            _grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Navy,
                Font = FontCell,
                SelectionBackColor = BlueSoft,
                SelectionForeColor = Navy,
                Padding = new Padding(12, 0, 0, 0)
            };

            // Columns (ID, Code, Business Name, Contact, Location, Associated Admin, Status, Registered Date, Actions)
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CompanyId",
                HeaderText = "ID",
                Width = 55,
                MinimumWidth = 50
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CompanyCode",
                HeaderText = "Code",
                Width = 85,
                MinimumWidth = 75
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CompanyName",
                HeaderText = "Business Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 150,
                FillWeight = 120
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Contact",
                HeaderText = "Contact",
                Width = 150,
                MinimumWidth = 125
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Location",
                HeaderText = "Location",
                Width = 130,
                MinimumWidth = 110
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "AssociatedAdmin",
                HeaderText = "Associated Admin",
                Width = 185,
                MinimumWidth = 160
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                Width = 90,
                MinimumWidth = 85
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedAt",
                HeaderText = "Date Registered",
                Width = 125,
                MinimumWidth = 110
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "Actions",
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

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.MouseLeave += (s, e) => SetHover(-1);
            _grid.Scroll += (s, e) => { CloseActiveMenu(); SetHover(-1); };
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _companies.Count)
                {
                    var idText = _grid.Rows[e.RowIndex].Cells["CompanyId"].Value?.ToString() ?? "";
                    if (int.TryParse(idText, out var id))
                    {
                        var comp = _companies.FirstOrDefault(c => c.CompanyId == id);
                        if (comp != null) OpenEditDialog(comp);
                    }
                }
            };

            _contentPanel.Controls.Add(_grid);

            _contentPanel.Resize += (s, e) => Relayout();
            Resize += (s, e) => Relayout();
            Relayout();

            ResumeLayout(true);
        }

        private void Relayout()
        {
            if (_contentPanel == null || _grid == null) return;

            int w = _contentPanel.ClientSize.Width;
            int h = _contentPanel.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            int totalW = Math.Max(700, w - (PadX * 2));

            // Align "+ Register New Business" to top right
            if (_registerBtn != null)
            {
                _registerBtn.Location = new Point(Math.Max(PadX + 350, w - _registerBtn.Width - PadX), 24);
            }

            // Distribute 3 KPI cards evenly across totalW
            int cardW = Math.Max(180, (totalW - (Gap * 2)) / 3);
            if (_totalCard != null && _activeCard != null && _inactiveCard != null)
            {
                _totalCard.SetBounds(PadX, KpiY, cardW, KpiH);
                _activeCard.SetBounds(PadX + cardW + Gap, KpiY, cardW, KpiH);
                _inactiveCard.SetBounds(PadX + (cardW + Gap) * 2, KpiY, cardW, KpiH);
            }

            // Filter bar spans the full content width
            if (_filterBar != null)
            {
                _filterBar.SetBounds(PadX, FilterY, totalW, FilterH);
                if (_refreshBtn != null)
                {
                    _refreshBtn.Location = new Point(_filterBar.Width - _refreshBtn.Width - 12, 8);
                }
            }

            // Grid stretches horizontally and fills remaining vertical space
            int gridH = Math.Max(180, h - GridY - 24);
            _grid.SetBounds(PadX, GridY, totalW, gridH);
        }

        private static Panel CreateKpiCard(string title, out Label numLbl, Color numColor, Action? onClick = null)
        {
            var p = new Panel
            {
                BackColor = Color.White,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(CardBorder, 1);
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 8);
                e.Graphics.DrawPath(pen, path);
            };
            p.Resize += (s, e) => p.Invalidate();

            var titleL = new Label
            {
                Text = title,
                Font = FontKpiLbl,
                ForeColor = Muted,
                Location = new Point(16, 12),
                AutoSize = true,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
            };
            numLbl = new Label
            {
                Text = "—",
                Font = FontKpiNum,
                ForeColor = numColor,
                Location = new Point(14, 30),
                AutoSize = true,
                Cursor = onClick != null ? Cursors.Hand : Cursors.Default
            };

            if (onClick != null)
            {
                p.Click += (s, e) => onClick();
                titleL.Click += (s, e) => onClick();
                numLbl.Click += (s, e) => onClick();
            }

            p.Controls.Add(titleL);
            p.Controls.Add(numLbl);
            return p;
        }

        private async Task LoadCompaniesAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                if (_refreshBtn != null) _refreshBtn.Enabled = false;

                var list = await _http.GetFromJsonAsync<List<CompanyListItemDto>>("api/companies");
                _companies = list ?? new();
                UpdateKpis();
                ApplyFilter();
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    $"Unable to connect to the backend server to retrieve business records.\n\nPlease verify that the API server is active on http://localhost:5180.\n\nError: {ex.Message}",
                    "Server Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("Loading business records timed out. Please try refreshing.", "Request Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load companies: {ex.Message}", "Data Collection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                if (_refreshBtn != null) _refreshBtn.Enabled = true;
            }
        }

        private void UpdateKpis()
        {
            int total = _companies.Count;
            int active = _companies.Count(c => c.IsActive);
            int inactive = total - active;

            _kpiTotalNum.Text = total.ToString();
            _kpiActiveNum.Text = active.ToString();
            _kpiInactiveNum.Text = inactive.ToString();
        }

        private void ApplyFilter()
        {
            CloseActiveMenu();
            SetHover(-1);
            _grid.Rows.Clear();

            var search = _searchTxt.Text.Trim().ToLowerInvariant();
            var filter = _statusFilterCombo.SelectedItem?.ToString() ?? "All Statuses";

            var rows = _companies.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                rows = rows.Where(c =>
                    c.CompanyName.ToLowerInvariant().Contains(search) ||
                    c.CompanyCode.ToLowerInvariant().Contains(search) ||
                    (c.ContactEmail != null && c.ContactEmail.ToLowerInvariant().Contains(search)) ||
                    (c.City != null && c.City.ToLowerInvariant().Contains(search)) ||
                    (c.AdminUser != null && c.AdminUser.ToLowerInvariant().Contains(search)));
            }

            if (filter == "Active")
                rows = rows.Where(c => c.IsActive);
            else if (filter == "Inactive")
                rows = rows.Where(c => !c.IsActive);

            foreach (var c in rows)
            {
                string contact = !string.IsNullOrWhiteSpace(c.ContactEmail)
                    ? c.ContactEmail
                    : (!string.IsNullOrWhiteSpace(c.ContactPhone) ? c.ContactPhone : "—");

                string loc = !string.IsNullOrWhiteSpace(c.City)
                    ? (!string.IsNullOrWhiteSpace(c.Province) ? $"{c.City}, {c.Province}" : c.City)
                    : (!string.IsNullOrWhiteSpace(c.Country) ? c.Country : "—");

                string adminDisplay = string.IsNullOrWhiteSpace(c.AdminUser) || c.AdminUser.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)
                    ? "Unassigned"
                    : (string.IsNullOrWhiteSpace(c.AdminEmail) ? c.AdminUser : $"{c.AdminUser}\n{c.AdminEmail}");

                string status = c.IsActive ? "Active" : "Inactive";

                _grid.Rows.Add(
                    c.CompanyId,
                    c.CompanyCode,
                    c.CompanyName,
                    contact,
                    loc,
                    adminDisplay,
                    status,
                    c.CreatedAt.ToString("yyyy-MM-dd"),
                    "Actions");
            }

            _grid.ClearSelection();
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string col = _grid.Columns[e.ColumnIndex].Name;

            // Associated Admin Cell
            if (col == "AssociatedAdmin")
            {
                PaintAssociatedAdminCell(e);
            }
            // Status Pill
            else if (col == "Status")
            {
                e.Handled = true;
                e.PaintBackground(e.ClipBounds, true);

                var status = Convert.ToString(e.Value) ?? "Active";
                bool isActive = status == "Active";

                var pillBg = isActive ? BlueSoft : SlateBlueSoft;
                var pillFg = isActive ? DeepNavy : SlateBlue;

                int pillW = 76;
                int pillH = 24;
                var r = new Rectangle(
                    e.CellBounds.X + 10,
                    e.CellBounds.Y + (e.CellBounds.Height - pillH) / 2,
                    pillW,
                    pillH);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var bgBrush = new SolidBrush(pillBg);
                using var path = RoundedRect(r, 12);
                e.Graphics.FillPath(bgBrush, path);

                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
                TextRenderer.DrawText(e.Graphics, status, FontTag, r, pillFg, flags);
            }
            // Actions: Three-dot dropdown menu
            else if (col == "Actions")
            {
                bool hover = _hoverAction == (e.RowIndex << 2);
                SuperAdminActionMenuHelper.PaintActionsCell(e, hover);
            }
        }

        private void PaintAssociatedAdminCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Handled = true;
            e.PaintBackground(e.ClipBounds, true);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var name = parts[0];
            var email = parts.Length > 1 ? parts[1] : "";

            var b = e.CellBounds;
            if (b.Width <= 4 || b.Height <= 4) return;

            int avatarSize = Math.Min(30, Math.Max(16, b.Height - 14));
            int ax = b.X + 10;
            int ay = b.Y + (b.Height - avatarSize) / 2;

            bool isUnassigned = string.IsNullOrWhiteSpace(name) || name == "—" || name.Equals("Unassigned", StringComparison.OrdinalIgnoreCase);
            Color avatarColor = isUnassigned ? Color.FromArgb(0x94, 0xA3, 0xB8) : Navy;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(avatarColor))
                e.Graphics.FillEllipse(bg, ax, ay, avatarSize, avatarSize);

            string initials = isUnassigned ? "—" : Initials(name);
            var avatarRect = new Rectangle(ax, ay, avatarSize, avatarSize);

            using var fontAvatar = new Font("Segoe UI Semibold", 8f);
            TextRenderer.DrawText(
                e.Graphics,
                initials,
                fontAvatar,
                avatarRect,
                Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);

            int tx = ax + avatarSize + 10;
            int tw = Math.Max(10, b.Right - tx - 8);

            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine;

            using var fontName = new Font("Segoe UI Semibold", 9f);
            using var fontSub = new Font("Segoe UI", 8f);

            int h1 = fontName.Height;
            int h2 = fontSub.Height;
            int gap = 1;
            int top = b.Y + (b.Height - (h1 + (string.IsNullOrEmpty(email) ? 0 : h2 + gap))) / 2;

            TextRenderer.DrawText(e.Graphics, isUnassigned ? "Unassigned" : name, fontName,
                new Rectangle(tx, top, tw, h1), isUnassigned ? Muted : Navy, flags);

            if (!string.IsNullOrEmpty(email))
            {
                TextRenderer.DrawText(e.Graphics, email, fontSub,
                    new Rectangle(tx, top + h1 + gap, tw, h2), Muted, flags);
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

            var idText = _grid.Rows[e.RowIndex].Cells["CompanyId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            var company = _companies.FirstOrDefault(c => c.CompanyId == id);
            if (company == null) return;

            ShowActionsMenu(e.RowIndex, company);
        }

        private void ShowActionsMenu(int rowIndex, CompanyListItemDto company)
        {
            CloseActiveMenu();

            var menu = SuperAdminActionMenuHelper.CreateMenu();

            SuperAdminActionMenuHelper.AddMenuItem(menu, "View", () => OpenViewDialog(company));
            SuperAdminActionMenuHelper.AddMenuItem(menu, "Edit", () => OpenEditDialog(company));

            string toggleText = company.IsActive ? "Deactivate" : "Activate";
            SuperAdminActionMenuHelper.AddMenuItem(
                menu,
                toggleText,
                () => { _ = ToggleStatusAsync(company); },
                isDanger: company.IsActive);

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

        private void OpenRegisterDialog()
        {
            using var dlg = new RegisterBusinessDialog();
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                MessageBox.Show(
                    $"Business '{dlg.CreatedCompanyName}' ({dlg.CreatedCompanyCode}) and its initial administrator have been registered successfully!",
                    "Registration Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _ = LoadCompaniesAsync();
            }
        }

        private void OpenViewDialog(CompanyListItemDto company)
        {
            using var dlg = new BusinessViewDialog(company);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = LoadCompaniesAsync();
            }
        }

        private void OpenEditDialog(CompanyListItemDto company)
        {
            using var dlg = new BusinessEditDialog(company);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                _ = LoadCompaniesAsync();
            }
        }

        private async Task ToggleStatusAsync(CompanyListItemDto company)
        {
            if (company.IsActive)
            {
                var confirm = MessageBox.Show(
                    $"Are you sure you want to deactivate '{company.CompanyName}' ({company.CompanyCode})?\n\n" +
                    "Administrators and staff belonging to this company will not be able to perform business operations while inactive.\n" +
                    "All existing business records, subscriptions, and transactions will remain safe and preserved.",
                    "Deactivate Business",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes) return;
            }
            else
            {
                var confirm = MessageBox.Show(
                    $"Reactivate '{company.CompanyName}' ({company.CompanyCode})?\n\n" +
                    "This will restore full access for administrators and users linked to this company.",
                    "Reactivate Business",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;
            }

            try
            {
                var payload = new { isActive = !company.IsActive };
                using var resp = await _http.PutAsJsonAsync($"api/companies/{company.CompanyId}/status", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to update status: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await LoadCompaniesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Network error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "—") return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }
    }
}
