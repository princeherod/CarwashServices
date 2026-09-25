using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Follow-Up dialog used only by Service Staff.
    ///
    /// Layout:
    ///   ┌───────────────────────────────────────┐
    ///   │ Title strip (fixed 88px)              │
    ///   ├───────────────────────────────────────┤
    ///   │                                       │
    ///   │ Scrollable body                       │  ← AutoScroll on a panel
    ///   │                                       │
    ///   ├───────────────────────────────────────┤
    ///   │ [Cancel]              [Submit for App]│  ← Docked bottom, never scrolls
    ///   └───────────────────────────────────────┘
    ///
    /// Every submission lands in Pending Approval. There is no Send Now
    /// SMTP path here — approval is handled by an Admin later.
    /// </summary>
    public class ServiceStaffFollowUpDialog : Form
    {
        private readonly List<TenantCustomerDto> _myCustomers;
        private readonly int _staffId;

        // Body controls
        private CheckedListBox _customerList = null!;
        private TextBox _reasonTxt = null!;
        private ComboBox _discountCombo = null!;
        private DateTimePicker _validUntilPicker = null!;
        private ComboBox _typeCombo = null!;
        private TextBox _previewBox = null!;
        private DateTimePicker _scheduledPicker = null!;
        private Button _sendNowBtn = null!, _scheduleBtn = null!;
        private Label _badge = null!;
        private Label _sendOnLbl = null!;

        private bool _sendNow = false;

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentSoft = Color.FromArgb(0xD6, 0xE9, 0xFA);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color BodyBg = Color.White;

        // ---- Layout ----
        private const int DialogW = 720;
        private const int DialogH = 720;      // fixed, reasonable
        private const int HeaderH = 88;       // title strip
        private const int FooterH = 72;       // buttons
        private const int BodyLeft = 40;
        private const int BodyW = 640;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/")
        };

        public ServiceStaffFollowUpDialog(List<TenantCustomerDto> myCustomers, int staffId)
        {
            _myCustomers = myCustomers ?? new();
            _staffId = staffId;

            BuildUi();
        }

        private void BuildUi()
        {
            Text = "New Follow-Up — Submit for Approval";
            ClientSize = new Size(DialogW, DialogH);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- 1. Header strip (docked top) ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderH,
                BackColor = BodyBg
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Controls.Add(header);

            var title = new Label
            {
                Text = "New Follow-Up",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 16f),
                Location = new Point(BodyLeft, 20),
                AutoSize = true,
                BackColor = BodyBg
            };
            header.Controls.Add(title);

            _badge = new Label
            {
                Text = "0 selected",
                ForeColor = Accent,
                Font = new Font("Segoe UI Semibold", 9f),
                BackColor = AccentSoft,
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true,
                Location = new Point(title.PreferredWidth + BodyLeft + 14, 24)
            };
            header.Controls.Add(_badge);

            var subtitle = new Label
            {
                Text = "Pick customers from your assigned list. Your manager will approve this before it can be sent.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(BodyLeft + 2, 52),
                AutoSize = true,
                BackColor = BodyBg
            };
            header.Controls.Add(subtitle);

            // ---- 2. Footer strip (docked bottom, buttons never scroll) ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = FooterH,
                BackColor = BodyBg
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            Controls.Add(footer);

            // Placed from the right so they stay aligned regardless of DPI
            var submit = new Button
            {
                Text = "Submit for Approval",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Accent,
                Size = new Size(200, 44),
                Location = new Point(DialogW - 200 - 40, 14),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            submit.FlatAppearance.BorderSize = 0;
            submit.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x19, 0x76, 0xD2);
            submit.Click += async (s, e) => await SubmitAsync();
            footer.Controls.Add(submit);

            var cancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted,
                BackColor = Color.White,
                Size = new Size(110, 44),
                Location = new Point(submit.Left - 12 - 110, 14),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            cancel.FlatAppearance.BorderColor = BorderSoft;
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancel);

            // ---- 3. Body (fills the middle, scrolls) ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BodyBg,
                Padding = new Padding(BodyLeft, 16, 20, 16)
            };
            Controls.Add(body);

            // IMPORTANT: Fill is added last so it occupies the space between
            // the docked header and footer.
            body.BringToFront();

            int contentW = BodyW;
            int y = 0;

            // Customers
            body.Controls.Add(Cap("CUSTOMERS *", 0, y));
            y += 22;

            _customerList = new CheckedListBox
            {
                Location = new Point(0, y),
                Size = new Size(contentW, 130),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false,
                BackColor = Color.White
            };
            foreach (var c in _myCustomers)
                _customerList.Items.Add(new CustomerItem(c.TenantCustomerId,
                    $"{c.CustomerName}  ·  {c.ContactNumber}"));
            _customerList.ItemCheck += (s, e) =>
            {
                if (_customerList.IsHandleCreated)
                    BeginInvoke(new Action(UpdateBadge));
            };
            body.Controls.Add(_customerList);
            y += _customerList.Height + 16;

            // When
            body.Controls.Add(Cap("WHEN *", 0, y));
            y += 22;

            _sendNowBtn = MakeSegmented("Send Now (needs approval)", 0, y);
            _sendNowBtn.Click += (s, e) => SetWhen(true);
            body.Controls.Add(_sendNowBtn);

            _scheduleBtn = MakeSegmented("Schedule", 320, y);
            _scheduleBtn.Click += (s, e) => SetWhen(false);
            body.Controls.Add(_scheduleBtn);
            y += 60;

            // Send On
            _sendOnLbl = Cap("SEND ON", 0, y);
            body.Controls.Add(_sendOnLbl);
            y += 22;

            _scheduledPicker = new DateTimePicker
            {
                Location = new Point(0, y),
                Width = contentW,
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy  hh:mm tt",
                Value = DateTime.Today.AddDays(1).AddHours(9)
            };
            body.Controls.Add(_scheduledPicker);
            y += 52;

            // Reason
            body.Controls.Add(Cap("REASON *", 0, y));
            y += 22;

            _reasonTxt = new TextBox
            {
                Location = new Point(0, y),
                Width = contentW,
                Height = 60,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                PlaceholderText = "Why is this follow-up being created?",
                ScrollBars = ScrollBars.Vertical
            };
            body.Controls.Add(_reasonTxt);
            y += 92;

            // Discount + Valid Until
            body.Controls.Add(Cap("DISCOUNT OFFER", 0, y));
            body.Controls.Add(Cap("VALID UNTIL", 330, y));
            y += 22;

            _discountCombo = new ComboBox
            {
                Location = new Point(0, y),
                Width = 310,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _discountCombo.Items.AddRange(new object[]
            {
                "No discount",
                "10% off next wash",
                "15% off next wash",
                "20% off next wash",
                "Free wax add-on",
                "Free interior vacuum"
            });
            _discountCombo.SelectedIndex = 1;
            body.Controls.Add(_discountCombo);

            _validUntilPicker = new DateTimePicker
            {
                Location = new Point(330, y),
                Width = 310,
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                Value = DateTime.Today.AddDays(30)
            };
            body.Controls.Add(_validUntilPicker);
            y += 52;

            // Type
            body.Controls.Add(Cap("TYPE", 0, y));
            y += 22;

            _typeCombo = new ComboBox
            {
                Location = new Point(0, y),
                Width = contentW,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _typeCombo.Items.AddRange(new object[]
            {
                "Promotional Offer",
                "Service Reminder",
                "Post-Service Feedback",
                "Renewal"
            });
            _typeCombo.SelectedIndex = 0;
            body.Controls.Add(_typeCombo);
            y += 52;

            // Message Preview
            body.Controls.Add(Cap("MESSAGE PREVIEW", 0, y));
            y += 22;

            _previewBox = new TextBox
            {
                Location = new Point(0, y),
                Width = contentW,
                Height = 110,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                ScrollBars = ScrollBars.Vertical
            };
            body.Controls.Add(_previewBox);
            y += 130;

            // Bottom padding so the last field never sits flush against the footer
            y += 20;

            // Tell the scrollable body how tall its content is
            body.AutoScrollMinSize = new Size(contentW + BodyLeft + 20, y);

            SetWhen(false);
        }

        private static Label Cap(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true,
            BackColor = BodyBg
        };

        private Button MakeSegmented(string text, int x, int y)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(300, 40),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = BorderSoft;
            return b;
        }

        private void SetWhen(bool sendNow)
        {
            _sendNow = sendNow;

            _sendNowBtn.BackColor = sendNow ? AccentSoft : Color.White;
            _sendNowBtn.ForeColor = sendNow ? Accent : Navy;
            _sendNowBtn.Font = new Font("Segoe UI Semibold", 9.5f);

            _scheduleBtn.BackColor = sendNow ? Color.White : AccentSoft;
            _scheduleBtn.ForeColor = sendNow ? Navy : Accent;
            _scheduleBtn.Font = new Font("Segoe UI Semibold", 9.5f);

            _scheduledPicker.Enabled = !sendNow;
            _sendOnLbl.ForeColor = sendNow ? Muted : Navy;
        }

        private void UpdateBadge()
        {
            _badge.Text = $"{_customerList.CheckedItems.Count} selected";
        }

        private async Task SubmitAsync()
        {
            var checkedIndices = _customerList.CheckedIndices.Cast<int>().ToList();
            if (checkedIndices.Count == 0)
            {
                MessageBox.Show("Please select at least one customer.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var reason = _reasonTxt.Text.Trim();
            if (reason.Length == 0)
            {
                MessageBox.Show("Reason is required.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_previewBox.Text))
            {
                MessageBox.Show("Message preview cannot be empty.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_validUntilPicker.Value.Date < DateTime.Today)
            {
                MessageBox.Show("Valid Until cannot be in the past.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var offer = _discountCombo.SelectedItem?.ToString();
            if (offer == "No discount") offer = null;

            var scheduled = _sendNow ? DateTime.Now : _scheduledPicker.Value;

            if (!_sendNow && scheduled <= DateTime.Now)
            {
                MessageBox.Show("Scheduled time must be in the future.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var customerIds = checkedIndices
                .Where(i => i >= 0 && i < _customerList.Items.Count)
                .Select(i => ((CustomerItem)_customerList.Items[i]).CustomerId)
                .ToList();

            var payload = new
            {
                customerIds,
                type = _typeCombo.SelectedItem?.ToString() ?? "Service Reminder",
                contactMethod = "Email",
                reason,
                discountOffer = offer,
                notes = _previewBox.Text,
                scheduledDate = scheduled,
                validUntil = (DateTime?)_validUntilPicker.Value,
                scheduledNow = _sendNow,
                isDraft = false,
                createdBy = _staffId
            };

            try
            {
                Cursor = Cursors.WaitCursor;

                var resp = await _http.PostAsJsonAsync("api/follow-ups/bulk", payload);

                if (resp.IsSuccessStatusCode)
                {
                    BulkFollowUpResponse? parsed = null;
                    try { parsed = await resp.Content.ReadFromJsonAsync<BulkFollowUpResponse>(); }
                    catch { }

                    int created = parsed?.Count ?? 0;
                    int skipped = parsed?.Skipped ?? 0;
                    int pending = parsed?.PendingApproval ?? 0;

                    var lines = new List<string>
                    {
                        "Submitted for approval.",
                        "",
                        $"Created: {created}",
                        $"Awaiting Admin approval: {pending}"
                    };
                    if (skipped > 0)
                        lines.Add($"Skipped (already had an open follow-up): {skipped}");

                    MessageBox.Show(
                        string.Join(Environment.NewLine, lines),
                        "Submitted",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    MessageBox.Show(
                        "None of the selected customers can receive a new follow-up.",
                        "Already Contacted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Submit failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Submit failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private sealed class CustomerItem
        {
            public int CustomerId { get; }
            public string Display { get; }
            public CustomerItem(int id, string display) { CustomerId = id; Display = display; }
            public override string ToString() => Display;
        }
    }
}