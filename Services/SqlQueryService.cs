using System.Text;
using Microsoft.Data.SqlClient;
using SQLPerfAgent.Models;

namespace SQLPerfAgent.Services;

/// <summary>
/// Runs DMV queries directly against SQL Server using Microsoft.Data.SqlClient.
/// Works with both Windows Auth and SQL Auth.
/// </summary>
internal sealed class SqlQueryService
{
    private readonly SqlConnectionConfig _config;

    public SqlQueryService(SqlConnectionConfig config) => _config = config;

    /// <summary>
    /// Runs all DMV diagnostic queries and returns a combined text report.
    /// </summary>
    public async Task<string> RunDiagnosticQueriesAsync(string? database)
    {
        var connStr = _config.ToConnectionString(database ?? "master");
        var sb = new StringBuilder();

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        var dbFilter = database is not null
            ? $"DB_ID('{database}')"
            : "NULL"; // current db context

        // 1. Missing Indexes
        sb.AppendLine("=== MISSING INDEXES ===");
        await RunQueryAsync(conn, sb, $"""
            SELECT TOP 20
                d.statement AS [Table],
                d.equality_columns AS EqualityColumns,
                d.inequality_columns AS InequalityColumns,
                d.included_columns AS IncludedColumns,
                CAST(ROUND(gs.avg_total_user_cost * gs.avg_user_impact * (gs.user_seeks + gs.user_scans), 0) AS BIGINT) AS ImpactScore
            FROM sys.dm_db_missing_index_details d
            JOIN sys.dm_db_missing_index_groups g ON d.index_handle = g.index_handle
            JOIN sys.dm_db_missing_index_group_stats gs ON g.index_group_handle = gs.group_handle
            {(database is not null ? $"WHERE d.database_id = DB_ID('{database}')" : "")}
            ORDER BY ImpactScore DESC
            """);

        // 2. Index Fragmentation
        sb.AppendLine("\n=== INDEX FRAGMENTATION (>30%) ===");
        await RunQueryAsync(conn, sb, $"""
            SELECT
                OBJECT_SCHEMA_NAME(ips.object_id, ips.database_id) + '.' + OBJECT_NAME(ips.object_id, ips.database_id) AS TableName,
                i.name AS IndexName,
                CAST(ips.avg_fragmentation_in_percent AS DECIMAL(5,1)) AS FragPct,
                ips.page_count AS Pages
            FROM sys.dm_db_index_physical_stats(
                {(database is not null ? $"DB_ID('{database}')" : "DB_ID()")}, NULL, NULL, NULL, 'LIMITED') ips
            JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
            WHERE ips.avg_fragmentation_in_percent > 30
              AND ips.page_count > 100
              AND i.name IS NOT NULL
            ORDER BY ips.avg_fragmentation_in_percent DESC
            """);

        // 3. Top 10 Expensive Queries by CPU
        sb.AppendLine("\n=== TOP 10 EXPENSIVE QUERIES BY CPU ===");
        await RunQueryAsync(conn, sb, $"""
            SELECT TOP 10
                qs.total_worker_time AS TotalCPU_us,
                qs.execution_count AS Executions,
                qs.total_worker_time / NULLIF(qs.execution_count, 0) AS AvgCPU_us,
                SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
                  ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
                    ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1) AS QueryText
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            {(database is not null ? $"WHERE st.dbid = DB_ID('{database}')" : "")}
            ORDER BY qs.total_worker_time DESC
            """);

        // 4. Unused Indexes (high updates, zero reads)
        sb.AppendLine("\n=== UNUSED INDEXES WITH HIGH UPDATE OVERHEAD ===");
        await RunQueryAsync(conn, sb, $"""
            SELECT
                OBJECT_SCHEMA_NAME(i.object_id) + '.' + OBJECT_NAME(i.object_id) AS TableName,
                i.name AS IndexName,
                ius.user_updates AS Updates,
                ius.user_seeks + ius.user_scans + ius.user_lookups AS Reads
            FROM sys.dm_db_index_usage_stats ius
            JOIN sys.indexes i ON ius.object_id = i.object_id AND ius.index_id = i.index_id
            WHERE ius.database_id = {(database is not null ? $"DB_ID('{database}')" : "DB_ID()")}
              AND ius.user_seeks = 0 AND ius.user_scans = 0 AND ius.user_lookups = 0
              AND ius.user_updates > 0
              AND i.is_primary_key = 0 AND i.is_unique = 0
              AND i.name IS NOT NULL
            ORDER BY ius.user_updates DESC
            """);

        return sb.ToString();
    }

    private static async Task RunQueryAsync(SqlConnection conn, StringBuilder sb, string sql)
    {
        try
        {
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync();

            var colCount = reader.FieldCount;
            var colNames = Enumerable.Range(0, colCount).Select(reader.GetName).ToArray();

            // Collect rows
            var rows = new List<string[]>();
            while (await reader.ReadAsync())
            {
                var vals = new string[colCount];
                for (int c = 0; c < colCount; c++)
                    vals[c] = reader.IsDBNull(c) ? "(null)" : reader.GetValue(c)?.ToString() ?? "";
                rows.Add(vals);
            }

            if (rows.Count == 0)
            {
                sb.AppendLine("  (no results)");
                return;
            }

            // Compute column widths
            var widths = new int[colCount];
            for (int c = 0; c < colCount; c++)
                widths[c] = Math.Max(colNames[c].Length, rows.Max(r => r[c].Length));

            // Header
            sb.AppendLine(string.Join(" | ", colNames.Select((n, i) => n.PadRight(widths[i]))));
            sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));

            // Data rows
            foreach (var row in rows)
                sb.AppendLine(string.Join(" | ", row.Select((v, i) => v.PadRight(widths[i]))));

            sb.AppendLine($"  ({rows.Count} row(s))");
        }
        catch (SqlException ex)
        {
            sb.AppendLine($"  Query error: {ex.Message}");
        }
    }
}
