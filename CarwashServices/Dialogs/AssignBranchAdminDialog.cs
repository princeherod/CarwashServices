using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Auth;
using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class AssignBranchAdminDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color CardBg = Color.White;
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8);
        private static readonly Color BlueSoft = Color.FromArgb(0xDB, 0xEA, 0xFE);
        private static readonly Color SlateBorder = Color.FromArgb(0xD1, 0xD9, 0xE6);

        private readonly BranchDto _branch;
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<EligibleBranchUserDto> _eligibleUsers = new();
        private List<BranchDto> _tenantBranches = new();

        // Mode toggles
        private RadioButton _radioSelectExisting = null!;
        private RadioButton _radioCreateNew = null!;
        private Panel _existingPanel = null!;
        private Panel _createPanel = null!;

        // Mode 1 controls
        private ComboBox _userCombo = null!;
        private Label _userStatusHint = null!;

        // Mode 2 controls
        private TextBox _firstNameTxt = null!;
        private TextBox _lastNameTxt = null!;
        private TextBox _emailTxt = null!;
        private TextBox _passwordTxt = null!;
        private TextBox _confirmPasswordTxt = null!;
        private ComboBox _roleCombo = null!;
        private ComboBox _branchCombo = null!;

        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;
        private bool _isBusy = false;

        private class UserComboItem
        {
            public int? UserId { get; set; }
            public string DisplayText { get; set; } = string.Empty;
            public override string ToString() => DisplayText;
        }

        private class RoleComboItem
        {
            public int RoleId { get; set; }
            public string Text { get; set; } = string.Empty;
            public override string ToString() => Text;
        }

        private class BranchComboItem
        {
            public int BranchId { get; set; }
            public string BranchName { get; set; } = string.Empty;
            public override string ToString() => BranchName;
        }

        public AssignBranchAdminDialog(BranchDto branch)
        {
            _branch = branch;
            InitializeComponent();
            Load += async (s, e) =>
            {
                await LoadEligibleUsersAsync();
                await LoadTenantBranchesAsync();
            };
        }

        private void InitializeComponent()
        {
            Text = $"Assign Branch Admin - {_branch.BranchName}";
            Size = new Size(540, 680);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            // Header
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Navy
            };
            var titleLbl = new Label
            {
                Text = "ASSIGN BRANCH ADMIN",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12.5f),
                Location = new Point(24, 14),
                AutoSize = true
            };
            var subLbl = new Label
            {
                Text = $"{_branch.BranchName} ({_branch.BranchCode})  •  {SessionUser.CompanyName}",
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(24, 39),
                AutoSize = true
            };
            header.Controls.AddRange(new Control[] { titleLbl, subLbl });
            Controls.Add(header);

            // Body
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 14, 24, 14),
                AutoScroll = true
            };

            int y = 12;

            // Branch Summary Banner
            var infoCard = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(476, 68),
                BackColor = CardBg
            };
            infoCard.Paint += (s, e) =>
            {
                using var p = new Pen(CardBorder);
                e.Graphics.DrawRectangle(p, 0, 0, infoCard.Width - 1, infoCard.Height - 1);
                using var accent = new SolidBrush(Blue);
                e.Graphics.FillRectangle(accent, 0, 0, 4, infoCard.Height);
            };

            var branchNameLbl = new Label
            {
                Text = _branch.BranchName,
                Font = new Font("Segoe UI Semibold", 10.5f),
                ForeColor = Navy,
                Location = new Point(14, 10),
                AutoSize = true
            };
            var branchLocLbl = new Label
            {
                Text = $"Location: {_branch.DisplayLocation}  |  Current Admin: {(!string.IsNullOrWhiteSpace(_branch.AssignedAdminName) ? _branch.AssignedAdminName : "Unassigned")}",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(14, 35),
                AutoSize = true
            };
            infoCard.Controls.AddRange(new Control[] { branchNameLbl, branchLocLbl });
            body.Controls.Add(infoCard);
            y += 78;

            // Mode Selector
            var modePanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(476, 34),
                BackColor = Color.Transparent
            };
            _radioSelectExisting = new RadioButton
            {
                Text = "Select Existing Company User",
                Checked = true,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Navy,
                Location = new Point(0, 4),
                Size = new Size(230, 24)
            };
            _radioCreateNew = new RadioButton
            {
                Text = "+ Create New Branch Admin",
                Checked = false,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Navy,
                Location = new Point(240, 4),
                Size = new Size(230, 24)
            };
            _radioSelectExisting.CheckedChanged += (s, e) => ToggleMode();
            _radioCreateNew.CheckedChanged += (s, e) => ToggleMode();

            modePanel.Controls.AddRange(new Control[] { _radioSelectExisting, _radioCreateNew });
            body.Controls.Add(modePanel);
            y += 40;

            // --- Panel 1: Select Existing User ---
            _existingPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(476, 320),
                BackColor = CardBg,
                Visible = true
            };
            _existingPanel.Paint += (s, e) =>
            {
                using var p = new Pen(CardBorder);
                e.Graphics.DrawRectangle(p, 0, 0, _existingPanel.Width - 1, _existingPanel.Height - 1);
            };

            var selectLbl = new Label
            {
                Text = "ASSIGNED USER / ADMIN *",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Muted,
                Location = new Point(16, 16),
                AutoSize = true
            };
            _userCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(16, 38),
                Size = new Size(444, 28)
            };
            _userCombo.SelectedIndexChanged += UserCombo_SelectedIndexChanged;

            _userStatusHint = new Label
            {
                Text = "Loading eligible users...",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(16, 76),
                Size = new Size(444, 40)
            };

            var scopeNote = new Label
            {
                Text = $"• Only users belonging to {SessionUser.CompanyName} are shown.\n• Cross-tenant assignments are strictly prohibited.\n• When assigned, the user will be restricted to {_branch.BranchName} upon logging in.",
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Muted,
                Location = new Point(16, 130),
                Size = new Size(444, 60)
            };

            _existingPanel.Controls.AddRange(new Control[] { selectLbl, _userCombo, _userStatusHint, scopeNote });
            body.Controls.Add(_existingPanel);

            // --- Panel 2: Create New Branch Admin ---
            _createPanel = new Panel
            {
                Location = new Point(24, y),
                Size = new Size(476, 320),
                BackColor = CardBg,
                Visible = false
            };
            _createPanel.Paint += (s, e) =>
            {
                using var p = new Pen(CardBorder);
                e.Graphics.DrawRectangle(p, 0, 0, _createPanel.Width - 1, _createPanel.Height - 1);
            };

            int cy = 12;
            int colW = 214;

            // Row 1: First Name & Last Name
            _createPanel.Controls.Add(CreateCaption("FIRST NAME *", 16, cy));
            _createPanel.Controls.Add(CreateCaption("LAST NAME *", 16 + colW + 16, cy));
            cy += 20;

            _firstNameTxt = CreateInput(16, cy, colW);
            _lastNameTxt = CreateInput(16 + colW + 16, cy, colW);
            _createPanel.Controls.AddRange(new Control[] { _firstNameTxt, _lastNameTxt });
            cy += 38;

            // Row 2: Email & Role
            _createPanel.Controls.Add(CreateCaption("EMAIL ADDRESS *", 16, cy));
            _createPanel.Controls.Add(CreateCaption("ROLE *", 16 + colW + 16, cy));
            cy += 20;

            _emailTxt = CreateInput(16, cy, colW);
            _roleCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(16 + colW + 16, cy),
                Size = new Size(colW, 28)
            };
            // Expected Role IDs: Admin = 2, Manager = 3, Service Staff = 4
            _roleCombo.Items.Add(new RoleComboItem { RoleId = 2, Text = "Admin" });
            _roleCombo.Items.Add(new RoleComboItem { RoleId = 3, Text = "Manager" });
            _roleCombo.Items.Add(new RoleComboItem { RoleId = 4, Text = "Service Staff" });
            _roleCombo.SelectedIndex = 0; // Admin (2) by default
            _createPanel.Controls.AddRange(new Control[] { _emailTxt, _roleCombo });
            cy += 38;

            // Row 3: Password & Confirm Password
            _createPanel.Controls.Add(CreateCaption("PASSWORD *", 16, cy));
            _createPanel.Controls.Add(CreateCaption("CONFIRM PASSWORD *", 16 + colW + 16, cy));
            cy += 20;

            _passwordTxt = CreateInput(16, cy, colW, true);
            _confirmPasswordTxt = CreateInput(16 + colW + 16, cy, colW, true);
            _createPanel.Controls.AddRange(new Control[] { _passwordTxt, _confirmPasswordTxt });
            cy += 38;

            // Row 4: Assigned Branch
            _createPanel.Controls.Add(CreateCaption("BRANCH *", 16, cy));
            cy += 20;

            _branchCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(16, cy),
                Size = new Size(colW, 28)
            };
            _createPanel.Controls.Add(_branchCombo);

            var createHint = new Label
            {
                Text = $"Account will be created under {SessionUser.CompanyName} and assigned to the selected branch.",
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Muted,
                Location = new Point(16 + colW + 16, cy + 4),
                Size = new Size(colW, 36)
            };
            _createPanel.Controls.Add(createHint);

            body.Controls.Add(_createPanel);
            Controls.Add(body);

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(0xEA, 0xF0, 0xF8)
            };

            _saveBtn = new Button
            {
                Text = "Assign Admin",
                Size = new Size(130, 36),
                Location = new Point(Width - 264, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.Click += async (s, e) => await SaveAssignmentAsync();

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Size = new Size(90, 36),
                Location = new Point(Width - 124, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Cursor = Cursors.Hand
            };
            _cancelBtn.FlatAppearance.BorderColor = CardBorder;
            _cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.AddRange(new Control[] { _saveBtn, _cancelBtn });
            Controls.Add(footer);
        }

        private void ToggleMode()
        {
            bool isExisting = _radioSelectExisting.Checked;
            _existingPanel.Visible = isExisting;
            _createPanel.Visible = !isExisting;
            _saveBtn.Text = isExisting ? "Assign Admin" : "Create Admin";
        }

        private static Label CreateCaption(string text, int x, int y) => new()
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8f),
            ForeColor = Muted,
            Location = new Point(x, y),
            AutoSize = true
        };

        private static TextBox CreateInput(int x, int y, int width, bool isPassword = false) => new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 26),
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Navy,
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = isPassword
        };

        private async Task LoadTenantBranchesAsync()
        {
            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var branches = await _http.GetFromJsonAsync<List<BranchDto>>($"api/tenant/{companyId}/branches") ?? new();
                _tenantBranches = branches;

                _branchCombo.Items.Clear();
                int selectIdx = 0;
                for (int i = 0; i < branches.Count; i++)
                {
                    var b = branches[i];
                    int idx = _branchCombo.Items.Add(new BranchComboItem
                    {
                        BranchId = b.BranchId,
                        BranchName = b.BranchName
                    });
                    if (b.BranchId == _branch.BranchId)
                    {
                        selectIdx = idx;
                    }
                }
                if (_branchCombo.Items.Count > 0)
                {
                    _branchCombo.SelectedIndex = selectIdx;
                }
            }
            catch { }
        }

        private async Task LoadEligibleUsersAsync()
        {
            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var users = await _http.GetFromJsonAsync<List<EligibleBranchUserDto>>(
                    $"api/tenant/{companyId}/branches/{_branch.BranchId}/eligible-users") ?? new();
                _eligibleUsers = users;

                _userCombo.Items.Clear();
                _userCombo.Items.Add(new UserComboItem { UserId = null, DisplayText = "(Unassigned — Remove Branch Admin)" });

                int selectedIdx = 0;
                for (int i = 0; i < _eligibleUsers.Count; i++)
                {
                    var u = _eligibleUsers[i];
                    string branchNote = !string.IsNullOrWhiteSpace(u.CurrentBranchName) ? $" [{u.CurrentBranchName}]" : " [All Branches / Unassigned]";
                    string label = $"{u.FullName} ({u.Email}) — {u.RoleName}{branchNote}";

                    int idx = _userCombo.Items.Add(new UserComboItem { UserId = u.UserId, DisplayText = label });
                    if (u.IsAssignedToThisBranch || u.UserId == _branch.AssignedAdminId)
                    {
                        selectedIdx = idx;
                    }
                }

                _userCombo.SelectedIndex = selectedIdx;
                UpdateUserStatusHint();
            }
            catch (Exception ex)
            {
                _userStatusHint.Text = $"Failed to load eligible users: {ex.Message}";
                _userStatusHint.ForeColor = Color.Crimson;
            }
        }

        private void UserCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateUserStatusHint();
        }

        private void UpdateUserStatusHint()
        {
            if (_userCombo.SelectedItem is not UserComboItem item) return;
            if (item.UserId == null)
            {
                _userStatusHint.Text = "This branch will have no designated admin assigned.";
                _userStatusHint.ForeColor = Muted;
                return;
            }

            var user = _eligibleUsers.FirstOrDefault(u => u.UserId == item.UserId);
            if (user == null) return;

            if (user.IsAssignedToThisBranch)
            {
                _userStatusHint.Text = $"Currently assigned to this branch ({_branch.BranchName}).";
                _userStatusHint.ForeColor = Blue;
            }
            else if (user.CurrentBranchId.HasValue && user.CurrentBranchId > 0)
            {
                _userStatusHint.Text = $"Notice: User is currently assigned to '{user.CurrentBranchName}'. Assigning will reassign them to '{_branch.BranchName}'.";
                _userStatusHint.ForeColor = Color.FromArgb(0xEA, 0x58, 0x0C); // Amber/Orange
            }
            else
            {
                _userStatusHint.Text = $"Eligible active user from {SessionUser.CompanyName}.";
                _userStatusHint.ForeColor = Color.FromArgb(0x16, 0x65, 0x34); // Green
            }
        }

        private async Task SaveAssignmentAsync()
        {
            if (_isBusy) return;

            int companyId = SessionUser.CurrentCompanyId;

            if (_radioSelectExisting.Checked)
            {
                if (_userCombo.SelectedItem is not UserComboItem item)
                {
                    MessageBox.Show("Please select a user to assign or choose '(Unassigned)'.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _isBusy = true;
                _saveBtn.Enabled = false;
                _saveBtn.Text = "Assigning...";

                try
                {
                    var req = new AssignBranchAdminRequestDto { UserId = item.UserId };
                    var resp = await _http.PostAsJsonAsync($"api/tenant/{companyId}/branches/{_branch.BranchId}/assign-admin", req);

                    if (resp.IsSuccessStatusCode)
                    {
                        var updated = await resp.Content.ReadFromJsonAsync<BranchDto>();
                        if (updated != null)
                        {
                            _branch.AssignedAdminId = updated.AssignedAdminId;
                            _branch.AssignedAdminName = updated.AssignedAdminName;
                            _branch.AssignedAdminEmail = updated.AssignedAdminEmail;
                        }

                        MessageBox.Show(
                            item.UserId.HasValue
                                ? $"Admin successfully assigned to '{_branch.BranchName}'."
                                : $"Admin unassigned from '{_branch.BranchName}'.",
                            "Assignment Updated",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }

                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to assign admin: {err}", "Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _isBusy = false;
                    _saveBtn.Enabled = true;
                    _saveBtn.Text = "Assign Admin";
                }
            }
            else
            {
                // Create New Branch Admin
                string fn = _firstNameTxt.Text.Trim();
                string ln = _lastNameTxt.Text.Trim();
                string email = _emailTxt.Text.Trim();
                string pass = _passwordTxt.Text;
                string confirmPass = _confirmPasswordTxt.Text;
                int roleId = (_roleCombo.SelectedItem is RoleComboItem rItem) ? rItem.RoleId : 2;
                int targetBranchId = (_branchCombo.SelectedItem is BranchComboItem bItem) ? bItem.BranchId : _branch.BranchId;
                string targetBranchName = (_branchCombo.SelectedItem is BranchComboItem bItem2) ? bItem2.BranchName : _branch.BranchName;

                if (string.IsNullOrWhiteSpace(fn)) { MessageBox.Show("First name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (string.IsNullOrWhiteSpace(ln)) { MessageBox.Show("Last name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    MessageBox.Show("A valid email address is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(pass) || pass.Length < 4)
                {
                    MessageBox.Show("Password must be at least 4 characters long.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (pass != confirmPass)
                {
                    MessageBox.Show("Password and Confirm Password do not match. Please re-enter passwords.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _confirmPasswordTxt.Focus();
                    return;
                }

                _isBusy = true;
                _saveBtn.Enabled = false;
                _saveBtn.Text = "Creating...";

                try
                {
                    var req = new CreateBranchAdminRequestDto
                    {
                        FirstName = fn,
                        LastName = ln,
                        Email = email,
                        Password = pass,
                        RoleId = roleId, // Admin = 2
                        CompanyId = companyId,
                        BranchId = targetBranchId
                    };
                    var resp = await _http.PostAsJsonAsync($"api/tenant/{companyId}/branches/{targetBranchId}/create-admin", req);

                    if (resp.IsSuccessStatusCode)
                    {
                        var updated = await resp.Content.ReadFromJsonAsync<BranchDto>();
                        if (updated != null && targetBranchId == _branch.BranchId)
                        {
                            _branch.AssignedAdminId = updated.AssignedAdminId;
                            _branch.AssignedAdminName = updated.AssignedAdminName;
                            _branch.AssignedAdminEmail = updated.AssignedAdminEmail;
                        }

                        string roleTitle = roleId switch
                        {
                            2 => "Admin",
                            3 => "Manager",
                            4 => "Service Staff",
                            _ => "Admin"
                        };

                        MessageBox.Show(
                            $"New branch account '{fn} {ln}' ({email}) created with role '{roleTitle}' and assigned to '{targetBranchName}' successfully!",
                            "Account Created & Assigned",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        DialogResult = DialogResult.OK;
                        Close();
                        return;
                    }

                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to create branch admin: {err}", "Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _isBusy = false;
                    _saveBtn.Enabled = true;
                    _saveBtn.Text = "Create Admin";
                }
            }
        }
    }
}
