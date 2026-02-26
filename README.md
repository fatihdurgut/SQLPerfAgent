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
┌──────────────┐                          │  + Tiger     │
│ Recommended  │ ◄── Fix Scripts ──────── │   Toolbox    │
│    Fixes     │                          │              │
└──────────────┘                          └──────────────┘
```

**Quick links:** [Features](#features) • [Getting Started](#getting-started) • [Usage](#usage) • [Scan Modes](#scan-modes)

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

**Tiger Toolbox Integration**

SQLPerfAgent integrates battle-tested diagnostic scripts from [Microsoft's Tiger Toolbox](https://github.com/microsoft/tigertoolbox):

- **Best Practices Checks** — Backup status, MaxDOP configuration, memory pressure, deprecated features
- **VLF Analysis** — Virtual Log File counts and performance impact assessment
- **TempDB Configuration** — File count, size equality, autogrow settings validation
- **Advanced Index Analysis** — Duplicate and redundant index detection

### AI-Powered Insights

- **Natural language explanations** of complex performance issues
- **Contextual fix generation** with safety checks and rollback instructions
- **Interactive Q&A mode** for asking free-form questions about database performance
- **Pattern recognition** across multiple diagnostic results
- **Production-safe recommendations** following SQL Server best practices

### Developer Experience

- **Interactive CLI workflow** with guided prompts
- **Three scan modes** (Quick, Deep, Tiger) for different analysis depths
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

## Scan Modes

Choose the analysis depth that fits your needs:

### Quick Scan (30-60 seconds)

Fast diagnostic using standard DMV queries. Ideal for daily health checks.

**Checks performed:**
- Missing indexes with impact scores
- Index fragmentation levels
- Top expensive queries
- Unused indexes

### Deep Scan (1-2 minutes)

Comprehensive analysis including Tiger Toolbox best practices. Perfect for weekly or monthly reviews.

**Everything in Quick Scan, plus:**
- Database backup status
- Memory pressure indicators
- MaxDOP configuration
- VLF counts
- TempDB configuration
- Instant File Initialization status
- Deprecated feature usage

### Tiger Mode (2-5 minutes)

Most thorough diagnostic available. Use for major performance investigations or before production deployments.

**Everything in Deep Scan, plus:**
- Duplicate index detection
- Redundant index analysis
- Advanced index pattern analysis

> [!TIP]
> Start with Quick Scan for routine monitoring. Use Deep Scan monthly, and Tiger Mode when investigating specific performance issues.

## Usage

### Workflow Overview

The agent guides you through an interactive workflow with optional Q&A:

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
│  │ SQL Query    │  │ Tiger Toolbox│  │ Copilot Fix     │   │
│  │ Service      │  │ Service      │  │ Service         │   │
│  └──────┬───────┘  └──────┬───────┘  └────────┬────────┘   │
│         │                 │                   │            │
└─────────┼─────────────────┼───────────────────┼────────────┘
          │                 │                   │
          ▼                 ▼                   ▼
    ┌──────────┐      ┌──────────┐      ┌──────────────┐
    │   SQL    │      │  Tiger   │      │   GitHub     │
    │  Server  │      │ Toolbox  │      │   Copilot    │
    │   DMVs   │      │ Scripts  │      │   SDK        │
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
| **TigerToolboxService** | Runs Microsoft Tiger Toolbox diagnostic scripts |
| **CopilotFixService** | Manages GitHub Copilot SDK session for AI analysis |
| **ConsoleUI** | Provides interactive command-line interface |
| **mssql-mcp** | MCP server enabling Copilot to execute SQL queries |

### Project Structure

```
SQLPerfAgent/
├── Program.cs                      # Main workflow orchestration
├── Models/
│   ├── Recommendation.cs           # Data models and DTOs
│   └── TolerantEnumConverter.cs    # Graceful JSON parsing with error tolerance
├── Services/
│   ├── CopilotFixService.cs        # GitHub Copilot integration
│   ├── SqlQueryService.cs          # DMV query execution
│   └── TigerToolboxService.cs      # Tiger Toolbox integration
├── TigerToolbox/
│   ├── BestPracticesChecks.sql     # Best practices diagnostics
│   ├── VLFCheck.sql                # VLF analysis
│   ├── TempDBChecks.sql            # TempDB validation
│   └── DuplicateIndexes.sql        # Advanced index analysis
└── UI/
    └── ConsoleUI.cs                # Console interaction helpers
```

## How It Works

1. **Data Collection**
   - Connects to SQL Server using Microsoft.Data.SqlClient
   - Executes DMV queries for standard diagnostics
   - Runs Tiger Toolbox scripts for deep analysis (optional)

2. **AI Analysis**
   - Raw results sent to GitHub Copilot via SDK
   - Copilot analyzes patterns and identifies issues
   - Generates structured recommendations with severity levels

3. **Fix Generation**
   - For each recommendation, Copilot generates T-SQL scripts
   - Scripts include safety checks, comments, and rollback instructions
   - Follows SQL Server best practices (ONLINE operations, MAXDOP settings)

4. **Execution**
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
