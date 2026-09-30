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

namespace CarwashServices.Roles.Admin
{
    public class UsersView : UserControl
    {
        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color StatusActiveFg = Color.FromArgb(0x15, 0x65, 0xC0);
        private static readonly Color StatusActiveBg = Color.FromArgb(0xEA, 0xF2, 0xFD);
        private static readonly Color StatusInactiveFg = Color.FromArgb(0x5C, 0x76, 0x8D);
        private static readonly Color StatusInactiveBg = Color.FromArgb(0xF0, 0xF4, 0xF8);
        private static readonly Color Slate = Color.FromArgb(0x39, 0x49, 0xAB);
        private static readonly Color SlateSoft = Color.FromArgb(0xE8, 0xEA, 0xF6);

        private static readonly Font FontAvatar = new("Segoe UI Semibold", 10f);
        private static readonly Font FontName = new("Segoe UI Semibold", 10f);
        private static readonly Font FontEmail = new("Segoe UI", 8.5f);
        private static readonly Font FontTag = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontIdSub = new("Segoe UI", 8.5f);

        // ---- Data ----
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<UserListItemDto> _all = new();
        private bool _isSwitchingBranch;

        // ---- UI ----
        private Panel _contentPanel = null!;
        private Panel _branchContextPanel = null!;
        private ComboBox _branchCombo = null!;
        private Label _companyLabel = null!;
        private Panel _rolesCard = null!;
        private DataGridView _grid = null!;
        private Panel _emptyStatePanel = null!;
        private Button _newUserBtn = null!;

        private const int PadX = 40;
        private int _gridTop = 270;
        private const int PageBottom = 24;

        // Aligned with Auth/UserRole.cs: 1 = SuperAdmin, 2 = Admin, 3 = Manager, 4 = Service Staff.
        private static readonly Dictionary<int, string> RoleNames = new()
        {
            { 1, "Admin" },
            { 2, "Admin" },
            { 3, "Manager" },
            { 4, "Service Staff" }
        };

        private readonly int? _roleFilter;
        private readonly string? _customTitle;

        public UsersView(int? roleFilter = null, string? customTitle = null)
        {
            _roleFilter = roleFilter;
            _customTitle = customTitle;

            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            InitializeUI();

            CarwashServices.Auth.SessionUser.BranchChanged += () =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                Invoke(async () =>
                {
                    SyncBranchCombo();
                    await LoadAsync();
                });
            };

            Load += async (s, e) =>
            {
                await LoadBranchesAsync();
                await LoadAsync();
            };
        }

        private void InitializeUI()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            Controls.Add(_contentPanel);

            _contentPanel.Controls.Add(new Label
            {
                Text = _customTitle ?? "Manage Users",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(PadX, 16),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = _roleFilter == 1
                    ? "Administrator accounts, system privileges, and account status."
                    : "System user accounts, roles, access permissions, and account status.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(PadX, 58),
                AutoSize = true
            });

            // ============ BRANCH & CONTEXT BAR ============
            _branchContextPanel = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, 94),
                Height = 60,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _branchContextPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _branchContextPanel.Width - 1, _branchContextPanel.Height - 1);
            };
            _contentPanel.Controls.Add(_branchContextPanel);

            string compName = !string.IsNullOrWhiteSpace(CarwashServices.Auth.SessionUser.CompanyName)
                ? CarwashServices.Auth.SessionUser.CompanyName
                : "CleanRide Car Wash";

            _companyLabel = new Label
            {
                Text = $"🏢 Company:  {compName}",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Navy,
                Location = new Point(20, 19),
                AutoSize = true
            };
            _branchContextPanel.Controls.Add(_companyLabel);

            var branchLbl = new Label
            {
                Text = "📍 Current Branch:",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Muted,
                Location = new Point(320, 20),
                AutoSize = true
            };
            _branchContextPanel.Controls.Add(branchLbl);

            _branchCombo = new ComboBox
            {
                Location = new Point(450, 16),
                Width = 240,
                Font = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _branchCombo.SelectedIndexChanged += async (s, e) =>
            {
                if (_isSwitchingBranch || _branchCombo.SelectedItem == null) return;
                if (_branchCombo.SelectedItem is ComboItem item && item.Id.HasValue && item.Id.Value > 0)
                {
                    CarwashServices.Auth.SessionUser.SetBranch(item.Id.Value, item.Text);
                    await LoadAsync();
                }
            };
            _branchContextPanel.Controls.Add(_branchCombo);

            _newUserBtn = new Button
            {
                Text = "+  Create User",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(150, 38),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _newUserBtn.FlatAppearance.BorderSize = 0;
            _newUserBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _newUserBtn.Click += (s, e) => OpenNewUserDialog();
            _branchContextPanel.Controls.Add(_newUserBtn);

            // ============ ROLES REFERENCE CARD ============
            _rolesCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, 164),
                Height = 84,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _rolesCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _rolesCard.Width - 1, _rolesCard.Height - 1);
            };
            _contentPanel.Controls.Add(_rolesCard);

            _rolesCard.Controls.Add(new Label
            {
                Text = "ROLES REFERENCE TABLE",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(20, 10),
                AutoSize = true
            });

            string[] chips =
            {
                "id:1  Super Admin",
                "id:2  Admin",
                "id:3  Manager",
                "id:4  Service Staff"
            };
            int cx = 20;
            foreach (var text in chips)
            {
                var chip = new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI Semibold", 9f),
                    ForeColor = Muted,
                    BackColor = Color.FromArgb(0xF1, 0xF4, 0xF9),
                    Padding = new Padding(12, 5, 12, 5),
                    AutoSize = true,
                    Location = new Point(cx, 36)
                };
                void Round()
                {
                    using var p = RoundedRect(new Rectangle(0, 0, chip.Width, chip.Height), chip.Height / 2);
                    chip.Region = new Region(p);
                }
                chip.SizeChanged += (s, e) => Round();
                Round();
                _rolesCard.Controls.Add(chip);
                cx += chip.PreferredWidth + 16;
            }

            _gridTop = 260;

            // ============ DATA GRID ============
            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                Location = new Point(PadX, _gridTop),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
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
                ColumnHeadersHeight = 44,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 68 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = FontCell,
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserId", HeaderText = "User ID", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Full Name / Email",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 240,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Role", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Branch", HeaderText = "Branch", Width = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "Date Created", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actions", HeaderText = "Actions", Width = 110 });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _grid.CellMouseMove += Grid_CellMouseMove;
            _contentPanel.Controls.Add(_grid);

            // ============ EMPTY STATE PANEL ============
            _emptyStatePanel = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, _gridTop),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _emptyStatePanel.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, _emptyStatePanel.Width - 1, _emptyStatePanel.Height - 1);
            };

            var emptyIcon = new Label
            {
                Text = "👥",
                Font = new Font("Segoe UI", 36f),
                ForeColor = Muted,
                Size = new Size(80, 70),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var emptyTitle = new Label
            {
                Text = "No users have been assigned to this branch yet.",
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = Navy,
                AutoSize = true
            };
            var emptySub = new Label
            {
                Text = "Click the button below to create and assign the first user for this branch.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Muted,
                AutoSize = true
            };
            var emptyCreateBtn = new Button
            {
                Text = "+  Create User",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(160, 42),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            emptyCreateBtn.FlatAppearance.BorderSize = 0;
            emptyCreateBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            emptyCreateBtn.Click += (s, e) => OpenNewUserDialog();

            _emptyStatePanel.Controls.Add(emptyIcon);
            _emptyStatePanel.Controls.Add(emptyTitle);
            _emptyStatePanel.Controls.Add(emptySub);
            _emptyStatePanel.Controls.Add(emptyCreateBtn);

            void LayoutEmptyState()
            {
                int w = _emptyStatePanel.ClientSize.Width;
                int h = _emptyStatePanel.ClientSize.Height;
                int cy = Math.Max(40, (h - 220) / 2);

                emptyIcon.Location = new Point((w - emptyIcon.Width) / 2, cy);
                emptyTitle.Location = new Point((w - emptyTitle.PreferredWidth) / 2, cy + 80);
                emptySub.Location = new Point((w - emptySub.PreferredWidth) / 2, cy + 115);
                emptyCreateBtn.Location = new Point((w - emptyCreateBtn.Width) / 2, cy + 155);
            }
            _emptyStatePanel.Resize += (s, e) => LayoutEmptyState();
            _contentPanel.Controls.Add(_emptyStatePanel);

            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;
                int contentW = Math.Max(0, w - 2 * PadX);

                _branchContextPanel.Width = contentW;
                _newUserBtn.Location = new Point(_branchContextPanel.Width - _newUserBtn.Width - 16, 11);

                _rolesCard.Width = contentW;
                _grid.SetBounds(PadX, _gridTop, contentW, Math.Max(0, h - _gridTop - PageBottom));
                _emptyStatePanel.SetBounds(PadX, _gridTop, contentW, Math.Max(0, h - _gridTop - PageBottom));
                LayoutEmptyState();
            }
            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();
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

        private async Task LoadBranchesAsync()
        {
            try
            {
                int cid = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var branches = await _http.GetFromJsonAsync<List<Dtos.BranchDto>>($"api/tenant/{cid}/branches") ?? new();

                _isSwitchingBranch = true;
                _branchCombo.Items.Clear();

                int selectIdx = 0;
                for (int i = 0; i < branches.Count; i++)
                {
                    var b = branches[i];
                    string label = b.IsMainBranch ? $"{b.BranchName} (Main)" : b.BranchName;
                    int idx = _branchCombo.Items.Add(new ComboItem(b.BranchId, label));

                    if (CarwashServices.Auth.SessionUser.CurrentBranchId == b.BranchId)
                    {
                        selectIdx = idx;
                    }
                    else if (!CarwashServices.Auth.SessionUser.CurrentBranchId.HasValue && b.IsMainBranch)
                    {
                        selectIdx = idx;
                    }
                }

                if (_branchCombo.Items.Count > 0)
                {
                    _branchCombo.SelectedIndex = selectIdx;
                    var curItem = _branchCombo.SelectedItem as ComboItem;
                    if (curItem != null && curItem.Id > 0 && !CarwashServices.Auth.SessionUser.CurrentBranchId.HasValue)
                    {
                        CarwashServices.Auth.SessionUser.SetBranch(curItem.Id, curItem.Text);
                    }
                }

                if (CarwashServices.Auth.SessionUser.IsSingleBranchUser)
                {
                    _branchCombo.Enabled = false;
                }

                _isSwitchingBranch = false;
            }
            catch
            {
                _isSwitchingBranch = false;
            }
        }

        private void SyncBranchCombo()
        {
            if (_branchCombo == null || _branchCombo.IsDisposed) return;
            _isSwitchingBranch = true;
            for (int i = 0; i < _branchCombo.Items.Count; i++)
            {
                if (_branchCombo.Items[i] is ComboItem ci && ci.Id == CarwashServices.Auth.SessionUser.CurrentBranchId)
                {
                    _branchCombo.SelectedIndex = i;
                    break;
                }
            }
            _isSwitchingBranch = false;
        }

        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _newUserBtn.Enabled = false;

                if (CarwashServices.Auth.SessionUser.IsLoggedIn)
                {
                    _http.DefaultRequestHeaders.Remove("X-Current-User-Id");
                    _http.DefaultRequestHeaders.Remove("X-User-Id");
                    _http.DefaultRequestHeaders.Add("X-Current-User-Id", CarwashServices.Auth.SessionUser.UserId.ToString());
                    _http.DefaultRequestHeaders.Add("X-User-Id", CarwashServices.Auth.SessionUser.UserId.ToString());
                }

                int? targetBranchId = null;
                if (_branchCombo.SelectedItem is ComboItem bi && bi.Id.HasValue && bi.Id.Value > 0)
                {
                    targetBranchId = bi.Id.Value;
                }
                else if (CarwashServices.Auth.SessionUser.CurrentBranchId.HasValue)
                {
                    targetBranchId = CarwashServices.Auth.SessionUser.CurrentBranchId.Value;
                }

                string url;
                if (CarwashServices.Auth.SessionUser.RoleId == 1 || CarwashServices.Auth.SessionUser.Role == CarwashServices.Auth.UserRole.SuperAdmin)
                {
                    url = targetBranchId.HasValue
                        ? $"api/users?branchId={targetBranchId.Value}"
                        : "api/users?roleIds=1,2,3,4";
                }
                else
                {
                    int cid = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                    url = targetBranchId.HasValue
                        ? $"api/users?companyId={cid}&branchId={targetBranchId.Value}"
                        : $"api/users?companyId={cid}";
                }

                var list = await _http.GetFromJsonAsync<List<UserListItemDto>>(url)
                           ?? new List<UserListItemDto>();
                _all = list;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load users.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newUserBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            var query = _all.AsEnumerable();
            if (_roleFilter.HasValue)
            {
                query = query.Where(x => x.RoleId == _roleFilter.Value);
            }

            var filteredList = query.OrderBy(x => x.UserId).ToList();

            foreach (var u in filteredList)
            {
                var nameCell = u.FullName;
                if (!string.IsNullOrWhiteSpace(u.Email))
                    nameCell += "\n" + u.Email;

                var roleCell = RoleNames.TryGetValue(u.RoleId, out var rn) ? rn : $"Role {u.RoleId}";
                roleCell += "\nid:" + u.RoleId;

                string branchDisplay = !string.IsNullOrWhiteSpace(u.BranchName)
                    ? u.BranchName
                    : (u.BranchId.HasValue ? $"Branch {u.BranchId}" : "Main Branch");

                bool isSelf = CarwashServices.Auth.SessionUser.IsLoggedIn && u.UserId == CarwashServices.Auth.SessionUser.UserId;
                string actionText = isSelf ? "" : "Edit";

                _grid.Rows.Add(
                    u.UserId,
                    nameCell,
                    roleCell,
                    branchDisplay,
                    u.Status,
                    u.CreatedAt.ToString("yyyy-MM-dd"),
                    actionText);
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();

            if (filteredList.Count == 0)
            {
                _grid.Visible = false;
                _emptyStatePanel.Visible = true;
                _emptyStatePanel.BringToFront();
            }
            else
            {
                _grid.Visible = true;
                _emptyStatePanel.Visible = false;
            }
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "Name": PaintNameCell(e); break;
                case "Role": PaintRoleCell(e); break;
                case "Branch": PaintBranchCell(e); break;
                case "Status": PaintStatusCell(e); break;
                case "Actions": PaintEditButton(e); break;
            }
        }

        private void PaintNameCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var name = parts[0];
            var email = parts.Length > 1 ? parts[1] : "";

            var b = e.CellBounds;

            if (b.Width <= 4 || b.Height <= 4)
            {
                e.Handled = true;
                return;
            }

            int avatarSize = Math.Min(38, Math.Max(16, b.Height - 12));
            int ax = b.X + 16;
            int ay = b.Y + (b.Height - avatarSize) / 2;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Navy))
                e.Graphics.FillEllipse(bg, ax, ay, avatarSize, avatarSize);

            string initials = Initials(name);
            var avatarRect = new Rectangle(ax, ay, avatarSize, avatarSize);

            TextRenderer.DrawText(
                e.Graphics,
                initials,
                FontAvatar,
                avatarRect,
                Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);

            int tx = ax + avatarSize + 14;
            int tw = Math.Max(10, b.Right - tx - 12);

            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine;

            int h1 = FontName.Height;
            int h2 = FontEmail.Height;
            int gap = 2;
            int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;

            TextRenderer.DrawText(e.Graphics, name, FontName,
                new Rectangle(tx, top, tw, h1), Navy, flags);
            TextRenderer.DrawText(e.Graphics, email, FontEmail,
                new Rectangle(tx, top + h1 + gap, tw, h2), Muted, flags);

            e.Handled = true;
        }

        private void PaintRoleCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var raw = Convert.ToString(e.Value) ?? "";
            var parts = raw.Split('\n');
            var roleName = parts[0];
            var idLine = parts.Length > 1 ? parts[1] : "";

            var b = e.CellBounds;
            int x = b.X + 16;
            int y = b.Y + 12;

            using (var font = FontTag)
            {
                var size = TextRenderer.MeasureText(e.Graphics, roleName, font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                int pillW = size.Width + 18;
                int pillH = size.Height + 6;
                var pill = new Rectangle(x, y, pillW, pillH);

                var (bg, fg) = RoleColors(roleName);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(pill, pillH / 2))
                using (var brush = new SolidBrush(bg))
                    e.Graphics.FillPath(brush, path);

                TextRenderer.DrawText(e.Graphics, roleName, font, pill, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            TextRenderer.DrawText(e.Graphics, idLine, FontIdSub,
                new Rectangle(x, y + 24, b.Width - 24, 18), Muted,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private void PaintBranchCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var branchName = Convert.ToString(e.Value) ?? "";
            if (string.IsNullOrWhiteSpace(branchName))
                branchName = "Main Branch";

            var b = e.CellBounds;
            int x = b.X + 16;
            int y = b.Y + (b.Height - 26) / 2;

            using (var font = FontTag)
            {
                var size = TextRenderer.MeasureText(e.Graphics, branchName, font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                int pillW = Math.Min(b.Width - 32, size.Width + 18);
                int pillH = 26;
                var pill = new Rectangle(x, y, pillW, pillH);

                var bg = Color.FromArgb(0xF0, 0xF4, 0xF8);
                var fg = Color.FromArgb(0x33, 0x4E, 0x68);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(pill, pillH / 2))
                using (var brush = new SolidBrush(bg))
                using (var pen = new Pen(Color.FromArgb(0xDC, 0xE2, 0xEC), 1f))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }

                TextRenderer.DrawText(e.Graphics, branchName, font, pill, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            e.Handled = true;
        }

        private void PaintStatusCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var status = Convert.ToString(e.Value) ?? "Active";
            bool isActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

            var b = e.CellBounds;
            int x = b.X + 16;
            int y = b.Y + (b.Height - 24) / 2;

            var bg = isActive ? StatusActiveBg : StatusInactiveBg;
            var fg = isActive ? StatusActiveFg : StatusInactiveFg;

            using (var font = FontTag)
            {
                var size = TextRenderer.MeasureText(e.Graphics, status, font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                int pillW = size.Width + 18;
                int pillH = 24;
                var pill = new Rectangle(x, y, pillW, pillH);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(pill, pillH / 2))
                using (var brush = new SolidBrush(bg))
                    e.Graphics.FillPath(brush, path);

                TextRenderer.DrawText(e.Graphics, status, font, pill, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            e.Handled = true;
        }

        private void PaintEditButton(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var actionText = Convert.ToString(e.Value) ?? "";
            if (string.IsNullOrWhiteSpace(actionText))
            {
                // Self row: do not paint the Edit button (completely hidden)
                e.Handled = true;
                return;
            }

            var b = e.CellBounds;
            int btnW = 68, btnH = 30;
            var rect = new Rectangle(
                b.X + (b.Width - btnW) / 2,
                b.Y + (b.Height - btnH) / 2,
                btnW, btnH);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(rect, 6))
            using (var fill = new SolidBrush(Color.White))
            using (var pen = new Pen(Blue, 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(e.Graphics, "Edit", FontTag, rect, Blue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

        private static (Color bg, Color fg) RoleColors(string role) => role switch
        {
            "Manager" => (SlateSoft, Slate),
            "Service Staff" => (BlueSoft, Blue),
            "Admin" => (Color.FromArgb(0xEA, 0xF2, 0xFD), Color.FromArgb(0x0D, 0x47, 0xA1)),
            "Super Admin" => (Color.FromArgb(0x0A, 0x16, 0x33), Color.White),
            _ => (Color.FromArgb(0xEE, 0xF1, 0xF6), Muted)
        };

        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        private void Grid_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "Actions")
            {
                var action = _grid.Rows[e.RowIndex].Cells["Actions"].Value?.ToString() ?? "";
                _grid.Cursor = (action == "Edit") ? Cursors.Hand : Cursors.Default;
            }
            else
            {
                _grid.Cursor = Cursors.Default;
            }
        }

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

            var actionText = _grid.Rows[e.RowIndex].Cells["Actions"].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(actionText) || actionText != "Edit") return;

            var idText = _grid.Rows[e.RowIndex].Cells["UserId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            // Extra client-side guard: do not allow editing own account
            if (CarwashServices.Auth.SessionUser.IsLoggedIn && id == CarwashServices.Auth.SessionUser.UserId)
            {
                return;
            }

            OpenEditUserDialog(id);
        }

        private async void OpenNewUserDialog()
        {
            int? currentBranchId = null;
            string? currentBranchName = null;

            if (_branchCombo.SelectedItem is ComboItem bi && bi.Id.HasValue && bi.Id.Value > 0)
            {
                currentBranchId = bi.Id.Value;
                currentBranchName = bi.Text;
            }
            else if (CarwashServices.Auth.SessionUser.CurrentBranchId.HasValue)
            {
                currentBranchId = CarwashServices.Auth.SessionUser.CurrentBranchId.Value;
                currentBranchName = CarwashServices.Auth.SessionUser.CurrentBranchName;
            }

            using var dlg = new UserEditDialog(null, currentBranchId, currentBranchName);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAsync();
            }
        }

        private async void OpenEditUserDialog(int userId)
        {
            using var dlg = new UserEditDialog(userId);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAsync();
            }
        }
    }
}