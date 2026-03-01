-- =============================================
-- VLF (Virtual Log File) Count Check
-- Source: Microsoft Tiger Toolbox
-- Modified for SQLPerfAgent integration
-- =============================================

-- High VLF counts cause transaction log performance issues
-- Recommended: < 50 VLFs for most databases
-- Warning: > 100 VLFs
-- Critical: > 1000 VLFs

IF OBJECT_ID('tempdb..#vlfcounts') IS NOT NULL
    DROP TABLE #vlfcounts;

CREATE TABLE #vlfcounts (
    DatabaseName sysname,
    VLFCount INT,
    LogSizeMB DECIMAL(10,2)
);

EXEC sp_MSforeachdb 'USE [?];
INSERT INTO #vlfcounts
SELECT 
    DB_NAME() AS DatabaseName,
    COUNT(*) AS VLFCount,
    CAST(SUM(size) * 8.0 / 1024 AS DECIMAL(10,2)) AS LogSizeMB
FROM sys.database_files
WHERE type_desc = ''LOG''
GROUP BY name;';

SELECT 
    DatabaseName,
    VLFCount,
    LogSizeMB,
    CASE 
        WHEN VLFCount > 1000 THEN 'Critical'
        WHEN VLFCount > 100 THEN 'Warning'
        WHEN VLFCount > 50 THEN 'Monitor'
        ELSE 'OK'
    END AS Status,
    'High VLF count can cause transaction log performance issues. Consider log file management.' AS Recommendation
FROM #vlfcounts
WHERE VLFCount > 50
ORDER BY VLFCount DESC;

DROP TABLE #vlfcounts;
