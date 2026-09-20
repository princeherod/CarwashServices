using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    public class FollowUpEditDialog : Form
    {
        private readonly List<TenantCustomerDto> _customers;
        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/")
        };

        // Controls
        private CheckedListBox _customerList = null!;
        private CheckBox _selectAll = null!;
        private Button _smsBtn = null!, _emailBtn = null!;
        private Button _sendNowBtn = null!, _scheduleBtn = null!;
        private DateTimePicker _scheduledPicker = null!;
        private DateTimePicker _validUntilPicker = null!;
        private ComboBox _discountCombo = null!;
        private ComboBox _typeCombo = null!;
        private TextBox _previewBox = null!;

        // UI-only references
        private Label _badge = null!;
        private Label _sendOnLbl = null!;
        private Label _charCount = null!;

        private bool _sendNow = true;
        private string _contactMethod = "SMS";
        private bool _previewUserEdited = false;

        // Layout
        private const int ContentW = 640;
        private const int ContentLeft = 40;
        private const int HalfW = 310;
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

        public FollowUpEditDialog(List<TenantCustomerDto> customers, List<int>? preselectedCustomerIds)
        {
            _customers = customers ?? new();
            _preselectedIds = new HashSet<int>(preselectedCustomerIds ?? new List<int>());
            InitializeForm();

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
            _previewUserEdited = false;

            Text = "Follow up with at-risk customers";

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

            var scroller = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };
            Controls.Add(scroller);

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White
            };
            Controls.Add(footer);
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BorderSoft });

            var root = new Panel
            {
                Location = new Point(ContentLeft, 24),
                Width = ContentW,
                BackColor = Color.White
            };
            scroller.Controls.Add(root);

            int y = 0;

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
                Location = new Point(title.PreferredWidth + 14, y + 4)
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

            root.Controls.Add(Caption("CUSTOMERS *", 0, y));

            _selectAll = new CheckBox
            {
                Text = "Select all",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Navy,
                Location = new Point(ContentW - 88, y - 4),
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

            y += 60;

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
            y += 52;

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
            y += 52;

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
            y += 52;

            root.Controls.Add(Caption("MESSAGE PREVIEW", 0, y));

            var resetLbl = new LinkLabel
            {
                Text = "Reset to template",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(ContentW - 130, y - 1),
                AutoSize = true,
                Cursor = Cursors.Hand,
                BackColor = Color.White,
                LinkColor = Accent,
                ActiveLinkColor = Accent
            };
            resetLbl.LinkClicked += (s, e) =>
            {
                _previewUserEdited = false;
                UpdatePreview();
            };
            root.Controls.Add(resetLbl);
            y += 22;

            _previewBox = new TextBox
            {
                Location = new Point(0, y),
                Width = ContentW,
                Height = 100,
                Multiline = true,
                ReadOnly = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                ScrollBars = ScrollBars.Vertical
            };
            _previewBox.TextChanged += (s, e) =>
            {
                if (_previewBox.Focused) _previewUserEdited = true;
                UpdateCharCount();
            };
            root.Controls.Add(_previewBox);
            y += 106;

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

            int contentRight = ContentLeft + ContentW;

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

            SetMethod("SMS");
            SetWhen(true);
            UpdatePreview();
        }

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
                _sendOnLbl.ForeColor = now ? MutedLight : Muted;
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

            if (_previewUserEdited)
            {
                UpdateCharCount();
                return;
            }

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
            UpdateCharCount();
        }

        private void UpdateCharCount()
        {
            if (_charCount == null || _previewBox == null) return;
            _charCount.Text = $"{_previewBox.Text.Length} characters  ·  1 SMS";
        }

        private string FirstCheckedCustomerName()
        {
            if (_customerList == null || _customerList.CheckedItems.Count == 0)
                return "there";

            var idx = _customerList.CheckedIndices[0];
            if (idx < 0 || idx >= _customers.Count) return "there";

            var full = _customers[idx].CustomerName ?? "there";
            return full.Split(' ')[0];
        }

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
                    // Parse the response so we can show "X created, Y skipped".
                    int created = 0, skipped = 0;
                    try
                    {
                        var parsed = await resp.Content.ReadFromJsonAsync<BulkFollowUpResponse>();
                        if (parsed != null)
                        {
                            created = parsed.Count;
                            skipped = parsed.Skipped;
                        }
                    }
                    catch { /* non-fatal */ }

                    string msg;
                    if (skipped > 0)
                    {
                        msg = $"{created} created, {skipped} skipped " +
                              $"(those customers already had an open follow-up).";
                    }
                    else
                    {
                        msg = draft
                            ? "Saved as draft."
                            : _sendNow
                                ? "Follow-ups sent."
                                : "Follow-ups scheduled.";
                    }

                    MessageBox.Show(msg, "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else if (resp.StatusCode == HttpStatusCode.Conflict)
                {
                    MessageBox.Show(
                        "None of the selected customers can receive a new follow-up.\n\n" +
                        "They all already have an open follow-up " +
                        "(Pending, Scheduled, Sent, or Redeemed).\n\n" +
                        "Wait for those to expire or resolve before sending again.",
                        "Already Contacted",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    // ---- Response shape from POST api/follow-ups/bulk ----
    public class BulkFollowUpResponse
    {
        public int Count { get; set; }
        public int Skipped { get; set; }
        public List<int> SkippedIds { get; set; } = new();
        public string? Message { get; set; }
    }
}