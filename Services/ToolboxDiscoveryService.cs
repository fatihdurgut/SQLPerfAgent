using System.Text.RegularExpressions;
using SQLPerfAgent.Models;
using SQLPerfAgent.UI;

namespace SQLPerfAgent.Services;

/// <summary>
/// Discovers toolbox tools by scanning the Toolbox/ folder at startup.
/// Each subfolder that contains a tool.md file is treated as a tool.
/// </summary>
internal static class ToolboxDiscoveryService
{
    private static readonly string ToolboxRoot =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Toolbox");

    /// <summary>
    /// Scans the Toolbox/ directory for tool subfolders and returns all discovered tools.
    /// A valid tool must contain a tool.md file and at least one .sql file.
    /// </summary>
    public static async Task<List<ToolboxItem>> DiscoverToolsAsync()
    {
        var tools = new List<ToolboxItem>();

        if (!Directory.Exists(ToolboxRoot))
        {
            ConsoleUI.WriteWarning($"Toolbox folder not found at: {ToolboxRoot}");
            return tools;
        }

        var subfolders = Directory.GetDirectories(ToolboxRoot)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var folder in subfolders)
        {
            var toolMdPath = Path.Combine(folder, "tool.md");
            if (!File.Exists(toolMdPath))
            {
                ConsoleUI.WriteWarning($"Skipping '{Path.GetFileName(folder)}' — no tool.md found.");
                continue;
            }

            var sqlFiles = Directory.GetFiles(folder, "*.sql");
            if (sqlFiles.Length == 0)
            {
                ConsoleUI.WriteWarning($"Skipping '{Path.GetFileName(folder)}' — no .sql files found.");
                continue;
            }

            var toolMdContent = await File.ReadAllTextAsync(toolMdPath);

            // Determine script execution order from tool.md references, fallback to alphabetical
            var orderedScripts = await ResolveScriptOrderAsync(toolMdContent, sqlFiles);

            // Detect GO batch separation hint from tool.md
            var usesGoBatch = toolMdContent.Contains("GO batch separation", StringComparison.OrdinalIgnoreCase)
                           || toolMdContent.Contains("GO batch", StringComparison.OrdinalIgnoreCase);

            var tool = new ToolboxItem
            {
                Name = Path.GetFileName(folder),
                ToolMdContent = toolMdContent,
                Scripts = orderedScripts,
                FolderPath = folder,
                UsesGoBatchSeparation = usesGoBatch
            };

            tools.Add(tool);
        }

        return tools;
    }

    /// <summary>
    /// Resolves the order of SQL scripts based on references in the tool.md content.
    /// If the tool.md references specific .sql filenames, those are used in order of appearance.
    /// Otherwise, scripts are loaded alphabetically.
    /// </summary>
    private static async Task<List<ToolboxScript>> ResolveScriptOrderAsync(
        string toolMdContent, string[] sqlFilePaths)
    {
        var scripts = new List<ToolboxScript>();

        // Extract .sql filenames mentioned in tool.md in order of appearance
        var mentionedFiles = Regex.Matches(toolMdContent, @"[\w\-]+\.sql", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build ordered file list: referenced files first (in MD order), then remaining alphabetically
        var orderedPaths = new List<string>();

        foreach (var mentioned in mentionedFiles)
        {
            var match = sqlFilePaths.FirstOrDefault(
                p => Path.GetFileName(p).Equals(mentioned, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !orderedPaths.Contains(match))
                orderedPaths.Add(match);
        }

        // Add any .sql files not mentioned in the MD (alphabetical)
        foreach (var path in sqlFilePaths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (!orderedPaths.Contains(path))
                orderedPaths.Add(path);
        }

        foreach (var path in orderedPaths)
        {
            var content = await File.ReadAllTextAsync(path);
            scripts.Add(new ToolboxScript
            {
                FileName = Path.GetFileName(path),
                Content = content
            });
        }

        return scripts;
    }
}
