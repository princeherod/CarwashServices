using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CarwashServices
{
    public class CustomerManagementForm : Form
    {
        // ---- Controls ----
        private Panel _headerPanel;
        private Label _titleLabel;
        private Panel _leftPanel;
        private Panel _rightPanel;

        private TextBox _firstNameTxt;
        private TextBox _lastNameTxt;
        private TextBox _emailTxt;
        private TextBox _phoneTxt;
        private TextBox _addressTxt;

        private Button _saveBtn;
        private Button _clearBtn;
        private Button _refreshBtn;

        private DataGridView _customerGrid;

        // ---- State ----
        private HttpClient _http;
        private int _selectedCustomerId = 0;

        public CustomerManagementForm()
        {
            InitializeForm();
            InitializeHttp();
        }

        private void InitializeHttp()
        {
            _http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };
        }

        private void InitializeForm()
        {
            // ---- Form ----
            Text = "Carwash Services CRM — Customer Data Collection";
            Size = new Size(1200, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.LightBlueBg;
            Font = Theme.BodyFont;
            MinimumSize = new Size(1000, 600);

            // ---- Header bar ----
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Theme.DarkBlue
            };

            _titleLabel = new Label
            {
                Text = "👥  CUSTOMER DATA COLLECTION",
                ForeColor = Theme.White,
                Font = Theme.TitleFont,
                AutoSize = true,
                Location = new Point(20, 16)
            };
            _headerPanel.Controls.Add(_titleLabel);

            // ---- Left panel (form) ----
            _leftPanel = new Panel
            {
                Location = new Point(20, 80),
                Size = new Size(360, 520),
                BackColor = Theme.White,
                Padding = new Padding(20),
                BorderStyle = BorderStyle.FixedSingle
            };

            var formTitle = new Label
            {
                Text = "New Customer",
                ForeColor = Theme.DarkBlue,
                Font = Theme.HeaderFont,
                Location = new Point(20, 20),
                AutoSize = true
            };
            _leftPanel.Controls.Add(formTitle);

            int y = 60;

            // First Name
            _leftPanel.Controls.Add(MakeLabel("FIRST NAME *", y));
            _firstNameTxt = MakeTextBox(y + 22, 300);
            _leftPanel.Controls.Add(_firstNameTxt);
            y += 70;

            // Last Name
            _leftPanel.Controls.Add(MakeLabel("LAST NAME *", y));
            _lastNameTxt = MakeTextBox(y + 22, 300);
            _leftPanel.Controls.Add(_lastNameTxt);
            y += 70;

            // Email
            _leftPanel.Controls.Add(MakeLabel("EMAIL", y));
            _emailTxt = MakeTextBox(y + 22, 300);
            _leftPanel.Controls.Add(_emailTxt);
            y += 70;

            // Phone
            _leftPanel.Controls.Add(MakeLabel("PHONE", y));
            _phoneTxt = MakeTextBox(y + 22, 300);
            _leftPanel.Controls.Add(_phoneTxt);
            y += 70;

            // Address
            _leftPanel.Controls.Add(MakeLabel("ADDRESS", y));
            _addressTxt = MakeTextBox(y + 22, 300);
            _leftPanel.Controls.Add(_addressTxt);
            y += 70;

            // Save button
            _saveBtn = MakeButton("💾 Save", 20, y, 140);
            Theme.StyleButton(_saveBtn, Theme.MediumBlue);
            _saveBtn.Click += async (s, e) => await SaveCustomerAsync();
            _leftPanel.Controls.Add(_saveBtn);

            // Clear button
            _clearBtn = MakeButton("🗑 Clear", 180, y, 140);
            Theme.StyleButton(_clearBtn, Color.FromArgb(0x60, 0x7D, 0x8B));
            _clearBtn.Click += (s, e) => ClearForm();
            _leftPanel.Controls.Add(_clearBtn);

            // ---- Right panel (grid) ----
            _rightPanel = new Panel
            {
                Location = new Point(400, 80),
                Size = new Size(760, 520),
                BackColor = Theme.White,
                Padding = new Padding(20),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            var listTitle = new Label
            {
                Text = "📋 Captured Customers",
                ForeColor = Theme.DarkBlue,
                Font = Theme.HeaderFont,
                Location = new Point(20, 20),
                AutoSize = true
            };
            _rightPanel.Controls.Add(listTitle);

            _refreshBtn = MakeButton("⟳ Refresh", 620, 16, 120);
            Theme.StyleButton(_refreshBtn, Theme.MediumBlue);
            _refreshBtn.Click += async (s, e) => await LoadCustomersAsync();
            _rightPanel.Controls.Add(_refreshBtn);

            _customerGrid = new DataGridView
            {
                Location = new Point(20, 60),
                Size = new Size(720, 430),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Theme.StyleGrid(_customerGrid);
            _customerGrid.Columns.Add("CustomerId", "ID");
            _customerGrid.Columns.Add("CustomerCode", "Code");
            _customerGrid.Columns.Add("CustomerName", "Name");
            _customerGrid.Columns.Add("ContactNumber", "Phone");
            _customerGrid.Columns.Add("EmailAddress", "Email");
            _customerGrid.Columns.Add("Address", "Address");
            _customerGrid.Columns.Add("IsActive", "Active");
            _customerGrid.CellClick += CustomerGrid_CellClick;
            _rightPanel.Controls.Add(_customerGrid);

            // ---- Add to form ----
            Controls.Add(_headerPanel);
            Controls.Add(_leftPanel);
            Controls.Add(_rightPanel);

            Load += async (s, e) => await LoadCustomersAsync();
        }

        // ---- Helper builders ----
        private Label MakeLabel(string text, int y) => new Label
        {
            Text = text,
            ForeColor = Theme.BodyText,
            Font = Theme.HeaderFont,
            Location = new Point(20, y),
            AutoSize = true
        };

        private TextBox MakeTextBox(int y, int width)
        {
            var tb = new TextBox { Location = new Point(20, y), Width = width };
            Theme.StyleTextBox(tb);
            return tb;
        }

        private Button MakeButton(string text, int x, int y, int width) => new Button
        {
            Text = text,
            Location = new Point(x, y),
            Width = width
        };

        // ---- Data operations ----
        private async Task LoadCustomersAsync()
        {
            try
            {
                var list = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers");

                _customerGrid.Rows.Clear();
                if (list == null) return;

                foreach (var c in list)
                {
                    _customerGrid.Rows.Add(
                        c.TenantCustomerId,
                        c.CustomerCode,
                        c.CustomerName,
                        c.ContactNumber,
                        c.EmailAddress,
                        c.Address,
                        c.IsActive ? "Yes" : "No");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customers.\n\n" +
                    $"Make sure the API is running on http://localhost:5180\n\n" +
                    $"{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(_firstNameTxt.Text) ||
                string.IsNullOrWhiteSpace(_lastNameTxt.Text))
            {
                MessageBox.Show("First name and last name are required.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dto = new
            {
                customerCode = $"CUST{DateTime.Now:yyyyMMddHHmmss}",
                customerName = $"{_firstNameTxt.Text.Trim()} {_lastNameTxt.Text.Trim()}",
                contactNumber = _phoneTxt.Text.Trim(),
                emailAddress = _emailTxt.Text.Trim(),
                address = _addressTxt.Text.Trim(),
                isActive = true,
                createdAt = DateTime.UtcNow
            };

            try
            {
                var resp = await _http.PostAsJsonAsync("api/tenant/1/tenant-customers", dto);

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Customer saved.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ClearForm();
                    await LoadCustomersAsync();
                }
                else
                {
                    MessageBox.Show($"Save failed: {resp.StatusCode}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Save failed.\n\n" +
                    $"Make sure the API is running on http://localhost:5180\n\n" +
                    $"{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CustomerGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = _customerGrid.Rows[e.RowIndex];
            _selectedCustomerId = Convert.ToInt32(row.Cells[0].Value);
            var fullName = row.Cells[2].Value?.ToString() ?? "";

            var parts = fullName.Split(' ', 2);
            _firstNameTxt.Text = parts.Length > 0 ? parts[0] : "";
            _lastNameTxt.Text = parts.Length > 1 ? parts[1] : "";
            _emailTxt.Text = row.Cells[4].Value?.ToString() ?? "";
            _phoneTxt.Text = row.Cells[3].Value?.ToString() ?? "";
            _addressTxt.Text = row.Cells[5].Value?.ToString() ?? "";
        }

        private void ClearForm()
        {
            _selectedCustomerId = 0;
            _firstNameTxt.Clear();
            _lastNameTxt.Clear();
            _emailTxt.Clear();
            _phoneTxt.Clear();
            _addressTxt.Clear();
            _customerGrid.ClearSelection();
            _firstNameTxt.Focus();
        }
    }

    // ---- DTO for API responses ----
    public class TenantCustomerDto
    {
        public int TenantCustomerId { get; set; }
        public string CustomerCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string? ContactNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}