using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Create / edit a service request.
    ///
    /// This dialog edits only the CONTENT of a request:
    ///   - Customer, Service, Priority, Requested Date, Scheduled Date, Notes.
    ///   - Completed Date is read-only (set by the Service Staff flow).
    ///   - No Status dropdown.
    ///   - No Assigned Staff — that belongs in the "Assign Service Staff" module.
    ///
    /// On CREATE the API forces status = "Pending" and AssignedStaffId = null.
    /// On EDIT the assignment is not touched by this dialog.
    /// </summary>
    public class ServiceRequestEditDialog : Form
    {
        private readonly int? _requestId;
        private readonly List<CustomerDto> _customers;
        private readonly List<ServiceDto> _services;
        private ServiceRequestDto? _existing;

        private ComboBox _customerCombo = null!;
        private ComboBox _serviceCombo = null!;
        private ComboBox _priorityCombo = null!;
        private DateTimePicker _requestedPicker = null!;
        private DateTimePicker _scheduledPicker = null!;

        private Label _completedValueLbl = null!;
        private TextBox _notesTxt = null!;

        private readonly Dictionary<Control, Label> _fieldErrors = new();

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(10, 22, 51);
        private static readonly Color TextDark = Color.FromArgb(10, 22, 51);
        private static readonly Color TextMuted = Color.FromArgb(107, 122, 154);
        private static readonly Color BorderSoft = Color.FromArgb(225, 231, 240);
        private static readonly Color AccentBlue = Color.FromArgb(30, 136, 229);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color FieldErrorBg = Color.FromArgb(0xFF, 0xF1, 0xF1);
        private static readonly Color ReadOnlyBg = Color.FromArgb(0xF7, 0xFA, 0xFD);

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/")
        };

        /// <summary>
        /// Signature kept identical to previous versions so no caller needs
        /// to change. The `staff` argument is accepted for compatibility but
        /// no longer used — assignment lives in its own module.
        /// </summary>
        public ServiceRequestEditDialog(
            int? requestId,
            List<CustomerDto> customers,
            List<ServiceDto> services,
            List<UserDto>? staff = null)
        {
            _requestId = requestId;
            _customers = customers ?? new();
            _services = services ?? new();
            _ = staff;   // retained for API compatibility

            InitializeForm();

            if (_requestId.HasValue)
                Load += async (s, e) => await LoadAsync(_requestId.Value);
        }

        private void InitializeForm()
        {
            bool isEdit = _requestId.HasValue;

            Text = isEdit
                ? $"Edit SERVICE_REQUEST — #{_requestId}"
                : "New Service Request";
            Size = new Size(860, isEdit ? 680 : 600);
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

            // ============ REQUEST DETAILS ============
            body.Controls.Add(SectionDivider("Request Details", y, body.Width - 60));
            y += 40;

            // ---- Customer ----
            body.Controls.Add(MakeLabel("Customer *", 30, y));
            _customerCombo = MakeCombo(30, y + 22, 745);
            foreach (var c in _customers)
                _customerCombo.Items.Add(new ComboItem(c.CustomerId, c.FullName));
            body.Controls.Add(_customerCombo);
            AddErrorLabel(body, _customerCombo, 30, y + 50);
            y += 88;

            // ---- Service ----
            body.Controls.Add(MakeLabel("Service *", 30, y));
            _serviceCombo = MakeCombo(30, y + 22, 745);
            foreach (var s in _services)
                _serviceCombo.Items.Add(new ComboItem(s.ServiceId, $"{s.ServiceName} (₱{s.Price:N0})"));
            body.Controls.Add(_serviceCombo);
            AddErrorLabel(body, _serviceCombo, 30, y + 50);
            y += 88;

            // (Assigned Staff combo intentionally omitted — see the
            //  "Assign Service Staff" module.)

            // ============ SCHEDULING ============
            body.Controls.Add(SectionDivider("Scheduling", y, body.Width - 60));
            y += 40;

            // ---- Priority ----
            body.Controls.Add(MakeLabel("Priority", 30, y));
            _priorityCombo = MakeCombo(30, y + 22, 360);
            _priorityCombo.Items.AddRange(new object[] { "Normal", "High", "VIP" });
            _priorityCombo.SelectedIndex = 0;
            body.Controls.Add(_priorityCombo);
            AddErrorLabel(body, _priorityCombo, 30, y + 50);
            y += 88;

            // ---- Requested Date ----
            body.Controls.Add(MakeLabel("Requested Date *", 30, y));
            _requestedPicker = MakeDatePicker(30, y + 22, 360);
            _requestedPicker.MinDate = DateTime.Today;
            _requestedPicker.Value = DateTime.Now;
            _requestedPicker.ValueChanged += (s, e) =>
            {
                ClearFieldError(_requestedPicker);
                if (_scheduledPicker != null) ValidateScheduledField();
            };
            body.Controls.Add(_requestedPicker);
            AddErrorLabel(body, _requestedPicker, 30, y + 50);

            // ---- Scheduled Date ----
            body.Controls.Add(MakeLabel("Scheduled Date *", 415, y));
            _scheduledPicker = MakeDatePicker(415, y + 22, 360);
            _scheduledPicker.MinDate = DateTime.Today;
            _scheduledPicker.Value = DateTime.Now;
            _scheduledPicker.ValueChanged += (s, e) => ClearFieldError(_scheduledPicker);
            body.Controls.Add(_scheduledPicker);
            AddErrorLabel(body, _scheduledPicker, 415, y + 50);
            y += 88;

            // ---- Completed Date (read-only, edit mode only) ----
            if (isEdit)
            {
                body.Controls.Add(MakeLabel("Completed Date", 30, y));

                _completedValueLbl = new Label
                {
                    Location = new Point(30, y + 22),
                    Width = 360,
                    Height = 30,
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = TextMuted,
                    BackColor = ReadOnlyBg,
                    BorderStyle = BorderStyle.FixedSingle,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 0, 0),
                    Text = "—"
                };
                body.Controls.Add(_completedValueLbl);

                body.Controls.Add(new Label
                {
                    Text = "Set automatically when the Service Staff completes the job.",
                    ForeColor = TextMuted,
                    Font = new Font("Segoe UI", 8.5f),
                    Location = new Point(30, y + 56),
                    AutoSize = true
                });

                y += 88;
            }

            // ---- Notes ----
            body.Controls.Add(MakeLabel("Notes", 30, y));
            _notesTxt = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 745,
                Height = 90,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                PlaceholderText = "Optional notes...",
                ScrollBars = ScrollBars.Vertical
            };
            body.Controls.Add(_notesTxt);
            y += 125;

            // ---- Hint ----
            body.Controls.Add(new Label
            {
                Text = isEdit
                    ? "Assignment is handled in the Assign Service Staff module. Status is updated by the assigned Service Staff."
                    : "New requests start as Pending. Use Assign Service Staff to pick the Service Staff who will handle it.",
                ForeColor = AccentBlue,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(30, y),
                AutoSize = false,
                Size = new Size(745, 20)
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
                Text = isEdit ? "Save Changes" : "Create Request",
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

        private void AddErrorLabel(Control parent, Control field, int x, int y)
        {
            var lbl = new Label
            {
                Text = "",
                ForeColor = Danger,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(x, y),
                AutoSize = true,
                Visible = false
            };
            parent.Controls.Add(lbl);
            _fieldErrors[field] = lbl;
        }

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
                Location = new Point(width / 2 - 60, 0),
                AutoSize = true,
                BackColor = Color.White
            };
            p.Controls.Add(line);

            p.Controls.Add(new Panel
            {
                Location = new Point(0, 9),
                Size = new Size(width / 2 - 80, 1),
                BackColor = BorderSoft
            });
            p.Controls.Add(new Panel
            {
                Location = new Point(width / 2 + 70, 9),
                Size = new Size(width / 2 - 70, 1),
                BackColor = BorderSoft
            });

            return p;
        }

        // ================================================================
        //  Error helpers
        // ================================================================
        private void ClearFieldError(Control c)
        {
            if (_fieldErrors.TryGetValue(c, out var lbl))
            {
                lbl.Text = "";
                lbl.Visible = false;
            }

            if (c is TextBox tb) { tb.BackColor = Color.White; }
            else if (c is ComboBox cb) { cb.BackColor = Color.White; }
            else if (c is DateTimePicker dp) { dp.CalendarMonthBackground = Color.White; }
        }

        private void MarkFieldError(Control c, string message)
        {
            if (_fieldErrors.TryGetValue(c, out var lbl))
            {
                lbl.Text = message;
                lbl.Visible = true;
                lbl.BringToFront();
            }

            if (c is TextBox tb) { tb.BackColor = FieldErrorBg; }
            else if (c is ComboBox cb) { cb.BackColor = FieldErrorBg; }
            else if (c is DateTimePicker dp) { dp.CalendarMonthBackground = FieldErrorBg; }
        }

        private bool HasFieldError(Control c)
        {
            return _fieldErrors.TryGetValue(c, out var lbl) && !string.IsNullOrEmpty(lbl.Text);
        }

        // ================================================================
        //  Live validation for Scheduled Date
        // ================================================================
        private void ValidateScheduledField()
        {
            if (_requestedPicker == null || _scheduledPicker == null) return;

            if (_scheduledPicker.Value < _requestedPicker.Value)
            {
                MarkFieldError(_scheduledPicker,
                    "Scheduled date must be on or after the requested date.");
            }
            else
            {
                ClearFieldError(_scheduledPicker);
            }
        }

        // ================================================================
        //  DATA LOAD (edit mode)
        // ================================================================
        private async Task LoadAsync(int id)
        {
            try
            {
                var req = await _http.GetFromJsonAsync<ServiceRequestDto>(
                    $"api/service-requests/{id}?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}");

                if (req == null) return;
                _existing = req;

                SelectComboById(_customerCombo, req.CustomerId);
                SelectComboById(_serviceCombo, req.ServiceId);

                _priorityCombo.SelectedItem = req.Priority ?? "Normal";

                if (req.RequestedDate != default)
                {
                    if (req.RequestedDate < DateTime.Today)
                        _requestedPicker.MinDate = req.RequestedDate;
                    _requestedPicker.Value = req.RequestedDate;
                }

                if (req.ScheduledDate.HasValue)
                {
                    if (req.ScheduledDate.Value < DateTime.Today)
                        _scheduledPicker.MinDate = req.ScheduledDate.Value;
                    _scheduledPicker.Value = req.ScheduledDate.Value;
                }

                if (_completedValueLbl != null)
                {
                    _completedValueLbl.Text = req.CompletedDate.HasValue
                        ? req.CompletedDate.Value.ToString("MM/dd/yyyy hh:mm tt")
                        : "—";
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

        // ================================================================
        //  VALIDATION
        // ================================================================
        private bool ValidateForm()
        {
            foreach (var c in _fieldErrors.Keys)
                ClearFieldError(c);

            bool ok = true;

            if (_customerCombo.SelectedItem is not ComboItem custItem || custItem.Id == null)
            {
                MarkFieldError(_customerCombo, "Please select a customer.");
                ok = false;
            }

            if (_serviceCombo.SelectedItem is not ComboItem svcItem || svcItem.Id == null)
            {
                MarkFieldError(_serviceCombo, "Please select a service.");
                ok = false;
            }

            if (_requestedPicker.Value.Date < DateTime.Today)
            {
                MarkFieldError(_requestedPicker, "Requested date cannot be in the past.");
                ok = false;
            }

            if (_scheduledPicker.Value < _requestedPicker.Value)
            {
                MarkFieldError(_scheduledPicker,
                    "Scheduled date must be on or after the requested date.");
                ok = false;
            }

            return ok;
        }

        // ================================================================
        //  SAVE
        // ================================================================
        private async Task SaveAsync()
        {
            bool isEdit = _requestId.HasValue;

            if (!ValidateForm())
            {
                foreach (var c in _fieldErrors.Keys)
                {
                    if (HasFieldError(c))
                    {
                        c.Focus();
                        break;
                    }
                }
                return;
            }

            var custItem = (ComboItem)_customerCombo.SelectedItem!;
            var svcItem = (ComboItem)_serviceCombo.SelectedItem!;

            // Single payload for every role.
            //   - No status (Service Staff owns it).
            //   - No completedDate (Service Staff flow sets it).
            //   - No assignedStaffId / clearAssignment (Assign Service Staff owns it).
            var dto = new
            {
                requestId = _requestId ?? 0,
                customerId = custItem.Id!.Value,
                serviceId = svcItem.Id!.Value,
                branchId = _requestId.HasValue
                    ? (_existing?.BranchId ?? CarwashServices.Auth.SessionUser.CurrentBranchId)
                    : CarwashServices.Auth.SessionUser.CurrentBranchId,
                priority = _priorityCombo.SelectedItem?.ToString() ?? "Normal",
                requestedDate = _requestedPicker.Value,
                scheduledDate = (DateTime?)_scheduledPicker.Value,
                notes = _notesTxt.Text?.Trim(),
                createdAt = DateTime.UtcNow
            };

            try
            {
                HttpResponseMessage resp;

                var companyId = CarwashServices.Auth.SessionUser.CurrentCompanyId;
                if (_requestId.HasValue)
                {
                    resp = await _http.PutAsJsonAsync(
                        $"api/service-requests/{_requestId.Value}?companyId={companyId}", dto);
                }
                else
                {
                    resp = await _http.PostAsJsonAsync(
                        $"api/service-requests?companyId={companyId}", dto);
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