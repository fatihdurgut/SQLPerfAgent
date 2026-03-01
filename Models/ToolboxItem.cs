namespace SQLPerfAgent.Models;

/// <summary>
/// Represents a single tool discovered in the Toolbox folder.
/// Each tool is a subfolder containing a tool.md (manifest/prompt) and one or more .sql scripts.
/// </summary>
public sealed record ToolboxItem
{
    /// <summary>
    /// Name of the tool (subfolder name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Raw content of the tool.md file — serves as both documentation and AI prompt context.
    /// </summary>
    public required string ToolMdContent { get; init; }

    /// <summary>
    /// Ordered list of SQL scripts to execute for this tool.
    /// Order is determined by parsing the tool.md for script references, with alphabetical fallback.
    /// </summary>
    public required IReadOnlyList<ToolboxScript> Scripts { get; init; }

    /// <summary>
    /// Full path to the tool's folder.
    /// </summary>
    public required string FolderPath { get; init; }

    /// <summary>
    /// Whether the tool.md indicates scripts should use GO batch separation.
    /// </summary>
    public bool UsesGoBatchSeparation { get; init; }

    /// <summary>
    /// First line of the tool.md (after the # header), used as a brief description.
    /// </summary>
    public string Description => ExtractDescription();

    private string ExtractDescription()
    {
        var lines = ToolMdContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Skip markdown headers
            if (trimmed.StartsWith('#'))
                continue;
            // Return first non-empty, non-header line
            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed;
        }
        return Name;
    }
}

/// <summary>
/// Represents a single SQL script file within a toolbox tool.
/// </summary>
public sealed record ToolboxScript
{
    /// <summary>
    /// File name of the SQL script (e.g., "BestPracticesChecks.sql").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Full content of the SQL script.
    /// </summary>
    public required string Content { get; init; }
}
