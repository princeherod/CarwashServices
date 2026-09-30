using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    public class CreateTenantPlanDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);

        private readonly SubscriptionPlanItemDto? _existingPlan;

        private Label _titleLbl = null!;
        private Label _subtitleLbl = null!;
        private TextBox _nameTxt = null!;
        private TextBox _descTxt = null!;
        private TextBox _priceTxt = null!;
        private ComboBox _cycleCombo = null!;
        private NumericUpDown _usersNum = null!;
        private NumericUpDown _customersNum = null!;
        private ComboBox? _statusCombo;
        private CheckBox _multiBranchChk = null!;
        private Label _errorLbl = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        public CreateTenantPlanDialog() : this(null)
        {
        }

        public CreateTenantPlanDialog(SubscriptionPlanItemDto? existingPlan)
        {
            _existingPlan = existingPlan;
            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);

            if (_existingPlan != null)
            {
                Text = "Edit Tenant Subscription Plan";
                _titleLbl.Text = "Edit Subscription Plan";
                _subtitleLbl.Text = $"Update configuration for {_existingPlan.PlanName}.";
                _saveBtn.Text = "Save Changes";

                _nameTxt.Text = _existingPlan.PlanName;
                _descTxt.Text = _existingPlan.Description;
                _priceTxt.Text = _existingPlan.Price.ToString("0.##");
                int cycleIdx = _cycleCombo.Items.IndexOf(_existingPlan.BillingCycle);
                if (cycleIdx >= 0) _cycleCombo.SelectedIndex = cycleIdx;
                _usersNum.Value = Math.Max(1, Math.Min(1000, _existingPlan.MaxUsers));
                _customersNum.Value = Math.Max(1, Math.Min(100000, _existingPlan.MaxCustomers));
                if (_statusCombo != null) _statusCombo.SelectedIndex = _existingPlan.IsActive ? 0 : 1;
                _multiBranchChk.Checked = _existingPlan.MultiBranchEnabled;
            }
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
            Text = "Create Tenant Subscription Plan";
            ClientSize = new Size(560, _existingPlan != null ? 560 : 500);
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

            _titleLbl = new Label
            {
                Text = "New Subscription Plan",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(32, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(_titleLbl);

            _subtitleLbl = new Label
            {
                Text = "Configure a CRM subscription plan for tenant companies.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(32, 42),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(_subtitleLbl);
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
                Size = new Size(260, 52),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            footer.Controls.Add(_errorLbl);

            _saveBtn = new Button
            {
                Text = "Create Plan",
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
            int w = 496;
            int y = 16;

            // Plan Name
            body.Controls.Add(Caption("PLAN NAME", left, y));
            y += 20;
            _nameTxt = new TextBox { Location = new Point(left, y), Width = w, Font = new Font("Segoe UI", 10f) };
            body.Controls.Add(_nameTxt);
            y += 36;

            // Description
            body.Controls.Add(Caption("DESCRIPTION", left, y));
            y += 20;
            _descTxt = new TextBox { Location = new Point(left, y), Width = w, Height = 56, Multiline = true, Font = new Font("Segoe UI", 9.5f) };
            body.Controls.Add(_descTxt);
            y += 66;

            // Price & Duration (2 cols)
            int colW = (w - 16) / 2;
            body.Controls.Add(Caption("PRICE (₱)", left, y));
            body.Controls.Add(Caption("DURATION", left + colW + 16, y));
            y += 20;

            _priceTxt = new TextBox { Location = new Point(left, y), Width = colW, Font = new Font("Segoe UI", 10f), Text = "1999" };
            _cycleCombo = new ComboBox
            {
                Location = new Point(left + colW + 16, y),
                Width = colW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            _cycleCombo.Items.AddRange(new object[] { "Monthly", "Quarterly", "Annual" });
            _cycleCombo.SelectedIndex = 0;

            body.Controls.Add(_priceTxt);
            body.Controls.Add(_cycleCombo);
            y += 36;

            // Max Users & Max Customers (2 cols)
            body.Controls.Add(Caption("MAX USERS", left, y));
            body.Controls.Add(Caption("MAX CUSTOMERS", left + colW + 16, y));
            y += 20;

            _usersNum = new NumericUpDown
            {
                Location = new Point(left, y),
                Width = colW,
                Minimum = 1,
                Maximum = 1000,
                Value = 10,
                Font = new Font("Segoe UI", 9.5f)
            };
            _customersNum = new NumericUpDown
            {
                Location = new Point(left + colW + 16, y),
                Width = colW,
                Minimum = 1,
                Maximum = 100000,
                Value = 500,
                Font = new Font("Segoe UI", 9.5f)
            };
            body.Controls.Add(_usersNum);
            body.Controls.Add(_customersNum);
            y += 36;

            // Status & Multi-Branch
            if (_existingPlan != null)
            {
                body.Controls.Add(Caption("STATUS", left, y));
                body.Controls.Add(Caption("MULTI-BRANCH", left + colW + 16, y));
                y += 20;

                _statusCombo = new ComboBox
                {
                    Location = new Point(left, y),
                    Width = colW,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 9.5f)
                };
                _statusCombo.Items.AddRange(new object[] { "Active", "Inactive" });
                _statusCombo.SelectedIndex = 0;
                body.Controls.Add(_statusCombo);

                _multiBranchChk = new CheckBox
                {
                    Text = "Enable Multi-Branch",
                    Location = new Point(left + colW + 16, y + 2),
                    AutoSize = true,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    ForeColor = Navy
                };
                body.Controls.Add(_multiBranchChk);
            }
            else
            {
                body.Controls.Add(Caption("MULTI-BRANCH", left, y));
                y += 20;

                _multiBranchChk = new CheckBox
                {
                    Text = "Enable Multi-Branch support for this subscription tier",
                    Location = new Point(left, y + 2),
                    AutoSize = true,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    ForeColor = Navy
                };
                body.Controls.Add(_multiBranchChk);
            }
        }

        public void SetInitialValues(string name, string desc, decimal price, string cycle, int maxUsers, int maxCustomers, bool active, bool multiBranch = false)
        {
            _nameTxt.Text = name;
            _descTxt.Text = desc;
            _priceTxt.Text = price.ToString("0.##");
            int cycleIdx = _cycleCombo.Items.IndexOf(cycle);
            if (cycleIdx >= 0) _cycleCombo.SelectedIndex = cycleIdx;
            _usersNum.Value = Math.Max(1, maxUsers);
            _customersNum.Value = Math.Max(1, maxCustomers);
            if (_statusCombo != null) _statusCombo.SelectedIndex = active ? 0 : 1;
            _multiBranchChk.Checked = multiBranch;
        }

        private async Task SaveAsync()
        {
            _errorLbl.Text = "";

            var name = _nameTxt.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                _errorLbl.Text = "Plan name is required.";
                _nameTxt.Focus();
                return;
            }

            if (!decimal.TryParse(_priceTxt.Text.Trim(), out var price) || price < 0)
            {
                _errorLbl.Text = "Please enter a valid non-negative price.";
                _priceTxt.Focus();
                return;
            }

            bool isEditing = _existingPlan != null;
            _saveBtn.Enabled = false;
            _saveBtn.Text = isEditing ? "Saving..." : "Creating...";

            try
            {
                var payload = new
                {
                    planName = name,
                    description = _descTxt.Text.Trim(),
                    price = price,
                    billingCycle = _cycleCombo.SelectedItem?.ToString() ?? "Monthly",
                    maxUsers = (int)_usersNum.Value,
                    maxCustomers = (int)_customersNum.Value,
                    multiBranchEnabled = _multiBranchChk.Checked,
                    isActive = _statusCombo != null ? (_statusCombo.SelectedIndex == 0) : true
                };

                HttpResponseMessage response;
                if (isEditing)
                {
                    response = await _http.PutAsJsonAsync($"api/billing/plans/{_existingPlan!.PlanId}", payload);
                }
                else
                {
                    response = await _http.PostAsJsonAsync("api/billing/plans", payload);
                }

                if (response.IsSuccessStatusCode)
                {
                    if (isEditing)
                    {
                        MessageBox.Show($"Subscription plan '{name}' has been updated successfully.", "Plan Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Subscription plan '{name}' has been created successfully.", "Plan Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    _errorLbl.Text = string.IsNullOrWhiteSpace(msg) ? (isEditing ? "Failed to update plan." : "Failed to create plan.") : msg;
                }
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _saveBtn.Enabled = true;
                _saveBtn.Text = isEditing ? "Save Changes" : "Create Plan";
            }
        }
    }
}
