using System.Text;
using Microsoft.Data.SqlClient;
using SQLPerfAgent.Models;

namespace SQLPerfAgent.Services;

/// <summary>
/// Runs Tiger Toolbox best practices checks against SQL Server.
/// Based on Microsoft Tiger Toolbox BPCheck script.
/// </summary>
internal sealed class TigerToolboxService
{
    private readonly SqlConnectionConfig _config;

    public TigerToolboxService(SqlConnectionConfig config) => _config = config;

    /// <summary>
    /// Runs all Tiger Toolbox diagnostic checks and returns a combined text report.
    /// </summary>
    public async Task<string> RunTigerChecksAsync(string? database, bool includeAdvanced = false)
    {
        var connStr = _config.ToConnectionString(database ?? "master");
        var sb = new StringBuilder();

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        // Run all Tiger Toolbox checks
        await RunBestPracticesChecksAsync(conn, sb);
        await RunVLFChecksAsync(conn, sb);
        await RunTempDBChecksAsync(conn, sb);
        
        if (includeAdvanced)
        {
            await RunDuplicateIndexChecksAsync(conn, sb, database);
        }

        return sb.ToString();
    }

    private async Task RunBestPracticesChecksAsync(SqlConnection conn, StringBuilder sb)
    {
        sb.AppendLine("=== TIGER TOOLBOX: BEST PRACTICES CHECKS ===");
        
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TigerToolbox", "BestPracticesChecks.sql");
        if (!File.Exists(scriptPath))
        {
            sb.AppendLine("  (BestPracticesChecks.sql not found)");
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        await RunMultiPartScriptAsync(conn, sb, sql);
    }

    private async Task RunVLFChecksAsync(SqlConnection conn, StringBuilder sb)
    {
        sb.AppendLine("\n=== TIGER TOOLBOX: VLF (VIRTUAL LOG FILE) CHECKS ===");
        
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TigerToolbox", "VLFCheck.sql");
        if (!File.Exists(scriptPath))
        {
            sb.AppendLine("  (VLFCheck.sql not found)");
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        await RunScriptAsync(conn, sb, sql);
    }

    private async Task RunTempDBChecksAsync(SqlConnection conn, StringBuilder sb)
    {
        sb.AppendLine("\n=== TIGER TOOLBOX: TEMPDB CONFIGURATION CHECKS ===");
        
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TigerToolbox", "TempDBChecks.sql");
        if (!File.Exists(scriptPath))
        {
            sb.AppendLine("  (TempDBChecks.sql not found)");
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        await RunMultiPartScriptAsync(conn, sb, sql);
    }

    private async Task RunDuplicateIndexChecksAsync(SqlConnection conn, StringBuilder sb, string? database)
    {
        sb.AppendLine("\n=== TIGER TOOLBOX: DUPLICATE & REDUNDANT INDEXES ===");
        
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TigerToolbox", "DuplicateIndexes.sql");
        if (!File.Exists(scriptPath))
        {
            sb.AppendLine("  (DuplicateIndexes.sql not found)");
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        await RunScriptAsync(conn, sb, sql);
    }

    private async Task RunScriptAsync(SqlConnection conn, StringBuilder sb, string sql)
    {
        try
        {
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            await using var reader = await cmd.ExecuteReaderAsync();

            var colCount = reader.FieldCount;
            if (colCount == 0)
            {
                sb.AppendLine("  (no columns returned)");
                return;
            }

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
                sb.AppendLine("  ✓ No issues found");
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

            sb.AppendLine($"  ({rows.Count} issue(s) found)");
        }
        catch (SqlException ex)
        {
            sb.AppendLine($"  ⚠ Query error: {ex.Message}");
        }
    }

    private async Task RunMultiPartScriptAsync(SqlConnection conn, StringBuilder sb, string sql)
    {
        // Split script by GO statements and execute each batch
        var batches = sql.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("--"))
                continue;

            try
            {
                await using var cmd = new SqlCommand(trimmed, conn) { CommandTimeout = 120 };
                
                // Check if this is a SELECT statement
                if (trimmed.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    await using var reader = await cmd.ExecuteReaderAsync();

                    var colCount = reader.FieldCount;
                    if (colCount == 0) continue;

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

                    if (rows.Count == 0) continue;

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

                    sb.AppendLine();
                }
                else
                {
                    // Execute non-SELECT statement
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                sb.AppendLine($"  ⚠ Batch error: {ex.Message}");
            }
        }
    }
}
