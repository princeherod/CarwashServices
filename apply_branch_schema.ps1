$dbs = @('cleanRide_db', 'aquaShine_db', 'sparkleRide_db')
foreach ($db in $dbs) {
    $c = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=$db;Trusted_Connection=True;")
    $c.Open()
    $cmd = $c.CreateCommand()
    $cmd.CommandText = @"
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
BEGIN
    ALTER TABLE ServiceRequests ADD BranchId INT NULL;
    CREATE INDEX IX_ServiceRequests_BranchId ON ServiceRequests(BranchId);
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TenantCustomers' AND COLUMN_NAME = 'BranchId')
BEGIN
    ALTER TABLE TenantCustomers ADD BranchId INT NULL;
    CREATE INDEX IX_TenantCustomers_BranchId ON TenantCustomers(BranchId);
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'FollowUps' AND COLUMN_NAME = 'BranchId')
BEGIN
    ALTER TABLE FollowUps ADD BranchId INT NULL;
    CREATE INDEX IX_FollowUps_BranchId ON FollowUps(BranchId);
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BillingTransactions' AND COLUMN_NAME = 'BranchId')
BEGIN
    ALTER TABLE BillingTransactions ADD BranchId INT NULL;
    CREATE INDEX IX_BillingTransactions_BranchId ON BillingTransactions(BranchId);
END
"@
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Output "Applied schema to $db"
    $c.Close()
}

# Update Users table in MSME_MasterCrm
$mc = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=MSME_MasterCrm;Trusted_Connection=True;")
$mc.Open()
$mcmd = $mc.CreateCommand()
$mcmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'BranchId')
BEGIN
    ALTER TABLE Users ADD BranchId INT NULL;
END
"@
$mcmd.ExecuteNonQuery() | Out-Null
Write-Output "Applied schema to MSME_MasterCrm"
$mc.Close()

# Seed CleanRide branches if not seeded
$cr = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=cleanRide_db;Trusted_Connection=True;")
$cr.Open()
$crcmd = $cr.CreateCommand()
$crcmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM Branches)
BEGIN
    SET IDENTITY_INSERT Branches ON;
    INSERT INTO Branches (BranchId, BranchCode, BranchName, Address, City, Province, ContactNumber, Email, IsMainBranch, IsActive, IsArchived, CreatedAt)
    VALUES 
    (1, 'BR-MAIN', 'Main Branch', 'J.P. Laurel Ave, Bajada', 'Davao City', 'Davao del Sur', '+63 82 221 4500', 'main@cleanride.com', 1, 1, 0, GETUTCDATE()),
    (2, 'BR-CAL', 'CleanRide Calinan', 'Davao-Bukidnon Highway, Calinan', 'Calinan, Davao City', 'Davao del Sur', '+63 82 295 1234', 'calinan@cleanride.com', 0, 1, 0, GETUTCDATE()),
    (3, 'BR-MAT', 'CleanRide Matina', 'MacArthur Highway, Matina', 'Matina, Davao City', 'Davao del Sur', '+63 82 297 8899', 'matina@cleanride.com', 0, 1, 0, GETUTCDATE());
    SET IDENTITY_INSERT Branches OFF;

    -- Distribute existing records across the 3 branches
    UPDATE ServiceRequests SET BranchId = (RequestId % 3) + 1 WHERE BranchId IS NULL;
    UPDATE TenantCustomers SET BranchId = (TenantCustomerId % 3) + 1 WHERE BranchId IS NULL;
    UPDATE FollowUps SET BranchId = (FollowUpId % 3) + 1 WHERE BranchId IS NULL;
    UPDATE BillingTransactions SET BranchId = (TransactionId % 3) + 1 WHERE BranchId IS NULL;
END
ELSE
BEGIN
    -- Ensure existing records have BranchId
    UPDATE ServiceRequests SET BranchId = 1 WHERE BranchId IS NULL;
    UPDATE TenantCustomers SET BranchId = 1 WHERE BranchId IS NULL;
    UPDATE FollowUps SET BranchId = 1 WHERE BranchId IS NULL;
    UPDATE BillingTransactions SET BranchId = 1 WHERE BranchId IS NULL;
END
"@
$crcmd.ExecuteNonQuery() | Out-Null
Write-Output "CleanRide database check complete"
$cr.Close()
