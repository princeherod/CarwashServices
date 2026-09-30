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
    /// Layout strategy:
    ///   ┌───────────────────────────────────────────────┐
    ///   │ Header (docked top, fixed height)             │
    ///   ├───────────────────────────────────────────────┤
    ///   │ Scroll host (docked fill)                     │
    ///   │   ┌────────────────────────────────────┐  ▓   │
    ///   │   │ Content panel (fixed 692 wide)     │      │
    ///   │   │   CUSTOMERS *   Select all  Show N │      │
    ///   │   │   ...                              │      │
    ///   │   └────────────────────────────────────┘      │
    ///   ├───────────────────────────────────────────────┤
    ///   │ Footer (docked bottom)                        │
    ///   └───────────────────────────────────────────────┘
    /// </summary>
    public class ServiceStaffFollowUpDialog : Form
    {
        private readonly List<TenantCustomerDto> _myCustomers;
        private readonly int _staffId;

        // ---- Controls ----
        private TextBox _customerSearch = null!;
        private CheckedListBox _customerList = null!;
        private LinkLabel _selectAllLink = null!;
        private TextBox _reasonTxt = null!;
        private ComboBox _discountCombo = null!;
        private DateTimePicker _validUntilPicker = null!;
        private ComboBox _typeCombo = null!;
        private TextBox _previewBox = null!;
        private DateTimePicker _scheduledPicker = null!;
        private Button _sendNowBtn = null!, _scheduleBtn = null!;
        private bool _sendNow = false;
        private Label _badge = null!;
        private Label _sendOnLbl = null!;
        private Label _showingLbl = null!;

        // ---- Filtering state ----
        // The "Select all" link operates on the *currently visible* rows.
        // We keep a reference to the filtered list so we can iterate it
        // without re-querying the backing data source.
        private List<TenantCustomerDto> _filtered = new();
        private bool _suppressItemCheck = false;

        // ---- Palette ----
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Faint = Color.FromArgb(0x9A, 0xA7, 0xBF);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentSoft = Color.FromArgb(0xD6, 0xE9, 0xFA);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);

        // ================================================================
        //  Layout
        // ================================================================
        private const int DialogW = 800;
        private const int DialogH = 760;

        private const int Gutter = 44;
        private const int ContentW = DialogW - Gutter * 2 - 20;   // 692

        private const int HeaderH = 108;
        private const int FooterH = 76;

        private const int Gap = 20;
        private const int ColW = (ContentW - Gap) / 2;

        private const int LabelToField = 26;
        private const int SectionGap = 28;

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

            // ============================================================
            //  Header — docked top
            // ============================================================
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderH,
                BackColor = Color.White
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Controls.Add(header);

            header.Controls.Add(new Label
            {
                Text = "New Follow-Up",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 17f),
                Location = new Point(Gutter, 26),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            _badge = new Label
            {
                Text = "0 selected",
                ForeColor = Accent,
                Font = new Font("Segoe UI Semibold", 9f),
                BackColor = AccentSoft,
                Padding = new Padding(10, 5, 10, 5),
                AutoSize = true,
                Location = new Point(Gutter + 210, 30)
            };
            header.Controls.Add(_badge);

            header.Controls.Add(new Label
            {
                Text = "Pick customers from your assigned list. Your manager will approve this before it can be sent.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(Gutter, 68),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            // ============================================================
            //  Footer — docked bottom
            // ============================================================
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = FooterH,
                BackColor = Color.White
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            Controls.Add(footer);

            var submit = new Button
            {
                Text = "Submit for Approval",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Accent,
                Size = new Size(200, 44),
                Location = new Point(DialogW - Gutter - 200, 16),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
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
                Location = new Point(submit.Left - 12 - 110, 16),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                UseVisualStyleBackColor = false
            };
            cancel.FlatAppearance.BorderColor = BorderSoft;
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancel);

            CancelButton = cancel;

            // ============================================================
            //  Scroll host — docked fill
            // ============================================================
            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = Padding.Empty
            };
            Controls.Add(scrollHost);
            scrollHost.BringToFront();

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true
            };
            scrollHost.Controls.Add(scrollPanel);

            // ============================================================
            //  Content panel
            // ============================================================
            int contentTopPad = 24;

            var content = new Panel
            {
                Location = new Point(Gutter, contentTopPad),
                Width = ContentW,
                Height = 4000,
                BackColor = Color.White
            };
            scrollPanel.Controls.Add(content);

            int y = 0;

            // ------------------------------------------------------------
            //  CUSTOMERS section header row
            //    left   : CUSTOMERS *
            //    middle : Select all / Unselect all  (LinkLabel)
            //    right  : Showing N of M
            // ------------------------------------------------------------
            content.Controls.Add(Cap("CUSTOMERS *", 0, y));

            _selectAllLink = new LinkLabel
            {
                Text = "Select all",
                Font = new Font("Segoe UI", 9f),
                LinkColor = Accent,
                ActiveLinkColor = Accent,
                VisitedLinkColor = Accent,
                AutoSize = true,
                Location = new Point(140, y + 1),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            _selectAllLink.LinkClicked += (s, e) => ToggleSelectAllVisible();
            content.Controls.Add(_selectAllLink);

            _showingLbl = new Label
            {
                Text = "",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(ContentW - 200, y + 2),
                Size = new Size(200, 16),
                TextAlign = ContentAlignment.TopRight,
                AutoSize = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            content.Controls.Add(_showingLbl);

            y += LabelToField;

            // Search bar
            var searchWrap = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(ContentW, 36),
                BackColor = Color.White,
                Padding = new Padding(30, 8, 12, 0)
            };
            searchWrap.Paint += (s, e) =>
            {
                using var pen = new Pen(searchWrap.Focused ? Accent : BorderSoft);
                e.Graphics.DrawRectangle(pen, 0, 0, searchWrap.Width - 1, searchWrap.Height - 1);
            };
            content.Controls.Add(searchWrap);

            var searchIcon = new Label
            {
                Text = "🔍",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 11f),
                Location = new Point(8, 8),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            searchWrap.Controls.Add(searchIcon);

            _customerSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.White,
                ForeColor = Navy,
                PlaceholderText = "Search by name or phone number...",
                Dock = DockStyle.Top
            };
            searchWrap.Controls.Add(_customerSearch);

            _customerSearch.TextChanged += (s, e) =>
            {
                ApplyCustomerFilter();
                UpdateBadge();
                UpdateSelectAllLabel();
            };
            _customerSearch.Enter += (s, e) => searchWrap.Invalidate();
            _customerSearch.Leave += (s, e) => searchWrap.Invalidate();

            y += 36 + 12;

            // Customer list host
            var listHost = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(ContentW, 160),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            content.Controls.Add(listHost);

            _customerList = new CheckedListBox
            {
                Location = new Point(1, 1),
                Size = new Size(ContentW - 2, 158),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                IntegralHeight = false,
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            _customerList.ItemCheck += (s, e) =>
            {
                if (_suppressItemCheck) return;
                if (_customerList.IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        UpdateBadge();
                        UpdateSelectAllLabel();
                    }));
                }
            };
            listHost.Controls.Add(_customerList);

            y += 160;
            y += SectionGap;

            // ------------------------------------------------------------
            //  WHEN
            // ------------------------------------------------------------
            content.Controls.Add(Cap("WHEN *", 0, y));
            y += LabelToField;

            _sendNowBtn = MakeSegmented("Send Now (needs approval)", 0, y, ColW);
            _sendNowBtn.Click += (s, e) => SetWhen(true);
            content.Controls.Add(_sendNowBtn);

            _scheduleBtn = MakeSegmented("Schedule", ColW + Gap, y, ColW);
            _scheduleBtn.Click += (s, e) => SetWhen(false);
            content.Controls.Add(_scheduleBtn);

            y += 40;
            y += SectionGap;

            // ------------------------------------------------------------
            //  SEND ON
            // ------------------------------------------------------------
            _sendOnLbl = Cap("SEND ON", 0, y);
            content.Controls.Add(_sendOnLbl);
            y += LabelToField;

            _scheduledPicker = new DateTimePicker
            {
                Location = new Point(0, y),
                Width = ContentW,
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy  hh:mm tt",
                Value = DateTime.Today.AddDays(1).AddHours(9),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            content.Controls.Add(_scheduledPicker);

            y += 32;
            y += SectionGap;

            // ------------------------------------------------------------
            //  REASON
            // ------------------------------------------------------------
            content.Controls.Add(Cap("REASON *", 0, y));
            y += LabelToField;

            _reasonTxt = new TextBox
            {
                Location = new Point(0, y),
                Width = ContentW,
                Height = 76,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                PlaceholderText = "Why is this follow-up being created?",
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            content.Controls.Add(_reasonTxt);

            y += 76;
            y += SectionGap;

            // ------------------------------------------------------------
            //  DISCOUNT OFFER  |  VALID UNTIL
            // ------------------------------------------------------------
            content.Controls.Add(Cap("DISCOUNT OFFER", 0, y));
            content.Controls.Add(Cap("VALID UNTIL", ColW + Gap, y));
            y += LabelToField;

            _discountCombo = new ComboBox
            {
                Location = new Point(0, y),
                Width = ColW,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
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
            content.Controls.Add(_discountCombo);

            _validUntilPicker = new DateTimePicker
            {
                Location = new Point(ColW + Gap, y),
                Width = ColW,
                Font = new Font("Segoe UI", 10f),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                Value = DateTime.Today.AddDays(30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            content.Controls.Add(_validUntilPicker);

            y += 32;
            y += SectionGap;

            // ------------------------------------------------------------
            //  TYPE
            // ------------------------------------------------------------
            content.Controls.Add(Cap("TYPE", 0, y));
            y += LabelToField;

            _typeCombo = new ComboBox
            {
                Location = new Point(0, y),
                Width = ContentW,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _typeCombo.Items.AddRange(new object[]
            {
                "Promotional Offer",
                "Service Reminder",
                "Post-Service Feedback",
                "Renewal"
            });
            _typeCombo.SelectedIndex = 0;
            content.Controls.Add(_typeCombo);

            y += 32;
            y += SectionGap;

            // ------------------------------------------------------------
            //  MESSAGE PREVIEW
            // ------------------------------------------------------------
            content.Controls.Add(Cap("MESSAGE PREVIEW", 0, y));
            y += LabelToField;

            _previewBox = new TextBox
            {
                Location = new Point(0, y),
                Width = ContentW,
                Height = 110,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            content.Controls.Add(_previewBox);

            y += 110;
            y += 24;

            content.Height = y;

            scrollPanel.AutoScrollMinSize = new Size(
                ContentW + Gutter * 2 + SystemInformation.VerticalScrollBarWidth + 4,
                contentTopPad + y + contentTopPad);

            // ---- Initial state ----
            ApplyCustomerFilter();
            SetWhen(false);
            UpdateBadge();
            UpdateSelectAllLabel();
        }

        // ================================================================
        //  Builders
        // ================================================================
        private static Label Cap(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true,
            BackColor = Color.Transparent
        };

        private Button MakeSegmented(string text, int x, int y, int width)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(width, 40),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            b.FlatAppearance.BorderColor = BorderSoft;
            return b;
        }

        // ================================================================
        //  Customer filter
        // ================================================================
        private void ApplyCustomerFilter()
        {
            var term = (_customerSearch?.Text ?? "").Trim().ToLowerInvariant();

            _filtered = string.IsNullOrEmpty(term)
                ? new List<TenantCustomerDto>(_myCustomers)
                : _myCustomers.Where(c =>
                    (c.CustomerName?.ToLowerInvariant().Contains(term) ?? false) ||
                    (c.ContactNumber?.ToLowerInvariant().Contains(term) ?? false))
                .ToList();

            // Remember what was checked before the refresh
            var checkedIds = new HashSet<int>();
            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (_customerList.GetItemChecked(i) && _customerList.Items[i] is CustomerItem ci)
                    checkedIds.Add(ci.CustomerId);
            }

            _suppressItemCheck = true;
            _customerList.BeginUpdate();
            _customerList.Items.Clear();
            foreach (var c in _filtered)
                _customerList.Items.Add(new CustomerItem(c.TenantCustomerId,
                    $"{c.CustomerName}  ·  {c.ContactNumber}"));
            _customerList.EndUpdate();

            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (_customerList.Items[i] is CustomerItem ci && checkedIds.Contains(ci.CustomerId))
                    _customerList.SetItemChecked(i, true);
            }
            _suppressItemCheck = false;

            if (_showingLbl != null)
                _showingLbl.Text = $"Showing {_filtered.Count} of {_myCustomers.Count}";
        }

        // ================================================================
        //  Select all / Unselect all
        // ================================================================
        private bool AreAllVisibleChecked()
        {
            if (_customerList.Items.Count == 0) return false;

            for (int i = 0; i < _customerList.Items.Count; i++)
            {
                if (!_customerList.GetItemChecked(i)) return false;
            }
            return true;
        }

        private void UpdateSelectAllLabel()
        {
            if (_selectAllLink == null) return;

            if (_customerList.Items.Count == 0)
            {
                _selectAllLink.Visible = false;
                return;
            }

            _selectAllLink.Visible = true;
            _selectAllLink.Text = AreAllVisibleChecked() ? "Unselect all" : "Select all";
        }

        private void ToggleSelectAllVisible()
        {
            if (_customerList.Items.Count == 0) return;

            bool allChecked = AreAllVisibleChecked();
            bool target = !allChecked;

            _suppressItemCheck = true;
            _customerList.BeginUpdate();
            try
            {
                for (int i = 0; i < _customerList.Items.Count; i++)
                    _customerList.SetItemChecked(i, target);
            }
            finally
            {
                _customerList.EndUpdate();
                _suppressItemCheck = false;
            }

            UpdateBadge();
            UpdateSelectAllLabel();
        }

        // ================================================================
        //  When toggle
        // ================================================================
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
            _sendOnLbl.ForeColor = sendNow ? Faint : Muted;
        }

        private void UpdateBadge()
        {
            _badge.Text = $"{_customerList.CheckedItems.Count} selected";
        }

        // ================================================================
        //  Submit
        // ================================================================
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

                var resp = await _http.PostAsJsonAsync($"api/follow-ups/bulk?companyId={CarwashServices.Auth.SessionUser.CurrentCompanyId}", payload);

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