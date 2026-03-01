using System.Text;
using Microsoft.Data.SqlClient;
using SQLPerfAgent.Models;

namespace SQLPerfAgent.Services;

/// <summary>
/// Executes toolbox tools (SQL scripts) against SQL Server.
/// Replaces the hardcoded TigerToolboxService with a generic, plugin-driven execution engine.
/// </summary>
internal sealed class ToolboxExecutionService
{
    private readonly SqlConnectionConfig _config;

    public ToolboxExecutionService(SqlConnectionConfig config) => _config = config;

    /// <summary>
    /// Runs all discovered toolbox tools and returns a combined text report.
    /// </summary>
    public async Task<string> RunAllToolsAsync(List<ToolboxItem> tools, string? database)
    {
        var connStr = _config.ToConnectionString(database ?? "master");
        var sb = new StringBuilder();

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        foreach (var tool in tools)
        {
            sb.AppendLine($"=== TOOLBOX: {tool.Name.ToUpperInvariant()} ===");

            try
            {
                await RunToolAsync(conn, sb, tool);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ⚠ Tool '{tool.Name}' failed: {ex.Message}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Runs a single toolbox tool by executing its SQL scripts in order.
    /// </summary>
    private async Task RunToolAsync(SqlConnection conn, StringBuilder sb, ToolboxItem tool)
    {
        foreach (var script in tool.Scripts)
        {
            if (tool.UsesGoBatchSeparation)
            {
                await RunMultiPartScriptAsync(conn, sb, script.Content);
            }
            else
            {
                await RunScriptAsync(conn, sb, script.Content);
            }
        }
    }

    /// <summary>
    /// Executes a single SQL statement and formats results as a text table.
    /// </summary>
    private static async Task RunScriptAsync(SqlConnection conn, StringBuilder sb, string sql)
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

    /// <summary>
    /// Splits a script by GO statements and executes each batch.
    /// SELECT batches produce formatted output; non-SELECT batches are executed silently.
    /// </summary>
    private static async Task RunMultiPartScriptAsync(SqlConnection conn, StringBuilder sb, string sql)
    {
        var batches = sql.Split(
            ["\nGO\n", "\nGO\r\n", "\r\nGO\r\n"],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("--"))
                continue;

            try
            {
                await using var cmd = new SqlCommand(trimmed, conn) { CommandTimeout = 120 };

                if (trimmed.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    await using var reader = await cmd.ExecuteReaderAsync();

                    var colCount = reader.FieldCount;
                    if (colCount == 0) continue;

                    var colNames = Enumerable.Range(0, colCount).Select(reader.GetName).ToArray();

                    var rows = new List<string[]>();
                    while (await reader.ReadAsync())
                    {
                        var vals = new string[colCount];
                        for (int c = 0; c < colCount; c++)
                            vals[c] = reader.IsDBNull(c) ? "(null)" : reader.GetValue(c)?.ToString() ?? "";
                        rows.Add(vals);
                    }

                    if (rows.Count == 0) continue;

                    var widths = new int[colCount];
                    for (int c = 0; c < colCount; c++)
                        widths[c] = Math.Max(colNames[c].Length, rows.Max(r => r[c].Length));

                    sb.AppendLine(string.Join(" | ", colNames.Select((n, i) => n.PadRight(widths[i]))));
                    sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));

                    foreach (var row in rows)
                        sb.AppendLine(string.Join(" | ", row.Select((v, i) => v.PadRight(widths[i]))));

                    sb.AppendLine();
                }
                else
                {
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
