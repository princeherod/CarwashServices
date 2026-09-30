using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarwashServices.Auth;
using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Initial Terms and Conditions agreement dialog presented when a tenant company
    /// logs in for the first time or before terms have been accepted.
    /// Emulates a professional desktop installer EULA / License Agreement wizard.
    /// </summary>
    public class CompanyTermsAgreementDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        private static readonly Color AccentDisabled = Color.FromArgb(0x9E, 0xC5, 0xED);
        private static readonly Color BorderSoft = Color.FromArgb(0xD9, 0xE0, 0xEA);
        private static readonly Color CardBg = Color.FromArgb(0xF8, 0xFA, 0xFD);
        private static readonly Color Danger = Color.FromArgb(0xD9, 0x30, 0x25);

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly AuthUserDto _user;
        private TermsItemDto? _latestTerms;

        // UI Controls
        private Label _orgLbl = null!;
        private Label _accountLbl = null!;
        private Label _termsMetaLbl = null!;
        private TextBox _termsContentTxt = null!;
        private RadioButton _acceptRadio = null!;
        private RadioButton _declineRadio = null!;
        private Button _acceptBtn = null!;
        private Button _declineBtn = null!;
        private Button _copyBtn = null!;
        private Label _statusLbl = null!;

        public CompanyTermsAgreementDialog(AuthUserDto user)
        {
            _user = user ?? throw new ArgumentNullException(nameof(user));

            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);

            Load += async (s, e) => await LoadTermsContentAsync();
            FormClosing += OnFormClosing;
        }

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
            Text = "AquaShine CRM — License Agreement & Terms of Service";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 720);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            // =================================================================
            //  HEADER (Installer banner style)
            // =================================================================
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFD),
                Padding = new Padding(28, 16, 28, 12)
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);

                // Small installer graphic icon (shield / document) on top-right
                var iconRect = new Rectangle(header.Width - 68, 18, 44, 44);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(0xE3, 0xEE, 0xFB));
                using var path = RoundedRect(iconRect, 10);
                e.Graphics.FillPath(brush, path);

                using var textBrush = new SolidBrush(Accent);
                using var font = new Font("Segoe UI", 16f, FontStyle.Bold);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("§", font, textBrush, iconRect, sf);
            };

            var titleLbl = new Label
            {
                Text = "Terms & Conditions of Service",
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = Navy,
                Location = new Point(28, 14),
                AutoSize = true
            };

            var subtitleLbl = new Label
            {
                Text = "Please review the license terms before using AquaShine CRM. You must accept these terms to proceed.",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Muted,
                Location = new Point(28, 44),
                Size = new Size(header.Width - 110, 36)
            };

            header.Controls.Add(titleLbl);
            header.Controls.Add(subtitleLbl);
            Controls.Add(header);

            // =================================================================
            //  FOOTER (Installer wizard buttons)
            // =================================================================
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = Color.FromArgb(0xFA, 0xFB, 0xFD)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _statusLbl = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Danger,
                Location = new Point(28, 26),
                AutoSize = true
            };
            footer.Controls.Add(_statusLbl);

            _declineBtn = new Button
            {
                Text = "Decline & Exit",
                Size = new Size(130, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Location = new Point(footer.ClientSize.Width - 28 - 160 - 140, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _declineBtn.FlatAppearance.BorderColor = BorderSoft;
            _declineBtn.Click += (s, e) => HandleDecline();
            footer.Controls.Add(_declineBtn);

            _acceptBtn = new Button
            {
                Text = "Accept & Continue",
                Size = new Size(160, 40),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = AccentDisabled,
                Cursor = Cursors.Hand,
                Enabled = false,
                Location = new Point(footer.ClientSize.Width - 28 - 160, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _acceptBtn.FlatAppearance.BorderSize = 0;
            _acceptBtn.Click += async (s, e) => await HandleAcceptAsync();
            footer.Controls.Add(_acceptBtn);

            Controls.Add(footer);

            // =================================================================
            //  MAIN BODY CONTAINER
            // =================================================================
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 16, 28, 8),
                BackColor = Color.White
            };

            // 1. Metadata Info Card
            var metaCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 0, 12)
            };
            metaCard.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft, 1);
                using var path = RoundedRect(new Rectangle(0, 0, metaCard.Width - 1, metaCard.Height - 1), 6);
                e.Graphics.DrawPath(pen, path);
            };

            _orgLbl = new Label
            {
                Text = $"Business: {_user.CompanyName} ({_user.CompanyCode})",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                Location = new Point(16, 12),
                AutoSize = true
            };
            _accountLbl = new Label
            {
                Text = $"User Account: {_user.FullName} ({_user.Email})",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Muted,
                Location = new Point(16, 36),
                AutoSize = true
            };
            _termsMetaLbl = new Label
            {
                Text = "Terms: Loading latest version…",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Muted,
                Location = new Point(360, 14),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            metaCard.Controls.Add(_orgLbl);
            metaCard.Controls.Add(_accountLbl);
            metaCard.Controls.Add(_termsMetaLbl);

            // 2. Document Container with Copy Toolbar
            var docContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8)
            };

            var docHeaderBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32
            };

            var docTitleLbl = new Label
            {
                Text = "TERMS AND CONDITIONS AGREEMENT TEXT:",
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Muted,
                Location = new Point(0, 8),
                AutoSize = true
            };
            docHeaderBar.Controls.Add(docTitleLbl);

            _copyBtn = new Button
            {
                Text = "Copy to Clipboard",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Accent,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(130, 26),
                Cursor = Cursors.Hand,
                Location = new Point(docHeaderBar.Width - 130, 3),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _copyBtn.FlatAppearance.BorderColor = BorderSoft;
            _copyBtn.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_termsContentTxt.Text))
                {
                    Clipboard.SetText(_termsContentTxt.Text);
                    MessageBox.Show("Terms & Conditions text copied to clipboard for your records.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            docHeaderBar.Controls.Add(_copyBtn);

            _termsContentTxt = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(0x1F, 0x29, 0x37),
                Font = new Font("Segoe UI", 9.5f),
                Text = "Loading Terms & Conditions from server...\r\n\r\nPlease wait while the latest Super Admin terms are retrieved.",
                Margin = new Padding(0, 4, 0, 8)
            };

            var docWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 36, 0, 0)
            };
            docWrapper.Controls.Add(_termsContentTxt);
            docWrapper.Controls.Add(docHeaderBar);
            docHeaderBar.BringToFront();

            // 3. Choice Options (Classic installer radio buttons)
            var choicePanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 84,
                Padding = new Padding(4, 8, 4, 4)
            };

            _acceptRadio = new RadioButton
            {
                Text = "I accept the agreement and agree to abide by all the Terms & Conditions of Service.",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                Location = new Point(8, 12),
                Size = new Size(680, 24),
                Cursor = Cursors.Hand,
                Checked = false
            };
            _acceptRadio.CheckedChanged += (s, e) =>
            {
                _acceptBtn.Enabled = _acceptRadio.Checked;
                _acceptBtn.BackColor = _acceptRadio.Checked ? Accent : AccentDisabled;
            };

            _declineRadio = new RadioButton
            {
                Text = "I do not accept the agreement (I understand CRM access will remain blocked).",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Muted,
                Location = new Point(8, 42),
                Size = new Size(680, 24),
                Cursor = Cursors.Hand,
                Checked = true
            };

            choicePanel.Controls.Add(_acceptRadio);
            choicePanel.Controls.Add(_declineRadio);

            body.Controls.Add(docWrapper);
            body.Controls.Add(choicePanel);
            body.Controls.Add(metaCard);

            Controls.Add(body);
            body.BringToFront();
        }

        private async Task LoadTermsContentAsync()
        {
            try
            {
                var latest = await _http.GetFromJsonAsync<TermsItemDto>("api/terms/latest");
                if (latest != null)
                {
                    _latestTerms = latest;
                    _termsMetaLbl.Text = $"Version: {latest.Version}  •  Effective: {latest.EffectiveDateFormatted}";
                    _termsContentTxt.Text = (latest.Content ?? string.Empty)
                        .Replace("\r\n", "\n")
                        .Replace("\n", Environment.NewLine);
                    _termsContentTxt.SelectionStart = 0;
                    _termsContentTxt.SelectionLength = 0;
                }
                else
                {
                    LoadFallbackTerms();
                }
            }
            catch
            {
                _statusLbl.Text = "Note: Could not reach live terms endpoint. Showing standard terms.";
                LoadFallbackTerms();
            }
        }

        private void LoadFallbackTerms()
        {
            _termsMetaLbl.Text = "Version: v1.0  •  Default Agreement";
            _termsContentTxt.Text =
                "AQUASHINE CARWASH CRM - TERMS & CONDITIONS OF SERVICE\r\n\r\n" +
                "1. ACCEPTANCE OF TERMS\r\n" +
                "By registering an account and accessing the AquaShine CRM Platform, your business agrees to be bound by these Terms and Conditions.\r\n\r\n" +
                "2. TENANT DATA OWNERSHIP & PRIVACY\r\n" +
                "Each registered business maintains full ownership of its operational data, customer lists, and financial records stored within its provisioned database. AquaShine CRM will not distribute, sell, or disclose proprietary business data to third parties.\r\n\r\n" +
                "3. PERMITTED USE & SECURITY\r\n" +
                "Users must keep credentials secure and notify system administrators of any unauthorized access. Abusive behaviors, reverse-engineering, or automated unauthorized scraping are strictly prohibited.\r\n\r\n" +
                "4. SUBSCRIPTION & SERVICE LEVEL\r\n" +
                "Service features and capacity limits are determined by the business's active tenant subscription plan. Failure to maintain subscription standing may result in restricted service availability.\r\n\r\n" +
                "5. TERMINATION & MODIFICATION\r\n" +
                "AquaShine CRM administrators reserve the right to suspend or terminate accounts that violate system policies.\r\n\r\n" +
                "For inquiries regarding these terms, contact your Super Administrator.";
        }

        private async Task HandleAcceptAsync()
        {
            if (!_acceptRadio.Checked)
            {
                MessageBox.Show("Please select 'I accept the agreement' before continuing.", "Agreement Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int companyId = _user.CompanyId ?? SessionUser.CurrentCompanyId;
            int userId = _user.UserId;
            string version = _latestTerms?.Version ?? "v1.0";
            int? termsId = _latestTerms?.TermsId;

            _statusLbl.Text = "";
            _acceptBtn.Enabled = false;
            _declineBtn.Enabled = false;
            _acceptBtn.Text = "Recording...";
            Cursor = Cursors.WaitCursor;

            try
            {
                var payload = new AcceptTermsRequest
                {
                    CompanyId = companyId,
                    UserId = userId,
                    TermsId = termsId,
                    Version = version
                };

                using var resp = await _http.PostAsJsonAsync("api/terms/accept", payload);
                if (resp.IsSuccessStatusCode)
                {
                    SessionUser.TermsAccepted = true;
                    SessionUser.TermsAcceptedVersion = version;

                    MessageBox.Show(
                        $"Thank you! The Terms & Conditions ({version}) have been accepted for {_user.CompanyName}.\r\n\r\nWelcome to AquaShine CRM!",
                        "Agreement Accepted",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    _statusLbl.Text = "Failed to record acceptance. Please try again.";
                    MessageBox.Show($"Server error recording terms acceptance: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _statusLbl.Text = $"Network error: {ex.Message}";
                MessageBox.Show($"Network error connecting to API: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _acceptBtn.Enabled = _acceptRadio.Checked;
                _declineBtn.Enabled = true;
                _acceptBtn.Text = "Accept & Continue";
            }
        }

        private void HandleDecline()
        {
            var confirm = MessageBox.Show(
                "Are you sure you want to decline the Terms and Conditions?\r\n\r\n" +
                "If you decline, you cannot access or use AquaShine CRM. You will be returned to the login screen.\r\n\r\n" +
                "Do you want to decline and exit?",
                "Confirm Decline",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm == DialogResult.Yes)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var compId = _user.CompanyId ?? SessionUser.CurrentCompanyId;
                        await _http.PostAsJsonAsync("api/terms/decline", new DeclineTermsRequest
                        {
                            CompanyId = compId,
                            UserId = _user.UserId,
                            Reason = "User clicked Decline in installer dialog."
                        });
                    }
                    catch { }
                });

                SessionUser.Clear();
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                SessionUser.Clear();
                DialogResult = DialogResult.Cancel;
            }
        }
    }
}
