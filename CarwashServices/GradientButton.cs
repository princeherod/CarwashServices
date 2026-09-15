using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CarwashServices
{
    public class GradientButton : Button
    {
        private Color _colorTop = Color.FromArgb(25, 118, 210);
        private Color _colorBottom = Color.FromArgb(13, 71, 161);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ColorTop
        {
            get => _colorTop;
            set { _colorTop = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ColorBottom
        {
            get => _colorBottom;
            set { _colorBottom = value; Invalidate(); }
        }

        public GradientButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width, Height);
            using var path = new GraphicsPath();
            int r = 12;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();

            using var brush = new LinearGradientBrush(
                rect, _colorTop, _colorBottom, LinearGradientMode.Vertical);
            g.FillPath(brush, path);

            var glowColor = Color.FromArgb(80, Color.White);
            using var pen = new Pen(glowColor, 1);
            g.DrawPath(pen, path);

            TextRenderer.DrawText(
                g, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}