namespace CarwashServices.Roles.SuperAdmin
{
    /// <summary>
    /// Centralized repository of all user-facing labels, titles, column headers, 
    /// subtitles, and status messages for the Super Admin panel.
    /// Ensures consistent, human-readable Title Case presentation without raw database names.
    /// </summary>
    public static class SuperAdminLabels
    {
        // =====================================================================
        // Navigation & Shell
        // =====================================================================
        public const string BrandTitle = "AquaShine";
        public const string BrandSubtitle = "CRM System";
        public const string SuperAdminRoleTitle = "Super Admin";
        public const string SuperAdminModulesHeader = "Super Admin Modules";
        public const string AccessDeniedHeader = "403 — Access Denied";

        public const string NavManageAdminAccounts = "Manage Admin Accounts";
        public const string NavBackupRestoreData = "Backup & Restore Data";
        public const string NavManageSubscriptionBilling = "Manage Subscription / Billing";
        public const string NavTermsAndConditions = "Terms & Conditions";

        // =====================================================================
        // Module Page Subtitles
        // =====================================================================
        public const string ManageAdminAccountsSubtitle =
            "View and manage Admin and Super Admin accounts";

        public const string BackupRestoreDataSubtitle =
            "Create backups and restore previous data";

        public const string ManageSubscriptionBillingSubtitle =
            "Manage tenant plans, subscriptions, and billing";

        public const string TermsAndConditionsSubtitle =
            "Publish and review versions of the Terms & Conditions";

        public const string TermsAndConditionsAdminSubtitle =
            "Browse platform terms of service and inspect version revisions";

        // =====================================================================
        // Module 1: Manage Admin Accounts
        // =====================================================================
        public const string ButtonNewAdmin = "+  New Admin";
        public const string RolesReferenceTitle = "Administrator Roles Reference";
        public const string ChipSuperAdmin = "Super Admin";
        public const string ChipAdmin = "Admin";

        // Grid Columns
        public const string ColUserId = "User ID";
        public const string ColNameEmail = "Name & Email";
        public const string ColRole = "Role";
        public const string ColStatus = "Status";
        public const string ColDateCreated = "Date Created";
        public const string ColActions = "Actions";
        public const string ActionEdit = "Edit";

        // Dialog: Admin Account Edit
        public const string DialogNewAdminTitle = "Create New Admin Account";
        public const string DialogEditAdminTitle = "Edit Admin Account";
        public const string DialogAdminSubtitle = "Manage credentials, system role assignment, and account status";
        public const string FieldFullName = "Full Name *";
        public const string FieldEmailAddress = "Email Address *";
        public const string FieldRole = "Role *";
        public const string FieldStatus = "Status *";
        public const string HeaderSecurityCredentials = "Security Credentials";
        public const string FieldPasswordRequired = "Password *";
        public const string FieldPasswordOptional = "New Password";
        public const string FieldConfirmPasswordRequired = "Confirm Password *";
        public const string FieldConfirmPasswordOptional = "Confirm New Password";
        public const string HintPasswordRequired = "(Required for new accounts)";
        public const string HintPasswordOptional = "(Leave blank to keep existing password)";

        // =====================================================================
        // Module 2: Backup & Restore Data
        // =====================================================================
        public const string CardCreateBackupTitle = "Create Manual Backup";
        public const string CardCreateBackupDesc = "Creates a manual backup of the current data.";
        public const string BackupDetailsHeader = "Backup Details";
        public const string DestinationDirectoryLabel = "Destination: %USERPROFILE%\\CarwashBackups  ·  Database: MSME_MasterERP";
        public const string ButtonCreateManualBackup = "Create Manual Backup";

        public const string CardRestoreTitle = "Restore from Backup";
        public const string CardRestoreDesc = "Select a verified snapshot from Backup History to roll back the system database.";
        public const string RestoreWarningCaution = "Caution: Restoring a backup overwrites all current database records. Active user connections will be disconnected during restore.";
        public const string ButtonRestoreSelectedBackup = "Restore Selected Backup";
        public const string StatusLoadingBackups = "Loading backups from Backup History...";
        public const string StatusNoBackupsFound = "No successful backups found in Backup History.\nClick 'Create Manual Backup' to create your first snapshot.";
        public const string BackupTypeAuto = "Auto";
        public const string BackupTypeManual = "Manual";

        // =====================================================================
        // Module 3: Manage Subscription & Billing
        // =====================================================================
        public const string TileTotalPaid = "Total Paid";
        public const string BadgeCollected = "✓ Collected";
        public const string TileOutstanding = "Outstanding";
        public const string BadgePending = "⚠ Pending";
        public const string TileActiveSubscriptions = "Active Subscriptions";
        public const string BadgeEnrolled = "● Enrolled";

        // Segmented Tabs
        public const string TabSubscriptionPlans = "Subscription Plans";
        public const string TabCustomerSubscriptions = "Customer Subscriptions";
        public const string TabBillingTransactions = "Billing Transactions";

        // Tab 1: Subscription Plans Columns
        public const string ColPlanId = "Plan ID";
        public const string ColPlanName = "Plan Name";
        public const string ColPlanDetails = "Plan Name & Details";
        public const string ColPrice = "Price";
        public const string ColBillingCycle = "Billing Cycle";
        public const string ColEnrolledTenants = "Enrolled Tenants";
        public const string ColDescription = "Description";

        // Tab 2: Customer Subscriptions Columns
        public const string ColSubscriptionId = "Subscription";
        public const string ColTenantCompany = "Tenant Company";
        public const string ColAdminUser = "Admin User";
        public const string ColSubscriptionPlan = "Subscription Plan";
        public const string ColStartDate = "Start Date";
        public const string ColRenewalDate = "Renewal Date";

        // Tab 3: Billing Transactions Columns
        public const string ColTransactionId = "Transaction ID";
        public const string ColReferenceNumber = "Reference Number";
        public const string ColAmount = "Amount";
        public const string ColTransactionDate = "Transaction Date";
        public const string ColPaymentStatus = "Payment Status";

        // =====================================================================
        // Module 4: Terms & Conditions
        // =====================================================================
        public const string CardVersionHistoryTitle = "Version History";
        public const string CardVersionHistoryDesc = "Historical revisions from newest to oldest. Select any entry to inspect its contents.";
        public const string BadgeLatest = "Latest";
        public const string PrefixEffectiveDate = "Effective Date: ";
        public const string PrefixCreatedBy = "Created By: ";
        public const string CardDocumentInspectorTitle = "Terms of Service";

        // Meta Bar
        public const string MetaTermsId = "Terms ID: ";
        public const string MetaEffectiveDate = "Effective Date: ";
        public const string MetaCreatedBy = "Created By: ";

        public const string ButtonPreview = "👁  Preview";
        public const string ButtonPublishNewVersion = "+ Publish New Version";
        public const string StatusLoadingVersions = "Loading versions...";
        public const string StatusNoVersionsFound = "No terms & conditions found.";

        // Dialog: Terms Preview
        public const string DialogPreviewTitle = "Document Preview — {0}";
        public const string DialogPreviewHeader = "Terms & Conditions of Service";
        public const string DialogPreviewMeta = "Version: {0}   •   Effective Date: {1}   •   Published by: {2}";
        public const string ButtonCopyToClipboard = "Copy to Clipboard";
        public const string ButtonClose = "Close";

        // Dialog: Publish Terms
        public const string DialogPublishTitle = "Publish New Terms & Conditions";
        public const string DialogPublishHeader = "Publish New Terms & Conditions";
        public const string DialogPublishSubtitle = "Modify the active copy below to publish a revision. The version is incremented and effective immediately.";
        public const string FieldNewVersion = "New Version";
        public const string FieldEffectiveDate = "Effective Date";
        public const string FieldPublisher = "Publisher";
        public const string ButtonCancel = "Cancel";
        public const string ButtonPublishRevision = "Publish Revision";
    }
}
