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
            Text = "AquaShine CRM";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA);
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(1200, 700);

            _sidebar = new Sidebar();
            _sidebar.ModuleSelected += Sidebar_ModuleSelected;
            Controls.Add(_sidebar);

            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0x0A, 0x14, 0x28)
            };

            _titleLabel = new Label
            {
                Text = "MANAGE CUSTOMERS",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true,
                Location = new Point(24, 16)
            };
            _headerPanel.Controls.Add(_titleLabel);
            Controls.Add(_headerPanel);
            _headerPanel.BringToFront();

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA)
            };
            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            NavigateTo("Manage Customers");
        }

        private void Sidebar_ModuleSelected(object? sender, string key)
        {
            NavigateTo(key);
        }

        public void NavigateToModule(string key)
        {
            NavigateTo(key);
        }

        public void NavigateToFollowUpsWithCustomers(System.Collections.Generic.List<int> customerIds)
        {
            // 1) Navigate to the module
            NavigateTo("Follow-Ups / Reminders");

            // 2) Find the FollowUpsView we just added and open the dialog with preselection
            foreach (Control c in _contentPanel.Controls)
            {
                if (c is FollowUpsView fuv)
                {
                    fuv.OpenAddDialogWithCustomers(customerIds);
                    break;
                }
            }
        }

        private void NavigateTo(string key)
        {
            foreach (Control c in _contentPanel.Controls)
                c.Dispose();
            _contentPanel.Controls.Clear();

            UserControl? view = null;
            string headerText = "";

            switch (key)
            {
                case "View Dashboard":
                    ShowComingSoon(key);
                    return;

                case "Analytics":
                    view = new AnalyticsView();
                    headerText = "ANALYTICS";
                    break;

                case "View Reports":
                    ShowComingSoon(key);
                    return;

                case "Manage Users":
                    ShowComingSoon(key);
                    return;

                case "Manage Customers":
                    view = new CustomersView();
                    headerText = "MANAGE CUSTOMERS";
                    break;

                case "Manage Services":
                    view = new ServicesView();
                    headerText = "MANAGE SERVICES";
                    break;

                case "Manage Service Requests":
                    view = new ServiceRequestsView();
                    headerText = "MANAGE SERVICE REQUESTS";
                    break;

                case "Follow-Ups / Reminders":
                    view = new FollowUpsView();
                    headerText = "FOLLOW-UPS / REMINDERS";
                    break;

                case "Manage Admin Accounts":
                    ShowComingSoon(key);
                    return;

                default:
                    ShowComingSoon(key);
                    return;
            }

            if (view == null) return;

            view.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(view);
            _titleLabel.Text = headerText;
            _sidebar.SetActiveModule(key);
        }

        private void ShowComingSoon(string key)
        {
            MessageBox.Show(
                $"'{key}' is coming soon.",
                "Coming Soon",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            _sidebar.SetActiveModule(_sidebar.ActiveModuleKey);
        }
    }
}