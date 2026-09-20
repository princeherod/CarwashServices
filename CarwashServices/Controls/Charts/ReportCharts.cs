using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace CarwashServices.Controls.Charts
{
    // ============================================================
    //  Shared helpers
    // ============================================================
    internal static class ChartUtil
    {
        public static readonly Color GridColor = Color.FromArgb(0xEE, 0xF1, 0xF5);
        public static readonly Color AxisText = Color.FromArgb(0x9A, 0xA7, 0xBF);
        public static readonly Color LabelText = Color.FromArgb(0x4A, 0x5A, 0x78);
        public static readonly Color TooltipBg = Color.FromArgb(0x0A, 0x16, 0x33);

        public static void Quality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        /// <summary>Compact currency: ₱850 · ₱1,500 · ₱12.5k · ₱1.2M</summary>
        public static string Money(decimal v)
        {
            decimal a = Math.Abs(v);
            if (a >= 1_000_000m) return $"₱{v / 1_000_000m:0.##}M";
            if (a >= 10_000m) return $"₱{v / 1_000m:0.#}k";
            return $"₱{v:N0}";
        }

        public static decimal NiceStep(decimal max, int divisions)
        {
            if (max <= 0) return 1m;
            decimal raw = max / divisions;
            decimal mag = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)raw)));
            if (mag <= 0) return 1m;
            decimal norm = raw / mag;
            decimal nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
            return nice * mag;
        }

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

        public static void DrawEmpty(Graphics g, Rectangle bounds, string text)
        {
            using var f = new Font("Segoe UI", 10f);
            using var b = new SolidBrush(AxisText);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text, f, b, bounds, sf);
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
            using var dim = new SolidBrush(Color.FromArgb(0xB8, 0xC4, 0xDC));
            using var white = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            g.DrawString(title, font, dim, x + 10f, y + 5f);
            g.DrawString(value, bold, white, x + 10f, y + 5f + s1.Height);
        }
    }

    // ============================================================
    //  Rounded white card that blends into the page background
    // ============================================================
    internal class CardPanel : Panel
    {
        private Color _borderColor = Color.FromArgb(0xE1, 0xE7, 0xF0);

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        public CardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? SystemColors.Control);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);
            using var path = ChartUtil.RoundedRect(r, 12f);
            using var fill = new SolidBrush(BackColor);
            using var pen = new Pen(_borderColor, 1f);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
    }

    internal sealed class SmoothFlowPanel : FlowLayoutPanel
    {
        public SmoothFlowPanel() { DoubleBuffered = true; }
    }

    // ============================================================
    //  Monthly line chart (area fill, hover tooltip, empty state)
    // ============================================================
    public class ReportLineChart : Control
    {
        private const int PadL = 62, PadR = 28, PadT = 24, PadB = 34;

        private static readonly Font AxisFont = new("Segoe UI", 8f);
        private static readonly Font TipFont = new("Segoe UI", 8.5f);

        private List<(string Label, decimal Value)> _points = new();
        private Color _lineColor = Color.FromArgb(0x1E, 0x88, 0xE5);
        private int _hover = -1;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, decimal Value)> Points
        {
            get => _points;
            set { _points = value ?? new(); _hover = -1; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color LineColor
        {
            get => _lineColor;
            set { _lineColor = value; Invalidate(); }
        }

        public ReportLineChart()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        private bool TryLayout(out Rectangle area, out decimal axisMax, out decimal step, out PointF[] pts)
        {
            area = new Rectangle(PadL, PadT, Width - PadL - PadR, Height - PadT - PadB);
            axisMax = 1m;
            step = 1m;
            pts = Array.Empty<PointF>();
            if (area.Width < 20 || area.Height < 20) return false;

            decimal max = _points.Count == 0 ? 0m : _points.Max(p => p.Value);
            if (max <= 0) max = 1000m;

            step = ChartUtil.NiceStep(max, 4);
            axisMax = Math.Ceiling(max / step) * step;

            int n = _points.Count;
            pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                float x = n == 1
                    ? area.Left + area.Width / 2f
                    : area.Left + (float)(area.Width * i / (double)(n - 1));
                float y = area.Bottom - (float)(area.Height * (double)(_points[i].Value / axisMax));
                pts[i] = new PointF(x, y);
            }
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            ChartUtil.Quality(g);
            g.Clear(BackColor);

            if (!TryLayout(out var area, out var axisMax, out var step, out var pts)) return;

            using var gridPen = new Pen(ChartUtil.GridColor, 1f);
            using var axisBrush = new SolidBrush(ChartUtil.AxisText);
            using var farFmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center
            };

            // Y grid + labels
            int ticks = (int)Math.Round(axisMax / step);
            for (int i = 0; i <= ticks; i++)
            {
                decimal v = step * i;
                float y = area.Bottom - (float)(area.Height * (double)(v / axisMax));
                g.DrawLine(gridPen, area.Left, y, area.Right, y);
                g.DrawString(ChartUtil.Money(v), AxisFont, axisBrush,
                    new RectangleF(0, y - 9, PadL - 8, 18), farFmt);
            }

            int n = pts.Length;
            if (n == 0)
            {
                ChartUtil.DrawEmpty(g, area, "No revenue data for this period");
                return;
            }

            // Area fill under the line
            if (n >= 2)
            {
                var poly = new List<PointF>(pts)
                {
                    new PointF(pts[n - 1].X, area.Bottom),
                    new PointF(pts[0].X, area.Bottom)
                };
                using var fill = new LinearGradientBrush(
                    new Rectangle(area.Left, area.Top, area.Width, area.Height + 1),
                    Color.FromArgb(70, _lineColor), Color.FromArgb(0, _lineColor), 90f);
                g.FillPolygon(fill, poly.ToArray());

                using var pen = new Pen(_lineColor, 2.4f) { LineJoin = LineJoin.Round };
                g.DrawLines(pen, pts);
            }

            // X labels (skip some when crowded)
            int stride = Math.Max(1, (int)Math.Ceiling(n * 52.0 / Math.Max(1, area.Width)));
            using var centerFmt = new StringFormat { Alignment = StringAlignment.Center };
            for (int i = 0; i < n; i += stride)
            {
                var rect = new RectangleF(pts[i].X - 26, area.Bottom + 8, 52, 20);
                g.DrawString(_points[i].Label, AxisFont, axisBrush, rect, centerFmt);
            }

            // Hover guide
            if (_hover >= 0 && _hover < n)
            {
                using var guide = new Pen(Color.FromArgb(0xC9, 0xD3, 0xE6), 1f) { DashStyle = DashStyle.Dash };
                g.DrawLine(guide, pts[_hover].X, area.Top, pts[_hover].X, area.Bottom);
            }

            // Dots
            using var dotFill = new SolidBrush(_lineColor);
            using var dotRing = new Pen(Color.White, 2f);
            for (int i = 0; i < n; i++)
            {
                float r = i == _hover ? 6f : 4f;
                g.FillEllipse(dotFill, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
                g.DrawEllipse(dotRing, pts[i].X - r, pts[i].Y - r, r * 2, r * 2);
            }

            // Tooltip
            if (_hover >= 0 && _hover < n)
            {
                ChartUtil.DrawTooltip(g, TipFont, _points[_hover].Label,
                    $"₱{_points[_hover].Value:N0}", pts[_hover], ClientRectangle);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = -1;
            if (TryLayout(out var area, out _, out _, out var pts) && pts.Length > 0)
            {
                float best = float.MaxValue;
                for (int i = 0; i < pts.Length; i++)
                {
                    float d = Math.Abs(pts[i].X - e.X);
                    if (d < best) { best = d; idx = i; }
                }
                float tol = pts.Length > 1
                    ? Math.Max(20f, area.Width / (float)(pts.Length - 1) / 2f)
                    : 30f;
                if (best > tol) idx = -1;
            }
            if (idx != _hover) { _hover = idx; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1) { _hover = -1; Invalidate(); }
        }
    }

    // ============================================================
    //  Horizontal bar chart (Revenue by Service)
    //  Sorted high → low, top N + "Other", value labels, hover share
    // ============================================================
    public class ReportBarChart : Control
    {
        private const int PadT = 16, PadB = 30, PadR = 72;

        private static readonly Font LabelFont = new("Segoe UI", 8.5f);
        private static readonly Font ValueFont = new("Segoe UI Semibold", 8.5f);

        private List<(string Label, decimal Value)> _bars = new();
        private List<(string Label, decimal Value)> _items = new();
        private decimal _total;
        private int _maxBars = 8;
        private int _hover = -1;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, decimal Value)> Bars
        {
            get => _bars;
            set { _bars = value ?? new(); Rebuild(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BarColor { get; set; } = Color.FromArgb(0x22, 0xA0, 0x66);

        /// <summary>Bars beyond this count are grouped into "Other".</summary>
        [DefaultValue(8)]
        public int MaxBars
        {
            get => _maxBars;
            set { _maxBars = Math.Max(2, value); Rebuild(); }
        }

        public ReportBarChart()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        private void Rebuild()
        {
            var sorted = _bars.OrderByDescending(b => b.Value).ToList();
            _total = sorted.Sum(b => b.Value);

            if (sorted.Count > _maxBars)
            {
                var head = sorted.Take(_maxBars - 1).ToList();
                decimal rest = sorted.Skip(_maxBars - 1).Sum(b => b.Value);
                head.Add(("Other", rest));
                sorted = head;
            }
            _items = sorted;
            _hover = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            ChartUtil.Quality(g);
            g.Clear(BackColor);

            if (_items.Count == 0)
            {
                ChartUtil.DrawEmpty(g, ClientRectangle, "No service data for this period");
                return;
            }

            // Left gutter sized to the longest label (capped)
            float longest = _items.Max(i => g.MeasureString(i.Label, LabelFont).Width);
            int padL = (int)Math.Min(Math.Max(longest + 20f, 90f), Width * 0.4f);

            int cw = Width - padL - PadR;
            int ch = Height - PadT - PadB;
            if (cw < 40 || ch < 40) return;

            decimal max = _items.Max(i => i.Value);
            if (max <= 0) max = 1m;
            decimal step = ChartUtil.NiceStep(max, 4);
            decimal axisMax = Math.Ceiling(max / step) * step;

            int n = _items.Count;
            int rowH = ch / n;
            int barH = Math.Max(8, Math.Min(28, (int)(rowH * 0.55)));

            using var labelBrush = new SolidBrush(ChartUtil.LabelText);
            using var axisBrush = new SolidBrush(ChartUtil.AxisText);
            using var gridPen = new Pen(ChartUtil.GridColor, 1f);
            using var hoverBg = new SolidBrush(Color.FromArgb(0xF5, 0xF8, 0xFC));
            using var barBrush = new SolidBrush(BarColor);
            using var barHoverBrush = new SolidBrush(ControlPaint.Dark(BarColor, 0.08f));

            using var rowFmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            using var leftFmt = new StringFormat { LineAlignment = StringAlignment.Center };
            using var centerFmt = new StringFormat { Alignment = StringAlignment.Center };

            // Hovered row highlight
            if (_hover >= 0 && _hover < n)
            {
                using var hp = ChartUtil.RoundedRect(
                    new RectangleF(4, PadT + _hover * rowH, Width - 8, rowH), 8f);
                g.FillPath(hoverBg, hp);
            }

            // X grid + labels
            int ticks = (int)Math.Round(axisMax / step);
            for (int i = 0; i <= ticks; i++)
            {
                decimal v = step * i;
                float x = padL + (float)(cw * (double)(v / axisMax));
                g.DrawLine(gridPen, x, PadT, x, PadT + ch);
                g.DrawString(ChartUtil.Money(v), LabelFont, axisBrush,
                    new RectangleF(x - 32, PadT + ch + 6, 64, 18), centerFmt);
            }

            // Bars
            for (int i = 0; i < n; i++)
            {
                var item = _items[i];
                int y = PadT + i * rowH + (rowH - barH) / 2;
                int bw = Math.Max(2, (int)(cw * (double)(item.Value / axisMax)));

                g.DrawString(item.Label, LabelFont, labelBrush,
                    new RectangleF(0, y, padL - 10, barH), rowFmt);

                using (var path = ChartUtil.RoundedRect(new RectangleF(padL, y, bw, barH), barH / 2f))
                    g.FillPath(i == _hover ? barHoverBrush : barBrush, path);

                g.DrawString(ChartUtil.Money(item.Value), ValueFont, labelBrush,
                    new RectangleF(padL + bw + 8, y, PadR + 40, barH), leftFmt);
            }

            // Tooltip with share of total
            if (_hover >= 0 && _hover < n)
            {
                var item = _items[_hover];
                int y = PadT + _hover * rowH + (rowH - barH) / 2;
                int bw = Math.Max(2, (int)(cw * (double)(item.Value / axisMax)));
                double share = _total > 0 ? (double)(item.Value / _total) * 100.0 : 0;
                ChartUtil.DrawTooltip(g, LabelFont, item.Label,
                    $"₱{item.Value:N0}  ·  {share:0.#}%",
                    new PointF(padL + bw / 2f, y), ClientRectangle);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = -1;
            int n = _items.Count;
            int ch = Height - PadT - PadB;
            if (n > 0 && ch > 0)
            {
                int rowH = Math.Max(1, ch / n);
                int row = (e.Y - PadT) / rowH;
                if (e.Y >= PadT && row >= 0 && row < n) idx = row;
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