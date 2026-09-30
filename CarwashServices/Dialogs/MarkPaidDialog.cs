using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;
using CarwashServices.Shell;

namespace CarwashServices.Dialogs
{
    public class MarkPaidDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color Accent = Color.FromArgb(0x1E, 0x88, 0xE5);
        private static readonly Color AccentHover = Color.FromArgb(0x15, 0x6F, 0xC4);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);
        private static readonly Color Danger = Color.FromArgb(0xC6, 0x28, 0x28);
        private static readonly Color Green = Color.FromArgb(0x16, 0xA3, 0x4A);

        private readonly BillingTransactionItemDto _tx;

        private ComboBox _methodCombo = null!;
        private TextBox _refTxt = null!;
        private Label _errorLbl = null!;
        private Button _saveBtn = null!;
        private Button _cancelBtn = null!;

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        public MarkPaidDialog(BillingTransactionItemDto tx)
        {
            _tx = tx;

            InitializeForm();
            Sidebar.EnableDoubleBuffering(this);
        }

        private static Label Caption(string text, int x, int y) => new()
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private void InitializeForm()
        {
            Text = $"Mark Transaction #{_tx.TransactionId} as Paid";
            ClientSize = new Size(500, 460);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 16)
            };
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var titleLbl = new Label
            {
                Text = "Record Payment",
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 15f),
                Location = new Point(32, 14),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(titleLbl);

            var subtitleLbl = new Label
            {
                Text = $"Confirm payment for {_tx.TenantCompany} — {_tx.AmountFormatted}",
                ForeColor = Muted,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(32, 42),
                AutoSize = true,
                UseMnemonic = false
            };
            header.Controls.Add(subtitleLbl);
            Controls.Add(header);

            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White,
                Padding = new Padding(24, 16, 24, 16)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            _errorLbl = new Label
            {
                Location = new Point(32, 12),
                Size = new Size(220, 52),
                ForeColor = Danger,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            footer.Controls.Add(_errorLbl);

            _saveBtn = new Button
            {
                Text = "Confirm Payment",
                Font = new Font("Segoe UI Semibold", 9.5f),
                Size = new Size(140, 40),
                Location = new Point(footer.ClientSize.Width - 140 - 32, 18),
                BackColor = Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _saveBtn.FlatAppearance.BorderSize = 0;
            _saveBtn.Click += async (s, e) => await SaveAsync();
            footer.Controls.Add(_saveBtn);

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 9.5f),
                Size = new Size(90, 40),
                Location = new Point(footer.ClientSize.Width - 140 - 32 - 100, 18),
                BackColor = Color.White,
                ForeColor = Muted,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            _cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;
            footer.Controls.Add(_cancelBtn);
            Controls.Add(footer);

            // ---- Body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(32, 16, 32, 16)
            };
            Controls.Add(body);
            body.BringToFront();

            int left = 32;
            int w = 436;
            int y = 16;

            // Summary Card
            var summaryCard = new Panel
            {
                Location = new Point(left, y),
                Size = new Size(w, 64),
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC)
            };
            summaryCard.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderSoft);
                e.Graphics.DrawRectangle(pen, 0, 0, summaryCard.Width - 1, summaryCard.Height - 1);
            };

            var txInfoLbl = new Label
            {
                Text = $"Transaction #{_tx.TransactionId}  •  {_tx.PlanName}",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = Navy,
                Location = new Point(14, 12),
                AutoSize = true
            };
            var amtLbl = new Label
            {
                Text = $"Amount Due: {_tx.AmountFormatted}",
                Font = new Font("Segoe UI Bold", 11f),
                ForeColor = Navy,
                Location = new Point(14, 34),
                AutoSize = true
            };
            summaryCard.Controls.Add(txInfoLbl);
            summaryCard.Controls.Add(amtLbl);
            body.Controls.Add(summaryCard);
            y += 80;

            // Payment Method
            body.Controls.Add(Caption("PAYMENT METHOD", left, y));
            y += 20;
            _methodCombo = new ComboBox
            {
                Location = new Point(left, y),
                Width = w,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f)
            };
            _methodCombo.Items.AddRange(new object[] { "GCash", "Cash", "Bank Transfer", "Card" });
            _methodCombo.SelectedIndex = 0;
            _methodCombo.SelectedIndexChanged += (s, e) =>
            {
                var m = _methodCombo.SelectedItem?.ToString() ?? "GCash";
                _refTxt.Text = GenerateReference(m);
            };
            body.Controls.Add(_methodCombo);
            y += 36;

            // Reference Number
            body.Controls.Add(Caption("REFERENCE NUMBER (AUTOMATICALLY GENERATED)", left, y));
            y += 20;
            _refTxt = new TextBox
            {
                Location = new Point(left, y),
                Width = w,
                Font = new Font("Segoe UI", 10f),
                ReadOnly = true,
                TabStop = false,
                BackColor = Color.FromArgb(0xF8, 0xFA, 0xFC),
                ForeColor = Navy,
                Text = GenerateReference("GCash")
            };
            body.Controls.Add(_refTxt);

            var hintLbl = new Label
            {
                Text = "⚡ Reference number is generated automatically based on payment method and timestamp.",
                Font = new Font("Segoe UI", 8.2f),
                ForeColor = Muted,
                Location = new Point(left, y + 32),
                AutoSize = true
            };
            body.Controls.Add(hintLbl);
        }

        private static string GenerateReference(string method)
        {
            string prefix = method.Trim().ToUpperInvariant() switch
            {
                "GCASH" => "GCASH",
                "CASH" => "CASH",
                "BANK TRANSFER" => "BANK",
                "CARD" => "CARD",
                _ => "PAY"
            };
            return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        }

        public void SetInitialPayment(string method, string refNumber)
        {
            int idx = _methodCombo.Items.IndexOf(method);
            if (idx >= 0) _methodCombo.SelectedIndex = idx;
            if (!string.IsNullOrWhiteSpace(refNumber))
                _refTxt.Text = refNumber;
        }

        private async Task SaveAsync()
        {
            _errorLbl.Text = "";

            var method = _methodCombo.SelectedItem?.ToString() ?? "GCash";
            var refNum = _refTxt.Text.Trim();

            if (string.IsNullOrWhiteSpace(refNum))
            {
                refNum = GenerateReference(method);
                _refTxt.Text = refNum;
            }

            _saveBtn.Enabled = false;
            _saveBtn.Text = "Recording...";

            try
            {
                var payload = new
                {
                    transactionId = _tx.TransactionId,
                    paymentMethod = method,
                    referenceNumber = refNum
                };

                var response = await _http.PostAsJsonAsync("api/billing/mark-paid", payload);
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Payment of {_tx.AmountFormatted} for Transaction #{_tx.TransactionId} has been successfully recorded with Reference No. '{refNum}'.", "Payment Recorded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    _errorLbl.Text = string.IsNullOrWhiteSpace(msg) ? "Failed to record payment." : msg;
                }
            }
            catch (Exception ex)
            {
                _errorLbl.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _saveBtn.Enabled = true;
                _saveBtn.Text = "Confirm Payment";
            }
        }
    }
}
