using System.Text.Json;
using GitHub.Copilot.SDK;
using SQLPerfAgent.Models;

namespace SQLPerfAgent.Services;

/// <summary>
/// Manages the Copilot SDK session with mssql-mcp for fetching recommendations and generating fixes.
/// </summary>
internal sealed class CopilotFixService : IAsyncDisposable
{
    private readonly CopilotClient _client;
    private CopilotSession? _session;
    private readonly SqlConnectionConfig _connectionConfig;

    public CopilotFixService(SqlConnectionConfig connectionConfig)
    {
        _client = new CopilotClient();
        _connectionConfig = connectionConfig;
    }

    /// <summary>
    /// Checks if the user has an active GitHub Copilot subscription.
    /// </summary>
    /// <returns>True if authenticated, false otherwise.</returns>
    public static async Task<bool> CheckAuthenticationAsync()
    {
        try
        {
            using var client = new CopilotClient();
            
            // Try to create a minimal session to verify authentication
            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = "Cloude Opus 4.6",
                Streaming = false
            });
            
            // If we get here, authentication succeeded
            return true;
        }
        catch (Exception)
        {
            // Authentication failed or no valid subscription
            return false;
        }
    }

    /// <summary>
    /// Initializes the Copilot session with mssql-mcp server attached.
    /// </summary>
    public async Task InitializeAsync()
    {
        var mssqlMcpEnv = _connectionConfig.ToMcpEnvVars();

        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = "Cloude Opus 4.6",
            Streaming = true,
            McpServers = new Dictionary<string, object>
            {
                ["mssql"] = new McpLocalServerConfig
                {
                    Command = "npx",
                    Args = ["-y", "mssql-mcp@latest"],
                    Env = mssqlMcpEnv
                }
            },
            SystemMessage = new SystemMessageConfig
            {
                Content = """
                    You are a SQL Server performance and security expert.
                    You help analyze SQL Server instances for performance issues, security vulnerabilities, and configuration problems.
                    
                    ## Extensible Toolbox Architecture
                    
                    This application uses a plugin-based toolbox system. Diagnostic tools are discovered from the Toolbox/ folder at startup.
                    Each tool has a tool.md file that describes what it checks and how to interpret results.
                    When analyzing results, the tool.md content for each tool will be provided as context — use it to understand
                    what each tool's output means, what severity to assign, and how to generate appropriate fix scripts.
                    
                    ## SQL Performance Best Practices
                    
                    ### Query Optimization
                    - Avoid SELECT * — always use explicit column selection
                    - Avoid functions in WHERE clauses that prevent index usage (e.g., YEAR(), UPPER())
                    - Use SARGable predicates: WHERE col >= '2024-01-01' instead of WHERE YEAR(col) = 2024
                    - Prefer INNER JOIN over implicit joins and use proper JOIN order
                    - Use EXISTS instead of IN for subqueries when appropriate
                    - Replace correlated subqueries with window functions or CTEs
                    - Use conditional aggregation instead of multiple separate queries
                    - Use cursor-based pagination instead of OFFSET for large datasets  
                    - Use batch INSERT/UPDATE/DELETE instead of row-by-row operations
                    
                    ### Index Strategy
                    - Create indexes on frequently filtered/joined/sorted columns
                    - Design composite indexes with most selective column first
                    - Use covering indexes (INCLUDE columns) to avoid key lookups
                    - Use partial/filtered indexes for specific query patterns
                    - Avoid over-indexing — each index adds INSERT/UPDATE/DELETE overhead
                    - Remove unused indexes (zero seeks/scans/lookups with high updates)
                    - Remove duplicate indexes (exact same key columns)
                    - Remove redundant indexes (one index makes another unnecessary)
                    - Rebuild or reorganize indexes with >30% fragmentation
                    - Avoid large index keys (>900 bytes) that cause performance issues
                    - Avoid low fill factor (<80%) unless specifically needed
                    
                    ### Anti-Patterns to Flag
                    - SELECT * in production queries
                    - Functions wrapping indexed columns in WHERE clauses
                    - Implicit conversions causing index scans
                    - Missing indexes on foreign key columns
                    - OR conditions that could be rewritten as UNION ALL
                    - N+1 query patterns
                    - Large OFFSET pagination
                    - Unbounded result sets (missing TOP/LIMIT)
                    - Duplicate or redundant indexes adding overhead
                    - Non-unique clustered indexes (anti-pattern)
                    - Clustered indexes with GUIDs causing excessive fragmentation
                    
                    ### Fix Script Guidelines
                    - Always include comments explaining what each part does
                    - Include safety checks (IF EXISTS, etc.)
                    - Never drop data without explicit confirmation
                    - Prefer non-blocking operations when possible (ONLINE = ON for index operations)
                    - Include rollback instructions when applicable
                    - Test with realistic data volumes
                    - For duplicate indexes, verify no hard-coded index hints exist before dropping
                    - For VLF fixes, schedule during maintenance windows (requires log shrink/regrow)
                    
                    Use the mssql MCP server tools to query the database and execute scripts.
                    """,
                Mode = SystemMessageMode.Append
            }
        });
    }

    /// <summary>
    /// Asks Copilot to analyze pre-fetched DMV results and return structured recommendations.
    /// Includes toolbox tool.md context so the AI understands each tool's output.
    /// </summary>
    public async Task<string?> AnalyzeResultsAsync(string dmvResults, string? database, List<ToolboxItem>? toolboxItems = null)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var dbScope = database is not null
            ? $"for database '{database}'"
            : "for all user databases on this instance";

        // Build toolbox context from tool.md files
        var toolboxContext = string.Empty;
        if (toolboxItems is not null && toolboxItems.Count > 0)
        {
            var toolContextBuilder = new System.Text.StringBuilder();
            toolContextBuilder.AppendLine();
            toolContextBuilder.AppendLine("## Toolbox Plugin Context");
            toolContextBuilder.AppendLine("The following toolbox tools were run. Use their documentation to interpret results:");
            toolContextBuilder.AppendLine();
            foreach (var tool in toolboxItems)
            {
                toolContextBuilder.AppendLine($"### {tool.Name}");
                toolContextBuilder.AppendLine(tool.ToolMdContent);
                toolContextBuilder.AppendLine();
            }
            toolboxContext = toolContextBuilder.ToString();
        }

        var prompt = $"""
            I have run diagnostic queries and toolbox checks against the SQL Server instance {dbScope}.
            Here are the raw results:

            {dmvResults}
            {toolboxContext}
            Analyze these results and create recommendations using the SQL performance best practices from your system instructions.
            Use the toolbox plugin context above to correctly interpret the output from each toolbox tool.
            
            Pay special attention to:
            - Missing indexes with high impact scores — suggest composite/covering indexes with proper column order
            - Fragmented indexes — recommend REBUILD (>30%) vs REORGANIZE (10-30%), prefer ONLINE operations
            - Expensive queries — identify anti-patterns (SELECT *, function calls in WHERE, implicit conversions, large OFFSET pagination, correlated subqueries)
            - Unused indexes — flag indexes with zero reads but high update overhead for removal
            - Any issues flagged by toolbox checks — use the tool.md interpretation guidance for severity and recommendations
            
            For each finding, provide:
            - Category: Performance, Security, or Configuration
            - Severity: High, Medium, Low, or Informational
            - Title: Short description
            - Description: Detailed explanation of the issue, its impact, and the recommended fix approach
            - AffectedObject: The specific database object affected (table, index, query text snippet)
            - Source: "DMV" or the toolbox tool name (e.g., "BestPracticesChecks", "VLFCheck", etc.)

            Format the results as a JSON array of objects with these fields.
            Return ONLY the JSON array, no markdown fencing, no extra text.
            If there are no findings, return an empty array: []
            """;

        return await SendAndWaitForResponseAsync(prompt, silent: true);
    }

    /// <summary>
    /// Asks Copilot to fetch recommendations from the connected SQL Server via MCP tools.
    /// </summary>
    public async Task<string?> FetchRecommendationsAsync(string? database)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var dbScope = database is not null
            ? $"for database '{database}'"
            : "for all user databases on this instance";

        var prompt = $"""
            Analyze the SQL Server instance {dbScope} and find recommendations.
            Use the mssql tools to query the following DMVs and return results:
            
            1. **Missing Indexes**: Query sys.dm_db_missing_index_details, sys.dm_db_missing_index_groups, sys.dm_db_missing_index_group_stats to find missing indexes with their impact score.
            2. **Index Fragmentation**: Query sys.dm_db_index_physical_stats for indexes with >30% fragmentation.
            3. **Expensive Queries**: Query sys.dm_exec_query_stats joined with sys.dm_exec_sql_text for top 10 queries by total CPU time.
            4. **Unused Indexes**: Find indexes with 0 user seeks/scans/lookups but high user updates from sys.dm_db_index_usage_stats.
            
            For each finding, provide:
            - Category: Performance, Security, or Configuration
            - Severity: High, Medium, Low, or Informational
            - Title: Short description
            - Description: Detailed explanation
            - AffectedObject: The database object affected (table, index, query)
            - Source: "DMV"
            
            Format the results as a JSON array of objects with these fields.
            Return ONLY the JSON array, no markdown fencing.
            """;

        return await SendAndWaitForResponseAsync(prompt, silent: true);
    }

    /// <summary>
    /// Asks Copilot to explain a recommendation.
    /// </summary>
    public async Task<string?> ExplainRecommendationAsync(Recommendation rec)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var prompt = $"""
            Explain this SQL Server recommendation in simple terms:
            
            **{rec.Title}**
            Category: {rec.Category}
            Severity: {rec.Severity}
            Affected Object: {rec.AffectedObject}
            Description: {rec.Description}
            Source: {rec.Source}
            
            Explain:
            1. What the issue is and why it matters
            2. The potential impact on performance or security
            3. What the fix involves
            
            Keep the explanation concise and practical.
            """;

        return await SendAndWaitForResponseAsync(prompt);
    }

    /// <summary>
    /// Asks Copilot to generate a fix script for a recommendation.
    /// </summary>
    public async Task<string?> GenerateFixScriptAsync(Recommendation rec)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var prompt = $"""
            Generate a SQL fix script for this recommendation:
            
            **{rec.Title}**
            Category: {rec.Category}
            Affected Object: {rec.AffectedObject}
            Description: {rec.Description}
            
            Requirements:
            - Use the mssql tools to check the current state first
            - Include safety checks (IF EXISTS, etc.)
            - Add comments explaining each step
            - Include rollback instructions as comments
            - Return ONLY the executable SQL script
            """;

        return await SendAndWaitForResponseAsync(prompt);
    }

    /// <summary>
    /// Asks Copilot to execute a fix script via mssql-mcp.
    /// </summary>
    public async Task<string?> ExecuteFixAsync(string sql)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var prompt = $"""
            Execute the following SQL script using the mssql tools.
            Report whether it succeeded or failed, and include any relevant output.
            
            ```sql
            {sql}
            ```
            """;

        return await SendAndWaitForResponseAsync(prompt);
    }

    /// <summary>
    /// Asks Copilot to suggest which toolbox tools to run based on the user's description of their problem.
    /// Returns the AI's response as plain text with tool name suggestions.
    /// </summary>
    public async Task<string?> SuggestToolsAsync(string userQuestion, List<ToolboxItem> availableTools, string? databaseContext = null)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var toolListBuilder = new System.Text.StringBuilder();
        foreach (var tool in availableTools)
        {
            toolListBuilder.AppendLine($"- **{tool.Name}**: {tool.Description}");
        }

        var contextNote = databaseContext is not null
            ? $"\nThe user is currently connected to database '{databaseContext}'."
            : "\nThe user is connected to a SQL Server instance (scanning all databases).";

        var prompt = $"""
            The user is deciding which diagnostic tools to run against their SQL Server.
            {contextNote}

            Available toolbox tools:
            {toolListBuilder}

            Note: Standard DMV queries (missing indexes, index fragmentation, expensive queries, unused indexes) always run automatically.

            The user says: "{userQuestion}"

            Based on their description, suggest which tools would be most helpful.
            For each suggested tool, briefly explain WHY it's relevant to their concern.
            If ALL tools are relevant, say so.
            If none of the toolbox tools are particularly relevant (their concern is covered by the standard DMV queries), explain that.

            Keep the response concise and practical. Use the exact tool names from the list above.
            """;

        return await SendAndWaitForResponseAsync(prompt);
    }

    /// <summary>
    /// Handles free-form questions about database performance and provides AI-powered answers.
    /// </summary>
    public async Task<string?> AskQuestionAsync(string question, string? databaseContext = null)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var contextNote = databaseContext is not null 
            ? $"\n\nContext: The user is currently analyzing database '{databaseContext}'." 
            : string.Empty;

        var prompt = $"""
            The user has a question about SQL Server performance, configuration, or diagnostics:
            
            "{question}"
            {contextNote}
            
            Please provide:
            1. A clear answer to their question
            2. Specific SQL queries or diagnostic checks they can run to investigate this (use mssql tools if appropriate)
            3. Actionable recommendations or best practices related to their question
            4. Any warnings or things to be aware of
            
            Use the mssql tools to query the database if it would help answer their question.
            Be specific, practical, and provide examples where helpful.
            """;

        return await SendAndWaitForResponseAsync(prompt);
    }

    /// <summary>
    /// Sends a prompt and waits for the complete response, optionally streaming to console.
    /// </summary>
    private async Task<string?> SendAndWaitForResponseAsync(string prompt, bool silent = false)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var done = new TaskCompletionSource();
        string? fullResponse = null;
        string? errorMessage = null;

        var subscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    if (!silent)
                        Console.Write(delta.Data.DeltaContent);
                    break;
                case AssistantMessageEvent msg:
                    fullResponse = msg.Data.Content;
                    break;
                case SessionErrorEvent error:
                    errorMessage = error.Data.Message;
                    Console.Error.WriteLine($"\n  Error: {error.Data.Message}");
                    done.TrySetResult();
                    break;
                case SessionIdleEvent:
                    done.TrySetResult();
                    break;
            }
        });

        try
        {
            await _session.SendAsync(new MessageOptions { Prompt = prompt });

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            cts.Token.Register(() => done.TrySetResult());
            await done.Task;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            subscription.Dispose();
        }

        Console.WriteLine();

        if (errorMessage is not null)
            throw new InvalidOperationException($"Copilot session error: {errorMessage}");

        return fullResponse;
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
            await _session.DisposeAsync();
        await _client.DisposeAsync();
    }
}
