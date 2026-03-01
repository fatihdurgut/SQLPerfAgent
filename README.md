# SQL Performance Agent

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![GitHub Copilot SDK](https://img.shields.io/badge/GitHub_Copilot_SDK-0.1.26-black?style=flat-square&logo=github)](https://github.com/features/copilot)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

An AI-powered CLI tool that diagnoses SQL Server performance issues and generates actionable fix scripts using GitHub Copilot.

```
┌──────────────┐                          ┌──────────────┐
│  SQL Server  │ ◄── DMV Queries ───────  │              │
│              │ ──► Diagnostics ───────► │ SQLPerfAgent │
└──────────────┘                          │              │
                                          │  + Copilot   │
┌──────────────┐                          │  + Toolbox   │
│ Recommended  │ ◄── Fix Scripts ──────── │   Plugins    │
│    Fixes     │                          │              │
└──────────────┘                          └──────────────┘
```

**Quick links:** [Features](#features) • [Getting Started](#getting-started) • [Usage](#usage) • [Extensible Toolbox](#extensible-toolbox) • [Creating Your Own Tool](#creating-your-own-tool)

---

## What does it do?

SQLPerfAgent connects to your SQL Server instance, runs comprehensive diagnostic queries, and uses AI to analyze the results. It identifies performance bottlenecks, configuration issues, and security concerns — then generates ready-to-run T-SQL scripts to fix them.

Think of it as a DBA consultant in your terminal, powered by Microsoft's Tiger Toolbox diagnostics and GitHub Copilot's AI analysis.

## Features

### Diagnostic Capabilities

**Standard DMV Analysis**
- Missing index detection with impact scoring
- Index fragmentation analysis and recommendations
- Expensive query identification (CPU, I/O, memory)
- Unused index detection (reducing maintenance overhead)

**Extensible Toolbox (Plugin System)**

SQLPerfAgent ships with diagnostic scripts from [Microsoft's Tiger Toolbox](https://github.com/microsoft/tigertoolbox) and supports **user-created plugins**:

- **Best Practices Checks** — Backup status, MaxDOP configuration, memory pressure, deprecated features
- **VLF Analysis** — Virtual Log File counts and performance impact assessment
- **TempDB Configuration** — File count, size equality, autogrow settings validation
- **Duplicate Index Detection** — Duplicate and redundant index analysis
- **Your Own Tools** — Drop a subfolder with a `tool.md` and `.sql` files into `Toolbox/` to extend the agent

### AI-Powered Insights

- **Natural language explanations** of complex performance issues
- **Contextual fix generation** with safety checks and rollback instructions
- **Interactive Q&A mode** for asking free-form questions about database performance
- **Pattern recognition** across multiple diagnostic results
- **Production-safe recommendations** following SQL Server best practices

### Developer Experience

- **Interactive CLI workflow** with guided prompts
- **Auto-discovery of toolbox plugins** — all tools in `Toolbox/` run automatically
- **Interactive Q&A mode** for asking ad-hoc database questions
- **Flexible authentication** (Windows Auth or SQL Server Auth)
- **Multi-database scanning** capability
- **Two fix modes** (one-by-one review or batch execution)

## Prerequisites

| Requirement | Purpose |
|-------------|---------|
| [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Runtime for the CLI application |
| [GitHub Copilot](https://github.com/features/copilot) subscription | AI-powered analysis and fix generation |
| [Node.js](https://nodejs.org/) | Required for mssql-mcp server (launched automatically) |
| SQL Server instance | Target database server (2016+ recommended) |

> [!NOTE]
> The tool supports both on-premises SQL Server and Azure SQL Database. Windows Authentication and SQL Server Authentication are both supported.

> [!IMPORTANT]
> You must be authenticated with GitHub Copilot before running the tool. The application will verify your authentication at startup and guide you through the authentication process if needed.

### Authentication Setup

If you haven't authenticated with GitHub yet:

1. **Install GitHub CLI** (if not already installed)
   ```bash
   # Windows (via winget)
   winget install GitHub.cli
   
   # macOS
   brew install gh
   ```

2. **Authenticate with GitHub**
   ```bash
   gh auth login
   ```

3. **Verify you have Copilot access**
   - Visit [GitHub Copilot](https://github.com/features/copilot)
   - Ensure your subscription is active

## Getting Started

### Installation

1. **Clone the repository**

   ```bash
   git clone https://github.com/fatihdurgut/SQLPerfAgent.git
   cd SQLPerfAgent
   ```

2. **Build the project**

   ```bash
   dotnet build
   ```

3. **Run the agent**

   ```bash
   dotnet run
   ```

### Quick Start Example

```bash
$ dotnet run

── GitHub Copilot Authentication ──
  ℹ Verifying GitHub Copilot access...
  ✓ GitHub Copilot authentication verified.

── SQL Server Connection Setup ──
  Server [localhost]: myserver.database.windows.net
  Authentication method:
    [1] Windows Authentication
    [2] SQL Server Authentication  ← Select this
  Username [sa]: admin
  Password: ********

── Database Selection ──
  [1] ProductionDB
  [2] StagingDB
  [3] Scan all user databases

── Scan Mode Selection ──
  [1] Quick Scan (30-60s)
  [2] Deep Scan (1-2 min)
  [3] Tiger Mode (2-5 min)  ← Most comprehensive

🔍 Running comprehensive scan...
⚡ Found 5 recommendations

── Performance Issues ──
  [High]   Missing index on Orders.CustomerID (Impact: 15.2M)
  [Medium] Fragmented index IX_Products (72.3%)

Would you like to review and apply fixes? [y/n]
```

## Extensible Toolbox

SQLPerfAgent uses a **plugin-based toolbox system**. At startup, it automatically discovers all tools in the `Toolbox/` folder and runs them alongside the built-in DMV queries.

### Built-in Tools

The following tools ship with SQLPerfAgent (from Microsoft's Tiger Toolbox):

| Tool | Description |
|------|-------------|
| **BestPracticesChecks** | Memory pressure, backup status, MaxDOP, IFI, deprecated features |
| **VLFCheck** | Virtual Log File count analysis across all databases |
| **TempDBChecks** | TempDB file count, size equality, autogrow settings |
| **DuplicateIndexes** | Duplicate and redundant index detection |

### How It Works

1. At startup, SQLPerfAgent scans `Toolbox/` for subfolders
2. Each subfolder with a `tool.md` file and at least one `.sql` file is a valid tool
3. All discovered tools are displayed and auto-run against the target database
4. The `tool.md` content is passed to the AI as context, so Copilot knows how to interpret each tool's output
5. Results from all tools are combined and analyzed together

> [!TIP]
> You can add or remove tools simply by adding or removing subfolders in `Toolbox/`. No code changes needed!

## Creating Your Own Tool

Extend SQLPerfAgent with your own diagnostic checks:

### 1. Create a subfolder

```
Toolbox/
└── MyCustomCheck/
    ├── tool.md          # Required: describes the tool to both humans and AI
    └── MyCheck.sql      # Required: one or more .sql files
```

### 2. Write a `tool.md`

The `tool.md` file serves dual purpose — it's both **documentation for users** and **prompt context for the AI**. Write it in Markdown with these sections:

```markdown
# My Custom Check

Brief description of what this tool does.

## Scripts

Run `MyCheck.sql` as a single execution (or "using GO batch separation" if needed).

## What It Checks

- Describe each check the SQL performs
- Include thresholds and expected values

## Interpretation

- Explain how to interpret the results
- Specify severity levels for different findings
- Include remediation guidance
```

**Key conventions:**
- If your tool.md mentions **"GO batch separation"**, the SQL will be split by `GO` statements and executed as multiple batches
- Script execution order is determined by the order `.sql` filenames appear in the tool.md. Alphabetical fallback if not mentioned.
- The AI reads the entire `tool.md` content, so write it as if you're explaining the tool to a DBA expert

### 3. Write your SQL script(s)

Your SQL scripts should:
- Return result sets with descriptive column names
- Include a `Status` or `Recommendation` column when possible
- Work against SQL Server 2016+ (for broadest compatibility)
- Handle edge cases gracefully (use `TRY`/`CATCH` or `IF EXISTS` checks)

### Example: Custom Wait Stats Tool

**`Toolbox/WaitStats/tool.md`:**
```markdown
# Wait Statistics Analysis

Analyzes SQL Server wait statistics to identify performance bottlenecks.

## Scripts

Run `WaitStats.sql` as a single execution.

## What It Checks

- Top 10 wait types by total wait time (excluding benign waits)
- Identifies I/O, CPU, memory, and locking bottlenecks

## Interpretation

- PAGEIOLATCH waits indicate disk I/O bottleneck — check for missing indexes or slow storage
- CXPACKET waits indicate parallelism issues — review MaxDOP settings
- LCK waits indicate blocking — review query patterns and isolation levels
- High severity if any single wait type exceeds 40% of total waits
```

**`Toolbox/WaitStats/WaitStats.sql`:**
```sql
SELECT TOP 10
    wait_type AS WaitType,
    waiting_tasks_count AS WaitCount,
    wait_time_ms / 1000.0 AS WaitTimeSec,
    CAST(100.0 * wait_time_ms / SUM(wait_time_ms) OVER() AS DECIMAL(5,1)) AS PctOfTotal
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN ('SLEEP_TASK', 'BROKER_IO_FLUSH', ...)
ORDER BY wait_time_ms DESC;
```

## Usage

### Workflow Overview

The agent guides you through an interactive workflow with optional Q&A:

#### 0. Authentication Verification

The application automatically verifies your GitHub Copilot authentication at startup:

- ✓ If authenticated: Proceeds to SQL Server connection
- ✗ If not authenticated: Displays authentication instructions and exits

#### 1. Connection Setup

Connect to your SQL Server using Windows or SQL Server authentication:

```
Server: myserver.database.windows.net
Authentication: SQL Server Authentication
Database: ProductionDB (or scan all databases)
```

#### 2. Scan Execution

Choose your scan mode and let the agent analyze your database:

- DMV queries run automatically
- Tiger Toolbox scripts execute (Deep/Tiger modes)
- Results sent to GitHub Copilot for AI analysis

#### 3. Review Recommendations

View categorized findings with severity indicators:

```
── Found 5 Recommendation(s) ──

  ── Performance ──
    1. [High]   Missing index on Orders.CustomerID
       Object: dbo.Orders
       Impact: 15.2M seeks could benefit from this index

    2. [Medium] Fragmented index IX_Products_Name (72.3%)
       Object: dbo.Products
       Recommendation: REBUILD for >30% fragmentation

  ── Configuration ──
    3. [Warning] TempDB has 2 files, but server has 8 CPUs
       Recommendation: Add 6 more TempDB files

  ── Best Practices ──
    4. [Info]   Instant File Initialization is disabled
       Impact: Slower database growth operations
```

#### 4. Interactive Q&A (Optional)

Ask free-form questions about your database performance, configuration, or SQL Server topics. The AI will:

- Provide detailed answers with context
- Suggest specific diagnostic queries to run
- Execute queries using mssql tools when helpful
- Offer actionable recommendations and best practices

**Example Q&A session:**

```bash
── Interactive Q&A Mode ──
  ℹ Ask questions about your database performance, configuration, or any SQL Server topic.
  ℹ Type 'done' when you're ready to proceed to fix mode, or 'quit' to exit.

  Q: Why is my database experiencing slow reads during peak hours?

  A: Let me analyze your database's read performance patterns...
     
     Based on the query statistics, I can see:
     
     1. **High Page Life Expectancy Issues**
        Your buffer pool is showing signs of memory pressure. Current PLE is 180 seconds,
        which is below the recommended 300 seconds for your workload.
     
     2. **Missing Indexes Detected**
        Several frequently executed queries are performing index scans instead of seeks:
        - Orders table: CustomerID column (15.2M seeks could benefit)
        - Products table: CategoryID + Price (8.3M seeks)
     
     3. **Recommendations**
        - Add the missing indexes identified above
        - Consider increasing max server memory if physical memory is available
        - Review top 10 queries by I/O to identify optimization opportunities
     
     Would you like me to run additional diagnostics on wait statistics?

  Q: What are the current wait statistics showing?

  A: Analyzing wait statistics for the last hour...
     
     [Executes query against sys.dm_os_wait_stats]
     
     Top wait types:
     1. PAGEIOLATCH_SH (42%) - Disk I/O waits reading data pages
     2. CXPACKET (18%) - Parallel query coordination
     3. WRITELOG (12%) - Transaction log writes
     
     **Analysis:**
     - PAGEIOLATCH_SH indicates I/O bottleneck - missing indexes or slow storage
     - CXPACKET waits suggest reviewing MaxDOP settings (currently: 0)
     
     **Next Steps:**
     1. Implement the missing indexes
     2. Set MaxDOP to 8 (current CPU count)
     3. Consider faster storage for data files if budget allows

  Q: done
  ✓ Exiting Q&A mode.
```

> [!TIP]
> Use Q&A mode to explore specific performance concerns, understand current findings, or learn about SQL Server concepts before applying fixes.

#### 5. Apply Fixes

Choose your fix strategy:

**One-by-one mode:**
- Copilot explains the issue in detail
- Generates a fix script with comments
- Shows the script for review
- Asks for confirmation before executing
- Provides rollback instructions if applicable

**Batch mode:**
- Generates all fix scripts at once
- Displays comprehensive summary
- Single confirmation to execute all
- Faster for multiple related issues

#### 6. Summary Report

View results of the fix execution:

```
── Summary Report ──
  Fixed:   3 recommendations applied successfully
  Skipped: 1 (manual review required)
  Errors:  0
  Pending: 1
```

### Fix Script Examples

**Missing Index Fix:**
```sql
-- Fix for: Missing index on Orders.CustomerID
-- Impact Score: 15,234,789
-- Generated by SQLPerfAgent + GitHub Copilot

-- Safety check
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID('dbo.Orders') 
    AND name = 'IX_Orders_CustomerID'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_CustomerID
    ON dbo.Orders (CustomerID)
    INCLUDE (OrderDate, TotalAmount)
    WITH (ONLINE = ON, MAXDOP = 4);
    
    PRINT 'Index created successfully';
END
ELSE
    PRINT 'Index already exists - no action needed';
GO

-- Rollback (if needed):
-- DROP INDEX IX_Orders_CustomerID ON dbo.Orders;
```

**Index Fragmentation Fix:**
```sql
-- Rebuild fragmented index: IX_Products_Name (72.3% fragmentation)
-- Recommended action: REBUILD (fragmentation >30%)

ALTER INDEX IX_Products_Name 
ON dbo.Products 
REBUILD 
WITH (
    ONLINE = ON,
    MAXDOP = 4,
    SORT_IN_TEMPDB = ON
);
```

> [!IMPORTANT]
> Always review generated scripts before applying them to production. While the agent includes safety checks and follows best practices, human verification is essential for production changes.

> [!WARNING]
> Some operations (like index rebuilds or TempDB modifications) may require maintenance windows. Plan accordingly.

## Architecture

SQLPerfAgent combines multiple technologies to deliver intelligent SQL Server diagnostics:

```
┌────────────────────────────────────────────────────────────┐
│                      SQLPerfAgent CLI                      │
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │ SQL Query    │  │  Toolbox     │  │ Copilot Fix     │   │
│  │ Service      │  │  Discovery & │  │ Service         │   │
│  │              │  │  Execution   │  │                 │   │
│  └──────┬───────┘  └──────┬───────┘  └────────┬────────┘   │
│         │                 │                   │            │
└─────────┼─────────────────┼───────────────────┼────────────┘
          │                 │                   │
          ▼                 ▼                   ▼
    ┌──────────┐      ┌──────────┐      ┌──────────────┐
    │   SQL    │      │ Toolbox/ │      │   GitHub     │
    │  Server  │      │ Plugins  │      │   Copilot    │
    │   DMVs   │      │(tool.md +│      │   SDK        │
    │          │      │ *.sql)   │      │              │
    └──────────┘      └──────────┘      └──────┬───────┘
                                               │
                                               ▼
                                        ┌──────────────┐
                                        │  mssql-mcp   │
                                        │   Server     │
                                        └──────────────┘
```

### Key Components

| Component | Purpose |
|-----------|---------|
| **SqlQueryService** | Executes DMV diagnostic queries using Microsoft.Data.SqlClient |
| **ToolboxDiscoveryService** | Discovers toolbox plugins from the Toolbox/ folder at startup |
| **ToolboxExecutionService** | Runs discovered toolbox SQL scripts against SQL Server |
| **CopilotFixService** | Manages GitHub Copilot SDK session for AI analysis |
| **ConsoleUI** | Provides interactive command-line interface |
| **mssql-mcp** | MCP server enabling Copilot to execute SQL queries |

### Project Structure

```
SQLPerfAgent/
├── Program.cs                      # Main workflow orchestration
├── Models/
│   ├── Recommendation.cs           # Data models and DTOs
│   ├── ToolboxItem.cs              # Toolbox plugin model
│   └── TolerantEnumConverter.cs    # Graceful JSON parsing with error tolerance
├── Services/
│   ├── CopilotFixService.cs        # GitHub Copilot integration
│   ├── SqlQueryService.cs          # DMV query execution
│   ├── ToolboxDiscoveryService.cs  # Plugin discovery from Toolbox/ folder
│   └── ToolboxExecutionService.cs  # Generic SQL script execution engine
├── Toolbox/                        # Extensible plugin folder
│   ├── BestPracticesChecks/        # Each subfolder is a tool
│   │   ├── tool.md                 # Tool manifest & AI context
│   │   └── BestPracticesChecks.sql
│   ├── VLFCheck/
│   │   ├── tool.md
│   │   └── VLFCheck.sql
│   ├── TempDBChecks/
│   │   ├── tool.md
│   │   └── TempDBChecks.sql
│   └── DuplicateIndexes/
│       ├── tool.md
│       └── DuplicateIndexes.sql
└── UI/
    └── ConsoleUI.cs                # Console interaction helpers
```

## How It Works

1. **Plugin Discovery**
   - Scans `Toolbox/` folder for subfolders with `tool.md` + `.sql` files
   - Reads each `tool.md` for execution instructions and AI context
   - Displays discovered tools to the user

2. **Data Collection**
   - Connects to SQL Server using Microsoft.Data.SqlClient
   - Executes built-in DMV queries for standard diagnostics
   - Runs all discovered toolbox plugins automatically

3. **AI Analysis**
   - Raw results + tool.md context sent to GitHub Copilot via SDK
   - Copilot uses tool documentation to interpret each tool's output
   - Generates structured recommendations with severity levels

4. **Fix Generation**
   - For each recommendation, Copilot generates T-SQL scripts
   - Scripts include safety checks, comments, and rollback instructions
   - Follows SQL Server best practices (ONLINE operations, MAXDOP settings)

5. **Execution**
   - User reviews generated scripts
   - Scripts execute via mssql-mcp server
   - Results tracked and summarized

## Resources

**Documentation:**
- [GitHub Copilot SDK](https://github.com/features/copilot)
- [Microsoft Tiger Toolbox](https://github.com/microsoft/tigertoolbox)
- [SQL Server DMVs](https://learn.microsoft.com/sql/relational-databases/system-dynamic-management-views/)
- [mssql-mcp on npm](https://www.npmjs.com/package/mssql-mcp)

**Related Projects:**
- [BPCheck](https://github.com/microsoft/tigertoolbox/tree/master/BPCheck) - SQL Server best practices checker
- [sp_Blitz](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) - Another popular SQL Server diagnostic tool
