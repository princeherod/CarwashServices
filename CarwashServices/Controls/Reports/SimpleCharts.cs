using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace CarwashServices.Controls.Reports
{
    internal static class SimpleChartUtil
    {
        public static readonly Color TooltipBg = Color.FromArgb(0x0A, 0x16, 0x33);
        public static readonly Color TooltipDim = Color.FromArgb(0xB8, 0xC4, 0xDC);
        public static readonly Color AxisText = Color.FromArgb(0x9A, 0xA7, 0xBF);

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawTooltip(Graphics g, Font font, string title, string value,
                                       PointF anchor, Rectangle bounds)
        {
            using var bold = new Font(font, FontStyle.Bold);
            var s1 = g.MeasureString(title, font);
            var s2 = g.MeasureString(value, bold);
            float w = Math.Max(s1.Width, s2.Width) + 20f;
            float h = s1.Height + s2.Height + 10f;

            float x = anchor.X - w / 2f;
            float y = anchor.Y - h - 12f;
            if (y < bounds.Top + 2) y = anchor.Y + 14f;
            x = Math.Max(bounds.Left + 2, Math.Min(x, bounds.Right - w - 2));

            using var path = RoundedRect(new RectangleF(x, y, w, h), 6f);
            using var bg = new SolidBrush(TooltipBg);
            using var dim = new SolidBrush(TooltipDim);
            using var white = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            g.DrawString(title, font, dim, x + 10f, y + 5f);
            g.DrawString(value, bold, white, x + 10f, y + 5f + s1.Height);
        }
    }

    /// <summary>Ring chart with a coloured legend below it and hover tooltips.</summary>
    public class DonutChartSimple : Control
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, double Value, Color Color)> Segments { get; set; } = new();

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CenterTop { get; set; } = "0";

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CenterBottom { get; set; } = "Total";

        private static readonly Font TipFont = new("Segoe UI", 8.5f);
        private int _hover = -1;
        private RectangleF _arcRect;
        private float _thick;

        public DonutChartSimple()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            const int legendH = 36;
            int chartH = Math.Max(40, Height - legendH);

            double total = Segments.Sum(s => s.Value);
            int size = Math.Max(50, Math.Min(Width - 20, chartH - 20));
            var rect = new RectangleF((Width - size) / 2f, (chartH - size) / 2f, size, size);
            float thick = size * 0.17f;
            var arcRect = RectangleF.Inflate(rect, -thick / 2f, -thick / 2f);

            _arcRect = arcRect;
            _thick = thick;

            if (total <= 0)
            {
                using var pen = new Pen(Color.FromArgb(0xEC, 0xF0, 0xF7), thick);
                g.DrawEllipse(pen, arcRect);
            }
            else
            {
                int nonZero = Segments.Count(s => s.Value > 0);
                float gap = nonZero > 1 ? 3f : 0f;
                float start = -90f;
                for (int i = 0; i < Segments.Count; i++)
                {
                    var seg = Segments[i];
                    if (seg.Value <= 0) continue;
                    float sweep = (float)(seg.Value / total * 360.0);
                    using var pen = new Pen(i == _hover
                        ? ControlPaint.Light(seg.Color, 0.15f)
                        : seg.Color, thick);
                    if (nonZero == 1) g.DrawEllipse(pen, arcRect);
                    else if (sweep - gap > 0.5f) g.DrawArc(pen, arcRect, start + gap / 2f, sweep - gap);
                    start += sweep;
                }
            }

            using var topFont = new Font("Segoe UI Semibold", 22f);
            using var botFont = new Font("Segoe UI", 8.5f);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var navy = new SolidBrush(Color.FromArgb(0x0A, 0x16, 0x33));
            using var muted = new SolidBrush(Color.FromArgb(0x6B, 0x7A, 0x9A));

            g.DrawString(CenterTop, topFont, navy,
                new RectangleF(rect.X, rect.Y, rect.Width, rect.Height - 10), sf);
            g.DrawString(CenterBottom, botFont, muted,
                new RectangleF(rect.X, rect.Y + 24, rect.Width, rect.Height - 10), sf);

            if (Segments.Count > 0)
            {
                using var legendFont = new Font("Segoe UI", 9f);
                float y = chartH + 6;
                float totalWidth = 0;
                var widths = new float[Segments.Count];
                for (int i = 0; i < Segments.Count; i++)
                {
                    var s = Segments[i];
                    var w = g.MeasureString(s.Label, legendFont).Width + 26;
                    widths[i] = w;
                    totalWidth += w;
                }
                float x = Math.Max(0, (Width - totalWidth) / 2f);

                for (int i = 0; i < Segments.Count; i++)
                {
                    var s = Segments[i];
                    using (var b = new SolidBrush(s.Color))
                        g.FillRectangle(b, x + 4, y + 4, 12, 12);
                    using (var txt = new SolidBrush(Color.FromArgb(0x4A, 0x5A, 0x78)))
                        g.DrawString(s.Label, legendFont, txt, x + 22, y + 1);
                    x += widths[i];
                }
            }

            // Tooltip
            if (_hover >= 0 && _hover < Segments.Count)
            {
                var seg = Segments[_hover];
                double sum = Segments.Sum(s => s.Value);
                double share = sum > 0 ? seg.Value / sum * 100.0 : 0;

                // Anchor just above the top of the arc
                float cx = Width / 2f;
                float cy = chartH / 2f - (size / 2f) - 4f;
                SimpleChartUtil.DrawTooltip(g, TipFont, seg.Label,
                    $"{seg.Value:N0}  ·  {share:0.#}%",
                    new PointF(cx, cy), ClientRectangle);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Location);
            if (idx != _hover) { _hover = idx; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Invalidate(); }
        }

        private int HitTest(Point pt)
        {
            if (_arcRect.Width <= 0 || Segments.Count == 0) return -1;

            float cx = _arcRect.Left + _arcRect.Width / 2f;
            float cy = _arcRect.Top + _arcRect.Height / 2f;
            float dx = pt.X - cx;
            float dy = pt.Y - cy;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            float outerR = _arcRect.Width / 2f + _thick / 2f;
            float innerR = _arcRect.Width / 2f - _thick / 2f;
            if (dist < innerR || dist > outerR) return -1;

            // atan2 with -90° offset to match the drawing start angle
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;   // -180..180
            angle += 90;                                            // now 0 = top
            if (angle < 0) angle += 360;

            double total = Segments.Sum(s => s.Value);
            if (total <= 0) return -1;

            double accum = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                double sweep = Segments[i].Value / total * 360.0;
                if (angle >= accum && angle < accum + sweep) return i;
                accum += sweep;
            }
            return -1;
        }
    }

    /// <summary>Horizontal bar chart with category labels on the left and hover tooltips.</summary>
    public class BarChartSimple : Control
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, double Value)> Items { get; set; } = new();

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BarColor { get; set; } = Color.FromArgb(0x1E, 0x88, 0xE5);

        /// <summary>Optional formatter for the value shown in the tooltip. Defaults to N0.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<double, string> TipValueFormat { get; set; }

        private static readonly Font TipFont = new("Segoe UI", 8.5f);
        private int _hover = -1;
        private int _padL, _padT, _rowH, _barH;
        private int _cw;
        private double _max;

        public BarChartSimple()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (Items.Count == 0)
            {
                using var f = new Font("Segoe UI", 10f);
                using var b = new SolidBrush(Color.FromArgb(0x9A, 0xA7, 0xBF));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("No data for this period", f, b, ClientRectangle, sf);
                return;
            }

            using var labelFont = new Font("Segoe UI", 8.5f);
            using var valueFont = new Font("Segoe UI Semibold", 8.5f);
            using var labelBrush = new SolidBrush(Color.FromArgb(0x4A, 0x5A, 0x78));

            float longest = Items.Max(i => g.MeasureString(i.Label, labelFont).Width);
            int padL = (int)Math.Min(Math.Max(longest + 16f, 90f), Width * 0.42f);
            int padR = 60;
            int padT = 8, padB = 8;

            int cw = Width - padL - padR;
            int ch = Height - padT - padB;
            if (cw < 40 || ch < 40) return;

            double max = Math.Max(1, Items.Max(i => i.Value));
            int n = Items.Count;
            int rowH = ch / n;
            int barH = Math.Max(8, Math.Min(24, (int)(rowH * 0.55f)));

            _padL = padL;
            _padT = padT;
            _rowH = rowH;
            _barH = barH;
            _cw = cw;
            _max = max;

            using var rowFmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            using var leftFmt = new StringFormat { LineAlignment = StringAlignment.Center };
            using var barBrush = new SolidBrush(BarColor);
            using var barHoverBrush = new SolidBrush(ControlPaint.Dark(BarColor, 0.08f));

            for (int i = 0; i < n; i++)
            {
                var it = Items[i];
                int y = padT + i * rowH + (rowH - barH) / 2;
                int bw = Math.Max(2, (int)(cw * (it.Value / max)));

                g.DrawString(it.Label, labelFont, labelBrush,
                    new RectangleF(0, y, padL - 10, barH), rowFmt);

                using (var path = SimpleChartUtil.RoundedRect(new RectangleF(padL, y, bw, barH), barH / 2f))
                    g.FillPath(i == _hover ? barHoverBrush : barBrush, path);

                g.DrawString(it.Value.ToString("N0"), valueFont, labelBrush,
                    new RectangleF(padL + bw + 8, y, padR + 40, barH), leftFmt);
            }

            // Tooltip
            if (_hover >= 0 && _hover < n)
            {
                var it = Items[_hover];
                double total = Items.Sum(x => x.Value);
                double share = total > 0 ? it.Value / total * 100.0 : 0;
                string value = TipValueFormat != null
                    ? $"{TipValueFormat(it.Value)}  ·  {share:0.#}%"
                    : $"{it.Value:N0}  ·  {share:0.#}%";

                int y = padT + _hover * rowH + (rowH - barH) / 2;
                int bw = Math.Max(2, (int)(cw * (it.Value / max)));
                SimpleChartUtil.DrawTooltip(g, TipFont, it.Label, value,
                    new PointF(padL + bw / 2f, y), ClientRectangle);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = -1;
            int n = Items.Count;
            if (n > 0 && _rowH > 0)
            {
                int row = (e.Y - _padT) / _rowH;
                if (e.Y >= _padT && row >= 0 && row < n) idx = row;
            }
            if (idx != _hover) { _hover = idx; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Invalidate(); }
        }
    }
}