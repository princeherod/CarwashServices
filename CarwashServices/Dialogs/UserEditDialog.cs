using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class UserEditDialog : Form
    {
        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color Line = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);

        // ---- Constants ----
        private const int PadX = 30;
        private const int ContentW = 620;
        private const int Gap = 16;
        private const int W2 = (ContentW - Gap) / 2;
        private const int X2b = PadX + W2 + Gap;

        private static readonly (int Id, string Name)[] SuperAdminRoles =
        {
            (1, "Super Admin"),
            (2, "Admin"),
            (3, "Manager"),
            (4, "Service Staff")
        };

        private static readonly (int Id, string Name)[] StandardRoles =
        {
            (2, "Admin"),
            (3, "Manager"),
            (4, "Service Staff")
        };

        // ---- State ----
        private readonly int? _userId;
        private readonly bool _isEdit;
        private readonly int? _defaultBranchId;
        private readonly string? _defaultBranchName;
        private UserDetailDto? _loaded;

        // ---- Controls ----
        private TextBox _firstNameTxt = null!;
        private TextBox _lastNameTxt = null!;
        private TextBox _emailTxt = null!;
        private ComboBox _roleCombo = null!;
        private ComboBox? _statusCombo;
        private ComboBox _branchCombo = null!;
        private TextBox _passwordTxt = null!;
        private TextBox _confirmTxt = null!;
        private Label _errorLbl = null!;
        private Button _deleteBtn = null!;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public UserEditDialog(int? userId, int? defaultBranchId = null, string? defaultBranchName = null)
        {
            _userId = userId;
            _isEdit = userId.HasValue;
            _defaultBranchId = defaultBranchId;
            _defaultBranchName = defaultBranchName;

            if (CarwashServices.Auth.SessionUser.IsLoggedIn)
            {
                _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Current-User-Id", CarwashServices.Auth.SessionUser.UserId.ToString());
                _http.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Id", CarwashServices.Auth.SessionUser.UserId.ToString());
            }
            InitializeForm();

            Load += async (s, e) =>
            {
                if (_isEdit)
                {
                    await LoadUserAsync(userId!.Value);
                    await LoadBranchesAsync(_loaded?.BranchId);
                }
                else
                {
                    await LoadBranchesAsync(_defaultBranchId);
                }
            };
        }

        // =================================================================
        //  UI
        // =================================================================
        private void InitializeForm()
        {
            Text = _isEdit ? "Edit System User" : "Create System User";
            ClientSize = new Size(720, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.White };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(Line);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            header.Controls.Add(new Label
            {
                Text = _isEdit ? "Edit System User" : "Create System User",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(PadX, 20),
                AutoSize = true
            });
            Controls.Add(header);

            // ---- Footer ----
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Color.White };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(Line);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                AutoSize = false,
                Location = new Point(PadX, 10),
                Size = new Size(ContentW, 20),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            footer.Controls.Add(_errorLbl);

            _deleteBtn = new Button
            {
                Text = "Delete User",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Danger,
                BackColor = Color.White,
                Size = new Size(130, 42),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Visible = _isEdit
            };
            _deleteBtn.FlatAppearance.BorderColor = Line;
            _deleteBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xFD, 0xE7, 0xE6);
            _deleteBtn.Click += async (s, e) => await DeleteAsync();
            footer.Controls.Add(_deleteBtn);

            var cancelBtn = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted,
                BackColor = Color.White,
                Size = new Size(100, 42),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = false
            };
            cancelBtn.FlatAppearance.BorderColor = Line;
            cancelBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            footer.Controls.Add(cancelBtn);

            var saveBtn = new Button
            {
                Text = _isEdit ? "Save Changes" : "Create User",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                Size = new Size(160, 42),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(saveBtn);

            void PlaceButtons()
            {
                saveBtn.Location = new Point(footer.ClientSize.Width - PadX - saveBtn.Width, 17);
                cancelBtn.Location = new Point(saveBtn.Left - 12 - cancelBtn.Width, 17);
                _deleteBtn.Location = new Point(PadX, 17);
            }
            footer.Resize += (s, e) => PlaceButtons();
            PlaceButtons();

            CancelButton = cancelBtn;
            Controls.Add(footer);

            // ---- Body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true,
                Padding = new Padding(0)
            };
            Controls.Add(body);
            body.BringToFront();

            int y = 18;

            // ============ USER DETAILS ============
            body.Controls.Add(SectionDivider("User Details", y));
            y += 40;

            body.Controls.Add(MakeLabel("First Name *", PadX, y));
            _firstNameTxt = MakeTextBox(PadX, y + 22, W2);
            _firstNameTxt.PlaceholderText = "First Name";
            _firstNameTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_firstNameTxt);

            body.Controls.Add(MakeLabel("Last Name *", X2b, y));
            _lastNameTxt = MakeTextBox(X2b, y + 22, W2);
            _lastNameTxt.PlaceholderText = "Last Name";
            _lastNameTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_lastNameTxt);
            y += 76;

            body.Controls.Add(MakeLabel("Email Address *", PadX, y));
            _emailTxt = MakeTextBox(PadX, y + 22, ContentW);
            _emailTxt.PlaceholderText = "user@carwash.com";
            _emailTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_emailTxt);
            y += 76;

            body.Controls.Add(MakeLabel("Role *", PadX, y));
            _roleCombo = new ComboBox
            {
                Location = new Point(PadX, y + 22),
                Width = _isEdit ? W2 : ContentW,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            var roles = (CarwashServices.Auth.SessionUser.RoleId == 1 || CarwashServices.Auth.SessionUser.Role == CarwashServices.Auth.UserRole.SuperAdmin)
                ? SuperAdminRoles
                : StandardRoles;
            foreach (var r in roles)
                _roleCombo.Items.Add(new ComboItem(r.Id, r.Name));
            _roleCombo.SelectedIndex = 0;
            body.Controls.Add(_roleCombo);

            if (_isEdit)
            {
                body.Controls.Add(MakeLabel("Status", X2b, y));
                _statusCombo = new ComboBox
                {
                    Location = new Point(X2b, y + 22),
                    Width = W2,
                    Font = new Font("Segoe UI", 10f),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.White
                };
                _statusCombo.Items.AddRange(new object[] { "Active", "Inactive" });
                _statusCombo.SelectedIndex = 0;
                body.Controls.Add(_statusCombo);
            }
            y += 76;

            // ============ BRANCH ASSIGNMENT ============
            body.Controls.Add(MakeLabel("Assigned Branch *", PadX, y));
            _branchCombo = new ComboBox
            {
                Location = new Point(PadX, y + 22),
                Width = ContentW,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            body.Controls.Add(_branchCombo);
            y += 88;

            // ============ PASSWORD & SECURITY ============
            body.Controls.Add(SectionDivider("Password & Security", y));
            y += 40;

            body.Controls.Add(MakeLabel("Password", PadX, y));
            _passwordTxt = MakeTextBox(PadX, y + 22, W2);
            _passwordTxt.PlaceholderText = _isEdit ? "Leave blank to keep current" : "••••••••";
            _passwordTxt.UseSystemPasswordChar = true;
            _passwordTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_passwordTxt);

            body.Controls.Add(MakeLabel("Confirm Password", X2b, y));
            _confirmTxt = MakeTextBox(X2b, y + 22, W2);
            _confirmTxt.PlaceholderText = "••••••••";
            _confirmTxt.UseSystemPasswordChar = true;
            _confirmTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_confirmTxt);
            y += 76;
        }

        private static Label MakeLabel(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private static TextBox MakeTextBox(int x, int y, int width) => new TextBox
        {
            Location = new Point(x, y),
            Width = width,
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        private Control SectionDivider(string text, int y)
        {
            var font = new Font("Segoe UI Semibold", 8.5f);
            int tw = TextRenderer.MeasureText(text, font).Width;
            int lx = (ContentW - tw) / 2;

            var p = new Panel
            {
                Location = new Point(PadX, y),
                Size = new Size(ContentW, 20),
                BackColor = Color.White
            };
            p.Paint += (s, e) =>
            {
                using var pen = new Pen(Line);
                int cy = p.Height / 2;
                e.Graphics.DrawLine(pen, 0, cy, lx - 8, cy);
                e.Graphics.DrawLine(pen, lx + tw + 8, cy, ContentW, cy);
            };

            var lbl = new Label
            {
                Text = text,
                ForeColor = Faint,
                Font = font,
                Location = new Point(lx, 0),
                AutoSize = true
            };
            p.Controls.Add(lbl);
            return p;
        }

        private async Task LoadBranchesAsync(int? selectedBranchId = null)
        {
            try
            {
                int cid = _loaded?.CompanyId ?? CarwashServices.Auth.SessionUser.CurrentCompanyId;
                var branches = await _http.GetFromJsonAsync<List<Dtos.BranchDto>>($"api/tenant/{cid}/branches") ?? new();

                _branchCombo.Items.Clear();
                int selectIdx = 0;
                for (int i = 0; i < branches.Count; i++)
                {
                    var b = branches[i];
                    string label = b.IsMainBranch ? $"{b.BranchName} (Main)" : b.BranchName;
                    int idx = _branchCombo.Items.Add(new ComboItem(b.BranchId, label));
                    if (selectedBranchId.HasValue && selectedBranchId.Value == b.BranchId)
                    {
                        selectIdx = idx;
                    }
                    else if (!selectedBranchId.HasValue && _defaultBranchId.HasValue && _defaultBranchId.Value == b.BranchId)
                    {
                        selectIdx = idx;
                    }
                }

                if (_branchCombo.Items.Count > 0)
                {
                    _branchCombo.SelectedIndex = selectIdx;
                }

                if (CarwashServices.Auth.SessionUser.IsSingleBranchUser)
                {
                    _branchCombo.Enabled = false;
                }
            }
            catch
            {
                if (_defaultBranchId.HasValue)
                {
                    _branchCombo.Items.Clear();
                    _branchCombo.Items.Add(new ComboItem(_defaultBranchId.Value, _defaultBranchName ?? "Assigned Branch"));
                    _branchCombo.SelectedIndex = 0;
                }
            }
        }

        // =================================================================
        //  DATA ACCESS
        // =================================================================
        private async Task LoadUserAsync(int userId)
        {
            try
            {
                var u = await _http.GetFromJsonAsync<UserDetailDto>($"api/users/{userId}");
                if (u is null)
                {
                    MessageBox.Show("User could not be found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }
                _loaded = u;

                _firstNameTxt.Text = u.FirstName;
                _lastNameTxt.Text = u.LastName;
                _emailTxt.Text = u.Email;

                bool roleMatched = false;
                for (int i = 0; i < _roleCombo.Items.Count; i++)
                {
                    if (_roleCombo.Items[i] is ComboItem ci && ci.Id == u.RoleId)
                    {
                        _roleCombo.SelectedIndex = i;
                        roleMatched = true;
                        break;
                    }
                }

                if (!roleMatched)
                {
                    string rName = u.RoleId switch
                    {
                        1 => "Super Admin",
                        2 => "Admin",
                        3 => "Manager",
                        4 => "Service Staff",
                        _ => $"Role {u.RoleId}"
                    };
                    int addedIdx = _roleCombo.Items.Add(new ComboItem(u.RoleId, rName));
                    _roleCombo.SelectedIndex = addedIdx;
                }
                if (_statusCombo != null)
                    _statusCombo.SelectedItem = string.IsNullOrWhiteSpace(u.Status) ? "Active" : u.Status;

                ClearError();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load user.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =================================================================
        //  VALIDATION + SAVE
        // =================================================================
        private bool ValidateForm(out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(_firstNameTxt.Text))
            { error = "First name is required."; _firstNameTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_lastNameTxt.Text))
            { error = "Last name is required."; _lastNameTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_emailTxt.Text))
            { error = "Email is required."; _emailTxt.Focus(); return false; }

            if (!EmailRegex.IsMatch(_emailTxt.Text.Trim()))
            { error = "Email isn't in a valid format. Example: name@example.com"; _emailTxt.Focus(); return false; }

            if (_roleCombo.SelectedItem is not ComboItem role || role.Id == null)
            { error = "Please pick a role."; _roleCombo.Focus(); return false; }

            var pwd = _passwordTxt.Text;
            var confirm = _confirmTxt.Text;

            if (!_isEdit && string.IsNullOrWhiteSpace(pwd))
            { error = "Password is required."; _passwordTxt.Focus(); return false; }

            if (!string.IsNullOrEmpty(pwd))
            {
                if (pwd.Length < 6)
                { error = "Password must be at least 6 characters."; _passwordTxt.Focus(); return false; }

                if (pwd != confirm)
                { error = "Passwords do not match."; _confirmTxt.Focus(); return false; }
            }

            return true;
        }

        private async Task SaveAsync()
        {
            if (_isEdit && _userId.HasValue && CarwashServices.Auth.SessionUser.IsLoggedIn && _userId.Value == CarwashServices.Auth.SessionUser.UserId)
            {
                _errorLbl.Text = "You cannot edit your own user account.";
                _errorLbl.Visible = true;
                return;
            }

            if (!ValidateForm(out var error))
            {
                _errorLbl.Text = error;
                _errorLbl.Visible = true;
                return;
            }
            ClearError();

            var role = (ComboItem)_roleCombo.SelectedItem!;
            var status = _statusCombo?.SelectedItem?.ToString() ?? "Active";
            var password = _passwordTxt.Text;

            var first = _firstNameTxt.Text.Trim();
            var last = _lastNameTxt.Text.Trim();
            var full = $"{first} {last}".Trim();

            int? selectedBranchId = null;
            if (_branchCombo.SelectedItem is ComboItem bItem && bItem.Id > 0)
            {
                selectedBranchId = bItem.Id;
            }
            else if (_defaultBranchId.HasValue)
            {
                selectedBranchId = _defaultBranchId.Value;
            }

            try
            {
                HttpResponseMessage resp;

                int? targetCompanyId = _isEdit
                    ? (_loaded?.CompanyId ?? (CarwashServices.Auth.SessionUser.CompanyId.HasValue ? CarwashServices.Auth.SessionUser.CurrentCompanyId : (int?)null))
                    : (CarwashServices.Auth.SessionUser.CompanyId.HasValue ? CarwashServices.Auth.SessionUser.CompanyId.Value : (int?)null);

                int currentUserId = CarwashServices.Auth.SessionUser.UserId;

                if (_isEdit)
                {
                    object body = string.IsNullOrEmpty(password)
                        ? new
                        {
                            firstName = first,
                            lastName = last,
                            fullName = full,
                            email = _emailTxt.Text.Trim(),
                            roleId = role.Id,
                            status,
                            companyId = targetCompanyId,
                            branchId = selectedBranchId,
                            currentUserId = currentUserId
                        }
                        : new
                        {
                            firstName = first,
                            lastName = last,
                            fullName = full,
                            email = _emailTxt.Text.Trim(),
                            roleId = role.Id,
                            status,
                            password,
                            companyId = targetCompanyId,
                            branchId = selectedBranchId,
                            currentUserId = currentUserId
                        };

                    resp = await _http.PutAsJsonAsync($"api/users/{_userId!.Value}", body);
                }
                else
                {
                    var body = new
                    {
                        firstName = first,
                        lastName = last,
                        fullName = full,
                        email = _emailTxt.Text.Trim(),
                        roleId = role.Id, // Saved as RoleId = 2 for Admin
                        status,
                        password,
                        companyId = targetCompanyId,
                        branchId = selectedBranchId
                    };
                    resp = await _http.PostAsJsonAsync("api/users", body);
                }

                if (resp.IsSuccessStatusCode)
                {
                    if (_isEdit)
                    {
                        MessageBox.Show($"User '{full}' has been updated successfully.", "User Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"User '{full}' ({_emailTxt.Text.Trim()}) has been created successfully.", "User Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var text = await resp.Content.ReadAsStringAsync();
                    string errorMsg = $"Save failed ({(int)resp.StatusCode}).";
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(text);
                        if (doc.RootElement.TryGetProperty("message", out var m))
                            errorMsg = m.GetString() ?? errorMsg;
                    }
                    catch { }

                    _errorLbl.Text = errorMsg;
                    _errorLbl.Visible = true;
                }
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Connection error: {ex.Message}";
                _errorLbl.Visible = true;
            }
        }

        private async Task DeleteAsync()
        {
            if (!_isEdit || !_userId.HasValue) return;

            var r = MessageBox.Show(
                $"Are you sure you want to delete '{_loaded?.FullName ?? "this user"}'?\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (r != DialogResult.Yes) return;

            try
            {
                var resp = await _http.DeleteAsync($"api/users/{_userId.Value}");
                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("User deleted successfully.", "User Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var text = await resp.Content.ReadAsStringAsync();
                    string msg = "Delete failed.";
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(text);
                        if (doc.RootElement.TryGetProperty("message", out var m))
                            msg = m.GetString() ?? msg;
                    }
                    catch { }
                    MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearError()
        {
            _errorLbl.Text = "";
            _errorLbl.Visible = false;
        }
    }
}