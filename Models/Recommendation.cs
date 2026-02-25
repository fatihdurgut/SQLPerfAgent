namespace SQLPerfAgent.Models;

/// <summary>
/// Category of a SQL Server recommendation.
/// </summary>
public enum RecommendationCategory
{
    Performance,
    Security,
    Configuration
}

/// <summary>
/// Severity level for a recommendation.
/// </summary>
public enum RecommendationSeverity
{
    High,
    Medium,
    Low,
    Informational
}

/// <summary>
/// A single actionable recommendation from SQL Server analysis.
/// </summary>
public sealed record Recommendation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public required RecommendationCategory Category { get; init; }
    public required RecommendationSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string AffectedObject { get; init; }
    public string? FixScript { get; set; }
    public string? Source { get; init; } // "DMV", "TigerToolbox", or "AssessmentAPI"
    public bool IsFixed { get; set; }
    public bool IsSkipped { get; set; }
    public string? Error { get; set; }
    
    // Tiger Toolbox enhancements
    public string? ReferenceUrl { get; init; }
    public int SeverityScore { get; init; } // 1-10 scale
    public string? BaselineComparison { get; init; } // e.g., "40% worse than baseline"
    public string[]? RelatedChecks { get; init; }
}

/// <summary>
/// Connection configuration for SQL Server.
/// </summary>
public sealed record SqlConnectionConfig
{
    public required string Server { get; init; }
    public bool UseWindowsAuth { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Database { get; init; } // null = scan all user databases
    public bool ScanAllDatabases => Database is null;

    /// <summary>
    /// Builds a connection string for direct SQL Server access.
    /// </summary>
    public string ToConnectionString(string? overrideDatabase = null)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            TrustServerCertificate = true,
            ConnectTimeout = 10
        };

        if (UseWindowsAuth)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = Username ?? "";
            builder.Password = Password ?? "";
        }

        if (overrideDatabase is not null)
            builder.InitialCatalog = overrideDatabase;
        else if (!ScanAllDatabases)
            builder.InitialCatalog = Database!;

        return builder.ConnectionString;
    }

    /// <summary>
    /// Builds environment variables for mssql-mcp server configuration.
    /// </summary>
    public Dictionary<string, string> ToMcpEnvVars()
    {
        var env = new Dictionary<string, string>
        {
            ["DB_SERVER"] = Server,
            ["DB_TRUST_SERVER_CERTIFICATE"] = "true"
        };

        if (!UseWindowsAuth)
        {
            env["DB_USER"] = Username ?? "";
            env["DB_PASSWORD"] = Password ?? "";
        }

        if (!ScanAllDatabases)
        {
            env["DB_DATABASE"] = Database!;
        }

        return env;
    }
}
