using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class ServiceEditDialog : Form
    {
        private int? _serviceId;

        private TextBox _nameTxt;
        private TextBox _descriptionTxt;
        private TextBox _priceTxt;
        private TextBox _durationTxt;
        private ComboBox _categoryCombo;
        private ComboBox _activeCombo;

        private HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(10, 22, 51);
        private static readonly Color TextDark = Color.FromArgb(10, 22, 51);
        private static readonly Color TextMuted = Color.FromArgb(107, 122, 154);
        private static readonly Color BorderSoft = Color.FromArgb(225, 231, 240);
        private static readonly Color AccentBlue = Color.FromArgb(30, 136, 229);

        public ServiceEditDialog(int? serviceId)
        {
            _serviceId = serviceId;
            InitializeForm();

            if (serviceId.HasValue)
                Load += async (s, e) => await LoadServiceAsync(serviceId.Value);
        }

        private void InitializeForm()
        {
            Text = _serviceId.HasValue ? $"Edit Service — #{_serviceId}" : "Add New Service";
            Size = new Size(840, 720);
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
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(30, 0, 30, 0)
            };

            var headerTitle = new Label
            {
                Text = _serviceId.HasValue ? $"Edit Service — #{_serviceId}" : "Add New Service",
                ForeColor = TextDark,
                Font = new Font("Segoe UI Semibold", 13f),
                Location = new Point(30, 24),
                AutoSize = true
            };
            header.Controls.Add(headerTitle);

            var closeBtn = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11f),
                ForeColor = TextMuted,
                BackColor = Color.White,
                Size = new Size(32, 32),
                Location = new Point(Width - 65, 20),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            header.Controls.Add(closeBtn);
            Controls.Add(header);

            // ---- Body ----
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

            body.Controls.Add(SectionDivider("SERVICES FIELDS", y, body.Width - 60));
            y += 40;

            // SERVICE_NAME *
            body.Controls.Add(MakeLabel("SERVICE_NAME *", 30, y));
            _nameTxt = MakeTextBox(30, y + 22, 745);
            _nameTxt.PlaceholderText = "e.g. Basic Wash";
            body.Controls.Add(_nameTxt);
            y += 75;

            // DESCRIPTION
            body.Controls.Add(MakeLabel("DESCRIPTION", 30, y));
            _descriptionTxt = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 745,
                Height = 90,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Describe what this service includes..."
            };
            body.Controls.Add(_descriptionTxt);
            y += 125;

            // PRICE + DURATION
            body.Controls.Add(MakeLabel("PRICE (DECIMAL) *", 30, y));
            _priceTxt = MakeTextBox(30, y + 22, 360);
            _priceTxt.PlaceholderText = "500.00";
            body.Controls.Add(_priceTxt);

            body.Controls.Add(MakeLabel("DURATION_MINUTES (INT) *", 415, y));
            _durationTxt = MakeTextBox(415, y + 22, 360);
            _durationTxt.PlaceholderText = "30";
            body.Controls.Add(_durationTxt);
            y += 75;

            // CATEGORY + IS_ACTIVE
            body.Controls.Add(MakeLabel("CATEGORY", 30, y));
            _categoryCombo = new ComboBox
            {
                Location = new Point(30, y + 22),
                Width = 360,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _categoryCombo.Items.AddRange(new object[]
            {
                "", "Exterior", "Interior", "Full Service", "Specialty"
            });
            body.Controls.Add(_categoryCombo);

            body.Controls.Add(MakeLabel("IS_ACTIVE", 415, y));
            _activeCombo = new ComboBox
            {
                Location = new Point(415, y + 22),
                Width = 360,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _activeCombo.Items.AddRange(new object[] { "true", "false" });
            _activeCombo.SelectedIndex = 0;
            body.Controls.Add(_activeCombo);
            y += 85;

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White
            };

            var cancelBtn = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextMuted,
                BackColor = Color.White,
                Size = new Size(110, 42),
                Location = new Point(footer.Width - 320, 15),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancelBtn);

            var saveBtn = new Button
            {
                Text = _serviceId.HasValue ? "Save Changes" : "Add Service",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                Size = new Size(180, 42),
                Location = new Point(footer.Width - 200, 15),
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
            ForeColor = TextMuted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private TextBox MakeTextBox(int x, int y, int width) => new TextBox
        {
            Location = new Point(x, y),
            Width = width,
            Height = 34,
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

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
                ForeColor = AccentBlue,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(width / 2 - 70, 0),
                AutoSize = true,
                BackColor = Color.White
            };
            p.Controls.Add(line);

            p.Controls.Add(new Panel
            {
                Location = new Point(0, 9),
                Size = new Size(width / 2 - 90, 1),
                BackColor = BorderSoft
            });
            p.Controls.Add(new Panel
            {
                Location = new Point(width / 2 + 80, 9),
                Size = new Size(width / 2 - 80, 1),
                BackColor = BorderSoft
            });

            return p;
        }

        private async Task LoadServiceAsync(int id)
        {
            try
            {
                // ProductDto now lives in CarwashServices.Views (Dtos.cs)
                var list = await _http.GetFromJsonAsync<System.Collections.Generic.List<ProductDto>>(
                    "api/tenant/1/products");
                if (list == null) return;
                var p = list.Find(x => x.ProductId == id);
                if (p == null) return;

                _nameTxt.Text = p.ProductName;
                _descriptionTxt.Text = p.Description ?? "";
                _priceTxt.Text = p.UnitPrice.ToString("0.##");
                _durationTxt.Text = p.DurationMinutes.ToString();
                _categoryCombo.SelectedItem = p.Category ?? "";
                _activeCombo.SelectedItem = p.IsActive ? "true" : "false";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load service.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(_nameTxt.Text) ||
                string.IsNullOrWhiteSpace(_priceTxt.Text) ||
                string.IsNullOrWhiteSpace(_durationTxt.Text))
            {
                MessageBox.Show("SERVICE_NAME, PRICE, and DURATION_MINUTES are required.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_priceTxt.Text.Trim(), out var price))
            {
                MessageBox.Show("PRICE must be a valid number.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(_durationTxt.Text.Trim(), out var duration))
            {
                MessageBox.Show("DURATION_MINUTES must be a valid integer.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dto = new
            {
                productCode = _serviceId.HasValue
                    ? $"SVC{_serviceId.Value:00000}"
                    : $"SVC{DateTime.Now:yyyyMMddHHmmss}",
                productName = _nameTxt.Text.Trim(),
                description = _descriptionTxt.Text.Trim(),
                unitPrice = price,
                durationMinutes = duration,
                category = _categoryCombo.SelectedItem?.ToString() ?? "",
                isActive = _activeCombo.SelectedItem?.ToString() == "true",
                createdAt = DateTime.UtcNow
            };

            try
            {
                HttpResponseMessage resp;

                if (_serviceId.HasValue)
                {
                    resp = await _http.PutAsJsonAsync(
                        $"api/tenant/1/products/{_serviceId.Value}", dto);
                }
                else
                {
                    resp = await _http.PostAsJsonAsync(
                        "api/tenant/1/products", dto);
                }

                if (resp.IsSuccessStatusCode)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Save failed: {resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}