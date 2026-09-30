using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

using CarwashServices.Auth;
using CarwashServices.Common;
using CarwashServices.Dtos;
using CarwashServices.Roles;
using CarwashServices.Roles.Admin;
using CarwashServices.Roles.SuperAdmin;

namespace CarwashServices.Shell
{
    public class MainForm : Form
    {
        private Sidebar _sidebar;
        private Panel _headerPanel;
        private Label _titleLabel;
        private Panel _contentPanel;

        private Panel _cloudSyncContainer;
        private Label _cloudDotLabel;
        private Label _cloudTextLabel;
        private Button _cloudSyncButton;
        private System.Windows.Forms.Timer _cloudSyncTimer;
        private ToolTip _cloudTooltip;

        private Panel? _branchSwitcherContainer;
        private ComboBox? _branchSwitcherCombo;
        private bool _isSwitchingBranch;

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
            Text = !string.IsNullOrWhiteSpace(SessionUser.CompanyName)
                ? $"{SessionUser.CompanyName} - Management System"
                : (SessionUser.Role == UserRole.SuperAdmin ? "Super Admin Platform - Carwash CRM" : "Carwash CRM");
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

            var theme = TenantThemeManager.Current;

            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = theme.HeaderBg
            };

            _titleLabel = new Label
            {
                Text = "",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 14f),
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(24, 16),
                Visible = false // Removed duplicate title from top bar as per requirement 5
            };
            _headerPanel.Controls.Add(_titleLabel);
            InitCloudSyncUi();
            InitBranchSwitcherUi();
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
            if (SessionUser.Role == UserRole.SuperAdmin) return true;
            if (moduleKey == "Branches" || moduleKey == "Branching")
            {
                if (SessionUser.MultiBranchEnabled) return true;
            }
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

        public void NavigateToFollowUpsWithCustomers(
            List<int> customerIds,
            string? defaultReason = null,
            Dictionary<int, Dtos.SegmentCustomerDto>? segmentInfo = null)
        {
            if (!CanAccess("Follow-Ups / Reminders")) return;
            NavigateTo("Follow-Ups / Reminders");

            foreach (Control c in _contentPanel.Controls)
            {
                if (c is FollowUpsView fuv)
                {
                    fuv.OpenAddDialogWithCustomers(customerIds, defaultReason, segmentInfo);
                    break;
                }
            }
        }

        // ================================================================
        //  NAVIGATION
        // ================================================================
        private void NavigateTo(string key)
        {
            // Role guards for Super Admin modules (reachable only by RoleId == 4)
            if (key is "Admin Panel" or "Manage Admin Accounts" or
                       "Business Intelligence" or
                       "Subscriptions" or "Manage Subscription / Billing" or "Manage Subscription/Billing" or "Subscription & Billing Management" or
                       "Manage Businesses" or
                       "Backup" or "Backup & Restore Data" or "Backup & Restore" or "Backup and Restore Data")
            {
                if (SessionUser.RoleId != 4 && SessionUser.Role != UserRole.SuperAdmin)
                {
                    ShowAccessDenied(key, "Super Admin (Role 4)");
                    return;
                }
            }
            // Terms & Conditions guard: reachable by Role 1 (Admin) and Role 4 (Super Admin)
            else if (key == "Terms & Conditions")
            {
                if (SessionUser.RoleId != 4 && SessionUser.RoleId != 1)
                {
                    ShowAccessDenied(key, "Super Admin (Role 4) or Admin (Role 1)");
                    return;
                }
            }
            else if (!CanAccess(key))
            {
                ShowAccessDenied(key, $"Authorized Roles for {key}");
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

                    case "Admin Panel":
                        view = new DashboardView();
                        headerText = "ADMIN PANEL";
                        break;

                    case "Manage Admin Accounts":
                        view = new UsersView(roleFilter: 1, customTitle: "Manage Admin Accounts");
                        headerText = "MANAGE ADMIN ACCOUNTS";
                        break;

                    case "Business Intelligence":
                        view = new AnalyticsView();
                        headerText = "BUSINESS INTELLIGENCE";
                        break;

                    case "Subscriptions":
                    case "Manage Subscription / Billing":
                    case "Manage Subscription/Billing":
                    case "Subscription & Billing Management":
                        view = new Roles.SuperAdmin.ManageSubscriptionBillingView();
                        headerText = "SUBSCRIPTIONS";
                        break;

                    case "Manage Businesses":
                        view = new Roles.SuperAdmin.ManageBusinessesView();
                        headerText = "MANAGE BUSINESSES";
                        break;

                    case "Backup":
                    case "Backup & Restore":
                    case "Backup & Restore Data":
                    case "Backup and Restore Data":
                        view = new Roles.SuperAdmin.BackupRestoreView();
                        headerText = "BACKUP & RESTORE";
                        break;

                    case "Settings":
                        using (var settingsDlg = new Dialogs.SettingsDialog())
                        {
                            settingsDlg.ShowDialog(this);
                        }
                        _sidebar.SetActiveModule(_sidebar.ActiveModuleKey);
                        return;

                    case "Terms & Conditions":
                        bool isReadOnly = SessionUser.RoleId != 4;
                        view = new Roles.SuperAdmin.TermsAndConditionsView(isReadOnly);
                        headerText = SuperAdminLabels.NavTermsAndConditions;
                        break;

                    case "Branches":
                    case "Branching":
                        view = new Roles.BranchesView();
                        headerText = "BRANCH MANAGEMENT";
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

        private void ShowAccessDenied(string key, string requiredRole)
        {
            _contentPanel.SuspendLayout();
            try
            {
                foreach (Control c in _contentPanel.Controls)
                    c.Dispose();
                _contentPanel.Controls.Clear();

                var deniedView = new Roles.AccessDeniedView(key, requiredRole, () =>
                {
                    RedirectToAuthorized();
                });
                deniedView.Dock = DockStyle.Fill;
                _contentPanel.Controls.Add(deniedView);
                _titleLabel.Text = "403 — ACCESS DENIED";
                _sidebar.SetActiveModule(null);
            }
            finally
            {
                _contentPanel.ResumeLayout(true);
            }
        }

        private void RedirectToAuthorized()
        {
            var modules = RoleRouter.ModulesFor(SessionUser.Role);
            if (modules.Length > 0)
            {
                NavigateTo(modules[0]);
            }
        }

        // ================================================================
        //  MONSTERASP CLOUD SYNC UI & MONITORING
        // ================================================================
        private void InitCloudSyncUi()
        {
            _cloudTooltip = new ToolTip();

            _cloudSyncContainer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 270,
                BackColor = Color.Transparent
            };

            _cloudDotLabel = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x4C, 0xAF, 0x50), // Green default
                AutoSize = true,
                Location = new Point(10, 18)
            };

            _cloudTextLabel = new Label
            {
                Text = "Cloud: Online",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(28, 21)
            };

            _cloudSyncButton = new Button
            {
                Text = "☁ Sync Now",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0x19, 0x76, 0xD2),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Size = new Size(100, 30),
                Location = new Point(160, 15),
                Cursor = Cursors.Hand
            };
            _cloudSyncButton.FlatAppearance.BorderSize = 0;
            _cloudSyncButton.Click += async (s, e) => await TriggerManualSyncAsync();

            _cloudSyncContainer.Controls.Add(_cloudDotLabel);
            _cloudSyncContainer.Controls.Add(_cloudTextLabel);
            _cloudSyncContainer.Controls.Add(_cloudSyncButton);

            _headerPanel.Controls.Add(_cloudSyncContainer);

            _cloudSyncTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            _cloudSyncTimer.Tick += async (s, e) => await RefreshCloudSyncStatusAsync();
            _cloudSyncTimer.Start();

            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(async () => await RefreshCloudSyncStatusAsync()));
                }
            });
        }

        private async Task RefreshCloudSyncStatusAsync()
        {
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };
                http.Timeout = TimeSpan.FromSeconds(4);
                var status = await http.GetFromJsonAsync<CloudSyncStatusResponse>("api/cloud-sync/status");
                if (status != null && !IsDisposed)
                {
                    if (status.IsCloudOnline)
                    {
                        if (status.PendingCount > 0)
                        {
                            _cloudDotLabel.ForeColor = Color.FromArgb(0x60, 0xA5, 0xFA); // Ice Blue
                            _cloudTextLabel.Text = $"Syncing ({status.PendingCount})...";
                        }
                        else
                        {
                            _cloudDotLabel.ForeColor = Color.FromArgb(0x38, 0xB6, 0xFF); // Sky Blue (Online)
                            _cloudTextLabel.Text = "Cloud: Online";
                        }
                    }
                    else
                    {
                        _cloudDotLabel.ForeColor = Color.FromArgb(0x64, 0x74, 0x8B); // Slate Blue (Offline)
                        _cloudTextLabel.Text = status.PendingCount > 0 ? $"Offline ({status.PendingCount} q'd)" : "Cloud: Offline";
                    }

                    _cloudTooltip.SetToolTip(_cloudSyncContainer,
                        $"MonsterASP Cloud ({status.CloudServer})\nStatus: {(status.IsCloudOnline ? "Online" : "Offline (Local)")}\nPending: {status.PendingCount}\nSynced: {status.SyncedCount}\nLast Sync: {(status.LastSyncTime?.ToLocalTime().ToString("g") ?? "Never")}");
                }
            }
            catch
            {
                if (!IsDisposed)
                {
                    _cloudDotLabel.ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E);
                    _cloudTextLabel.Text = "Cloud: Local";
                }
            }
        }

        private async Task TriggerManualSyncAsync()
        {
            try
            {
                _cloudSyncButton.Enabled = false;
                _cloudSyncButton.Text = "Syncing...";
                using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/") };
                http.Timeout = TimeSpan.FromSeconds(30);
                var resp = await http.PostAsync("api/cloud-sync/sync-now", null);
                if (resp.IsSuccessStatusCode)
                {
                    var result = await resp.Content.ReadFromJsonAsync<SyncResultResponse>();
                    MessageBox.Show(
                        result?.Message ?? "Synchronization completed successfully!",
                        "Cloud Sync",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Sync request could not be processed.", "Cloud Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                await RefreshCloudSyncStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync error: {ex.Message}", "Cloud Sync", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _cloudSyncButton.Enabled = true;
                    _cloudSyncButton.Text = "☁ Sync Now";
                }
            }
        }

        private class CloudSyncStatusResponse
        {
            public bool IsCloudOnline { get; set; }
            public int PendingCount { get; set; }
            public int SyncedCount { get; set; }
            public int FailedCount { get; set; }
            public DateTime? LastSyncTime { get; set; }
            public string CloudServer { get; set; } = "";
            public string CloudDatabase { get; set; } = "";
            public string StatusMessage { get; set; } = "";
        }

        private class SyncResultResponse
        {
            public bool Success { get; set; }
            public bool IsCloudOnline { get; set; }
            public int ProcessedCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public string Message { get; set; } = "";
        }

        // ================================================================
        //  BRANCH SWITCHER UI
        // ================================================================
        private void InitBranchSwitcherUi()
        {
            bool isCleanRide = (SessionUser.CompanyCode ?? "").ToUpperInvariant().Contains("CLEAN")
                || (SessionUser.CompanyCode ?? "").ToUpperInvariant() == "COMP003"
                || (SessionUser.CompanyName ?? "").ToLowerInvariant().Contains("cleanride")
                || (SessionUser.Email ?? "").ToLowerInvariant() == "admin@cleanride.com";

            if (!SessionUser.MultiBranchEnabled && !isCleanRide)
                return;

            _branchSwitcherContainer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 270,
                BackColor = Color.Transparent
            };

            var branchIcon = new Label
            {
                Text = "☵ Branch:",
                ForeColor = Color.FromArgb(0x9A, 0xA8, 0xC0),
                Font = new Font("Segoe UI Semibold", 8.5f),
                Location = new Point(6, 21),
                AutoSize = true
            };

            _branchSwitcherCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(72, 16),
                Width = 190,
                BackColor = Color.FromArgb(0x14, 0x2A, 0x52),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            _branchSwitcherCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_isSwitchingBranch || _branchSwitcherCombo.SelectedItem == null) return;
                var item = _branchSwitcherCombo.SelectedItem as ComboItem;
                if (item != null)
                {
                    int? bId = item.Id > 0 ? item.Id : null;
                    SessionUser.SetBranch(bId, item.Text);

                    // Re-navigate to current module so view refreshes
                    if (!string.IsNullOrEmpty(_sidebar.ActiveModuleKey))
                    {
                        NavigateTo(_sidebar.ActiveModuleKey);
                    }
                }
            };

            _branchSwitcherContainer.Controls.Add(branchIcon);
            _branchSwitcherContainer.Controls.Add(_branchSwitcherCombo);

            _headerPanel.Controls.Add(_branchSwitcherContainer);
            _branchSwitcherContainer.BringToFront();

            if (SessionUser.IsSingleBranchUser)
            {
                _branchSwitcherCombo.Items.Clear();
                _branchSwitcherCombo.Items.Add(new ComboItem(SessionUser.AssignedBranchId!.Value, SessionUser.AssignedBranchName ?? "Assigned Branch"));
                _branchSwitcherCombo.SelectedIndex = 0;
                _branchSwitcherCombo.Enabled = false;
                return;
            }

            SessionUser.BranchChanged += () =>
            {
                if (_branchSwitcherCombo != null && !_branchSwitcherCombo.IsDisposed)
                {
                    SyncBranchSwitcherCombo();
                }
            };

            _ = Task.Run(async () =>
            {
                await LoadBranchesIntoSwitcherAsync();
            });
        }

        private async Task LoadBranchesIntoSwitcherAsync()
        {
            if (SessionUser.IsSingleBranchUser) return;

            try
            {
                int companyId = SessionUser.CurrentCompanyId;
                using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5180/"), Timeout = TimeSpan.FromSeconds(5) };
                var branches = await http.GetFromJsonAsync<List<Dtos.BranchDto>>($"api/tenant/{companyId}/branches") ?? new();

                if (IsDisposed || !IsHandleCreated) return;

                Invoke(() =>
                {
                    if (_branchSwitcherCombo == null || _branchSwitcherCombo.IsDisposed) return;
                    _isSwitchingBranch = true;
                    _branchSwitcherCombo.Items.Clear();
                    _branchSwitcherCombo.Items.Add(new ComboItem(0, "All Branches"));

                    int selectIdx = 0;
                    for (int i = 0; i < branches.Count; i++)
                    {
                        var b = branches[i];
                        int itemIdx = _branchSwitcherCombo.Items.Add(new ComboItem(b.BranchId, b.BranchName));
                        if (SessionUser.CurrentBranchId == b.BranchId)
                        {
                            selectIdx = itemIdx;
                        }
                    }

                    _branchSwitcherCombo.SelectedIndex = selectIdx;
                    _isSwitchingBranch = false;
                });
            }
            catch { }
        }

        private void SyncBranchSwitcherCombo()
        {
            if (_branchSwitcherCombo == null || _branchSwitcherCombo.IsDisposed) return;
            _isSwitchingBranch = true;
            for (int i = 0; i < _branchSwitcherCombo.Items.Count; i++)
            {
                var item = _branchSwitcherCombo.Items[i] as ComboItem;
                if (item != null)
                {
                    if ((SessionUser.CurrentBranchId == null || SessionUser.CurrentBranchId == 0) && item.Id == 0)
                    {
                        _branchSwitcherCombo.SelectedIndex = i;
                        break;
                    }
                    if (SessionUser.CurrentBranchId == item.Id)
                    {
                        _branchSwitcherCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            _isSwitchingBranch = false;
        }
    }
}