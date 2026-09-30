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
    /// Super Admin Module: Manage Admin Accounts.
    /// Lists all Administrator and Super Administrator accounts with role and status pills,
    /// avatar initials, creation dates, and full CRUD via AdminAccountEditDialog.
    /// </summary>
    public class ManageAdminAccountsView : UserControl
    {
        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color HeaderBg = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xE3, 0xF1, 0xFD);
        private static readonly Color Purple = Color.FromArgb(0x7C, 0x3A, 0xED);
        private static readonly Color PurpleSoft = Color.FromArgb(0xF3, 0xE8, 0xFF);
        private static readonly Color Green = Color.FromArgb(0x1E, 0x7A, 0x34);
        private static readonly Color GreenSoft = Color.FromArgb(0xE4, 0xF5, 0xE8);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color RedSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);

        // ================================================================
        //  Fonts
        // ================================================================
        private static readonly Font FontAvatar = new("Segoe UI Semibold", 10f);
        private static readonly Font FontName = new("Segoe UI Semibold", 10f);
        private static readonly Font FontEmail = new("Segoe UI", 8.5f);
        private static readonly Font FontTag = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontCell = new("Segoe UI", 9.5f);
        private static readonly Font FontIdSub = new("Segoe UI", 8.5f);

        // ================================================================
        //  Layout
        // ================================================================
        private const int PadX = 40;
        private const int GridTop = 270;
        private const int PageBottom = 24;

        // ================================================================
        //  Data & HTTP
        // ================================================================
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<UserListItemDto> _admins = new();

        // ================================================================
        //  Controls
        // ================================================================
        private Panel _contentPanel = null!;
        private DataGridView _grid = null!;
        private Button _newAdminBtn = null!;
        private Panel _rolesCard = null!;

        // Actions Three-Dot Menu & Hover
        private int _hoverAction = -1;
        private ContextMenuStrip? _activeMenu;
        private int _menuRow = -1;

        public ManageAdminAccountsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            if (SessionUser.RoleId != 4)
            {
                Controls.Add(new AccessDeniedView("Manage Admin Accounts", "Super Admin (Role 4)"));
                return;
            }

            InitializeUI();

            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadAdminsAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            Controls.Add(_contentPanel);

            // Breadcrumb
            _contentPanel.Controls.Add(new Label
            {
                Text = "Super Admin Modules  ›  Manage Admin Accounts",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 12),
                AutoSize = true,
                UseMnemonic = false
            });

            // Title
            _contentPanel.Controls.Add(new Label
            {
                Text = "Manage Admin Accounts",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(PadX, 34),
                AutoSize = true,
                UseMnemonic = false
            });

            // Subtitle
            _contentPanel.Controls.Add(new Label
            {
                Text = SuperAdminLabels.ManageAdminAccountsSubtitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(PadX, 80),
                AutoSize = true,
                UseMnemonic = false
            });

            // "+ New Admin" Button
            _newAdminBtn = new Button
            {
                Text = SuperAdminLabels.ButtonNewAdmin,
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(160, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _newAdminBtn.FlatAppearance.BorderSize = 0;
            _newAdminBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _newAdminBtn.Click += (s, e) => OpenNewAdminDialog();
            _contentPanel.Controls.Add(_newAdminBtn);

            // Roles Reference Card
            _rolesCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, 126),
                Height = 114,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _rolesCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, _rolesCard.Width - 1, _rolesCard.Height - 1), 10);
                using var borderPen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            };
            _contentPanel.Controls.Add(_rolesCard);

            _rolesCard.Controls.Add(new Label
            {
                Text = SuperAdminLabels.RolesReferenceTitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(20, 14),
                AutoSize = true,
                UseMnemonic = false
            });

            // Role chips: Super Admin (purple) and Admin (blue)
            var chipSuper = CreateRoleChip(SuperAdminLabels.ChipSuperAdmin, PurpleSoft, Purple, 20, 44);
            _rolesCard.Controls.Add(chipSuper);

            var chipAdmin = CreateRoleChip(SuperAdminLabels.ChipAdmin, BlueSoft, Blue, chipSuper.Right + 16, 44);
            _rolesCard.Controls.Add(chipAdmin);

            // DataGridView Table
            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                Location = new Point(PadX, GridTop),
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
                RowTemplate = { Height = 72 },
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
                ScrollBars = ScrollBars.Vertical,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserId", HeaderText = SuperAdminLabels.ColUserId, Width = 95 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = SuperAdminLabels.ColNameEmail,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 220,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = SuperAdminLabels.ColRole, Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = SuperAdminLabels.ColStatus, Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = SuperAdminLabels.ColDateCreated, Width = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Actions",
                HeaderText = SuperAdminLabels.ColActions,
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
                if (e.RowIndex >= 0 && e.RowIndex < _admins.Count)
                {
                    var idText = _grid.Rows[e.RowIndex].Cells["UserId"].Value?.ToString() ?? "";
                    if (int.TryParse(idText, out var id))
                    {
                        var admin = _admins.FirstOrDefault(u => u.UserId == id);
                        if (admin != null) OpenEditAdminDialog(admin);
                    }
                }
            };
            _contentPanel.Controls.Add(_grid);

            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;
                int contentW = Math.Max(0, w - 2 * PadX);

                _newAdminBtn.Location = new Point(w - _newAdminBtn.Width - PadX, 30);
                _rolesCard.Width = contentW;
                _grid.SetBounds(PadX, GridTop, contentW, Math.Max(0, h - GridTop - PageBottom));
            }

            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();

            ResumeLayout(true);
        }

        private static Label CreateRoleChip(string text, Color bg, Color fg, int x, int y)
        {
            var chip = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = fg,
                BackColor = bg,
                Padding = new Padding(14, 6, 14, 6),
                AutoSize = true,
                Location = new Point(x, y),
                UseMnemonic = false
            };
            void Round()
            {
                using var p = RoundedRect(new Rectangle(0, 0, chip.Width, chip.Height), chip.Height / 2);
                chip.Region = new Region(p);
            }
            chip.SizeChanged += (s, e) => Round();
            Round();
            return chip;
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

        public async Task LoadAdminsAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _newAdminBtn.Enabled = false;

                // Pull data from GET /api/users?roleIds=1,4
                var list = await _http.GetFromJsonAsync<List<UserListItemDto>>("api/users?roleIds=1,4")
                           ?? new List<UserListItemDto>();
                _admins = list;
                PopulateGrid();
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    $"Unable to reach the server to load admin accounts.\n\nPlease verify that the backend API is running on http://localhost:5180.\n\nDetails: {ex.Message}",
                    "Server Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load admin accounts from server.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newAdminBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void PopulateGrid()
        {
            CloseActiveMenu();
            SetHover(-1);
            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var u in _admins.OrderBy(x => x.UserId))
            {
                var nameCell = u.FullName;
                if (!string.IsNullOrWhiteSpace(u.Email))
                    nameCell += "\n" + u.Email;

                string roleName = u.RoleId == 4 ? SuperAdminLabels.ChipSuperAdmin : SuperAdminLabels.ChipAdmin;
                string roleCell = roleName;

                _grid.Rows.Add(
                    u.UserId,
                    nameCell,
                    roleCell,
                    u.Status,
                    u.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    "Actions");
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        // ================================================================
        //  Cell Painting
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "Name":
                    PaintNameCell(e);
                    break;
                case "Role":
                    PaintRoleCell(e);
                    break;
                case "Status":
                    PaintStatusCell(e);
                    break;
                case "Actions":
                    bool hover = _hoverAction == (e.RowIndex << 2);
                    SuperAdminActionMenuHelper.PaintActionsCell(e, hover);
                    break;
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

            int avatarSize = Math.Min(40, Math.Max(16, b.Height - 8));
            int ax = b.X + 16;
            int ay = b.Y + (b.Height - avatarSize) / 2;

            // Fetch row to see if Super Admin
            int rowIdx = e.RowIndex;
            bool isSuperAdmin = false;
            if (rowIdx >= 0 && rowIdx < _admins.Count)
            {
                isSuperAdmin = _admins[rowIdx].RoleId == 4;
            }

            Color avatarBg = isSuperAdmin ? Purple : Navy;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(avatarBg))
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

            var roleName = Convert.ToString(e.Value) ?? "";
            var b = e.CellBounds;
            int x = b.X + 16;

            bool isSuper = roleName.Contains("Super Admin", StringComparison.OrdinalIgnoreCase);
            Color pillBg = isSuper ? PurpleSoft : BlueSoft;
            Color pillFg = isSuper ? Purple : Blue;

            using (var font = FontTag)
            {
                var size = TextRenderer.MeasureText(e.Graphics, roleName, font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                int pillW = size.Width + 22;
                int pillH = size.Height + 8;
                int y = b.Y + (b.Height - pillH) / 2;
                var pill = new Rectangle(x, y, pillW, pillH);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(pill, pillH / 2);
                using var brush = new SolidBrush(pillBg);
                e.Graphics.FillPath(brush, path);

                TextRenderer.DrawText(e.Graphics, roleName, font, pill, pillFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            e.Handled = true;
        }

        private void PaintStatusCell(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            if (text.Length == 0) { e.Handled = true; return; }

            bool active = string.Equals(text, "Active", StringComparison.OrdinalIgnoreCase);
            var fg = active ? Green : Red;
            var bg = active ? GreenSoft : RedSoft;

            var b = e.CellBounds;
            using var font = FontTag;
            var size = TextRenderer.MeasureText(e.Graphics, text, font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int pillW = size.Width + 24;
            int pillH = size.Height + 10;
            var pill = new Rectangle(
                b.X + 16,
                b.Y + (b.Height - pillH) / 2,
                pillW, pillH);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(pill, pillH / 2))
            using (var brush = new SolidBrush(bg))
                e.Graphics.FillPath(brush, path);

            TextRenderer.DrawText(e.Graphics, text, font, pill, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            e.Handled = true;
        }

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

            var idText = _grid.Rows[e.RowIndex].Cells["UserId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            var existing = _admins.FirstOrDefault(u => u.UserId == id);
            if (existing == null) return;

            ShowActionsMenu(e.RowIndex, existing);
        }

        private void ShowActionsMenu(int rowIndex, UserListItemDto admin)
        {
            CloseActiveMenu();

            var menu = SuperAdminActionMenuHelper.CreateMenu();

            SuperAdminActionMenuHelper.AddMenuItem(menu, "Edit", () => OpenEditAdminDialog(admin));

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

        private async void OpenNewAdminDialog()
        {
            using var dlg = new AdminAccountEditDialog(null);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAdminsAsync();
            }
        }

        private async void OpenEditAdminDialog(UserListItemDto? user)
        {
            if (user == null) return;
            using var dlg = new AdminAccountEditDialog(user);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAdminsAsync();
            }
        }
    }
}
