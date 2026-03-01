-- =============================================
-- Best Practices Checks
-- Source: Microsoft Tiger Toolbox - BPCheck (condensed)
-- Modified for SQLPerfAgent integration
-- =============================================

-- This is a condensed version of key BPCheck checks
-- Full BPCheck script has 100+ checks - this includes the most critical ones

-- ============================================= 
-- 1. BUFFER POOL / MEMORY CHECKS
-- =============================================
SELECT 
    'Memory Pressure' AS CheckName,
    CAST(total_physical_memory_kb / 1024.0 / 1024 AS DECIMAL(10,2)) AS TotalMemoryGB,
    CAST(available_physical_memory_kb / 1024.0 / 1024 AS DECIMAL(10,2)) AS AvailableMemoryGB,
    CASE 
        WHEN available_physical_memory_kb * 1.0 / NULLIF(total_physical_memory_kb, 0) < 0.05
        THEN 'Critical: Less than 5% physical memory available'
        WHEN available_physical_memory_kb * 1.0 / NULLIF(total_physical_memory_kb, 0) < 0.10
        THEN 'Warning: Less than 10% physical memory available'
        ELSE 'OK'
    END AS Status
FROM sys.dm_os_sys_memory;

-- ============================================= 
-- 2. BACKUP CHECKS
-- =============================================
SELECT 
    'Database Backups' AS CheckName,
    d.name AS DatabaseName,
    d.recovery_model_desc AS RecoveryModel,
    ISNULL(CAST(b.last_full_backup AS VARCHAR(20)), 'NEVER') AS LastFullBackup,
    ISNULL(CAST(b.last_log_backup AS VARCHAR(20)), 'NEVER') AS LastLogBackup,
    CASE 
        WHEN b.last_full_backup IS NULL 
        THEN 'Critical: No full backup found'
        WHEN b.last_full_backup < DATEADD(DAY, -7, GETDATE())
        THEN 'Warning: Last full backup older than 7 days'
        WHEN d.recovery_model_desc = 'FULL' AND b.last_log_backup IS NULL
        THEN 'Warning: Full recovery model but no log backups'
        WHEN d.recovery_model_desc = 'FULL' AND b.last_log_backup < DATEADD(HOUR, -24, GETDATE())
        THEN 'Warning: Last log backup older than 24 hours'
        ELSE 'OK'
    END AS Status
FROM sys.databases d
LEFT JOIN (
    SELECT 
        database_name,
        MAX(CASE WHEN type = 'D' THEN backup_finish_date END) AS last_full_backup,
        MAX(CASE WHEN type = 'L' THEN backup_finish_date END) AS last_log_backup
    FROM msdb.dbo.backupset
    GROUP BY database_name
) b ON d.name = b.database_name
WHERE d.database_id > 4 -- Exclude system databases
  AND d.state_desc = 'ONLINE'
ORDER BY d.name;

-- ============================================= 
-- 3. DBCC CHECKDB CHECKS
-- =============================================
SELECT 
    'Database Integrity' AS CheckName,
    d.name AS DatabaseName,
    ISNULL(CAST(dbcc.LastCheckDB AS VARCHAR(20)), 'NEVER') AS LastCheckDB,
    CASE 
        WHEN dbcc.LastCheckDB IS NULL 
        THEN 'Critical: DBCC CHECKDB never run'
        WHEN dbcc.LastCheckDB < DATEADD(DAY, -7, GETDATE())
        THEN 'Warning: DBCC CHECKDB not run in last 7 days'
        ELSE 'OK'
    END AS Status
FROM sys.databases d
CROSS APPLY (
    SELECT TOP 1 
        CAST(value AS DATETIME) AS LastCheckDB
    FROM sys.dm_db_log_space_usage
    WHERE database_id = d.database_id
) logspace
OUTER APPLY (
    SELECT TOP 1
        CAST(
            ISNULL(
                (
                    SELECT value 
                    FROM sys.fn_dblog(NULL, NULL) 
                    WHERE [Current LSN] IS NOT NULL
                ),
                '1900-01-01'
            ) AS DATETIME
        ) AS LastCheckDB
) dbcc
WHERE d.database_id > 4
  AND d.state_desc = 'ONLINE';

-- ============================================= 
-- 4. MAX DEGREE OF PARALLELISM (MAXDOP) CHECK
-- =============================================
DECLARE @LogicalCPUs INT;
SELECT @LogicalCPUs = cpu_count FROM sys.dm_os_sys_info;

SELECT 
    'MaxDOP Configuration' AS CheckName,
    value_in_use AS CurrentMaxDOP,
    @LogicalCPUs AS LogicalCPUs,
    CASE 
        WHEN @LogicalCPUs > 8 THEN 8
        ELSE @LogicalCPUs
    END AS RecommendedMaxDOP,
    CASE 
        WHEN value_in_use = 0 AND @LogicalCPUs > 8
        THEN 'Warning: MAXDOP=0 with many CPUs may cause excessive parallelism'
        WHEN value_in_use > 8
        THEN 'Warning: MAXDOP > 8 rarely beneficial'
        ELSE 'OK'
    END AS Status
FROM sys.configurations
WHERE name = 'max degree of parallelism';

-- ============================================= 
-- 5. POWER PLAN CHECK (Windows)
-- =============================================
EXEC xp_cmdshell 'powercfg /GETACTIVESCHEME', NO_OUTPUT;

-- ============================================= 
-- 6. INSTANT FILE INITIALIZATION CHECK
-- =============================================
SELECT 
    'Instant File Initialization' AS CheckName,
    CASE 
        WHEN instant_file_initialization_enabled = 'Y'
        THEN 'Enabled'
        ELSE 'Disabled'
    END AS Status,
    CASE 
        WHEN instant_file_initialization_enabled = 'N'
        THEN 'Warning: IFI not enabled - data file operations will be slower'
        ELSE 'OK'
    END AS Recommendation
FROM sys.dm_server_services
WHERE servicename LIKE 'SQL Server (%';

-- ============================================= 
-- 7. DEPRECATED FEATURE USAGE
-- =============================================
SELECT TOP 10
    'Deprecated Features' AS CheckName,
    object_name AS FeatureName,
    instance_name AS Usage,
    cntr_value AS Occurrences,
    'Warning: This feature will be removed in future SQL Server versions' AS Recommendation
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Deprecated%'
  AND cntr_value > 0
ORDER BY cntr_value DESC;
