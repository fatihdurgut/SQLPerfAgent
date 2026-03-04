using System.Text.Json;
using SQLPerfAgent.Models;
using SQLPerfAgent.Services;
using SQLPerfAgent.UI;

// ── Banner ──
ConsoleUI.WriteBanner();

try
{
// ── Step 0: GitHub Copilot Authentication Check ──
ConsoleUI.WriteHeader("GitHub Copilot Authentication");
ConsoleUI.WriteInfo("Verifying GitHub Copilot access...");

var isAuthenticated = await CopilotFixService.CheckAuthenticationAsync();

if (!isAuthenticated)
{
    ConsoleUI.WriteError("GitHub Copilot authentication failed.");
    Console.WriteLine();
    ConsoleUI.WriteWarning("This tool requires an active GitHub Copilot subscription.");
    Console.WriteLine();
    
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("  To authenticate:");
    Console.ResetColor();
    Console.WriteLine("    1. Ensure you have a GitHub Copilot subscription");
    Console.WriteLine("    2. Sign in to GitHub in VS Code or GitHub CLI");
    Console.WriteLine("    3. Run: gh auth login");
    Console.WriteLine();
    
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  For more information:");
    Console.WriteLine("    - GitHub Copilot: https://github.com/features/copilot");
    Console.WriteLine("    - GitHub CLI: https://cli.github.com/");
    Console.ResetColor();
    Console.WriteLine();
    
    ConsoleUI.WriteInfo("Please authenticate and try again.");
    return 1;
}

ConsoleUI.WriteSuccess("GitHub Copilot authentication verified.");

var gitHubUsername = await CopilotFixService.GetGitHubUsernameAsync();
if (gitHubUsername is not null)
{
    ConsoleUI.WriteInfo($"Signed in as: {gitHubUsername}");
}

// ── Step 1: Connection Setup ──
ConsoleUI.WriteHeader("SQL Server Connection Setup");

var server = ConsoleUI.PromptInput("Server", "localhost");

var authChoice = ConsoleUI.PromptChoice(
    "Authentication method:",
    "Windows Authentication (Trusted Connection)",
    "SQL Server Authentication (username/password)");

string? username = null;
string? password = null;
if (authChoice == 1)
{
    username = ConsoleUI.PromptInput("Username", "sa");
    password = ConsoleUI.PromptPassword("Password");
}

// ── Step 2: Database Selection ──
ConsoleUI.WriteHeader("Database Selection");

ConsoleUI.WriteInfo("Fetching database list...");
var databases = new List<string>();
try
{
    var tempConfig = new SqlConnectionConfig
    {
        Server = server,
        UseWindowsAuth = authChoice == 0,
        Username = username,
        Password = password,
        Database = null
    };
    using var conn = new Microsoft.Data.SqlClient.SqlConnection(tempConfig.ToConnectionString("master"));
    await conn.OpenAsync();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name";
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        databases.Add(reader.GetString(0));
}
catch (Exception ex)
{
    ConsoleUI.WriteWarning($"Could not fetch database list: {ex.Message}");
}

string? database = null;
if (databases.Count > 0)
{
    var options = new List<string>(databases) { "Scan all user databases" };
    var dbChoice = ConsoleUI.PromptChoice(
        "Select a database to scan:",
        options.ToArray());

    if (dbChoice < databases.Count)
        database = databases[dbChoice];
}
else
{
    ConsoleUI.WriteInfo("Falling back to manual entry.");
    database = ConsoleUI.PromptInput("Database name");
}

var connectionConfig = new SqlConnectionConfig
{
    Server = server,
    UseWindowsAuth = authChoice == 0,
    Username = username,
    Password = password,
    Database = database
};

ConsoleUI.WriteSuccess($"Connecting to {server}" +
    (database is not null ? $" / {database}" : " (all databases)") +
    (connectionConfig.UseWindowsAuth ? " [Windows Auth]" : " [SQL Auth]"));

// ── Step 3A: Discover Toolbox Plugins ──
ConsoleUI.WriteHeader("Toolbox Discovery");
ConsoleUI.WriteInfo("Scanning for toolbox plugins...");

var allToolboxItems = await ToolboxDiscoveryService.DiscoverToolsAsync();
var toolboxItems = new List<ToolboxItem>(); // Will hold user's selected tools

if (allToolboxItems.Count > 0)
{
    ConsoleUI.WriteSuccess($"Found {allToolboxItems.Count} toolbox tool(s):");
    for (int i = 0; i < allToolboxItems.Count; i++)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    {i + 1}. {allToolboxItems[i].Name} — {allToolboxItems[i].Description}");
        Console.ResetColor();
    }
}
else
{
    ConsoleUI.WriteInfo("No toolbox plugins found. Only standard DMV queries will run.");
    ConsoleUI.WriteInfo("Add tools in the Toolbox/ folder — see README for details.");
}

// ── Step 3: Initialize Copilot with mssql-mcp ──
ConsoleUI.WriteHeader("Initializing Copilot Agent");
ConsoleUI.WriteInfo("Starting Copilot SDK with mssql-mcp server...");

var copilotService = new CopilotFixService(connectionConfig);
try
{
    await copilotService.InitializeAsync();
    ConsoleUI.WriteSuccess("Copilot agent ready with mssql-mcp connection.");
}
catch (Exception ex)
{
    ConsoleUI.WriteError($"Failed to initialize: {ex.Message}");
    return 1;
}

// ── Step 3B: Interactive Tool Selection ──
if (allToolboxItems.Count > 0)
{
    ConsoleUI.WriteHeader("Tool Selection");
    ConsoleUI.WriteInfo("Standard DMV queries (missing indexes, fragmentation, expensive queries, unused indexes) always run.");

    var selectionMode = ConsoleUI.PromptChoice(
        "Which additional toolbox checks would you like to run?",
        "Run all toolbox tools",
        "Let me pick specific tools",
        "Describe my problem — get AI suggestions",
        "Skip toolbox tools (DMV queries only)");

    if (selectionMode == 0)
    {
        // Run all
        toolboxItems = allToolboxItems;
        ConsoleUI.WriteSuccess($"All {toolboxItems.Count} toolbox tool(s) selected.");
    }
    else if (selectionMode == 1)
    {
        // Manual multi-select
        var toolOptions = allToolboxItems.Select(t => $"{t.Name} — {t.Description}").ToArray();
        var selected = ConsoleUI.PromptMultiChoice("Select tools to run:", toolOptions);
        toolboxItems = selected.Select(i => allToolboxItems[i]).ToList();
        ConsoleUI.WriteSuccess($"Selected {toolboxItems.Count} tool(s): {string.Join(", ", toolboxItems.Select(t => t.Name))}");
    }
    else if (selectionMode == 2)
    {
        // AI-assisted selection loop
        ConsoleUI.WriteInfo("Describe your problem or concern, and Copilot will suggest which tools to run.");
        ConsoleUI.WriteInfo("Type 'done' when you're ready to proceed with the suggested tools.\n");

        var suggestedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  Describe your concern: ");
            Console.ResetColor();
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                throw new QuitException();

            if (input.Equals("done", StringComparison.OrdinalIgnoreCase))
                break;

            ConsoleUI.WriteInfo("Analyzing your concern...\n");

            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                var suggestion = await copilotService.SuggestToolsAsync(input, allToolboxItems, database);
                Console.ResetColor();

                if (suggestion is not null)
                {
                    // Extract mentioned tool names from AI response
                    foreach (var tool in allToolboxItems)
                    {
                        if (suggestion.Contains(tool.Name, StringComparison.OrdinalIgnoreCase))
                            suggestedToolNames.Add(tool.Name);
                    }
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ResetColor();
                ConsoleUI.WriteWarning($"Could not get suggestion: {ex.Message}");
            }

            // Show running tally of suggested tools
            if (suggestedToolNames.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  Suggested so far: {string.Join(", ", suggestedToolNames)}");
                Console.ResetColor();
            }

            ConsoleUI.WriteInfo("Ask another question, or type 'done' to proceed.\n");
        }

        // Let the user confirm or adjust the AI suggestions
        if (suggestedToolNames.Count > 0)
        {
            var suggested = allToolboxItems
                .Where(t => suggestedToolNames.Contains(t.Name))
                .ToList();

            Console.WriteLine();
            ConsoleUI.WriteInfo($"Based on your input, these tools are suggested:");
            foreach (var t in suggested)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    • {t.Name} — {t.Description}");
                Console.ResetColor();
            }

            var confirmSuggestions = ConsoleUI.PromptChoice(
                "How would you like to proceed?",
                $"Run suggested tools ({suggested.Count})",
                "Let me adjust — pick from full list",
                "Run all tools",
                "Skip toolbox tools");

            if (confirmSuggestions == 0)
            {
                toolboxItems = suggested;
            }
            else if (confirmSuggestions == 1)
            {
                var toolOptions = allToolboxItems.Select(t =>
                {
                    var marker = suggestedToolNames.Contains(t.Name) ? " ★" : "";
                    return $"{t.Name} — {t.Description}{marker}";
                }).ToArray();
                var selected = ConsoleUI.PromptMultiChoice("Select tools to run (★ = AI suggested):", toolOptions);
                toolboxItems = selected.Select(i => allToolboxItems[i]).ToList();
            }
            else if (confirmSuggestions == 2)
            {
                toolboxItems = allToolboxItems;
            }
            // else: skip (toolboxItems stays empty)
        }
        else
        {
            // No tools were suggested — ask if they want to pick manually or skip
            var fallback = ConsoleUI.PromptChoice(
                "No specific tools were suggested. What would you like to do?",
                "Run all tools anyway",
                "Pick specific tools manually",
                "Skip toolbox tools (DMV queries only)");

            if (fallback == 0)
                toolboxItems = allToolboxItems;
            else if (fallback == 1)
            {
                var toolOptions = allToolboxItems.Select(t => $"{t.Name} — {t.Description}").ToArray();
                var selected = ConsoleUI.PromptMultiChoice("Select tools to run:", toolOptions);
                toolboxItems = selected.Select(i => allToolboxItems[i]).ToList();
            }
        }

        if (toolboxItems.Count > 0)
            ConsoleUI.WriteSuccess($"Selected {toolboxItems.Count} tool(s): {string.Join(", ", toolboxItems.Select(t => t.Name))}");
        else
            ConsoleUI.WriteInfo("No toolbox tools selected — running DMV queries only.");
    }
    // else selectionMode == 3: skip, toolboxItems stays empty
}

// ── Step 4: Fetch & Analyze (with retry loop) ──
List<Recommendation> recommendations = [];

while (true)
{
    ConsoleUI.WriteHeader("Fetching Recommendations");
    
    string dmvResults;
    
    // Always run standard DMV queries
    ConsoleUI.WriteInfo($"Running diagnostics{(database is not null ? $" on [{database}]" : " across all user databases")}...");
    
    var queryService = new SqlQueryService(connectionConfig);
    var sb = new System.Text.StringBuilder();
    
    ConsoleUI.WriteInfo("  • Running standard DMV queries...");
    try
    {
        var dmvData = await queryService.RunDiagnosticQueriesAsync(database);
        sb.AppendLine(dmvData);
        sb.AppendLine();
    }
    catch (Exception ex)
    {
        ConsoleUI.WriteError($"  Failed to run DMV queries: {ex.Message}");
        return 1;
    }
    
    // Run selected toolbox tools
    if (toolboxItems.Count > 0)
    {
        ConsoleUI.WriteInfo($"  • Running {toolboxItems.Count} toolbox check(s)...");
        var toolboxService = new ToolboxExecutionService(connectionConfig);
        try
        {
            var toolboxData = await toolboxService.RunAllToolsAsync(toolboxItems, database);
            sb.AppendLine(toolboxData);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteWarning($"  Some toolbox checks failed: {ex.Message}");
        }
    }
    
    dmvResults = sb.ToString();
    ConsoleUI.WriteSuccess($"Scan completed ({dmvResults.Split('\n').Length} lines of data).");

    // Send results to Copilot for analysis
    ConsoleUI.WriteInfo("Sending results to Copilot for analysis...");
    Console.WriteLine();

    var originalOut = Console.Out;
    Console.SetOut(TextWriter.Null);
    string? recommendationsJson;
    try
    {
        recommendationsJson = await copilotService.AnalyzeResultsAsync(dmvResults, database, toolboxItems);
    }
    finally
    {
        Console.SetOut(originalOut);
    }

    if (recommendationsJson is not null)
    {
        try
        {
            var jsonStart = recommendationsJson.IndexOf('[');
            var jsonEnd = recommendationsJson.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = recommendationsJson[jsonStart..(jsonEnd + 1)];
                
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = 
                    { 
                        TolerantEnumConverterFactory.CreateCategoryConverter(),
                        TolerantEnumConverterFactory.CreateSeverityConverter()
                    },
                    // Allow missing required properties by treating them as null/default
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                };
                
                recommendations = JsonSerializer.Deserialize<List<Recommendation>>(json, jsonOptions) ?? [];
                
                if (recommendations.Count > 0)
                {
                    ConsoleUI.WriteSuccess($"Parsed {recommendations.Count} recommendation(s) from AI analysis.");
                }
            }
            else
            {
                // No JSON array found in response
                ConsoleUI.WriteInfo("AI analysis returned no structured recommendations.");
            }
        }
        catch (JsonException)
        {
            // JSON parsing failed - show user-friendly message
            ConsoleUI.WriteInfo("AI analysis completed, but results could not be structured.");
            ConsoleUI.WriteInfo("This might mean no issues were found, or the response format was unexpected.");
            
            // Optional: Show technical details for debugging
            // To enable debug output, uncomment the catch parameter and the line below:
            // ConsoleUI.WriteWarning($"Debug info: {ex.Message}");
        }
        catch (Exception)
        {
            // Unexpected error
            ConsoleUI.WriteWarning("An unexpected error occurred while processing AI recommendations.");
            ConsoleUI.WriteInfo($"Please try running the scan again. If the issue persists, contact support.");
            
            // Optional: Show technical details for debugging
            // To enable debug output, uncomment the catch parameter and the line below:
            // ConsoleUI.WriteError($"Debug info: {ex.GetType().Name} - {ex.Message}");
        }
    }
    else
    {
        ConsoleUI.WriteInfo("No response received from AI analysis.");
    }

    // If we got results, break out and continue to the fix workflow
    if (recommendations.Count > 0)
        break;

    // No issues found — offer the user a choice
    ConsoleUI.WriteSuccess("No issues found" +
        (database is not null ? $" in [{database}]." : " across all user databases.") +
        " The server looks healthy!");
    Console.WriteLine();

    // Build next-action options based on context
    var nextOptions = new List<string>();
    if (databases.Count > 1)
        nextOptions.Add("Scan a different database");
    nextOptions.Add("Connect to a different server");
    nextOptions.Add("Exit");

    var nextChoice = ConsoleUI.PromptChoice("What would you like to do?", nextOptions.ToArray());
    var chosen = nextOptions[nextChoice];

    if (chosen == "Scan a different database")
    {
        // Let them pick another database from the same server
        var dbOptions = databases.Where(d => d != database).ToList();
        dbOptions.Add("Scan all user databases");
        var newDbChoice = ConsoleUI.PromptChoice("Select a database to scan:", dbOptions.ToArray());

        database = newDbChoice < dbOptions.Count - 1 ? dbOptions[newDbChoice] : null;

        // Rebuild connectionConfig with new database
        connectionConfig = new SqlConnectionConfig
        {
            Server = server,
            UseWindowsAuth = authChoice == 0,
            Username = username,
            Password = password,
            Database = database
        };
        continue; // loop back to scan again
    }
    else if (chosen == "Connect to a different server")
    {
        ConsoleUI.WriteHeader("New Server Connection");
        server = ConsoleUI.PromptInput("Server", "localhost");

        authChoice = ConsoleUI.PromptChoice(
            "Authentication method:",
            "Windows Authentication (Trusted Connection)",
            "SQL Server Authentication (username/password)");

        username = null;
        password = null;
        if (authChoice == 1)
        {
            username = ConsoleUI.PromptInput("Username", "sa");
            password = ConsoleUI.PromptPassword("Password");
        }

        // Re-fetch database list for new server
        databases.Clear();
        ConsoleUI.WriteInfo("Fetching database list...");
        try
        {
            var tempConfig = new SqlConnectionConfig
            {
                Server = server,
                UseWindowsAuth = authChoice == 0,
                Username = username,
                Password = password,
                Database = null
            };
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(tempConfig.ToConnectionString("master"));
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sys.databases WHERE database_id > 4 ORDER BY name";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                databases.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteWarning($"Could not fetch database list: {ex.Message}");
        }

        database = null;
        if (databases.Count > 0)
        {
            var dbOptions = new List<string>(databases) { "Scan all user databases" };
            var newDbChoice = ConsoleUI.PromptChoice("Select a database to scan:", dbOptions.ToArray());
            if (newDbChoice < databases.Count)
                database = databases[newDbChoice];
        }
        else
        {
            database = ConsoleUI.PromptInput("Database name");
        }

        connectionConfig = new SqlConnectionConfig
        {
            Server = server,
            UseWindowsAuth = authChoice == 0,
            Username = username,
            Password = password,
            Database = database
        };

        ConsoleUI.WriteSuccess($"Connecting to {server}" +
            (database is not null ? $" / {database}" : " (all databases)") +
            (connectionConfig.UseWindowsAuth ? " [Windows Auth]" : " [SQL Auth]"));

        // Re-initialize Copilot with new config
        await copilotService.DisposeAsync();
        copilotService = new CopilotFixService(connectionConfig);
        try
        {
            await copilotService.InitializeAsync();
            ConsoleUI.WriteSuccess("Copilot agent ready.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteError($"Failed to initialize: {ex.Message}");
            return 1;
        }

        continue; // loop back to scan again
    }
    else
    {
        // Exit
        ConsoleUI.WriteInfo("Done. Thank you for using SQL Performance & Security Agent!");
        return 0;
    }
}

// ── Step 5: Display Recommendations ──
ConsoleUI.WriteHeader($"Found {recommendations.Count} Recommendation(s)");

var grouped = recommendations
    .OrderBy(r => r.Severity)
    .GroupBy(r => r.Category);

int index = 1;
foreach (var group in grouped)
{
    Console.ForegroundColor = group.Key switch
    {
        RecommendationCategory.Performance => ConsoleColor.Red,
        RecommendationCategory.Security => ConsoleColor.Magenta,
        RecommendationCategory.Configuration => ConsoleColor.Yellow,
        _ => ConsoleColor.White
    };
    Console.WriteLine($"\n  ── {group.Key} ──");
    Console.ResetColor();

    foreach (var rec in group)
    {
        var severityColor = rec.Severity switch
        {
            RecommendationSeverity.High => ConsoleColor.Red,
            RecommendationSeverity.Medium => ConsoleColor.DarkYellow,
            RecommendationSeverity.Low => ConsoleColor.Gray,
            _ => ConsoleColor.DarkGray
        };

        Console.Write($"  {index,3}. ");
        Console.ForegroundColor = severityColor;
        Console.Write($"[{rec.Severity}] ");
        Console.ResetColor();
        Console.WriteLine($"{rec.Title}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"       Object: {rec.AffectedObject}");
        // Word-wrap description to console width
        var descLines = WordWrap(rec.Description, Math.Max(40, Console.WindowWidth - 10));
        foreach (var line in descLines)
            Console.WriteLine($"       {line}");
        Console.ResetColor();

        index++;
    }
}

// ── Step 5.5: Interactive Q&A (Optional) ──
ConsoleUI.WriteHeader("Next Steps");

var nextAction = ConsoleUI.PromptChoice(
    "What would you like to do?",
    "Ask a question about database performance",
    "Apply fixes for the recommendations above",
    "Exit without applying fixes");

if (nextAction == 0)
{
    // Interactive Q&A mode
    ConsoleUI.WriteHeader("Interactive Q&A Mode");
    ConsoleUI.WriteInfo("Ask questions about your database performance, configuration, or any SQL Server topic.");
    ConsoleUI.WriteInfo("Type 'done' when you're ready to proceed to fix mode, or 'quit' to exit.\n");

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  Q: ");
        Console.ResetColor();
        var question = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(question))
            continue;

        if (question.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleUI.WriteSuccess("Exiting Q&A mode.");
            break;
        }

        if (question.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            ConsoleUI.WriteInfo("Thank you for using SQL Performance & Security Agent!");
            return 0;
        }

        Console.WriteLine();
        ConsoleUI.WriteInfo("Analyzing your question...");
        Console.WriteLine();

        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  A: ");
            Console.ResetColor();
            
            var answer = await copilotService.AskQuestionAsync(question, database);
            
            if (answer is not null)
            {
                Console.WriteLine();
            }
            else
            {
                ConsoleUI.WriteWarning("Could not generate an answer. Please try rephrasing your question.");
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteError($"Error processing question: {ex.Message}");
        }

        Console.WriteLine();
    }

    // After Q&A, ask again if they want to apply fixes
    var proceedToFixes = ConsoleUI.PromptChoice(
        "Would you like to apply fixes now?",
        "Yes, proceed to fix mode",
        "No, exit");

    if (proceedToFixes == 1)
    {
        ConsoleUI.WriteInfo("Thank you for using SQL Performance & Security Agent!");
        return 0;
    }
}
else if (nextAction == 2)
{
    ConsoleUI.WriteInfo("Thank you for using SQL Performance & Security Agent!");
    return 0;
}

// ── Step 6: Fix Mode Selection ──
ConsoleUI.WriteHeader("Fix Mode");

var modeChoice = ConsoleUI.PromptChoice(
    "How would you like to fix these recommendations?",
    "Fix one by one (explain each, generate script, ask permission)",
    "Fix all (generate all scripts, review summary, then execute)");

// ── Step 7: Fix Workflow ──
if (modeChoice == 0)
{
    // One-by-one mode
    ConsoleUI.WriteHeader("One-by-One Fix Mode");

    foreach (var rec in recommendations)
    {
        ConsoleUI.WriteHeader($"Recommendation: {rec.Title}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  Category: {rec.Category} | Severity: {rec.Severity} | Object: {rec.AffectedObject}");
        Console.ResetColor();

        // Explain
        ConsoleUI.WriteInfo("Copilot is explaining this issue...");
        Console.WriteLine();
        await copilotService.ExplainRecommendationAsync(rec);

        // Generate fix
        ConsoleUI.WriteInfo("Generating fix script...");
        Console.WriteLine();
        var fixScript = await copilotService.GenerateFixScriptAsync(rec);
        rec.FixScript = fixScript;

        if (fixScript is not null)
        {
            // Ask permission
            var action = ConsoleUI.PromptConfirm("Execute this fix?");
            switch (action)
            {
                case "yes":
                    ConsoleUI.WriteInfo("Executing fix...");
                    var result = await copilotService.ExecuteFixAsync(fixScript);
                    rec.IsFixed = true;
                    ConsoleUI.WriteSuccess("Fix applied.");
                    break;
                case "skip":
                    rec.IsSkipped = true;
                    ConsoleUI.WriteWarning("Skipped.");
                    break;
                case "abort":
                    ConsoleUI.WriteWarning("Aborting remaining fixes.");
                    goto FixComplete;
            }
        }
        else
        {
            ConsoleUI.WriteWarning("Could not generate a fix script for this recommendation.");
            rec.IsSkipped = true;
        }
    }
}
else
{
    // Batch mode
    ConsoleUI.WriteHeader("Batch Fix Mode");
    ConsoleUI.WriteInfo("Generating fix scripts for all recommendations...");

    foreach (var rec in recommendations)
    {
        Console.WriteLine();
        ConsoleUI.WriteInfo($"Generating fix for: {rec.Title}");
        var fixScript = await copilotService.GenerateFixScriptAsync(rec);
        rec.FixScript = fixScript;
    }

    // Show summary
    ConsoleUI.WriteHeader("Fix Scripts Summary");
    foreach (var rec in recommendations)
    {
        Console.WriteLine($"  • {rec.Title}");
        if (rec.FixScript is not null)
        {
            ConsoleUI.WriteSql(rec.FixScript);
        }
        else
        {
            ConsoleUI.WriteWarning("  No fix script generated.");
        }
    }

    var action = ConsoleUI.PromptConfirm("Execute ALL fix scripts?");
    if (action == "yes")
    {
        foreach (var rec in recommendations.Where(r => r.FixScript is not null))
        {
            ConsoleUI.WriteInfo($"Executing: {rec.Title}");
            try
            {
                await copilotService.ExecuteFixAsync(rec.FixScript!);
                rec.IsFixed = true;
                ConsoleUI.WriteSuccess("Done.");
            }
            catch (Exception ex)
            {
                rec.Error = ex.Message;
                ConsoleUI.WriteError($"Failed: {ex.Message}");
            }
        }
    }
    else
    {
        ConsoleUI.WriteWarning("Execution cancelled.");
    }
}

FixComplete:

// ── Step 8: Summary Report ──
ConsoleUI.WriteHeader("Summary Report");

var fixedCount = recommendations.Count(r => r.IsFixed);
var skippedCount = recommendations.Count(r => r.IsSkipped);
var errorCount = recommendations.Count(r => r.Error is not null);
var pendingCount = recommendations.Count - fixedCount - skippedCount - errorCount;

ConsoleUI.WriteSuccess($"Fixed:   {fixedCount}");
ConsoleUI.WriteWarning($"Skipped: {skippedCount}");
ConsoleUI.WriteError($"Errors:  {errorCount}");
if (pendingCount > 0)
    ConsoleUI.WriteInfo($"Pending: {pendingCount}");

Console.WriteLine();
ConsoleUI.WriteInfo("Done. Thank you for using SQL Performance & Security Agent!");

await copilotService.DisposeAsync();
return 0;
}
catch (QuitException)
{
    Console.WriteLine();
    ConsoleUI.WriteInfo("Goodbye! Thank you for using SQL Performance & Security Agent!");
    return 0;
}

// ── Helpers ──
static List<string> WordWrap(string text, int maxWidth)
{
    var lines = new List<string>();
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var current = new System.Text.StringBuilder();
    foreach (var word in words)
    {
        if (current.Length > 0 && current.Length + 1 + word.Length > maxWidth)
        {
            lines.Add(current.ToString());
            current.Clear();
        }
        if (current.Length > 0) current.Append(' ');
        current.Append(word);
    }
    if (current.Length > 0) lines.Add(current.ToString());
    return lines;
}