using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Dtos;

namespace CarwashServices.Dialogs
{
    /// <summary>
    /// Simple modal for recording a complaint or feedback against a customer.
    /// The Kind is fixed at construction ("Complaint" or "Feedback").
    /// </summary>
    public class InteractionEditDialog : Form
    {
        private static readonly Color Navy = Color.FromArgb(0x0A, 0x16, 0x33);
        private static readonly Color Muted = Color.FromArgb(0x6B, 0x7A, 0x9A);
        private static readonly Color BorderSoft = Color.FromArgb(0xE1, 0xE7, 0xF0);

        private readonly int _customerId;
        private readonly string _kind;

        private TextBox _titleTxt = null!;
        private TextBox _detailsTxt = null!;
        private ComboBox _severityCombo = null!;
        private ComboBox _statusCombo = null!;

        private static readonly HttpClient Http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5180/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        public InteractionEditDialog(int customerId, string kind)
        {
            _customerId = customerId;
            _kind = kind;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = _kind == "Complaint" ? "Record Complaint" : "Record Feedback";
            Size = new Size(600, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // ---- Header ----
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White
            };
            header.Controls.Add(new Label
            {
                Text = Text,
                ForeColor = Navy,
                Font = new Font("Segoe UI Semibold", 14f),
                Location = new Point(24, 24),
                AutoSize = true
            });
            Controls.Add(header);

            // ---- Body ----
            int y = 90;
            int L = 24;
            int W = 540;

            Controls.Add(Label("Title", L, y)); y += 22;
            _titleTxt = new TextBox
            {
                Location = new Point(L, y),
                Width = W,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_titleTxt);
            y += 40;

            Controls.Add(Label("Details", L, y)); y += 22;
            _detailsTxt = new TextBox
            {
                Location = new Point(L, y),
                Width = W,
                Height = 120,
                Multiline = true,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };
            Controls.Add(_detailsTxt);
            y += 140;

            Controls.Add(Label("Severity", L, y));
            Controls.Add(Label("Status", L + 270, y));
            y += 22;

            _severityCombo = new ComboBox
            {
                Location = new Point(L, y),
                Width = 250,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _severityCombo.Items.AddRange(new object[] { "Low", "Normal", "High" });
            _severityCombo.SelectedIndex = 1;
            Controls.Add(_severityCombo);

            _statusCombo = new ComboBox
            {
                Location = new Point(L + 270, y),
                Width = 250,
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _statusCombo.Items.AddRange(new object[] { "Open", "Resolved" });
            _statusCombo.SelectedIndex = 0;
            Controls.Add(_statusCombo);

            // ---- Footer ----
            var cancelBtn = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted,
                BackColor = Color.White,
                Size = new Size(110, 42),
                Location = new Point(Width - 250, Height - 80),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            cancelBtn.FlatAppearance.BorderColor = BorderSoft;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancelBtn);

            var saveBtn = new Button
            {
                Text = "Save",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = Color.White,
                BackColor = Navy,
                Size = new Size(110, 42),
                Location = new Point(Width - 130, Height - 80),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += async (s, e) => await SaveAsync();
            Controls.Add(saveBtn);
        }

        private Label Label(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5f),
            Location = new Point(x, y),
            AutoSize = true
        };

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(_titleTxt.Text))
            {
                MessageBox.Show("Title is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dto = new
            {
                customerId = _customerId,
                kind = _kind,
                severity = _severityCombo.SelectedItem?.ToString() ?? "Normal",
                title = _titleTxt.Text.Trim(),
                details = _detailsTxt.Text.Trim(),
                status = _statusCombo.SelectedItem?.ToString() ?? "Open",
                createdAt = DateTime.UtcNow
            };

            try
            {
                var resp = await Http.PostAsJsonAsync("api/tenant/1/customer-interactions", dto);
                if (resp.IsSuccessStatusCode)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"Save failed.\n\n{resp.StatusCode}\n\n{body}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Save failed.\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}