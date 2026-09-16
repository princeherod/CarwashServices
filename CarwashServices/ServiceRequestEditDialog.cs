using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Views;

namespace CarwashServices
{
    public class ServiceRequestEditDialog : Form
    {
        private readonly int? _requestId;
        private readonly List<CustomerDto> _customers;
        private readonly List<ServiceDto> _services;

        private ComboBox _customerCombo;
        private ComboBox _serviceCombo;
        private TextBox _statusTxt;
        private ComboBox _priorityCombo;
        private DateTimePicker _requestedPicker;
        private DateTimePicker _scheduledPicker;
        private DateTimePicker _completedPicker;
        private CheckBox _completedEnabled;
        private TextBox _notesTxt;

        private static readonly Color Navy = Color.FromArgb(10, 22, 51);
        private static readonly Color TextDark = Color.FromArgb(10, 22, 51);
        private static readonly Color TextMuted = Color.FromArgb(107, 122, 154);
        private static readonly Color BorderSoft = Color.FromArgb(225, 231, 240);
        private static readonly Color AccentBlue = Color.FromArgb(30, 136, 229);
        private static readonly Color ReadOnlyBg = Color.FromArgb(0xF5, 0xF7, 0xFA);

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/")
        };

        public ServiceRequestEditDialog(
            int? requestId,
            List<CustomerDto> customers,
            List<ServiceDto> services)
        {
            _requestId = requestId;
            _customers = customers ?? new();
            _services = services ?? new();

            InitializeForm();

            if (_requestId.HasValue)
                Load += async (s, e) => await LoadAsync(_requestId.Value);
        }

        private void InitializeForm()
        {
            Text = _requestId.HasValue
                ? $"Edit SERVICE_REQUEST — #{_requestId}"
                : "New Service Request";
            Size = new Size(860, 780);
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
            header.Controls.Add(new Label
            {
                Text = Text,
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

            // ============ FK REFERENCES ============
            body.Controls.Add(SectionDivider("FK REFERENCES", y, body.Width - 60));
            y += 40;

            // ---- CUSTOMER_ID (full width) ----
            body.Controls.Add(MakeLabel("CUSTOMER_ID (FK → CUSTOMERS) *", 30, y));
            _customerCombo = MakeCombo(30, y + 22, 745);
            foreach (var c in _customers)
                _customerCombo.Items.Add(new ComboItem(c.CustomerId, $"{c.CustomerId} — {c.FullName}"));
            body.Controls.Add(_customerCombo);
            y += 75;

            // ---- SERVICE_ID (full width) ----
            body.Controls.Add(MakeLabel("SERVICE_ID (FK → SERVICES) *", 30, y));
            _serviceCombo = MakeCombo(30, y + 22, 745);
            foreach (var s in _services)
                _serviceCombo.Items.Add(new ComboItem(s.ServiceId, $"{s.ServiceId} — {s.ServiceName} (₱{s.Price:N0})"));
            body.Controls.Add(_serviceCombo);
            y += 75;

            // ============ SERVICE_REQUESTS FIELDS ============
            body.Controls.Add(SectionDivider("SERVICE_REQUESTS FIELDS", y, body.Width - 60));
            y += 40;

            // ---- STATUS (read-only) ----
            body.Controls.Add(MakeLabel("STATUS *", 30, y));
            _statusTxt = MakeTextBox(30, y + 22, 360);
            _statusTxt.Text = "Pending";
            _statusTxt.ReadOnly = true;
            _statusTxt.BackColor = ReadOnlyBg;
            _statusTxt.TabStop = false;
            body.Controls.Add(_statusTxt);

            // ---- PRIORITY ----
            body.Controls.Add(MakeLabel("PRIORITY", 415, y));
            _priorityCombo = MakeCombo(415, y + 22, 360);
            _priorityCombo.Items.AddRange(new object[] { "Normal", "High", "VIP" });
            _priorityCombo.SelectedIndex = 0;
            body.Controls.Add(_priorityCombo);
            y += 75;

            // ---- REQUESTED_DATE ----
            body.Controls.Add(MakeLabel("REQUESTED_DATE *", 30, y));
            _requestedPicker = MakeDatePicker(30, y + 22, 360);
            _requestedPicker.Value = DateTime.Now;
            body.Controls.Add(_requestedPicker);

            // ---- SCHEDULED_DATE ----
            body.Controls.Add(MakeLabel("SCHEDULED_DATE *", 415, y));
            _scheduledPicker = MakeDatePicker(415, y + 22, 360);
            _scheduledPicker.Value = DateTime.Now;
            body.Controls.Add(_scheduledPicker);
            y += 75;

            // ---- COMPLETED_DATE (optional) ----
            body.Controls.Add(MakeLabel("COMPLETED_DATE (nullable)", 30, y));
            _completedPicker = MakeDatePicker(30, y + 22, 320);
            _completedPicker.Enabled = false;

            _completedEnabled = new CheckBox
            {
                Text = "Set",
                Location = new Point(360, y + 26),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _completedEnabled.CheckedChanged += (s, e) =>
                _completedPicker.Enabled = _completedEnabled.Checked;

            body.Controls.Add(_completedPicker);
            body.Controls.Add(_completedEnabled);
            y += 75;

            // ---- NOTES ----
            body.Controls.Add(MakeLabel("NOTES", 30, y));
            _notesTxt = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 745,
                Height = 90,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Optional notes..."
            };
            body.Controls.Add(_notesTxt);
            y += 125;

            body.Controls.Add(new Label
            {
                Text = "Note: Status and staff assignment are managed by Service Staff.",
                ForeColor = AccentBlue,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(30, y),
                AutoSize = true
            });

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
                Text = _requestId.HasValue ? "Save Changes" : "Create Request",
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

        // ---- UI helpers ----
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
            Height = 32,
            Font = new Font("Segoe UI", 10f),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        private ComboBox MakeCombo(int x, int y, int width) => new ComboBox
        {
            Location = new Point(x, y),
            Width = width,
            Font = new Font("Segoe UI", 10f),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White
        };

        private DateTimePicker MakeDatePicker(int x, int y, int width) => new DateTimePicker
        {
            Location = new Point(x, y),
            Width = width,
            Font = new Font("Segoe UI", 10f),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "MM/dd/yyyy hh:mm tt",
            ShowUpDown = false
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
                Location = new Point(width / 2 - 90, 0),
                AutoSize = true,
                BackColor = Color.White
            };
            p.Controls.Add(line);

            p.Controls.Add(new Panel
            {
                Location = new Point(0, 9),
                Size = new Size(width / 2 - 110, 1),
                BackColor = BorderSoft
            });
            p.Controls.Add(new Panel
            {
                Location = new Point(width / 2 + 100, 9),
                Size = new Size(width / 2 - 100, 1),
                BackColor = BorderSoft
            });

            return p;
        }

        // ---- DATA LOAD ----
        private async Task LoadAsync(int id)
        {
            try
            {
                var req = await _http.GetFromJsonAsync<ServiceRequestDto>(
                    $"api/service-requests/{id}");

                if (req == null) return;

                SelectComboById(_customerCombo, req.CustomerId);
                SelectComboById(_serviceCombo, req.ServiceId);

                _statusTxt.Text = req.Status;
                _priorityCombo.SelectedItem = req.Priority ?? "Normal";

                if (req.RequestedDate != default)
                    _requestedPicker.Value = req.RequestedDate;

                if (req.ScheduledDate.HasValue)
                    _scheduledPicker.Value = req.ScheduledDate.Value;

                if (req.CompletedDate.HasValue)
                {
                    _completedEnabled.Checked = true;
                    _completedPicker.Value = req.CompletedDate.Value;
                }

                _notesTxt.Text = req.Notes ?? "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load service request.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectComboById(ComboBox combo, int? id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboItem ci && ci.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        // ---- SAVE ----
        private async Task SaveAsync()
        {
            if (_customerCombo.SelectedItem is not ComboItem custItem || custItem.Id == null)
            {
                MessageBox.Show("Please select a customer.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_serviceCombo.SelectedItem is not ComboItem svcItem || svcItem.Id == null)
            {
                MessageBox.Show("Please select a service.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Send DTO without assignedStaffId / createdBy.
            // Server fills them with defaults on POST, ignores them on PUT.
            var dto = new
            {
                requestId = _requestId ?? 0,
                customerId = custItem.Id.Value,
                serviceId = svcItem.Id.Value,
                status = _statusTxt.Text,
                priority = _priorityCombo.SelectedItem?.ToString() ?? "Normal",
                requestedDate = _requestedPicker.Value,
                scheduledDate = (DateTime?)_scheduledPicker.Value,
                completedDate = _completedEnabled.Checked
                                    ? (DateTime?)_completedPicker.Value
                                    : null,
                notes = _notesTxt.Text?.Trim(),
                createdAt = DateTime.UtcNow
            };

            try
            {
                HttpResponseMessage resp;

                if (_requestId.HasValue)
                {
                    resp = await _http.PutAsJsonAsync(
                        $"api/service-requests/{_requestId.Value}", dto);
                }
                else
                {
                    resp = await _http.PostAsJsonAsync(
                        "api/service-requests", dto);
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