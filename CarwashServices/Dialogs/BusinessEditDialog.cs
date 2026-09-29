using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    public class BusinessEditDialog : Form
    {
        // Palette
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color DangerSoft = Color.FromArgb(0xFD, 0xE7, 0xE6);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly CompanyListItemDto _company;

        private TextBox _companyCodeTxt = null!;
        private TextBox _companyNameTxt = null!;
        private TextBox _phoneTxt = null!;
        private TextBox _emailTxt = null!;
        private TextBox _addressTxt = null!;
        private TextBox _cityTxt = null!;
        private TextBox _provinceTxt = null!;
        private TextBox _postalCodeTxt = null!;
        private TextBox _countryTxt = null!;
        private CheckBox _activeChk = null!;

        // Associated Admin controls
        private Label _currentAdminLbl = null!;
        private Button _unassignAdminBtn = null!;
        private ComboBox _assignAdminCombo = null!;
        private Button _newAdminBtn = null!;
        private bool _unassignRequested = false;
        private List<AvailableAdminDto> _availableAdmins = new();

        private Label _errorLbl = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;

        public BusinessEditDialog(CompanyListItemDto company)
        {
            _company = company ?? throw new ArgumentNullException(nameof(company));
            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);
            PopulateData();
            _ = LoadAvailableAdminsAsync();
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
            Text = $"Edit Business - {_company.CompanyName}";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 760);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 12)
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = "Edit Business Details",
                Font = new Font("Segoe UI Semibold", 13.5f),
                ForeColor = Navy,
                Location = new Point(32, 14),
                AutoSize = true
            };
            var subtitleLbl = new Label
            {
                Text = $"Update information and operating settings for {_company.CompanyName} ({_company.CompanyCode}).",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(32, 42),
                AutoSize = true
            };
            header.Controls.Add(titleLbl);
            header.Controls.Add(subtitleLbl);
            Controls.Add(header);

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = Color.FromArgb(0xFA, 0xFB, 0xFD)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                ForeColor = Danger,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
            footer.Controls.Add(_errorLbl);

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Size = new Size(95, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            _cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            _cancelBtn.FlatAppearance.BorderSize = 1;
            _cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(_cancelBtn);

            _saveBtn = new Button
            {
                Text = "Save Changes",
                Size = new Size(130, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Accent,
                Cursor = Cursors.Hand
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(_saveBtn);

            void RoundBtn(Button btn, int radius = 6)
            {
                using var p = RoundedRect(new Rectangle(0, 0, btn.Width, btn.Height), radius);
                btn.Region = new Region(p);
            }
            RoundBtn(_saveBtn);
            RoundBtn(_cancelBtn);

            void PlaceFooterControls()
            {
                const int padX = 32;
                const int topY = 16;
                _saveBtn.Location = new Point(footer.ClientSize.Width - padX - _saveBtn.Width, topY);
                _cancelBtn.Location = new Point(_saveBtn.Left - 12 - _cancelBtn.Width, topY);
                int errorWidth = Math.Max(10, _cancelBtn.Left - padX - 16);
                _errorLbl.SetBounds(padX, 8, errorWidth, 56);
            }
            footer.Resize += (s, e) => PlaceFooterControls();
            Controls.Add(footer);
            PlaceFooterControls();

            // ---- Body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 16)
            };

            int leftColX = 32;
            int colW = 290;
            int rightColX = leftColX + colW + 20;
            int fullW = colW * 2 + 20;
            int y = 14;

            // Row 1: Code (read-only) + Name (required)
            body.Controls.Add(Caption("COMPANY CODE", leftColX, y));
            body.Controls.Add(Caption("COMPANY NAME *", rightColX, y));
            y += 20;

            _companyCodeTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                ReadOnly = true,
                BackColor = Color.FromArgb(0xF1, 0xF5, 0xF9),
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            body.Controls.Add(_companyCodeTxt);

            _companyNameTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_companyNameTxt);
            y += 44;

            // Row 2: Phone + Email
            body.Controls.Add(Caption("CONTACT PHONE", leftColX, y));
            body.Controls.Add(Caption("CONTACT EMAIL", rightColX, y));
            y += 20;

            _phoneTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_phoneTxt);

            _emailTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_emailTxt);
            y += 44;

            // Row 3: Address Line
            body.Controls.Add(Caption("ADDRESS LINE", leftColX, y));
            y += 20;

            _addressTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_addressTxt);
            y += 44;

            // Row 4: City + Province / State
            body.Controls.Add(Caption("CITY", leftColX, y));
            body.Controls.Add(Caption("PROVINCE / STATE", rightColX, y));
            y += 20;

            _cityTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_cityTxt);

            _provinceTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_provinceTxt);
            y += 44;

            // Row 5: Postal Code + Country
            body.Controls.Add(Caption("POSTAL CODE", leftColX, y));
            body.Controls.Add(Caption("COUNTRY", rightColX, y));
            y += 20;

            _postalCodeTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_postalCodeTxt);

            _countryTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_countryTxt);
            y += 48;

            // ===== SECTION: ASSOCIATED ADMINISTRATOR =====
            var adminDiv = new Panel
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 1),
                BackColor = BorderSoft
            };
            body.Controls.Add(adminDiv);
            y += 16;

            var adminSecLbl = new Label
            {
                Text = "ASSOCIATED ADMINISTRATOR",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(adminSecLbl);
            y += 24;

            // Current admin info banner / card
            var currentAdminCard = new Panel
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 54),
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFD)
            };
            currentAdminCard.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, currentAdminCard.Width - 1, currentAdminCard.Height - 1);
            };

            _currentAdminLbl = new Label
            {
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                Location = new Point(14, 16),
                AutoSize = true,
                Text = "Loading current admin..."
            };
            currentAdminCard.Controls.Add(_currentAdminLbl);

            _unassignAdminBtn = new Button
            {
                Text = "Remove / Unassign Admin",
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = Danger,
                BackColor = DangerSoft,
                Size = new Size(180, 32),
                Location = new Point(currentAdminCard.Width - 194, 11),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _unassignAdminBtn.FlatAppearance.BorderColor = Color.FromArgb(0xF8, 0xC6, 0xC4);
            _unassignAdminBtn.Click += (s, e) =>
            {
                _unassignRequested = true;
                _currentAdminLbl.Text = "⚠ Admin will be UNASSIGNED from this business upon saving.";
                _currentAdminLbl.ForeColor = Danger;
                _unassignAdminBtn.Enabled = false;
                if (_assignAdminCombo.Items.Count > 1)
                {
                    _assignAdminCombo.SelectedIndex = 1; // "— Remove / Unassign Admin —"
                }
            };
            currentAdminCard.Controls.Add(_unassignAdminBtn);
            body.Controls.Add(currentAdminCard);
            y += 62;

            // Reassign dropdown + New Admin button
            body.Controls.Add(Caption("ASSIGN / REASSIGN ADMINISTRATOR", leftColX, y));
            y += 20;

            int comboW = fullW - 160;
            _assignAdminCombo = new ComboBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(comboW, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            _assignAdminCombo.Items.Add("(Keep current administrator)");
            _assignAdminCombo.Items.Add("— Remove / Unassign Admin —");
            _assignAdminCombo.SelectedIndex = 0;
            _assignAdminCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_assignAdminCombo.SelectedIndex == 1)
                {
                    _unassignRequested = true;
                    _currentAdminLbl.Text = "⚠ Admin will be UNASSIGNED upon saving.";
                    _currentAdminLbl.ForeColor = Danger;
                }
                else if (_assignAdminCombo.SelectedIndex > 1)
                {
                    _unassignRequested = false;
                    _currentAdminLbl.Text = $"Will assign: {_assignAdminCombo.SelectedItem}";
                    _currentAdminLbl.ForeColor = Accent;
                    _unassignAdminBtn.Enabled = true;
                }
            };
            body.Controls.Add(_assignAdminCombo);

            _newAdminBtn = new Button
            {
                Text = "+ New Admin",
                Location = new Point(leftColX + comboW + 12, y - 1),
                Size = new Size(148, 30),
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = Navy,
                BackColor = Color.FromArgb(0xF1, 0xF5, 0xF9),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _newAdminBtn.FlatAppearance.BorderColor = BorderSoft;
            _newAdminBtn.Click += async (s, e) =>
            {
                using var dlg = new AdminAccountEditDialog();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadAvailableAdminsAsync();
                }
            };
            body.Controls.Add(_newAdminBtn);
            y += 48;

            // Divider before Active checkbox
            var div2 = new Panel
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 1),
                BackColor = BorderSoft
            };
            body.Controls.Add(div2);
            y += 16;

            // Status Checkbox
            _activeChk = new CheckBox
            {
                Text = "Active (Business is permitted to log in and use platform features)",
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 26),
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy
            };
            body.Controls.Add(_activeChk);

            Controls.Add(body);
            body.BringToFront();
        }

        private void PopulateData()
        {
            _companyCodeTxt.Text = _company.CompanyCode;
            _companyNameTxt.Text = _company.CompanyName;
            _phoneTxt.Text = _company.ContactPhone ?? "";
            _emailTxt.Text = _company.ContactEmail ?? "";
            _addressTxt.Text = _company.AddressLine ?? "";
            _cityTxt.Text = _company.City ?? "";
            _provinceTxt.Text = _company.Province ?? _company.State ?? "";
            _postalCodeTxt.Text = _company.PostalCode ?? "";
            _countryTxt.Text = string.IsNullOrWhiteSpace(_company.Country) ? "Philippines" : _company.Country;
            _activeChk.Checked = _company.IsActive;

            UpdateCurrentAdminCard();
        }

        private void UpdateCurrentAdminCard()
        {
            bool hasAdmin = !string.IsNullOrWhiteSpace(_company.AdminUser) &&
                            !_company.AdminUser.Equals("Unassigned", StringComparison.OrdinalIgnoreCase);

            if (hasAdmin)
            {
                _currentAdminLbl.Text = string.IsNullOrWhiteSpace(_company.AdminEmail)
                    ? $"Current Admin: {_company.AdminUser}"
                    : $"Current Admin: {_company.AdminUser} ({_company.AdminEmail})";
                _currentAdminLbl.ForeColor = Navy;
                _unassignAdminBtn.Visible = true;
                _unassignAdminBtn.Enabled = true;
            }
            else
            {
                _currentAdminLbl.Text = "Current Admin: [Unassigned — No administrator account linked]";
                _currentAdminLbl.ForeColor = Muted;
                _unassignAdminBtn.Visible = false;
            }
        }

        private async Task LoadAvailableAdminsAsync()
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<AvailableAdminDto>>($"api/companies/available-admins?companyId={_company.CompanyId}");
                if (list != null)
                {
                    _availableAdmins = list;
                    _assignAdminCombo.Items.Clear();
                    _assignAdminCombo.Items.Add("(Keep current administrator)");
                    _assignAdminCombo.Items.Add("— Remove / Unassign Admin —");

                    foreach (var a in _availableAdmins)
                    {
                        string label = $"{a.FullName} ({a.Email})";
                        if (a.IsCurrentCompany)
                        {
                            label += " [Current Admin]";
                        }
                        else if (a.CompanyId.HasValue)
                        {
                            label += $" [Assigned to Company #{a.CompanyId}]";
                        }
                        else
                        {
                            label += " [Unassigned Admin]";
                        }
                        _assignAdminCombo.Items.Add(label);
                    }
                    _assignAdminCombo.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void SetBusy(bool busy)
        {
            _saveBtn.Enabled = !busy;
            _cancelBtn.Enabled = !busy;
            _saveBtn.Text = busy ? "Saving..." : "Save Changes";
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private async Task SaveAsync()
        {
            _errorLbl.Text = "";

            var name = _companyNameTxt.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                _errorLbl.Text = "Company Name cannot be empty.";
                _companyNameTxt.Focus();
                return;
            }

            SetBusy(true);

            try
            {
                int? assignAdminUserId = null;
                bool unassign = false;

                if (_unassignRequested || _assignAdminCombo.SelectedIndex == 1)
                {
                    unassign = true;
                }
                else if (_assignAdminCombo.SelectedIndex > 1)
                {
                    int adminIndex = _assignAdminCombo.SelectedIndex - 2;
                    if (adminIndex >= 0 && adminIndex < _availableAdmins.Count)
                    {
                        assignAdminUserId = _availableAdmins[adminIndex].UserId;
                    }
                }

                var payload = new UpdateCompanyRequestDto
                {
                    CompanyName = name,
                    ContactPhone = string.IsNullOrWhiteSpace(_phoneTxt.Text) ? null : _phoneTxt.Text.Trim(),
                    ContactEmail = string.IsNullOrWhiteSpace(_emailTxt.Text) ? null : _emailTxt.Text.Trim(),
                    AddressLine = string.IsNullOrWhiteSpace(_addressTxt.Text) ? null : _addressTxt.Text.Trim(),
                    City = string.IsNullOrWhiteSpace(_cityTxt.Text) ? null : _cityTxt.Text.Trim(),
                    Province = string.IsNullOrWhiteSpace(_provinceTxt.Text) ? null : _provinceTxt.Text.Trim(),
                    State = string.IsNullOrWhiteSpace(_provinceTxt.Text) ? null : _provinceTxt.Text.Trim(),
                    PostalCode = string.IsNullOrWhiteSpace(_postalCodeTxt.Text) ? null : _postalCodeTxt.Text.Trim(),
                    Country = string.IsNullOrWhiteSpace(_countryTxt.Text) ? null : _countryTxt.Text.Trim(),
                    IsActive = _activeChk.Checked,
                    AdminUserId = assignAdminUserId,
                    UnassignAdmin = unassign
                };

                using var resp = await _http.PutAsJsonAsync($"api/companies/{_company.CompanyId}", payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _errorLbl.Text = $"Save failed ({resp.StatusCode}): {ExtractMessage(err)}";
                    return;
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

        private static string ExtractMessage(string json)
        {
            try
            {
                var match = Regex.Match(json, @"""message""\s*:\s*""([^""]+)""");
                if (match.Success) return match.Groups[1].Value;
            }
            catch { }
            return json;
        }
    }
}
