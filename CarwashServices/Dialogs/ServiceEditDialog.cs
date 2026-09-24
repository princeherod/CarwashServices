using System;
using System.Drawing;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class ServiceEditDialog : Form
    {
        private int? _serviceId;

        private TextBox _nameTxt = null!;
        private TextBox _descriptionTxt = null!;
        private TextBox _priceTxt = null!;
        private TextBox _durationTxt = null!;
        private ComboBox _categoryCombo = null!;
        private ComboBox _activeCombo = null!;
        private Panel _activeRow = null!;

        private readonly ErrorProvider _errors = new ErrorProvider();

        private HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(10, 22, 51);
        private static readonly Color TextDark = Color.FromArgb(10, 22, 51);
        private static readonly Color TextMuted = Color.FromArgb(107, 122, 154);
        private static readonly Color BorderSoft = Color.FromArgb(225, 231, 240);
        private static readonly Color AccentBlue = Color.FromArgb(30, 136, 229);
        private static readonly Color FieldErrorBg = Color.FromArgb(0xFF, 0xF5, 0xF5);

        // ---- Validation regexes ----
        private static readonly Regex NameRegex =
            new(@"^[\p{L}\s\.\,\-\'&/\(\)]{2,150}$", RegexOptions.Compiled);

        private static readonly Regex DescriptionRegex =
            new(@"^[\p{L}\s\.\,\-\'&/\(\)!?;:""]{0,1000}$", RegexOptions.Compiled);

        private static readonly Regex CategoryRegex =
            new(@"^(?=.*[\p{L}])[\p{L}\s\-&]{2,50}$", RegexOptions.Compiled);

        public ServiceEditDialog(int? serviceId)
        {
            _serviceId = serviceId;
            InitializeForm();

            if (serviceId.HasValue)
                Load += async (s, e) => await LoadServiceAsync(serviceId.Value);
        }

        private void InitializeForm()
        {
            bool isEdit = _serviceId.HasValue;

            Text = isEdit ? $"Edit Service — #{_serviceId}" : "Add New Service";
            Size = new Size(840, isEdit ? 720 : 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _errors.BlinkStyle = ErrorBlinkStyle.NeverBlink;
            _errors.ContainerControl = this;

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(30, 0, 30, 0)
            };

            header.Controls.Add(new Label
            {
                Text = isEdit ? $"Edit Service — #{_serviceId}" : "Add New Service",
                ForeColor = TextDark,
                Font = new Font("Segoe UI Semibold", 13f),
                Location = new Point(30, 24),
                AutoSize = true
            });

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

            body.Controls.Add(SectionDivider("SERVICE DETAILS", y, body.Width - 60));
            y += 40;

            // Service Name *
            body.Controls.Add(MakeLabel("Service Name *", 30, y));
            _nameTxt = MakeTextBox(30, y + 22, 745);
            _nameTxt.PlaceholderText = "e.g. Basic Wash";
            _nameTxt.TextChanged += (s, e) => ClearFieldError(_nameTxt);
            body.Controls.Add(_nameTxt);
            y += 75;

            // Description
            body.Controls.Add(MakeLabel("Description", 30, y));
            _descriptionTxt = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 745,
                Height = 90,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Describe what this service includes...",
                ScrollBars = ScrollBars.Vertical
            };
            _descriptionTxt.TextChanged += (s, e) => ClearFieldError(_descriptionTxt);
            body.Controls.Add(_descriptionTxt);
            y += 125;

            // Price + Duration
            body.Controls.Add(MakeLabel("Price (₱) *", 30, y));
            _priceTxt = MakeTextBox(30, y + 22, 360);
            _priceTxt.PlaceholderText = "500.00";
            _priceTxt.TextChanged += (s, e) => ClearFieldError(_priceTxt);
            body.Controls.Add(_priceTxt);

            body.Controls.Add(MakeLabel("Duration (minutes) *", 415, y));
            _durationTxt = MakeTextBox(415, y + 22, 360);
            _durationTxt.PlaceholderText = "30";
            _durationTxt.TextChanged += (s, e) => ClearFieldError(_durationTxt);
            body.Controls.Add(_durationTxt);
            y += 75;

            // Category + Status
            body.Controls.Add(MakeLabel("Category *", 30, y));
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
            _categoryCombo.SelectedIndex = 0;
            _categoryCombo.SelectedIndexChanged += (s, e) => ClearFieldError(_categoryCombo);
            body.Controls.Add(_categoryCombo);

            // Status — only shown on edit. Create forces Active.
            _activeRow = new Panel
            {
                Location = new Point(415, y),
                Size = new Size(360, 60),
                BackColor = Color.White,
                Visible = isEdit
            };
            _activeRow.Controls.Add(MakeLabel("Status", 0, 0));
            _activeCombo = new ComboBox
            {
                Location = new Point(0, 22),
                Width = 360,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            _activeCombo.Items.AddRange(new object[] { "Active", "Inactive" });
            _activeCombo.SelectedIndex = 0;
            _activeRow.Controls.Add(_activeCombo);
            body.Controls.Add(_activeRow);

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
                Text = isEdit ? "Save Changes" : "Add Service",
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

            p.Controls.Add(new Label
            {
                Text = text,
                ForeColor = AccentBlue,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(width / 2 - 70, 0),
                AutoSize = true,
                BackColor = Color.White
            });

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

        // =================================================================
        //  Error helpers
        // =================================================================
        private void ClearFieldError(Control c)
        {
            if (c is TextBox tb)
            {
                tb.BackColor = Color.White;
                _errors.SetError(tb, "");
            }
            else if (c is ComboBox cb)
            {
                cb.BackColor = Color.White;
                _errors.SetError(cb, "");
            }
        }

        private void MarkFieldError(Control c, string message)
        {
            if (c is TextBox tb)
            {
                tb.BackColor = FieldErrorBg;
                _errors.SetError(tb, message);
            }
            else if (c is ComboBox cb)
            {
                cb.BackColor = FieldErrorBg;
                _errors.SetError(cb, message);
            }
        }

        // =================================================================
        //  LOAD (edit mode)
        // =================================================================
        private async Task LoadServiceAsync(int id)
        {
            try
            {
                var list = await _http.GetFromJsonAsync<System.Collections.Generic.List<ProductDto>>(
                    "api/tenant/1/products");
                if (list == null) return;
                var p = list.Find(x => x.ProductId == id);
                if (p == null) return;

                _nameTxt.Text = p.ProductName ?? "";
                _descriptionTxt.Text = p.Description ?? "";
                _priceTxt.Text = p.UnitPrice.ToString("0.##");
                _durationTxt.Text = p.DurationMinutes.ToString();

                if (!string.IsNullOrWhiteSpace(p.Category) &&
                    _categoryCombo.Items.Contains(p.Category))
                {
                    _categoryCombo.SelectedItem = p.Category;
                }
                else if (!string.IsNullOrWhiteSpace(p.Category))
                {
                    _categoryCombo.Items.Add(p.Category);
                    _categoryCombo.SelectedItem = p.Category;
                }

                _activeCombo.SelectedItem = p.IsActive ? "Active" : "Inactive";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load service.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =================================================================
        //  VALIDATION
        // =================================================================
        private bool ValidateForm()
        {
            ClearFieldError(_nameTxt);
            ClearFieldError(_descriptionTxt);
            ClearFieldError(_priceTxt);
            ClearFieldError(_durationTxt);
            ClearFieldError(_categoryCombo);

            bool ok = true;

            // ---- Service Name ----
            var name = _nameTxt.Text.Trim();
            if (name.Length == 0)
            {
                MarkFieldError(_nameTxt, "Service name is required.");
                ok = false;
            }
            else if (name.Length < 2)
            {
                MarkFieldError(_nameTxt, "Service name must be at least 2 characters.");
                ok = false;
            }
            else if (name.Length > 150)
            {
                MarkFieldError(_nameTxt, "Service name must be 150 characters or fewer.");
                ok = false;
            }
            else if (Regex.IsMatch(name, @"\d"))
            {
                MarkFieldError(_nameTxt, "Service name can't contain numbers — letters only.");
                ok = false;
            }
            else if (!NameRegex.IsMatch(name))
            {
                MarkFieldError(_nameTxt, "Letters, spaces, and . , - ' & / ( ) only.");
                ok = false;
            }

            // ---- Description ----
            var desc = _descriptionTxt.Text.Trim();
            if (desc.Length > 1000)
            {
                MarkFieldError(_descriptionTxt, "Description must be 1000 characters or fewer.");
                ok = false;
            }
            else if (desc.Length > 0 && Regex.IsMatch(desc, @"\d"))
            {
                MarkFieldError(_descriptionTxt, "Description can't contain numbers — letters only.");
                ok = false;
            }
            else if (desc.Length > 0 && !DescriptionRegex.IsMatch(desc))
            {
                MarkFieldError(_descriptionTxt, "Description contains invalid characters.");
                ok = false;
            }

            // ---- Price ----
            var priceText = _priceTxt.Text.Trim();
            if (priceText.Length == 0)
            {
                MarkFieldError(_priceTxt, "Price is required.");
                ok = false;
            }
            else if (!decimal.TryParse(priceText, NumberStyles.Number,
                                       CultureInfo.InvariantCulture, out var price))
            {
                MarkFieldError(_priceTxt, "Price must be a number like 500 or 499.99.");
                ok = false;
            }
            else if (price <= 0)
            {
                MarkFieldError(_priceTxt, "Price must be greater than zero.");
                ok = false;
            }
            else if (price > 1_000_000m)
            {
                MarkFieldError(_priceTxt, "Price must be 1,000,000 or less.");
                ok = false;
            }

            // ---- Duration ----
            var durationText = _durationTxt.Text.Trim();
            if (durationText.Length == 0)
            {
                MarkFieldError(_durationTxt, "Duration is required.");
                ok = false;
            }
            else if (!int.TryParse(durationText, out var duration))
            {
                MarkFieldError(_durationTxt, "Duration must be a whole number (minutes).");
                ok = false;
            }
            else if (duration <= 0)
            {
                MarkFieldError(_durationTxt, "Duration must be at least 1 minute.");
                ok = false;
            }
            else if (duration > 1440)
            {
                MarkFieldError(_durationTxt, "Duration can't exceed 1440 minutes (24 hours).");
                ok = false;
            }

            // ---- Category ----
            var category = (string?)_categoryCombo.SelectedItem ?? "";
            if (string.IsNullOrWhiteSpace(category))
            {
                MarkFieldError(_categoryCombo, "Please pick a category.");
                ok = false;
            }
            else if (!CategoryRegex.IsMatch(category))
            {
                MarkFieldError(_categoryCombo, "Category must contain at least one letter.");
                ok = false;
            }

            return ok;
        }

        // =================================================================
        //  SAVE
        // =================================================================
        private async Task SaveAsync()
        {
            if (!ValidateForm())
            {
                foreach (var c in new Control[] { _nameTxt, _descriptionTxt, _priceTxt, _durationTxt, _categoryCombo })
                {
                    if (!string.IsNullOrEmpty(_errors.GetError(c)))
                    {
                        c.Focus();
                        break;
                    }
                }
                return;
            }

            var price = decimal.Parse(_priceTxt.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);
            var duration = int.Parse(_durationTxt.Text.Trim());
            var category = (string?)_categoryCombo.SelectedItem ?? "";

            bool isActive = _serviceId.HasValue
                ? string.Equals(_activeCombo.SelectedItem?.ToString(), "Active",
                                StringComparison.OrdinalIgnoreCase)
                : true;

            var dto = new
            {
                productCode = _serviceId.HasValue
                    ? $"SVC{_serviceId.Value:00000}"
                    : $"SVC{DateTime.Now:yyyyMMddHHmmss}",
                productName = _nameTxt.Text.Trim(),
                description = _descriptionTxt.Text.Trim(),
                unitPrice = price,
                durationMinutes = duration,
                category = category,
                isActive = isActive,
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