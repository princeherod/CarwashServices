using System;
using System.Drawing;
using System.Windows.Forms;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Asks the Admin / Manager for a rejection reason.
    /// Returns OK when a non-empty reason is entered.
    /// </summary>
    public class RejectFollowUpDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Red = Color.FromArgb(0xC6, 0x28, 0x28);

        private TextBox _reasonTxt = null!;

        public string RejectionReason => _reasonTxt.Text.Trim();

        public RejectFollowUpDialog()
        {
            Text = "Reject Follow-Up";
            ClientSize = new Size(520, 320);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            Controls.Add(new Label
            {
                Text = "Reject this follow-up?",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(30, 24),
                AutoSize = true
            });

            Controls.Add(new Label
            {
                Text = "The Service Staff will see this reason.",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(30, 58),
                AutoSize = true
            });

            Controls.Add(new Label
            {
                Text = "REJECTION REASON *",
                ForeColor = Muted,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(30, 96),
                AutoSize = true
            });

            _reasonTxt = new TextBox
            {
                Location = new Point(30, 118),
                Width = 460,
                Height = 100,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                PlaceholderText = "Why is this follow-up being rejected?",
                ScrollBars = ScrollBars.Vertical
            };
            Controls.Add(_reasonTxt);

            var cancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted,
                BackColor = Color.White,
                Size = new Size(110, 42),
                Location = new Point(270, 240),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            cancel.FlatAppearance.BorderColor = BorderSoft;
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            var reject = new Button
            {
                Text = "Reject Follow-Up",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Red,
                Size = new Size(160, 42),
                Location = new Point(390, 240),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            reject.FlatAppearance.BorderSize = 0;
            reject.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_reasonTxt.Text))
                {
                    MessageBox.Show("Please enter a rejection reason.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _reasonTxt.Focus();
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(reject);

            CancelButton = cancel;
            AcceptButton = reject;
        }
    }
}