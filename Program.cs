using System.Text.Json;
using SQLPerfAgent.Models;
using SQLPerfAgent.Services;
using SQLPerfAgent.UI;

// ── Banner ──
ConsoleUI.WriteBanner();

try
{
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

// ── Step 4: Fetch & Analyze (with retry loop) ──
List<Recommendation> recommendations = [];

while (true)
{
    ConsoleUI.WriteHeader("Fetching Recommendations");
    ConsoleUI.WriteInfo($"Querying DMVs{(database is not null ? $" on [{database}]" : " across all user databases")}...");

    // Run DMV queries directly using SqlClient (supports Windows Auth)
    var queryService = new SqlQueryService(connectionConfig);
    string dmvResults;
    try
    {
        dmvResults = await queryService.RunDiagnosticQueriesAsync(database);
        ConsoleUI.WriteSuccess($"DMV queries completed ({dmvResults.Split('\n').Length} lines of data).");
    }
    catch (Exception ex)
    {
        ConsoleUI.WriteError($"Failed to query SQL Server: {ex.Message}");
        return 1;
    }

    // Send results to Copilot for analysis
    ConsoleUI.WriteInfo("Sending results to Copilot for analysis...");
    Console.WriteLine();

    var originalOut = Console.Out;
    Console.SetOut(TextWriter.Null);
    string? recommendationsJson;
    try
    {
        recommendationsJson = await copilotService.AnalyzeResultsAsync(dmvResults, database);
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
                recommendations = JsonSerializer.Deserialize<List<Recommendation>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }) ?? [];
            }
        }
        catch (JsonException ex)
        {
            ConsoleUI.WriteWarning($"Could not parse structured recommendations: {ex.Message}");
        }
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