-- =============================================
-- Duplicate and Redundant Index Detection
-- Source: Microsoft Tiger Toolbox - Index-Information
-- Modified for SQLPerfAgent integration
-- =============================================

-- Duplicate indexes: Indexes with identical key columns
-- Redundant indexes: One index makes another unnecessary

;WITH IndexColumns AS (
    SELECT 
        i.object_id,
        i.index_id,
        i.name AS IndexName,
        i.type_desc AS IndexType,
        i.is_unique,
        i.is_primary_key,
        STUFF((
            SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
            FROM sys.index_columns ic
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE ic.object_id = i.object_id 
              AND ic.index_id = i.index_id 
              AND ic.is_included_column = 0
            ORDER BY ic.key_ordinal
            FOR XML PATH('')
        ), 1, 2, '') AS KeyColumns,
        STUFF((
            SELECT ', ' + c.name
            FROM sys.index_columns ic
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE ic.object_id = i.object_id 
              AND ic.index_id = i.index_id 
              AND ic.is_included_column = 1
            ORDER BY c.name
            FOR XML PATH('')
        ), 1, 2, '') AS IncludedColumns
    FROM sys.indexes i
    WHERE i.type IN (1, 2) -- Clustered and Non-clustered
      AND i.is_hypothetical = 0
      AND i.object_id > 100
)
SELECT 
    OBJECT_SCHEMA_NAME(i1.object_id) + '.' + OBJECT_NAME(i1.object_id) AS TableName,
    i1.IndexName AS Index1,
    i2.IndexName AS Index2,
    i1.KeyColumns,
    CASE 
        WHEN i1.KeyColumns = i2.KeyColumns AND ISNULL(i1.IncludedColumns, '') = ISNULL(i2.IncludedColumns, '') 
        THEN 'Exact Duplicate'
        WHEN i1.KeyColumns = i2.KeyColumns 
        THEN 'Duplicate Keys (different includes)'
        WHEN LEFT(i2.KeyColumns, LEN(i1.KeyColumns)) = i1.KeyColumns 
        THEN 'Redundant (Index1 prefix of Index2)'
        ELSE 'Unknown'
    END AS DuplicateType,
    CASE 
        WHEN i1.is_primary_key = 1 THEN 'Keep (Primary Key)'
        WHEN i2.is_primary_key = 1 THEN 'Consider Dropping'
        WHEN i1.is_unique = 1 AND i2.is_unique = 0 THEN 'Keep (Unique Constraint)'
        WHEN i1.is_unique = 0 AND i2.is_unique = 1 THEN 'Consider Dropping'
        WHEN i1.index_id < i2.index_id THEN 'Keep (Created First)'
        ELSE 'Consider Dropping'
    END AS Recommendation,
    'DROP INDEX [' + i1.IndexName + '] ON [' + 
    OBJECT_SCHEMA_NAME(i1.object_id) + '].[' + OBJECT_NAME(i1.object_id) + '];' AS DropScript
FROM IndexColumns i1
JOIN IndexColumns i2 ON i1.object_id = i2.object_id 
    AND i1.index_id < i2.index_id
    AND (
        -- Exact duplicate
        (i1.KeyColumns = i2.KeyColumns AND ISNULL(i1.IncludedColumns, '') = ISNULL(i2.IncludedColumns, ''))
        OR
        -- Duplicate keys
        (i1.KeyColumns = i2.KeyColumns)
        OR
        -- Redundant (i1 is prefix of i2)
        (LEFT(i2.KeyColumns, LEN(i1.KeyColumns)) = i1.KeyColumns AND i1.is_unique = 0)
    )
ORDER BY TableName, i1.IndexName;
