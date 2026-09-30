using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRM.Infrastructure.Services;

public class CloudSyncService : ICloudSyncService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudSyncService> _logger;
    private readonly string _localMasterConnStr;
    private readonly string _cloudConnStr;
    private readonly string _cloudInternalConnStr;

    private static bool? _lastKnownCloudOnline;
    private static DateTime _lastCloudCheckTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CloudSyncService(IConfiguration configuration, ILogger<CloudSyncService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _localMasterConnStr = _configuration.GetConnectionString("MasterErp")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=MSME_MasterCrm;Trusted_Connection=True;TrustServerCertificate=True;";

        _cloudConnStr = _configuration.GetConnectionString("CloudConnection")
            ?? "Server=db67193.public.databaseasp.net;Database=db67193;User Id=db67193;Password=i!8PSn+5-9sR;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15;MultipleActiveResultSets=True;";

        _cloudInternalConnStr = _configuration.GetConnectionString("CloudInternalConnection")
            ?? "Server=db67193.databaseasp.net;Database=db67193;User Id=db67193;Password=i!8PSn+5-9sR;Encrypt=False;TrustServerCertificate=True;Connect Timeout=15;MultipleActiveResultSets=True;";
    }

    private async Task<SqlConnection> GetOpenCloudConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Try public connection first (used for remote clients)
        try
        {
            var conn = new SqlConnection(_cloudConnStr);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }
        catch
        {
            // Fallback to internal connection (used if hosted on MonsterASP)
            var conn2 = new SqlConnection(_cloudInternalConnStr);
            await conn2.OpenAsync(cancellationToken);
            return conn2;
        }
    }

    public async Task<bool> IsCloudAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_lastKnownCloudOnline.HasValue && (DateTime.UtcNow - _lastCloudCheckTime) < CacheDuration)
        {
            return _lastKnownCloudOnline.Value;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            await using var conn = await GetOpenCloudConnectionAsync(cts.Token);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 3;
            var res = await cmd.ExecuteScalarAsync(cts.Token);

            _lastKnownCloudOnline = (res != null);
            _lastCloudCheckTime = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cloud database is currently offline or unreachable.");
            _lastKnownCloudOnline = false;
            _lastCloudCheckTime = DateTime.UtcNow;
            return false;
        }
    }

    public async Task QueueAndSyncRecordAsync(
        string tableName,
        int? companyId,
        string recordKey,
        string operation,
        object payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return;

        string payloadJson;
        if (payload is string str)
        {
            payloadJson = str;
        }
        else
        {
            payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
        }

        int queueId = 0;

        // 1. Insert into local queue
        try
        {
            await using var lConn = new SqlConnection(_localMasterConnStr);
            await lConn.OpenAsync(cancellationToken);
            await using var cmd = lConn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO LocalSyncQueue (TableName, CompanyId, RecordKey, Operation, PayloadJson, CreatedAt, Status, RetryCount)
                OUTPUT INSERTED.QueueId
                VALUES (@t, @cid, @rk, @op, @pj, GETUTCDATE(), 'Pending', 0);";
            cmd.Parameters.AddWithValue("@t", tableName);
            cmd.Parameters.AddWithValue("@cid", (object?)companyId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@rk", recordKey ?? "");
            cmd.Parameters.AddWithValue("@op", operation ?? "UPDATE");
            cmd.Parameters.AddWithValue("@pj", payloadJson);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            queueId = Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue record for sync to LocalSyncQueue: Table={Table}, Key={Key}", tableName, recordKey);
            return;
        }

        // 2. If cloud is online, attempt immediate sync (Dual Write)
        bool isOnline = await IsCloudAvailableAsync(cancellationToken);
        if (isOnline)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(4));

                await using var cConn = await GetOpenCloudConnectionAsync(cts.Token);
                bool success = await UpsertCloudRecordAsync(cConn, tableName, companyId, recordKey, operation, payloadJson, cts.Token);

                if (success)
                {
                    // Mark as synced locally
                    await using var lConn = new SqlConnection(_localMasterConnStr);
                    await lConn.OpenAsync(cancellationToken);
                    await using var uCmd = lConn.CreateCommand();
                    uCmd.CommandText = "UPDATE LocalSyncQueue SET Status = 'Synced', ProcessedAt = GETUTCDATE() WHERE QueueId = @qid;";
                    uCmd.Parameters.AddWithValue("@qid", queueId);
                    await uCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Immediate cloud push failed for QueueId={QueueId}; leaving as Pending for background sync.", queueId);
                // Mark online status as potentially offline
                _lastKnownCloudOnline = false;
                _lastCloudCheckTime = DateTime.UtcNow;
            }
        }
    }

    public async Task<SyncResult> ProcessPendingSyncQueueAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();

        if (!await _syncLock.WaitAsync(100, cancellationToken))
        {
            result.Message = "Sync is already in progress.";
            return result;
        }

        try
        {
            bool isOnline = await IsCloudAvailableAsync(cancellationToken);
            result.IsCloudOnline = isOnline;

            if (!isOnline)
            {
                result.Success = false;
                result.Message = "Cloud database is currently offline.";
                return result;
            }

            // Fetch pending records from local queue
            var pendingItems = new List<(int QueueId, string TableName, int? CompanyId, string RecordKey, string Operation, string PayloadJson)>();

            await using (var lConn = new SqlConnection(_localMasterConnStr))
            {
                await lConn.OpenAsync(cancellationToken);
                await using var cmd = lConn.CreateCommand();
                cmd.CommandText = @"
                    SELECT TOP 200 QueueId, TableName, CompanyId, RecordKey, Operation, PayloadJson
                    FROM LocalSyncQueue
                    WHERE Status = 'Pending'
                    ORDER BY QueueId ASC;";

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    pendingItems.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5)
                    ));
                }
            }

            if (pendingItems.Count == 0)
            {
                result.Success = true;
                result.Message = "All records are already synced.";
                return result;
            }

            result.ProcessedCount = pendingItems.Count;
            await using var cConn = await GetOpenCloudConnectionAsync(cancellationToken);

            string batchId = Guid.NewGuid().ToString("N")[..8];

            foreach (var item in pendingItems)
            {
                try
                {
                    bool ok = await UpsertCloudRecordAsync(cConn, item.TableName, item.CompanyId, item.RecordKey, item.Operation, item.PayloadJson, cancellationToken);
                    if (ok)
                    {
                        // Mark synced in local queue
                        await using var lConn = new SqlConnection(_localMasterConnStr);
                        await lConn.OpenAsync(cancellationToken);
                        await using var uCmd = lConn.CreateCommand();
                        uCmd.CommandText = "UPDATE LocalSyncQueue SET Status = 'Synced', ProcessedAt = GETUTCDATE() WHERE QueueId = @qid;";
                        uCmd.Parameters.AddWithValue("@qid", item.QueueId);
                        await uCmd.ExecuteNonQueryAsync(cancellationToken);

                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailureCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    result.Errors.Add($"QueueId {item.QueueId} ({item.TableName}): {ex.Message}");
                    _logger.LogWarning(ex, "Failed to sync pending QueueId={QueueId} to cloud.", item.QueueId);

                    // Update error info in local queue
                    try
                    {
                        await using var lConn = new SqlConnection(_localMasterConnStr);
                        await lConn.OpenAsync(cancellationToken);
                        await using var uCmd = lConn.CreateCommand();
                        uCmd.CommandText = "UPDATE LocalSyncQueue SET RetryCount = RetryCount + 1, LastError = @err WHERE QueueId = @qid;";
                        uCmd.Parameters.AddWithValue("@qid", item.QueueId);
                        uCmd.Parameters.AddWithValue("@err", ex.Message);
                        await uCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch { /* ignore */ }
                }
            }

            result.Success = (result.FailureCount == 0);
            result.Message = $"Sync completed: {result.SuccessCount} synced, {result.FailureCount} failed.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during ProcessPendingSyncQueueAsync.");
            result.Success = false;
            result.Message = $"Sync error: {ex.Message}";
            result.Errors.Add(ex.Message);
            return result;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SyncResult> RunBaselineSyncAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        if (!await _syncLock.WaitAsync(100, cancellationToken))
        {
            result.Message = "Sync operation already in progress.";
            return result;
        }

        try
        {
            bool isOnline = await IsCloudAvailableAsync(cancellationToken);
            result.IsCloudOnline = isOnline;
            if (!isOnline)
            {
                result.Success = false;
                result.Message = "Cannot run baseline sync: Cloud database is offline.";
                return result;
            }

            await using var cConn = await GetOpenCloudConnectionAsync(cancellationToken);

            // 1. Sync Master Tables
            await using (var mConn = new SqlConnection(_localMasterConnStr))
            {
                await mConn.OpenAsync(cancellationToken);

                // Roles
                await SyncTableRowsAsync(mConn, cConn, "Roles", "RoleId", null, cancellationToken, result);

                // Companies
                await SyncTableRowsAsync(mConn, cConn, "Companies", "CompanyId", null, cancellationToken, result);

                // Users
                await SyncTableRowsAsync(mConn, cConn, "Users", "UserId", null, cancellationToken, result);

                // CompanyDatabases
                await SyncTableRowsAsync(mConn, cConn, "CompanyDatabases", "CompanyDatabaseId", null, cancellationToken, result);

                // TermsConditions
                await SyncTableRowsAsync(mConn, cConn, "TermsConditions", "TermsId", null, cancellationToken, result);

                // TenantSubscriptionPlans
                await SyncTableRowsAsync(mConn, cConn, "TenantSubscriptionPlans", "PlanId", null, cancellationToken, result);

                // TenantSubscriptions
                await SyncTableRowsAsync(mConn, cConn, "TenantSubscriptions", "TenantSubscriptionId", null, cancellationToken, result);

                // TenantBillingTransactions
                await SyncTableRowsAsync(mConn, cConn, "TenantBillingTransactions", "TransactionId", null, cancellationToken, result);

                // BackupLogs
                await SyncTableRowsAsync(mConn, cConn, "BackupLogs", "BackupId", null, cancellationToken, result);
            }

            // 2. Discover all local tenant databases and sync tenant tables
            var tenantDatabases = new List<(int CompanyId, string DbName)>();
            await using (var mConn = new SqlConnection(_localMasterConnStr))
            {
                await mConn.OpenAsync(cancellationToken);
                await using var cmd = mConn.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT CompanyId, DatabaseName FROM CompanyDatabases WHERE IsActive = 1;";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    tenantDatabases.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            var tenantTables = new[]
            {
                ("TenantCustomers", "TenantCustomerId"),
                ("Products", "ProductId"),
                ("Suppliers", "SupplierId"),
                ("Inventories", "InventoryId"),
                ("CustomerInteractions", "InteractionId"),
                ("ServiceRequests", "RequestId"),
                ("BillingTransactions", "TransactionId"),
                ("ServiceStatusLogs", "LogId"),
                ("FollowUps", "FollowUpId")
            };

            foreach (var tdb in tenantDatabases)
            {
                var tConnStr = $"Server=(localdb)\\MSSQLLocalDB;Database={tdb.DbName};Trusted_Connection=True;TrustServerCertificate=True;";
                try
                {
                    await using var tConn = new SqlConnection(tConnStr);
                    await tConn.OpenAsync(cancellationToken);

                    foreach (var (tName, pkName) in tenantTables)
                    {
                        await SyncTenantTableRowsAsync(tConn, cConn, tName, pkName, tdb.CompanyId, cancellationToken, result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not open tenant database '{DatabaseName}' for company {CompanyId}.", tdb.DbName, tdb.CompanyId);
                }
            }

            // Mark all currently pending queue items as synced
            try
            {
                await using var lConn = new SqlConnection(_localMasterConnStr);
                await lConn.OpenAsync(cancellationToken);
                await using var cmd = lConn.CreateCommand();
                cmd.CommandText = "UPDATE LocalSyncQueue SET Status = 'Synced', ProcessedAt = GETUTCDATE() WHERE Status = 'Pending';";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch { /* ignore */ }

            result.Success = true;
            result.Message = $"Baseline sync successful! Synced {result.SuccessCount} total records to MonsterASP cloud.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Baseline sync failed.");
            result.Success = false;
            result.Message = $"Baseline sync failed: {ex.Message}";
            result.Errors.Add(ex.Message);
            return result;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<CloudSyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new CloudSyncStatusDto
        {
            CloudServer = "db67193.public.databaseasp.net",
            CloudDatabase = "db67193"
        };

        status.IsCloudOnline = await IsCloudAvailableAsync(cancellationToken);

        try
        {
            await using var lConn = new SqlConnection(_localMasterConnStr);
            await lConn.OpenAsync(cancellationToken);
            await using var cmd = lConn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) AS PendingCount,
                    SUM(CASE WHEN Status = 'Synced' THEN 1 ELSE 0 END) AS SyncedCount,
                    SUM(CASE WHEN Status = 'Failed' THEN 1 ELSE 0 END) AS FailedCount,
                    MAX(ProcessedAt) AS LastSyncTime
                FROM LocalSyncQueue;";

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                status.PendingCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                status.SyncedCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                status.FailedCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                status.LastSyncTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read sync status from LocalSyncQueue.");
        }

        status.StatusMessage = status.IsCloudOnline
            ? (status.PendingCount > 0 ? $"Online - {status.PendingCount} items pending sync" : "Online - All data fully synced")
            : $"Offline (Local Mode) - {status.PendingCount} items queued for sync";

        return status;
    }

    // =========================================================================
    // UPSERT IMPLEMENTATIONS FOR SPECIFIC TABLES
    // =========================================================================
    private async Task<bool> UpsertCloudRecordAsync(
        SqlConnection cConn,
        string tableName,
        int? companyId,
        string recordKey,
        string operation,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        // If operation is DELETE and it's a tenant table, soft-delete or delete
        if (operation.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteCloudDeleteAsync(cConn, tableName, companyId, recordKey, cancellationToken);
        }

        return tableName.ToLowerInvariant() switch
        {
            "companies" => await UpsertCompanyAsync(cConn, root, cancellationToken),
            "users" => await UpsertUserAsync(cConn, root, cancellationToken),
            "roles" => await UpsertRoleAsync(cConn, root, cancellationToken),
            "companydatabases" => await UpsertCompanyDatabaseAsync(cConn, root, cancellationToken),
            "termsconditions" => await UpsertTermsConditionAsync(cConn, root, cancellationToken),
            "tenantsubscriptionplans" => await UpsertTenantSubscriptionPlanAsync(cConn, root, cancellationToken),
            "tenantsubscriptions" => await UpsertTenantSubscriptionAsync(cConn, root, cancellationToken),
            "tenantbillingtransactions" => await UpsertTenantBillingTransactionAsync(cConn, root, cancellationToken),
            "backuplogs" => await UpsertBackupLogAsync(cConn, root, cancellationToken),
            "tenantcustomers" => await UpsertTenantCustomerAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "products" => await UpsertProductAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "suppliers" => await UpsertSupplierAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "inventories" => await UpsertInventoryAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "customerinteractions" => await UpsertCustomerInteractionAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "servicerequests" => await UpsertServiceRequestAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "billingtransactions" => await UpsertBillingTransactionAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "servicestatuslogs" => await UpsertServiceStatusLogAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            "followups" => await UpsertFollowUpAsync(cConn, companyId ?? GetIntProperty(root, "CompanyId", 0), root, cancellationToken),
            _ => true
        };
    }

    private static async Task<bool> ExecuteCloudDeleteAsync(SqlConnection conn, string tableName, int? companyId, string recordKey, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        if (companyId.HasValue && companyId.Value > 0)
        {
            string pkCol = GetTenantPkColumn(tableName);
            cmd.CommandText = $"DELETE FROM [{tableName}] WHERE CompanyId = @cid AND [{pkCol}] = @rk;";
            cmd.Parameters.AddWithValue("@cid", companyId.Value);
            cmd.Parameters.AddWithValue("@rk", Convert.ToInt32(recordKey));
        }
        else
        {
            string pkCol = GetMasterPkColumn(tableName);
            cmd.CommandText = $"DELETE FROM [{tableName}] WHERE [{pkCol}] = @rk;";
            cmd.Parameters.AddWithValue("@rk", Convert.ToInt32(recordKey));
        }
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static string GetMasterPkColumn(string tableName) => tableName.ToLowerInvariant() switch
    {
        "companies" => "CompanyId",
        "users" => "UserId",
        "roles" => "RoleId",
        "companydatabases" => "CompanyDatabaseId",
        "termsconditions" => "TermsId",
        "tenantsubscriptionplans" => "PlanId",
        "tenantsubscriptions" => "TenantSubscriptionId",
        "tenantbillingtransactions" => "TransactionId",
        "backuplogs" => "BackupId",
        _ => "Id"
    };

    private static string GetTenantPkColumn(string tableName) => tableName.ToLowerInvariant() switch
    {
        "tenantcustomers" => "TenantCustomerId",
        "products" => "ProductId",
        "suppliers" => "SupplierId",
        "inventories" => "InventoryId",
        "customerinteractions" => "InteractionId",
        "servicerequests" => "RequestId",
        "billingtransactions" => "TransactionId",
        "servicestatuslogs" => "LogId",
        "followups" => "FollowUpId",
        _ => "Id"
    };

    // Helper property readers from JsonElement
    private static string GetStringProperty(JsonElement elem, string name, string fallback = "")
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.String => val.GetString() ?? fallback,
                JsonValueKind.Null => fallback,
                _ => val.ToString()
            };
        }
        return fallback;
    }

    private static int GetIntProperty(JsonElement elem, string name, int fallback = 0)
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i)) return i;
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var si)) return si;
        }
        return fallback;
    }

    private static int? GetNullableIntProperty(JsonElement elem, string name)
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i)) return i;
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var si)) return si;
        }
        return null;
    }

    private static decimal GetDecimalProperty(JsonElement elem, string name, decimal fallback = 0m)
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            if (val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out var d)) return d;
            if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), out var sd)) return sd;
        }
        return fallback;
    }

    private static bool GetBoolProperty(JsonElement elem, string name, bool fallback = false)
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            if (val.ValueKind == JsonValueKind.True) return true;
            if (val.ValueKind == JsonValueKind.False) return false;
            if (val.ValueKind == JsonValueKind.String && bool.TryParse(val.GetString(), out var sb)) return sb;
        }
        return fallback;
    }

    private static DateTime GetDateTimeProperty(JsonElement elem, string name)
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            if (val.ValueKind == JsonValueKind.String && DateTime.TryParse(val.GetString(), out var dt)) return dt;
        }
        return DateTime.UtcNow;
    }

    private static DateTime? GetNullableDateTimeProperty(JsonElement elem, string name)
    {
        if (elem.TryGetProperty(name, out var val) || elem.TryGetProperty(ToCamelCase(name), out val))
        {
            if (val.ValueKind == JsonValueKind.String && DateTime.TryParse(val.GetString(), out var dt)) return dt;
        }
        return null;
    }

    private static string ToCamelCase(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    // Individual Table Upserts
    private static async Task<bool> UpsertCompanyAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "CompanyId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM Companies WHERE CompanyId = @CompanyId)
BEGIN
    UPDATE Companies SET
        CompanyCode = @CompanyCode, CompanyName = @CompanyName, IsActive = @IsActive, CreatedAt = @CreatedAt,
        AddressLine = @AddressLine, City = @City, ContactEmail = @ContactEmail, ContactPhone = @ContactPhone,
        Country = @Country, PostalCode = @PostalCode, Province = @Province, State = @State,
        TermsAccepted = @TermsAccepted, TermsAcceptedAt = @TermsAcceptedAt, TermsAcceptedBy = @TermsAcceptedBy,
        TermsAcceptedVersion = @TermsAcceptedVersion, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @CompanyId;
END
ELSE
BEGIN
    INSERT INTO Companies (CompanyId, CompanyCode, CompanyName, IsActive, CreatedAt, AddressLine, City, ContactEmail, ContactPhone, Country, PostalCode, Province, State, TermsAccepted, TermsAcceptedAt, TermsAcceptedBy, TermsAcceptedVersion, SyncedAt)
    VALUES (@CompanyId, @CompanyCode, @CompanyName, @IsActive, @CreatedAt, @AddressLine, @City, @ContactEmail, @ContactPhone, @Country, @PostalCode, @Province, @State, @TermsAccepted, @TermsAcceptedAt, @TermsAcceptedBy, @TermsAcceptedVersion, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CompanyId", id);
        cmd.Parameters.AddWithValue("@CompanyCode", GetStringProperty(e, "CompanyCode"));
        cmd.Parameters.AddWithValue("@CompanyName", GetStringProperty(e, "CompanyName"));
        cmd.Parameters.AddWithValue("@IsActive", GetBoolProperty(e, "IsActive", true));
        cmd.Parameters.AddWithValue("@CreatedAt", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@AddressLine", (object?)GetStringProperty(e, "AddressLine") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City", (object?)GetStringProperty(e, "City") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContactEmail", (object?)GetStringProperty(e, "ContactEmail") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContactPhone", (object?)GetStringProperty(e, "ContactPhone") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Country", (object?)GetStringProperty(e, "Country") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PostalCode", (object?)GetStringProperty(e, "PostalCode") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Province", (object?)GetStringProperty(e, "Province") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@State", (object?)GetStringProperty(e, "State") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TermsAccepted", GetBoolProperty(e, "TermsAccepted", false));
        cmd.Parameters.AddWithValue("@TermsAcceptedAt", (object?)GetNullableDateTimeProperty(e, "TermsAcceptedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TermsAcceptedBy", (object?)GetStringProperty(e, "TermsAcceptedBy") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TermsAcceptedVersion", (object?)GetStringProperty(e, "TermsAcceptedVersion") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertUserAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "UserId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM Users WHERE UserId = @UserId)
BEGIN
    UPDATE Users SET
        RoleId = @RoleId, IdentityUserId = @IdentityUserId, Email = @Email, PasswordHash = @PasswordHash,
        Status = @Status, CreatedAt = @CreatedAt, CompanyId = @CompanyId, FirstName = @FirstName,
        LastName = @LastName, FullName = @FullName, SyncedAt = GETUTCDATE()
    WHERE UserId = @UserId;
END
ELSE
BEGIN
    INSERT INTO Users (UserId, RoleId, IdentityUserId, Email, PasswordHash, Status, CreatedAt, CompanyId, FirstName, LastName, FullName, SyncedAt)
    VALUES (@UserId, @RoleId, @IdentityUserId, @Email, @PasswordHash, @Status, @CreatedAt, @CompanyId, @FirstName, @LastName, @FullName, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@UserId", id);
        cmd.Parameters.AddWithValue("@RoleId", GetIntProperty(e, "RoleId", 1));
        cmd.Parameters.AddWithValue("@IdentityUserId", GetStringProperty(e, "IdentityUserId"));
        cmd.Parameters.AddWithValue("@Email", GetStringProperty(e, "Email"));
        cmd.Parameters.AddWithValue("@PasswordHash", GetStringProperty(e, "PasswordHash"));
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status", "Active"));
        cmd.Parameters.AddWithValue("@CreatedAt", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@CompanyId", (object?)GetNullableIntProperty(e, "CompanyId") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FirstName", GetStringProperty(e, "FirstName"));
        cmd.Parameters.AddWithValue("@LastName", GetStringProperty(e, "LastName"));
        cmd.Parameters.AddWithValue("@FullName", (object?)GetStringProperty(e, "FullName") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertRoleAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "RoleId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM Roles WHERE RoleId = @RoleId)
    UPDATE Roles SET RoleName = @RoleName, SyncedAt = GETUTCDATE() WHERE RoleId = @RoleId;
ELSE
    INSERT INTO Roles (RoleId, RoleName, SyncedAt) VALUES (@RoleId, @RoleName, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@RoleId", id);
        cmd.Parameters.AddWithValue("@RoleName", GetStringProperty(e, "RoleName"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertCompanyDatabaseAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "CompanyDatabaseId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM CompanyDatabases WHERE CompanyDatabaseId = @Id)
    UPDATE CompanyDatabases SET CompanyId = @Cid, ServerName = @Server, DatabaseName = @Db, IsActive = @Active, CredentialKey = @Cred, SyncedAt = GETUTCDATE() WHERE CompanyDatabaseId = @Id;
ELSE
    INSERT INTO CompanyDatabases (CompanyDatabaseId, CompanyId, ServerName, DatabaseName, IsActive, CredentialKey, SyncedAt)
    VALUES (@Id, @Cid, @Server, @Db, @Active, @Cred, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Cid", GetIntProperty(e, "CompanyId"));
        cmd.Parameters.AddWithValue("@Server", GetStringProperty(e, "ServerName"));
        cmd.Parameters.AddWithValue("@Db", GetStringProperty(e, "DatabaseName"));
        cmd.Parameters.AddWithValue("@Active", GetBoolProperty(e, "IsActive", true));
        cmd.Parameters.AddWithValue("@Cred", GetStringProperty(e, "CredentialKey"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertTermsConditionAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "TermsId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM TermsConditions WHERE TermsId = @Id)
    UPDATE TermsConditions SET Version = @Ver, Content = @Content, EffectiveDate = @EffDate, CreatedBy = @CreatedBy, SyncedAt = GETUTCDATE() WHERE TermsId = @Id;
ELSE
    INSERT INTO TermsConditions (TermsId, Version, Content, EffectiveDate, CreatedBy, SyncedAt)
    VALUES (@Id, @Ver, @Content, @EffDate, @CreatedBy, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Ver", GetStringProperty(e, "Version"));
        cmd.Parameters.AddWithValue("@Content", GetStringProperty(e, "Content"));
        cmd.Parameters.AddWithValue("@EffDate", GetDateTimeProperty(e, "EffectiveDate"));
        cmd.Parameters.AddWithValue("@CreatedBy", GetIntProperty(e, "CreatedBy", 1));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertTenantSubscriptionPlanAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "PlanId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM TenantSubscriptionPlans WHERE PlanId = @Id)
    UPDATE TenantSubscriptionPlans SET PlanName = @Name, Description = @Desc, Price = @Price, BillingCycle = @Cycle, MaxUsers = @MaxU, MaxCustomers = @MaxC, IsActive = @Active, CreatedAt = @Created, ArchivedAt = @ArchAt, IsArchived = @Arch, MultiBranchEnabled = @MultiBranch, SyncedAt = GETUTCDATE() WHERE PlanId = @Id;
ELSE
    INSERT INTO TenantSubscriptionPlans (PlanId, PlanName, Description, Price, BillingCycle, MaxUsers, MaxCustomers, IsActive, CreatedAt, ArchivedAt, IsArchived, MultiBranchEnabled, SyncedAt)
    VALUES (@Id, @Name, @Desc, @Price, @Cycle, @MaxU, @MaxC, @Active, @Created, @ArchAt, @Arch, @MultiBranch, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", GetStringProperty(e, "PlanName"));
        cmd.Parameters.AddWithValue("@Desc", (object?)GetStringProperty(e, "Description") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Price", GetDecimalProperty(e, "Price"));
        cmd.Parameters.AddWithValue("@Cycle", GetStringProperty(e, "BillingCycle", "Monthly"));
        cmd.Parameters.AddWithValue("@MaxU", GetIntProperty(e, "MaxUsers", 5));
        cmd.Parameters.AddWithValue("@MaxC", GetIntProperty(e, "MaxCustomers", 100));
        cmd.Parameters.AddWithValue("@Active", GetBoolProperty(e, "IsActive", true));
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@ArchAt", (object?)GetNullableDateTimeProperty(e, "ArchivedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Arch", GetBoolProperty(e, "IsArchived", false));
        cmd.Parameters.AddWithValue("@MultiBranch", GetBoolProperty(e, "MultiBranchEnabled", false));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertTenantSubscriptionAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "TenantSubscriptionId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantSubscriptionId = @Id)
    UPDATE TenantSubscriptions SET CompanyId = @Cid, PlanId = @Pid, Status = @Status, StartDate = @Start, RenewalDate = @Renew, AutoRenew = @Auto, CreatedAt = @Created, SyncedAt = GETUTCDATE() WHERE TenantSubscriptionId = @Id;
ELSE
    INSERT INTO TenantSubscriptions (TenantSubscriptionId, CompanyId, PlanId, Status, StartDate, RenewalDate, AutoRenew, CreatedAt, SyncedAt)
    VALUES (@Id, @Cid, @Pid, @Status, @Start, @Renew, @Auto, @Created, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Cid", GetIntProperty(e, "CompanyId"));
        cmd.Parameters.AddWithValue("@Pid", (object?)GetNullableIntProperty(e, "PlanId") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status", "Active"));
        cmd.Parameters.AddWithValue("@Start", (object?)GetNullableDateTimeProperty(e, "StartDate") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Renew", (object?)GetNullableDateTimeProperty(e, "RenewalDate") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Auto", GetBoolProperty(e, "AutoRenew", false));
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertTenantBillingTransactionAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "TransactionId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM TenantBillingTransactions WHERE TransactionId = @Id)
    UPDATE TenantBillingTransactions SET TenantSubscriptionId = @SubId, CompanyId = @Cid, Amount = @Amt, PaymentMethod = @Method, PaymentStatus = @Status, TransactionDate = @Date, ReferenceNumber = @Ref, SyncedAt = GETUTCDATE() WHERE TransactionId = @Id;
ELSE
    INSERT INTO TenantBillingTransactions (TransactionId, TenantSubscriptionId, CompanyId, Amount, PaymentMethod, PaymentStatus, TransactionDate, ReferenceNumber, SyncedAt)
    VALUES (@Id, @SubId, @Cid, @Amt, @Method, @Status, @Date, @Ref, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@SubId", (object?)GetNullableIntProperty(e, "TenantSubscriptionId") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Cid", GetIntProperty(e, "CompanyId"));
        cmd.Parameters.AddWithValue("@Amt", GetDecimalProperty(e, "Amount"));
        cmd.Parameters.AddWithValue("@Method", (object?)GetStringProperty(e, "PaymentMethod") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "PaymentStatus", "Completed"));
        cmd.Parameters.AddWithValue("@Date", GetDateTimeProperty(e, "TransactionDate"));
        cmd.Parameters.AddWithValue("@Ref", (object?)GetStringProperty(e, "ReferenceNumber") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertBackupLogAsync(SqlConnection conn, JsonElement e, CancellationToken ct)
    {
        int id = GetIntProperty(e, "BackupId");
        if (id <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM BackupLogs WHERE BackupId = @Id)
    UPDATE BackupLogs SET PerformedBy = @By, Type = @Type, BackupDate = @Date, FileLocation = @Loc, Status = @Status, SyncedAt = GETUTCDATE() WHERE BackupId = @Id;
ELSE
    INSERT INTO BackupLogs (BackupId, PerformedBy, Type, BackupDate, FileLocation, Status, SyncedAt)
    VALUES (@Id, @By, @Type, @Date, @Loc, @Status, GETUTCDATE());";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@By", GetIntProperty(e, "PerformedBy", 1));
        cmd.Parameters.AddWithValue("@Type", GetStringProperty(e, "Type", "Manual"));
        cmd.Parameters.AddWithValue("@Date", GetDateTimeProperty(e, "BackupDate"));
        cmd.Parameters.AddWithValue("@Loc", GetStringProperty(e, "FileLocation"));
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status", "Success"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    // Tenant tables
    private static async Task<bool> UpsertTenantCustomerAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "TenantCustomerId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM TenantCustomers WHERE CompanyId = @Cid AND TenantCustomerId = @Id)
BEGIN
    UPDATE TenantCustomers SET
        CustomerCode = @Code, FirstName = @First, LastName = @Last, CustomerName = @Name,
        ContactNumber = @Phone, EmailAddress = @Email, Street = @Street, City = @City,
        Province = @Province, Address = @Address, IsActive = @Active, CreatedAt = @Created,
        PlateNumber = @Plate, VehicleMake = @Make, VehicleModel = @Model, VehicleYear = @Year,
        VehicleColor = @Color, VehicleType = @VType, Source = @Src, IsArchived = @Arch,
        ArchivedAt = @ArchAt, ArchivedBy = @ArchBy, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND TenantCustomerId = @Id;
END
ELSE
BEGIN
    INSERT INTO TenantCustomers (CompanyId, TenantCustomerId, CustomerCode, FirstName, LastName, CustomerName, ContactNumber, EmailAddress, Street, City, Province, Address, IsActive, CreatedAt, PlateNumber, VehicleMake, VehicleModel, VehicleYear, VehicleColor, VehicleType, Source, IsArchived, ArchivedAt, ArchivedBy, SyncedAt)
    VALUES (@Cid, @Id, @Code, @First, @Last, @Name, @Phone, @Email, @Street, @City, @Province, @Address, @Active, @Created, @Plate, @Make, @Model, @Year, @Color, @VType, @Src, @Arch, @ArchAt, @ArchBy, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@Code", GetStringProperty(e, "CustomerCode"));
        cmd.Parameters.AddWithValue("@First", GetStringProperty(e, "FirstName"));
        cmd.Parameters.AddWithValue("@Last", GetStringProperty(e, "LastName"));
        cmd.Parameters.AddWithValue("@Name", (object?)GetStringProperty(e, "CustomerName") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phone", (object?)GetStringProperty(e, "ContactNumber") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)GetStringProperty(e, "EmailAddress") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Street", (object?)GetStringProperty(e, "Street") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City", (object?)GetStringProperty(e, "City") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Province", (object?)GetStringProperty(e, "Province") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Address", (object?)GetStringProperty(e, "Address") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Active", GetBoolProperty(e, "IsActive", true));
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@Plate", (object?)GetStringProperty(e, "PlateNumber") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Make", (object?)GetStringProperty(e, "VehicleMake") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Model", (object?)GetStringProperty(e, "VehicleModel") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Year", (object?)GetNullableIntProperty(e, "VehicleYear") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Color", (object?)GetStringProperty(e, "VehicleColor") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@VType", (object?)GetStringProperty(e, "VehicleType") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Src", (object?)GetStringProperty(e, "Source") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Arch", GetBoolProperty(e, "IsArchived", false));
        cmd.Parameters.AddWithValue("@ArchAt", (object?)GetNullableDateTimeProperty(e, "ArchivedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArchBy", (object?)GetStringProperty(e, "ArchivedBy") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertProductAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "ProductId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM Products WHERE CompanyId = @Cid AND ProductId = @Id)
BEGIN
    UPDATE Products SET
        ProductCode = @Code, ProductName = @Name, UnitPrice = @Price, IsActive = @Active,
        CreatedAt = @Created, Category = @Cat, Description = @Desc, DurationMinutes = @Dur,
        IsArchived = @Arch, ArchivedAt = @ArchAt, ArchivedBy = @ArchBy, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND ProductId = @Id;
END
ELSE
BEGIN
    INSERT INTO Products (CompanyId, ProductId, ProductCode, ProductName, UnitPrice, IsActive, CreatedAt, Category, Description, DurationMinutes, IsArchived, ArchivedAt, ArchivedBy, SyncedAt)
    VALUES (@Cid, @Id, @Code, @Name, @Price, @Active, @Created, @Cat, @Desc, @Dur, @Arch, @ArchAt, @ArchBy, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@Code", GetStringProperty(e, "ProductCode"));
        cmd.Parameters.AddWithValue("@Name", GetStringProperty(e, "ProductName"));
        cmd.Parameters.AddWithValue("@Price", GetDecimalProperty(e, "UnitPrice"));
        cmd.Parameters.AddWithValue("@Active", GetBoolProperty(e, "IsActive", true));
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@Cat", (object?)GetStringProperty(e, "Category") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Desc", (object?)GetStringProperty(e, "Description") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Dur", GetIntProperty(e, "DurationMinutes", 0));
        cmd.Parameters.AddWithValue("@Arch", GetBoolProperty(e, "IsArchived", false));
        cmd.Parameters.AddWithValue("@ArchAt", (object?)GetNullableDateTimeProperty(e, "ArchivedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArchBy", (object?)GetStringProperty(e, "ArchivedBy") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertSupplierAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "SupplierId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM Suppliers WHERE CompanyId = @Cid AND SupplierId = @Id)
BEGIN
    UPDATE Suppliers SET
        SupplierCode = @Code, SupplierName = @Name, ContactFirstName = @First, ContactLastName = @Last,
        ContactPerson = @Person, ContactNumber = @Phone, EmailAddress = @Email, Address = @Address,
        IsActive = @Active, CreatedAt = @Created, IsArchived = @Arch, ArchivedAt = @ArchAt,
        ArchivedBy = @ArchBy, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND SupplierId = @Id;
END
ELSE
BEGIN
    INSERT INTO Suppliers (CompanyId, SupplierId, SupplierCode, SupplierName, ContactFirstName, ContactLastName, ContactPerson, ContactNumber, EmailAddress, Address, IsActive, CreatedAt, IsArchived, ArchivedAt, ArchivedBy, SyncedAt)
    VALUES (@Cid, @Id, @Code, @Name, @First, @Last, @Person, @Phone, @Email, @Address, @Active, @Created, @Arch, @ArchAt, @ArchBy, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@Code", GetStringProperty(e, "SupplierCode"));
        cmd.Parameters.AddWithValue("@Name", GetStringProperty(e, "SupplierName"));
        cmd.Parameters.AddWithValue("@First", (object?)GetStringProperty(e, "ContactFirstName") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Last", (object?)GetStringProperty(e, "ContactLastName") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Person", (object?)GetStringProperty(e, "ContactPerson") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phone", (object?)GetStringProperty(e, "ContactNumber") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)GetStringProperty(e, "EmailAddress") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Address", (object?)GetStringProperty(e, "Address") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Active", GetBoolProperty(e, "IsActive", true));
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@Arch", GetBoolProperty(e, "IsArchived", false));
        cmd.Parameters.AddWithValue("@ArchAt", (object?)GetNullableDateTimeProperty(e, "ArchivedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArchBy", (object?)GetStringProperty(e, "ArchivedBy") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertInventoryAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "InventoryId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM Inventories WHERE CompanyId = @Cid AND InventoryId = @Id)
BEGIN
    UPDATE Inventories SET
        ProductId = @Pid, QuantityOnHand = @Qty, ReorderLevel = @Reorder,
        LastUpdatedAt = @LastUpdate, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND InventoryId = @Id;
END
ELSE
BEGIN
    INSERT INTO Inventories (CompanyId, InventoryId, ProductId, QuantityOnHand, ReorderLevel, LastUpdatedAt, SyncedAt)
    VALUES (@Cid, @Id, @Pid, @Qty, @Reorder, @LastUpdate, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@Pid", GetIntProperty(e, "ProductId"));
        cmd.Parameters.AddWithValue("@Qty", GetDecimalProperty(e, "QuantityOnHand"));
        cmd.Parameters.AddWithValue("@Reorder", GetDecimalProperty(e, "ReorderLevel"));
        cmd.Parameters.AddWithValue("@LastUpdate", GetDateTimeProperty(e, "LastUpdatedAt"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertCustomerInteractionAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "InteractionId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM CustomerInteractions WHERE CompanyId = @Cid AND InteractionId = @Id)
BEGIN
    UPDATE CustomerInteractions SET
        CustomerId = @CustId, Kind = @Kind, Severity = @Sev, Title = @Title,
        Details = @Details, Status = @Status, CreatedAt = @Created, RecordedBy = @RecordedBy,
        SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND InteractionId = @Id;
END
ELSE
BEGIN
    INSERT INTO CustomerInteractions (CompanyId, InteractionId, CustomerId, Kind, Severity, Title, Details, Status, CreatedAt, RecordedBy, SyncedAt)
    VALUES (@Cid, @Id, @CustId, @Kind, @Sev, @Title, @Details, @Status, @Created, @RecordedBy, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@CustId", GetIntProperty(e, "CustomerId"));
        cmd.Parameters.AddWithValue("@Kind", GetStringProperty(e, "Kind", "Note"));
        cmd.Parameters.AddWithValue("@Sev", GetStringProperty(e, "Severity", "Normal"));
        cmd.Parameters.AddWithValue("@Title", GetStringProperty(e, "Title"));
        cmd.Parameters.AddWithValue("@Details", (object?)GetStringProperty(e, "Details") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status", "Open"));
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@RecordedBy", (object?)GetStringProperty(e, "RecordedBy") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertServiceRequestAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "RequestId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM ServiceRequests WHERE CompanyId = @Cid AND RequestId = @Id)
BEGIN
    UPDATE ServiceRequests SET
        CustomerId = @CustId, ServiceId = @SvcId, AssignedStaffId = @StaffId, CreatedBy = @CreatedBy,
        Status = @Status, Priority = @Priority, Notes = @Notes, RequestedDate = @ReqDate,
        ScheduledDate = @SchedDate, CompletedDate = @CompDate, IsArchived = @Arch,
        ArchivedAt = @ArchAt, ArchivedBy = @ArchBy, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND RequestId = @Id;
END
ELSE
BEGIN
    INSERT INTO ServiceRequests (CompanyId, RequestId, CustomerId, ServiceId, AssignedStaffId, CreatedBy, Status, Priority, Notes, RequestedDate, ScheduledDate, CompletedDate, IsArchived, ArchivedAt, ArchivedBy, SyncedAt)
    VALUES (@Cid, @Id, @CustId, @SvcId, @StaffId, @CreatedBy, @Status, @Priority, @Notes, @ReqDate, @SchedDate, @CompDate, @Arch, @ArchAt, @ArchBy, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@CustId", GetIntProperty(e, "CustomerId"));
        cmd.Parameters.AddWithValue("@SvcId", GetIntProperty(e, "ServiceId"));
        cmd.Parameters.AddWithValue("@StaffId", (object?)GetNullableIntProperty(e, "AssignedStaffId") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", GetIntProperty(e, "CreatedBy", 1));
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status", "Pending"));
        cmd.Parameters.AddWithValue("@Priority", (object?)GetStringProperty(e, "Priority") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", (object?)GetStringProperty(e, "Notes") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReqDate", GetDateTimeProperty(e, "RequestedDate"));
        cmd.Parameters.AddWithValue("@SchedDate", (object?)GetNullableDateTimeProperty(e, "ScheduledDate") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompDate", (object?)GetNullableDateTimeProperty(e, "CompletedDate") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Arch", GetBoolProperty(e, "IsArchived", false));
        cmd.Parameters.AddWithValue("@ArchAt", (object?)GetNullableDateTimeProperty(e, "ArchivedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArchBy", (object?)GetStringProperty(e, "ArchivedBy") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertBillingTransactionAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "TransactionId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM BillingTransactions WHERE CompanyId = @Cid AND TransactionId = @Id)
BEGIN
    UPDATE BillingTransactions SET
        CustomerSubscriptionId = @SubId, RequestId = @ReqId, Amount = @Amt,
        PaymentStatus = @Status, TransactionDate = @Date, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND TransactionId = @Id;
END
ELSE
BEGIN
    INSERT INTO BillingTransactions (CompanyId, TransactionId, CustomerSubscriptionId, RequestId, Amount, PaymentStatus, TransactionDate, SyncedAt)
    VALUES (@Cid, @Id, @SubId, @ReqId, @Amt, @Status, @Date, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@SubId", (object?)GetNullableIntProperty(e, "CustomerSubscriptionId") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReqId", (object?)GetNullableIntProperty(e, "RequestId") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Amt", GetDecimalProperty(e, "Amount"));
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "PaymentStatus", "Completed"));
        cmd.Parameters.AddWithValue("@Date", GetDateTimeProperty(e, "TransactionDate"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertServiceStatusLogAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "LogId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM ServiceStatusLogs WHERE CompanyId = @Cid AND LogId = @Id)
BEGIN
    UPDATE ServiceStatusLogs SET
        RequestId = @ReqId, Status = @Status, UpdatedBy = @By, UpdatedAt = @At,
        Notes = @Notes, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND LogId = @Id;
END
ELSE
BEGIN
    INSERT INTO ServiceStatusLogs (CompanyId, LogId, RequestId, Status, UpdatedBy, UpdatedAt, Notes, SyncedAt)
    VALUES (@Cid, @Id, @ReqId, @Status, @By, @At, @Notes, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@ReqId", GetIntProperty(e, "RequestId"));
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status"));
        cmd.Parameters.AddWithValue("@By", GetIntProperty(e, "UpdatedBy", 1));
        cmd.Parameters.AddWithValue("@At", GetDateTimeProperty(e, "UpdatedAt"));
        cmd.Parameters.AddWithValue("@Notes", GetStringProperty(e, "Notes"));
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static async Task<bool> UpsertFollowUpAsync(SqlConnection conn, int companyId, JsonElement e, CancellationToken ct)
    {
        int localId = GetIntProperty(e, "FollowUpId");
        if (localId <= 0 || companyId <= 0) return false;

        var sql = @"
IF EXISTS (SELECT 1 FROM FollowUps WHERE CompanyId = @Cid AND FollowUpId = @Id)
BEGIN
    UPDATE FollowUps SET
        CustomerId = @CustId, Type = @Type, ContactMethod = @Method, Reason = @Reason,
        DiscountOffer = @Offer, Notes = @Notes, Status = @Status, ScheduledDate = @Sched,
        ValidUntil = @Valid, SentAt = @SentAt, CreatedAt = @Created, CreatedBy = @CreatedBy,
        IsArchived = @Arch, ArchivedAt = @ArchAt, ArchivedBy = @ArchBy,
        ApprovalStatus = @AppStatus, ApprovedBy = @AppBy, ApprovedAt = @AppAt,
        RejectionReason = @RejReason, RejectedBy = @RejBy, RejectedAt = @RejAt,
        ServiceRequestRequestId = @SvcReqId, SyncedAt = GETUTCDATE()
    WHERE CompanyId = @Cid AND FollowUpId = @Id;
END
ELSE
BEGIN
    INSERT INTO FollowUps (CompanyId, FollowUpId, CustomerId, Type, ContactMethod, Reason, DiscountOffer, Notes, Status, ScheduledDate, ValidUntil, SentAt, CreatedAt, CreatedBy, IsArchived, ArchivedAt, ArchivedBy, ApprovalStatus, ApprovedBy, ApprovedAt, RejectionReason, RejectedBy, RejectedAt, ServiceRequestRequestId, SyncedAt)
    VALUES (@Cid, @Id, @CustId, @Type, @Method, @Reason, @Offer, @Notes, @Status, @Sched, @Valid, @SentAt, @Created, @CreatedBy, @Arch, @ArchAt, @ArchBy, @AppStatus, @AppBy, @AppAt, @RejReason, @RejBy, @RejAt, @SvcReqId, GETUTCDATE());
END;";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Cid", companyId);
        cmd.Parameters.AddWithValue("@Id", localId);
        cmd.Parameters.AddWithValue("@CustId", GetIntProperty(e, "CustomerId"));
        cmd.Parameters.AddWithValue("@Type", GetStringProperty(e, "Type", "FollowUp"));
        cmd.Parameters.AddWithValue("@Method", GetStringProperty(e, "ContactMethod", "Email"));
        cmd.Parameters.AddWithValue("@Reason", (object?)GetStringProperty(e, "Reason") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Offer", (object?)GetStringProperty(e, "DiscountOffer") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", (object?)GetStringProperty(e, "Notes") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", GetStringProperty(e, "Status", "Pending"));
        cmd.Parameters.AddWithValue("@Sched", GetDateTimeProperty(e, "ScheduledDate"));
        cmd.Parameters.AddWithValue("@Valid", (object?)GetNullableDateTimeProperty(e, "ValidUntil") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SentAt", (object?)GetNullableDateTimeProperty(e, "SentAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Created", GetDateTimeProperty(e, "CreatedAt"));
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)GetNullableIntProperty(e, "CreatedBy") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Arch", GetBoolProperty(e, "IsArchived", false));
        cmd.Parameters.AddWithValue("@ArchAt", (object?)GetNullableDateTimeProperty(e, "ArchivedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArchBy", (object?)GetStringProperty(e, "ArchivedBy") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AppStatus", GetStringProperty(e, "ApprovalStatus", "Pending"));
        cmd.Parameters.AddWithValue("@AppBy", (object?)GetNullableIntProperty(e, "ApprovedBy") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AppAt", (object?)GetNullableDateTimeProperty(e, "ApprovedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RejReason", (object?)GetStringProperty(e, "RejectionReason") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RejBy", (object?)GetNullableIntProperty(e, "RejectedBy") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RejAt", (object?)GetNullableDateTimeProperty(e, "RejectedAt") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SvcReqId", (object?)GetNullableIntProperty(e, "ServiceRequestRequestId") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    // Helper for bulk baseline sync
    private static async Task SyncTableRowsAsync(
        SqlConnection lConn,
        SqlConnection cConn,
        string tableName,
        string pkColumn,
        int? companyId,
        CancellationToken ct,
        SyncResult result)
    {
        await using var cmd = lConn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM [{tableName}];";
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            string json = JsonSerializer.Serialize(dict);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool ok = tableName.ToLowerInvariant() switch
            {
                "companies" => await UpsertCompanyAsync(cConn, root, ct),
                "users" => await UpsertUserAsync(cConn, root, ct),
                "roles" => await UpsertRoleAsync(cConn, root, ct),
                "companydatabases" => await UpsertCompanyDatabaseAsync(cConn, root, ct),
                "termsconditions" => await UpsertTermsConditionAsync(cConn, root, ct),
                "tenantsubscriptionplans" => await UpsertTenantSubscriptionPlanAsync(cConn, root, ct),
                "tenantsubscriptions" => await UpsertTenantSubscriptionAsync(cConn, root, ct),
                "tenantbillingtransactions" => await UpsertTenantBillingTransactionAsync(cConn, root, ct),
                "backuplogs" => await UpsertBackupLogAsync(cConn, root, ct),
                _ => false
            };

            if (ok) result.SuccessCount++;
            else result.FailureCount++;
            result.ProcessedCount++;
        }
    }

    private static async Task SyncTenantTableRowsAsync(
        SqlConnection tConn,
        SqlConnection cConn,
        string tableName,
        string pkColumn,
        int companyId,
        CancellationToken ct,
        SyncResult result)
    {
        await using var cmd = tConn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM [{tableName}];";
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            string json = JsonSerializer.Serialize(dict);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool ok = tableName.ToLowerInvariant() switch
            {
                "tenantcustomers" => await UpsertTenantCustomerAsync(cConn, companyId, root, ct),
                "products" => await UpsertProductAsync(cConn, companyId, root, ct),
                "suppliers" => await UpsertSupplierAsync(cConn, companyId, root, ct),
                "inventories" => await UpsertInventoryAsync(cConn, companyId, root, ct),
                "customerinteractions" => await UpsertCustomerInteractionAsync(cConn, companyId, root, ct),
                "servicerequests" => await UpsertServiceRequestAsync(cConn, companyId, root, ct),
                "billingtransactions" => await UpsertBillingTransactionAsync(cConn, companyId, root, ct),
                "servicestatuslogs" => await UpsertServiceStatusLogAsync(cConn, companyId, root, ct),
                "followups" => await UpsertFollowUpAsync(cConn, companyId, root, ct),
                _ => false
            };

            if (ok) result.SuccessCount++;
            else result.FailureCount++;
            result.ProcessedCount++;
        }
    }
}
