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
        private static readonly Color Line = Color.FromArgb(0xE5, 0xE8, 0xEE);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);

        private const int PadX = 30;        // left / right margin inside the dialog
        private const int ContentW = 720;   // usable width (780 - 2 * 30)
        private const int Gap = 16;         // gap between columns
        private const int W2 = (ContentW - Gap) / 2;          // 352
        private const int X2b = PadX + W2 + Gap;              // 398
        private const int W3 = (ContentW - 2 * Gap) / 3;      // 229
        private const int X3b = PadX + W3 + Gap;              // 275
        private const int X3c = PadX + 2 * (W3 + Gap);       // 520
        private const int W3c = PadX + ContentW - X3c;        // 230

        // -----------------------------------------------------------------
        //  State
        // -----------------------------------------------------------------
        private readonly int? _customerId;
        private TenantCustomerDto? _loaded;   // the record as it came from the API (edit mode)

        private TextBox _fullNameTxt = null!;
        private TextBox _phoneTxt = null!;
        private TextBox _emailTxt = null!;
        private TextBox _addressTxt = null!;

        private TextBox _plateTxt = null!;
        private TextBox _makeTxt = null!;
        private TextBox _modelTxt = null!;
        private TextBox _yearTxt = null!;
        private TextBox _colorTxt = null!;
        private ComboBox _typeCombo = null!;

        private ComboBox _sourceCombo = null!;
        private ComboBox _statusCombo = null!;

        private Label _titleLbl = null!;
        private Label _errorLbl = null!;

        private readonly HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };

        // ---- Validation patterns ----
        private static readonly Regex PhoneRegex = new Regex(@"^[0-9+\-\s()]{7,20}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PlateRegex = new Regex(@"^[A-Za-z0-9\- ]{2,15}$", RegexOptions.Compiled);

        public CustomerEditDialog(int? customerId)
        {
            _customerId = customerId;
            InitializeForm();

            if (customerId.HasValue)
                Load += async (s, e) => await LoadCustomerAsync(customerId.Value);
        }

        private string TitleText(string? code = null)
        {
            if (!_customerId.HasValue) return "Register New Customer";
            var tag = string.IsNullOrWhiteSpace(code) ? "#" + _customerId : code;
            return "Edit Customer — " + tag;
        }

        // =================================================================
        //  UI
        // =================================================================
        private void InitializeForm()
        {
            Text = _customerId.HasValue ? "Edit Customer" : "Register New Customer";
            ClientSize = new Size(780, 640);              // client size, not outer size
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // ---- Header (no custom X button; the window already has one) ----
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
                Size = new Size(400, 48),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            footer.Controls.Add(_errorLbl);

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
                Text = _customerId.HasValue ? "Save Changes" : "Register Customer",
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

            // Right-align the buttons with the same 30px margin as the fields
            void PlaceButtons()
            {
                saveBtn.Location = new Point(footer.ClientSize.Width - PadX - saveBtn.Width, 16);
                cancelBtn.Location = new Point(saveBtn.Left - 12 - cancelBtn.Width, 16);
            }
            footer.Resize += (s, e) => PlaceButtons();
            PlaceButtons();

            CancelButton = cancelBtn;   // Esc closes the dialog

            // ---- Body ----
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };

            int y = 12;
            body.Controls.Add(SectionDivider("CUSTOMER DETAILS", y));
            y = 44;

            _fullNameTxt = AddText(body, "Full name *", PadX, y, W2, "e.g. Juan Dela Cruz");
            _phoneTxt = AddText(body, "Phone *", X2b, y, W2, "e.g. 09171234567");
            y += 60;

            _emailTxt = AddText(body, "Email *", PadX, y, ContentW, "e.g. juan@example.com");
            y += 60;

            body.Controls.Add(MakeLabel("Address", PadX, y));
            _addressTxt = new TextBox
            {
                Location = new Point(PadX, y + 18),
                Size = new Size(ContentW, 60),
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Street, City, Province"
            };
            _addressTxt.TextChanged += (s, e) => ClearError();
            body.Controls.Add(_addressTxt);
            y += 100;

            body.Controls.Add(SectionDivider("VEHICLE INFO", y));
            y += 32;

            _plateTxt = AddText(body, "Plate number *", PadX, y, W3, "e.g. ABC 1234");
            _makeTxt = AddText(body, "Make", X3b, y, W3, "e.g. Toyota");
            _modelTxt = AddText(body, "Model", X3c, y, W3c, "e.g. Fortuner");
            y += 60;

            _yearTxt = AddText(body, "Year", PadX, y, W3, "e.g. 2020");
            _colorTxt = AddText(body, "Color", X3b, y, W3, "e.g. White");
            _typeCombo = AddCombo(body, "Type", X3c, y, W3c,
                new object[] { "", "Sedan", "SUV", "Pickup", "Hatchback", "Van", "Motorcycle" });
            y += 60;

            body.Controls.Add(SectionDivider("OTHER", y));
            y += 32;

            _sourceCombo = AddCombo(body, "Source", PadX, y, W2,
                new object[] { "", "Facebook", "Google", "Referral", "Walk-in", "Instagram", "Other" });
            _statusCombo = AddCombo(body, "Status", X2b, y, W2,
                new object[] { "Active", "Inactive" });
            _statusCombo.SelectedIndex = 0;

            // Docking order matters: edge-docked panels first, Fill last (front of z-order)
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
                FlatStyle = FlatStyle.Standard,
                BackColor = Color.White
            };
            cb.Items.AddRange(items);
            parent.Controls.Add(cb);
            return cb;
        }

        // Centered label with a line on each side. Width is fixed to ContentW so it
        // lines up exactly with the fields (the old one used body.Width, which was wrong).
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

                _fullNameTxt.Text = c.CustomerName;
                _phoneTxt.Text = c.ContactNumber ?? "";
                _emailTxt.Text = c.EmailAddress ?? "";
                _addressTxt.Text = c.Address ?? "";

                _plateTxt.Text = c.PlateNumber ?? "";
                _makeTxt.Text = c.VehicleMake ?? "";
                _modelTxt.Text = c.VehicleModel ?? "";
                _yearTxt.Text = c.VehicleYear?.ToString() ?? "";
                _colorTxt.Text = c.VehicleColor ?? "";

                if (!string.IsNullOrEmpty(c.VehicleType)) _typeCombo.SelectedItem = c.VehicleType;
                if (!string.IsNullOrEmpty(c.Source)) _sourceCombo.SelectedItem = c.Source;
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

            if (string.IsNullOrWhiteSpace(_fullNameTxt.Text))
            { errorMessage = "Full name is required."; _fullNameTxt.Focus(); return false; }
            if (_fullNameTxt.Text.Trim().Length < 3)
            { errorMessage = "Full name must be at least 3 characters."; _fullNameTxt.Focus(); return false; }
            if (_fullNameTxt.Text.Trim().Length > 200)
            { errorMessage = "Full name must be 200 characters or less."; _fullNameTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_phoneTxt.Text))
            { errorMessage = "Phone is required."; _phoneTxt.Focus(); return false; }
            if (!PhoneRegex.IsMatch(_phoneTxt.Text.Trim()))
            {
                errorMessage = "Phone must be 7–20 digits. Only numbers, spaces, +, -, and ( ) are allowed.";
                _phoneTxt.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(_emailTxt.Text))
            { errorMessage = "Email is required."; _emailTxt.Focus(); return false; }
            if (!EmailRegex.IsMatch(_emailTxt.Text.Trim()))
            { errorMessage = "Email isn't in a valid format. Example: name@example.com"; _emailTxt.Focus(); return false; }
            if (_emailTxt.Text.Trim().Length > 200)
            { errorMessage = "Email must be 200 characters or less."; _emailTxt.Focus(); return false; }

            if (_addressTxt.Text.Trim().Length > 500)
            { errorMessage = "Address must be 500 characters or less."; _addressTxt.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_plateTxt.Text))
            { errorMessage = "Plate number is required."; _plateTxt.Focus(); return false; }
            if (!PlateRegex.IsMatch(_plateTxt.Text.Trim()))
            {
                errorMessage = "Plate number must be 2–15 characters (letters, numbers, spaces, hyphens only).";
                _plateTxt.Focus();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_yearTxt.Text))
            {
                if (!int.TryParse(_yearTxt.Text.Trim(), out int yr))
                { errorMessage = "Year must be a number (e.g. 2020)."; _yearTxt.Focus(); return false; }

                int maxYear = DateTime.Now.Year + 1;
                if (yr < 1900 || yr > maxYear)
                { errorMessage = $"Year must be between 1900 and {maxYear}."; _yearTxt.Focus(); return false; }
            }

            return true;
        }

        // =================================================================
        //  CONFIRM
        // =================================================================
        private static string Or(string? s, string fallback = "(none)")
            => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();

        private bool ConfirmSave()
        {
            string action = _customerId.HasValue ? "save changes to" : "register";
            string title = _customerId.HasValue ? "Confirm Save Changes" : "Confirm Registration";

            string summary =
                $"You are about to {action} this customer:\n\n" +
                $"  Full name : {_fullNameTxt.Text.Trim()}\n" +
                $"  Phone     : {_phoneTxt.Text.Trim()}\n" +
                $"  Email     : {_emailTxt.Text.Trim()}\n" +
                $"  Address   : {Or(_addressTxt.Text)}\n\n" +
                $"  Plate #   : {_plateTxt.Text.Trim()}\n" +
                $"  Make      : {Or(_makeTxt.Text)}\n" +
                $"  Model     : {Or(_modelTxt.Text)}\n" +
                $"  Year      : {Or(_yearTxt.Text)}\n" +
                $"  Color     : {Or(_colorTxt.Text)}\n" +
                $"  Type      : {Or(_typeCombo.SelectedItem?.ToString())}\n\n" +
                $"  Source    : {Or(_sourceCombo.SelectedItem?.ToString())}\n" +
                $"  Status    : {Or(_statusCombo.SelectedItem?.ToString())}\n\n" +
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
            // 1. Validate — show the message inline in the footer instead of a popup
            if (!ValidateForm(out string error))
            {
                _errorLbl.Text = error;
                return;
            }
            ClearError();

            // 2. Confirm
            if (!ConfirmSave()) return;

            // 3. Vehicle year
            int? vehicleYear = null;
            if (int.TryParse(_yearTxt.Text.Trim(), out var yr)) vehicleYear = yr;

            // 4. Build payload. On edit, keep the original customer code and created date
            //    (the old code regenerated both on every save).
            string customerCode =
                _loaded?.CustomerCode
                ?? (_customerId.HasValue
                    ? $"CUST{_customerId:00000}"
                    : $"CUST{DateTime.Now:yyyyMMddHHmmss}");

            var dto = new
            {
                customerCode,
                customerName = _fullNameTxt.Text.Trim(),
                contactNumber = _phoneTxt.Text.Trim(),
                emailAddress = _emailTxt.Text.Trim(),
                address = _addressTxt.Text.Trim(),
                isActive = _statusCombo.SelectedItem?.ToString() != "Inactive",
                createdAt = _loaded?.CreatedAt ?? DateTime.UtcNow,

                plateNumber = _plateTxt.Text.Trim(),
                vehicleMake = _makeTxt.Text.Trim(),
                vehicleModel = _modelTxt.Text.Trim(),
                vehicleYear,
                vehicleColor = _colorTxt.Text.Trim(),
                vehicleType = _typeCombo.SelectedItem?.ToString(),
                source = _sourceCombo.SelectedItem?.ToString()
            };

            try
            {
                HttpResponseMessage resp = _customerId.HasValue
                    ? await _http.PutAsJsonAsync($"api/tenant/1/tenant-customers/{_customerId.Value}", dto)
                    : await _http.PostAsJsonAsync("api/tenant/1/tenant-customers", dto);

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        _customerId.HasValue ? "Customer updated." : "Customer registered.",
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
    }
}