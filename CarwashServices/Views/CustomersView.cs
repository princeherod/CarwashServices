using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CarwashServices.Views
{
    public class CustomersView : UserControl
    {
        private Panel _contentPanel;
        private TextBox _searchBox;
        private Button _newCustomerBtn;
        private DataGridView _grid;

        private HttpClient _http;
        private List<TenantCustomerDto> _allCustomers = new();
        private System.Windows.Forms.Timer _searchDebounceTimer;

        public CustomersView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            Font = new Font("Segoe UI", 9.5f);

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5180/"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                ApplyFilter();
            };

            InitializeUI();
        }

        private void InitializeUI()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0xF5, 0xF7, 0xFA),
                Padding = new Padding(30, 20, 30, 20)
            };
            Controls.Add(_contentPanel);

            // Breadcrumb
            var breadcrumb = new Label
            {
                Text = "Modules  ›  Manage Customers",
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(30, 18),
                AutoSize = true
            };
            _contentPanel.Controls.Add(breadcrumb);

            var dateLbl = new Label
            {
                Text = DateTime.Now.ToString("MMM d, yyyy"),
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(_contentPanel.Width - 220, 22),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(dateLbl);

            var pageTitle = new Label
            {
                Text = "Manage Customers",
                ForeColor = Color.FromArgb(0x0A, 0x14, 0x28),
                Font = new Font("Segoe UI Semibold", 20f),
                Location = new Point(30, 70),
                AutoSize = true
            };
            _contentPanel.Controls.Add(pageTitle);

            var subtitle = new Label
            {
                Text = "CUSTOMERS table — customer_id · full_name · phone · email · address · created_at",
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(30, 110),
                AutoSize = true
            };
            _contentPanel.Controls.Add(subtitle);

            _newCustomerBtn = new Button
            {
                Text = "+ New Customer",
                BackColor = Color.FromArgb(0x0A, 0x14, 0x28),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(170, 44),
                Location = new Point(_contentPanel.Width - 220, 75),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _newCustomerBtn.FlatAppearance.BorderSize = 0;
            _newCustomerBtn.Click += NewCustomerBtn_Click;
            _contentPanel.Controls.Add(_newCustomerBtn);

            _searchBox = new TextBox
            {
                Location = new Point(30, 155),
                Width = 600,
                Height = 40,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search full_name, plate_number, email..."
            };
            _contentPanel.Controls.Add(_searchBox);
            _searchBox.TextChanged += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            };

            var gridCard = new Panel
            {
                Location = new Point(30, 215),
                Size = new Size(_contentPanel.Width - 60, _contentPanel.Height - 250),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _contentPanel.Controls.Add(gridCard);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(0xE5, 0xE8, 0xEE),
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xF5, 0xF7, 0xFA),
                    ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(12, 0, 0, 0)
                },
                ColumnHeadersHeight = 44,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 64 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Color.FromArgb(0x0A, 0x14, 0x28),
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Color.FromArgb(0x0A, 0x14, 0x28),
                    Padding = new Padding(12, 0, 0, 0)
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerId", HeaderText = "CUSTOMER_ID", FillWeight = 8 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "FULL_NAME / CONTACT", FillWeight = 30 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Plate", HeaderText = "PLATE_NUMBER / VEHICLE", FillWeight = 28 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "SOURCE", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", HeaderText = "CREATED_AT", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Edit",
                HeaderText = "ACTIONS",
                Text = "Edit",
                UseColumnTextForButtonValue = true,
                FillWeight = 8,
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(0x1E, 0x88, 0xE5),
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Color.FromArgb(0x1E, 0x88, 0xE5),
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI Semibold", 9f)
                }
            });

            _grid.CellContentClick += Grid_CellContentClick;
            gridCard.Controls.Add(_grid);

            Load += async (s, e) => await LoadCustomersAsync();
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                _newCustomerBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var list = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers");

                _allCustomers = list ?? new List<TenantCustomerDto>();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load customers.\n\n" +
                    $"Make sure the API is running on http://localhost:5180\n\n" +
                    $"{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newCustomerBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            var search = _searchBox.Text?.Trim().ToLower() ?? "";

            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var c in _allCustomers)
            {
                if (!string.IsNullOrEmpty(search) &&
                    !(c.CustomerName?.ToLower().Contains(search) ?? false) &&
                    !(c.EmailAddress?.ToLower().Contains(search) ?? false) &&
                    !(c.ContactNumber?.ToLower().Contains(search) ?? false) &&
                    !(c.PlateNumber?.ToLower().Contains(search) ?? false))
                    continue;

                var nameCell = c.CustomerName ?? "";
                if (!string.IsNullOrWhiteSpace(c.EmailAddress) ||
                    !string.IsNullOrWhiteSpace(c.ContactNumber))
                {
                    var contact = !string.IsNullOrWhiteSpace(c.EmailAddress)
                        ? c.EmailAddress
                        : c.ContactNumber;
                    nameCell += $"\n{contact}";
                }

                var plateCell = "";
                if (!string.IsNullOrWhiteSpace(c.PlateNumber))
                {
                    plateCell = c.PlateNumber;
                    var vehicleDesc = $"{c.VehicleYear} {c.VehicleMake} {c.VehicleModel}".Trim();
                    if (!string.IsNullOrWhiteSpace(vehicleDesc))
                        plateCell += $"\n{vehicleDesc}";
                }

                _grid.Rows.Add(
                    c.TenantCustomerId,
                    nameCell,
                    plateCell,
                    c.Source ?? "",
                    c.CreatedAt.ToString("yyyy-MM-dd"),
                    "Edit");
            }

            _grid.ResumeLayout();
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Edit") return;

            var id = Convert.ToInt32(_grid.Rows[e.RowIndex].Cells["CustomerId"].Value);
            EditCustomerAsync(id);
        }

        private async void NewCustomerBtn_Click(object? sender, EventArgs e)
        {
            using var dlg = new CustomerEditDialog(null);
            dlg.ShowDialog(this.FindForm());
            await LoadCustomersAsync();
        }

        private async void EditCustomerAsync(int customerId)
        {
            using var dlg = new CustomerEditDialog(customerId);
            dlg.ShowDialog(this.FindForm());
            await LoadCustomersAsync();
        }
    }
}