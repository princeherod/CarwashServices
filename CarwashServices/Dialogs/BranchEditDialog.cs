using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Auth;
using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class BranchEditDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color BorderSoft = Color.FromArgb(0xD1, 0xD9, 0xE6);
        private static readonly Color Blue = Color.FromArgb(0x1D, 0x4E, 0xD8);

        private readonly int? _branchId;
        private readonly BranchDto? _existing;

        private TextBox _codeTxt = null!;
        private TextBox _nameTxt = null!;
        private TextBox _cityTxt = null!;
        private TextBox _provinceTxt = null!;
        private TextBox _addressTxt = null!;
        private TextBox _phoneTxt = null!;
        private TextBox _emailTxt = null!;
        private CheckBox _isMainChk = null!;
        private CheckBox _isActiveChk = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;

        private static readonly HttpClient Http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        public BranchEditDialog(BranchDto? existing = null)
        {
            _existing = existing;
            _branchId = existing?.BranchId;

            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            Text = _existing != null ? $"Edit Branch - {_existing.BranchName}" : "Add New Branch";
            Size = new Size(540, 620);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Navy
            };
            var titleLbl = new Label
            {
                Text = _existing != null ? "EDIT BRANCH DETAILS" : "CREATE NEW BRANCH",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 12f),
                Location = new Point(24, 14),
                AutoSize = true
            };
            var subLbl = new Label
            {
                Text = $"{SessionUser.CompanyName} - Branch Operations Setup",
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(24, 38),
                AutoSize = true
            };
            header.Controls.AddRange(new Control[] { titleLbl, subLbl });
            Controls.Add(header);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 16, 24, 16),
                AutoScroll = true
            };

            int y = 14;
            int colW = 224;

            // Row 1: Code and Name
            body.Controls.Add(CreateCaption("BRANCH CODE *", 24, y));
            body.Controls.Add(CreateCaption("BRANCH NAME *", 24 + colW + 16, y));
            y += 22;

            _codeTxt = CreateTextBox(24, y, colW);
            _codeTxt.CharacterCasing = CharacterCasing.Upper;
            _nameTxt = CreateTextBox(24 + colW + 16, y, colW);
            body.Controls.AddRange(new Control[] { _codeTxt, _nameTxt });
            y += 44;

            // Row 2: City and Province
            body.Controls.Add(CreateCaption("CITY / MUNICIPALITY *", 24, y));
            body.Controls.Add(CreateCaption("PROVINCE", 24 + colW + 16, y));
            y += 22;

            _cityTxt = CreateTextBox(24, y, colW);
            _provinceTxt = CreateTextBox(24 + colW + 16, y, colW);
            body.Controls.AddRange(new Control[] { _cityTxt, _provinceTxt });
            y += 44;

            // Row 3: Street Address
            body.Controls.Add(CreateCaption("STREET ADDRESS", 24, y));
            y += 22;
            _addressTxt = CreateTextBox(24, y, colW * 2 + 16);
            body.Controls.Add(_addressTxt);
            y += 44;

            // Row 4: Phone and Email
            body.Controls.Add(CreateCaption("CONTACT PHONE", 24, y));
            body.Controls.Add(CreateCaption("EMAIL ADDRESS", 24 + colW + 16, y));
            y += 22;

            _phoneTxt = CreateTextBox(24, y, colW);
            _emailTxt = CreateTextBox(24 + colW + 16, y, colW);
            body.Controls.AddRange(new Control[] { _phoneTxt, _emailTxt });
            y += 48;

            // Row 5: Flags
            _isMainChk = new CheckBox
            {
                Text = "Set as Primary / Main Branch",
                Location = new Point(24, y),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy
            };
            _isActiveChk = new CheckBox
            {
                Text = "Active for operations",
                Location = new Point(24 + colW + 16, y),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy
            };
            body.Controls.AddRange(new Control[] { _isMainChk, _isActiveChk });
            y += 46;

            var hintLbl = new Label
            {
                Text = "All operational data (Service Requests, Customers, Follow-ups) recorded under this branch remain isolated but roll up to business-wide reports.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(24, y),
                Size = new Size(colW * 2 + 16, 36)
            };
            body.Controls.Add(hintLbl);

            Controls.Add(body);

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(0xEA, 0xF0, 0xF8)
            };

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Size = new Size(100, 36),
                Location = new Point(Width - 240, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Navy,
                Cursor = Cursors.Hand
            };
            _cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            _cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _saveBtn = new Button
            {
                Text = _existing != null ? "Save Changes" : "Create Branch",
                Size = new Size(116, 36),
                Location = new Point(Width - 132, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Cursor = Cursors.Hand
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.Click += async (s, e) => await SaveAsync();

            footer.Controls.AddRange(new Control[] { _cancelBtn, _saveBtn });
            Controls.Add(footer);
        }

        private static Label CreateCaption(string text, int x, int y) => new()
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 8f),
            ForeColor = Muted
        };

        private static TextBox CreateTextBox(int x, int y, int width) => new()
        {
            Location = new Point(x, y),
            Size = new Size(width, 30),
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        private void PopulateData()
        {
            if (_existing != null)
            {
                _codeTxt.Text = _existing.BranchCode;
                _nameTxt.Text = _existing.BranchName;
                _cityTxt.Text = _existing.City ?? "";
                _provinceTxt.Text = _existing.Province ?? "";
                _addressTxt.Text = _existing.Address ?? "";
                _phoneTxt.Text = _existing.ContactNumber ?? "";
                _emailTxt.Text = _existing.Email ?? "";
                _isMainChk.Checked = _existing.IsMainBranch;
                _isActiveChk.Checked = _existing.IsActive;
            }
            else
            {
                _codeTxt.Text = "BR-";
                _cityTxt.Text = "Davao City";
                _provinceTxt.Text = "Davao del Sur";
            }
        }

        private async Task SaveAsync()
        {
            var code = _codeTxt.Text.Trim();
            var name = _nameTxt.Text.Trim();
            var city = _cityTxt.Text.Trim();

            if (string.IsNullOrWhiteSpace(code) || code.Length < 3)
            {
                MessageBox.Show("Please enter a valid Branch Code (minimum 3 characters).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _codeTxt.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a Branch Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _nameTxt.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                MessageBox.Show("Please enter a City or Municipality for the branch.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cityTxt.Focus();
                return;
            }

            _saveBtn.Enabled = false;
            _saveBtn.Text = "Saving...";

            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                var payload = new
                {
                    BranchCode = code,
                    BranchName = name,
                    Address = _addressTxt.Text.Trim(),
                    City = city,
                    Province = _provinceTxt.Text.Trim(),
                    ContactNumber = _phoneTxt.Text.Trim(),
                    Email = _emailTxt.Text.Trim(),
                    IsMainBranch = _isMainChk.Checked,
                    IsActive = _isActiveChk.Checked
                };

                HttpResponseMessage resp;
                if (_existing != null)
                {
                    resp = await Http.PutAsJsonAsync($"api/tenant/{companyId}/branches/{_existing.BranchId}", payload);
                }
                else
                {
                    resp = await Http.PostAsJsonAsync($"api/tenant/{companyId}/branches", payload);
                }

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        _existing != null ? "Branch updated successfully." : "Branch created successfully.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to save branch: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _saveBtn.Enabled = true;
                _saveBtn.Text = _existing != null ? "Save Changes" : "Create Branch";
            }
        }
    }
}
