using System;
using System.Drawing;
using System.Windows.Forms;
using CarwashServices.Views;

namespace CarwashServices
{
    public class MainForm : Form
    {
        private Sidebar _sidebar;
        private Panel _headerPanel;
        private Label _titleLabel;
        private Panel _contentPanel;

        public MainForm()
        {
            Text = "Carwash Services CRM";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA);
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(1200, 700);

            // ---- Sidebar ----
            _sidebar = new Sidebar();
            _sidebar.ModuleSelected += Sidebar_ModuleSelected;
            Controls.Add(_sidebar);

            // ---- Header ----
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0x0A, 0x14, 0x28)
            };

            _titleLabel = new Label
            {
                Text = "👥  MANAGE CUSTOMERS",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true,
                Location = new Point(24, 16)
            };
            _headerPanel.Controls.Add(_titleLabel);
            Controls.Add(_headerPanel);
            _headerPanel.BringToFront();

            // ---- Content ----
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA)
            };
            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            // ---- Start on Customers ----
            NavigateTo("Manage Customers");
        }

        private void Sidebar_ModuleSelected(object? sender, string key)
        {
            NavigateTo(key);
        }

        private void NavigateTo(string key)
        {
            // Dispose the previous view
            foreach (Control c in _contentPanel.Controls)
            {
                c.Dispose();
            }
            _contentPanel.Controls.Clear();

            UserControl? view = null;
            string headerText = "";

            switch (key)
            {
                case "Manage Customers":
                    view = new CustomersView();
                    headerText = "👥  MANAGE CUSTOMERS";
                    break;

                case "Manage Services":
                    view = new ServicesView();
                    headerText = "🔧  MANAGE SERVICES";
                    break;

                default:
                    MessageBox.Show($"'{key}' is coming soon.",
                        "Coming Soon", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Revert to the previous active module in the sidebar
                    _sidebar.SetActiveModule(_sidebar.ActiveModuleKey);
                    return;
            }

            view.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(view);
            _titleLabel.Text = headerText;
            _sidebar.SetActiveModule(key);
        }
    }
}