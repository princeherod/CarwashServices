using System.Drawing;
using System.Windows.Forms;

namespace CarwashServices.Common
{
    public static class Theme
    {
        // ---- Colors ----
        public static readonly Color DarkBlue = Color.FromArgb(0x0D, 0x47, 0xA1);
        public static readonly Color MediumBlue = Color.FromArgb(0x19, 0x76, 0xD2);
        public static readonly Color LightBlueBg = Color.FromArgb(0xE3, 0xF2, 0xFD);
        public static readonly Color VeryLightBlue = Color.FromArgb(0xBB, 0xDE, 0xFB);
        public static readonly Color White = Color.White;
        public static readonly Color BodyText = Color.FromArgb(0x21, 0x21, 0x21);
        public static readonly Color Success = Color.FromArgb(0x2E, 0x7D, 0x32);
        public static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        public static readonly Color Warning = Color.FromArgb(0xEF, 0x6C, 0x00);
        public static readonly Color PanelBorder = Color.FromArgb(0x90, 0xCA, 0xF9);

        // ---- Fonts ----
        public static readonly Font TitleFont = new Font("Segoe UI Semibold", 14f);
        public static readonly Font HeaderFont = new Font("Segoe UI Semibold", 10f);
        public static readonly Font BodyFont = new Font("Segoe UI", 9.5f);
        public static readonly Font ButtonFont = new Font("Segoe UI Semibold", 9.5f);

        // ---- Button styling helper ----
        public static void StyleButton(Button btn, Color backColor)
        {
            btn.BackColor = backColor;
            btn.ForeColor = White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = ButtonFont;
            btn.Cursor = Cursors.Hand;
            btn.Height = 34;
        }

        public static void StyleTextBox(TextBox tb)
        {
            tb.Font = BodyFont;
            tb.BackColor = White;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Height = 28;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = White;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = PanelBorder;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = MediumBlue;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = White;
            grid.ColumnHeadersDefaultCellStyle.Font = HeaderFont;
            grid.ColumnHeadersHeight = 34;
            grid.RowTemplate.Height = 30;
            grid.DefaultCellStyle.Font = BodyFont;
            grid.DefaultCellStyle.SelectionBackColor = VeryLightBlue;
            grid.DefaultCellStyle.SelectionForeColor = BodyText;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(0xF5, 0xFA, 0xFF);
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }
    }
}