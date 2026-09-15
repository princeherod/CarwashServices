using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Views;

namespace CarwashServices
{
    public class CustomerEditDialog : Form
    {
        private int? _customerId;

        private TextBox _fullNameTxt;
        private TextBox _phoneTxt;
        private TextBox _emailTxt;
        private TextBox _addressTxt;

        private TextBox _plateTxt;
        private TextBox _makeTxt;
        private TextBox _modelTxt;
        private TextBox _yearTxt;
        private TextBox _colorTxt;
        private ComboBox _typeCombo;
        private ComboBox _sourceCombo;

        private HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };

        public CustomerEditDialog(int? customerId)
        {
            _customerId = customerId;
            InitializeForm();

            if (customerId.HasValue)
            {
                Load += async (s, e) => await LoadCustomerAsync(customerId.Value);
            }
        }

        private void InitializeForm()
        {
            Text = _customerId.HasValue ? $"Edit Customer — #{_customerId}" : "Register New Customer";
            Size = new Size(800, 720);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(30, 0, 30, 0)
            };

            var headerTitle = new Label
            {
                Text = _customerId.HasValue
                    ? $"Edit Customer — #{_customerId}"
                    : "Register New Customer",
                ForeColor = Color.FromArgb(0x0A, 0x14, 0x28),
                Font = new Font("Segoe UI Semibold", 13f),
                Location = new Point(30, 20),
                AutoSize = true
            };
            header.Controls.Add(headerTitle);

            var closeBtn = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                BackColor = Color.White,
                Size = new Size(30, 30),
                Location = new Point(Width - 60, 15),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            header.Controls.Add(closeBtn);

            Controls.Add(header);

            // ---- Body (scrollable) ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(30, 20, 30, 20),
                BackColor = Color.White
            };
            Controls.Add(body);
            body.BringToFront();

            int y = 20;

            // Section: CUSTOMERS FIELDS
            body.Controls.Add(SectionDivider("CUSTOMERS FIELDS", y, body.Width - 60));
            y += 40;

            // Row 1: FULL_NAME + PHONE
            body.Controls.Add(MakeLabel("FULL_NAME *", 30, y));
            _fullNameTxt = MakeTextBox(30, y + 20, 340);
            body.Controls.Add(_fullNameTxt);

            body.Controls.Add(MakeLabel("PHONE *", 400, y));
            _phoneTxt = MakeTextBox(400, y + 20, 340);
            body.Controls.Add(_phoneTxt);
            y += 70;

            // Row 2: EMAIL (full width)
            body.Controls.Add(MakeLabel("EMAIL *", 30, y));
            _emailTxt = MakeTextBox(30, y + 20, 710);
            body.Controls.Add(_emailTxt);
            y += 70;

            // Row 3: ADDRESS (multiline)
            body.Controls.Add(MakeLabel("ADDRESS", 30, y));
            _addressTxt = new TextBox
            {
                Location = new Point(30, y + 20),
                Width = 710,
                Height = 60,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            body.Controls.Add(_addressTxt);
            y += 90;

            // Section: VEHICLE INFO
            body.Controls.Add(SectionDivider("VEHICLE INFO (CARWASH EXTENSION)", y, body.Width - 60));
            y += 40;

            // Row: PLATE_NUMBER + VEHICLE_MAKE + VEHICLE_MODEL
            body.Controls.Add(MakeLabel("PLATE_NUMBER *", 30, y));
            _plateTxt = MakeTextBox(30, y + 20, 220);
            body.Controls.Add(_plateTxt);

            body.Controls.Add(MakeLabel("VEHICLE_MAKE", 270, y));
            _makeTxt = MakeTextBox(270, y + 20, 220);
            body.Controls.Add(_makeTxt);

            body.Controls.Add(MakeLabel("VEHICLE_MODEL", 510, y));
            _modelTxt = MakeTextBox(510, y + 20, 230);
            body.Controls.Add(_modelTxt);
            y += 70;

            // Row: VEHICLE_YEAR + VEHICLE_COLOR + VEHICLE_TYPE
            body.Controls.Add(MakeLabel("VEHICLE_YEAR", 30, y));
            _yearTxt = MakeTextBox(30, y + 20, 220);
            body.Controls.Add(_yearTxt);

            body.Controls.Add(MakeLabel("VEHICLE_COLOR", 270, y));
            _colorTxt = MakeTextBox(270, y + 20, 220);
            body.Controls.Add(_colorTxt);

            body.Controls.Add(MakeLabel("VEHICLE_TYPE", 510, y));
            _typeCombo = new ComboBox
            {
                Location = new Point(510, y + 20),
                Width = 230,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _typeCombo.Items.AddRange(new object[]
            {
                "Sedan", "SUV", "Pickup", "Hatchback", "Van", "Motorcycle"
            });
            body.Controls.Add(_typeCombo);
            y += 70;

            // Row: SOURCE
            body.Controls.Add(MakeLabel("SOURCE", 30, y));
            _sourceCombo = new ComboBox
            {
                Location = new Point(30, y + 20),
                Width = 710,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _sourceCombo.Items.AddRange(new object[]
            {
                "Facebook", "Google", "Referral", "Walk-in", "Instagram", "Other"
            });
            body.Controls.Add(_sourceCombo);
            y += 90;

            // ---- Footer buttons ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(30, 10, 30, 20)
            };

            var cancelBtn = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                BackColor = Color.White,
                Size = new Size(100, 40),
                Location = new Point(footer.Width - 300, 15),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(0xE5, 0xE8, 0xEE);
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancelBtn);

            var saveBtn = new Button
            {
                Text = _customerId.HasValue ? "Save Changes" : "Register Customer",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0x0A, 0x14, 0x28),
                Size = new Size(180, 40),
                Location = new Point(footer.Width - 190, 15),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(saveBtn);

            Controls.Add(footer);
            footer.BringToFront();
        }

        private Label MakeLabel(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private TextBox MakeTextBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Width = width,
                Height = 32,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
        }

        private Panel SectionDivider(string text, int y, int width)
        {
            var p = new Panel
            {
                Location = new Point(30, y),
                Size = new Size(width, 20),
                BackColor = Color.Transparent
            };

            var line = new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(0x1E, 0x88, 0xE5),
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(width / 2 - 80, 0),
                AutoSize = true,
                BackColor = Color.White
            };
            p.Controls.Add(line);

            var left = new Panel
            {
                Location = new Point(0, 9),
                Size = new Size(width / 2 - 100, 1),
                BackColor = Color.FromArgb(0xE5, 0xE8, 0xEE)
            };
            p.Controls.Add(left);

            var right = new Panel
            {
                Location = new Point(width / 2 + 90, 9),
                Size = new Size(width / 2 - 90, 1),
                BackColor = Color.FromArgb(0xE5, 0xE8, 0xEE)
            };
            p.Controls.Add(right);

            return p;
        }

        private async Task LoadCustomerAsync(int id)
        {
            try
            {
                var list = await _http.GetFromJsonAsync<System.Collections.Generic.List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers");

                if (list == null) return;
                var c = list.Find(x => x.TenantCustomerId == id);
                if (c == null) return;

                // ---- Customer fields ----
                _fullNameTxt.Text = c.CustomerName;
                _phoneTxt.Text = c.ContactNumber ?? "";
                _emailTxt.Text = c.EmailAddress ?? "";
                _addressTxt.Text = c.Address ?? "";

                // ---- Vehicle fields ----
                _plateTxt.Text = c.PlateNumber ?? "";
                _makeTxt.Text = c.VehicleMake ?? "";
                _modelTxt.Text = c.VehicleModel ?? "";
                _yearTxt.Text = c.VehicleYear?.ToString() ?? "";
                _colorTxt.Text = c.VehicleColor ?? "";

                if (!string.IsNullOrEmpty(c.VehicleType))
                    _typeCombo.SelectedItem = c.VehicleType;

                if (!string.IsNullOrEmpty(c.Source))
                    _sourceCombo.SelectedItem = c.Source;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customer.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // SAVE — uses PUT for edit, POST for create
        // ============================================================
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(_fullNameTxt.Text) ||
                string.IsNullOrWhiteSpace(_phoneTxt.Text) ||
                string.IsNullOrWhiteSpace(_emailTxt.Text))
            {
                MessageBox.Show("FULL_NAME, PHONE, and EMAIL are required.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Parse vehicle year as nullable int
            int? vehicleYear = null;
            if (int.TryParse(_yearTxt.Text.Trim(), out var yr))
                vehicleYear = yr;

            var dto = new
            {
                customerCode = _customerId.HasValue
                    ? $"CUST{_customerId:00000}"
                    : $"CUST{DateTime.Now:yyyyMMddHHmmss}",
                customerName = _fullNameTxt.Text.Trim(),
                contactNumber = _phoneTxt.Text.Trim(),
                emailAddress = _emailTxt.Text.Trim(),
                address = _addressTxt.Text.Trim(),
                isActive = true,
                createdAt = DateTime.UtcNow,

                // ---- Vehicle + source fields ----
                plateNumber = _plateTxt.Text.Trim(),
                vehicleMake = _makeTxt.Text.Trim(),
                vehicleModel = _modelTxt.Text.Trim(),
                vehicleYear = vehicleYear,
                vehicleColor = _colorTxt.Text.Trim(),
                vehicleType = _typeCombo.SelectedItem?.ToString(),
                source = _sourceCombo.SelectedItem?.ToString()
            };

            try
            {
                HttpResponseMessage resp;

                if (_customerId.HasValue)
                {
                    // ---- EDIT → PUT (update existing row) ----
                    resp = await _http.PutAsJsonAsync(
                        $"api/tenant/1/tenant-customers/{_customerId.Value}", dto);
                }
                else
                {
                    // ---- NEW → POST (insert new row) ----
                    resp = await _http.PostAsJsonAsync(
                        "api/tenant/1/tenant-customers", dto);
                }

                if (resp.IsSuccessStatusCode)
                {
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