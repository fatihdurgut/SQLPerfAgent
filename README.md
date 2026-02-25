<!-- prettier-ignore -->
<div align="center">

# SQL Performance & Security Agent

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![GitHub Copilot SDK](https://img.shields.io/badge/GitHub_Copilot_SDK-0.1.26-black?style=flat-square&logo=github)](https://github.com/features/copilot)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

An AI-powered CLI agent that analyzes SQL Server instances for performance issues, security vulnerabilities, and configuration problems — then generates and applies fixes using GitHub Copilot.

[Overview](#overview) • [Features](#features) • [Prerequisites](#prerequisites) • [Getting started](#getting-started) • [Usage](#usage)

</div>

## Overview

**SQLPerfAgent** connects directly to your SQL Server, queries Dynamic Management Views (DMVs) for diagnostics, and sends the results to a GitHub Copilot-powered agent for expert analysis. It produces actionable recommendations with ready-to-run fix scripts — all from your terminal.

The agent uses the [GitHub Copilot SDK](https://github.com/features/copilot) with an [mssql-mcp](https://www.npmjs.com/package/mssql-mcp) server to interact with your database, combining direct DMV queries with AI-driven analysis.

```
┌──────────────┐     DMV Queries     ┌──────────────┐
│              │ ──────────────────►  │              │
│  SQLPerfAgent│                      │  SQL Server  │
│   (CLI App)  │ ◄──────────────────  │              │
│              │   Raw diagnostics    └──────────────┘
│              │
│              │     Analysis via     ┌──────────────┐
│              │ ──────────────────►  │   GitHub     │
│              │                      │   Copilot    │
│              │ ◄──────────────────  │   + mssql-mcp│
└──────────────┘   Recommendations    └──────────────┘
```

## Features

- **Three scan modes** — Quick (DMVs only), Deep (DMVs + Tiger Toolbox), or Tiger Mode (comprehensive)
- **Tiger Toolbox integration** — Incorporates Microsoft's battle-tested SQL Server diagnostic tools
  - Best practices checks (backup status, MaxDOP, memory pressure, deprecated features)
  - VLF (Virtual Log File) analysis and recommendations
  - TempDB configuration verification
  - Duplicate and redundant index detection
- **Automated DMV diagnostics** — queries missing indexes, index fragmentation, expensive queries, and unused indexes
- **AI-powered analysis** — sends raw DMV data to GitHub Copilot for expert-level interpretation
- **Actionable fix scripts** — generates SQL scripts with safety checks, comments, and rollback instructions
- **Two fix modes** — review and apply fixes one-by-one, or batch all fixes together
- **Flexible authentication** — supports both Windows Authentication and SQL Server Authentication
- **Multi-database scanning** — scan a single database or all user databases on an instance
- **Interactive CLI** — guided prompts for connection setup, database selection, and fix approval

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [GitHub Copilot CLI](https://github.com/features/copilot) installed and available in PATH
- [Node.js](https://nodejs.org/) (for the mssql-mcp server, launched automatically via `npx`)
- Access to a SQL Server instance (local or remote)

> [!NOTE]
> A valid GitHub Copilot subscription is required to use the Copilot SDK features.

## Getting started

1. Clone the repository:
   ```bash
   git clone https://github.com/<your-org>/SQLPerfAgent.git
   cd SQLPerfAgent
   ```

2. Restore dependencies and build:
   ```bash
   dotnet build
   ```

3. Run the agent:
   ```bash
   dotnet run
   ```

## What's New: Tiger Toolbox Integration

SQLPerfAgent now integrates diagnostic scripts from [Microsoft Tiger Toolbox](https://github.com/microsoft/tigertoolbox), a collection of battle-tested SQL Server tools from Microsoft's Tiger Team:

### New Diagnostic Capabilities

- **VLF Analysis**: Detects excessive Virtual Log Files that impact transaction log performance
- **TempDB Configuration**: Validates file count, size equality, autogrow settings, and drive placement
- **Backup Verification**: Checks for missing or outdated full and log backups
- **Memory Pressure Detection**: Identifies when available physical memory is critically low
- **MaxDOP Configuration**: Validates max degree of parallelism against CPU count
- **Instant File Initialization**: Verifies IFI is enabled for faster data file operations
- **Deprecated Features**: Flags usage of features being removed in future SQL versions
- **Duplicate Index Detection**: Finds indexes with identical key columns adding unnecessary overhead
- **Redundant Index Detection**: Identifies indexes that make other indexes unnecessary

### Scan Modes Explained

**Quick Scan** (30-60 seconds)
- Standard DMV queries for common performance issues
- Best for routine health checks

**Deep Scan** (1-2 minutes)
- All Quick Scan checks
- Tiger Toolbox best practices checks
- Comprehensive system configuration analysis
- Best for monthly health reviews

**Tiger Mode** (2-5 minutes)
- All Deep Scan checks
- Advanced index analysis (duplicate/redundant detection)
- Most thorough diagnostic available
- Best for major performance investigations

## Usage

The agent walks you through an interactive workflow:

### 1. Connect to SQL Server

Choose your server and authentication method. The agent auto-discovers user databases on the instance.

```
── SQL Server Connection Setup ──
  Server [localhost]: myserver.local
  Authentication method:
    [1] Windows Authentication (Trusted Connection)
    [2] SQL Server Authentication (username/password)
```

### 2. Select a database

Pick a specific database or scan all user databases at once.

### 3. Choose scan mode

Select the depth of analysis you want:

```
── Scan Mode Selection ──
  Select scan mode:
    [1] Quick Scan (Standard DMV queries only)
    [2] Deep Scan (DMVs + Tiger Toolbox checks)
    [3] Tiger Mode (Comprehensive - All checks including advanced index analysis)
```

- **Quick Scan**: Fast DMV-based analysis (missing indexes, fragmentation, expensive queries, unused indexes)
- **Deep Scan**: Adds Tiger Toolbox checks (VLF, TempDB, backups, memory, MaxDOP, deprecated features)
- **Tiger Mode**: Most comprehensive - includes all above plus duplicate/redundant index detection

### 4. Review recommendations

The agent runs diagnostic queries, sends the results to Copilot for analysis, and displays categorized findings:

```
── Found 5 Recommendation(s) ──

  ── Performance ──
    1. [High]   Missing index on Orders.CustomerID
    2. [Medium] Fragmented index IX_Products_Name (47.3%)

  ── Configuration ──
    3. [Low]    Unused index IX_Legacy_Flag
```

### 4. Apply fixes

Choose between two modes:

- **One-by-one** — Copilot explains each issue, generates a fix script, and asks for permission before executing
- **Batch** — generates all scripts upfront, shows a summary, then executes with a single confirmation

> [!IMPORTANT]
> Always review generated scripts before applying them to production databases. The agent includes safety checks, but human review is essential.

### 5. Summary report

After processing, the agent shows a summary of applied, skipped, and failed fixes.

## Project structure

```
SQLPerfAgent/
├── Program.cs                  # Entry point and main workflow
├── Models/
│   └── Recommendation.cs      # Data models and connection config
├── Services/
│   ├── CopilotFixService.cs   # Copilot SDK session and AI interactions
│   └── SqlQueryService.cs     # Direct DMV queries via SqlClient
└── UI/
    └── ConsoleUI.cs            # Console display helpers and prompts
```

## Resources

- [GitHub Copilot SDK documentation](https://github.com/features/copilot)
- [SQL Server Dynamic Management Views](https://learn.microsoft.com/sql/relational-databases/system-dynamic-management-views/)
- [mssql-mcp on npm](https://www.npmjs.com/package/mssql-mcp)
