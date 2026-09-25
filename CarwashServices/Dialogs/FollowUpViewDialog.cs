using System;
using System.Drawing;
using System.Windows.Forms;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Read-only "View" dialog for a follow-up. Shows everything the
    /// Admin / Manager needs before approving or rejecting.
    /// </summary>
    public class FollowUpViewDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color BgCard = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color Amber = Color.FromArgb(0xC8, 0x6D, 0x00);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);

        public FollowUpViewDialog(
            int followUpId,
            string customerName,
            string customerEmail,
            string createdBy,
            string reason,
            string discountOffer,
            DateTime? validUntil,
            string sendVia,
            DateTime sendOn,
            string messagePreview,
            string status,
            string approvalStatus,
            string? rejectionReason,
            string? approvedByName,
            DateTime? approvedAt)
        {
            Text = $"Follow-Up #{followUpId}";
            ClientSize = new Size(620, 720);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.White };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Controls.Add(header);

            header.Controls.Add(new Label
            {
                Text = $"Follow-Up #{followUpId}",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(30, 22),
                AutoSize = true
            });

            // ---- Footer ----
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = Color.White };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            Controls.Add(footer);

            var close = new Button
            {
                Text = "Close",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                Size = new Size(120, 42),
                Location = new Point(620 - 120 - 30, 12),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);

            CancelButton = close;

            // ---- Body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(30, 16, 30, 16)
            };
            Controls.Add(body);
            body.BringToFront();

            int y = 0;
            int colW = 560;

            AddField(body, "CUSTOMER", customerName, 0, ref y, colW);
            AddField(body, "CUSTOMER EMAIL", customerEmail, 0, ref y, colW);
            AddField(body, "CREATED BY", createdBy, 0, ref y, colW);
            AddField(body, "SEND VIA", sendVia, 0, ref y, colW);
            AddField(body, "SEND ON", sendOn.ToString("yyyy-MM-dd hh:mm tt"), 0, ref y, colW);

            AddField(body, "REASON", reason, 0, ref y, colW, multiline: true);

            AddField(body, "DISCOUNT OFFER", discountOffer, 0, ref y, colW);
            AddField(body, "VALID UNTIL",
                validUntil.HasValue ? validUntil.Value.ToString("yyyy-MM-dd") : "—",
                0, ref y, colW);

            AddField(body, "STATUS", status, 0, ref y, colW);

            // Approval section
            AddSeparator(body, 0, ref y, colW, "APPROVAL");
            AddField(body, "APPROVAL STATUS", approvalStatus, 0, ref y, colW);

            if (!string.IsNullOrWhiteSpace(approvedByName))
                AddField(body, "APPROVED BY", approvedByName!, 0, ref y, colW);
            if (approvedAt.HasValue)
                AddField(body, "APPROVED AT", approvedAt.Value.ToString("yyyy-MM-dd hh:mm tt"), 0, ref y, colW);

            if (!string.IsNullOrWhiteSpace(rejectionReason))
            {
                var lbl = AddField(body, "REJECTION REASON", rejectionReason!, 0, ref y, colW, multiline: true);
                lbl.ForeColor = Red;
            }

            AddField(body, "MESSAGE PREVIEW", messagePreview, 0, ref y, colW, multiline: true);

            y += 24;
            body.AutoScrollMinSize = new Size(colW + 60, y);
        }

        private static Label AddField(Control parent, string caption, string value,
                                       int x, ref int y, int width, bool multiline = false)
        {
            parent.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(x, y),
                AutoSize = true
            });
            y += 20;

            int h = multiline ? 60 : 22;
            var valueLbl = new Label
            {
                Text = value,
                ForeColor = Navy,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(x, y),
                Size = new Size(width, h),
                AutoSize = false,
                BackColor = BgCard,
                Padding = new Padding(8, 3, 8, 3),
                TextAlign = multiline ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft
            };
            parent.Controls.Add(valueLbl);

            y += h + 14;
            return valueLbl;
        }

        private static void AddSeparator(Control parent, int x, ref int y, int width, string caption)
        {
            y += 8;
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, 20),
                BackColor = Color.White
            };
            panel.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8f),
                Location = new Point(0, 4),
                AutoSize = true
            });
            panel.Controls.Add(new Panel
            {
                Location = new Point(80, 12),
                Size = new Size(width - 80, 1),
                BackColor = BorderSoft
            });
            parent.Controls.Add(panel);
            y += 28;
        }
    }
}