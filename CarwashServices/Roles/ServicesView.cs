using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dialogs;
using CarwashServices.Dtos;

namespace CarwashServices.Roles
{
    public class ServicesView : UserControl
    {
        // -----------------------------------------------------------------
        //  Palette (aligned with CustomersView)
        // -----------------------------------------------------------------
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color PageBg = Color.FromArgb(0xF5, 0xF7, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE5, 0xE8, 0xEE);
        private static readonly Color HeaderBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x43);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);

        // Fonts used in the grid painter (created once, not on every paint)
        private static readonly Font NameFont = new Font("Segoe UI Semibold", 10f);
        private static readonly Font DescFont = new Font("Segoe UI", 8.5f);
        private static readonly Font StatusFont = new Font("Segoe UI Semibold", 9.5f);

        // -----------------------------------------------------------------
        //  State
        // -----------------------------------------------------------------
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private List<ProductDto> _allServices = new();
        private string _activeCategory = "All";
        private readonly System.Windows.Forms.Timer _searchDebounce = new() { Interval = 300 };

        private Panel _root = null!;          // padded outer container
        private Panel _contentPanel = null!;  // docked inner container (children are laid out inside it)
        private DataGridView _grid = null!;
        private TextBox _searchBox = null!;
        private Button _addBtn = null!;
        private Panel _chipBar = null!;

        // -----------------------------------------------------------------
        //  Ctor
        // -----------------------------------------------------------------
        public ServicesView()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            _searchDebounce.Tick += (s, e) => { _searchDebounce.Stop(); ApplyFilter(); };

            InitializeUI();

            Load += async (s, e) => await LoadServicesAsync();
        }

        // -----------------------------------------------------------------
        //  UI
        // -----------------------------------------------------------------
        private void InitializeUI()
        {
            // The padding must live on a container whose child is DOCKED,
            // otherwise absolutely-positioned children ignore it.
            _root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(30, 20, 30, 20)
            };
            Controls.Add(_root);

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };
            _root.Controls.Add(_contentPanel);

            // ---- Breadcrumb ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Modules  ›  Manage Services",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 0),
                AutoSize = true
            });

            // ---- Header block (title + subtitle) ----
            _contentPanel.Controls.Add(new Label
            {
                Text = "Manage Services",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 30),
                AutoSize = true
            });

            _contentPanel.Controls.Add(new Label
            {
                Text = "SERVICES — service_id · service_name · description · price · duration_minutes",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(0, 82),          // moved down so it no longer overlaps the title
                AutoSize = true
            });

            // ---- Add Service button ----
            _addBtn = new Button
            {
                Text = "+  Add Service",
                Font = new Font("Segoe UI Semibold", 10f),
                Size = new Size(170, 44),
                Cursor = Cursors.Hand,
                BackColor = Navy,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _addBtn.FlatAppearance.BorderSize = 0;
            _addBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x16, 0x2A, 0x5C);
            _addBtn.Click += (s, e) => OpenAddServiceDialog();
            _contentPanel.Controls.Add(_addBtn);

            // ---- Category chips ----
            _chipBar = new Panel
            {
                Location = new Point(0, 118),
                Height = 40,
                BackColor = PageBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentPanel.Controls.Add(_chipBar);
            BuildChips();

            // ---- Filter card (same look as Manage Customers) ----
            var filterCard = new Panel
            {
                BackColor = Color.White,
                Location = new Point(0, 168),
                Height = 76
            };
            filterCard.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, filterCard.Width - 1, filterCard.Height - 1);
            };
            _contentPanel.Controls.Add(filterCard);

            filterCard.Controls.Add(new Label
            {
                Text = "Search",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(16, 8),
                AutoSize = true
            });

            // Bordered wrapper + borderless TextBox = a clearly visible search field
            var searchWrap = new Panel
            {
                Location = new Point(16, 28),
                Size = new Size(400, 36),
                BackColor = Color.White,
                Padding = new Padding(10, 7, 10, 0)
            };
            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "Search by service name, description or category",
                Dock = DockStyle.Top
            };
            searchWrap.Controls.Add(_searchBox);

            bool focused = false;
            _searchBox.Enter += (s, e) => { focused = true; searchWrap.Invalidate(); };
            _searchBox.Leave += (s, e) => { focused = false; searchWrap.Invalidate(); };
            searchWrap.Resize += (s, e) => searchWrap.Invalidate();
            searchWrap.Paint += (s, e) =>
            {
                using var pen = new Pen(focused ? Accent : Faint);
                e.Graphics.DrawRectangle(pen, 0, 0, searchWrap.Width - 1, searchWrap.Height - 1);
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _searchDebounce.Stop();
                _searchDebounce.Start();
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _searchDebounce.Stop();
                    ApplyFilter();
                }
            };
            filterCard.Controls.Add(searchWrap);

            var searchBtn = new Button
            {
                Text = "Search",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                BackColor = Navy,
                ForeColor = Color.White,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            searchBtn.FlatAppearance.BorderSize = 0;
            searchBtn.Click += (s, e) => ApplyFilter();
            filterCard.Controls.Add(searchBtn);

            var refreshBtn = new Button
            {
                Text = "Refresh",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Navy,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderColor = CardBorder;
            StyleOutlineButton(refreshBtn);
            refreshBtn.Click += async (s, e) => await LoadServicesAsync();
            filterCard.Controls.Add(refreshBtn);

            // ---- Grid ----
            _grid = new DataGridView
            {
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = CardBorder,
                EnableHeadersVisualStyles = false,
                Location = new Point(0, 256),
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = HeaderBg,
                    ForeColor = Muted,
                    SelectionBackColor = HeaderBg,
                    SelectionForeColor = Muted,
                    Font = new Font("Segoe UI Semibold", 9f),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(16, 0, 0, 0),
                    WrapMode = DataGridViewTriState.False
                },
                ColumnHeadersHeight = 46,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowTemplate = { Height = 56 },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = Navy,
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(0xEA, 0xF2, 0xFD),
                    SelectionForeColor = Navy,
                    Padding = new Padding(16, 0, 8, 0),
                    WrapMode = DataGridViewTriState.False
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

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ServiceId", HeaderText = "SERVICE_ID", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name",
                HeaderText = "SERVICE_NAME / DESCRIPTION",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 240,
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "PRICE", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Duration", HeaderText = "DURATION", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "CATEGORY", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsActive", HeaderText = "STATUS", Width = 110 });

            _grid.CellPainting += Grid_CellPainting;
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var id = Convert.ToInt32(_grid.Rows[e.RowIndex].Cells["ServiceId"].Value);
                EditServiceAsync(id);
            };

            _contentPanel.Controls.Add(_grid);

            // ---- Layout ----
            void Relayout()
            {
                var w = _contentPanel.ClientSize.Width;
                var h = _contentPanel.ClientSize.Height;

                _addBtn.Location = new Point(w - _addBtn.Width, 30);
                _chipBar.Width = w;

                filterCard.Width = w;
                refreshBtn.Location = new Point(w - 16 - refreshBtn.Width, 28);
                searchBtn.Location = new Point(refreshBtn.Left - 10 - searchBtn.Width, 28);
                searchWrap.Width = Math.Max(120, searchBtn.Left - 12 - searchWrap.Left);

                _grid.SetBounds(0, 256, w, Math.Max(0, h - 256));
            }
            _contentPanel.Resize += (s, e) => Relayout();
            Relayout();
        }

        private static void StyleOutlineButton(Button b)
        {
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            b.BackColor = Color.White;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE9, 0xEE, 0xF6);
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
                    Size = new Size(120, 36),
                    Location = new Point(x, 0),
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    UseVisualStyleBackColor = false
                };

                bool isActive = cat == _activeCategory;
                chip.BackColor = isActive ? Navy : Color.White;
                chip.ForeColor = isActive ? Color.White : Navy;
                chip.FlatAppearance.BorderColor = isActive ? Navy : CardBorder;
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

        // -----------------------------------------------------------------
        //  Data
        // -----------------------------------------------------------------
        private async Task LoadServicesAsync()
        {
            try
            {
                _addBtn.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var list = await _http.GetFromJsonAsync<List<ProductDto>>("api/tenant/1/products");
                _allServices = list ?? new List<ProductDto>();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load services.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _addBtn.Enabled = true;
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

                // Two lines joined with "\n" — the CellPainting handler renders them on separate rows
                var nameCell = s.ProductName ?? "";
                if (!string.IsNullOrWhiteSpace(s.Description))
                    nameCell += "\n" + s.Description;

                int idx = _grid.Rows.Add(
                    s.ProductId,
                    nameCell,
                    $"₱{s.UnitPrice:N0}",
                    $"{s.DurationMinutes} min",
                    string.IsNullOrWhiteSpace(s.Category) ? "—" : s.Category,
                    s.IsActive ? "Active" : "Inactive");

                // Status — plain colored text (no pill), same as Manage Customers
                var statusCell = _grid.Rows[idx].Cells["IsActive"];
                var statusColor = s.IsActive ? Green : Red;
                statusCell.Style.ForeColor = statusColor;
                statusCell.Style.SelectionForeColor = statusColor;
                statusCell.Style.Font = StatusFont;
            }

            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        // ================================================================
        //  CELL PAINTING — two-line name cell
        // ================================================================
        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (_grid.Columns[e.ColumnIndex].Name == "Name")
                PaintTwoLine(e);
        }

        private static void PaintTwoLine(DataGridViewCellPaintingEventArgs e)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var text = Convert.ToString(e.Value) ?? "";
            var parts = text.Split('\n');
            var l1 = parts[0];
            var l2 = parts.Length > 1 ? parts[1] : null;

            var b = e.CellBounds;
            int x = b.X + 16;
            int w = Math.Max(10, b.Width - 24);
            var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;

            if (l2 == null)
            {
                TextRenderer.DrawText(e.Graphics, l1, NameFont,
                    new Rectangle(x, b.Y, w, b.Height),
                    Navy, flags);
            }
            else
            {
                // Heights come from the fonts, so nothing gets clipped on high-DPI screens
                int h1 = NameFont.Height, h2 = DescFont.Height, gap = 2;
                int top = b.Y + (b.Height - (h1 + h2 + gap)) / 2;

                TextRenderer.DrawText(e.Graphics, l1, NameFont,
                    new Rectangle(x, top, w, h1),
                    Navy, flags);

                TextRenderer.DrawText(e.Graphics, l2, DescFont,
                    new Rectangle(x, top + h1 + gap, w, h2),
                    Muted, flags);
            }

            e.Handled = true;
        }

        // -----------------------------------------------------------------
        //  Dialogs
        // -----------------------------------------------------------------
        private async void OpenAddServiceDialog()
        {
            using var dlg = new ServiceEditDialog(null);
            dlg.ShowDialog(FindForm());
            await LoadServicesAsync();
        }

        private async void EditServiceAsync(int serviceId)
        {
            using var dlg = new ServiceEditDialog(serviceId);
            dlg.ShowDialog(FindForm());
            await LoadServicesAsync();
        }
    }
}