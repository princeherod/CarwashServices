using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Records a complaint or feedback against a customer.
    /// The Kind is fixed at construction ("Complaint" or "Feedback").
    /// On create the Status is locked to Open; it becomes editable only
    /// when the dialog is opened in edit mode with an existing record.
    /// </summary>
    public class InteractionEditDialog : Form
    {
        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color FieldErrorBg = Color.FromArgb(0xFF, 0xF5, 0xF5);
        private static readonly Color ReadOnlyBg = Color.FromArgb(0xF5, 0xF7, 0xFA);

        private readonly int _customerId;
        private readonly string _kind;
        private readonly int? _interactionId;   // null = create, set = edit

        private TextBox _titleTxt = null!;
        private TextBox _detailsTxt = null!;
        private ComboBox _severityCombo = null!;
        private ComboBox _statusCombo = null!;

        private readonly ErrorProvider _errors = new ErrorProvider();

        private static readonly HttpClient Http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Letters, digits, spaces, and common punctuation. Must contain at
        // least one letter — pure-digit strings are rejected.
        private static readonly Regex TitleRegex =
            new(@"^(?=.*[\p{L}])[\p{L}\p{N}\s\.\,\-\'\!\?\(\)]{2,200}$", RegexOptions.Compiled);

        public InteractionEditDialog(int customerId, string kind)
            : this(customerId, kind, null) { }

        public InteractionEditDialog(int customerId, string kind, int? interactionId)
        {
            _customerId = customerId;
            _kind = kind;
            _interactionId = interactionId;
            BuildUi();

            if (_interactionId.HasValue)
                Load += async (s, e) => await LoadExistingAsync(_interactionId.Value);
        }

        private void BuildUi()
        {
            bool isEdit = _interactionId.HasValue;

            Text = isEdit
                ? (_kind == "Complaint" ? "Edit Complaint" : "Edit Feedback")
                : (_kind == "Complaint" ? "Record Complaint" : "Record Feedback");

            Size = new Size(600, isEdit ? 560 : 520);
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
                BackColor = Color.White
            };
            header.Controls.Add(new Label
            {
                Text = Text,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(24, 24),
                AutoSize = true
            });
            Controls.Add(header);

            // ---- Body ----
            int y = 90;
            int L = 24;
            int W = 540;

            Controls.Add(Label("Title *", L, y)); y += 22;
            _titleTxt = new TextBox
            {
                Location = new Point(L, y),
                Width = W,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            _titleTxt.TextChanged += (s, e) => ClearFieldError(_titleTxt);
            Controls.Add(_titleTxt);
            y += 40;

            Controls.Add(Label("Details *", L, y)); y += 22;
            _detailsTxt = new TextBox
            {
                Location = new Point(L, y),
                Width = W,
                Height = 120,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };
            _detailsTxt.TextChanged += (s, e) => ClearFieldError(_detailsTxt);
            Controls.Add(_detailsTxt);
            y += 140;

            Controls.Add(Label("Severity", L, y));
            Controls.Add(Label("Status", L + 270, y));
            y += 22;

            _severityCombo = new ComboBox
            {
                Location = new Point(L, y),
                Width = 250,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _severityCombo.Items.AddRange(new object[] { "Low", "Normal", "High" });
            _severityCombo.SelectedIndex = 1; // Normal
            Controls.Add(_severityCombo);

            _statusCombo = new ComboBox
            {
                Location = new Point(L + 270, y),
                Width = 250,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = isEdit ? Color.White : ReadOnlyBg,
                Enabled = isEdit        // locked on create
            };
            _statusCombo.Items.AddRange(new object[] { "Open", "Resolved" });
            _statusCombo.SelectedIndex = 0; // always Open on create
            Controls.Add(_statusCombo);

            if (!isEdit)
            {
                var lockHint = new Label
                {
                    Text = "New records are created as Open. Use the Edit button to mark Resolved.",
                    ForeColor = Faint,
                    Font = new Font("Segoe UI", 8.5f),
                    Location = new Point(L + 270, y + 30),
                    AutoSize = false,
                    Size = new Size(250, 32)
                };
                Controls.Add(lockHint);
            }

            // ---- Footer ----
            var cancelBtn = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted,
                BackColor = Color.White,
                Size = new Size(110, 42),
                Location = new Point(Width - 250, Height - 80),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancelBtn);

            var saveBtn = new Button
            {
                Text = isEdit ? "Save" : "Save",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                Size = new Size(110, 42),
                Location = new Point(Width - 130, Height - 80),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += async (s, e) => await SaveAsync();
            Controls.Add(saveBtn);
        }

        private Label Label(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        // ================================================================
        //  Error helpers
        // ================================================================
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

        private bool ValidateForm()
        {
            ClearFieldError(_titleTxt);
            ClearFieldError(_detailsTxt);

            bool ok = true;

            var title = _titleTxt.Text.Trim();
            if (title.Length == 0)
            {
                MarkFieldError(_titleTxt, "Title is required.");
                ok = false;
            }
            else if (title.Length < 3)
            {
                MarkFieldError(_titleTxt, "Title must be at least 3 characters.");
                ok = false;
            }
            else if (title.Length > 200)
            {
                MarkFieldError(_titleTxt, "Title must be 200 characters or fewer.");
                ok = false;
            }
            else if (!TitleRegex.IsMatch(title))
            {
                MarkFieldError(_titleTxt, "Title must contain at least one letter and only letters, numbers, spaces, or . , - ' ! ? ( ).");
                ok = false;
            }

            var details = _detailsTxt.Text.Trim();
            if (details.Length == 0)
            {
                MarkFieldError(_detailsTxt, "Details are required.");
                ok = false;
            }
            else if (details.Length < 5)
            {
                MarkFieldError(_detailsTxt, "Details must be at least 5 characters.");
                ok = false;
            }
            else if (details.Length > 2000)
            {
                MarkFieldError(_detailsTxt, "Details must be 2000 characters or fewer.");
                ok = false;
            }

            return ok;
        }

        // ================================================================
        //  LOAD (edit mode)
        // ================================================================
        private async Task LoadExistingAsync(int id)
        {
            try
            {
                var row = await Http.GetFromJsonAsync<CustomerInteractionDto>(
                    $"api/tenant/1/customer-interactions/{id}");

                if (row == null) return;

                _titleTxt.Text = row.Title ?? "";
                _detailsTxt.Text = row.Details ?? "";

                if (!string.IsNullOrWhiteSpace(row.Severity))
                    _severityCombo.SelectedItem = row.Severity;

                if (!string.IsNullOrWhiteSpace(row.Status))
                    _statusCombo.SelectedItem = row.Status;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load interaction.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ================================================================
        //  SAVE
        // ================================================================
        private async Task SaveAsync()
        {
            if (!ValidateForm())
            {
                // Focus the first invalid control
                if (!string.IsNullOrEmpty(_errors.GetError(_titleTxt)))
                    _titleTxt.Focus();
                else if (!string.IsNullOrEmpty(_errors.GetError(_detailsTxt)))
                    _detailsTxt.Focus();
                return;
            }

            var dto = new
            {
                customerId = _customerId,
                kind = _kind,
                severity = _severityCombo.SelectedItem?.ToString() ?? "Normal",
                title = _titleTxt.Text.Trim(),
                details = _detailsTxt.Text.Trim(),
                status = _statusCombo.SelectedItem?.ToString() ?? "Open"
            };

            try
            {
                HttpResponseMessage resp;

                if (_interactionId.HasValue)
                {
                    // Edit — only the fields sent are updated on the server.
                    resp = await Http.PutAsJsonAsync(
                        $"api/tenant/1/customer-interactions/{_interactionId.Value}",
                        dto);
                }
                else
                {
                    resp = await Http.PostAsJsonAsync(
                        "api/tenant/1/customer-interactions",
                        new
                        {
                            customerId = dto.customerId,
                            kind = dto.kind,
                            severity = dto.severity,
                            title = dto.title,
                            details = dto.details,
                            status = "Open",          // forced on create
                            createdAt = DateTime.UtcNow
                        });
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
                        $"Save failed.\n\n{resp.StatusCode}\n\n{body}",
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