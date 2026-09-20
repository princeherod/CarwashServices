using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CarwashServices.Controls.Charts
{
    // -------------------- DONUT CHART --------------------
    public class DonutChart : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, double Value, Color Color)> Segments { get; set; } = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CenterTop { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CenterBottom { get; set; } = "";

        public DonutChart()
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

            double total = Segments.Sum(s => s.Value);
            if (total <= 0) return;

            int size = Math.Min(Width, Height) - 40;
            if (size < 60) size = 60;
            var rect = new Rectangle(20, 20, size, size);

            float start = -90f;

            foreach (var seg in Segments)
            {
                float sweep = (float)(seg.Value / total * 360.0);
                using var brush = new SolidBrush(seg.Color);
                g.FillPie(brush, rect, start, sweep);
                start += sweep;
            }

            int holeSize = (int)(size * 0.62);
            var holeRect = new Rectangle(
                rect.X + (rect.Width - holeSize) / 2,
                rect.Y + (rect.Height - holeSize) / 2,
                holeSize, holeSize);
            using (var hole = new SolidBrush(BackColor))
                g.FillEllipse(hole, holeRect);

            using var topFont = new Font("Segoe UI Semibold", 18f);
            using var botFont = new Font("Segoe UI", 9f);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.DrawString(CenterTop, topFont, Brushes.Black,
                new Rectangle(rect.X, rect.Y, rect.Width, rect.Height - 12), sf);
            g.DrawString(CenterBottom, botFont, Brushes.Gray,
                new Rectangle(rect.X, rect.Y + 22, rect.Width, rect.Height - 12), sf);
        }
    }

    // -------------------- BAR CHART --------------------
    public class BarChart : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, decimal Value)> Bars { get; set; } = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BarColor { get; set; } = Color.FromArgb(0x42, 0xA5, 0xF5);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color HighlightBarColor { get; set; } = Color.FromArgb(0x0A, 0x16, 0x33);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int HighlightIndex { get; set; } = -1;

        public BarChart()
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

            if (Bars.Count == 0) return;

            int padL = 60, padR = 20, padT = 20, padB = 40;
            int chartW = Width - padL - padR;
            int chartH = Height - padT - padB;

            decimal max = Bars.Max(b => b.Value);
            if (max <= 0) max = 1m;

            using var gridPen = new Pen(Color.FromArgb(0xEE, 0xF1, 0xF5), 1);
            using var labelBrush = new SolidBrush(Color.Gray);
            using var yFont = new Font("Segoe UI", 8f);

            for (int i = 0; i <= 4; i++)
            {
                int y = padT + chartH - (chartH * i / 4);
                g.DrawLine(gridPen, padL, y, padL + chartW, y);

                decimal val = max * i / 4;
                g.DrawString($"₱{val / 1000m:N0}k", yFont, labelBrush, 4, y - 8);
            }

            int n = Bars.Count;
            int slot = chartW / Math.Max(1, n);
            int barW = (int)(slot * 0.55);

            for (int i = 0; i < n; i++)
            {
                var b = Bars[i];
                int h = (int)(chartH * (double)(b.Value / max));
                int x = padL + i * slot + (slot - barW) / 2;
                int y = padT + chartH - h;

                using var brush = new SolidBrush(i == HighlightIndex ? HighlightBarColor : BarColor);
                g.FillRectangle(brush, x, y, barW, h);

                using var xFont = new Font("Segoe UI", 8f);
                var lblRect = new Rectangle(padL + i * slot, padT + chartH + 6, slot, 30);
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(b.Label, xFont, labelBrush, lblRect, sf);
            }
        }
    }

    // -------------------- LINE CHART --------------------
    public class LineChart : Control
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<(string Label, double Value)> Points { get; set; } = new();

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color LineColor { get; set; } = Color.FromArgb(0x1E, 0x88, 0xE5);

        public LineChart()
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

            if (Points.Count < 2) return;

            int padL = 60, padR = 20, padT = 20, padB = 30;
            int chartW = Width - padL - padR;
            int chartH = Height - padT - padB;

            double max = Math.Max(1, Points.Max(p => p.Value));
            double min = 0;

            using var gridPen = new Pen(Color.FromArgb(0xEE, 0xF1, 0xF5), 1);
            using var labelBrush = new SolidBrush(Color.Gray);
            using var yFont = new Font("Segoe UI", 8f);

            for (int i = 0; i <= 4; i++)
            {
                int y = padT + chartH - (chartH * i / 4);
                g.DrawLine(gridPen, padL, y, padL + chartW, y);

                double val = min + (max - min) * i / 4;
                g.DrawString($"{val * 100:N0}%", yFont, labelBrush, 4, y - 8);
            }

            int n = Points.Count;
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                float x = padL + (float)(chartW * i / (double)(n - 1));
                float v = (float)((Points[i].Value - min) / (max - min));
                float y = padT + chartH - (chartH * v);
                pts[i] = new PointF(x, y);
            }

            using var pen = new Pen(LineColor, 2.5f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, pts);

            using var dotBrush = new SolidBrush(LineColor);
            foreach (var p in pts)
                g.FillEllipse(dotBrush, p.X - 3, p.Y - 3, 6, 6);

            using var xFont = new Font("Segoe UI", 8f);
            for (int i = 0; i < n; i++)
            {
                var lblRect = new Rectangle(padL + (int)(chartW * i / (double)(n - 1)) - 20, padT + chartH + 6, 40, 20);
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(Points[i].Label, xFont, labelBrush, lblRect, sf);
            }
        }
    }
}