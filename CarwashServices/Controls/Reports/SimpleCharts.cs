using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CarwashServices.Controls.Reports
{
    /// <summary>Ring chart with a coloured legend below it.</summary>
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
                foreach (var seg in Segments)
                {
                    if (seg.Value <= 0) continue;
                    float sweep = (float)(seg.Value / total * 360.0);
                    using var pen = new Pen(seg.Color, thick);
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
        }
    }

    /// <summary>Horizontal bar chart with category labels on the left.</summary>
    public class BarChartSimple : Control
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, double Value)> Items { get; set; } = new();

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BarColor { get; set; } = Color.FromArgb(0x1E, 0x88, 0xE5);

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

            using var rowFmt = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            using var leftFmt = new StringFormat { LineAlignment = StringAlignment.Center };
            using var barBrush = new SolidBrush(BarColor);

            for (int i = 0; i < n; i++)
            {
                var it = Items[i];
                int y = padT + i * rowH + (rowH - barH) / 2;
                int bw = Math.Max(2, (int)(cw * (it.Value / max)));

                g.DrawString(it.Label, labelFont, labelBrush,
                    new RectangleF(0, y, padL - 10, barH), rowFmt);

                using (var path = RoundedRect(new RectangleF(padL, y, bw, barH), barH / 2f))
                    g.FillPath(barBrush, path);

                g.DrawString(it.Value.ToString("N0"), valueFont, labelBrush,
                    new RectangleF(padL + bw + 8, y, padR + 40, barH), leftFmt);
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
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
    }
}