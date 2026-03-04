<!-- prettier-ignore -->
<div align="center">

# SQL Performance Agent

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![GitHub Copilot SDK](https://img.shields.io/badge/GitHub_Copilot_SDK-0.1.26-black?style=flat-square&logo=github)](https://github.com/features/copilot)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

An AI-powered CLI tool that diagnoses SQL Server performance issues and generates actionable fix scripts — powered by GitHub Copilot.

[Features](#features) • [Getting Started](#getting-started) • [Usage](#usage) • [Extensible Toolbox](#extensible-toolbox) • [Creating Your Own Tool](#creating-your-own-tool) • [Architecture](#architecture) • [Documentation](docs/README.md)

</div>

---

SQLPerfAgent connects to your SQL Server instance, runs diagnostic DMV queries and extensible toolbox plugins, then uses GitHub Copilot to analyze the results. It identifies performance bottlenecks, configuration issues, and security concerns — and generates ready-to-run T-SQL scripts to fix them.

```
┌──────────────┐                          ┌──────────────┐
│  SQL Server  │ ◄── DMV Queries ───────  │              │
│              │ ──► Diagnostics ───────► │ SQLPerfAgent │
└──────────────┘                          │              │
                                          │  + Copilot   │
┌──────────────┐                          │  + Toolbox   │
│  Actionable  │ ◄── Fix Scripts ──────── │    Plugins   │
│    Fixes     │                          │              │
└──────────────┘                          └──────────────┘
```

## Features

**Diagnostics**
- Missing index detection with impact scoring
- Index fragmentation analysis and rebuild/reorganize recommendations
- Expensive query identification (CPU, I/O, memory)
- Unused index detection with write overhead analysis
- Extensible toolbox plugins for additional checks (TempDB, VLFs, duplicate indexes, best practices, and more)

**AI-Powered Analysis**
- Natural language explanations of complex performance issues
- Contextual fix generation with safety checks and rollback instructions
- Interactive Q&A mode for free-form questions about your database
- AI-assisted tool selection — describe your problem and Copilot suggests which diagnostics to run

**Interactive CLI**
- Guided connection setup with Windows Auth and SQL Auth support
- Multi-database scanning capability
- Four tool selection modes: run all, manual pick, AI suggestions, or skip
- One-by-one or batch fix application with confirmation prompts

## Prerequisites

| Requirement | Purpose |
|---|---|
| [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Build and run the application |
| [GitHub Copilot](https://github.com/features/copilot) subscription | AI analysis and fix generation |
| [GitHub CLI](https://cli.github.com/) | Authentication with GitHub Copilot |
| [Node.js 18+](https://nodejs.org/) | Required for the mssql-mcp server (launched automatically via `npx`) |
| SQL Server instance | Target database (2016+ recommended) |

> [!NOTE]
> Both on-premises SQL Server and Azure SQL Database are supported. The tool validates your GitHub Copilot authentication at startup and provides setup instructions if needed.

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/fatihdurgut/SQLPerfAgent.git
   cd SQLPerfAgent
   ```

2. **Build the project**

   ```bash
   dotnet build
   ```

3. **Authenticate with GitHub** (if not already)

   ```bash
   gh auth login
   ```

4. **Run the agent**

   ```bash
   dotnet run
   ```

## Usage

The agent walks you through an interactive workflow:

### 1. Connect to SQL Server

```
── SQL Server Connection Setup ──
  Server [localhost]: myserver.database.windows.net
  Authentication method:
    [1] Windows Authentication
    [2] SQL Server Authentication
  Username [sa]: admin
  Password: ********

── Database Selection ──
  [1] ProductionDB
  [2] StagingDB
  [3] Scan all user databases
```

### 2. Choose which tools to run

After toolbox discovery, you pick how to select diagnostics:

```
── Tool Selection ──
  Which additional toolbox checks would you like to run?
    [1] Run all toolbox tools
    [2] Let me pick specific tools
    [3] Describe my problem — get AI suggestions
    [4] Skip toolbox tools (DMV queries only)
```

Option **3** opens an AI-assisted conversation — describe your concern and Copilot recommends the most relevant tools:

```
  Describe your concern: My database is experiencing slow writes during peak hours

  Based on your description, I recommend:
  • VLFCheck — high VLF counts directly impact write performance
  • TempDBChecks — TempDB contention causes write bottlenecks under concurrency
  • BestPracticesChecks — MaxDOP and memory settings affect write throughput

  Suggested so far: VLFCheck, TempDBChecks, BestPracticesChecks
  Ask another question, or type 'done' to proceed.
```

> [!TIP]
> Standard DMV queries (missing indexes, fragmentation, expensive queries, unused indexes) always run regardless of tool selection.

### 3. Review recommendations

The AI analyzes combined results and returns categorized findings:

```
── Found 5 Recommendation(s) ──

  ── Performance ──
    1. [High]   Missing index on Orders.CustomerID (Impact: 15.2M)
    2. [Medium] Fragmented index IX_Products_Name (72.3%)

  ── Configuration ──
    3. [Medium] TempDB has 2 files, but server has 8 CPUs
    4. [Low]    Percentage-based autogrow on TempDB

  ── Security ──
    5. [High]   No full backup for ProductionDB in 14 days
```

### 4. Ask questions (optional)

Enter interactive Q&A mode to explore specific concerns before applying fixes:

```
  Q: Why is my buffer pool showing memory pressure?
  A: Your Page Life Expectancy is 180 seconds, below the recommended 300...
     [Runs additional diagnostics via mssql-mcp]

  Q: done
```

### 5. Apply fixes

Choose **one-by-one** (review each script individually) or **batch** mode (generate all, review, then execute):

```sql
-- Fix: Missing index on Orders.CustomerID
-- Impact Score: 15,234,789
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
END
GO
-- Rollback: DROP INDEX IX_Orders_CustomerID ON dbo.Orders;
```

> [!IMPORTANT]
> Always review generated scripts before applying to production. While the agent includes safety checks and follows best practices, human verification is essential.

## Extensible Toolbox

SQLPerfAgent uses a plugin-based toolbox system. At startup, it scans the `Toolbox/` folder for subfolders containing a `tool.md` manifest and one or more `.sql` scripts.

### Built-in tools

| Tool | Description |
|---|---|
| **BestPracticesChecks** | Memory pressure, backup status, MaxDOP, instant file initialization, deprecated features |
| **VLFCheck** | Virtual Log File count analysis across all databases |
| **TempDBChecks** | File count, size equality, autogrow settings, file location validation |
| **DuplicateIndexes** | Exact duplicate, duplicate key, and redundant index detection |

These are sourced from [Microsoft's Tiger Toolbox](https://github.com/microsoft/tigertoolbox), adapted for the plugin format.

### How discovery works

1. Scans `Toolbox/` for subfolders with a `tool.md` + at least one `.sql` file
2. Reads `tool.md` to determine script execution order and batch separation mode
3. Passes `tool.md` content to the AI as context so Copilot knows how to interpret each tool's output
4. Displays all discovered tools for the user to select from

> [!TIP]
> Add or remove tools by adding or removing subfolders in `Toolbox/`. No code changes required.

## Creating Your Own Tool

### 1. Create a subfolder in `Toolbox/`

```
Toolbox/
└── WaitStats/
    ├── tool.md          # Required: manifest & AI context
    └── WaitStats.sql    # Required: one or more .sql files
```

### 2. Write `tool.md`

This file serves dual purpose — documentation for users **and** prompt context for the AI:

```markdown
# Wait Statistics Analysis

Analyzes SQL Server wait statistics to identify performance bottlenecks.

## Scripts

Run `WaitStats.sql` as a single execution.

## What It Checks

- Top 10 wait types by total wait time (excluding benign waits)
- Identifies I/O, CPU, memory, and locking bottlenecks

## Interpretation

- PAGEIOLATCH waits → disk I/O bottleneck, check for missing indexes
- CXPACKET waits → parallelism issues, review MaxDOP settings
- High severity if any single wait type exceeds 40% of total waits
```

**Conventions:**
- Include `"GO batch separation"` in the text if your script uses `GO` statements between batches
- Script execution order follows the order `.sql` filenames appear in the markdown (alphabetical fallback)
- Write the interpretation section as if explaining to a DBA — the AI uses it directly

### 3. Write your SQL script

```sql
SELECT TOP 10
    wait_type AS WaitType,
    waiting_tasks_count AS WaitCount,
    wait_time_ms / 1000.0 AS WaitTimeSec,
    CAST(100.0 * wait_time_ms / SUM(wait_time_ms) OVER() AS DECIMAL(5,1)) AS PctOfTotal
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN ('SLEEP_TASK', 'BROKER_IO_FLUSH', 'WAITFOR')
ORDER BY wait_time_ms DESC;
```

Scripts should return result sets with descriptive column names. Include `Status` or `Recommendation` columns when possible for best AI interpretation.

## Architecture

```
┌────────────────────────────────────────────────────────────┐
│                      SQLPerfAgent CLI                      │
│                                                            │
│  ┌───────────────┐  ┌─────────────────┐  ┌──────────────┐  │
│  │ SqlQuery      │  │ Toolbox         │  │ CopilotFix   │  │
│  │ Service       │  │ Discovery &     │  │ Service      │  │
│  │ (DMV queries) │  │ Execution       │  │ (AI + MCP)   │  │
│  └───────┬───────┘  └────────┬────────┘  └──────┬───────┘  │
└──────────┼───────────────────┼──────────────────┼──────────┘
           │                   │                  │
           ▼                   ▼                  ▼
     ┌──────────┐       ┌──────────┐       ┌──────────────┐
     │   SQL    │       │ Toolbox/ │       │   GitHub     │
     │  Server  │       │ Plugins  │       │   Copilot    │
     └──────────┘       └──────────┘       │   SDK        │
                                           └──────┬───────┘
                                                  │
                                                  ▼
                                           ┌──────────────┐
                                           │  mssql-mcp   │
                                           │   Server     │
                                           └──────────────┘
```

For detailed architecture documentation — including project structure, component responsibilities, technology stack, data flow diagrams, and how the AI integration works — see the **[full documentation](docs/README.md#architecture)**.

## Documentation

The **[docs/](docs/README.md)** folder contains in-depth documentation:

- **[Problem & Solution](docs/README.md#problem-statement)** — Why this tool exists and how it works
- **[Deployment Guide](docs/README.md#deployment)** — Self-contained publish, framework-dependent deployment, Docker, and deployment checklist
- **[Architecture Deep Dive](docs/README.md#architecture)** — Component responsibilities, technology stack, AI integration pipeline, data flow diagrams
- **[Data Flow & Privacy](docs/README.md#data-flow)** — What data is sent where, credential handling
- **[Responsible AI Notes](docs/README.md#responsible-ai-rai-notes)** — Intended use, human oversight, limitations, transparency

## Resources

- [GitHub Copilot SDK](https://github.com/features/copilot) — AI engine powering the analysis
- [Microsoft Tiger Toolbox](https://github.com/microsoft/tigertoolbox) — Source for built-in diagnostic scripts
- [SQL Server DMV Reference](https://learn.microsoft.com/sql/relational-databases/system-dynamic-management-views/) — Dynamic Management Views documentation
- [mssql-mcp](https://www.npmjs.com/package/mssql-mcp) — MCP server for SQL Server connectivity
- [GitHub Copilot Privacy](https://docs.github.com/en/copilot/overview-of-github-copilot/about-github-copilot-individual#about-data-for-github-copilot) — Data handling policies
