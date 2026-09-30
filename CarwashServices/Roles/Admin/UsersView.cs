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

        // ---- UI ----
        private Panel _contentPanel = null!;
        private DataGridView _grid = null!;
        private Button _newUserBtn = null!;

        private const int PadX = 40;
        private const int GridTop = 240;
        private const int PageBottom = 24;

        // Aligned with Auth/UserRole.cs: 1 = Admin, 2 = Manager, 3 = Service Staff, 4 = Super Admin.
        private static readonly Dictionary<int, string> RoleNames = new()
        {
            { 1, "Admin" },
            { 2, "Manager" },
            { 3, "Service Staff" },
            { 4, "Super Admin" }
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

            Load += async (s, e) => await LoadAsync();
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
                Location = new Point(PadX, 20),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = _roleFilter == 1
                    ? "Administrator accounts, system privileges, and account status."
                    : "System user accounts, roles, access permissions, and account status.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(PadX, 64),
                AutoSize = true
            });

            _newUserBtn = new Button
            {
                Text = "+  New User",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(160, 44),
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
            _contentPanel.Controls.Add(_newUserBtn);

            var rolesCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(PadX, 106),
                Height = 110,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            rolesCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, rolesCard.Width - 1, rolesCard.Height - 1);
            };
            _contentPanel.Controls.Add(rolesCard);

            rolesCard.Controls.Add(new Label
            {
                Text = "ROLES REFERENCE TABLE",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(20, 14),
                AutoSize = true
            });

            // Aligned with Auth/UserRole.cs: 1 = Admin, 2 = Manager, 3 = Service Staff, 4 = Super Admin.
            string[] chips =
            {
                "id:1  Admin",
                "id:2  Manager",
                "id:3  Service Staff",
                "id:4  Super Admin"
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
                    Padding = new Padding(14, 6, 14, 6),
                    AutoSize = true,
                    Location = new Point(cx, 44)
                };
                void Round()
                {
                    using var p = RoundedRect(new Rectangle(0, 0, chip.Width, chip.Height), chip.Height / 2);
                    chip.Region = new Region(p);
                }
                chip.SizeChanged += (s, e) => Round();
                Round();
                rolesCard.Controls.Add(chip);
                cx += chip.PreferredWidth + 20;
            }

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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UserId", HeaderText = "User ID", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "Full Name / Email",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 260,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Role", Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "Date Created", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actions", HeaderText = "Actions", Width = 120 });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseClick += Grid_CellMouseClick;
            _contentPanel.Controls.Add(_grid);

            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;
                int contentW = Math.Max(0, w - 2 * PadX);

                _newUserBtn.Location = new Point(w - _newUserBtn.Width - PadX, 20);
                rolesCard.Width = contentW;
                _grid.SetBounds(PadX, GridTop, contentW, Math.Max(0, h - GridTop - PageBottom));
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

        private async Task LoadAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _newUserBtn.Enabled = false;

                var url = (CarwashServices.Auth.SessionUser.RoleId == 4 || CarwashServices.Auth.SessionUser.Role == CarwashServices.Auth.UserRole.SuperAdmin)
                    ? "api/users?roleIds=1,2,3,4"
                    : $"api/users?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}";

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

            foreach (var u in query.OrderBy(x => x.UserId))
            {
                var nameCell = u.FullName;
                if (!string.IsNullOrWhiteSpace(u.Email))
                    nameCell += "\n" + u.Email;

                var roleCell = RoleNames.TryGetValue(u.RoleId, out var rn) ? rn : $"Role {u.RoleId}";
                roleCell += "\nid:" + u.RoleId;

                _grid.Rows.Add(
                    u.UserId,
                    nameCell,
                    roleCell,
                    u.Status,
                    u.CreatedAt.ToString("yyyy-MM-dd"),
                    "Edit");
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _grid.Columns[e.ColumnIndex].Name;

            switch (col)
            {
                case "Name": PaintNameCell(e); break;
                case "Role": PaintRoleCell(e); break;
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

            int avatarSize = Math.Min(40, Math.Max(16, b.Height - 8));
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
            int y = b.Y + 14;

            using (var font = FontTag)
            {
                var size = TextRenderer.MeasureText(e.Graphics, roleName, font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                int pillW = size.Width + 20;
                int pillH = size.Height + 8;
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
                new Rectangle(x, y + 26, b.Width - 24, 18), Muted,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

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
            var fg = active ? StatusActiveFg : StatusInactiveFg;
            var bg = active ? StatusActiveBg : StatusInactiveBg;

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

        private void PaintEditButton(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var b = e.CellBounds;
            int btnW = 72, btnH = 32;
            var rect = new Rectangle(
                b.X + (b.Width - btnW) / 2,
                b.Y + (b.Height - btnH) / 2,
                btnW, btnH);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(rect, 8))
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

        private void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

            var idText = _grid.Rows[e.RowIndex].Cells["UserId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText, out var id)) return;

            OpenEditUserDialog(id);
        }

        private async void OpenNewUserDialog()
        {
            using var dlg = new UserEditDialog(null);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                await LoadAsync();
        }

        private async void OpenEditUserDialog(int userId)
        {
            using var dlg = new UserEditDialog(userId);
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                await LoadAsync();
        }
    }
}