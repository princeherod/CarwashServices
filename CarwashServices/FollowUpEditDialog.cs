using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Views;

namespace CarwashServices
{
    public class FollowUpEditDialog : Form
    {
        private readonly List<TenantCustomerDto> _customers;
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/")
        };

        // Controls
        private CheckedListBox _customerList;
        private CheckBox _selectAll;
        private Button _smsBtn, _emailBtn;
        private Button _sendNowBtn, _scheduleBtn;
        private DateTimePicker _scheduledPicker;
        private DateTimePicker _validUntilPicker;
        private ComboBox _discountCombo;
        private ComboBox _typeCombo;
        private TextBox _previewBox;

        // UI-only references
        private Label _badge;
        private Label _sendOnLbl;
        private Label _charCount;

        private bool _sendNow = true;
        private string _contactMethod = "SMS";

        // Layout: content column is 640px wide, centred in a 720px dialog
        private const int ContentW = 640;
        private const int ContentLeft = 40;
        private const int HalfW = 310;      // two-column rows: 310 | 20 gap | 310
        private const int RightColX = 330;

        // Palette
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color MutedLight = Color.FromArgb(0xB4, 0xBE, 0xD2);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentSoft = Color.FromArgb(0xD6, 0xE9, 0xFA);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color BgCard = Color.FromArgb(0xF7, 0xFA, 0xFD);

        private readonly HashSet<int> _preselectedIds;

        public FollowUpEditDialog(List<TenantCustomerDto> customers)
            : this(customers, null) { }

        public FollowUpEditDialog(List<TenantCustomerDto> customers, List<int> preselectedCustomerIds)
        {
            _customers = customers ?? new();
            _preselectedIds = new HashSet<int>(preselectedCustomerIds ?? new List<int>());
            InitializeForm();

            // After the list is built, tick the preselected ones
            if (_preselectedIds.Count > 0 && _customerList != null)
            {
                for (int i = 0; i < _customerList.Items.Count && i < _customers.Count; i++)
                {
                    if (_preselectedIds.Contains(_customers[i].TenantCustomerId))
                        _customerList.SetItemChecked(i, true);
                }
                UpdateBadge();
                UpdatePreview();
            }
        }

        private static Label Caption(string text, int x, int y) => new Label
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
            Text = "Follow up with at-risk customers";

            // Client size (not outer size) so the layout maths below is exact,
            // and never taller than the screen.
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
            ClientSize = new Size(720, Math.Min(820, wa.Height - 80));

            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // Scrollable body (Fill) — must be added BEFORE the docked footer
            var scroller = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };
            Controls.Add(scroller);

            // Footer stays pinned to the bottom so the buttons are always visible
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White
            };
            Controls.Add(footer);
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BorderSoft });

            // Content column: centred, equal left/right margins
            var root = new Panel
            {
                Location = new Point(ContentLeft, 24),
                Width = ContentW,
                BackColor = Color.White
            };
            scroller.Controls.Add(root);

            int y = 0;

            // ---- Title + badge ----
            var title = new Label
            {
                Text = "Follow up with at-risk customers",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 16f),
                Location = new Point(0, y),
                AutoSize = true
            };
            root.Controls.Add(title);

            _badge = new Label
            {
                Text = "0 selected",
                ForeColor = Accent,
                Font = new Font("Segoe UI Semibold", 9f),
                BackColor = AccentSoft,
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true,
                Location = new Point(title.PreferredWidth + 14, y + 4)   // sits after the title, no overlap
            };
            void RoundBadge()
            {
                if (_badge.Width <= 0 || _badge.Height <= 0) return;
                using var p = RoundedRect(new Rectangle(0, 0, _badge.Width, _badge.Height), _badge.Height / 2);
                _badge.Region = new Region(p);
            }
            _badge.SizeChanged += (s, e) => RoundBadge();
            root.Controls.Add(_badge);
            RoundBadge();
            y += 36;

            root.Controls.Add(new Label
            {
                Text = "Reach out before they pass 120 days and are marked as lost.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 34;

            // ---- Customers header with select all ----
            root.Controls.Add(Caption("CUSTOMERS *", 0, y));

            _selectAll = new CheckBox
            {
                Text = "Select all",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Navy,
                Location = new Point(ContentW - 88, y - 4),    // right-aligned to the list edge
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _selectAll.CheckedChanged += (s, e) =>
            {
                for (int i = 0; i < _customerList.Items.Count; i++)
                    _customerList.SetItemChecked(i, _selectAll.Checked);
            };
            root.Controls.Add(_selectAll);
            y += 26;

            // ---- Customer list ----
            _customerList = new CheckedListBox
            {
                Location = new Point(0, y),
                Size = new Size(ContentW, 170),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                BackColor = Color.White,
                IntegralHeight = false
            };

            foreach (var c in _customers.Take(20))
            {
                var label = c.CustomerName;
                if (!string.IsNullOrWhiteSpace(c.ContactNumber))
                    label += $"  ·  {c.ContactNumber}";
                _customerList.Items.Add(label);
            }

            _customerList.ItemCheck += (s, e) =>
            {
                // ItemCheck fires BEFORE the new state is committed; defer the badge update.
                // But only if the handle exists (during construction it may not).
                if (_customerList.IsHandleCreated)
                {
                    _customerList.BeginInvoke(new Action(() =>
                    {
                        UpdateBadge();
                        UpdatePreview();
                    }));
                }
                else
                {
                    UpdatePreview();
                }
            };

            root.Controls.Add(_customerList);
            y += _customerList.Height + 20;

            // ---- SEND VIA + WHEN row ----
            root.Controls.Add(Caption("SEND VIA", 0, y));
            root.Controls.Add(Caption("WHEN", RightColX, y));
            y += 22;

            _smsBtn = MakeSegmented("SMS", 0, y);
            _smsBtn.Click += (s, e) => SetMethod("SMS");
            root.Controls.Add(_smsBtn);

            _emailBtn = MakeSegmented("Email", 160, y);
            _emailBtn.Click += (s, e) => SetMethod("Email");
            root.Controls.Add(_emailBtn);

            _sendNowBtn = MakeSegmented("Send now", RightColX, y);
            _sendNowBtn.Click += (s, e) => SetWhen(true);
            root.Controls.Add(_sendNowBtn);

            _scheduleBtn = MakeSegmented("Schedule", RightColX + 160, y);
            _scheduleBtn.Click += (s, e) => SetWhen(false);
            root.Controls.Add(_scheduleBtn);

            y += 40 + 20;

            // ---- SEND ON (only when scheduling) ----
            _sendOnLbl = Caption("SEND ON", 0, y);
            root.Controls.Add(_sendOnLbl);
            y += 22;

            _scheduledPicker = new DateTimePicker
            {
                Location = new Point(0, y),
                Width = ContentW,
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy  hh:mm tt",
                Value = DateTime.Today.AddDays(1).AddHours(9),
                Enabled = false
            };
            root.Controls.Add(_scheduledPicker);
            y += 34 + 18;

            // ---- DISCOUNT OFFER + VALID UNTIL ----
            root.Controls.Add(Caption("DISCOUNT OFFER", 0, y));
            root.Controls.Add(Caption("VALID UNTIL", RightColX, y));
            y += 22;

            _discountCombo = new ComboBox
            {
                Location = new Point(0, y),
                Width = HalfW,
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
            _discountCombo.SelectedIndexChanged += (s, e) => UpdatePreview();
            root.Controls.Add(_discountCombo);

            _validUntilPicker = new DateTimePicker
            {
                Location = new Point(RightColX, y),
                Width = HalfW,
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                Value = DateTime.Today.AddDays(30)
            };
            _validUntilPicker.ValueChanged += (s, e) => UpdatePreview();
            root.Controls.Add(_validUntilPicker);
            y += 34 + 18;

            // ---- TYPE ----
            root.Controls.Add(Caption("TYPE", 0, y));
            y += 22;

            _typeCombo = new ComboBox
            {
                Location = new Point(0, y),
                Width = ContentW,
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
            root.Controls.Add(_typeCombo);
            y += 34 + 18;

            // ---- MESSAGE PREVIEW ONLY ----
            root.Controls.Add(Caption("MESSAGE PREVIEW", 0, y));
            y += 22;

            _previewBox = new TextBox
            {
                Location = new Point(0, y),
                Width = ContentW,
                Height = 100,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                BackColor = BgCard,
                ForeColor = Navy,
                ScrollBars = ScrollBars.Vertical
            };
            root.Controls.Add(_previewBox);
            y += 106;

            // Character count (right-aligned under the preview)
            _charCount = new Label
            {
                Text = "0 characters  ·  1 SMS",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.TopRight,
                Size = new Size(240, 18),
                Location = new Point(ContentW - 240, y),
                Name = "charCount"
            };
            root.Controls.Add(_charCount);
            y += 26;

            root.Height = y + 8;

            // ---- Footer buttons (right edge lines up with the content column) ----
            int contentRight = ContentLeft + ContentW;   // 680

            var saveDraft = new Button
            {
                Text = "Save draft",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Navy,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Size = new Size(130, 44),
                Location = new Point(contentRight - 200 - 12 - 130, 16),
                Cursor = Cursors.Hand
            };
            saveDraft.FlatAppearance.BorderColor = BorderSoft;
            saveDraft.FlatAppearance.MouseOverBackColor = BgCard;
            saveDraft.Click += async (s, e) => await SaveAsync(draft: true);
            footer.Controls.Add(saveDraft);

            var sendBtn = new Button
            {
                Text = "Send follow-up",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Accent,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Size = new Size(200, 44),
                Location = new Point(contentRight - 200, 16),
                Cursor = Cursors.Hand,
                Name = "sendBtn"
            };
            sendBtn.FlatAppearance.BorderSize = 0;
            sendBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x19, 0x76, 0xD2);
            sendBtn.Click += async (s, e) => await SaveAsync(draft: false);
            footer.Controls.Add(sendBtn);

            // Initial state
            SetMethod("SMS");
            SetWhen(true);
            UpdatePreview();
        }

        // ================================================================
        //  HELPERS
        // ================================================================
        private Button MakeSegmented(string text, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(150, 40),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = BorderSoft;
            btn.FlatAppearance.MouseOverBackColor = BgCard;
            return btn;
        }

        private void SetMethod(string m)
        {
            _contactMethod = m;
            Highlight(_smsBtn, m == "SMS");
            Highlight(_emailBtn, m == "Email");
            UpdatePreview();
        }

        private void SetWhen(bool now)
        {
            _sendNow = now;
            Highlight(_sendNowBtn, now);
            Highlight(_scheduleBtn, !now);
            _scheduledPicker.Enabled = !now;
            if (_sendOnLbl != null)
                _sendOnLbl.ForeColor = now ? MutedLight : Muted;   // caption dims while disabled
            UpdatePreview();
        }

        private void Highlight(Button b, bool active)
        {
            if (active)
            {
                b.BackColor = AccentSoft;
                b.ForeColor = Accent;
                b.Font = new Font("Segoe UI Semibold", 9.5f);
                b.FlatAppearance.BorderColor = Accent;
                b.FlatAppearance.MouseOverBackColor = AccentSoft;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Navy;
                b.Font = new Font("Segoe UI", 9.5f);
                b.FlatAppearance.BorderColor = BorderSoft;
                b.FlatAppearance.MouseOverBackColor = BgCard;
            }
        }

        private void UpdateBadge()
        {
            if (_customerList == null || _badge == null) return;
            if (IsDisposed || Disposing) return;

            int n = _customerList.CheckedItems.Count;
            _badge.Text = $"{n} selected";
        }

        private void UpdatePreview()
        {
            if (_previewBox == null) return;
            if (IsDisposed || Disposing) return;

            var firstChecked = FirstCheckedCustomerName();
            var offer = _discountCombo.SelectedItem?.ToString() ?? "";
            var until = _validUntilPicker.Value.ToString("MMMM d, yyyy");

            var offerPhrase = string.IsNullOrWhiteSpace(offer) || offer == "No discount"
                ? ""
                : $" and enjoy {offer.ToLower()}";

            var text =
                $"Hi {firstChecked}, it's been a while since your last wash at AquaShine. " +
                $"We'd love to see you again{offerPhrase}, valid until {until}. " +
                $"Book your next wash today!";

            _previewBox.Text = text;

            // Update char count
            if (_charCount != null)
                _charCount.Text = $"{text.Length} characters  ·  1 SMS";
        }

        private string FirstCheckedCustomerName()
        {
            if (_customerList == null || _customerList.CheckedItems.Count == 0)
                return "there";

            var idx = _customerList.CheckedIndices[0];
            if (idx < 0 || idx >= _customers.Count) return "there";

            var full = _customers[idx].CustomerName ?? "there";
            return full.Split(' ')[0]; // first name only
        }

        // ================================================================
        //  SAVE
        // ================================================================
        private async Task SaveAsync(bool draft)
        {
            var checkedIndices = _customerList.CheckedIndices.Cast<int>().ToList();
            if (checkedIndices.Count == 0)
            {
                MessageBox.Show("Please select at least one customer.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var customerIds = checkedIndices
                .Where(i => i < _customers.Count)
                .Select(i => _customers[i].TenantCustomerId)
                .ToList();

            var offer = _discountCombo.SelectedItem?.ToString();
            if (offer == "No discount") offer = null;

            var payload = new
            {
                customerIds,
                type = _typeCombo.SelectedItem?.ToString() ?? "Service Reminder",
                contactMethod = _contactMethod,
                reason = "Repeat customer reward",
                discountOffer = offer,
                notes = _previewBox.Text,
                scheduledDate = _sendNow
                    ? DateTime.Now
                    : _scheduledPicker.Value,
                validUntil = (DateTime?)_validUntilPicker.Value,
                scheduledNow = !draft && _sendNow
            };

            try
            {
                var resp = await _http.PostAsJsonAsync("api/follow-ups/bulk", payload);

                if (resp.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        draft
                            ? "Saved as draft."
                            : _sendNow
                                ? "Follow-ups sent."
                                : "Follow-ups scheduled.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

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