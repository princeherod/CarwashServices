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

namespace CarwashServices.Roles
{
    public class BranchesView : UserControl
    {
        // CleanRide Palette - Strictly Blue-based CRM
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x19, 0x2F); // Slate Navy
        private static readonly Color Muted = Color.FromArgb(0x64, 0x74, 0x8B); // Slate Muted
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xCF, 0xD8, 0xDC);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8); // CleanRide Steel Blue
        private static readonly Color BlueSoft = Color.FromArgb(0xDB, 0xEA, 0xFE); // Soft Ice Blue
        private static readonly Color ActiveBlue = Color.FromArgb(0x02, 0x84, 0xC7);
        private static readonly Color SlateArchived = Color.FromArgb(0x47, 0x55, 0x69);
        private static readonly Color SlateArchivedSoft = Color.FromArgb(0xF1, 0xF5, 0xF9);

        // Fonts
        private static readonly Font FontTitle = new("Segoe UI Semibold", 13.5f);
        private static readonly Font FontSubtitle = new("Segoe UI", 9f);
        private static readonly Font FontKpiNum = new("Segoe UI Semibold", 18f);
        private static readonly Font FontKpiLbl = new("Segoe UI", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontTag = new("Segoe UI Semibold", 8.5f);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<BranchDto> _allBranches = new();
        private List<BranchDto> _filteredBranches = new();

        private enum TabState { Active, Archived }
        private TabState _currentTab = TabState.Active;

        // UI Controls
        private Panel _bannerPanel = null!;
        private Panel _kpiPanel = null!;
        private Label _kpiTotalNum = null!;
        private Label _kpiActiveNum = null!;
        private Label _kpiArchivedNum = null!;
        private Label _kpiCurrentBranchNum = null!;

        private Panel _filterBar = null!;
        private Button _tabActiveBtn = null!;
        private Button _tabArchivedBtn = null!;
        private TextBox _searchTxt = null!;
        private Button _createBtn = null!;
        private Button _refreshBtn = null!;

        private DataGridView _grid = null!;
        private ContextMenuStrip _actionMenu = null!;

        private const int PadX = 36;
        private const int BannerY = 14;
        private const int BannerH = 50;
        private const int KpiY = 72;
        private const int KpiH = 72;
        private const int FilterY = 152;
        private const int FilterH = 46;
        private const int GridY = 206;

        public BranchesView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            InitializeUI();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadBranchesAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            // 0. CleanRide Enterprise Banner
            _bannerPanel = new Panel
            {
                Location = new Point(PadX, BannerY),
                Size = new Size(Width - PadX * 2, BannerH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            _bannerPanel.Paint += (s, e) =>
            {
                using var p = new Pen(CardBorder);
                e.Graphics.DrawRectangle(p, 0, 0, _bannerPanel.Width - 1, _bannerPanel.Height - 1);

                // Blue accent left edge
                using var accentBrush = new SolidBrush(Blue);
                e.Graphics.FillRectangle(accentBrush, 0, 0, 4, _bannerPanel.Height);
            };

            var bannerTitle = new Label
            {
                Text = "CLEANRIDE BRANCH MANAGEMENT",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 11.5f),
                Location = new Point(14, 8),
                AutoSize = true
            };
            var bannerSub = new Label
            {
                Text = "Single Company (COMP003) • Multi-Branch Architecture • Operational Transactions Isolated Per Branch",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(14, 28),
                AutoSize = true
            };

            var planBadge = new Label
            {
                Text = "ENTERPRISE MULTI-BRANCH PLAN • ACTIVE",
                ForeColor = Blue,
                BackColor = BlueSoft,
                Font = new Font("Segoe UI Semibold", 7.5f),
                Height = 26,
                Width = 240,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_bannerPanel.Width - 252, 12)
            };
            _bannerPanel.Controls.AddRange(new Control[] { bannerTitle, bannerSub, planBadge });
            Controls.Add(_bannerPanel);

            // 1. KPI Panel
            _kpiPanel = new Panel
            {
                Location = new Point(PadX, KpiY),
                Size = new Size(Width - PadX * 2, KpiH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            int cardW = 240;
            int gap = 16;

            var totalCard = CreateKpiCard("TOTAL BRANCHES", out _kpiTotalNum, 0, cardW);
            var activeCard = CreateKpiCard("ACTIVE BRANCHES", out _kpiActiveNum, cardW + gap, cardW);
            var archivedCard = CreateKpiCard("ARCHIVED BRANCHES", out _kpiArchivedNum, (cardW + gap) * 2, cardW);
            var activeBranchCard = CreateKpiCard("CURRENT ACTIVE CONTEXT", out _kpiCurrentBranchNum, (cardW + gap) * 3, cardW);

            _kpiPanel.Controls.AddRange(new Control[] { totalCard, activeCard, archivedCard, activeBranchCard });
            Controls.Add(_kpiPanel);

            // 2. Filter Bar & Tabs
            _filterBar = new Panel
            {
                Location = new Point(PadX, FilterY),
                Size = new Size(Width - PadX * 2, FilterH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            _filterBar.Paint += (s, e) =>
            {
                using var p = new Pen(CardBorder);
                e.Graphics.DrawRectangle(p, 0, 0, _filterBar.Width - 1, _filterBar.Height - 1);
            };

            _tabActiveBtn = new Button
            {
                Text = "Active Branches",
                Location = new Point(8, 6),
                Size = new Size(130, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = BlueSoft,
                ForeColor = Blue,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand
            };
            _tabActiveBtn.FlatAppearance.BorderSize = 0;
            _tabActiveBtn.Click += (s, e) => SwitchTab(TabState.Active);

            _tabArchivedBtn = new Button
            {
                Text = "Archived Branches",
                Location = new Point(144, 6),
                Size = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand
            };
            _tabArchivedBtn.FlatAppearance.BorderSize = 0;
            _tabArchivedBtn.Click += (s, e) => SwitchTab(TabState.Archived);

            var searchLabel = new Label
            {
                Text = "Search:",
                Location = new Point(300, 15),
                AutoSize = true,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f)
            };

            _searchTxt = new TextBox
            {
                Location = new Point(356, 10),
                Size = new Size(220, 28),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            _searchTxt.TextChanged += (s, e) => ApplyFilter();

            _createBtn = new Button
            {
                Text = "+ Create Branch",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_filterBar.Width - 230, 6),
                Size = new Size(120, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand
            };
            _createBtn.FlatAppearance.BorderSize = 0;
            _createBtn.Click += async (s, e) => await OnCreateBranchClickedAsync();

            _refreshBtn = new Button
            {
                Text = "Refresh",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_filterBar.Width - 100, 6),
                Size = new Size(90, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            _refreshBtn.FlatAppearance.BorderColor = CardBorder;
            _refreshBtn.Click += async (s, e) => await LoadBranchesAsync();

            _filterBar.Controls.AddRange(new Control[]
            {
                _tabActiveBtn, _tabArchivedBtn, searchLabel, _searchTxt, _createBtn, _refreshBtn
            });
            Controls.Add(_filterBar);

            // 3. Grid
            InitGrid();

            ResumeLayout(true);
        }

        private static Panel CreateKpiCard(string caption, out Label numLbl, int x, int width)
        {
            var p = new Panel
            {
                Location = new Point(x, 0),
                Size = new Size(width, KpiH),
                BackColor = Color.White
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };

            var cap = new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = FontKpiLbl,
                Location = new Point(14, 10),
                AutoSize = true
            };

            numLbl = new Label
            {
                Text = "0",
                ForeColor = Navy,
                Font = FontKpiNum,
                Location = new Point(12, 28),
                AutoSize = true
            };

            p.Controls.AddRange(new Control[] { cap, numLbl });
            return p;
        }

        private void InitGrid()
        {
            _grid = new DataGridView
            {
                Location = new Point(PadX, GridY),
                Size = new Size(Width - PadX * 2, Height - GridY - 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(0xEE, 0xF2, 0xF7),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 56 },
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            // Header style (no blue highlight on first header)
            var hs = _grid.ColumnHeadersDefaultCellStyle;
            hs.BackColor = HeaderBg;
            hs.ForeColor = Muted;
            hs.SelectionBackColor = HeaderBg;
            hs.SelectionForeColor = Muted;
            hs.Font = new Font("Segoe UI Semibold", 8.5f);
            hs.Padding = new Padding(12, 0, 0, 0);
            hs.WrapMode = DataGridViewTriState.False;
            _grid.ColumnHeadersHeight = 44;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            // Cell style (soft selection)
            var cs = _grid.DefaultCellStyle;
            cs.Font = FontCell;
            cs.ForeColor = Navy;
            cs.BackColor = Color.White;
            cs.SelectionBackColor = BlueSoft;
            cs.SelectionForeColor = Navy;
            cs.Padding = new Padding(12, 0, 0, 0);
            cs.WrapMode = DataGridViewTriState.False;

            void AddCol(string name, string header, float weight)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = weight,
                    MinimumWidth = 80,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }

            AddCol("BranchCode", "CODE", 10);
            AddCol("BranchName", "BRANCH NAME", 18);
            AddCol("Location", "LOCATION / CITY", 22);
            AddCol("Contact", "CONTACT", 13);
            AddCol("Type", "BRANCH TYPE", 13);
            AddCol("Admin", "ADMIN", 16);
            AddCol("Status", "STATUS", 10);
            AddCol("Activity", "ACTIVITY", 14);

            // Fixed-width, centered "⋮" column
            var actionsCol = new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = "",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 70,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            actionsCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            actionsCol.DefaultCellStyle.Padding = new Padding(0);
            actionsCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Add(actionsCol);

            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellClick += Grid_CellClick;
            _grid.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "Actions")
                    _grid.Cursor = Cursors.Hand;
            };
            _grid.CellMouseLeave += (s, e) => _grid.Cursor = Cursors.Default;

            Controls.Add(_grid);
        }

        private void SwitchTab(TabState tab)
        {
            _currentTab = tab;
            if (_currentTab == TabState.Active)
            {
                _tabActiveBtn.BackColor = BlueSoft;
                _tabActiveBtn.ForeColor = Blue;
                _tabArchivedBtn.BackColor = Color.White;
                _tabArchivedBtn.ForeColor = Muted;
            }
            else
            {
                _tabArchivedBtn.BackColor = BlueSoft;
                _tabArchivedBtn.ForeColor = Blue;
                _tabActiveBtn.BackColor = Color.White;
                _tabActiveBtn.ForeColor = Muted;
            }
            ApplyFilter();
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var list = await _http.GetFromJsonAsync<List<BranchDto>>($"api/tenant/{companyId}/branches?includeArchived=true") ?? new();
                _allBranches = list;

                // Update KPIs
                int total = _allBranches.Count;
                int active = _allBranches.Count(b => !b.IsArchived && b.IsActive);
                int archived = _allBranches.Count(b => b.IsArchived);

                _kpiTotalNum.Text = total.ToString();
                _kpiActiveNum.Text = active.ToString();
                _kpiArchivedNum.Text = archived.ToString();
                _kpiCurrentBranchNum.Text = string.IsNullOrWhiteSpace(SessionUser.CurrentBranchName) ? "All Branches" : SessionUser.CurrentBranchName;

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading branches: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            var query = _allBranches.AsEnumerable();

            if (_currentTab == TabState.Active)
            {
                query = query.Where(b => !b.IsArchived);
            }
            else
            {
                query = query.Where(b => b.IsArchived);
            }

            var text = _searchTxt.Text.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(text))
            {
                query = query.Where(b =>
                    b.BranchCode.ToLowerInvariant().Contains(text) ||
                    b.BranchName.ToLowerInvariant().Contains(text) ||
                    (b.City ?? "").ToLowerInvariant().Contains(text) ||
                    (b.Address ?? "").ToLowerInvariant().Contains(text));
            }

            _filteredBranches = query.ToList();
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            _grid.Rows.Clear();

            foreach (var b in _filteredBranches)
            {
                string loc = b.DisplayLocation;
                string type = b.IsMainBranch ? "★ Main Branch" : "Secondary Branch";
                string admin = !string.IsNullOrWhiteSpace(b.AssignedAdminName) ? b.AssignedAdminName : "Unassigned";
                string status = b.IsArchived ? "Archived" : (b.IsActive ? "Active" : "Inactive");
                string activity = $"{b.ServiceRequestsCount} reqs • {b.CustomersCount} cust";
                string actions = "⋮";

                int idx = _grid.Rows.Add(
                    b.BranchCode,
                    b.BranchName,
                    loc,
                    b.ContactNumber ?? "—",
                    type,
                    admin,
                    status,
                    activity,
                    actions
                );
                _grid.Rows[idx].Tag = b;
                _grid.Rows[idx].Cells["Location"].ToolTipText = loc;
                if (!string.IsNullOrWhiteSpace(b.AssignedAdminEmail))
                    _grid.Rows[idx].Cells["Admin"].ToolTipText = $"{b.AssignedAdminName} ({b.AssignedAdminEmail})";
            }
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var colName = _grid.Columns[e.ColumnIndex].Name;
            var b = _grid.Rows[e.RowIndex].Tag as BranchDto;
            if (b == null) return;

            if (colName == "Type")
            {
                if (b.IsMainBranch)
                {
                    e.CellStyle.ForeColor = Blue;
                    e.CellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
                }
                else
                {
                    e.CellStyle.ForeColor = Muted;
                }
            }
            else if (colName == "Admin")
            {
                if (string.IsNullOrWhiteSpace(b.AssignedAdminName) || b.AssignedAdminName == "Unassigned")
                {
                    e.CellStyle.ForeColor = Muted;
                    e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Italic);
                }
                else
                {
                    e.CellStyle.ForeColor = Navy;
                    e.CellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
                }
            }
            else if (colName == "Status")
            {
                if (b.IsArchived)
                {
                    e.CellStyle.ForeColor = SlateArchived;
                }
                else if (b.IsActive)
                {
                    e.CellStyle.ForeColor = Blue;
                    e.CellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
                }
                else
                {
                    e.CellStyle.ForeColor = Muted;
                }
            }
            else if (colName == "Actions")
            {
                e.CellStyle.ForeColor = Muted;
                e.CellStyle.Font = new Font("Segoe UI Symbol", 14f, FontStyle.Bold);
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;
            if (_grid.Rows[e.RowIndex].Tag is not BranchDto b) return;

            ShowActionMenu(b, e.ColumnIndex, e.RowIndex);
        }

        private void ShowActionMenu(BranchDto b, int colIndex, int rowIndex)
        {
            _actionMenu?.Dispose();
            _actionMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                Padding = new Padding(2)
            };

            ToolStripMenuItem Item(string text, Func<Task> action, Color? color = null, bool enabled = true)
            {
                var it = new ToolStripMenuItem(text)
                {
                    Enabled = enabled,
                    ForeColor = enabled ? (color ?? Navy) : Muted,
                    Padding = new Padding(4, 6, 4, 6)
                };
                it.Click += async (s, ev) => await action();
                return it;
            }

            _actionMenu.Items.Add(Item("View", () => { ShowBranchDetails(b); return Task.CompletedTask; }));

            if (_currentTab == TabState.Active)
            {
                _actionMenu.Items.Add(Item("Edit", () => EditBranchAsync(b)));
                _actionMenu.Items.Add(Item("Assign Admin", () => AssignAdminAsync(b), Blue));

                bool isCurrent = SessionUser.CurrentBranchId == b.BranchId;
                bool canSwitch = !isCurrent && !SessionUser.IsSingleBranchUser;
                _actionMenu.Items.Add(Item(
                    isCurrent ? "Current Context" : "Switch To",
                    () =>
                    {
                        SessionUser.SetBranch(b.BranchId, b.BranchName);
                        _kpiCurrentBranchNum.Text = b.BranchName;
                        MessageBox.Show(
                            $"Context switched to '{b.BranchName}'. Operational modules will now reflect transactions for this branch.",
                            "Active Branch Switched", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        PopulateGrid();
                        return Task.CompletedTask;
                    },
                    Blue,
                    enabled: canSwitch));

                _actionMenu.Items.Add(new ToolStripSeparator());
                _actionMenu.Items.Add(Item("Archive", () => ArchiveBranchAsync(b), SlateArchived));
            }
            else
            {
                _actionMenu.Items.Add(Item("Restore", () => RestoreBranchAsync(b), Blue));
            }

            var rect = _grid.GetCellDisplayRectangle(colIndex, rowIndex, false);
            int menuW = _actionMenu.PreferredSize.Width;
            // right-align the menu to the ⋮ cell, just under it
            _actionMenu.Show(_grid, new Point(rect.Right - menuW, rect.Bottom - 6));
        }

        private void ShowBranchDetails(BranchDto b)
        {
            using var dlg = new BranchDetailsDialog(b);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _kpiCurrentBranchNum.Text = SessionUser.CurrentBranchName;
                _ = LoadBranchesAsync();
            }
        }

        private async Task AssignAdminAsync(BranchDto b)
        {
            using var dlg = new AssignBranchAdminDialog(b);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                await LoadBranchesAsync();
            }
        }

        private async Task EditBranchAsync(BranchDto b)
        {
            using var dlg = new BranchEditDialog(b);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                await LoadBranchesAsync();
            }
        }

        private async Task ArchiveBranchAsync(BranchDto b)
        {
            if (b.IsMainBranch)
            {
                MessageBox.Show("Cannot archive the Primary / Main Branch. Designate another branch as Main Branch first.", "Action Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to archive branch '{b.BranchName}'?\n\nHistorical transactions, services, and customers will remain intact.",
                "Confirm Archive Branch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var resp = await _http.PostAsJsonAsync($"api/tenant/{companyId}/branches/{b.BranchId}/archive", new { ArchivedBy = SessionUser.FullName });
                if (resp.IsSuccessStatusCode)
                {
                    if (SessionUser.CurrentBranchId == b.BranchId)
                    {
                        SessionUser.SetBranch(null, "All Branches");
                    }
                    await LoadBranchesAsync();
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to archive branch: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RestoreBranchAsync(BranchDto b)
        {
            var confirm = MessageBox.Show(
                $"Restore branch '{b.BranchName}' back to active status?",
                "Restore Branch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var resp = await _http.PostAsJsonAsync($"api/tenant/{companyId}/branches/{b.BranchId}/restore", new { });
                if (resp.IsSuccessStatusCode)
                {
                    await LoadBranchesAsync();
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to restore branch: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task OnCreateBranchClickedAsync()
        {
            // Subscription check
            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var cap = await _http.GetFromJsonAsync<BranchCapabilityDto>($"api/tenant/{companyId}/branches/capability");

                if (cap == null || !cap.MultiBranchEnabled)
                {
                    MessageBox.Show(
                        "Your active subscription plan does not support multiple branches.\n\nPlease upgrade to the Enterprise Multi-Branch tier to create and manage multiple branches.",
                        "Subscription Restriction",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                using var dlg = new BranchEditDialog();
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    await LoadBranchesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not verify subscription capability: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}