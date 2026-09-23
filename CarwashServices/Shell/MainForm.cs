using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Roles;
using CarwashServices.Roles.Admin;

namespace CarwashServices.Shell
{
    public class MainForm : Form
    {
        private Sidebar _sidebar;
        private Panel _headerPanel;
        private Label _titleLabel;
        private Panel _contentPanel;

        public bool SignOutRequested { get; private set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
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

            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
            DoubleBuffered = true;

            SuspendLayout();

            _sidebar = new Sidebar();
            _sidebar.ModuleSelected += Sidebar_ModuleSelected;
            _sidebar.SignOutRequested += (s, e) =>
            {
                SignOutRequested = true;
                Close();
            };

            typeof(Sidebar)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_sidebar, true);

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

            typeof(Panel)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_contentPanel, true);

            Controls.Add(_contentPanel);
            _contentPanel.BringToFront();

            ResumeLayout(true);

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

        // ================================================================
        //  DRILL-DOWN NAVIGATION
        // ================================================================

        /// <summary>
        /// Navigate to Manage Customers, optionally filtered by segment and
        /// optionally focused on a specific tenant customer id.
        /// Pass "All" for segment to show everyone.
        /// </summary>
        public void NavigateToCustomers(string segment = "All",
                                        int? focusCustomerId = null,
                                        string source = null)
        {
            NavigateTo("Manage Customers");

            foreach (Control c in _contentPanel.Controls)
            {
                if (c is CustomersView cv)
                {
                    cv.ApplyDrillDown(segment, focusCustomerId, source);
                    break;
                }
            }
        }

        /// <summary>
        /// Navigate to Manage Service Requests, optionally filtered by status,
        /// service, and vehicle type, and optionally focused on a request id.
        /// </summary>
        public void NavigateToServiceRequests(string status = "All",
                                              string service = null,
                                              string vehicle = null,
                                              int? focusRequestId = null,
                                              string source = null)
        {
            NavigateTo("Manage Service Requests");

            foreach (Control c in _contentPanel.Controls)
            {
                if (c is ServiceRequestsView srv)
                {
                    srv.ApplyDrillDown(status, service, vehicle, focusRequestId, source);
                    break;
                }
            }
        }

        /// <summary>
        /// Navigate to Follow-Ups / Reminders, optionally filtered by status
        /// and optionally focused on a follow-up id.
        /// </summary>
        public void NavigateToFollowUps(string status = "All",
                                        int? focusFollowUpId = null,
                                        string source = null)
        {
            NavigateTo("Follow-Ups / Reminders");

            foreach (Control c in _contentPanel.Controls)
            {
                if (c is FollowUpsView fuv)
                {
                    fuv.ApplyDrillDown(status, focusFollowUpId, source);
                    break;
                }
            }
        }

        public void NavigateToFollowUpsWithCustomers(List<int> customerIds)
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
                        view = new UsersView();
                        headerText = "MANAGE USERS";
                        break;

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