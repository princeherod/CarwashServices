using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Shell;

namespace CarwashServices.Roles
{
    /// <summary>
    /// 403 Forbidden / Access Denied view.
    /// Rendered when an unauthorized user attempts to access a protected module directly.
    /// Displays security details, required role, current user role, and provides a redirect action.
    /// </summary>
    public class AccessDeniedView : UserControl
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color NavyHover = Color.FromArgb(0x16, 0x2A, 0x5C);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color PageBg = Color.FromArgb(0xF0, 0xF4, 0xFA);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Red = Color.FromArgb(0xDC, 0x26, 0x26);
        private static readonly Color RedSoft = Color.FromArgb(0xFE, 0xE2, 0xE2);
        private static readonly Color RedBorder = Color.FromArgb(0xFE, 0xCA, 0xCA);
        private static readonly Color CodeBg = Color.FromArgb(0x0F, 0x17, 0x2A);

        private readonly string _moduleName;
        private readonly string _requiredRole;
        private readonly Action? _onRedirect;
        private System.Windows.Forms.Timer? _countdownTimer;
        private int _secondsRemaining = 5;
        private Button _redirectBtn = null!;
        private Label _countdownLbl = null!;

        private Panel _card = null!;

        public AccessDeniedView(string moduleName = "Protected Module",
                                string requiredRole = "Super Admin (Role 4)",
                                Action? onRedirect = null)
        {
            _moduleName = moduleName;
            _requiredRole = requiredRole;
            _onRedirect = onRedirect;

            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            BuildUi();
            Sidebar.EnableDoubleBuffering(this);

            StartCountdown();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCard();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            CenterCard();
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Location = new Point(
                Math.Max(20, (ClientSize.Width - _card.Width) / 2),
                Math.Max(20, (ClientSize.Height - _card.Height) / 2));
        }

        private void BuildUi()
        {
            SuspendLayout();

            // Card Panel in center
            _card = new Panel
            {
                Size = new Size(580, 480),
                BackColor = Color.White
            };
            _card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, _card.Width - 1, _card.Height - 1);
                using var path = RoundedRect(rect, 14);
                using var brush = new SolidBrush(Color.White);
                e.Graphics.FillPath(brush, path);
                using var borderPen = new Pen(CardBorder, 1.2f);
                e.Graphics.DrawPath(borderPen, path);
            };

            // 1. Icon Circle with Shield / Lock
            var iconCircle = new Panel
            {
                Size = new Size(68, 68),
                Location = new Point((580 - 68) / 2, 28),
                BackColor = Color.Transparent
            };
            iconCircle.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var bg = new SolidBrush(RedSoft);
                g.FillEllipse(bg, 0, 0, 67, 67);

                using var pen = new Pen(Red, 2.4f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };

                // Shield outline
                PointF[] shield =
                {
                    new PointF(34, 15),
                    new PointF(52, 23),
                    new PointF(52, 38),
                    new PointF(34, 52),
                    new PointF(16, 38),
                    new PointF(16, 23)
                };
                g.DrawPolygon(pen, shield);

                // Lock bar / exclamation
                g.DrawLine(pen, 34, 25, 34, 37);
                g.FillEllipse(new SolidBrush(Red), 32.5f, 41f, 3.5f, 3.5f);
            };
            _card.Controls.Add(iconCircle);

            // 2. HTTP 403 Badge
            var badge403 = new Label
            {
                Text = "HTTP 403 FORBIDDEN",
                Font = new Font("Segoe UI Semibold", 8.2f),
                ForeColor = Red,
                BackColor = RedSoft,
                Size = new Size(150, 24),
                Location = new Point((580 - 150) / 2, 108),
                TextAlign = ContentAlignment.MiddleCenter
            };
            badge403.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var p = RoundedRect(new Rectangle(0, 0, badge403.Width - 1, badge403.Height - 1), 6);
                using var pen = new Pen(RedBorder, 1f);
                e.Graphics.DrawPath(pen, p);
            };
            _card.Controls.Add(badge403);

            // 3. Title
            var titleLbl = new Label
            {
                Text = "Access Denied",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = Navy,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(540, 34),
                Location = new Point(20, 140)
            };
            _card.Controls.Add(titleLbl);

            // 4. Subtitle
            var subLbl = new Label
            {
                Text = "You do not have the required permissions to access this module.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(540, 24),
                Location = new Point(20, 178)
            };
            _card.Controls.Add(subLbl);

            // 5. Details Info Box
            var infoBox = new Panel
            {
                Size = new Size(490, 126),
                Location = new Point(45, 212),
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC)
            };
            infoBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var p = RoundedRect(new Rectangle(0, 0, infoBox.Width - 1, infoBox.Height - 1), 8);
                using var pen = new Pen(CardBorder, 1f);
                e.Graphics.DrawPath(pen, p);
            };

            int infoY = 14;
            AddInfoRow(infoBox, "Target Route:", _moduleName, ref infoY);
            AddInfoRow(infoBox, "Required Role:", _requiredRole, ref infoY, isHighlight: true);
            string userStr = string.IsNullOrWhiteSpace(SessionUser.FullName)
                ? $"Role ID: {SessionUser.RoleId} ({SessionUser.Role})"
                : $"{SessionUser.FullName}  ·  Role ID: {SessionUser.RoleId} ({SessionUser.Role})";
            AddInfoRow(infoBox, "Your Session:", userStr, ref infoY);

            _card.Controls.Add(infoBox);

            // 6. Countdown text
            _countdownLbl = new Label
            {
                Text = $"Redirecting automatically in {_secondsRemaining} seconds...",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(540, 22),
                Location = new Point(20, 354)
            };
            _card.Controls.Add(_countdownLbl);

            // 7. Return / Redirect Button
            _redirectBtn = new Button
            {
                Text = "← Return to Authorized View",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Color.White,
                BackColor = Navy,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(240, 42),
                Location = new Point((580 - 240) / 2, 386),
                Cursor = Cursors.Hand
            };
            _redirectBtn.FlatAppearance.BorderSize = 0;
            _redirectBtn.FlatAppearance.MouseOverBackColor = NavyHover;
            _redirectBtn.Click += (s, e) => TriggerRedirect();
            _card.Controls.Add(_redirectBtn);

            Controls.Add(_card);
            CenterCard();

            ResumeLayout(true);
        }

        private static void AddInfoRow(Panel parent, string label, string value, ref int y, bool isHighlight = false)
        {
            var lbl = new Label
            {
                Text = label,
                Font = new Font("Segoe UI Semibold", 8.8f),
                ForeColor = Muted,
                Location = new Point(18, y),
                Size = new Size(115, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            parent.Controls.Add(lbl);

            var val = new Label
            {
                Text = value,
                Font = new Font(isHighlight ? "Segoe UI Semibold" : "Segoe UI", 8.8f),
                ForeColor = isHighlight ? Red : Navy,
                Location = new Point(135, y),
                Size = new Size(335, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            parent.Controls.Add(val);

            y += 28;
        }

        private void StartCountdown()
        {
            _countdownTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            _countdownTimer.Tick += (s, e) =>
            {
                _secondsRemaining--;
                if (_secondsRemaining <= 0)
                {
                    _countdownTimer.Stop();
                    TriggerRedirect();
                }
                else
                {
                    if (_countdownLbl != null && !IsDisposed)
                    {
                        _countdownLbl.Text = $"Redirecting automatically in {_secondsRemaining} seconds...";
                    }
                }
            };
            _countdownTimer.Start();
        }

        private void TriggerRedirect()
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _countdownTimer = null;

            _onRedirect?.Invoke();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _countdownTimer?.Stop();
                _countdownTimer?.Dispose();
                _countdownTimer = null;
            }
            base.Dispose(disposing);
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
}
