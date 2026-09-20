using System;
using System.Drawing;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Roles;

namespace CarwashServices.Shell
{
    public class MainForm : Form
    {
        private Sidebar _sidebar;
        private Panel _headerPanel;
        private Label _titleLabel;
        private Panel _contentPanel;

        // FIX: composite the entire form's child tree on the GPU — kills most flicker.
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;   // WS_EX_COMPOSITED
                return cp;
            }
        }

        public MainForm()
        {
            Text = "AquaShine CRM";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(0xF0, 0xF4, 0xFA);
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(1200, 700);

            // FIX: disable auto-redraw while we build the shell
            SuspendLayout();

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
                Text = "",
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

            // FIX: turn on double-buffering for the content panel so view swaps don't flicker
            typeof(Panel)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_contentPanel, true);

            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            ResumeLayout(true);

            // Pick a sensible default module for the logged-in role.
            var modules = RoleRouter.ModulesFor(SessionUser.Role);
            if (modules.Length > 0)
                NavigateTo(modules[0]);
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
            NavigateTo("Follow-Ups / Reminders");

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
            // FIX: freeze the panel layout while we swap views
            _contentPanel.SuspendLayout();
            try
            {
                foreach (Control c in _contentPanel.Controls)
                    c.Dispose();
                _contentPanel.Controls.Clear();

                UserControl? view = null;
                string headerText = "";

                switch (key)
                {
                    case "View Dashboard":
                        view = new DashboardView();
                        headerText = "VIEW DASHBOARD";
                        break;

                    case "Analytics":
                        view = new AnalyticsView();
                        headerText = "ANALYTICS";
                        break;

                    case "View Reports":
                        view = new ReportsView();
                        headerText = "VIEW REPORTS";
                        break;

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
            finally
            {
                _contentPanel.ResumeLayout(true);
            }
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