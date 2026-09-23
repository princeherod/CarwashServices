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
    /// <summary>
    /// Follow-Up create / edit dialog.
    ///
    /// On CREATE:
    ///   - Customer search + checkbox list with "Select all filtered".
    ///   - Send Via: SMS / Email.
    ///   - When: Send now / Schedule.
    ///   - Send On picker enabled only when Schedule is selected.
    ///   - Discount Offer, Valid Until, Type, Message Preview.
    ///
    /// On EDIT:
    ///   - Same fields preloaded from an existing FollowUpDto.
    ///   - Status is shown read-only.
    ///   - Saving sends a PUT that updates the original record.
    /// </summary>
    public class FollowUpEditDialog : Form
    {
        // ---- Input ----
        private readonly List<TenantCustomerDto> _customers;
        private readonly FollowUpDto? _existing;      // null = create
        private readonly List<int>? _preselectedIds;
        private readonly Dictionary<int, TenantCustomerDto> _custById;

        // ---- Controls ----
        private TextBox _searchBox = null!;
        private Label _showingLbl = null!;
        private LinkLabel _selectAllFilteredLbl = null!;
        private CheckedListBox _customerList = null!;
        private Label _badge = null!;

        private Button _smsBtn = null!, _emailBtn = null!;
        private Button _sendNowBtn = null!, _scheduleBtn = null!;
        private DateTimePicker _scheduledPicker = null!;
        private Label _sendOnLbl = null!;
        private ComboBox _discountCombo = null!;
        private DateTimePicker _validUntilPicker = null!;
        private ComboBox _typeCombo = null!;
        private TextBox _previewBox = null!;
        private Label _statusLbl = null!;

        // ---- State ----
        private bool _sendNow = true;
        private string _contactMethod = "SMS";
        private bool _previewUserEdited = false;
        private List<TenantCustomerDto> _filtered = new();

        // ---- Layout ----
        private const int ContentW = 640;
        private const int ContentLeft = 40;
        private const int HalfW = 310;
        private const int RightColX = 330;

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color MutedLight = Color.FromArgb(0xB4, 0xBE, 0xD2);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentSoft = Color.FromArgb(0xD6, 0xE9, 0xFA);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color BgCard = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Faint = Color.FromArgb(0xB4, 0xBE, 0xD2);

        private readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/")
        };

        // ============================================================
        //  CTORS
        // ============================================================
        public FollowUpEditDialog(List<TenantCustomerDto> customers)
            : this(customers, null, null) { }

        public FollowUpEditDialog(List<TenantCustomerDto> customers, List<int>? preselectedCustomerIds)
            : this(customers, null, preselectedCustomerIds) { }

        public FollowUpEditDialog(List<TenantCustomerDto> customers, FollowUpDto? existing)
            : this(customers, existing, null) { }

        private FollowUpEditDialog(
            List<TenantCustomerDto> customers,
            FollowUpDto? existing,
            List<int>? preselectedCustomerIds)
        {
            _customers = customers ?? new();
            _existing = existing;
            _preselectedIds = preselectedCustomerIds;
            _custById = _customers.ToDictionary(c => c.TenantCustomerId);

            InitializeForm();

            if (_existing != null) PreloadExisting();
            else if (_preselectedIds != null && _preselectedIds.Count > 0) PreselectCustomers();

            UpdateBadge();
            UpdatePreview();
        }

        // ============================================================
        //  HELPERS
        // ============================================================
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

        // ============================================================
        //  UI
        // ============================================================
        private void InitializeForm()
        {
            bool isEdit = _existing != null;

            Text = isEdit
                ? $"Follow-Up — #{_existing!.FollowUpId}"
                : "Follow up with at-risk customers";

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

            // ---- Title + badge ----
            var title = new Label
            {
                Text = isEdit ? "Edit Follow-Up" : "Follow up with at-risk customers",
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
                Text = isEdit
                    ? "Update the details below. Status is managed automatically."
                    : "Reach out before they pass 120 days and are marked as lost.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 34;

            // ---- Status (read-only, edit only) ----
            if (isEdit)
            {
                root.Controls.Add(Caption("STATUS (READ-ONLY)", 0, y));
                y += 22;

                _statusLbl = new Label
                {
                    Text = _existing!.Status,
                    ForeColor = Navy,
                    Font = new Font("Segoe UI Semibold", 10f),
                    Location = new Point(0, y),
                    Size = new Size(ContentW, 26),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0),
                    BackColor = BgCard,
                    BorderStyle = BorderStyle.FixedSingle
                };
                root.Controls.Add(_statusLbl);
                y += 40;
            }

            // ---- Customers ----
            root.Controls.Add(Caption("CUSTOMERS *", 0, y));

            _selectAllFilteredLbl = new LinkLabel
            {
                Text = "Select all filtered",
                Font = new Font("Segoe UI", 9f),
                LinkColor = Accent,
                ActiveLinkColor = Accent,
                AutoSize = true,
                Location = new Point(ContentW - 120, y - 1),
                Cursor = Cursors.Hand,
                BackColor = Color.White,
                Visible = !isEdit   // customer can't be changed once created
            };
            _selectAllFilteredLbl.LinkClicked += (s, e) => SelectAllFiltered();
            root.Controls.Add(_selectAllFilteredLbl);
            y += 24;

            // Search box
            var searchWrap = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(ContentW, 36),
                BackColor = Color.White,
                Padding = new Padding(34, 8, 12, 0)
            };
            var searchIcon = new Label
            {
                Text = "🔍",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 11f),
                Location = new Point(8, 8),
                AutoSize = true,
                BackColor = Color.White
            };
            searchWrap.Controls.Add(searchIcon);

            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "Search by name or phone number",
                Dock = DockStyle.Top
            };
            _searchBox.TextChanged += (s, e) => { ApplyCustomerFilter(); UpdateBadge(); };
            searchWrap.Controls.Add(_searchBox);

            bool searchFocused = false;
            _searchBox.Enter += (s, e) => { searchFocused = true; searchWrap.Invalidate(); };
            _searchBox.Leave += (s, e) => { searchFocused = false; searchWrap.Invalidate(); };
            searchWrap.Resize += (s, e) => searchWrap.Invalidate();
            searchWrap.Paint += (s, e) =>
            {
                using var pen = new Pen(searchFocused ? Accent : BorderSoft);
                e.Graphics.DrawRectangle(pen, 0, 0, searchWrap.Width - 1, searchWrap.Height - 1);
            };
            root.Controls.Add(searchWrap);
            y += 42;

            // Showing X of Y
            _showingLbl = new Label
            {
                Text = "Showing 0 of 0 customers",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, y),
                AutoSize = true
            };
            root.Controls.Add(_showingLbl);
            y += 22;

            // Checked list
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
            };
            root.Controls.Add(_customerList);
            y += _customerList.Height + 12;

            root.Controls.Add(new Label
            {
                Text = "Search narrows the list instantly — no more scrolling through a thousand rows.",
                ForeColor = Faint,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 30;

            // ---- Send Via / When ----
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

            // ---- Send On ----
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
                Value = DateTime.Today.AddDays(1).AddHours(9)
            };
            root.Controls.Add(_scheduledPicker);
            y += 52;

            // ---- Discount Offer / Valid Until ----
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

            // ---- Type ----
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

            // ---- Message Preview ----
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
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                ScrollBars = ScrollBars.Vertical
            };
            _previewBox.TextChanged += (s, e) =>
            {
                if (_previewBox.Focused) _previewUserEdited = true;
            };
            root.Controls.Add(_previewBox);
            y += 110;

            root.Height = y + 8;

            // ---- Footer ----
            int contentRight = ContentLeft + ContentW;

            var saveDraft = new Button
            {
                Text = isEdit ? "Cancel" : "Save draft",
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
            if (isEdit)
                saveDraft.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            else
                saveDraft.Click += async (s, e) => await SaveAsync(draft: true);
            footer.Controls.Add(saveDraft);

            var sendBtn = new Button
            {
                Text = isEdit ? "Save changes" : "Send follow-up",
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Accent,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Size = new Size(200, 44),
                Location = new Point(contentRight - 200, 16),
                Cursor = Cursors.Hand
            };
            sendBtn.FlatAppearance.BorderSize = 0;
            sendBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x19, 0x76, 0xD2);
            sendBtn.Click += async (s, e) => await SaveAsync(draft: false);
            footer.Controls.Add(sendBtn);

            // ---- Initial visual state ----
            SetMethod("SMS");
            SetWhen(true);
            ApplyCustomerFilter();
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
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = BorderSoft;
            btn.FlatAppearance.MouseOverBackColor = BgCard;
            return btn;
        }

        // ============================================================
        //  STATE HELPERS
        // ============================================================
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

            // The picker stays enabled on both modes; it's still shown so the
            // user can see the send time. On "Send now", it just reflects
            // DateTime.Now and isn't used by the server.
            _scheduledPicker.Enabled = true;

            if (now)
            {
                _scheduledPicker.Value = DateTime.Now;
                _sendOnLbl.ForeColor = MutedLight;
            }
            else
            {
                _sendOnLbl.ForeColor = Muted;
            }

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

        // ============================================================
        //  CUSTOMER LIST
        // ============================================================
        private void ApplyCustomerFilter()
        {
            var term = (_searchBox?.Text ?? "").Trim().ToLowerInvariant();

            _filtered = string.IsNullOrEmpty(term)
                ? new List<TenantCustomerDto>(_customers)
                : _customers.Where(c =>
                    (c.CustomerName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (c.ContactNumber?.ToLowerInvariant().Contains(term) ?? false))
                .ToList();

            // Preserve current checked IDs across the refresh.
            var checkedIds = new HashSet<int>();
            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (_customerList.GetItemChecked(i) &&
                    _customerList.Items[i] is CustomerItem ci)
                {
                    checkedIds.Add(ci.CustomerId);
                }
            }

            _customerList.BeginUpdate();
            _customerList.Items.Clear();
            foreach (var c in _filtered)
            {
                _customerList.Items.Add(
                    new CustomerItem(c.TenantCustomerId,
                                     $"{c.CustomerName}  ·  {c.ContactNumber}"));
            }
            _customerList.EndUpdate();

            // Re-check any that were checked before.
            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (_customerList.Items[i] is CustomerItem ci && checkedIds.Contains(ci.CustomerId))
                    _customerList.SetItemChecked(i, true);
            }

            if (_showingLbl != null)
                _showingLbl.Text = $"Showing {_filtered.Count} of {_customers.Count} customers";
        }

        private void SelectAllFiltered()
        {
            _customerList.BeginUpdate();
            for (int i = 0; i < _customerList.Items.Count; i++)
                _customerList.SetItemChecked(i, true);
            _customerList.EndUpdate();
            UpdateBadge();
            UpdatePreview();
        }

        private void PreselectCustomers()
        {
            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (_customerList.Items[i] is CustomerItem ci &&
                    _preselectedIds!.Contains(ci.CustomerId))
                {
                    _customerList.SetItemChecked(i, true);
                }
            }
        }

        private void UpdateBadge()
        {
            if (_badge == null || _customerList == null) return;
            if (IsDisposed || Disposing) return;

            int n = _customerList.CheckedItems.Count;
            _badge.Text = $"{n} selected";
        }

        // ============================================================
        //  PREVIEW
        // ============================================================
        private void UpdatePreview()
        {
            if (_previewBox == null) return;
            if (IsDisposed || Disposing) return;

            if (_previewUserEdited) return;

            var firstChecked = FirstCheckedCustomerName();
            var offer = _discountCombo.SelectedItem?.ToString() ?? "";
            var until = _validUntilPicker.Value.ToString("MMMM d, yyyy");

            var offerPhrase = string.IsNullOrWhiteSpace(offer) || offer == "No discount"
                ? ""
                : $" and enjoy {offer.ToLower()}";

            _previewBox.Text =
                $"Hi {firstChecked}, it's been a while since your last wash at AquaShine. " +
                $"We'd love to see you again{offerPhrase}, valid until {until}. " +
                $"Book your next wash today!";
        }

        private string FirstCheckedCustomerName()
        {
            if (_customerList == null || _customerList.CheckedItems.Count == 0)
                return "there";

            if (_customerList.CheckedItems[0] is CustomerItem ci &&
                _custById.TryGetValue(ci.CustomerId, out var cust))
            {
                var full = cust.CustomerName ?? "there";
                return full.Split(' ')[0];
            }
            return "there";
        }

        // ============================================================
        //  PRELOAD EXISTING (edit)
        // ============================================================
        private void PreloadExisting()
        {
            if (_existing == null) return;

            // Contact method
            SetMethod(_existing.ContactMethod == "Email" ? "Email" : "SMS");

            // When: Scheduled vs Sent
            if (_existing.ScheduledDate > DateTime.Now &&
                string.Equals(_existing.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                SetWhen(false);
                _scheduledPicker.Value = _existing.ScheduledDate;
            }
            else
            {
                SetWhen(true);
                _scheduledPicker.Value = _existing.ScheduledDate;
            }

            // Discount offer
            if (!string.IsNullOrWhiteSpace(_existing.DiscountOffer) &&
                _discountCombo.Items.Contains(_existing.DiscountOffer))
            {
                _discountCombo.SelectedItem = _existing.DiscountOffer;
            }
            else if (string.IsNullOrWhiteSpace(_existing.DiscountOffer))
            {
                _discountCombo.SelectedItem = "No discount";
            }
            else
            {
                _discountCombo.Items.Add(_existing.DiscountOffer);
                _discountCombo.SelectedItem = _existing.DiscountOffer;
            }

            // Valid Until
            if (_existing.ValidUntil.HasValue)
                _validUntilPicker.Value = _existing.ValidUntil.Value;

            // Type
            if (!string.IsNullOrWhiteSpace(_existing.Type) &&
                _typeCombo.Items.Contains(_existing.Type))
            {
                _typeCombo.SelectedItem = _existing.Type;
            }

            // Notes / preview
            if (!string.IsNullOrWhiteSpace(_existing.Notes))
            {
                _previewBox.Text = _existing.Notes;
                _previewUserEdited = true;
            }

            // Check the customer (single since editing an existing record).
            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (_customerList.Items[i] is CustomerItem ci &&
                    ci.CustomerId == _existing.CustomerId)
                {
                    _customerList.SetItemChecked(i, true);
                    break;
                }
            }

            // Disable customer editing — you can't reassign a follow-up's customer.
            _customerList.Enabled = false;
            _searchBox.Enabled = false;
            _selectAllFilteredLbl.Visible = false;
        }

        // ============================================================
        //  SAVE
        // ============================================================
        private async Task SaveAsync(bool draft)
        {
            var checkedIndices = _customerList.CheckedIndices.Cast<int>().ToList();

            // ---- Validate ----
            if (_existing == null && checkedIndices.Count == 0)
            {
                MessageBox.Show("Please select at least one customer.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_previewBox.Text))
            {
                MessageBox.Show("The message preview cannot be empty.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Valid Until must be after today.
            if (_validUntilPicker.Value.Date < DateTime.Today)
            {
                MessageBox.Show("Valid Until cannot be in the past.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Schedule must be in the future when Schedule is chosen.
            if (!_sendNow && _scheduledPicker.Value <= DateTime.Now)
            {
                MessageBox.Show("When 'Schedule' is selected, the send date must be in the future.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var offer = _discountCombo.SelectedItem?.ToString();
            if (offer == "No discount") offer = null;

            var scheduledDate = _sendNow ? DateTime.Now : _scheduledPicker.Value;

            // ---- Edit mode ----
            if (_existing != null)
            {
                var body = new
                {
                    customerId = _existing.CustomerId,
                    type = _typeCombo.SelectedItem?.ToString() ?? "Service Reminder",
                    contactMethod = _contactMethod,
                    reason = _existing.Reason,          // preserved, not edited here
                    discountOffer = offer,
                    notes = _previewBox.Text,
                    scheduledDate,
                    validUntil = (DateTime?)_validUntilPicker.Value
                };

                try
                {
                    var resp = await _http.PutAsJsonAsync(
                        $"api/follow-ups/{_existing.FollowUpId}", body);

                    if (resp.IsSuccessStatusCode)
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                    else
                    {
                        var text = await resp.Content.ReadAsStringAsync();
                        MessageBox.Show(
                            $"Save failed.\n\n{resp.StatusCode}\n\n{text}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Save failed.\n\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // ---- Create mode ----
            var customerIds = checkedIndices
                .Where(i => i >= 0 && i < _customerList.Items.Count)
                .Select(i => ((CustomerItem)_customerList.Items[i]).CustomerId)
                .ToList();

            var payload = new
            {
                customerIds,
                type = _typeCombo.SelectedItem?.ToString() ?? "Service Reminder",
                contactMethod = _contactMethod,
                reason = "Repeat customer reward",
                discountOffer = offer,
                notes = _previewBox.Text,
                scheduledDate,
                validUntil = (DateTime?)_validUntilPicker.Value,
                scheduledNow = !draft && _sendNow
            };

            try
            {
                var resp = await _http.PostAsJsonAsync("api/follow-ups/bulk", payload);

                if (resp.IsSuccessStatusCode)
                {
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
                    catch { }

                    string msg = skipped > 0
                        ? $"{created} created, {skipped} skipped " +
                          $"(those customers already had an open follow-up)."
                        : draft
                            ? "Saved as draft."
                            : _sendNow
                                ? "Follow-ups sent."
                                : "Follow-ups scheduled.";

                    MessageBox.Show(msg, "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else if (resp.StatusCode == HttpStatusCode.Conflict)
                {
                    MessageBox.Show(
                        "None of the selected customers can receive a new follow-up.\n\n" +
                        "They all already have an open follow-up.",
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

        // ============================================================
        //  SMALL HELPER TYPE
        // ============================================================
        private sealed class CustomerItem
        {
            public int CustomerId { get; }
            public string Display { get; }
            public CustomerItem(int id, string display) { CustomerId = id; Display = display; }
            public override string ToString() => Display;
        }
    }

    public class BulkFollowUpResponse
    {
        public int Count { get; set; }
        public int Skipped { get; set; }
        public List<int> SkippedIds { get; set; } = new();
        public string? Message { get; set; }
    }
}