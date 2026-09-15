using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CarwashServices.Views
{
    public class ServicesView : UserControl
    {
        private Panel _contentPanel;
        private DataGridView _grid;
        private TextBox _searchBox;
        private Button _addServiceBtn;
        private Panel _chipBar;

        private HttpClient _http;
        private List<ProductDto> _allServices = new();
        private string _activeCategory = "All";
        private System.Windows.Forms.Timer _searchDebounceTimer;

        public ServicesView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA);
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
                BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA),
                Padding = new Padding(40, 20, 40, 24)
            };
            Controls.Add(_contentPanel);

            // Breadcrumb
            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Services",
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            var dateLbl = new Label
            {
                Text = DateTime.Now.ToString("MMM d, yyyy"),
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(dateLbl);

            // Page header
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
                Text = "Manage Services",
                ForeColor = Color.FromArgb(0x0A, 0x16, 0x33),
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            header.Controls.Add(new Label
            {
                Text = "SERVICES — service_id · service_name · description · price · duration_minutes",
                ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 45),
                AutoSize = true
            });

            _addServiceBtn = new Button
            {
                Text = "+  Add Service",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(170, 44),
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(0x0D, 0x47, 0xA1),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _addServiceBtn.FlatAppearance.BorderSize = 0;
            _addServiceBtn.Click += (s, e) => OpenAddServiceDialog();
            header.Controls.Add(_addServiceBtn);

            // Filter chips
            _chipBar = new Panel
            {
                Location = new Point(0, 145),
                Height = 44,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            // Search box
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
                ForeColor = Color.FromArgb(0x0A, 0x16, 0x33),
                PlaceholderText = "🔍   Search service name, description, category..."
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            };
            searchWrap.Controls.Add(_searchBox);

            // Grid card
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
                    ForeColor = Color.FromArgb(0x6B, 0x7A, 0x9A),
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0),
                    SelectionBackColor = Color.FromArgb(0xE8, 0xF0, 0xFE)
                },
                ColumnHeadersHeight = 50,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 72 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Color.FromArgb(0x0A, 0x16, 0x33),
                    SelectionBackColor = Color.FromArgb(0xE3, 0xF2, 0xFD),
                    SelectionForeColor = Color.FromArgb(0x0A, 0x16, 0x33),
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ServiceId", HeaderText = "SERVICE_ID", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "SERVICE_NAME / DESCRIPTION",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "PRICE", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Duration", HeaderText = "DURATION", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "CATEGORY", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", HeaderText = "IS_ACTIVE", Width = 110 });

            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var id = Convert.ToInt32(_grid.Rows[e.RowIndex].Cells["ServiceId"].Value);
                EditServiceAsync(id);
            };

            gridCard.Controls.Add(_grid);

            // Relayout
            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;

                dateLbl.Location = new Point(w - dateLbl.Width - 40, 0);
                header.Width = w;
                _addServiceBtn.Location = new Point(w - _addServiceBtn.Width - 40, 20);
                _chipBar.Width = w;
                gridCard.Width = w;
                gridCard.Height = h - 260;
            }

            _contentPanel.Resize += (s, e) => Relayout();
            Load += async (s, e) =>
            {
                Relayout();
                await LoadServicesAsync();
            };
        }

        private void BuildChips()
        {
            _chipBar.Controls.Clear();

            var categories = new[] { "All", "Exterior", "Interior", "Full Service", "Specialty" };
            int x = 0;

            foreach (var cat in categories)
            {
                var chip = new Button
                {
                    Text = cat,
                    Font = new Font("Segoe UI Semibold", 9.5f),
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(120, 38),
                    Location = new Point(x, 0),
                    Cursor = Cursors.Hand
                };

                bool isActive = cat == _activeCategory;
                chip.BackColor = isActive ? Color.FromArgb(0x0A, 0x16, 0x33) : Color.White;
                chip.ForeColor = isActive ? Color.White : Color.FromArgb(0x0A, 0x16, 0x33);
                chip.FlatAppearance.BorderColor = isActive ? Color.FromArgb(0x0A, 0x16, 0x33) : Color.FromArgb(0xE1, 0xE7, 0xF0);
                chip.FlatAppearance.BorderSize = 1;

                var captured = cat;
                chip.Click += (s, e) =>
                {
                    _activeCategory = captured;
                    BuildChips();
                    ApplyFilter();
                };

                _chipBar.Controls.Add(chip);
                x += 130;
            }
        }

        private async Task LoadServicesAsync()
        {
            try
            {
                _addServiceBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var list = await _http.GetFromJsonAsync<List<ProductDto>>("api/tenant/1/products");
                _allServices = list ?? new List<ProductDto>();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load services.\n\nMake sure the API is running.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _addServiceBtn.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            var search = _searchBox.Text?.Trim().ToLower() ?? "";

            _grid.SuspendLayout();
            _grid.Rows.Clear();

            foreach (var s in _allServices)
            {
                if (_activeCategory != "All" &&
                    !string.Equals(s.Category, _activeCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(search) &&
                    !(s.ProductName?.ToLower().Contains(search) ?? false) &&
                    !(s.Description?.ToLower().Contains(search) ?? false) &&
                    !(s.Category?.ToLower().Contains(search) ?? false))
                    continue;

                var nameCell = s.ProductName ?? "";
                if (!string.IsNullOrWhiteSpace(s.Description))
                    nameCell += $"\n{s.Description}";

                _grid.Rows.Add(
                    s.ProductId,
                    nameCell,
                    $"₱{s.UnitPrice:N0}",
                    $"{s.DurationMinutes} min",
                    s.Category ?? "",
                    s.IsActive ? "true" : "false");
            }

            _grid.ResumeLayout();
        }

        private async void OpenAddServiceDialog()
        {
            using var dlg = new ServiceEditDialog(null);
            dlg.ShowDialog(this.FindForm());
            await LoadServicesAsync();
        }

        private async void EditServiceAsync(int serviceId)
        {
            using var dlg = new ServiceEditDialog(serviceId);
            dlg.ShowDialog(this.FindForm());
            await LoadServicesAsync();
        }
    }
}