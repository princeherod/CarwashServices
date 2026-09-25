using System;
using System.Drawing;
using System.Windows.Forms;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Read-only view of a service request. No editing, no status change.
    /// Scrollable body, fixed header and footer.
    /// </summary>
    public class ServiceRequestViewDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color BgCard = Color.FromArgb(0xF7, 0xFA, 0xFD);
        private static readonly Color SectionFg = Color.FromArgb(0x1E, 0x88, 0xE5);

        private const int DialogW = 620;
        private const int DialogH = 720;
        private const int HeaderH = 68;
        private const int FooterH = 66;
        private const int BodyLeft = 30;
        private const int BodyRight = 30;
        private const int FieldW = DialogW - BodyLeft - BodyRight;

        public ServiceRequestViewDialog(
            int requestId,
            DateTime requestedDate,
            DateTime? scheduledDate,
            DateTime? completedDate,
            string priority,
            string status,
            string notes,
            string customerName,
            string customerPhone,
            string customerEmail,
            string customerAddress,
            string serviceName,
            string serviceDescription,
            decimal servicePrice,
            int serviceDuration,
            string assignedStaff,
            string createdBy,
            DateTime createdAt)
        {
            Text = $"Service Request #{requestId}";
            ClientSize = new Size(DialogW, DialogH);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel { Dock = DockStyle.Top, Height = HeaderH, BackColor = Color.White };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Controls.Add(header);

            header.Controls.Add(new Label
            {
                Text = $"Service Request #{requestId}",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(BodyLeft, 22),
                AutoSize = true
            });

            // ---- Footer ----
            var footer = new Panel { Dock = DockStyle.Bottom, Height = FooterH, BackColor = Color.White };
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
                Location = new Point(DialogW - 120 - BodyRight, 12),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);
            CancelButton = close;

            // ---- Scrollable body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(BodyLeft, 16, BodyRight, 16)
            };
            Controls.Add(body);
            body.BringToFront();

            int y = 0;

            // Section: Request Information
            AddSectionHeader(body, "REQUEST INFORMATION", ref y);
            AddField(body, "Request ID", $"#{requestId}", ref y);
            AddField(body, "Requested Date", requestedDate.ToString("yyyy-MM-dd hh:mm tt"), ref y);
            AddField(body, "Scheduled Date",
                scheduledDate.HasValue ? scheduledDate.Value.ToString("yyyy-MM-dd hh:mm tt") : "—", ref y);
            AddField(body, "Completed Date",
                completedDate.HasValue ? completedDate.Value.ToString("yyyy-MM-dd hh:mm tt") : "—", ref y);
            AddField(body, "Priority", priority, ref y);
            AddField(body, "Status", status, ref y);

            // Section: Customer Information
            AddSectionHeader(body, "CUSTOMER INFORMATION", ref y);
            AddField(body, "Customer Name", customerName, ref y);
            AddField(body, "Phone", customerPhone, ref y);
            AddField(body, "Email", customerEmail, ref y);
            AddField(body, "Address", customerAddress, ref y, multiline: true);

            // Section: Service Information
            AddSectionHeader(body, "SERVICE INFORMATION", ref y);
            AddField(body, "Service Name", serviceName, ref y);
            if (!string.IsNullOrWhiteSpace(serviceDescription))
                AddField(body, "Description", serviceDescription, ref y, multiline: true);
            AddField(body, "Price", $"₱{servicePrice:N0}", ref y);
            AddField(body, "Duration", serviceDuration > 0 ? $"{serviceDuration} min" : "—", ref y);

            // Section: Assignment Information
            AddSectionHeader(body, "ASSIGNMENT INFORMATION", ref y);
            AddField(body, "Assigned Staff", assignedStaff, ref y);

            // Section: Additional Information
            AddSectionHeader(body, "ADDITIONAL INFORMATION", ref y);
            AddField(body, "Notes", string.IsNullOrWhiteSpace(notes) ? "—" : notes, ref y, multiline: true);
            AddField(body, "Created By", createdBy, ref y);
            AddField(body, "Created Date", createdAt.ToString("yyyy-MM-dd hh:mm tt"), ref y);

            y += 24;
            body.AutoScrollMinSize = new Size(FieldW, y);
        }

        private static void AddSectionHeader(Control parent, string caption, ref int y)
        {
            y += 10;

            var panel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(FieldW, 22),
                BackColor = Color.White
            };
            panel.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = SectionFg,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(0, 4),
                AutoSize = true
            });
            panel.Controls.Add(new Panel
            {
                Location = new Point(160, 12),
                Size = new Size(FieldW - 160, 1),
                BackColor = BorderSoft
            });
            parent.Controls.Add(panel);
            y += 30;
        }

        private static void AddField(Control parent, string caption, string value, ref int y, bool multiline = false)
        {
            parent.Controls.Add(new Label
            {
                Text = caption,
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 20;

            int h = multiline ? 60 : 24;
            parent.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                ForeColor = Navy,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(0, y),
                Size = new Size(FieldW, h),
                AutoSize = false,
                BackColor = BgCard,
                Padding = new Padding(8, 3, 8, 3),
                TextAlign = multiline ? ContentAlignment.TopLeft : ContentAlignment.MiddleLeft
            });
            y += h + 12;
        }
    }
}