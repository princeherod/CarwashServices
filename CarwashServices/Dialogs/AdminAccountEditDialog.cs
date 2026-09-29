using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;
using CarwashServices.Roles.SuperAdmin;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Dialog for creating and editing Admin & Super Admin accounts.
    /// Matches the styling of FollowUpEditDialog.
    /// </summary>
    public class AdminAccountEditDialog : Form
    {
        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color DangerSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);

        // ---- State ----
        private readonly UserListItemDto? _existing;
        private readonly bool _isEdit;

        // ---- Controls ----
        private TextBox _fullNameTxt = null!;
        private TextBox _emailTxt = null!;
        private ComboBox _roleCombo = null!;
        private ComboBox _statusCombo = null!;
        private TextBox _passwordTxt = null!;
        private TextBox _confirmTxt = null!;
        private Label _passwordHintLbl = null!;
        private Label _errorLbl = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;
        private Button? _deleteBtn;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static readonly (int Id, string Name)[] AdminRoles =
        {
            (4, "Super Admin"),
            (1, "Admin")
        };

        public AdminAccountEditDialog(UserListItemDto? existing = null)
        {
            _existing = existing;
            _isEdit = existing != null;

            InitializeForm();

            if (_isEdit)
            {
                PreloadData();
            }

            Sidebar.EnableDoubleBuffering(this);
        }

        private static Label Caption(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

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

        private void InitializeForm()
        {
            Text = _isEdit ? $"{SuperAdminLabels.DialogEditAdminTitle} — #{_existing!.UserId}" : SuperAdminLabels.DialogNewAdminTitle;
            ClientSize = new Size(640, 580);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 16)
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = _isEdit ? SuperAdminLabels.DialogEditAdminTitle : SuperAdminLabels.DialogNewAdminTitle,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(32, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(titleLbl);

            var subtitleLbl = new Label
            {
                Text = SuperAdminLabels.DialogAdminSubtitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(32, 42),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(subtitleLbl);
            Controls.Add(header);

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White,
                Padding = new Padding(24, 16, 24, 16)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                Location = new Point(32, 12),
                Size = new Size(280, 52),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            footer.Controls.Add(_errorLbl);

            _saveBtn = new Button
            {
                Text = _isEdit ? "Save Changes" : "Create Account",
                Font = new Font("Segoe UI Semibold", 9.5f),
                Size = new Size(130, 40),
                Location = new Point(footer.ClientSize.Width - 130 - 32, 18),
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.FlatAppearance.MouseOverBackColor = AccentHover;
            _saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(_saveBtn);

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9.5f),
                Size = new Size(90, 40),
                Location = new Point(footer.ClientSize.Width - 130 - 32 - 100, 18),
                BackColor = Color.White,
                ForeColor = Muted,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            _cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;
            footer.Controls.Add(_cancelBtn);

            if (_isEdit)
            {
                _deleteBtn = new Button
                {
                    Text = "Delete",
                    Font = new Font("Segoe UI", 9f),
                    Size = new Size(80, 40),
                    Location = new Point(footer.ClientSize.Width - 130 - 32 - 100 - 90, 18),
                    BackColor = DangerSoft,
                    ForeColor = Danger,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                _deleteBtn.FlatAppearance.BorderSize = 0;
                _deleteBtn.Click += async (s, e) => await DeleteAsync();
                footer.Controls.Add(_deleteBtn);
            }

            Controls.Add(footer);

            // ---- Body Container ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true,
                Padding = new Padding(32, 20, 32, 20)
            };
            Controls.Add(body);
            body.BringToFront();

            int leftColX = 32;
            int rightColX = 328;
            int colW = 276;
            int y = 16;

            // Row 1: Full Name & Email
            body.Controls.Add(Caption(SuperAdminLabels.FieldFullName, leftColX, y));
            body.Controls.Add(Caption(SuperAdminLabels.FieldEmailAddress, rightColX, y));
            y += 22;

            _fullNameTxt = CreateTextBox(leftColX, y, colW);
            body.Controls.Add(_fullNameTxt);

            _emailTxt = CreateTextBox(rightColX, y, colW);
            body.Controls.Add(_emailTxt);
            y += 50;

            // Row 2: Role & Status
            body.Controls.Add(Caption(SuperAdminLabels.FieldRole, leftColX, y));
            body.Controls.Add(Caption(SuperAdminLabels.FieldStatus, rightColX, y));
            y += 22;

            _roleCombo = new ComboBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White
            };
            foreach (var r in AdminRoles)
            {
                _roleCombo.Items.Add(new RoleItem(r.Id, r.Name));
            }
            _roleCombo.SelectedIndex = 0;
            body.Controls.Add(_roleCombo);

            _statusCombo = new ComboBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White
            };
            _statusCombo.Items.Add("Active");
            _statusCombo.Items.Add("Inactive");
            _statusCombo.SelectedIndex = 0;
            body.Controls.Add(_statusCombo);
            y += 54;

            // Divider
            var div = new Panel
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW * 2 + 20, 1),
                BackColor = BorderSoft
            };
            body.Controls.Add(div);
            y += 18;

            // Row 3: Security & Credentials
            var secLbl = new Label
            {
                Text = SuperAdminLabels.HeaderSecurityCredentials,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(secLbl);

            _passwordHintLbl = new Label
            {
                Text = _isEdit ? SuperAdminLabels.HintPasswordOptional : SuperAdminLabels.HintPasswordRequired,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(leftColX + secLbl.PreferredWidth + 10, y + 1),
                AutoSize = true
            };
            body.Controls.Add(_passwordHintLbl);
            y += 28;

            // Row 4: Password & Confirm
            body.Controls.Add(Caption(_isEdit ? SuperAdminLabels.FieldPasswordOptional : SuperAdminLabels.FieldPasswordRequired, leftColX, y));
            body.Controls.Add(Caption(_isEdit ? SuperAdminLabels.FieldConfirmPasswordOptional : SuperAdminLabels.FieldConfirmPasswordRequired, rightColX, y));
            y += 22;

            _passwordTxt = CreateTextBox(leftColX, y, colW);
            _passwordTxt.UseSystemPasswordChar = true;
            body.Controls.Add(_passwordTxt);

            _confirmTxt = CreateTextBox(rightColX, y, colW);
            _confirmTxt.UseSystemPasswordChar = true;
            body.Controls.Add(_confirmTxt);
            y += 50;
        }

        private static TextBox CreateTextBox(int x, int y, int width)
        {
            var tb = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 32),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            return tb;
        }

        private void PreloadData()
        {
            if (_existing == null) return;

            _fullNameTxt.Text = _existing.FullName;
            _emailTxt.Text = _existing.Email;

            for (int i = 0; i < _roleCombo.Items.Count; i++)
            {
                if (_roleCombo.Items[i] is RoleItem ri && ri.Id == _existing.RoleId)
                {
                    _roleCombo.SelectedIndex = i;
                    break;
                }
            }

            int statusIdx = _statusCombo.Items.IndexOf(_existing.Status);
            if (statusIdx >= 0) _statusCombo.SelectedIndex = statusIdx;
        }

        private async Task SaveAsync()
        {
            _errorLbl.Text = "";

            var name = _fullNameTxt.Text.Trim();
            var email = _emailTxt.Text.Trim();
            var pwd = _passwordTxt.Text;
            var confirm = _confirmTxt.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                _errorLbl.Text = "Full name is required.";
                _fullNameTxt.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(email) || !EmailRegex.IsMatch(email))
            {
                _errorLbl.Text = "Please enter a valid email address.";
                _emailTxt.Focus();
                return;
            }

            if (!_isEdit && string.IsNullOrWhiteSpace(pwd))
            {
                _errorLbl.Text = "Password is required for new accounts.";
                _passwordTxt.Focus();
                return;
            }

            if (!string.IsNullOrEmpty(pwd) && pwd != confirm)
            {
                _errorLbl.Text = "Passwords do not match.";
                _confirmTxt.Focus();
                return;
            }

            var roleItem = _roleCombo.SelectedItem as RoleItem;
            int roleId = roleItem?.Id ?? 4;
            var status = _statusCombo.SelectedItem?.ToString() ?? "Active";

            SetBusy(true);

            try
            {
                if (_isEdit)
                {
                    var payload = new
                    {
                        fullName = name,
                        email = email,
                        roleId = roleId,
                        status = status,
                        password = string.IsNullOrEmpty(pwd) ? null : pwd
                    };

                    using var resp = await _http.PutAsJsonAsync($"api/users/{_existing!.UserId}", payload);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var err = await resp.Content.ReadAsStringAsync();
                        _errorLbl.Text = $"Save failed ({resp.StatusCode}): {ExtractMessage(err)}";
                        return;
                    }
                }
                else
                {
                    var payload = new
                    {
                        fullName = name,
                        email = email,
                        roleId = roleId,
                        status = status,
                        password = pwd
                    };

                    using var resp = await _http.PostAsJsonAsync("api/users", payload);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var err = await resp.Content.ReadAsStringAsync();
                        _errorLbl.Text = $"Creation failed ({resp.StatusCode}): {ExtractMessage(err)}";
                        return;
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Network error: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task DeleteAsync()
        {
            if (_existing == null) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete admin account '{_existing.FullName}' ({_existing.Email})?",
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                using var resp = await _http.DeleteAsync($"api/users/{_existing.UserId}");
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _errorLbl.Text = $"Delete failed ({resp.StatusCode}): {ExtractMessage(err)}";
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _saveBtn.Enabled = !busy;
            _cancelBtn.Enabled = !busy;
            if (_deleteBtn != null) _deleteBtn.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private static string ExtractMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "Unknown error.";
            try
            {
                var match = Regex.Match(json, @"""message""\s*:\s*""([^""]+)""");
                if (match.Success) return match.Groups[1].Value;
            }
            catch { }
            return json.Length > 120 ? json.Substring(0, 120) + "..." : json;
        }

        private sealed class RoleItem
        {
            public int Id { get; }
            public string Name { get; }

            public RoleItem(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString() => $"{Name} (id: {Id})";
        }
    }
}
