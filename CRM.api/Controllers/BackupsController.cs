using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CRM.domain.Entities;
using CRM.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CRM.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupsController : ControllerBase
    {
        private readonly MasterErpDbContext _db;
        private readonly IConfiguration _config;

        public BackupsController(MasterErpDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // =====================================================================
        // GET /api/backups?status=Success
        // =====================================================================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status = null)
        {
            var query = _db.BackupLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(b => b.Status.ToLower() == status.ToLower());
            }

            var list = await query
                .OrderByDescending(b => b.BackupDate)
                .ThenByDescending(b => b.BackupId)
                .ToListAsync();

            var userIds = list.Select(b => b.PerformedBy).Distinct().ToList();
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var dtos = list.Select(b =>
            {
                var fileInfo = !string.IsNullOrWhiteSpace(b.FileLocation) && System.IO.File.Exists(b.FileLocation)
                    ? new FileInfo(b.FileLocation)
                    : null;

                long sizeBytes = fileInfo?.Length ?? 0;
                string sizeFormatted = FormatSize(sizeBytes);
                string fileName = !string.IsNullOrWhiteSpace(b.FileLocation)
                    ? Path.GetFileName(b.FileLocation)
                    : $"backup_{b.BackupId}.bak";

                users.TryGetValue(b.PerformedBy, out var userName);

                return new BackupItemDto
                {
                    BackupId = b.BackupId,
                    PerformedBy = b.PerformedBy,
                    PerformedByName = userName ?? $"User #{b.PerformedBy}",
                    Type = string.IsNullOrWhiteSpace(b.Type) ? "Manual" : b.Type,
                    BackupDate = b.BackupDate,
                    FileLocation = b.FileLocation,
                    FileName = fileName,
                    FileSizeBytes = sizeBytes,
                    FileSizeFormatted = sizeFormatted,
                    Status = b.Status,
                    FileExists = fileInfo != null
                };
            }).ToList();

            return Ok(dtos);
        }

        // =====================================================================
        // POST /api/backups
        // =====================================================================
        [HttpPost]
        public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest? request)
        {
            int userId = request?.PerformedBy ?? 0;

            // Resolve valid user ID
            if (userId <= 0 || !await _db.Users.AnyAsync(u => u.UserId == userId))
            {
                var fallbackUser = await _db.Users
                    .OrderBy(u => u.RoleId) // SuperAdmin role 1 preferred
                    .FirstOrDefaultAsync();

                if (fallbackUser != null)
                {
                    userId = fallbackUser.UserId;
                }
                else
                {
                    return BadRequest(new { message = "No valid user exists to record backup performance." });
                }
            }

            string backupType = string.IsNullOrWhiteSpace(request?.Type) ? "Manual" : request.Type;

            // Create backup directory in user profile
            string backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CarwashBackups");
            try
            {
                Directory.CreateDirectory(backupFolder);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Could not create backup directory: {ex.Message}" });
            }

            string timeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName = $"MSME_MasterERP_{backupType.ToLower()}_{timeStamp}.bak";
            string filePath = Path.Combine(backupFolder, fileName);

            // 1. Insert into BACKUP_LOGS with status 'In Progress'
            var log = new BackupLog
            {
                PerformedBy = userId,
                Type = backupType,
                BackupDate = DateTime.UtcNow,
                FileLocation = filePath,
                Status = "In Progress"
            };

            _db.BackupLogs.Add(log);
            await _db.SaveChangesAsync();

            // 2. Execute SQL Server BACKUP command
            try
            {
                var connStr = _config.GetConnectionString("MasterErp");
                var masterConnStr = new SqlConnectionStringBuilder(connStr)
                {
                    InitialCatalog = "master"
                }.ConnectionString;

                using (var conn = new SqlConnection(masterConnStr))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 180;
                    cmd.CommandText = "BACKUP DATABASE [MSME_MasterERP] TO DISK = @filePath WITH INIT, STATS = 10;";
                    cmd.Parameters.Add(new SqlParameter("@filePath", SqlDbType.NVarChar, -1) { Value = filePath });
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Update status to 'Success'
                log.Status = "Success";
                await _db.SaveChangesAsync();

                var fi = new FileInfo(filePath);
                return Ok(new
                {
                    message = "Backup created successfully.",
                    backupId = log.BackupId,
                    type = log.Type,
                    status = log.Status,
                    fileLocation = filePath,
                    fileName,
                    fileSize = fi.Exists ? fi.Length : 0,
                    backupDate = log.BackupDate
                });
            }
            catch (Exception ex)
            {
                // Update status to 'Failed'
                log.Status = "Failed";
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch { }

                return StatusCode(500, new { message = $"Failed to execute database backup: {ex.Message}" });
            }
        }

        // =====================================================================
        // POST /api/backups/{id}/restore
        // =====================================================================
        [HttpPost("{id:int}/restore")]
        public async Task<IActionResult> RestoreBackup(int id)
        {
            var log = await _db.BackupLogs.FirstOrDefaultAsync(b => b.BackupId == id);
            if (log == null)
            {
                return NotFound(new { message = $"Backup with ID {id} not found." });
            }

            if (!System.IO.File.Exists(log.FileLocation))
            {
                return BadRequest(new { message = $"Backup file '{log.FileLocation}' does not exist on disk." });
            }

            try
            {
                // Disconnect pooled connections
                SqlConnection.ClearAllPools();

                var connStr = _config.GetConnectionString("MasterErp");
                var masterConnStr = new SqlConnectionStringBuilder(connStr)
                {
                    InitialCatalog = "master"
                }.ConnectionString;

                using (var conn = new SqlConnection(masterConnStr))
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 300;
                    cmd.CommandText = @"
                        ALTER DATABASE [MSME_MasterERP] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        RESTORE DATABASE [MSME_MasterERP] FROM DISK = @filePath WITH REPLACE;
                        ALTER DATABASE [MSME_MasterERP] SET MULTI_USER;
                    ";
                    cmd.Parameters.Add(new SqlParameter("@filePath", SqlDbType.NVarChar, -1) { Value = log.FileLocation });
                    await cmd.ExecuteNonQueryAsync();
                }

                // Record restore event in the freshly restored database
                try
                {
                    var restoreLog = new BackupLog
                    {
                        PerformedBy = log.PerformedBy,
                        Type = "Restore",
                        BackupDate = DateTime.UtcNow,
                        FileLocation = log.FileLocation,
                        Status = "Success"
                    };
                    _db.BackupLogs.Add(restoreLog);
                    await _db.SaveChangesAsync();
                }
                catch { }

                return Ok(new
                {
                    message = "Database restored successfully.",
                    backupId = log.BackupId,
                    fileLocation = log.FileLocation,
                    restoredAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // Ensure MULTI_USER mode is restored in case of failure
                try
                {
                    var connStr = _config.GetConnectionString("MasterErp");
                    var masterConnStr = new SqlConnectionStringBuilder(connStr)
                    {
                        InitialCatalog = "master"
                    }.ConnectionString;
                    using var conn = new SqlConnection(masterConnStr);
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "ALTER DATABASE [MSME_MasterERP] SET MULTI_USER;";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { }

                return StatusCode(500, new { message = $"Database restore failed: {ex.Message}" });
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 KB";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
            return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
        }
    }

    public class CreateBackupRequest
    {
        public int? PerformedBy { get; set; }
        public string? Type { get; set; }
    }

    public class BackupItemDto
    {
        public int BackupId { get; set; }
        public int PerformedBy { get; set; }
        public string PerformedByName { get; set; } = string.Empty;
        public string Type { get; set; } = "Manual";
        public DateTime BackupDate { get; set; }
        public string FileLocation { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool FileExists { get; set; }
    }
}
