using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class CustomerEditDialog : Form
    {
        // -----------------------------------------------------------------
        //  Palette + layout constants
        // -----------------------------------------------------------------
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x14, 0x28);
        private static readonly Color NavyHover = Color.FromArgb(0x16, 0x28, 0x4A);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color Line = Color.FromArgb(0xE5, 0xE8, 0xEE);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);

        private const int PadX = 30;
        private const int ContentW = 760;
        private const int Gap = 16;
        private const int W2 = (ContentW - Gap) / 2;       // 372
        private const int X2b = PadX + W2 + Gap;           // 418
        private const int W3 = (ContentW - 2 * Gap) / 3;   // 242
        private const int X3b = PadX + W3 + Gap;           // 288
        private const int X3c = PadX + 2 * (W3 + Gap);     // 546
        private const int W3c = PadX + ContentW - X3c;     // 244

        // -----------------------------------------------------------------
        //  State
        // -----------------------------------------------------------------
        private readonly int? _customerId;
        private readonly bool _isEdit;
        private TenantCustomerDto? _loaded;

        private TextBox _firstNameTxt = null!;
        private TextBox _lastNameTxt = null!;
        private TextBox _phoneTxt = null!;
        private TextBox _emailTxt = null!;
        private TextBox _streetTxt = null!;
        private TextBox _cityTxt = null!;
        private TextBox _provinceTxt = null!;

        private TextBox _plateTxt = null!;
        private ComboBox _makeCombo = null!;
        private TextBox _makeOtherTxt = null!;
        private TextBox _modelTxt = null!;
        private ComboBox _yearCombo = null!;
        private ComboBox _bodyCombo = null!;
        private ComboBox _colorCombo = null!;
        private TextBox _colorOtherTxt = null!;

        private ComboBox _sourceCombo = null!;
        private ComboBox _statusCombo = null!;
        private Panel _statusRow = null!;

        private Label _titleLbl = null!;
        private Label _errorLbl = null!;
        private Button _deleteBtn = null!;

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly Regex PhoneRegex = new Regex(@"^[0-9+\-\s()]{7,20}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PlateRegex = new Regex(@"^[A-Za-z0-9\- ]{2,15}$", RegexOptions.Compiled);

        public CustomerEditDialog(int? customerId)
        {
            _customerId = customerId;
            _isEdit = customerId.HasValue;
            InitializeForm();

            if (_isEdit)
                Load += async (s, e) => await LoadCustomerAsync(customerId!.Value);
        }

        private string TitleText(string? code = null)
        {
            if (!_isEdit) return "Register New Customer";
            var tag = string.IsNullOrWhiteSpace(code) ? "#" + _customerId : code;
            return "Edit Customer — " + tag;
        }

        // =================================================================
        //  UI
        // =================================================================
        private void InitializeForm()
        {
            Text = _isEdit ? "Edit Customer" : "Register New Customer";
            ClientSize = new Size(820, 720);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(Line);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            _titleLbl = new Label
            {
                Text = TitleText(),
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 13f),
                Location = new Point(PadX, 15),
                AutoSize = true
            };
            header.Controls.Add(_titleLbl);

            // ---- Footer ----
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(Line);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                AutoSize = false,
                Location = new Point(PadX, 12),
                Size = new Size(360, 48),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            footer.Controls.Add(_errorLbl);

            // Delete (edit mode only)
            _deleteBtn = new Button
            {
                Text = "Delete Customer",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Danger,
                BackColor = Color.White,
                Size = new Size(160, 40),
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
                Size = new Size(100, 40),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = false
            };
            cancelBtn.FlatAppearance.BorderColor = Line;
            cancelBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            footer.Controls.Add(cancelBtn);

            var saveBtn = new Button
            {
                Text = _isEdit ? "Save Changes" : "Register Customer",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                Size = new Size(180, 40),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.FlatAppearance.MouseOverBackColor = NavyHover;
            saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(saveBtn);

            void PlaceButtons()
            {
                saveBtn.Location = new Point(footer.ClientSize.Width - PadX - saveBtn.Width, 16);
                cancelBtn.Location = new Point(saveBtn.Left - 12 - cancelBtn.Width, 16);
                _deleteBtn.Location = new Point(PadX, 16);
            }
            footer.Resize += (s, e) => PlaceButtons();
            PlaceButtons();

            CancelButton = cancelBtn;

            // ---- Body ----
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };

            int y = 12;

            // =================== CUSTOMER DETAILS ===================
            body.Controls.Add(SectionDivider("CUSTOMER DETAILS", y));
            y += 32;

            _firstNameTxt = AddText(body, "First name *", PadX, y, W2, "e.g. Juan");
            _lastNameTxt = AddText(body, "Last name *", X2b, y, W2, "e.g. Dela Cruz");
            y += 60;

            _phoneTxt = AddText(body, "Mobile number *", PadX, y, W2, "e.g. 0917 123 4567");
            AddHint(body, "Accepts 09xx or +63 9xx formats.", PadX, y + 44);

            _emailTxt = AddText(body, "Email *", X2b, y, W2, "e.g. juan@example.com");
            y += 78;

            _streetTxt = AddText(body, "Street / Barangay", PadX, y, W2, "e.g. 12 Rizal St., Brgy. Poblacion");
            y += 60;

            _cityTxt = AddText(body, "City / Municipality", PadX, y, W2, "e.g. Davao City");
            _provinceTxt = AddText(body, "Province", X2b, y, W2, "e.g. Davao del Sur");
            y += 74;

            // =================== VEHICLE INFORMATION ===================
            body.Controls.Add(SectionDivider("VEHICLE INFORMATION", y));
            y += 32;

            // Row 1: Plate / Make / Model
            _plateTxt = AddText(body, "Plate number *", PadX, y, W3, "e.g. ABC 1234");
            AddHint(body, "Letters and numbers only.", PadX, y + 44);

            _makeCombo = AddCombo(body, "Make (brand)", X3b, y, W3, BrandCatalog.Makes);
            // Make's "Other" free-text field sits below the combo (only shown when "Other" is picked)
            _makeOtherTxt = new TextBox
            {
                Location = new Point(X3b, y + 44),
                Width = W3,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Type brand",
                Visible = false
            };
            _makeOtherTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_makeOtherTxt);
            _makeCombo.SelectedIndexChanged += (s, e) =>
            {
                _makeOtherTxt.Visible = (string?)_makeCombo.SelectedItem == "Other";
            };

            _modelTxt = AddText(body, "Model", X3c, y, W3c, "e.g. Fortuner");
            y += 78;

            // Row 2: Year / Body / Color
            _yearCombo = AddCombo(body, "Year model", PadX, y, W3, BuildYearItems());
            _bodyCombo = AddCombo(body, "Body type", X3b, y, W3, BrandCatalog.BodyTypes);

            _colorCombo = AddCombo(body, "Color", X3c, y, W3c, BrandCatalog.Colors);
            // Color's "Other" free-text field sits next to the combo (only shown when "Other" is picked)
            _colorOtherTxt = new TextBox
            {
                Location = new Point(X3c + 130, y + 44),
                Width = Math.Max(60, W3c - 130),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Type color",
                Visible = false
            };
            _colorOtherTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_colorOtherTxt);
            _colorCombo.SelectedIndexChanged += (s, e) =>
            {
                _colorOtherTxt.Visible = (string?)_colorCombo.SelectedItem == "Other";
            };
            y += 78;

            // =================== OTHER ===================
            body.Controls.Add(SectionDivider("OTHER", y));
            y += 32;

            _sourceCombo = AddCombo(body, "Source", PadX, y, W2, BrandCatalog.Sources);

            // Status row (edit mode only)
            _statusRow = new Panel { Location = new Point(X2b, y), Size = new Size(W2, 60), BackColor = Color.White };
            _statusRow.Controls.Add(new Label
            {
                Text = "Status",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });
            _statusCombo = new ComboBox
            {
                Location = new Point(0, 18),
                Width = W2,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _statusCombo.Items.AddRange(new object[] { "Active", "Inactive" });
            _statusCombo.SelectedIndex = 0;
            _statusRow.Controls.Add(_statusCombo);
            body.Controls.Add(_statusRow);
            _statusRow.Visible = _isEdit;   // NOT shown when creating a new customer

            y += 78;

            Controls.Add(header);
            Controls.Add(footer);
            Controls.Add(body);
            body.BringToFront();
        }

        // ---- small builders ----
        private Label MakeLabel(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 9f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private void AddHint(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                ForeColor = Faint,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(x, y),
                AutoSize = true
            });
        }

        private TextBox AddText(Control parent, string label, int x, int y, int width, string placeholder)
        {
            parent.Controls.Add(MakeLabel(label, x, y));
            var tb = new TextBox
            {
                Location = new Point(x, y + 18),
                Width = width,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = placeholder
            };
            tb.TextChanged += (s, e) => ClearError();
            parent.Controls.Add(tb);
            return tb;
        }

        private ComboBox AddCombo(Control parent, string label, int x, int y, int width, object[] items)
        {
            parent.Controls.Add(MakeLabel(label, x, y));
            var cb = new ComboBox
            {
                Location = new Point(x, y + 18),
                Width = width,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            cb.Items.AddRange(items);
            cb.SelectedIndex = 0;
            parent.Controls.Add(cb);
            return cb;
        }

        private static object[] BuildYearItems()
        {
            var list = new System.Collections.Generic.List<object> { "" };
            foreach (var y in BrandCatalog.Years()) list.Add(y.ToString());
            return list.ToArray();
        }

        private Control SectionDivider(string text, int y)
        {
            var font = new Font("Segoe UI Semibold", 8.5f);
            int tw = TextRenderer.MeasureText(text, font).Width;
            int lx = (ContentW - tw) / 2;

            var p = new Panel
            {
                Location = new Point(PadX, y),
                Size = new Size(ContentW, 18),
                BackColor = Color.White
            };
            p.Controls.Add(new Label
            {
                Text = text,
                ForeColor = Accent,
                Font = font,
                AutoSize = true,
                BackColor = Color.White,
                Location = new Point(lx, 0)
            });
            p.Controls.Add(new Panel
            {
                Location = new Point(0, 9),
                Size = new Size(Math.Max(0, lx - 12), 1),
                BackColor = Line
            });
            p.Controls.Add(new Panel
            {
                Location = new Point(lx + tw + 12, 9),
                Size = new Size(Math.Max(0, ContentW - (lx + tw + 12)), 1),
                BackColor = Line
            });
            return p;
        }

        private void ClearError()
        {
            if (_errorLbl != null) _errorLbl.Text = "";
        }

        // =================================================================
        //  LOAD (edit mode)
        // =================================================================
        private async Task LoadCustomerAsync(int id)
        {
            try
            {
                var list = await _http.GetFromJsonAsync<System.Collections.Generic.List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers");

                if (list == null) return;
                var c = list.Find(x => x.TenantCustomerId == id);
                if (c == null) return;

                _loaded = c;
                _titleLbl.Text = TitleText(c.CustomerCode);

                // Split "Juan Dela Cruz" into first name + last name at the first space
                var full = c.CustomerName ?? "";
                int firstSpace = full.IndexOf(' ');
                if (firstSpace > 0)
                {
                    _firstNameTxt.Text = full.Substring(0, firstSpace);
                    _lastNameTxt.Text = full.Substring(firstSpace + 1).Trim();
                }
                else
                {
                    _firstNameTxt.Text = full;
                    _lastNameTxt.Text = "";
                }

                _phoneTxt.Text = c.ContactNumber ?? "";
                _emailTxt.Text = c.EmailAddress ?? "";

                // Address: we saved it as a single string — split on the first two commas if possible
                var addr = c.Address ?? "";
                var parts = addr.Split(',');
                if (parts.Length >= 3)
                {
                    _streetTxt.Text = parts[0].Trim();
                    _cityTxt.Text = parts[1].Trim();
                    _provinceTxt.Text = string.Join(", ", parts, 2, parts.Length - 2).Trim();
                }
                else if (parts.Length == 2)
                {
                    _streetTxt.Text = parts[0].Trim();
                    _cityTxt.Text = parts[1].Trim();
                }
                else
                {
                    _streetTxt.Text = addr;
                }

                _plateTxt.Text = c.PlateNumber ?? "";

                // Make: if the saved value is in our list, select it; otherwise "Other" + free text
                if (!string.IsNullOrWhiteSpace(c.VehicleMake) &&
                    Array.IndexOf(BrandCatalog.Makes, c.VehicleMake) >= 0)
                {
                    _makeCombo.SelectedItem = c.VehicleMake;
                }
                else if (!string.IsNullOrWhiteSpace(c.VehicleMake))
                {
                    _makeCombo.SelectedItem = "Other";
                    _makeOtherTxt.Text = c.VehicleMake;
                    _makeOtherTxt.Visible = true;
                }

                _modelTxt.Text = c.VehicleModel ?? "";

                if (c.VehicleYear.HasValue)
                    _yearCombo.SelectedItem = c.VehicleYear.Value.ToString();

                if (!string.IsNullOrWhiteSpace(c.VehicleType) &&
                    Array.IndexOf(BrandCatalog.BodyTypes, c.VehicleType) >= 0)
                    _bodyCombo.SelectedItem = c.VehicleType;
                else if (!string.IsNullOrWhiteSpace(c.VehicleType))
                    _bodyCombo.SelectedItem = "Other";

                if (!string.IsNullOrWhiteSpace(c.VehicleColor) &&
                    Array.IndexOf(BrandCatalog.Colors, c.VehicleColor) >= 0)
                {
                    _colorCombo.SelectedItem = c.VehicleColor;
                }
                else if (!string.IsNullOrWhiteSpace(c.VehicleColor))
                {
                    _colorCombo.SelectedItem = "Other";
                    _colorOtherTxt.Text = c.VehicleColor;
                    _colorOtherTxt.Visible = true;
                }

                if (!string.IsNullOrWhiteSpace(c.Source) &&
                    Array.IndexOf(BrandCatalog.Sources, c.Source) >= 0)
                    _sourceCombo.SelectedItem = c.Source;

                _statusCombo.SelectedItem = c.IsActive ? "Active" : "Inactive";

                ClearError();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customer.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =================================================================
        //  VALIDATION
        // =================================================================
        private bool ValidateForm(out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(_firstNameTxt.Text))
            { errorMessage = "First name is required."; _firstNameTxt.Focus(); return false; }
            if (_firstNameTxt.Text.Trim().Length < 2)
            { errorMessage = "First name must be at least 2 characters."; _firstNameTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_lastNameTxt.Text))
            { errorMessage = "Last name is required."; _lastNameTxt.Focus(); return false; }
            if (_lastNameTxt.Text.Trim().Length < 2)
            { errorMessage = "Last name must be at least 2 characters."; _lastNameTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_phoneTxt.Text))
            { errorMessage = "Mobile number is required."; _phoneTxt.Focus(); return false; }
            if (!PhoneRegex.IsMatch(_phoneTxt.Text.Trim()))
            {
                errorMessage = "Mobile number must be 7–20 digits. Only numbers, spaces, +, -, and ( ) are allowed.";
                _phoneTxt.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(_emailTxt.Text))
            { errorMessage = "Email is required."; _emailTxt.Focus(); return false; }
            if (!EmailRegex.IsMatch(_emailTxt.Text.Trim()))
            { errorMessage = "Email isn't in a valid format. Example: name@example.com"; _emailTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_plateTxt.Text))
            { errorMessage = "Plate number is required."; _plateTxt.Focus(); return false; }
            if (!PlateRegex.IsMatch(_plateTxt.Text.Trim()))
            {
                errorMessage = "Plate number must be 2–15 characters (letters, numbers, spaces, hyphens only).";
                _plateTxt.Focus();
                return false;
            }

            // If the user picked "Other" for make, force them to type a value
            if ((string?)_makeCombo.SelectedItem == "Other" &&
                string.IsNullOrWhiteSpace(_makeOtherTxt.Text))
            {
                errorMessage = "Please type the vehicle make you selected as 'Other'.";
                _makeOtherTxt.Focus();
                return false;
            }

            // If the user picked "Other" for colour, force them to type a value
            if ((string?)_colorCombo.SelectedItem == "Other" &&
                string.IsNullOrWhiteSpace(_colorOtherTxt.Text))
            {
                errorMessage = "Please type the colour you selected as 'Other'.";
                _colorOtherTxt.Focus();
                return false;
            }

            return true;
        }

        // =================================================================
        //  CONFIRM
        // =================================================================
        private static string Or(string? s, string fallback = "(none)")
            => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();

        private string ResolveMake()
        {
            var v = (string?)_makeCombo.SelectedItem ?? "";
            return v == "Other" ? _makeOtherTxt.Text.Trim() : v;
        }

        private string ResolveColor()
        {
            var v = (string?)_colorCombo.SelectedItem ?? "";
            return v == "Other" ? _colorOtherTxt.Text.Trim() : v;
        }

        private string ResolveAddress()
        {
            var parts = new[] { _streetTxt.Text.Trim(), _cityTxt.Text.Trim(), _provinceTxt.Text.Trim() };
            return string.Join(", ", Array.FindAll(parts, p => !string.IsNullOrWhiteSpace(p)));
        }

        private bool ConfirmSave()
        {
            string action = _isEdit ? "save changes to" : "register";
            string title = _isEdit ? "Confirm Save Changes" : "Confirm Registration";

            string summary =
                $"You are about to {action} this customer:\n\n" +
                $"  Name    : {_firstNameTxt.Text.Trim()} {_lastNameTxt.Text.Trim()}\n" +
                $"  Phone   : {_phoneTxt.Text.Trim()}\n" +
                $"  Email   : {_emailTxt.Text.Trim()}\n" +
                $"  Address : {Or(ResolveAddress())}\n\n" +
                $"  Plate # : {_plateTxt.Text.Trim()}\n" +
                $"  Make    : {Or(ResolveMake())}\n" +
                $"  Model   : {Or(_modelTxt.Text)}\n" +
                $"  Year    : {Or(_yearCombo.SelectedItem?.ToString())}\n" +
                $"  Body    : {Or(_bodyCombo.SelectedItem?.ToString())}\n" +
                $"  Color   : {Or(ResolveColor())}\n\n" +
                $"  Source  : {Or(_sourceCombo.SelectedItem?.ToString())}\n" +
                (_isEdit ? $"  Status  : {Or(_statusCombo.SelectedItem?.ToString())}\n\n" : "\n") +
                $"Proceed?";

            return MessageBox.Show(
                summary, title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        // =================================================================
        //  SAVE
        // =================================================================
        private async Task SaveAsync()
        {
            if (!ValidateForm(out string error))
            {
                _errorLbl.Text = error;
                return;
            }
            ClearError();

            if (!ConfirmSave()) return;

            int? vehicleYear = null;
            if (int.TryParse(_yearCombo.SelectedItem?.ToString(), out var yr))
                vehicleYear = yr;

            string customerCode =
                _loaded?.CustomerCode
                ?? (_isEdit
                    ? $"CUST{_customerId:00000}"
                    : $"CUST{DateTime.Now:yyyyMMddHHmmss}");

            var dto = new
            {
                customerCode,
                customerName = $"{_firstNameTxt.Text.Trim()} {_lastNameTxt.Text.Trim()}".Trim(),
                contactNumber = _phoneTxt.Text.Trim(),
                emailAddress = _emailTxt.Text.Trim(),
                address = ResolveAddress(),
                // On create, force Active. Status is only editable on edit.
                isActive = _isEdit
                    ? _statusCombo.SelectedItem?.ToString() != "Inactive"
                    : true,
                createdAt = _loaded?.CreatedAt ?? DateTime.UtcNow,

                plateNumber = _plateTxt.Text.Trim(),
                vehicleMake = ResolveMake(),
                vehicleModel = _modelTxt.Text.Trim(),
                vehicleYear,
                vehicleColor = ResolveColor(),
                vehicleType = _bodyCombo.SelectedItem?.ToString(),
                source = _sourceCombo.SelectedItem?.ToString()
            };

            try
            {
                HttpResponseMessage resp = _isEdit
                    ? await _http.PutAsJsonAsync($"api/tenant/1/tenant-customers/{_customerId!.Value}", dto)
                    : await _http.PostAsJsonAsync("api/tenant/1/tenant-customers", dto);

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        _isEdit ? "Customer updated." : "Customer registered.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Save failed.\n\nStatus: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Save failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =================================================================
        //  DELETE (edit mode only)
        // =================================================================
        private async Task DeleteAsync()
        {
            if (!_isEdit) return;

            var name = $"{_firstNameTxt.Text.Trim()} {_lastNameTxt.Text.Trim()}".Trim();
            var answer = MessageBox.Show(
                $"Delete this customer?\n\n{name}\n\nThis cannot be undone.",
                "Delete Customer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return;

            try
            {
                var resp = await _http.DeleteAsync($"api/tenant/1/tenant-customers/{_customerId!.Value}");
                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Customer deleted.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Delete failed.\n\nStatus: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Delete failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}