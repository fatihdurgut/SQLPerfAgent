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
                    - Rebuild or reorganize indexes with >30% fragmentation
                    
                    ### Anti-Patterns to Flag
                    - SELECT * in production queries
                    - Functions wrapping indexed columns in WHERE clauses
                    - Implicit conversions causing index scans
                    - Missing indexes on foreign key columns
                    - OR conditions that could be rewritten as UNION ALL
                    - N+1 query patterns
                    - Large OFFSET pagination
                    - Unbounded result sets (missing TOP/LIMIT)
                    
                    ### Fix Script Guidelines
                    - Always include comments explaining what each part does
                    - Include safety checks (IF EXISTS, etc.)
                    - Never drop data without explicit confirmation
                    - Prefer non-blocking operations when possible (ONLINE = ON for index operations)
                    - Include rollback instructions when applicable
                    - Test with realistic data volumes
                    
                    Use the mssql MCP server tools to query the database and execute scripts.
                    """,
                Mode = SystemMessageMode.Append
            }
        });
    }

    /// <summary>
    /// Asks Copilot to analyze pre-fetched DMV results and return structured recommendations.
    /// </summary>
    public async Task<string?> AnalyzeResultsAsync(string dmvResults, string? database)
    {
        ArgumentNullException.ThrowIfNull(_session);

        var dbScope = database is not null
            ? $"for database '{database}'"
            : "for all user databases on this instance";

        var prompt = $"""
            I have run DMV diagnostic queries against the SQL Server instance {dbScope}.
            Here are the raw results:

            {dmvResults}

            Analyze these results and create recommendations using the SQL performance best practices from your system instructions.
            
            Pay special attention to:
            - Missing indexes with high impact scores — suggest composite/covering indexes with proper column order
            - Fragmented indexes — recommend REBUILD (>30%) vs REORGANIZE (10-30%), prefer ONLINE operations
            - Expensive queries — identify anti-patterns (SELECT *, function calls in WHERE, implicit conversions, large OFFSET pagination, correlated subqueries)
            - Unused indexes — flag indexes with zero reads but high update overhead for removal
            
            For each finding, provide:
            - Category: Performance, Security, or Configuration
            - Severity: High, Medium, Low, or Informational
            - Title: Short description
            - Description: Detailed explanation of the issue, its impact, and the recommended fix approach
            - AffectedObject: The specific database object affected (table, index, query text snippet)
            - Source: "DMV"

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
