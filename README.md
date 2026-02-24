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

### 3. Review recommendations

The agent runs DMV diagnostic queries, sends the results to Copilot for analysis, and displays categorized findings:

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
