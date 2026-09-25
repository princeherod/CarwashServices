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
        //  ROLE GUARD
        // ================================================================
        private bool CanAccess(string moduleKey)
        {
            var allowed = RoleRouter.ModulesFor(SessionUser.Role);
            return Array.IndexOf(allowed, moduleKey) >= 0;
        }

        // ================================================================
        //  DRILL-DOWN NAVIGATION
        // ================================================================
        public void NavigateToCustomers(string segment = "All",
                                        int? focusCustomerId = null,
                                        string source = null)
        {
            if (!CanAccess("Manage Customers")) return;
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

        public void NavigateToServiceRequests(string status = "All",
                                              string service = null,
                                              string vehicle = null,
                                              int? focusRequestId = null,
                                              string source = null)
        {
            if (!CanAccess("Manage Service Requests")) return;
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

        public void NavigateToAssignedRequests(string statusFilter = "All statuses")
        {
            if (SessionUser.Role != UserRole.ServiceStaff) return;

            NavigateTo("View Assigned Requests");

            foreach (Control c in _contentPanel.Controls)
            {
                if (c is Roles.ServiceStaff.ServiceStaffAssignedRequestsView arv)
                {
                    arv.ApplyStatusFilter(statusFilter);
                    break;
                }
            }
        }

        public void NavigateToFollowUps(string status = "All",
                                        int? focusFollowUpId = null,
                                        string source = null)
        {
            if (!CanAccess("Follow-Ups / Reminders")) return;
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
            if (!CanAccess("Follow-Ups / Reminders")) return;
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

        // ================================================================
        //  NAVIGATION
        // ================================================================
        private void NavigateTo(string key)
        {
            if (!CanAccess(key))
            {
                MessageBox.Show(
                    $"Access denied.\n\nYour role ({SessionUser.Role}) is not authorized to open '{key}'.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _sidebar.SetActiveModule(_sidebar.ActiveModuleKey);
                return;
            }

            if (ComingSoonModules.Contains(key))
            {
                MessageBox.Show(
                    $"Coming Soon\n\n'{key}' is currently under development.",
                    "Coming Soon",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                _sidebar.SetActiveModule(_sidebar.ActiveModuleKey);
                return;
            }

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
                        if (SessionUser.Role == UserRole.ServiceStaff)
                        {
                            view = new Roles.ServiceStaff.ServiceStaffDashboardView();
                            headerText = "MY DASHBOARD";
                        }
                        else
                        {
                            view = new DashboardView();
                            headerText = "VIEW DASHBOARD";
                        }
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

                    case "Assign Service Staff":
                        view = new AssignServiceStaffView();
                        headerText = "ASSIGN SERVICE STAFF";
                        break;

                    case "Monitor Service Status":
                        view = new MonitorServiceStatusView();
                        headerText = "MONITOR SERVICE STATUS";
                        break;
                    case "Follow-Ups / Reminders":
                        if (SessionUser.Role == UserRole.ServiceStaff)
                        {
                            view = new Roles.ServiceStaff.ServiceStaffFollowUpsView();
                            headerText = "FOLLOW-UPS / REMINDERS";
                        }
                        else
                        {
                            view = new FollowUpsView();
                            headerText = "FOLLOW-UPS / REMINDERS";
                        }
                        break;

                    case "View Assigned Requests":
                        if (SessionUser.Role == UserRole.ServiceStaff)
                        {
                            view = new Roles.ServiceStaff.ServiceStaffAssignedRequestsView();
                            headerText = "VIEW ASSIGNED REQUESTS";
                        }
                        else
                        {
                            ShowComingSoon(key);
                            return;
                        }
                        break;
                    case "Update Service Status":
                        if (SessionUser.Role == UserRole.ServiceStaff)
                        {
                            view = new Roles.ServiceStaff.UpdateServiceStatusView();
                            headerText = "UPDATE SERVICE STATUS";
                        }
                        else
                        {
                            ShowComingSoon(key);
                            return;
                        }
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