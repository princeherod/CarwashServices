using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Reusable helper for rendering and managing the three-dot (⋮) action menu
    /// consistently across all Super Admin modules.
    /// </summary>
    public static class SuperAdminActionMenuHelper
    {
        public const int ActionsColWidth = 80;
        public const int DotsBtnWidth = 40;
        public const int ActionBtnHeight = 28;
        public const int MenuItemHeight = 34;
        public const int MenuWidth = 160;

        // Palette matching CRM & Super Admin design standards
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Blue = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color BlueSoft = Color.FromArgb(0xEA, 0xF2, 0xFD);
        private static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color DangerRed = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color DisabledMuted = Color.FromArgb(0x94, 0xA3, 0xB8);

        /// <summary>
        /// Calculates the centered bounds for the three-dot button inside a cell.
        /// </summary>
        public static Rectangle DotsRect(Rectangle cell)
        {
            if (cell.Width <= 0 || cell.Height <= 0) return Rectangle.Empty;

            int w = Math.Min(DotsBtnWidth, Math.Max(20, cell.Width - 4));
            int h = Math.Min(ActionBtnHeight, Math.Max(20, cell.Height - 4));
            int x = cell.X + (cell.Width - w) / 2;
            int y = cell.Y + (cell.Height - h) / 2;
            return new Rectangle(x, y, w, h);
        }

        /// <summary>
        /// Paints the background, selection, and the three-dot button in a DataGridView cell.
        /// </summary>
        public static void PaintActionsCell(DataGridViewCellPaintingEventArgs e, bool hover)
        {
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background |
                                  DataGridViewPaintParts.Border |
                                  DataGridViewPaintParts.SelectionBackground);

            var rect = DotsRect(e.CellBounds);
            if (!rect.IsEmpty)
            {
                DrawDotsButton(e.Graphics, rect, hover);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Draws the rounded three-dot button (⋮) with hover state.
        /// </summary>
        public static void DrawDotsButton(Graphics g, Rectangle rect, bool hover)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = hover ? BlueSoft : Color.White;
            Color border = hover ? Blue : CardBorder;
            Color dotColor = hover ? Blue : Navy;

            using (var path = RoundedRect(rect, 6))
            using (var fillBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, 1f))
            {
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            const int dotSize = 3;
            int cx = rect.X + (rect.Width - dotSize) / 2;
            int cy = rect.Y + rect.Height / 2;

            using var dotBrush = new SolidBrush(dotColor);
            g.FillEllipse(dotBrush, cx, cy - 7, dotSize, dotSize);
            g.FillEllipse(dotBrush, cx, cy - dotSize / 2, dotSize, dotSize);
            g.FillEllipse(dotBrush, cx, cy + 5, dotSize, dotSize);
        }

        /// <summary>
        /// Creates a rounded rectangle GraphicsPath.
        /// </summary>
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>
        /// Tests if a local mouse coordinate inside a cell hits the three-dot button.
        /// Returns (rowIndex &lt;&lt; 2) if hit, -1 otherwise.
        /// </summary>
        public static int HitTestActions(DataGridView grid, int rowIndex, Point localLocation, string colName = "Actions")
        {
            if (grid.Columns[colName] == null || rowIndex < 0 || rowIndex >= grid.RowCount) return -1;

            var cellBounds = grid.GetCellDisplayRectangle(grid.Columns[colName].Index, rowIndex, false);
            var absolute = new Point(cellBounds.X + localLocation.X, cellBounds.Y + localLocation.Y);

            if (DotsRect(cellBounds).Contains(absolute))
                return rowIndex << 2;

            return -1;
        }

        /// <summary>
        /// Creates a styled ContextMenuStrip matching the CRM system design.
        /// </summary>
        public static ContextMenuStrip CreateMenu()
        {
            return new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = Navy,
                Padding = new Padding(4),
                Renderer = new ToolStripProfessionalRenderer(new MenuColors())
            };
        }

        /// <summary>
        /// Adds a styled menu item to the context menu.
        /// </summary>
        public static ToolStripMenuItem AddMenuItem(ContextMenuStrip menu, string text, Action onClick, bool isDanger = false, bool isEnabled = true)
        {
            var item = new ToolStripMenuItem(text)
            {
                ForeColor = !isEnabled ? DisabledMuted : (isDanger ? DangerRed : Navy),
                Enabled = isEnabled,
                AutoSize = false,
                Height = MenuItemHeight,
                Padding = new Padding(14, 0, 14, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = MenuWidth,
                Font = new Font("Segoe UI", 9.5f)
            };

            if (isEnabled)
            {
                item.Click += (s, e) =>
                {
                    menu.Close();
                    onClick();
                };
            }

            menu.Items.Add(item);
            return item;
        }

        /// <summary>
        /// Shows the context menu anchored below the three-dot action button.
        /// </summary>
        public static void ShowMenu(ContextMenuStrip menu, DataGridView grid, int rowIndex, string colName = "Actions")
        {
            if (grid.Columns[colName] == null || rowIndex < 0 || rowIndex >= grid.RowCount) return;

            var cellBounds = grid.GetCellDisplayRectangle(grid.Columns[colName].Index, rowIndex, false);
            menu.Show(grid,
                new Point(cellBounds.Right - 8, cellBounds.Bottom - 4),
                ToolStripDropDownDirection.BelowLeft);
        }

        /// <summary>
        /// Custom ProfessionalColorTable for clean hover highlights.
        /// </summary>
        public sealed class MenuColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(0xEA, 0xF2, 0xFD);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0xEA, 0xF2, 0xFD);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0xEA, 0xF2, 0xFD);
            public override Color MenuItemBorder => CardBorder;
            public override Color MenuBorder => CardBorder;
        }
    }
}
