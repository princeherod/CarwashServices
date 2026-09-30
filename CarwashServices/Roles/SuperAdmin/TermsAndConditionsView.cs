using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Alias for TermsAndConditionsView to match exact naming requested.
    /// </summary>
    public class TermsConditionsView : TermsAndConditionsView
    {
        public TermsConditionsView() : base(isReadOnly: false)
        {
        }

        public TermsConditionsView(bool isReadOnly) : base(isReadOnly)
        {
        }
    }

    /// <summary>
    /// Read-only Terms & Conditions UserControl for the Admin panel.
    /// </summary>
    public class AdminTermsAndConditionsView : TermsAndConditionsView
    {
        public AdminTermsAndConditionsView() : base(isReadOnly: true)
        {
        }
    }

    /// <summary>
    /// Terms & Conditions module.
    /// Left card: "Version History" listing versions from GET /api/terms (newest first)
    ///            with a "Latest" pill on the top row; clicking a row loads it on the right.
    /// Right card: Read-only monospace box with terms_id/effective_date/created_by as meta line.
    /// Footer: In Super Admin mode, displays "Preview" and "Publish New Version".
    ///         In Admin mode (read-only), "Preview" and "Publish New Version" are hidden.
    /// </summary>
    public class TermsAndConditionsView : UserControl
    {
        private readonly bool _isReadOnly;

        // ================================================================
        //  Palette
        // ================================================================
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color NavyHover = Color.FromArgb(0x16, 0x2A, 0x5C);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Purple = Color.FromArgb(0x7C, 0x3A, 0xED);
        private static readonly Color PurpleHover = Color.FromArgb(0x6D, 0x28, 0xD9);
        private static readonly Color PurpleSoft = Color.FromArgb(0xF3, 0xE8, 0xFF);
        private static readonly Color PurpleBorder = Color.FromArgb(0xDD, 0xD6, 0xFE);
        private static readonly Color Green = Color.FromArgb(0x15, 0x80, 0x3D);
        private static readonly Color GreenSoft = Color.FromArgb(0xDC, 0xFC, 0xE7);
        private static readonly Color GreenBorder = Color.FromArgb(0x86, 0xEF, 0xAC);
        private static readonly Color Blue = Color.FromArgb(0x02, 0x84, 0xC7);
        private static readonly Color BlueSoft = Color.FromArgb(0xE0, 0xF2, 0xFE);
        private static readonly Color CodeBg = Color.FromArgb(0x0F, 0x17, 0x2A);
        private static readonly Color CodeFg = Color.FromArgb(0xF1, 0xF5, 0xF9);
        private static readonly Color RowHover = Color.FromArgb(0xF8, 0xFA, 0xFC);
        private static readonly Color RowSelected = Color.FromArgb(0xF3, 0xE8, 0xFF);

        // ================================================================
        //  HTTP
        // ================================================================
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // ================================================================
        //  State & Controls
        // ================================================================
        private Panel _scrollContainer = null!;
        private Panel _leftCard = null!;
        private Panel _rightCard = null!;

        // Left card controls
        private Label _leftDescLbl = null!;
        private Panel _historyListContainer = null!;
        private Label _loadingHistoryLbl = null!;

        // Right card controls
        private Label _docTitleLbl = null!;
        private Panel _metaBar = null!;
        private Label _metaTermsId = null!;
        private Label _metaEffective = null!;
        private Label _metaCreatedBy = null!;
        private Panel _codeBoxContainer = null!;
        private TextBox _contentTextBox = null!;
        private Label _rightFooterStatus = null!;
        private Button _previewBtn = null!;
        private Button _publishBtn = null!;

        private List<TermsItemDto> _termsList = new();
        private TermsItemDto? _selectedTerms;
        private readonly List<Panel> _rowPanels = new();

        public TermsAndConditionsView() : this(isReadOnly: SessionUser.RoleId != 1 && SessionUser.Role != UserRole.SuperAdmin)
        {
        }

        public TermsAndConditionsView(bool isReadOnly)
        {
            // Admin is read-only; Super Admin (Role 1) gets full access
            _isReadOnly = isReadOnly || (SessionUser.RoleId != 1 && SessionUser.Role != UserRole.SuperAdmin);

            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            // Role guard: reachable by Admin and Super Admin
            if (SessionUser.Role != UserRole.SuperAdmin && SessionUser.Role != UserRole.Admin)
            {
                Controls.Add(new AccessDeniedView("Terms & Conditions", "Super Admin (Role 1) or Admin (Role 2)"));
                return;
            }

            InitializeComponent();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadTermsAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            _scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                AutoScroll = true,
                Padding = new Padding(36, 16, 36, 24)
            };
            Controls.Add(_scrollContainer);

            // ---- Title ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = "Terms & Conditions",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(36, 20),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Subtitle ----
            _scrollContainer.Controls.Add(new Label
            {
                Text = _isReadOnly
                    ? SuperAdminLabels.TermsAndConditionsAdminSubtitle
                    : SuperAdminLabels.TermsAndConditionsSubtitle,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(36, 64),
                AutoSize = true,
                UseMnemonic = false
            });

            // ---- Two Cards ----
            _leftCard = CreateCardPanel();
            _rightCard = CreateCardPanel();

            _scrollContainer.Controls.Add(_leftCard);
            _scrollContainer.Controls.Add(_rightCard);

            RelayoutCards();

            BuildLeftCard();
            BuildRightCard();

            _leftCard.Resize += (s, e) => RelayoutLeftCard();
            _rightCard.Resize += (s, e) => RelayoutRightCard();

            RelayoutLeftCard();
            RelayoutRightCard();

            _scrollContainer.Resize += (s, e) => RelayoutCards();

            ResumeLayout(true);
        }

        private static Panel CreateCardPanel()
        {
            var p = new Panel
            {
                BackColor = Color.White
            };
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using var path = RoundedRect(rect, 12);
                using var borderPen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            };
            return p;
        }

        private void RelayoutCards()
        {
            int top = 106;
            int padX = 36;
            int gap = 24;
            int availableWidth = Math.Max(720, _scrollContainer.ClientSize.Width - (padX * 2));
            int availableHeight = Math.Max(600, _scrollContainer.ClientSize.Height - top - 24);

            int leftWidth = 340;
            int rightWidth = Math.Max(380, availableWidth - leftWidth - gap);

            _leftCard.SetBounds(padX, top, leftWidth, availableHeight);
            _rightCard.SetBounds(padX + leftWidth + gap, top, rightWidth, availableHeight);
        }

        // ================================================================
        //  LEFT CARD: Version History
        // ================================================================
        private void BuildLeftCard()
        {
            _leftCard.SuspendLayout();

            // Card Title
            var titleLbl = new Label
            {
                Text = "Version History",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(22, 20),
                AutoSize = true,
                UseMnemonic = false
            };
            _leftCard.Controls.Add(titleLbl);

            _leftDescLbl = new Label
            {
                Text = "Historical revisions from newest to oldest. Select any entry to inspect its contents.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.8f),
                Location = new Point(22, 50),
                Size = new Size(Math.Max(100, _leftCard.Width - 44), 36),
                UseMnemonic = false
            };
            _leftCard.Controls.Add(_leftDescLbl);

            // Container for list of version rows
            _historyListContainer = new Panel
            {
                AutoScroll = true,
                BackColor = Color.White
            };
            _leftCard.Controls.Add(_historyListContainer);

            _loadingHistoryLbl = new Label
            {
                Text = "Loading version history...",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _historyListContainer.Controls.Add(_loadingHistoryLbl);

            _leftCard.ResumeLayout(true);
        }

        private void RelayoutLeftCard()
        {
            if (_leftDescLbl != null)
                _leftDescLbl.Width = Math.Max(100, _leftCard.Width - 44);

            if (_historyListContainer != null)
            {
                int top = 92;
                int h = Math.Max(100, _leftCard.Height - top - 18);
                _historyListContainer.SetBounds(16, top, Math.Max(100, _leftCard.Width - 32), h);
            }
        }

        // ================================================================
        //  RIGHT CARD: Read-only Monospace Box + Meta Line + Footer
        // ================================================================
        private void BuildRightCard()
        {
            _rightCard.SuspendLayout();

            // Document Header Title
            _docTitleLbl = new Label
            {
                Text = "Terms of Service",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(24, 20),
                AutoSize = true,
                UseMnemonic = false
            };
            _rightCard.Controls.Add(_docTitleLbl);

            // Meta line container
            _metaBar = new Panel
            {
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
                Location = new Point(24, 52),
                Height = 36
            };
            _metaBar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _metaBar.Width - 1, _metaBar.Height - 1);
                using var path = RoundedRect(rect, 6);
                using var pen = new Pen(CardBorder, 1f);
                e.Graphics.DrawPath(pen, path);
            };

            _metaTermsId = new Label
            {
                Text = $"{SuperAdminLabels.MetaTermsId}--",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 8.8f),
                Location = new Point(14, 9),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _metaBar.Controls.Add(_metaTermsId);

            _metaEffective = new Label
            {
                Text = $"{SuperAdminLabels.MetaEffectiveDate}--",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.8f),
                Location = new Point(130, 9),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _metaBar.Controls.Add(_metaEffective);

            _metaCreatedBy = new Label
            {
                Text = $"{SuperAdminLabels.MetaCreatedBy}--",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.8f),
                Location = new Point(320, 9),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _metaBar.Controls.Add(_metaCreatedBy);

            _rightCard.Controls.Add(_metaBar);

            // Monospace code box container
            _codeBoxContainer = new Panel
            {
                BackColor = CodeBg
            };
            _codeBoxContainer.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _codeBoxContainer.Width - 1, _codeBoxContainer.Height - 1);
                using var path = RoundedRect(rect, 8);
                using var pen = new Pen(Color.FromArgb(0x33, 0x41, 0x55), 1.2f);
                e.Graphics.DrawPath(pen, path);
            };

            _contentTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = CodeBg,
                ForeColor = CodeFg,
                Font = new Font("Consolas", 9.75f),
                BorderStyle = BorderStyle.None,
                WordWrap = true
            };
            _codeBoxContainer.Controls.Add(_contentTextBox);
            _rightCard.Controls.Add(_codeBoxContainer);

            // Bottom Footer
            _rightFooterStatus = new Label
            {
                Text = "Select a version to inspect terms",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 8.8f),
                AutoSize = true,
                UseMnemonic = false
            };
            _rightCard.Controls.Add(_rightFooterStatus);

            _previewBtn = new Button
            {
                Text = SuperAdminLabels.ButtonPreview,
                Font = new Font("Segoe UI Semibold", 9.2f),
                ForeColor = Navy,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(116, 38),
                Visible = !_isReadOnly
            };
            _previewBtn.FlatAppearance.BorderColor = CardBorder;
            _previewBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF8, 0xFA, 0xFC);
            _previewBtn.Click += OnPreviewClicked;
            _rightCard.Controls.Add(_previewBtn);

            _publishBtn = new Button
            {
                Text = SuperAdminLabels.ButtonPublishNewVersion,
                Font = new Font("Segoe UI Semibold", 9.2f),
                ForeColor = Color.White,
                BackColor = Purple,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Size = new Size(190, 38),
                Visible = !_isReadOnly
            };
            _publishBtn.FlatAppearance.BorderSize = 0;
            _publishBtn.FlatAppearance.MouseOverBackColor = PurpleHover;
            _publishBtn.Click += OnPublishNewVersionClicked;
            _rightCard.Controls.Add(_publishBtn);

            _rightCard.ResumeLayout(true);
        }

        private void RelayoutRightCard()
        {
            if (_metaBar != null)
            {
                _metaBar.Width = Math.Max(200, _rightCard.Width - 48);
            }

            int footerY = Math.Max(200, _rightCard.Height - 54);

            if (!_isReadOnly)
            {
                if (_publishBtn != null)
                {
                    _publishBtn.Visible = true;
                    _publishBtn.Width = 190;
                    _publishBtn.Location = new Point(_rightCard.Width - 24 - _publishBtn.Width, footerY);
                }

                if (_previewBtn != null && _publishBtn != null)
                {
                    _previewBtn.Visible = true;
                    _previewBtn.Location = new Point(_publishBtn.Left - 10 - _previewBtn.Width, footerY);
                }

                if (_rightFooterStatus != null && _previewBtn != null)
                {
                    _rightFooterStatus.AutoEllipsis = true;
                    _rightFooterStatus.Location = new Point(24, footerY + 10);
                    _rightFooterStatus.Width = Math.Max(50, _previewBtn.Left - 36);
                }
            }
            else
            {
                if (_previewBtn != null) _previewBtn.Visible = false;
                if (_publishBtn != null) _publishBtn.Visible = false;

                if (_rightFooterStatus != null)
                {
                    _rightFooterStatus.AutoEllipsis = true;
                    _rightFooterStatus.Location = new Point(24, footerY + 10);
                    _rightFooterStatus.Width = Math.Max(50, _rightCard.Width - 48);
                }
            }

            if (_codeBoxContainer != null)
            {
                int top = 98;
                int h = Math.Max(120, footerY - top - 14);
                int w = Math.Max(200, _rightCard.Width - 48);
                _codeBoxContainer.SetBounds(24, top, w, h);

                if (_contentTextBox != null)
                {
                    _contentTextBox.SetBounds(14, 14, Math.Max(50, w - 28), Math.Max(50, h - 28));
                }
            }
        }

        // ================================================================
        //  DATA LOADING
        // ================================================================
        public async Task LoadTermsAsync(int? selectTermsId = null)
        {
            try
            {
                _loadingHistoryLbl.Visible = true;
                _loadingHistoryLbl.Text = "Loading versions...";

                var list = await _http.GetFromJsonAsync<List<TermsItemDto>>("api/terms");
                _termsList = list ?? new List<TermsItemDto>();

                RenderHistoryList(selectTermsId);
            }
            catch (Exception ex)
            {
                _loadingHistoryLbl.Visible = true;
                _loadingHistoryLbl.Text = $"Failed to load terms:\n{ex.Message}";
            }
        }

        private void RenderHistoryList(int? selectTermsId = null)
        {
            _historyListContainer.SuspendLayout();
            try
            {
                _rowPanels.Clear();
                _historyListContainer.Controls.Clear();

                if (_termsList.Count == 0)
                {
                    _loadingHistoryLbl.Visible = true;
                    _loadingHistoryLbl.Text = "No terms & conditions found.";
                    _historyListContainer.Controls.Add(_loadingHistoryLbl);
                    return;
                }

                _loadingHistoryLbl.Visible = false;

                int y = 4;
                int rowWidth = Math.Max(220, _historyListContainer.ClientSize.Width - 8);

                for (int i = 0; i < _termsList.Count; i++)
                {
                    var item = _termsList[i];
                    bool isLatest = (i == 0);

                    var row = CreateVersionRowPanel(item, isLatest, rowWidth);
                    row.Location = new Point(4, y);
                    _historyListContainer.Controls.Add(row);
                    _rowPanels.Add(row);

                    y += row.Height + 8;
                }

                // Auto-select latest or requested
                TermsItemDto? toSelect = null;
                if (selectTermsId.HasValue)
                {
                    toSelect = _termsList.FirstOrDefault(t => t.TermsId == selectTermsId.Value);
                }
                toSelect ??= _termsList.FirstOrDefault();

                if (toSelect != null)
                {
                    SelectTerms(toSelect);
                }
            }
            finally
            {
                _historyListContainer.ResumeLayout(true);
            }
        }

        private Panel CreateVersionRowPanel(TermsItemDto item, bool isLatest, int width)
        {
            var p = new Panel
            {
                Size = new Size(width, 74),
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Tag = item
            };

            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using var path = RoundedRect(rect, 8);

                bool isSel = (_selectedTerms?.TermsId == item.TermsId);
                Color borderClr = isSel ? Purple : CardBorder;
                float borderW = isSel ? 1.8f : 1f;

                using var pen = new Pen(borderClr, borderW);
                e.Graphics.DrawPath(pen, path);

                if (isSel)
                {
                    // Accent bar on left edge
                    using var accentBrush = new SolidBrush(Purple);
                    e.Graphics.FillRectangle(accentBrush, 0, 8, 4, p.Height - 16);
                }
            };

            // Version Label
            var versionLbl = new Label
            {
                Text = item.Version,
                Font = new Font("Segoe UI Semibold", 11.5f),
                ForeColor = Navy,
                Location = new Point(14, 10),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            p.Controls.Add(versionLbl);

            // Latest Pill (top row)
            if (isLatest)
            {
                var latestPill = new Panel
                {
                    Size = new Size(58, 22),
                    Location = new Point(versionLbl.Right + 12, 11),
                    BackColor = GreenSoft,
                    Cursor = Cursors.Hand
                };
                latestPill.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = new Rectangle(0, 0, latestPill.Width - 1, latestPill.Height - 1);
                    using var path = RoundedRect(rect, 10);
                    using var pen = new Pen(GreenBorder, 1f);
                    e.Graphics.DrawPath(pen, path);
                };

                var latestLbl = new Label
                {
                    Text = "Latest",
                    Font = new Font("Segoe UI Semibold", 8f),
                    ForeColor = Green,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                latestPill.Controls.Add(latestLbl);
                p.Controls.Add(latestPill);

                latestPill.Click += (s, e) => SelectTerms(item);
                latestLbl.Click += (s, e) => SelectTerms(item);
            }

            // Effective Date line
            var dateLbl = new Label
            {
                Text = $"{SuperAdminLabels.PrefixEffectiveDate}{item.EffectiveDateFormatted}",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Muted,
                Location = new Point(14, 34),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            p.Controls.Add(dateLbl);

            // Author line
            var authorLbl = new Label
            {
                Text = $"{SuperAdminLabels.PrefixCreatedBy}{item.CreatedByName}",
                Font = new Font("Segoe UI", 8.2f),
                ForeColor = Color.FromArgb(0x94, 0xA3, 0xB8),
                Location = new Point(14, 52),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            p.Controls.Add(authorLbl);

            // Click & Hover bindings
            void HandleClick(object? s, EventArgs e) => SelectTerms(item);

            p.Click += HandleClick;
            versionLbl.Click += HandleClick;
            dateLbl.Click += HandleClick;
            authorLbl.Click += HandleClick;

            p.MouseEnter += (s, e) =>
            {
                if (_selectedTerms?.TermsId != item.TermsId)
                    p.BackColor = RowHover;
            };
            p.MouseLeave += (s, e) =>
            {
                if (_selectedTerms?.TermsId != item.TermsId)
                    p.BackColor = Color.White;
            };

            return p;
        }

        private void SelectTerms(TermsItemDto item)
        {
            _selectedTerms = item;

            // Highlight selected row in list
            foreach (var row in _rowPanels)
            {
                if (row.Tag is TermsItemDto rowItem)
                {
                    bool isSel = (rowItem.TermsId == item.TermsId);
                    row.BackColor = isSel ? RowSelected : Color.White;
                    row.Invalidate();
                }
            }

            // Update Right Card
            _docTitleLbl.Text = $"Terms & Conditions — {item.Version}";
            _metaTermsId.Text = $"{SuperAdminLabels.MetaTermsId}#{item.TermsId}";
            _metaEffective.Text = $"{SuperAdminLabels.MetaEffectiveDate}{item.EffectiveDateFormatted}";
            _metaCreatedBy.Text = $"{SuperAdminLabels.MetaCreatedBy}{item.CreatedByName}";

            // Position metadata items
            _metaEffective.Location = new Point(_metaTermsId.Right + 18, 9);
            _metaCreatedBy.Location = new Point(_metaEffective.Right + 18, 9);

            // Normalize newlines so sections break cleanly in WinForms TextBox
            string normalized = (item.Content ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\n", Environment.NewLine);

            _contentTextBox.Text = normalized;
            _contentTextBox.SelectionStart = 0;
            _contentTextBox.SelectionLength = 0;

            _rightFooterStatus.Text = $"Loaded {item.Version} • Published {item.EffectiveDateFormatted} • Ready";
        }

        // ================================================================
        //  FOOTER ACTIONS: PREVIEW & PUBLISH
        // ================================================================
        private void OnPreviewClicked(object? sender, EventArgs e)
        {
            if (_selectedTerms == null)
            {
                MessageBox.Show("Please select a version to preview.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var previewDialog = new TermsPreviewDialog(_selectedTerms);
            previewDialog.ShowDialog(this);
        }

        private async void OnPublishNewVersionClicked(object? sender, EventArgs e)
        {
            // Calculate next version
            string nextVersion = "v1.0";
            if (_termsList.Count > 0)
            {
                nextVersion = IncrementVersionString(_termsList[0].Version);
            }

            string currentContent = _selectedTerms?.Content ??
                (_termsList.Count > 0 ? _termsList[0].Content : "AQUASHINE CARWASH CRM - TERMS & CONDITIONS OF SERVICE\n\n1. ACCEPTANCE OF TERMS\n...");

            using var publishDialog = new PublishTermsDialog(nextVersion, currentContent);
            if (publishDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    _rightFooterStatus.Text = "Publishing new version...";
                    _publishBtn.Enabled = false;

                    var req = new CreateTermsRequest
                    {
                        Version = publishDialog.PublishedVersion,
                        Content = publishDialog.PublishedContent,
                        CreatedBy = SessionUser.UserId > 0 ? SessionUser.UserId : 39
                    };

                    var res = await _http.PostAsJsonAsync("api/terms", req);
                    if (res.IsSuccessStatusCode)
                    {
                        var created = await res.Content.ReadFromJsonAsync<TermsItemDto>();
                        await LoadTermsAsync(created?.TermsId);
                        MessageBox.Show(
                            $"Successfully published {publishDialog.PublishedVersion} with today's effective date.",
                            "Terms Published",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        var err = await res.Content.ReadAsStringAsync();
                        MessageBox.Show($"Publish failed: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Publish error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _publishBtn.Enabled = true;
                }
            }
        }

        private static string IncrementVersionString(string current)
        {
            if (string.IsNullOrWhiteSpace(current)) return "v1.0";
            current = current.Trim();
            bool hasPrefix = current.StartsWith("v", StringComparison.OrdinalIgnoreCase);
            string numPart = hasPrefix ? current.Substring(1) : current;
            var parts = numPart.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
            {
                return $"{(hasPrefix ? "v" : "")}{major}.{minor + 1}";
            }
            if (int.TryParse(numPart, out int single))
            {
                return $"{(hasPrefix ? "v" : "")}{single + 1}.0";
            }
            return $"v{DateTime.UtcNow:yyyy.MM}";
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y + bounds.Height - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Y + bounds.Height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // =========================================================================
    //  PREVIEW MODAL DIALOG
    // =========================================================================
    public class TermsPreviewDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Purple = Color.FromArgb(0x7C, 0x3A, 0xED);
        private static readonly Color PurpleSoft = Color.FromArgb(0xF3, 0xE8, 0xFF);

        public TermsPreviewDialog(TermsItemDto terms)
        {
            Text = $"Document Preview — {terms.Version}";
            ClientSize = new Size(820, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.White };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = "Terms & Conditions of Service",
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Navy,
                Location = new Point(24, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(titleLbl);

            var metaLbl = new Label
            {
                Text = $"Version: {terms.Version}   •   Effective Date: {terms.EffectiveDateFormatted}   •   Published by: {terms.CreatedByName}",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Muted,
                Location = new Point(24, 46),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(metaLbl);
            Controls.Add(header);

            // Bottom bar
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC) };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            var copyBtn = new Button
            {
                Text = "Copy to Clipboard",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Navy,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(140, 36),
                Location = new Point(24, 14),
                Cursor = Cursors.Hand
            };
            copyBtn.FlatAppearance.BorderColor = CardBorder;
            copyBtn.Click += (s, e) =>
            {
                Clipboard.SetText(terms.Content);
                MessageBox.Show("Terms content copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            footer.Controls.Add(copyBtn);

            var closeBtn = new Button
            {
                Text = "Close",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 36),
                Location = new Point(footer.ClientSize.Width - 124, 14),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            footer.Controls.Add(closeBtn);
            Controls.Add(footer);

            // Document Content Text Box
            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(0x1E, 0x29, 0x3B),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                Text = (terms.Content ?? string.Empty).Replace("\r\n", "\n").Replace("\n", Environment.NewLine)
            };

            var textPad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 18), BackColor = Color.White };
            textPad.Controls.Add(textBox);
            Controls.Add(textPad);
            textPad.BringToFront();
        }
    }

    // =========================================================================
    //  PUBLISH NEW VERSION MODAL DIALOG
    // =========================================================================
    public class PublishTermsDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Purple = Color.FromArgb(0x7C, 0x3A, 0xED);
        private static readonly Color PurpleHover = Color.FromArgb(0x6D, 0x28, 0xD9);

        public string PublishedVersion { get; private set; } = string.Empty;
        public string PublishedContent { get; private set; } = string.Empty;

        private readonly TextBox _versionTxt;
        private readonly TextBox _effectiveTxt;
        private readonly TextBox _authorTxt;
        private readonly TextBox _contentTxt;
        private readonly Label _errorLbl;

        public PublishTermsDialog(string defaultVersion, string currentContent)
        {
            Text = "Publish New Terms & Conditions";
            ClientSize = new Size(820, 720);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Top Section (Header + Meta Bar) ----
            var topContainer = new Panel { Dock = DockStyle.Top, Height = 142, BackColor = Color.White };

            var header = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.White };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = "Publish New Terms & Conditions",
                Font = new Font("Segoe UI Semibold", 15f),
                ForeColor = Navy,
                Location = new Point(24, 12),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(titleLbl);

            var subLbl = new Label
            {
                Text = "Modify the active copy below to publish a revision. The version is incremented and effective immediately.",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Muted,
                Location = new Point(24, 42),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(subLbl);
            topContainer.Controls.Add(header);

            // ---- Meta Form Bar ----
            var metaBar = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC) };
            metaBar.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawLine(pen, 0, metaBar.Height - 1, metaBar.Width, metaBar.Height - 1);
            };

            // Version Field
            var verLbl = new Label { Text = SuperAdminLabels.FieldNewVersion, Font = new Font("Segoe UI Semibold", 8f), ForeColor = Muted, Location = new Point(24, 10), AutoSize = true, UseMnemonic = false };
            _versionTxt = new TextBox { Text = defaultVersion, Location = new Point(24, 30), Width = 110, Font = new Font("Segoe UI Semibold", 9.5f), ForeColor = Purple };
            metaBar.Controls.Add(verLbl);
            metaBar.Controls.Add(_versionTxt);

            // Effective Date Field
            var dateLbl = new Label { Text = SuperAdminLabels.FieldEffectiveDate, Font = new Font("Segoe UI Semibold", 8f), ForeColor = Muted, Location = new Point(160, 10), AutoSize = true, UseMnemonic = false };
            _effectiveTxt = new TextBox { Text = DateTime.UtcNow.ToString("yyyy-MM-dd"), ReadOnly = true, Location = new Point(160, 30), Width = 120, BackColor = Color.White };
            metaBar.Controls.Add(dateLbl);
            metaBar.Controls.Add(_effectiveTxt);

            // Publisher Field
            var authLbl = new Label { Text = SuperAdminLabels.FieldPublisher, Font = new Font("Segoe UI Semibold", 8f), ForeColor = Muted, Location = new Point(306, 10), AutoSize = true, UseMnemonic = false };
            _authorTxt = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(SessionUser.FullName) ? "Super Admin" : SessionUser.FullName,
                ReadOnly = true,
                Location = new Point(306, 30),
                Width = 220,
                BackColor = Color.White
            };
            metaBar.Controls.Add(authLbl);
            metaBar.Controls.Add(_authorTxt);

            topContainer.Controls.Add(metaBar);
            Controls.Add(topContainer);

            // ---- Footer ----
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC) };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                ForeColor = Color.FromArgb(0xDC, 0x26, 0x26),
                Font = new Font("Segoe UI", 8.8f),
                Location = new Point(24, 22),
                AutoSize = true,
                Visible = false
            };
            footer.Controls.Add(_errorLbl);

            var cancelBtn = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9.2f),
                ForeColor = Navy,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 38),
                Location = new Point(footer.ClientSize.Width - 270, 14),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            cancelBtn.FlatAppearance.BorderColor = CardBorder;
            footer.Controls.Add(cancelBtn);

            var submitBtn = new Button
            {
                Text = "Publish Revision",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Purple,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(140, 38),
                Location = new Point(footer.ClientSize.Width - 156, 14),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand
            };
            submitBtn.FlatAppearance.BorderSize = 0;
            submitBtn.FlatAppearance.MouseOverBackColor = PurpleHover;
            submitBtn.Click += OnSubmitPublish;
            footer.Controls.Add(submitBtn);

            Controls.Add(footer);

            // ---- Content Editor Box ----
            var editorPad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 16, 24, 16), BackColor = Color.White };
            _contentTxt = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0x0F, 0x17, 0x2A),
                ForeColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                WordWrap = true,
                Text = (currentContent ?? string.Empty).Replace("\r\n", "\n").Replace("\n", Environment.NewLine)
            };

            var editorBorder = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(0x0F, 0x17, 0x2A), Padding = new Padding(12) };
            editorBorder.Controls.Add(_contentTxt);
            editorPad.Controls.Add(editorBorder);

            Controls.Add(editorPad);
            editorPad.BringToFront();
        }

        private void OnSubmitPublish(object? sender, EventArgs e)
        {
            string ver = _versionTxt.Text.Trim();
            if (string.IsNullOrWhiteSpace(ver))
            {
                _errorLbl.Text = "Please enter a valid version string (e.g. v1.2).";
                _errorLbl.Visible = true;
                return;
            }

            string content = _contentTxt.Text.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                _errorLbl.Text = "Content cannot be empty.";
                _errorLbl.Visible = true;
                return;
            }

            PublishedVersion = ver;
            PublishedContent = content;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
