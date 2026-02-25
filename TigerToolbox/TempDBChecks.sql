-- =============================================
-- TempDB Configuration Checks
-- Source: Microsoft Tiger Toolbox - BPCheck
-- Modified for SQLPerfAgent integration
-- =============================================

-- Check TempDB file configuration best practices

DECLARE @cpuCount INT;
SELECT @cpuCount = cpu_count FROM sys.dm_os_sys_info;

-- 1. TempDB file count check
SELECT 
    'TempDB File Count' AS CheckName,
    COUNT(*) AS CurrentFileCount,
    CASE 
        WHEN @cpuCount <= 8 THEN @cpuCount
        ELSE 8
    END AS RecommendedFileCount,
    CASE 
        WHEN COUNT(*) < CASE WHEN @cpuCount <= 8 THEN @cpuCount ELSE 8 END 
        THEN 'Warning: Consider adding more TempDB data files'
        WHEN COUNT(*) > 8 
        THEN 'Warning: Too many TempDB files may cause overhead'
        ELSE 'OK'
    END AS Status
FROM sys.master_files
WHERE database_id = 2 AND type_desc = 'ROWS';

-- 2. TempDB file size equality check
WITH FileInfo AS (
    SELECT 
        name,
        size * 8 / 1024 AS SizeMB,
        growth,
        is_percent_growth
    FROM sys.master_files
    WHERE database_id = 2 AND type_desc = 'ROWS'
)
SELECT 
    'TempDB File Size Equality' AS CheckName,
    MIN(SizeMB) AS MinSizeMB,
    MAX(SizeMB) AS MaxSizeMB,
    CASE 
        WHEN MIN(SizeMB) <> MAX(SizeMB) 
        THEN 'Warning: TempDB data files should be equal size'
        ELSE 'OK'
    END AS Status,
    'ALTER DATABASE tempdb MODIFY FILE to make all files equal size' AS Recommendation
FROM FileInfo
HAVING MIN(SizeMB) <> MAX(SizeMB);

-- 3. TempDB autogrow settings check
SELECT 
    'TempDB Autogrow Settings' AS CheckName,
    name AS FileName,
    size * 8 / 1024 AS CurrentSizeMB,
    CASE 
        WHEN is_percent_growth = 1 
        THEN CAST(growth AS VARCHAR(10)) + '%'
        ELSE CAST(growth * 8 / 1024 AS VARCHAR(10)) + ' MB'
    END AS AutogrowSetting,
    CASE 
        WHEN is_percent_growth = 1 
        THEN 'Warning: Percentage autogrow not recommended for TempDB'
        WHEN growth * 8 / 1024 > 1024 
        THEN 'Warning: Autogrow > 1GB may cause performance issues'
        WHEN growth * 8 / 1024 < 64 
        THEN 'Warning: Autogrow < 64MB may cause frequent growths'
        ELSE 'OK'
    END AS Status
FROM sys.master_files
WHERE database_id = 2 AND type_desc = 'ROWS';

-- 4. TempDB on separate drive check
SELECT 
    'TempDB File Location' AS CheckName,
    physical_name,
    CASE 
        WHEN physical_name LIKE 'C:\%' 
        THEN 'Warning: TempDB on system drive (C:) not recommended'
        ELSE 'OK - Verify TempDB is on fast storage'
    END AS Status
FROM sys.master_files
WHERE database_id = 2;
