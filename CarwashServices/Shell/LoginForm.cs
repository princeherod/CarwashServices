using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Dtos;

namespace CarwashServices.Shell
{
    // =====================================================================
    //  LOGIN UI THEME + HELPERS
    // =====================================================================
    internal static class LoginUi
    {
        public static readonly Color BrandTop = Color.FromArgb(0x8F, 0xC5, 0xF8);
        public static readonly Color BrandBottom = Color.FromArgb(0xB4, 0xDA, 0xFC);
        public static readonly Color Navy = Color.FromArgb(0x0A, 0x1F, 0x44);
        public static readonly Color TextMuted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        public static readonly Color Link = Color.FromArgb(0x1A, 0x62, 0xC9);
        public static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        public static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        public static readonly Color AccentPressed = Color.FromArgb(0x0F, 0x5D, 0xA8);
        public static readonly Color AccentDisabled = Color.FromArgb(0x8F, 0xBF, 0xF0);
        public static readonly Color BorderSoft = Color.FromArgb(0xD9, 0xE0, 0xEA);
        public static readonly Color BorderStrong = Color.FromArgb(0xB8, 0xC2, 0xD3);
        public static readonly Color Dot = Color.FromArgb(0xB0, 0xB8, 0xC8);
        public static readonly Color Error = Color.FromArgb(0xD9, 0x30, 0x25);

        public enum IconKind { Mail, Lock, Eye, EyeOff }

        public static int Px(this Control c, float v) => (int)Math.Round(v * c.DeviceDpi / 96f);
        public static float Pf(this Control c, float v) => v * c.DeviceDpi / 96f;

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            radius = Math.Max(0f, Math.Min(radius, Math.Min(r.Width, r.Height) / 2f));
            float d = radius * 2f;
            var p = new GraphicsPath();
            if (d <= 0f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawIcon(Graphics g, IconKind kind, RectangleF bounds, Color color)
        {
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);

            using (var pen = new Pen(color, 1.7f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                switch (kind)
                {
                    case IconKind.Mail:
                        using (var body = RoundRect(new RectangleF(3f, 5f, 18f, 14f), 2.5f))
                            g.DrawPath(pen, body);
                        g.DrawLines(pen, new[]
                        {
                            new PointF(3.8f, 7.5f), new PointF(12f, 13.2f), new PointF(20.2f, 7.5f)
                        });
                        break;

                    case IconKind.Lock:
                        using (var body = RoundRect(new RectangleF(5f, 11f, 14f, 10f), 2.5f))
                            g.DrawPath(pen, body);
                        g.DrawArc(pen, 8f, 3.5f, 8f, 8f, 180f, 180f);
                        g.DrawLine(pen, 8f, 7.5f, 8f, 11f);
                        g.DrawLine(pen, 16f, 7.5f, 16f, 11f);
                        using (var dot = new SolidBrush(color))
                            g.FillEllipse(dot, 11f, 14.5f, 2f, 2f);
                        break;

                    case IconKind.Eye:
                    case IconKind.EyeOff:
                        using (var outline = new GraphicsPath())
                        {
                            outline.AddBezier(2f, 12f, 6f, 5f, 18f, 5f, 22f, 12f);
                            outline.AddBezier(22f, 12f, 18f, 19f, 6f, 19f, 2f, 12f);
                            g.DrawPath(pen, outline);
                        }
                        g.DrawEllipse(pen, 9f, 9f, 6f, 6f);
                        if (kind == IconKind.EyeOff)
                            g.DrawLine(pen, 4f, 20f, 20f, 4f);
                        break;
                }
            }

            g.Restore(state);
        }
    }

    // =====================================================================
    //  LOGIN FORM
    // =====================================================================
    [DesignerCategory("Code")]
    public class LoginForm : Form
    {
        private const string ApiBaseUrl = "http://localhost:5180/";
        private const int FormW = 420;
        private const int ContentH = 408;

        private static readonly HttpClient Http = new HttpClient
        {
            BaseAddress = new Uri(ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly RoundedInput _email =
            new RoundedInput(LoginUi.IconKind.Mail, "Enter your email", false);
        private readonly RoundedInput _password =
            new RoundedInput(LoginUi.IconKind.Lock, "Enter your password", true);
        private readonly RoundedCheckBox _remember = new RoundedCheckBox();
        private readonly RoundedButton _loginBtn = new RoundedButton();
        private readonly Label _errorLbl = new Label();

        private bool _busy;

        public LoginForm()
        {
            BuildUi();
            LoadRememberedEmail();

            // ---- Anti-flicker: stay invisible until the first layout settles.
            Opacity = 0;
            Shown += (s, e) =>
            {
                PerformLayout();
                Invalidate(true);
                Update();
                Opacity = 1;

                // Focus the right field only once we're actually visible.
                if (_email.Value.Length > 0) _password.FocusInput();
                else _email.FocusInput();
            };
        }

        private void BuildUi()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Text = "AquaShine — Sign In";
            ClientSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            DoubleBuffered = true;

            var brand = new BrandPanel { Dock = DockStyle.Left, Width = 460 };
            Controls.Add(brand);

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Controls.Add(right);
            right.BringToFront();

            var content = new Panel
            {
                Size = new Size(FormW, ContentH),
                BackColor = Color.White
            };
            right.Controls.Add(content);

            void CenterContent()
            {
                content.Location = new Point(
                    Math.Max(0, (right.ClientSize.Width - content.Width) / 2),
                    Math.Max(0, (right.ClientSize.Height - content.Height) / 2));
            }
            right.Resize += (s, e) => CenterContent();
            Load += (s, e) => CenterContent();

            content.Controls.Add(new Label
            {
                Text = "Welcome back",
                AutoSize = false,
                Size = new Size(FormW, 56),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 26f, FontStyle.Bold),
                ForeColor = LoginUi.Navy,
                BackColor = Color.White
            });

            content.Controls.Add(new Divider
            {
                Location = new Point(0, 64),
                Size = new Size(FormW, 20),
                BackColor = Color.White
            });

            content.Controls.Add(MakeFieldLabel("Email", 108));
            _email.Location = new Point(0, 132);
            _email.Size = new Size(FormW, 52);
            content.Controls.Add(_email);

            content.Controls.Add(MakeFieldLabel("Password", 200));
            _password.Location = new Point(0, 224);
            _password.Size = new Size(FormW, 52);
            content.Controls.Add(_password);

            _errorLbl.AutoSize = false;
            _errorLbl.AutoEllipsis = true;
            _errorLbl.Location = new Point(0, 282);
            _errorLbl.Size = new Size(FormW, 24);
            _errorLbl.TextAlign = ContentAlignment.MiddleLeft;
            _errorLbl.ForeColor = LoginUi.Error;
            _errorLbl.BackColor = Color.White;
            _errorLbl.Font = new Font("Segoe UI", 9.5f);
            content.Controls.Add(_errorLbl);

            _email.ValueChanged += (s, e) => _errorLbl.Text = string.Empty;
            _password.ValueChanged += (s, e) => _errorLbl.Text = string.Empty;

            var row = new TableLayoutPanel
            {
                Location = new Point(0, 314),
                Size = new Size(FormW, 28),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _remember.Dock = DockStyle.Fill;
            _remember.Margin = Padding.Empty;
            row.Controls.Add(_remember, 0, 0);

            var forgot = new LinkLabel
            {
                Text = "Forgot password?",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9.5f),
                LinkColor = LoginUi.Link,
                ActiveLinkColor = LoginUi.AccentHover,
                VisitedLinkColor = LoginUi.Link,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            forgot.LinkClicked += (s, e) =>
                MessageBox.Show(
                    "Please contact your administrator to reset your password.",
                    "Forgot Password",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            row.Controls.Add(forgot, 1, 0);
            content.Controls.Add(row);

            _loginBtn.Text = "Log In";
            _loginBtn.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _loginBtn.Location = new Point(0, 356);
            _loginBtn.Size = new Size(FormW, 52);
            _loginBtn.Click += async (s, e) => await DoLoginAsync();
            content.Controls.Add(_loginBtn);

            AcceptButton = _loginBtn;

            ResumeLayout(false);
        }

        private static Label MakeFieldLabel(string text, int y) => new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(0, y),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = LoginUi.Navy,
            BackColor = Color.White
        };

        private static string RememberFile =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AquaShine",
                "remember.txt");

        private void LoadRememberedEmail()
        {
            try
            {
                if (File.Exists(RememberFile))
                {
                    var email = File.ReadAllText(RememberFile).Trim();
                    if (!string.IsNullOrEmpty(email))
                    {
                        _email.Value = email;
                        _remember.Checked = true;
                    }
                }
            }
            catch { }
        }

        private void SaveRememberedEmail()
        {
            try
            {
                var dir = Path.GetDirectoryName(RememberFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (_remember.Checked)
                    File.WriteAllText(RememberFile, _email.Value.Trim());
                else if (File.Exists(RememberFile))
                    File.Delete(RememberFile);
            }
            catch { }
        }

        private void Fail(string message, RoundedInput field)
        {
            _errorLbl.Text = message;
            if (field != null)
            {
                field.SetError(true);
                field.FocusInput();
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginBtn.Enabled = !busy;
            _loginBtn.Text = busy ? "Signing in…" : "Log In";
            UseWaitCursor = busy;
        }

        private async Task DoLoginAsync()
        {
            if (_busy) return;

            var email = _email.Value.Trim();
            var password = _password.Value;

            _errorLbl.Text = string.Empty;

            if (email.Length == 0) { Fail("Please enter your email.", _email); return; }
            if (!EmailRegex.IsMatch(email)) { Fail("Please enter a valid email address.", _email); return; }
            if (password.Length == 0) { Fail("Please enter your password.", _password); return; }

            SetBusy(true);
            try
            {
                using var resp = await Http.PostAsJsonAsync("api/auth/login", new { email, password });

                if (resp.IsSuccessStatusCode)
                {
                    AuthUserDto? user = null;
                    try
                    {
                        user = await resp.Content.ReadFromJsonAsync<AuthUserDto>();
                        if (user != null)
                        {
                            SessionUser.UserId = user.UserId;
                            SessionUser.FullName = user.FullName;
                            SessionUser.Email = user.Email;
                            SessionUser.CompanyId = user.CompanyId;
                            SessionUser.CompanyName = user.CompanyName ?? "";
                            SessionUser.CompanyCode = user.CompanyCode ?? "";
                            SessionUser.RoleId = user.RoleId;
                            SessionUser.Role = SessionUser.ResolveRole(user.RoleId, user.Email, user.CompanyId);
                            SessionUser.TermsAccepted = user.TermsAccepted;
                            SessionUser.TermsAcceptedVersion = user.TermsAcceptedVersion;
                            SessionUser.MultiBranchEnabled = user.MultiBranchEnabled;
                            SessionUser.AssignedBranchId = user.BranchId;
                            SessionUser.AssignedBranchName = user.BranchName;

                            if (user.BranchId.HasValue && user.BranchId.Value > 0)
                            {
                                SessionUser.SetBranch(user.BranchId.Value, user.BranchName);
                            }
                            else
                            {
                                SessionUser.SetBranch(null, "All Branches");
                            }
                        }
                    }
                    catch { }

                    // Initial Terms & Conditions acceptance guard:
                    // If a company/tenant account has not accepted the terms and conditions yet,
                    // display the Terms & Conditions agreement dialog (installer-style)
                    // where they must review and either accept or decline before proceeding into the CRM.
                    if (user != null && user.CompanyId.HasValue && user.CompanyId.Value > 0 && !user.TermsAccepted)
                    {
                        Hide();
                        using var termsDlg = new Dialogs.CompanyTermsAgreementDialog(user);
                        var termsResult = termsDlg.ShowDialog();
                        if (termsResult != DialogResult.OK)
                        {
                            // Terms rejected or cancelled: revoke session and stay on login
                            SessionUser.Clear();
                            Show();
                            _password.ClearValue();
                            Fail("Access Denied: You must accept the Terms and Conditions to access the CRM system.", null);
                            return;
                        }
                        Show();
                    }

                    SaveRememberedEmail();

                    // Do NOT call Close(). Setting DialogResult makes
                    // ShowDialog() return; the loop in Program.Main handles
                    // disposal. Closing here would cause an extra repaint of
                    // the parent form behind the login window.
                    DialogResult = DialogResult.OK;
                    return;
                }

                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _password.ClearValue();
                    Fail("Invalid email or password.", _password);
                    return;
                }

                Fail($"Login failed ({(int)resp.StatusCode}). Please try again.", null);
            }
            catch (HttpRequestException)
            {
                Fail("Cannot reach the AquaShine server. Make sure CRM.api is running on " + ApiBaseUrl, null);
            }
            catch (OperationCanceledException)
            {
                Fail("The server did not respond in time. Please try again.", null);
            }
            catch (Exception ex)
            {
                Fail("Unexpected error: " + ex.Message, null);
            }
            finally
            {
                if (!IsDisposed) SetBusy(false);
            }
        }

        // ================================================================
        //  NESTED UI CONTROLS
        // ================================================================

        [DesignerCategory("Code")]
        private sealed class BrandPanel : Panel
        {
            public BrandPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                using (var bg = new LinearGradientBrush(
                    ClientRectangle, LoginUi.BrandTop, LoginUi.BrandBottom, 90f))
                {
                    g.FillRectangle(bg, ClientRectangle);
                }

                DrawDecoration(g);
                DrawBrandText(g);
            }

            private const bool ShowDecoration = true;

            private void DrawDecoration(Graphics g)
            {
                if (!ShowDecoration) return;

                float baseY = Height * 0.87f;
                using (var wave = new GraphicsPath())
                {
                    wave.AddBezier(0f, baseY, Width * 0.25f, baseY - 44f,
                                   Width * 0.60f, baseY + 44f, Width, baseY - 12f);
                    wave.AddLine((float)Width, baseY - 12f, (float)Width, (float)Height);
                    wave.AddLine((float)Width, (float)Height, 0f, (float)Height);
                    wave.CloseFigure();
                    using (var b = new SolidBrush(Color.FromArgb(38, Color.White)))
                        g.FillPath(b, wave);
                }

                Bubble(g, Width * 0.80f, Height * 0.14f, 46f, 34);
                Bubble(g, Width * 0.66f, Height * 0.24f, 20f, 40);
                Bubble(g, Width * 0.90f, Height * 0.30f, 12f, 46);
                Bubble(g, Width * 0.14f, Height * 0.80f, 30f, 32);
                Bubble(g, Width * 0.30f, Height * 0.72f, 14f, 44);
            }

            private void Bubble(Graphics g, float cx, float cy, float r, int alpha)
            {
                r = this.Pf(r);
                using (var fill = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                    g.FillEllipse(fill, cx - r, cy - r, r * 2, r * 2);
                using (var ring = new Pen(Color.FromArgb(Math.Min(255, alpha + 30), Color.White), 1.2f))
                    g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
            }

            private void DrawBrandText(Graphics g)
            {
                const string title = "AquaShine";
                const string sub = "Professional Car Wash Services";

                using (var titleFont = new Font("Segoe UI", 44f, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 15f, FontStyle.Regular))
                {
                    SizeF ts = g.MeasureString(title, titleFont);
                    SizeF ss = g.MeasureString(sub, subFont);
                    float gap = this.Pf(4);
                    float total = ts.Height + gap + ss.Height;

                    float x = Width * 0.09f;
                    float y = (Height - total) / 2f;

                    using (var shadow = new SolidBrush(Color.FromArgb(55, LoginUi.Navy)))
                    {
                        g.DrawString(title, titleFont, shadow, x + 1f, y + 2.5f);
                        g.DrawString(sub, subFont, shadow, x + 3f, y + ts.Height + gap + 1.5f);
                    }
                    g.DrawString(title, titleFont, Brushes.White, x, y);
                    using (var subBrush = new SolidBrush(Color.FromArgb(240, Color.White)))
                        g.DrawString(sub, subFont, subBrush, x + 3f, y + ts.Height + gap);
                }
            }
        }

        [DesignerCategory("Code")]
        private sealed class Divider : Control
        {
            public Divider()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                TabStop = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float midY = Height / 2f;
                float gap = this.Pf(14);
                float r = this.Pf(4);

                using (var pen = new Pen(LoginUi.BorderSoft, 1f))
                {
                    g.DrawLine(pen, 0f, midY, Width / 2f - gap, midY);
                    g.DrawLine(pen, Width / 2f + gap, midY, (float)Width, midY);
                }
                using (var dot = new SolidBrush(LoginUi.Dot))
                    g.FillEllipse(dot, Width / 2f - r, midY - r, r * 2, r * 2);
            }
        }

        [DesignerCategory("Code")]
        private sealed class RoundedInput : Panel
        {
            private readonly TextBox _text;
            private readonly LoginUi.IconKind _icon;
            private readonly bool _isPassword;
            private bool _focused;
            private bool _revealed;
            private bool _hasError;

            public event EventHandler ValueChanged;

            public RoundedInput(LoginUi.IconKind icon, string placeholder, bool isPassword)
            {
                _icon = icon;
                _isPassword = isPassword;

                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
                Cursor = Cursors.IBeam;

                _text = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Color.White,
                    ForeColor = LoginUi.Navy,
                    Font = new Font("Segoe UI", 11f),
                    PlaceholderText = placeholder,
                    UseSystemPasswordChar = isPassword
                };
                _text.GotFocus += (s, e) => { _focused = true; Invalidate(); };
                _text.LostFocus += (s, e) => { _focused = false; Invalidate(); };
                _text.TextChanged += (s, e) =>
                {
                    if (_hasError) { _hasError = false; Invalidate(); }
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                };
                Controls.Add(_text);
            }

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public string Value
            {
                get => _text.Text;
                set => _text.Text = value;
            }

            public void FocusInput()
            {
                _text.Focus();
                _text.SelectionStart = _text.TextLength;
            }

            public void ClearValue() => _text.Clear();

            public void SetError(bool on)
            {
                _hasError = on;
                Invalidate();
            }

            private Rectangle EyeBounds
            {
                get
                {
                    int size = this.Px(36);
                    return new Rectangle(Width - this.Px(46), (Height - size) / 2, size, size);
                }
            }

            private void LayoutText()
            {
                int left = this.Px(50);
                int rightPad = _isPassword ? this.Px(54) : this.Px(18);
                _text.Width = Math.Max(10, Width - left - rightPad);
                _text.Location = new Point(left, (Height - _text.Height) / 2);
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                LayoutText();
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                LayoutText();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Cursor = _isPassword && EyeBounds.Contains(e.Location) ? Cursors.Hand : Cursors.IBeam;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (_isPassword && EyeBounds.Contains(e.Location))
                {
                    _revealed = !_revealed;
                    _text.UseSystemPasswordChar = !_revealed;
                    Invalidate();
                }
                FocusInput();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : Color.White);

                var rect = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f);
                float radius = this.Pf(10);

                using (var path = LoginUi.RoundRect(rect, radius))
                {
                    using (var fill = new SolidBrush(Color.White))
                        g.FillPath(fill, path);

                    if (_focused || _hasError)
                    {
                        Color glowBase = _hasError ? LoginUi.Error : LoginUi.Accent;
                        using (var glow = new Pen(Color.FromArgb(38, glowBase), 3f))
                            g.DrawPath(glow, path);
                    }

                    Color border = _hasError ? LoginUi.Error
                                 : _focused ? LoginUi.Accent
                                 : LoginUi.BorderSoft;
                    using (var pen = new Pen(border, _focused || _hasError ? 1.5f : 1f))
                        g.DrawPath(pen, path);
                }

                float iconSize = this.Pf(22);
                var iconRect = new RectangleF(this.Pf(16), (Height - iconSize) / 2f, iconSize, iconSize);
                LoginUi.DrawIcon(g, _icon, iconRect, _focused ? LoginUi.Accent : LoginUi.TextMuted);

                if (_isPassword)
                {
                    Rectangle hit = EyeBounds;
                    var eyeRect = new RectangleF(
                        hit.X + (hit.Width - iconSize) / 2f,
                        hit.Y + (hit.Height - iconSize) / 2f,
                        iconSize, iconSize);
                    LoginUi.DrawIcon(g, _revealed ? LoginUi.IconKind.EyeOff : LoginUi.IconKind.Eye,
                                     eyeRect, LoginUi.TextMuted);
                }
            }
        }

        [DesignerCategory("Code")]
        private sealed class RoundedButton : Button
        {
            private bool _hover;
            private bool _down;

            public RoundedButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                UseVisualStyleBackColor = false;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
            protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : Color.White);

                Color fill = !Enabled ? LoginUi.AccentDisabled
                           : _down ? LoginUi.AccentPressed
                           : _hover ? LoginUi.AccentHover
                           : LoginUi.Accent;

                var rect = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
                using (var path = LoginUi.RoundRect(rect, this.Pf(10)))
                using (var brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);

                    if (Focused && ShowFocusCues)
                    {
                        var inner = new RectangleF(2.5f, 2.5f, Width - 6f, Height - 6f);
                        using (var ringPath = LoginUi.RoundRect(inner, this.Pf(8)))
                        using (var ring = new Pen(Color.FromArgb(200, Color.White), 1.5f))
                            g.DrawPath(ring, ringPath);
                    }
                }

                TextRenderer.DrawText(
                    g, Text, Font, ClientRectangle, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }

        [DesignerCategory("Code")]
        private sealed class RoundedCheckBox : CheckBox
        {
            private bool _hover;

            public RoundedCheckBox()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                AutoSize = false;
                Text = "Remember me";
                Font = new Font("Segoe UI", 10f);
                ForeColor = LoginUi.Navy;
                BackColor = Color.White;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackColor);

                float box = this.Pf(20);
                float top = (Height - box) / 2f;
                var boxRect = new RectangleF(1f, top, box - 2f, box - 2f);

                using (var path = LoginUi.RoundRect(boxRect, this.Pf(5)))
                {
                    if (Focused && ShowFocusCues)
                    {
                        using (var ring = new Pen(Color.FromArgb(70, LoginUi.Accent), 3f))
                            g.DrawPath(ring, path);
                    }

                    using (var fill = new SolidBrush(Checked ? LoginUi.Accent : Color.White))
                        g.FillPath(fill, path);

                    Color border = Checked || _hover || Focused ? LoginUi.Accent : LoginUi.BorderStrong;
                    using (var pen = new Pen(border, 1.4f))
                        g.DrawPath(pen, path);
                }

                if (Checked)
                {
                    using (var tick = new Pen(Color.White, this.Pf(2f)))
                    {
                        tick.StartCap = LineCap.Round;
                        tick.EndCap = LineCap.Round;
                        tick.LineJoin = LineJoin.Round;
                        g.DrawLines(tick, new[]
                        {
                            new PointF(boxRect.X + boxRect.Width * 0.26f, boxRect.Y + boxRect.Height * 0.53f),
                            new PointF(boxRect.X + boxRect.Width * 0.44f, boxRect.Y + boxRect.Height * 0.71f),
                            new PointF(boxRect.X + boxRect.Width * 0.76f, boxRect.Y + boxRect.Height * 0.33f)
                        });
                    }
                }

                int textLeft = (int)box + this.Px(10);
                var textRect = new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height);
                TextRenderer.DrawText(
                    g, Text, Font, textRect, ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }
}