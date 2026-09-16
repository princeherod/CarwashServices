using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CarwashServices.Views
{
    public class ServiceRequestsView : UserControl
    {
        private Panel _contentPanel;
        private DataGridView _grid;
        private TextBox _searchBox;
        private Button _newRequestBtn;
        private Panel _chipBar;

        private HttpClient _http;

        // Raw data from API
        private List<ServiceRequestDto> _all = new();
        private List<TenantCustomerDto> _tenantCustomers = new();
        private List<ProductDto> _tenantProducts = new();
        private List<UserDto> _users = new();

        // Lookup-mapped (so the dialog still receives CustomerDto / ServiceDto)
        private List<CustomerDto> _customerLookup = new();
        private List<ServiceDto> _serviceLookup = new();

        private string _activeStatus = "All";
        private System.Windows.Forms.Timer _debounce;

        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);

        public ServiceRequestsView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            _http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5180/"),
                Timeout = TimeSpan.FromSeconds(10)
            };

            _debounce = new System.Windows.Forms.Timer { Interval = 300 };
            _debounce.Tick += (s, e) => { _debounce.Stop(); ApplyFilter(); };

            InitializeUI();
        }

        private void InitializeUI()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(40, 20, 40, 24)
            };
            Controls.Add(_contentPanel);

            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Service Requests",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            var header = new Panel
            {
                Location = new Point(0, 30),
                Height = 90,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(header);

            header.Controls.Add(new Label
            {
                Text = "Manage Service Requests",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            header.Controls.Add(new Label
            {
                Text = "SERVICE_REQUESTS — request_id · customer_id · service_id · assigned_staff_id · created_by · status · scheduled_date",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 45),
                AutoSize = true
            });

            _newRequestBtn = new Button
            {
                Text = "+  New Request",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(170, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _newRequestBtn.FlatAppearance.BorderSize = 0;
            _newRequestBtn.Click += (s, e) => OpenNewRequestDialog();
            header.Controls.Add(_newRequestBtn);

            _chipBar = new Panel
            {
                Location = new Point(0, 145),
                Height = 44,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            var searchWrap = new Panel
            {
                Location = new Point(0, 200),
                Height = 44,
                Width = 620,
                BackColor = Color.White,
                Padding = new Padding(14, 8, 14, 8)
            };
            _contentPanel.Controls.Add(searchWrap);

            _searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "🔍   Search customer, plate, service..."
            };
            _searchBox.TextChanged += (s, e) => { _debounce.Stop(); _debounce.Start(); };
            searchWrap.Controls.Add(_searchBox);

            var gridCard = new Panel
            {
                Location = new Point(0, 260),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _contentPanel.Controls.Add(gridCard);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(0xE1, 0xE7, 0xF0),
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xE8, 0xF0, 0xFE),
                    ForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0)
                },
                ColumnHeadersHeight = 50,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 72 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Navy,
                    SelectionBackColor = Color.FromArgb(0xE3, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(16, 0, 0, 0)
                },
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestId", HeaderText = "REQUEST_ID", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Customer",
                HeaderText = "CUSTOMER_ID",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Service",
                HeaderText = "SERVICE_ID",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scheduled", HeaderText = "SCHEDULED_DATE", Width = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Staff",
                HeaderText = "ASSIGNED_STAFF_ID",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 80
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedBy",
                HeaderText = "CREATED_BY",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 80
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "PRIORITY", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 130 });
            _grid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "Edit",
                HeaderText = "ACTIONS",
                Text = "Edit",
                UseColumnTextForButtonValue = true,
                Width = 80,
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

            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;
                header.Width = w;
                _newRequestBtn.Location = new Point(w - _newRequestBtn.Width - 40, 20);
                _chipBar.Width = w;
                gridCard.Width = w;
                gridCard.Height = h - 260;
            }
            _contentPanel.Resize += (s, e) => Relayout();

            Load += async (s, e) =>
            {
                Relayout();
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            };
        }

        private void BuildChips()
        {
            _chipBar.Controls.Clear();
            var statuses = new[] { "All", "Pending", "Assigned", "InProgress", "Completed", "Cancelled" };
            int x = 0;
            foreach (var st in statuses)
            {
                var chip = new Button
                {
                    Text = st,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(120, 38),
                    Location = new Point(x, 0),
                    Cursor = Cursors.Hand
                };
                bool active = st == _activeStatus;
                chip.BackColor = active ? Navy : Color.White;
                chip.ForeColor = active ? Color.White : Navy;
                chip.FlatAppearance.BorderColor = active ? Navy : Color.FromArgb(0xE1, 0xE7, 0xF0);
                chip.FlatAppearance.BorderSize = 1;
                var captured = st;
                chip.Click += (s, e) => { _activeStatus = captured; BuildChips(); ApplyFilter(); };
                _chipBar.Controls.Add(chip);
                x += 130;
            }
        }

        // ================================================================
        // LOOKUPS — now reads from TENANT tables (matches Manage Customers)
        // ================================================================
        private async Task LoadLookupsAsync()
        {
            // Customers from the tenant DB (the table Manage Customers writes to)
            try
            {
                _tenantCustomers = await _http.GetFromJsonAsync<List<TenantCustomerDto>>(
                    "api/tenant/1/tenant-customers") ?? new();

                _customerLookup = _tenantCustomers
                    .Select(c => new CustomerDto
                    {
                        CustomerId = c.TenantCustomerId,
                        FullName = c.CustomerName,
                        Phone = c.ContactNumber,
                        Email = c.EmailAddress,
                        Address = c.Address
                    })
                    .ToList();
            }
            catch
            {
                _tenantCustomers = new();
                _customerLookup = new();
            }

            // Services from the tenant products table
            try
            {
                _tenantProducts = await _http.GetFromJsonAsync<List<ProductDto>>(
                    "api/tenant/1/products") ?? new();

                _serviceLookup = _tenantProducts
                    .Select(p => new ServiceDto
                    {
                        ServiceId = p.ProductId,
                        ServiceName = p.ProductName,
                        Price = p.UnitPrice,
                        DurationMinutes = p.DurationMinutes
                    })
                    .ToList();
            }
            catch
            {
                _tenantProducts = new();
                _serviceLookup = new();
            }

            // Staff / users from the master DB
            try
            {
                _users = await _http.GetFromJsonAsync<List<UserDto>>("api/users") ?? new();
            }
            catch
            {
                _users = new();
            }
        }

        private async Task LoadRequestsAsync()
        {
            try
            {
                _newRequestBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                _all = await _http.GetFromJsonAsync<List<ServiceRequestDto>>("api/service-requests")
                       ?? new();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load service requests.\n\nMake sure the API is running.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _newRequestBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyFilter));
                return;
            }

            var search = _searchBox.Text?.Trim().ToLower() ?? "";

            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var r in _all)
            {
                if (_activeStatus != "All" &&
                    !string.Equals(r.Status, _activeStatus, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Customer lookup — from TenantCustomers via mapped CustomerDto
                var cust = _customerLookup.FirstOrDefault(c => c.CustomerId == r.CustomerId);
                var tenantCust = _tenantCustomers.FirstOrDefault(c => c.TenantCustomerId == r.CustomerId);

                // Service lookup — from tenant Products via mapped ServiceDto
                var svc = _serviceLookup.FirstOrDefault(s => s.ServiceId == r.ServiceId);

                var staff = r.AssignedStaffId.HasValue
                    ? _users.FirstOrDefault(u => u.UserId == r.AssignedStaffId.Value)
                    : null;
                var creator = _users.FirstOrDefault(u => u.UserId == r.CreatedBy);

                // ---- Customer cell: name + email + (plate / vehicle) ----
                string custText;
                if (tenantCust != null)
                {
                    custText = tenantCust.CustomerName;
                    if (!string.IsNullOrWhiteSpace(tenantCust.EmailAddress))
                        custText += $"\n{tenantCust.EmailAddress}";

                    var plateBlob = $"{tenantCust.VehicleYear} {tenantCust.VehicleMake} {tenantCust.VehicleModel}".Trim();
                    if (!string.IsNullOrWhiteSpace(tenantCust.PlateNumber))
                        custText += $"\n{tenantCust.PlateNumber}" +
                                    (string.IsNullOrWhiteSpace(plateBlob) ? "" : $" {plateBlob}");
                }
                else if (cust != null)
                {
                    custText = cust.FullName;
                    if (!string.IsNullOrWhiteSpace(cust.Email))
                        custText += $"\n{cust.Email}";
                }
                else
                {
                    custText = $"id:{r.CustomerId}";
                }

                // ---- Service cell: name + price ----
                var svcText = svc != null
                    ? $"{svc.ServiceName}\nid:{svc.ServiceId}  ·  ₱{svc.Price:N0}"
                    : $"id:{r.ServiceId}";

                // ---- Staff cell ----
                var staffText = staff != null
                    ? $"{staff.FullName}\nid:{staff.UserId}"
                    : (r.AssignedStaffId.HasValue ? $"id:{r.AssignedStaffId}" : "NULL — Unassigned");

                // ---- CreatedBy cell ----
                var creatorText = creator != null
                    ? $"{creator.FullName}\nid:{creator.UserId}"
                    : $"id:{r.CreatedBy}";

                if (!string.IsNullOrEmpty(search))
                {
                    var blob = $"{custText} {svcText} {staffText} {creatorText} {r.Status} {r.Priority}".ToLower();
                    if (!blob.Contains(search)) continue;
                }

                _grid.Rows.Add(
                    $"#{r.RequestId}",
                    custText,
                    svcText,
                    r.ScheduledDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    staffText,
                    creatorText,
                    r.Priority ?? "Normal",
                    r.Status,
                    "Edit");
            }

            _grid.ResumeLayout();
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Edit") return;

            var idText = _grid.Rows[e.RowIndex].Cells["RequestId"].Value?.ToString() ?? "";
            if (!int.TryParse(idText.TrimStart('#'), out var id)) return;

            OpenEditRequestDialog(id);
        }

        private async void OpenNewRequestDialog()
        {
            using var dlg = new ServiceRequestEditDialog(
                null, _customerLookup, _serviceLookup);

            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            }
        }

        private async void OpenEditRequestDialog(int id)
        {
            using var dlg = new ServiceRequestEditDialog(
                id, _customerLookup, _serviceLookup);

            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                await LoadLookupsAsync();
                await LoadRequestsAsync();
            }
        }
    }
}