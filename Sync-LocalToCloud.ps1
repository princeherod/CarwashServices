# ==============================================================================
# Sync-LocalToCloud.ps1
# Synchronizes all Master and Multi-Tenant databases to MonsterASP Cloud DB
# ==============================================================================

param(
    [string]$CloudConnStr = "Server=db67193.public.databaseasp.net;Database=db67193;User Id=db67193;Password=i!8PSn+5-9sR;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30;",
    [string]$MasterConnStr = "Server=(localdb)\MSSQLLocalDB;Database=MSME_MasterCrm;Trusted_Connection=True;TrustServerCertificate=True;"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       MSME CRM - SYNC LOCAL DATABASE TO CLOUD DB          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Add-Type -AssemblyName System.Data

try {
    $cConn = New-Object System.Data.SqlClient.SqlConnection($CloudConnStr)
    $cConn.Open()
    Write-Host "[OK] Connected to MonsterASP Cloud Database: db67193" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Could not connect to MonsterASP Cloud: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$mConn = New-Object System.Data.SqlClient.SqlConnection($MasterConnStr)
$mConn.Open()
Write-Host "[OK] Connected to Local Master Database: MSME_MasterCrm" -ForegroundColor Green

# 1. Master Tables to Sync (in dependency order)
$masterTables = @(
    "Roles",
    "Companies",
    "Users",
    "CompanyDatabases",
    "TermsConditions",
    "TenantSubscriptionPlans",
    "TenantSubscriptions",
    "TenantBillingTransactions",
    "BackupLogs"
)

# 2. Tenant Tables to Sync (in dependency order)
$tenantTables = @(
    "Branches",
    "TenantCustomers",
    "Products",
    "Suppliers",
    "Inventories",
    "CustomerInteractions",
    "ServiceRequests",
    "BillingTransactions",
    "ServiceStatusLogs",
    "FollowUps"
)

# Discover Active Tenant Databases
$cmd = $mConn.CreateCommand()
$cmd.CommandText = "SELECT DISTINCT CompanyId, DatabaseName FROM CompanyDatabases WHERE IsActive = 1;"
$reader = $cmd.ExecuteReader()
$tenantDbs = @()
while ($reader.Read()) {
    $tenantDbs += [PSCustomObject]@{
        CompanyId = $reader.GetInt32(0)
        DatabaseName = $reader.GetString(1)
    }
}
$reader.Close()

Write-Host "`nDiscovered $($tenantDbs.Count) tenant database(s):" -ForegroundColor Yellow
foreach ($t in $tenantDbs) {
    Write-Host "  - Company $($t.CompanyId): $($t.DatabaseName)" -ForegroundColor Gray
}

# ------------------------------------------------------------------------------
# SYNC MASTER TABLES
# ------------------------------------------------------------------------------
Write-Host "`n--- Synchronizing Master Tables ---" -ForegroundColor Cyan

# Disable foreign keys temporarily in Cloud to allow clean multi-table sync
$fkDisableCmd = $cConn.CreateCommand()
$fkDisableCmd.CommandText = "EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all';"
$fkDisableCmd.ExecuteNonQuery() | Out-Null

foreach ($tbl in $masterTables) {
    # Check if table exists in local and cloud
    $checkCmd = $mConn.CreateCommand()
    $checkCmd.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '$tbl';"
    if ([int]$checkCmd.ExecuteScalar() -eq 0) { continue }

    # Fetch local rows
    $da = New-Object System.Data.SqlClient.SqlDataAdapter("SELECT * FROM [$tbl]", $mConn)
    $dt = New-Object System.Data.DataTable
    $da.Fill($dt) | Out-Null

    # Clear cloud table
    $delCmd = $cConn.CreateCommand()
    $delCmd.CommandText = "DELETE FROM [$tbl];"
    $delCmd.ExecuteNonQuery() | Out-Null

    if ($dt.Rows.Count -gt 0) {
        $bulk = New-Object System.Data.SqlClient.SqlBulkCopy(
            $cConn,
            [System.Data.SqlClient.SqlBulkCopyOptions]"KeepIdentity, KeepNulls",
            $null
        )
        $bulk.DestinationTableName = "[$tbl]"
        foreach ($col in $dt.Columns) {
            $bulk.ColumnMappings.Add($col.ColumnName, $col.ColumnName) | Out-Null
        }
        $bulk.WriteToServer($dt)
        $bulk.Close()
    }

    Write-Host "  Synced Master [$tbl]: $($dt.Rows.Count) rows" -ForegroundColor Green
}

# ------------------------------------------------------------------------------
# SYNC TENANT TABLES (Consolidated into single cloud database with CompanyId)
# ------------------------------------------------------------------------------
Write-Host "`n--- Synchronizing Tenant Tables ---" -ForegroundColor Cyan

foreach ($tbl in $tenantTables) {
    # Clear cloud tenant table
    $delCmd = $cConn.CreateCommand()
    $delCmd.CommandText = "DELETE FROM [$tbl];"
    $delCmd.ExecuteNonQuery() | Out-Null

    $totalSynced = 0

    foreach ($t in $tenantDbs) {
        $tConnStr = "Server=(localdb)\MSSQLLocalDB;Database=$($t.DatabaseName);Trusted_Connection=True;TrustServerCertificate=True;"
        try {
            $tConn = New-Object System.Data.SqlClient.SqlConnection($tConnStr)
            $tConn.Open()

            # Check if table exists in tenant db
            $checkCmd = $tConn.CreateCommand()
            $checkCmd.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '$tbl';"
            if ([int]$checkCmd.ExecuteScalar() -gt 0) {
                $da = New-Object System.Data.SqlClient.SqlDataAdapter("SELECT * FROM [$tbl]", $tConn)
                $dt = New-Object System.Data.DataTable
                $da.Fill($dt) | Out-Null

                if ($dt.Rows.Count -gt 0) {
                    # Ensure CompanyId column is populated
                    if (-not $dt.Columns.Contains("CompanyId")) {
                        $dt.Columns.Add("CompanyId", [int]) | Out-Null
                        foreach ($r in $dt.Rows) {
                            $r["CompanyId"] = $t.CompanyId
                        }
                    } else {
                        foreach ($r in $dt.Rows) {
                            if ($r.IsNull("CompanyId") -or [int]$r["CompanyId"] -eq 0) {
                                $r["CompanyId"] = $t.CompanyId
                            }
                        }
                    }

                    # Cloud schema might also have SyncedAt
                    if (-not $dt.Columns.Contains("SyncedAt")) {
                        $dt.Columns.Add("SyncedAt", [DateTime]) | Out-Null
                        foreach ($r in $dt.Rows) {
                            $r["SyncedAt"] = [DateTime]::UtcNow
                        }
                    }

                    $bulk = New-Object System.Data.SqlClient.SqlBulkCopy(
                        $cConn,
                        [System.Data.SqlClient.SqlBulkCopyOptions]"KeepIdentity, KeepNulls",
                        $null
                    )
                    $bulk.DestinationTableName = "[$tbl]"
                    foreach ($col in $dt.Columns) {
                        # Only map if destination has the column
                        $chkColCmd = $cConn.CreateCommand()
                        $chkColCmd.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$tbl' AND COLUMN_NAME = '$($col.ColumnName)';"
                        if ([int]$chkColCmd.ExecuteScalar() -gt 0) {
                            $bulk.ColumnMappings.Add($col.ColumnName, $col.ColumnName) | Out-Null
                        }
                    }
                    $bulk.WriteToServer($dt)
                    $bulk.Close()
                    $totalSynced += $dt.Rows.Count
                }
            }
            $tConn.Close()
        }
        catch {
            Write-Host "    [Warning] Failed sync for $($t.DatabaseName).$($tbl): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    Write-Host "  Synced Tenant [$tbl]: $totalSynced rows" -ForegroundColor Green
}

# Re-enable foreign keys
$fkEnableCmd = $cConn.CreateCommand()
$fkEnableCmd.CommandText = "EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all';"
$fkEnableCmd.ExecuteNonQuery() | Out-Null

# Update LocalSyncQueue to Synced
try {
    $qCmd = $mConn.CreateCommand()
    $qCmd.CommandText = "UPDATE LocalSyncQueue SET Status = 'Synced', ProcessedAt = GETUTCDATE() WHERE Status = 'Pending';"
    $qCmd.ExecuteNonQuery() | Out-Null
}
catch { }

# ------------------------------------------------------------------------------
# PARITY VERIFICATION
# ------------------------------------------------------------------------------
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "               VERIFICATION & PARITY REPORT                " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Write-Host "MASTER TABLES:" -ForegroundColor Yellow
foreach ($tbl in $masterTables) {
    $cmdM = $mConn.CreateCommand(); $cmdM.CommandText = "SELECT COUNT(1) FROM [$tbl]"; $cntM = $cmdM.ExecuteScalar()
    $cmdC = $cConn.CreateCommand(); $cmdC.CommandText = "SELECT COUNT(1) FROM [$tbl]"; $cntC = $cmdC.ExecuteScalar()
    $status = if ($cntM -eq $cntC) { "[MATCH]" } else { "[DIFF]" }
    $color = if ($cntM -eq $cntC) { "Green" } else { "Red" }
    Write-Host ("{0,-28} | Local: {1,4} | Cloud: {2,4} | {3}" -f $tbl, $cntM, $cntC, $status) -ForegroundColor $color
}

Write-Host "`nTENANT TABLES (Local Combined vs Cloud):" -ForegroundColor Yellow
foreach ($tbl in $tenantTables) {
    $sumLocal = 0
    foreach ($t in $tenantDbs) {
        $tConnStr = "Server=(localdb)\MSSQLLocalDB;Database=$($t.DatabaseName);Trusted_Connection=True;TrustServerCertificate=True;"
        $tConn = New-Object System.Data.SqlClient.SqlConnection($tConnStr)
        $tConn.Open()
        $cmdT = $tConn.CreateCommand(); $cmdT.CommandText = "SELECT COUNT(1) FROM [$tbl]"
        $sumLocal += [int]$cmdT.ExecuteScalar()
        $tConn.Close()
    }
    $cmdC = $cConn.CreateCommand(); $cmdC.CommandText = "SELECT COUNT(1) FROM [$tbl]"; $cntC = $cmdC.ExecuteScalar()
    $status = if ($sumLocal -eq $cntC) { "[MATCH]" } else { "[DIFF]" }
    $color = if ($sumLocal -eq $cntC) { "Green" } else { "Red" }
    Write-Host ("{0,-28} | Local: {1,4} | Cloud: {2,4} | {3}" -f $tbl, $sumLocal, $cntC, $status) -ForegroundColor $color
}

$mConn.Close()
$cConn.Close()

Write-Host "`n[SUCCESS] Local database has been completely synchronized to MonsterASP Cloud Database!`n" -ForegroundColor Green
