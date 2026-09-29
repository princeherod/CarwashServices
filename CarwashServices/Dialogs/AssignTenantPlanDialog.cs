using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    public class AssignTenantPlanDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);

        private readonly CustomerSubscriptionItemDto _companyItem;
        private List<SubscriptionPlanItemDto> _availablePlans = new();

        private Label _companyLbl = null!;
        private ComboBox _planCombo = null!;
        private ComboBox _statusCombo = null!;
        private DateTimePicker _startPicker = null!;
        private DateTimePicker _renewalPicker = null!;
        private CheckBox _autoRenewCheck = null!;
        private Label _errorLbl = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        public AssignTenantPlanDialog(CustomerSubscriptionItemDto companyItem)
        {
            _companyItem = companyItem;

            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadPlansAsync();
        }

        private static Label Caption(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private void InitializeForm()
        {
            Text = $"Assign Subscription Plan — {_companyItem.TenantCompany}";
            ClientSize = new Size(540, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 16)
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = "Assign Subscription Plan",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(32, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(titleLbl);

            var subtitleLbl = new Label
            {
                Text = $"Assign or update the CRM plan for {_companyItem.TenantCompany} ({_companyItem.CompanyCode}).",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(32, 42),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(subtitleLbl);
            Controls.Add(header);

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White,
                Padding = new Padding(24, 16, 24, 16)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                Location = new Point(32, 12),
                Size = new Size(250, 52),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            footer.Controls.Add(_errorLbl);

            _saveBtn = new Button
            {
                Text = "Assign Plan",
                Font = new Font("Segoe UI Semibold", 9.5f),
                Size = new Size(130, 40),
                Location = new Point(footer.ClientSize.Width - 130 - 32, 18),
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.FlatAppearance.MouseOverBackColor = AccentHover;
            _saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(_saveBtn);

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9.5f),
                Size = new Size(90, 40),
                Location = new Point(footer.ClientSize.Width - 130 - 32 - 100, 18),
                BackColor = Color.White,
                ForeColor = Muted,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            _cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;
            footer.Controls.Add(_cancelBtn);
            Controls.Add(footer);

            // ---- Body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 16),
                AutoScroll = true
            };
            Controls.Add(body);
            body.BringToFront();

            int left = 32;
            int w = 476;
            int y = 16;

            // Company Label
            body.Controls.Add(Caption("TENANT COMPANY", left, y));
            y += 20;
            _companyLbl = new Label
            {
                Text = $"{_companyItem.TenantCompany} ({_companyItem.CompanyCode})",
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = Navy,
                Location = new Point(left, y),
                AutoSize = true
            };
            body.Controls.Add(_companyLbl);
            y += 34;

            // Plan Dropdown
            body.Controls.Add(Caption("SUBSCRIPTION PLAN", left, y));
            y += 20;
            _planCombo = new ComboBox
            {
                Location = new Point(left, y),
                Width = w,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_planCombo);
            y += 36;

            // Status Dropdown
            body.Controls.Add(Caption("STATUS", left, y));
            y += 20;
            _statusCombo = new ComboBox
            {
                Location = new Point(left, y),
                Width = w,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            _statusCombo.Items.AddRange(new object[] { "Active", "Inactive" });
            _statusCombo.SelectedIndex = 0;
            body.Controls.Add(_statusCombo);
            y += 36;

            // Dates (2 cols)
            int colW = (w - 16) / 2;
            body.Controls.Add(Caption("START DATE", left, y));
            body.Controls.Add(Caption("RENEWAL DATE", left + colW + 16, y));
            y += 20;

            _startPicker = new DateTimePicker
            {
                Location = new Point(left, y),
                Width = colW,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 9.5f)
            };
            _renewalPicker = new DateTimePicker
            {
                Location = new Point(left + colW + 16, y),
                Width = colW,
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(1),
                Font = new Font("Segoe UI", 9.5f)
            };

            _startPicker.ValueChanged += (s, e) =>
            {
                _renewalPicker.Value = _startPicker.Value.AddMonths(1);
            };

            body.Controls.Add(_startPicker);
            body.Controls.Add(_renewalPicker);
            y += 36;

            // Auto-renew Checkbox
            _autoRenewCheck = new CheckBox
            {
                Text = "Auto-renew subscription upon renewal date",
                Location = new Point(left, y),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Navy
            };
            body.Controls.Add(_autoRenewCheck);
        }

        private async Task LoadPlansAsync()
        {
            try
            {
                _availablePlans = await _http.GetFromJsonAsync<List<SubscriptionPlanItemDto>>("api/billing/plans?activeOnly=true")
                                  ?? new List<SubscriptionPlanItemDto>();

                _planCombo.Items.Clear();
                foreach (var p in _availablePlans)
                {
                    _planCombo.Items.Add($"{p.PlanName} ({p.PriceFormatted} · Duration: {p.BillingCycle})");
                }

                if (_planCombo.Items.Count > 0)
                {
                    // If company already had a plan matching, select it, else 0
                    int matchIdx = _availablePlans.FindIndex(p => p.PlanId == _companyItem.PlanId);
                    _planCombo.SelectedIndex = matchIdx >= 0 ? matchIdx : 0;
                }
                else
                {
                    _planCombo.Items.Add("No active plans available");
                    _planCombo.SelectedIndex = 0;
                    _saveBtn.Enabled = false;
                    _errorLbl.Text = "No plans found. Please create a plan first.";
                }
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Failed to load plans: {ex.Message}";
            }
        }

        private async Task SaveAsync()
        {
            _errorLbl.Text = "";

            if (_availablePlans.Count == 0 || _planCombo.SelectedIndex < 0 || _planCombo.SelectedIndex >= _availablePlans.Count)
            {
                _errorLbl.Text = "Please select a valid plan.";
                return;
            }

            var selectedPlan = _availablePlans[_planCombo.SelectedIndex];

            _saveBtn.Enabled = false;
            _saveBtn.Text = "Assigning...";

            try
            {
                var payload = new
                {
                    companyId = _companyItem.CompanyId,
                    planId = selectedPlan.PlanId,
                    status = _statusCombo.SelectedItem?.ToString() ?? "Active",
                    startDate = _startPicker.Value.Date,
                    renewalDate = _renewalPicker.Value.Date,
                    autoRenew = _autoRenewCheck.Checked
                };

                var response = await _http.PostAsJsonAsync("api/billing/assign-plan", payload);
                if (response.IsSuccessStatusCode)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    _errorLbl.Text = string.IsNullOrWhiteSpace(msg) ? "Failed to assign plan." : msg;
                }
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _saveBtn.Enabled = true;
                _saveBtn.Text = "Assign Plan";
            }
        }
    }
}
