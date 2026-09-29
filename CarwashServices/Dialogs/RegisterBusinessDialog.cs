using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    public class RegisterBusinessDialog : Form
    {
        // Palette
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color SectionHeaderColor = Color.FromArgb(0x1E, 0x3A, 0x8A);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        // Controls - Business Info
        private TextBox _companyCodeTxt = null!;
        private TextBox _companyNameTxt = null!;
        private TextBox _phoneTxt = null!;
        private TextBox _emailTxt = null!;
        private TextBox _addressTxt = null!;
        private TextBox _cityTxt = null!;
        private TextBox _provinceTxt = null!;
        private TextBox _postalCodeTxt = null!;
        private TextBox _countryTxt = null!;

        // Controls - Admin Account
        private TextBox _adminFirstNameTxt = null!;
        private TextBox _adminLastNameTxt = null!;
        private TextBox _adminEmailTxt = null!;
        private TextBox _adminPasswordTxt = null!;
        private TextBox _adminConfirmTxt = null!;

        // Controls - Database Setup
        private TextBox _dbServerTxt = null!;
        private TextBox _dbNameTxt = null!;

        private Label _errorLbl = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;

        public int CreatedCompanyId { get; private set; }
        public string CreatedCompanyCode { get; private set; } = "";
        public string CreatedCompanyName { get; private set; } = "";

        public RegisterBusinessDialog()
        {
            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);
            Load += async (s, e) => await LoadNextCodeAsync();
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
            Text = "Register New Business";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 780);
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
                Text = "Register New Business",
                Font = new Font("Segoe UI Semibold", 13.5f),
                ForeColor = Navy,
                Location = new Point(32, 14),
                AutoSize = true
            };
            var subtitleLbl = new Label
            {
                Text = "Onboard a new tenant business and create their initial administrator account.",
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
                Text = "Register Business",
                Size = new Size(150, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Accent,
                Cursor = Cursors.Hand
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.Click += async (s, e) => await RegisterAsync();
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

            // ---- Body (Scrollable) ----
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

            // ===== SECTION 1: BUSINESS INFORMATION =====
            var sec1Lbl = new Label
            {
                Text = "1. BUSINESS INFORMATION",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = SectionHeaderColor,
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(sec1Lbl);
            y += 26;

            // Row 1: Company Code (auto, read-only) + Company Name (required)
            body.Controls.Add(Caption("COMPANY CODE (AUTO-GENERATED)", leftColX, y));
            body.Controls.Add(Caption("COMPANY NAME *", rightColX, y));
            y += 20;

            _companyCodeTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                ReadOnly = true,
                BackColor = Color.FromArgb(0xF1, 0xF5, 0xF9),
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 9.5f),
                Text = "Loading..."
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

            // Row 2: Contact Phone + Contact Email
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

            // Row 3: Address Line (Full width)
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
                Font = new Font("Segoe UI", 9.5f),
                Text = "Philippines"
            };
            body.Controls.Add(_countryTxt);
            y += 50;

            // Divider
            var div = new Panel
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 1),
                BackColor = BorderSoft
            };
            body.Controls.Add(div);
            y += 18;

            // ===== SECTION 2: INITIAL ADMINISTRATOR ACCOUNT =====
            var sec2Lbl = new Label
            {
                Text = "2. INITIAL ADMINISTRATOR ACCOUNT",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = SectionHeaderColor,
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(sec2Lbl);
            y += 22;

            var sec2Desc = new Label
            {
                Text = "This account will be assigned as the primary Admin for the business.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(sec2Desc);
            y += 28;

            // Row 6: First Name + Last Name
            body.Controls.Add(Caption("ADMIN FIRST NAME *", leftColX, y));
            body.Controls.Add(Caption("ADMIN LAST NAME *", rightColX, y));
            y += 20;

            _adminFirstNameTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_adminFirstNameTxt);

            _adminLastNameTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_adminLastNameTxt);
            y += 44;

            // Row 7: Admin Email Address
            body.Controls.Add(Caption("ADMIN EMAIL ADDRESS *", leftColX, y));
            y += 20;

            _adminEmailTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 30),
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_adminEmailTxt);
            y += 44;

            // Row 7: Password + Confirm Password
            body.Controls.Add(Caption("PASSWORD * (MIN 6 CHARACTERS)", leftColX, y));
            body.Controls.Add(Caption("CONFIRM PASSWORD *", rightColX, y));
            y += 20;

            _adminPasswordTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f),
                UseSystemPasswordChar = true
            };
            body.Controls.Add(_adminPasswordTxt);

            _adminConfirmTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f),
                UseSystemPasswordChar = true
            };
            body.Controls.Add(_adminConfirmTxt);
            y += 44;

            // Divider
            var div2 = new Panel
            {
                Location = new Point(leftColX, y),
                Size = new Size(fullW, 1),
                BackColor = BorderSoft
            };
            body.Controls.Add(div2);
            y += 18;

            // ===== SECTION 3: TENANT DATABASE CONFIGURATION =====
            var sec3Lbl = new Label
            {
                Text = "3. TENANT DATABASE CONFIGURATION",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = SectionHeaderColor,
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(sec3Lbl);
            y += 22;

            var sec3Desc = new Label
            {
                Text = "Configure the dedicated SQL database server and database name for multi-tenancy.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(leftColX, y),
                AutoSize = true
            };
            body.Controls.Add(sec3Desc);
            y += 28;

            // Row 8: DB Server & DB Name
            body.Controls.Add(Caption("DATABASE SERVER *", leftColX, y));
            body.Controls.Add(Caption("TENANT DATABASE NAME *", rightColX, y));
            y += 20;

            _dbServerTxt = new TextBox
            {
                Location = new Point(leftColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f),
                Text = "(localdb)\\MSSQLLocalDB"
            };
            body.Controls.Add(_dbServerTxt);

            _dbNameTxt = new TextBox
            {
                Location = new Point(rightColX, y),
                Size = new Size(colW, 30),
                Font = new Font("Segoe UI", 9.5f),
                Text = "Tenant_COMP005"
            };
            body.Controls.Add(_dbNameTxt);
            y += 50;

            Controls.Add(body);
            body.BringToFront();
        }

        private async Task LoadNextCodeAsync()
        {
            try
            {
                var res = await _http.GetFromJsonAsync<NextCompanyCodeDto>("api/companies/next-code");
                if (res != null && !string.IsNullOrWhiteSpace(res.NextCode))
                {
                    _companyCodeTxt.Text = res.NextCode;
                    _dbNameTxt.Text = $"Tenant_{res.NextCode}";
                }
                else
                {
                    _companyCodeTxt.Text = "COMP005";
                    _dbNameTxt.Text = "Tenant_COMP005";
                }
            }
            catch
            {
                _companyCodeTxt.Text = "COMP005";
                _dbNameTxt.Text = "Tenant_COMP005";
            }
        }

        private void SetBusy(bool busy)
        {
            _saveBtn.Enabled = !busy;
            _cancelBtn.Enabled = !busy;
            _saveBtn.Text = busy ? "Registering..." : "Register Business";
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private async Task RegisterAsync()
        {
            _errorLbl.Text = "";

            var compName = _companyNameTxt.Text.Trim();
            var compCode = _companyCodeTxt.Text.Trim();
            var adminFirst = _adminFirstNameTxt.Text.Trim();
            var adminLast = _adminLastNameTxt.Text.Trim();
            var adminEmail = _adminEmailTxt.Text.Trim();
            var pwd = _adminPasswordTxt.Text;
            var confirm = _adminConfirmTxt.Text;

            // Validations
            if (string.IsNullOrEmpty(compName))
            {
                _errorLbl.Text = "Company Name is required.";
                _companyNameTxt.Focus();
                return;
            }

            if (string.IsNullOrEmpty(compCode))
            {
                _errorLbl.Text = "Company Code is required.";
                return;
            }

            if (string.IsNullOrEmpty(adminFirst))
            {
                _errorLbl.Text = "Initial Admin First Name is required.";
                _adminFirstNameTxt.Focus();
                return;
            }

            if (string.IsNullOrEmpty(adminLast))
            {
                _errorLbl.Text = "Initial Admin Last Name is required.";
                _adminLastNameTxt.Focus();
                return;
            }

            if (string.IsNullOrEmpty(adminEmail))
            {
                _errorLbl.Text = "Initial Admin Email is required.";
                _adminEmailTxt.Focus();
                return;
            }

            if (!EmailRegex.IsMatch(adminEmail))
            {
                _errorLbl.Text = "Please enter a valid email address for the admin.";
                _adminEmailTxt.Focus();
                return;
            }

            if (string.IsNullOrEmpty(pwd))
            {
                _errorLbl.Text = "Admin password is required.";
                _adminPasswordTxt.Focus();
                return;
            }

            if (pwd.Length < 6)
            {
                _errorLbl.Text = "Password must be at least 6 characters long.";
                _adminPasswordTxt.Focus();
                return;
            }

            if (pwd != confirm)
            {
                _errorLbl.Text = "Passwords do not match.";
                _adminConfirmTxt.Focus();
                return;
            }

            SetBusy(true);

            try
            {
                // Step 1: Create Company and Initial Admin atomically
                var compPayload = new CreateCompanyRequestDto
                {
                    CompanyCode = compCode,
                    CompanyName = compName,
                    ContactPhone = string.IsNullOrWhiteSpace(_phoneTxt.Text) ? null : _phoneTxt.Text.Trim(),
                    ContactEmail = string.IsNullOrWhiteSpace(_emailTxt.Text) ? null : _emailTxt.Text.Trim(),
                    AddressLine = string.IsNullOrWhiteSpace(_addressTxt.Text) ? null : _addressTxt.Text.Trim(),
                    City = string.IsNullOrWhiteSpace(_cityTxt.Text) ? null : _cityTxt.Text.Trim(),
                    Province = string.IsNullOrWhiteSpace(_provinceTxt.Text) ? null : _provinceTxt.Text.Trim(),
                    State = string.IsNullOrWhiteSpace(_provinceTxt.Text) ? null : _provinceTxt.Text.Trim(),
                    PostalCode = string.IsNullOrWhiteSpace(_postalCodeTxt.Text) ? null : _postalCodeTxt.Text.Trim(),
                    Country = string.IsNullOrWhiteSpace(_countryTxt.Text) ? null : _countryTxt.Text.Trim(),
                    DatabaseServer = string.IsNullOrWhiteSpace(_dbServerTxt.Text) ? "(localdb)\\MSSQLLocalDB" : _dbServerTxt.Text.Trim(),
                    DatabaseName = string.IsNullOrWhiteSpace(_dbNameTxt.Text) ? $"Tenant_{compCode}" : _dbNameTxt.Text.Trim(),
                    InitialAdmin = new InitialAdminDto
                    {
                        FirstName = adminFirst,
                        LastName = adminLast,
                        FullName = $"{adminFirst} {adminLast}".Trim(),
                        Email = adminEmail,
                        Password = pwd
                    }
                };

                using var compResp = await _http.PostAsJsonAsync("api/companies", compPayload);
                if (!compResp.IsSuccessStatusCode)
                {
                    var err = await compResp.Content.ReadAsStringAsync();
                    _errorLbl.Text = $"Registration failed: {ExtractMessage(err)}";
                    return;
                }

                var createdCompany = await compResp.Content.ReadFromJsonAsync<CompanyListItemDto>();
                if (createdCompany == null)
                {
                    _errorLbl.Text = "Failed to parse created company details.";
                    return;
                }

                CreatedCompanyId = createdCompany.CompanyId;
                CreatedCompanyCode = createdCompany.CompanyCode;
                CreatedCompanyName = createdCompany.CompanyName;

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
