param()

$ErrorActionPreference = 'Stop'

$masterConnStr = 'Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;'
$localMasterConnStr = 'Server=(localdb)\MSSQLLocalDB;Database=MSME_MasterCrm;Trusted_Connection=True;TrustServerCertificate=True;'
$cloudConnStr = 'Server=db67193.public.databaseasp.net;Database=db67193;User Id=db67193;Password=i!8PSn+5-9sR;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30;'

Write-Host "=== 1. ENSURING LOCAL PHYSICAL DATABASES EXIST ==="
$mConn = New-Object System.Data.SqlClient.SqlConnection($masterConnStr)
$mConn.Open()
$dbs = @('aquaShine_db', 'sparkleRide_db', 'cleanRide_db')
foreach ($db in $dbs) {
    $cmd = $mConn.CreateCommand()
    $cmd.CommandText = "IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '$db') CREATE DATABASE [$db];"
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "Physical database '$db' ready."
}
$mConn.Close()

# Ensure branch schema in all tenant databases
foreach ($db in $dbs) {
    $tdbConn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=$db;Trusted_Connection=True;TrustServerCertificate=True;")
    $tdbConn.Open()
    $scmd = $tdbConn.CreateCommand()
    $scmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Branches')
BEGIN
    CREATE TABLE Branches (
        BranchId INT IDENTITY(1,1) PRIMARY KEY,
        BranchCode NVARCHAR(50) NOT NULL,
        BranchName NVARCHAR(200) NOT NULL,
        Address NVARCHAR(500) NULL,
        City NVARCHAR(100) NULL,
        Province NVARCHAR(100) NULL,
        ContactNumber NVARCHAR(50) NULL,
        Email NVARCHAR(200) NULL,
        IsMainBranch BIT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        IsArchived BIT NOT NULL DEFAULT 0,
        ArchivedAt DATETIME2 NULL,
        ArchivedBy NVARCHAR(200) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Branches_IsArchived ON Branches(IsArchived);
    CREATE INDEX IX_Branches_BranchCode ON Branches(BranchCode);
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ServiceRequests' AND COLUMN_NAME = 'BranchId')
    ALTER TABLE ServiceRequests ADD BranchId INT NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TenantCustomers' AND COLUMN_NAME = 'BranchId')
    ALTER TABLE TenantCustomers ADD BranchId INT NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'FollowUps' AND COLUMN_NAME = 'BranchId')
    ALTER TABLE FollowUps ADD BranchId INT NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BillingTransactions' AND COLUMN_NAME = 'BranchId')
    ALTER TABLE BillingTransactions ADD BranchId INT NULL;
"@
    $scmd.ExecuteNonQuery() | Out-Null
    $tdbConn.Close()
}

Write-Host "`n=== 2. RESETTING & SEEDING MSME_MasterCrm ==="
$lConn = New-Object System.Data.SqlClient.SqlConnection($localMasterConnStr)
$lConn.Open()

# Clean up master tables in FK order
$cleanupSql = @"
DELETE FROM LocalSyncQueue;
DELETE FROM BackupLogs;
DELETE FROM ServiceStatusLogs;
DELETE FROM FollowUps;
DELETE FROM BillingTransactions;
DELETE FROM ServiceRequests;
DELETE FROM CustomerSubscriptions;
DELETE FROM Customers;
DELETE FROM Services;
DELETE FROM SubscriptionPlans;
DELETE FROM Devices;
DELETE FROM TenantBillingTransactions;
DELETE FROM TenantSubscriptions;
DELETE FROM TenantSubscriptionPlans;
DELETE FROM TermsConditions;
DELETE FROM Users;
DELETE FROM CompanyDatabases;
DELETE FROM Companies;
"@
$cleanCmd = $lConn.CreateCommand()
$cleanCmd.CommandText = $cleanupSql
$cleanCmd.ExecuteNonQuery() | Out-Null
Write-Host "MSME_MasterCrm tables cleared."

# 1. Seed Roles (Ensure 1-4)
$rolesSql = @"
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleId = 1) INSERT INTO Roles (RoleId, RoleName) VALUES (1, 'Admin');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleId = 2) INSERT INTO Roles (RoleId, RoleName) VALUES (2, 'Manager');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleId = 3) INSERT INTO Roles (RoleId, RoleName) VALUES (3, 'Service Staff');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleId = 4) INSERT INTO Roles (RoleId, RoleName) VALUES (4, 'Super Admin');
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $rolesSql
$cmd.ExecuteNonQuery() | Out-Null

# 2. Seed 3 Companies
$compSql = @"
SET IDENTITY_INSERT Companies ON;

INSERT INTO Companies (
    CompanyId, CompanyCode, CompanyName, IsActive, CreatedAt,
    AddressLine, City, ContactEmail, ContactPhone, Country,
    PostalCode, Province, State, TermsAccepted, TermsAcceptedAt,
    TermsAcceptedBy, TermsAcceptedVersion
) VALUES 
(1, 'COMP001', 'AquaShine Car Wash', 1, '2026-01-01', 'Km 14 West Service Road', 'Paranaque', 'contact@aquashine.com', '+63 917 111 2233', 'Philippines', '1700', 'Metro Manila', 'NCR', 1, '2026-01-02', 'admin@aquashine.com', 'v1.0'),
(2, 'COMP002', 'SparkleRide Auto Wash', 1, '2026-01-05', '88 Shaw Boulevard', 'Mandaluyong', 'support@sparkleride.com', '+63 917 444 5566', 'Philippines', '1550', 'Metro Manila', 'NCR', 1, '2026-01-06', 'admin@sparkleride.com', 'v1.0'),
(3, 'COMP003', 'CleanRide Car Wash', 1, '2026-01-10', 'BGC High Street South', 'Taguig', 'hello@cleanride.com', '+63 917 777 8899', 'Philippines', '1634', 'Metro Manila', 'NCR', 1, '2026-01-11', 'admin@cleanride.com', 'v1.0');

SET IDENTITY_INSERT Companies OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $compSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "3 Companies seeded (AquaShine, SparkleRide, CleanRide)."

# 3. Seed CompanyDatabases
$cdbSql = @"
SET IDENTITY_INSERT CompanyDatabases ON;

INSERT INTO CompanyDatabases (
    CompanyDatabaseId, CompanyId, ServerName, DatabaseName, IsActive, CredentialKey
) VALUES
(1, 1, '(localdb)\MSSQLLocalDB', 'aquaShine_db', 1, 'TenantA'),
(2, 2, '(localdb)\MSSQLLocalDB', 'sparkleRide_db', 1, 'TenantA'),
(3, 3, '(localdb)\MSSQLLocalDB', 'cleanRide_db', 1, 'TenantA');

SET IDENTITY_INSERT CompanyDatabases OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $cdbSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "CompanyDatabases configured."

# 4. Seed TenantSubscriptionPlans
$planSql = @"
SET IDENTITY_INSERT TenantSubscriptionPlans ON;

INSERT INTO TenantSubscriptionPlans (
    PlanId, PlanName, Description, Price, BillingCycle,
    MaxUsers, MaxCustomers, IsActive, CreatedAt, IsArchived, MultiBranchEnabled
) VALUES
(1, 'Core Operations Plan', 'Main Transactions & Customer Data Collection package', 1499.00, 'Monthly', 10, 500, 1, '2026-01-01', 0, 0),
(2, 'Business Intelligence & Actions Suite', 'Business Intelligence Analytics, Reporting, and Customer Actions package', 2999.00, 'Monthly', 25, 2000, 1, '2026-01-01', 0, 0),
(3, 'Enterprise Multi-Branch Suite', 'Branching, Advanced Business Intelligence, and Customer Actions package', 4999.00, 'Monthly', 50, 5000, 1, '2026-01-01', 0, 1);

SET IDENTITY_INSERT TenantSubscriptionPlans OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $planSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "TenantSubscriptionPlans seeded."

# 5. Seed TenantSubscriptions
$subSql = @"
SET IDENTITY_INSERT TenantSubscriptions ON;

INSERT INTO TenantSubscriptions (
    TenantSubscriptionId, CompanyId, PlanId, Status, StartDate, RenewalDate, AutoRenew, CreatedAt
) VALUES
(1, 1, 1, 'Active', '2026-01-01', '2027-01-01', 1, '2026-01-01'),
(2, 2, 2, 'Active', '2026-01-05', '2027-01-05', 1, '2026-01-05'),
(3, 3, 3, 'Active', '2026-01-10', '2027-01-10', 1, '2026-01-10');

SET IDENTITY_INSERT TenantSubscriptions OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $subSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "TenantSubscriptions assigned."

# 6. Seed TenantBillingTransactions
$btSql = @"
SET IDENTITY_INSERT TenantBillingTransactions ON;

INSERT INTO TenantBillingTransactions (
    TransactionId, TenantSubscriptionId, CompanyId, Amount, PaymentMethod, PaymentStatus, TransactionDate, ReferenceNumber
) VALUES
(1, 1, 1, 1499.00, 'GCash', 'Completed', '2026-01-01', 'REF-AQUA-202601'),
(2, 2, 2, 2999.00, 'Credit Card', 'Completed', '2026-01-05', 'REF-SPARK-202601'),
(3, 3, 3, 4999.00, 'Bank Transfer', 'Completed', '2026-01-10', 'REF-CLEAN-202601');

SET IDENTITY_INSERT TenantBillingTransactions OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $btSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "TenantBillingTransactions seeded."

# 7. Seed Users: Exactly ONE Super Admin, plus Admin/Manager/Staff for the 3 companies
$usersSql = @"
SET IDENTITY_INSERT Users ON;

INSERT INTO Users (
    UserId, RoleId, IdentityUserId, Email, PasswordHash, Status, CreatedAt, CompanyId, FirstName, LastName
) VALUES
-- ONLY ONE SUPER ADMIN (Role 4):
(1, 4, 'superadmin-001', 'superadmin@carwashcrm.com', 'Password123!', 'Active', '2026-01-01', NULL, 'Chief', 'SuperAdmin'),

-- AquaShine Accounts (Company 1):
(2, 1, 'aqua-admin-001', 'admin@aquashine.com', 'Password123!', 'Active', '2026-01-01', 1, 'Eduardo', 'Santos'),
(3, 2, 'aqua-mgr-001', 'manager@aquashine.com', 'Password123!', 'Active', '2026-01-01', 1, 'Maria', 'Reyes'),
(4, 3, 'aqua-staff-001', 'staff@aquashine.com', 'Password123!', 'Active', '2026-01-01', 1, 'Juan', 'Dela Cruz'),

-- SparkleRide Accounts (Company 2):
(5, 1, 'spark-admin-001', 'admin@sparkleride.com', 'Password123!', 'Active', '2026-01-05', 2, 'Christine', 'Lim'),
(6, 2, 'spark-mgr-001', 'manager@sparkleride.com', 'Password123!', 'Active', '2026-01-05', 2, 'Paolo', 'Mendoza'),
(7, 3, 'spark-staff-001', 'staff@sparkleride.com', 'Password123!', 'Active', '2026-01-05', 2, 'Mark', 'Gonzales'),

-- CleanRide Accounts (Company 3):
(8, 1, 'clean-admin-001', 'admin@cleanride.com', 'Password123!', 'Active', '2026-01-10', 3, 'Antonio', 'Garcia'),
(9, 2, 'clean-mgr-001', 'manager@cleanride.com', 'Password123!', 'Active', '2026-01-10', 3, 'Rhea', 'Villanueva'),
(10, 3, 'clean-staff-001', 'staff@cleanride.com', 'Password123!', 'Active', '2026-01-10', 3, 'Carlo', 'Alvarez'),

-- Restored Old Admin Test Account (Role 1):
(11, 1, 'admin-001', 'admin@carwashcrm.com', 'Password123!', 'Active', '2026-01-01', NULL, 'System', 'Admin');

SET IDENTITY_INSERT Users OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $usersSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "Users seeded: 1 Super Admin + Company accounts."

# 8. Seed TermsConditions
$tcSql = @"
SET IDENTITY_INSERT TermsConditions ON;

INSERT INTO TermsConditions (
    TermsId, Version, Content, EffectiveDate, CreatedBy
) VALUES
(1, 'v1.0', 'Standard Software as a Service terms and conditions for Carwash CRM multi-tenant platform. All tenants agree to maintain accurate records, adhere to privacy standards, and ensure service transparency for registered customers.', '2026-01-01', 1);

SET IDENTITY_INSERT TermsConditions OFF;
"@
$cmd = $lConn.CreateCommand()
$cmd.CommandText = $tcSql
$cmd.ExecuteNonQuery() | Out-Null
Write-Host "TermsConditions seeded."

$lConn.Close()

Write-Host "`n=== 3. CREATING TABLES & SEEDING > 200 VALID RECORDS PER TENANT DATABASE ==="

$tenantSchemaSql = @"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantCustomers')
BEGIN
    CREATE TABLE [TenantCustomers] (
        [TenantCustomerId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerCode] NVARCHAR(50) NOT NULL,
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [CustomerName] AS (ltrim(rtrim(concat([FirstName],' ',[LastName])))),
        [ContactNumber] NVARCHAR(50) NULL,
        [EmailAddress] NVARCHAR(200) NULL,
        [Street] NVARCHAR(200) NULL,
        [City] NVARCHAR(100) NULL,
        [Province] NVARCHAR(100) NULL,
        [Address] AS (case when [Street] IS NOT NULL AND [City] IS NOT NULL then concat([Street],', ',[City],case when [Province] IS NOT NULL then concat(', ',[Province]) else '' end) when [Street] IS NOT NULL then [Street] when [City] IS NOT NULL then [City] end),
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [PlateNumber] NVARCHAR(50) NULL,
        [VehicleMake] NVARCHAR(100) NULL,
        [VehicleModel] NVARCHAR(100) NULL,
        [VehicleYear] INT NULL,
        [VehicleColor] NVARCHAR(50) NULL,
        [VehicleType] NVARCHAR(50) NULL,
        [Source] NVARCHAR(50) NULL,
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [ArchivedAt] DATETIME2 NULL,
        [ArchivedBy] NVARCHAR(200) NULL
    );
    CREATE UNIQUE INDEX [IX_TenantCustomers_CustomerCode] ON [TenantCustomers] ([CustomerCode]);
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Products')
BEGIN
    CREATE TABLE [Products] (
        [ProductId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductCode] NVARCHAR(50) NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [DurationMinutes] INT NOT NULL,
        [Category] NVARCHAR(50) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [ArchivedAt] DATETIME2 NULL,
        [ArchivedBy] NVARCHAR(200) NULL
    );
    CREATE UNIQUE INDEX [IX_Products_ProductCode] ON [Products] ([ProductCode]);
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Suppliers')
BEGIN
    CREATE TABLE [Suppliers] (
        [SupplierId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SupplierCode] NVARCHAR(50) NOT NULL,
        [SupplierName] NVARCHAR(200) NOT NULL,
        [ContactFirstName] NVARCHAR(100) NULL,
        [ContactLastName] NVARCHAR(100) NULL,
        [ContactPerson] AS (ltrim(rtrim(concat(isnull([ContactFirstName],''),' ',isnull([ContactLastName],''))))),
        [ContactNumber] NVARCHAR(50) NULL,
        [EmailAddress] NVARCHAR(200) NULL,
        [Address] NVARCHAR(500) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [ArchivedAt] DATETIME2 NULL,
        [ArchivedBy] NVARCHAR(200) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Inventories')
BEGIN
    CREATE TABLE [Inventories] (
        [InventoryId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [QuantityOnHand] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [ReorderLevel] DECIMAL(18,2) NOT NULL DEFAULT 10,
        [LastUpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerInteractions')
BEGIN
    CREATE TABLE [CustomerInteractions] (
        [InteractionId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerId] INT NOT NULL,
        [Kind] NVARCHAR(30) NOT NULL,
        [Severity] NVARCHAR(20) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Details] NVARCHAR(2000) NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [RecordedBy] NVARCHAR(200) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ServiceRequests')
BEGIN
    CREATE TABLE [ServiceRequests] (
        [RequestId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerId] INT NOT NULL,
        [ServiceId] INT NOT NULL,
        [AssignedStaffId] INT NULL,
        [CreatedBy] INT NOT NULL DEFAULT 1,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Pending',
        [Priority] NVARCHAR(20) NULL DEFAULT 'Normal',
        [Notes] NVARCHAR(1000) NULL,
        [RequestedDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ScheduledDate] DATETIME2 NULL,
        [CompletedDate] DATETIME2 NULL,
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [ArchivedAt] DATETIME2 NULL,
        [ArchivedBy] NVARCHAR(200) NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BillingTransactions')
BEGIN
    CREATE TABLE [BillingTransactions] (
        [TransactionId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerSubscriptionId] INT NULL,
        [RequestId] INT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [PaymentStatus] NVARCHAR(MAX) NOT NULL DEFAULT 'Completed',
        [TransactionDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ServiceStatusLogs')
BEGIN
    CREATE TABLE [ServiceStatusLogs] (
        [LogId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RequestId] INT NOT NULL,
        [Status] NVARCHAR(MAX) NOT NULL,
        [UpdatedBy] INT NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Notes] NVARCHAR(MAX) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FollowUps')
BEGIN
    CREATE TABLE [FollowUps] (
        [FollowUpId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CustomerId] INT NOT NULL,
        [Type] NVARCHAR(100) NOT NULL,
        [ContactMethod] NVARCHAR(50) NOT NULL DEFAULT 'Email',
        [Reason] NVARCHAR(200) NULL,
        [DiscountOffer] NVARCHAR(200) NULL,
        [Notes] NVARCHAR(1000) NULL,
        [Status] NVARCHAR(30) NOT NULL DEFAULT 'Pending',
        [ScheduledDate] DATETIME2 NOT NULL,
        [ValidUntil] DATETIME2 NULL,
        [SentAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] INT NULL,
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [ArchivedAt] DATETIME2 NULL,
        [ArchivedBy] NVARCHAR(200) NULL,
        [ApprovalStatus] NVARCHAR(MAX) NOT NULL DEFAULT 'Pending',
        [ApprovedBy] INT NULL,
        [ApprovedAt] DATETIME2 NULL,
        [RejectionReason] NVARCHAR(MAX) NULL,
        [RejectedBy] INT NULL,
        [RejectedAt] DATETIME2 NULL,
        [ServiceRequestRequestId] INT NULL
    );
END;
"@

# Helper data arrays for realistic dummy generation
$firstNames = @('Alexander','Beatriz','Carlo','Danilo','Elena','Fernando','Grace','Hannah','Ian','Jasmine','Kenneth','Leah','Marco','Nathan','Olivia','Patricia','Quentin','Rafael','Stephanie','Theresa','Ulysses','Victor','Wendy','Xavier','Yvette','Zachary','Gabriel','Angelica','Ramon','Bernadette','Cesar','Doreen','Emanuel','Francine','Gilbert','Hazel','Isagani','Joanna','Kiko','Lorena','Manuel','Nadine','Oscar','Paulina','Ronaldo','Sheila','Tristan','Valerie','Wilfredo','Zoe')
$lastNames = @('Santos','Reyes','Cruz','Bautista','Ocampo','Garcia','Mendoza','Torres','Tomas','Andrada','Castillo','Flores','Villanueva','Ramos','Castro','Rivera','Aquino','Navarro','Salazar','Mercado','Gomez','Perez','Soriano','Pineda','Morales','Pascual','Del Rosario','Estrada','David','Valdez','Sarmiento','San Jose','Legaspi','Padilla','Domingo','Alcantara','Velasco','Miranda','Aguilar','Bermudez','Chavez','Espiritu','Fabian','Guerrero','Hernandez','Ilagan','Javier','Katigbak','Luna','Macaraeg')
$streets = @('88 Shaw Blvd','Km 14 West Service Rd','245 Taft Avenue','12 Ayala Avenue','77 BGC High Street','15 Kalayaan Avenue','45 Ortigas Avenue','90 Katipunan Ave','102 Timog Avenue','56 Tomas Morato','33 Buendia Ext','19 McKinley Road','68 C5 Road','120 E. Rodriguez Sr. Ave','84 Quirino Highway','29 Macapagal Blvd','51 Alabang-Zapote Road','14 Pioneer Street','95 Bonifacio High Street','38 Chino Roces Ave')
$cities = @('Makati','Taguig','Quezon City','Pasig','Mandaluyong','Paranaque','Muntinlupa','San Juan','Las Pinas','Marikina')
$carMakes = @('Toyota','Honda','Mitsubishi','Ford','Nissan','Hyundai','Mazda','Kia','Suzuki','Subaru')
$carModels = @{
    'Toyota' = @('Vios','Fortuner','Innova','Corolla Cross','Hilux');
    'Honda' = @('Civic','City','CR-V','HR-V','BR-V');
    'Mitsubishi' = @('Montero Sport','Xpander','Mirage G4','Strada','Triton');
    'Ford' = @('Ranger','Everest','Territory','Explorer');
    'Nissan' = @('Navara','Terra','Almera','Kicks e-Power');
    'Hyundai' = @('Tucson','Creta','Stargazer','Santa Fe');
    'Mazda' = @('Mazda 3','CX-5','CX-30','Mazda 2');
    'Kia' = @('Seltos','Sonet','Carnival','Sportage');
    'Suzuki' = @('Jimny','Ertiga','Swift','Dzire');
    'Subaru' = @('Forester','XV','Crosstrek','Outback')
}
$colors = @('Pearl White','Metallic Gray','Jet Black','Deep Crystal Blue','Crimson Red','Silver Metallic','Platinum Bronze','Midnight Gray')
$bodyTypes = @('Sedan','SUV','Crossover','Pick-up','Hatchback','MPV')

$servicesList = @(
    @{ Code='SRV-001'; Name='Express Foam Exterior Wash'; Price=250.00; Dur=20; Cat='Exterior'; Desc='High-pressure rinse, snow foam hand shampoo, wheel cleaning, and microfiber dry.' },
    @{ Code='SRV-002'; Name='Deluxe Wash & Hand Spray Wax'; Price=550.00; Dur=40; Cat='Exterior'; Desc='Full exterior foam wash plus hand application of hydrophobic Carnauba spray wax.' },
    @{ Code='SRV-003'; Name='Executive Interior Detailing & Steam Sanitation'; Price=1800.00; Dur=90; Cat='Interior'; Desc='Deep steam extraction of seats, carpet shampoo, leather treatment, and dashboard UV coating.' },
    @{ Code='SRV-004'; Name='Full Body Ceramic Coating 9H (3-Year)'; Price=8500.00; Dur=240; Cat='Detailing'; Desc='Multi-stage paint decontamination, high-gloss polish, and 9H dual-layer ceramic quartz shield.' },
    @{ Code='SRV-005'; Name='Engine Bay Degreasing & Protective Dressing'; Price=850.00; Dur=45; Cat='Engine'; Desc='Safe water-based degreaser, grime agitation, steam rinse, and silicone-free matte dressing.' },
    @{ Code='SRV-006'; Name='Undercarriage Pressure Wash & Anti-Rust Coating'; Price=1200.00; Dur=60; Cat='Underchassis'; Desc='High-pressure underchassis mud blast, chassis salt removal, and protective asphalt spray.' },
    @{ Code='SRV-007'; Name='Two-Stage Precision Paint Correction'; Price=4500.00; Dur=180; Cat='Detailing'; Desc='Compound swirl mark elimination and fine finishing polish to restore mirror showroom gloss.' },
    @{ Code='SRV-008'; Name='Headlight Lens Restoration & UV Sealant'; Price=750.00; Dur=30; Cat='Detailing'; Desc='Wet-sand oxidized yellowing, machine buff clarity, and apply long-lasting UV clear coat.' },
    @{ Code='SRV-009'; Name='Aircon Evaporator & Cabin Ozone Deodorizer'; Price=900.00; Dur=30; Cat='Interior'; Desc='Hospital-grade ozone air purification eliminating stubborn odors, smoke, and air bacteria.' },
    @{ Code='SRV-010'; Name='Premium Leather Conditioning & Hydrophobic Shield'; Price=1100.00; Dur=50; Cat='Interior'; Desc='pH-neutral leather cleaner followed by deep conditioning balm to prevent cracking.' }
)

$suppliersList = @(
    @{ Code='SUP-001'; Name='Sonax Philippines Distributing Co.'; First='Eduardo'; Last='Villar'; Phone='+63 917 882 1010'; Email='sales@sonax.ph'; Addr='Ortigas Industrial Park, Pasig City' },
    @{ Code='SUP-002'; Name='Chemical Guys PH Car Care Supply'; First='Marissa'; Last='Chua'; Phone='+63 918 334 9900'; Email='orders@chemicalguys.ph'; Addr='BGC Commerce Hub, Taguig City' },
    @{ Code='SUP-003'; Name='3M Automotive Solutions Manila'; First='Gerardo'; Last='Santos'; Phone='+63 920 445 7711'; Email='auto@3m.com.ph'; Addr='Chino Roces Ave Ext, Makati City' },
    @{ Code='SUP-004'; Name='Meguiar''s Professional Detailing'; First='Patricia'; Last='Ong'; Phone='+63 917 556 8822'; Email='info@meguiars.ph'; Addr='Timog Commercial Center, Quezon City' },
    @{ Code='SUP-005'; Name='Karcher Cleaning Systems Philippines'; First='Ramon'; Last='Tan'; Phone='+63 919 667 3344'; Email='commercial@karcher.ph'; Addr='Alabang Business Center, Muntinlupa' }
)

$followUpTemplates = @(
    @{ Type='Routine Wash Reminder'; Reason='30 days since last Deluxe Wash'; Offer='10% Off Next Express Wash'; Notes='Customer vehicle is due for regular monthly maintenance.' },
    @{ Type='Ceramic Coating Inspection'; Reason='Annual ceramic coating hydrophobic water-beading check'; Offer='Free Top-Coat Silica Boost Spray'; Notes='Ensure ceramic warranty remains active and inspection passed.' },
    @{ Type='Interior Detailing Follow-Up'; Reason='Quarterly interior steam sanitation reminder'; Offer='Free Aircon Ozone Treatment with Detailing'; Notes='Recommended after heavy rainy season usage.' },
    @{ Type='VIP Loyalty Incentive'; Reason='Valued regular customer appreciation'; Offer='15% Service Discount Voucher'; Notes='Exclusive VIP customer promo valid for 30 days.' },
    @{ Type='Engine Bay Care Checkup'; Reason='6-month periodic engine bay degrease checkup'; Offer='Free Headlight Lens Wipe Down'; Notes='Ensure engine compartment remains free of grease and grime buildup.' }
)

# Seed each of the 3 tenant databases
$tenantDbs = @('aquaShine_db', 'sparkleRide_db', 'cleanRide_db')

foreach ($tdb in $tenantDbs) {
    Write-Host "`n--- Seeding $tdb ---"
    $tConnStr = "Server=(localdb)\MSSQLLocalDB;Database=$tdb;Trusted_Connection=True;TrustServerCertificate=True;"
    $tConn = New-Object System.Data.SqlClient.SqlConnection($tConnStr)
    $tConn.Open()

    # 1. Create tables if not present
    $cmd = $tConn.CreateCommand()
    $cmd.CommandText = $tenantSchemaSql
    $cmd.ExecuteNonQuery() | Out-Null

    # 2. Clear existing records in tenant DB
    $clearTenantSql = @"
DELETE FROM FollowUps;
DELETE FROM ServiceStatusLogs;
DELETE FROM BillingTransactions;
DELETE FROM ServiceRequests;
DELETE FROM CustomerInteractions;
DELETE FROM Inventories;
DELETE FROM Suppliers;
DELETE FROM Products;
DELETE FROM TenantCustomers;
"@
    $cmd.CommandText = $clearTenantSql
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "Cleared previous tables in $tdb."

    # 3. Seed 50 Customers
    Write-Host "Seeding 50 valid customers in $tdb..."
    for ($i = 0; $i -lt 50; $i++) {
        $fn = $firstNames[$i % $firstNames.Length]
        $ln = $lastNames[($i * 3 + 1) % $lastNames.Length]
        $code = "CUST-" + ($i + 1).ToString("000")
        $phone = "+63 917 " + (100 + $i * 17).ToString() + " " + (1000 + $i * 53).ToString()
        $email = $fn.ToLower() + "." + $ln.ToLower().Replace(" ", "") + "@gmail.com"
        $st = $streets[$i % $streets.Length]
        $city = $cities[$i % $cities.Length]
        $prov = "Metro Manila"
        $make = $carMakes[$i % $carMakes.Length]
        $modelsForMake = $carModels[$make]
        $model = $modelsForMake[$i % $modelsForMake.Length]
        $year = 2018 + ($i % 7)
        $color = $colors[$i % $colors.Length]
        $vtype = $bodyTypes[$i % $bodyTypes.Length]
        $plate = [char](65 + ($i % 26)) + [char](65 + (($i * 2) % 26)) + [char](65 + (($i * 3) % 26)) + "-" + (1000 + $i * 89).ToString().Substring(0,4)
        $src = if ($i % 3 -eq 0) { 'Walk-in' } elseif ($i % 3 -eq 1) { 'Social Media' } else { 'Referral' }
        $created = (Get-Date "2026-01-01").AddDays($i).ToString("yyyy-MM-dd HH:mm:ss")

        $insCmd = $tConn.CreateCommand()
        $insCmd.CommandText = @"
INSERT INTO TenantCustomers (
    CustomerCode, FirstName, LastName, ContactNumber, EmailAddress,
    Street, City, Province, IsActive, CreatedAt,
    PlateNumber, VehicleMake, VehicleModel, VehicleYear, VehicleColor,
    VehicleType, Source, IsArchived
) VALUES (
    @Code, @Fn, @Ln, @Phone, @Email,
    @Street, @City, @Prov, 1, @Created,
    @Plate, @Make, @Model, @Year, @Color,
    @VType, @Src, 0
);
"@
        $insCmd.Parameters.AddWithValue("@Code", $code) | Out-Null
        $insCmd.Parameters.AddWithValue("@Fn", $fn) | Out-Null
        $insCmd.Parameters.AddWithValue("@Ln", $ln) | Out-Null
        $insCmd.Parameters.AddWithValue("@Phone", $phone) | Out-Null
        $insCmd.Parameters.AddWithValue("@Email", $email) | Out-Null
        $insCmd.Parameters.AddWithValue("@Street", $st) | Out-Null
        $insCmd.Parameters.AddWithValue("@City", $city) | Out-Null
        $insCmd.Parameters.AddWithValue("@Prov", $prov) | Out-Null
        $insCmd.Parameters.AddWithValue("@Created", $created) | Out-Null
        $insCmd.Parameters.AddWithValue("@Plate", $plate) | Out-Null
        $insCmd.Parameters.AddWithValue("@Make", $make) | Out-Null
        $insCmd.Parameters.AddWithValue("@Model", $model) | Out-Null
        $insCmd.Parameters.AddWithValue("@Year", $year) | Out-Null
        $insCmd.Parameters.AddWithValue("@Color", $color) | Out-Null
        $insCmd.Parameters.AddWithValue("@VType", $vtype) | Out-Null
        $insCmd.Parameters.AddWithValue("@Src", $src) | Out-Null
        $insCmd.ExecuteNonQuery() | Out-Null
    }

    # 4. Seed 10 Products / Services
    Write-Host "Seeding 10 services in $tdb..."
    foreach ($p in $servicesList) {
        $pCmd = $tConn.CreateCommand()
        $pCmd.CommandText = @"
INSERT INTO Products (ProductCode, ProductName, UnitPrice, DurationMinutes, Category, Description, IsActive, CreatedAt, IsArchived)
VALUES (@Code, @Name, @Price, @Dur, @Cat, @Desc, 1, '2026-01-01', 0);
"@
        $pCmd.Parameters.AddWithValue("@Code", $p.Code) | Out-Null
        $pCmd.Parameters.AddWithValue("@Name", $p.Name) | Out-Null
        $pCmd.Parameters.AddWithValue("@Price", $p.Price) | Out-Null
        $pCmd.Parameters.AddWithValue("@Dur", $p.Dur) | Out-Null
        $pCmd.Parameters.AddWithValue("@Cat", $p.Cat) | Out-Null
        $pCmd.Parameters.AddWithValue("@Desc", $p.Desc) | Out-Null
        $pCmd.ExecuteNonQuery() | Out-Null
    }

    # 5. Seed 5 Suppliers
    Write-Host "Seeding 5 suppliers in $tdb..."
    foreach ($s in $suppliersList) {
        $sCmd = $tConn.CreateCommand()
        $sCmd.CommandText = @"
INSERT INTO Suppliers (SupplierCode, SupplierName, ContactFirstName, ContactLastName, ContactNumber, EmailAddress, Address, IsActive, CreatedAt, IsArchived)
VALUES (@Code, @Name, @First, @Last, @Phone, @Email, @Addr, 1, '2026-01-01', 0);
"@
        $sCmd.Parameters.AddWithValue("@Code", $s.Code) | Out-Null
        $sCmd.Parameters.AddWithValue("@Name", $s.Name) | Out-Null
        $sCmd.Parameters.AddWithValue("@First", $s.First) | Out-Null
        $sCmd.Parameters.AddWithValue("@Last", $s.Last) | Out-Null
        $sCmd.Parameters.AddWithValue("@Phone", $s.Phone) | Out-Null
        $sCmd.Parameters.AddWithValue("@Email", $s.Email) | Out-Null
        $sCmd.Parameters.AddWithValue("@Addr", $s.Addr) | Out-Null
        $sCmd.ExecuteNonQuery() | Out-Null
    }

    # 6. Seed 10 Inventories
    Write-Host "Seeding 10 inventory items in $tdb..."
    for ($i = 1; $i -le 10; $i++) {
        $qty = 20.0 + ($i * 5.0)
        $reorder = 10.0
        $iCmd = $tConn.CreateCommand()
        $iCmd.CommandText = @"
INSERT INTO Inventories (ProductId, QuantityOnHand, ReorderLevel, LastUpdatedAt)
VALUES ($i, $qty, $reorder, '2026-01-01');
"@
        $iCmd.ExecuteNonQuery() | Out-Null
    }

    # 7. Seed 30 Customer Interactions
    Write-Host "Seeding 30 customer interactions in $tdb..."
    $kinds = @('Inquiry', 'Feedback', 'Compliment', 'Special Request', 'FollowUp Note')
    for ($i = 1; $i -le 30; $i++) {
        $cId = ($i % 50) + 1
        $kind = $kinds[$i % $kinds.Length]
        $title = "$kind regarding service visit #$i"
        $details = "Customer verified vehicle condition and requested extra care for windshield and alloy wheels."
        $ciCmd = $tConn.CreateCommand()
        $ciCmd.CommandText = @"
INSERT INTO CustomerInteractions (CustomerId, Kind, Severity, Title, Details, Status, CreatedAt, RecordedBy)
VALUES ($cId, '$kind', 'Normal', @Title, @Details, 'Closed', DATEADD(day, $i, '2026-01-05'), 'FrontDesk');
"@
        $ciCmd.Parameters.AddWithValue("@Title", $title) | Out-Null
        $ciCmd.Parameters.AddWithValue("@Details", $details) | Out-Null
        $ciCmd.ExecuteNonQuery() | Out-Null
    }

    # 8. Seed 80 Service Requests + 80 Billing Transactions + 120 Status Logs
    Write-Host "Seeding 80 service requests, 80 billing transactions, and 120 status logs in $tdb..."
    $payMethods = @('Cash', 'GCash', 'Maya', 'Credit Card', 'Debit Card')
    for ($i = 1; $i -le 80; $i++) {
        $custId = ($i % 50) + 1
        $svcId = ($i % 10) + 1
        $staffId = 4
        $status = if ($i -ge 75) { 'InProgress' } elseif ($i -ge 70) { 'Pending' } else { 'Completed' }
        $reqDate = (Get-Date "2026-01-05").AddHours($i * 18).ToString("yyyy-MM-dd HH:mm:ss")
        $compDate = if ($status -eq 'Completed') { (Get-Date $reqDate).AddMinutes(45).ToString("yyyy-MM-dd HH:mm:ss") } else { [DBNull]::Value }
        $price = $servicesList[$svcId - 1].Price

        $srCmd = $tConn.CreateCommand()
        $srCmd.CommandText = @"
INSERT INTO ServiceRequests (CustomerId, ServiceId, AssignedStaffId, CreatedBy, Status, Priority, Notes, RequestedDate, ScheduledDate, CompletedDate, IsArchived)
OUTPUT INSERTED.RequestId
VALUES ($custId, $svcId, $staffId, 2, '$status', 'Normal', 'Client arrived on schedule. Complete walkaround performed.', '$reqDate', '$reqDate', @CompDate, 0);
"@
        $srCmd.Parameters.AddWithValue("@CompDate", $compDate) | Out-Null
        $reqId = [Convert]::ToInt32($srCmd.ExecuteScalar())

        # Billing Transaction for Completed requests
        if ($status -eq 'Completed') {
            $payMethod = $payMethods[$i % $payMethods.Length]
            $bCmd = $tConn.CreateCommand()
            $bCmd.CommandText = @"
INSERT INTO BillingTransactions (RequestId, Amount, PaymentStatus, TransactionDate)
VALUES ($reqId, $price, 'Completed', @CompDate);
"@
            $bCmd.Parameters.AddWithValue("@CompDate", $compDate) | Out-Null
            $bCmd.ExecuteNonQuery() | Out-Null
        }

        # ServiceStatusLogs
        $logCmd = $tConn.CreateCommand()
        $logCmd.CommandText = @"
INSERT INTO ServiceStatusLogs (RequestId, Status, UpdatedBy, UpdatedAt, Notes)
VALUES ($reqId, 'Pending', 2, '$reqDate', 'Service request initiated and queued.');
"@
        $logCmd.ExecuteNonQuery() | Out-Null

        if ($status -eq 'Completed' -or $status -eq 'InProgress') {
            $inProgDate = (Get-Date $reqDate).AddMinutes(10).ToString("yyyy-MM-dd HH:mm:ss")
            $logCmd.CommandText = @"
INSERT INTO ServiceStatusLogs (RequestId, Status, UpdatedBy, UpdatedAt, Notes)
VALUES ($reqId, 'InProgress', 4, '$inProgDate', 'Vehicle pulled into wash bay. Pre-wash rinse underway.');
"@
            $logCmd.ExecuteNonQuery() | Out-Null
        }

        if ($status -eq 'Completed') {
            $logCmd.CommandText = @"
INSERT INTO ServiceStatusLogs (RequestId, Status, UpdatedBy, UpdatedAt, Notes)
VALUES ($reqId, 'Completed', 4, @CompDate, 'Quality inspection passed. Vehicle ready for pickup.');
"@
            $logCmd.Parameters.AddWithValue("@CompDate", $compDate) | Out-Null
            $logCmd.ExecuteNonQuery() | Out-Null
        }
    }

    # 9. Seed 30 Follow-Ups
    Write-Host "Seeding 30 valid follow-ups in $tdb..."
    for ($i = 1; $i -le 30; $i++) {
        $custId = ($i % 50) + 1
        $tpl = $followUpTemplates[$i % $followUpTemplates.Length]
        $sched = (Get-Date "2026-02-01").AddDays($i * 2).ToString("yyyy-MM-dd HH:mm:ss")
        $validUntil = (Get-Date $sched).AddDays(30).ToString("yyyy-MM-dd HH:mm:ss")
        $appStatus = if ($i % 3 -eq 0) { 'Pending' } else { 'Approved' }
        $status = if ($appStatus -eq 'Approved') { 'Approved' } else { 'Pending' }

        $fuCmd = $tConn.CreateCommand()
        $fuCmd.CommandText = @"
INSERT INTO FollowUps (
    CustomerId, Type, ContactMethod, Reason, DiscountOffer,
    Notes, Status, ScheduledDate, ValidUntil, CreatedAt,
    CreatedBy, IsArchived, ApprovalStatus
) VALUES (
    $custId, @Type, 'Email', @Reason, @Offer,
    @Notes, '$status', '$sched', '$validUntil', '2026-01-20',
    2, 0, '$appStatus'
);
"@
        $fuCmd.Parameters.AddWithValue("@Type", $tpl.Type) | Out-Null
        $fuCmd.Parameters.AddWithValue("@Reason", $tpl.Reason) | Out-Null
        $fuCmd.Parameters.AddWithValue("@Offer", $tpl.Offer) | Out-Null
        $fuCmd.Parameters.AddWithValue("@Notes", $tpl.Notes) | Out-Null
        $fuCmd.ExecuteNonQuery() | Out-Null
    }

    # Count total records in tenant DB
    $totalCmd = $tConn.CreateCommand()
    $totalCmd.CommandText = @"
SELECT 
    (SELECT COUNT(1) FROM TenantCustomers) +
    (SELECT COUNT(1) FROM Products) +
    (SELECT COUNT(1) FROM Suppliers) +
    (SELECT COUNT(1) FROM Inventories) +
    (SELECT COUNT(1) FROM CustomerInteractions) +
    (SELECT COUNT(1) FROM ServiceRequests) +
    (SELECT COUNT(1) FROM BillingTransactions) +
    (SELECT COUNT(1) FROM ServiceStatusLogs) +
    (SELECT COUNT(1) FROM FollowUps);
"@
    $totalCount = $totalCmd.ExecuteScalar()
    Write-Host "Total valid records seeded in ${tdb}: $totalCount (Exceeds 200 minimum requirement!)"

    $tConn.Close()
}

Write-Host "`n=== 4. SYNCING CLEAN STATE TO MONSTERASP CLOUD (db67193) ==="
try {
    $cConn = New-Object System.Data.SqlClient.SqlConnection($cloudConnStr)
    $cConn.Open()

    # Clear cloud tables
    $clearCloudSql = @"
DELETE FROM CloudSyncLog;
DELETE FROM FollowUps;
DELETE FROM ServiceStatusLogs;
DELETE FROM BillingTransactions;
DELETE FROM ServiceRequests;
DELETE FROM CustomerInteractions;
DELETE FROM Inventories;
DELETE FROM Suppliers;
DELETE FROM Products;
DELETE FROM TenantCustomers;
DELETE FROM BackupLogs;
DELETE FROM TenantBillingTransactions;
DELETE FROM TenantSubscriptions;
DELETE FROM TenantSubscriptionPlans;
DELETE FROM TermsConditions;
DELETE FROM Users;
DELETE FROM CompanyDatabases;
DELETE FROM Companies;
DELETE FROM Roles;
"@
    $cCmd = $cConn.CreateCommand()
    $cCmd.CommandText = $clearCloudSql
    $cCmd.CommandTimeout = 120
    $cCmd.ExecuteNonQuery() | Out-Null
    Write-Host "MonsterASP cloud database db67193 cleared for clean mirror."
    $cConn.Close()
} catch {
    Write-Host "Warning during cloud clean: $($_.Exception.Message)"
}

Write-Host "`nDatabase reset & multi-tenant seed completed successfully!"
