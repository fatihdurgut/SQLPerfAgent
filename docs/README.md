<!-- prettier-ignore -->
<div align="center">

# SQL Performance Agent — Documentation

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![GitHub Copilot SDK](https://img.shields.io/badge/GitHub_Copilot_SDK-0.1.26-black?style=flat-square&logo=github)](https://github.com/features/copilot)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](../LICENSE)

</div>

---

## Table of Contents

- [Problem Statement](#problem-statement)
- [Solution](#solution)
- [Prerequisites](#prerequisites)
- [Setup](#setup)
- [Deployment](#deployment)
- [Architecture](#architecture)
- [Data Flow](#data-flow)
- [Responsible AI (RAI) Notes](#responsible-ai-rai-notes)
- [Further Reading](#further-reading)

---

## Problem Statement

Database administrators and developers routinely face SQL Server performance issues — slow queries, missing indexes, fragmented indexes, misconfigured TempDB, excessive Virtual Log Files — that degrade application responsiveness and increase infrastructure costs.

Diagnosing these issues today requires:

1. **Deep DMV expertise** — knowing which Dynamic Management Views to query, how to join them, and how to interpret the raw output.
2. **Manual script writing** — crafting safe T-SQL fix scripts with proper `IF EXISTS` guards, `ONLINE` operations, and rollback instructions.
3. **Toolbox fragmentation** — jumping between disparate diagnostic scripts (Tiger Toolbox, community scripts, custom queries) with no unified analysis layer.
4. **Context switching** — copying raw DMV output into documentation or chat tools to get a second opinion on what the numbers mean.

The result: performance tuning is slow, error-prone, and inaccessible to teams without a dedicated DBA.

---

## Solution

**SQLPerfAgent** is an AI-powered CLI tool that automates the full diagnostic-to-fix lifecycle for SQL Server performance tuning.

It connects to your SQL Server instance, runs a combination of **built-in DMV queries** and **extensible toolbox plugins**, then sends the raw results to **GitHub Copilot** for AI-powered analysis. Copilot categorizes findings by severity (High / Medium / Low), explains each issue in plain language, and generates ready-to-run T-SQL fix scripts — complete with safety checks, `ONLINE` index operations, and rollback instructions.

### What makes it different

| Capability | Traditional Approach | SQLPerfAgent |
|---|---|---|
| Diagnostics | Run scripts manually, interpret raw DMV output | Automated DMV + plugin execution with AI interpretation |
| Fix generation | Write T-SQL by hand, hope for safety checks | AI-generated scripts with `IF EXISTS` guards and rollback |
| Extensibility | Copy-paste scripts from blogs and forums | Drop a folder in `Toolbox/` — no code changes required |
| Tool selection | Know which scripts to run upfront | Describe your problem in plain English; AI suggests relevant tools |
| Interactive Q&A | Search docs, Stack Overflow, hope for context | Ask Copilot questions about *your specific instance* in real time |

### Key workflow

```
Connect to SQL Server
        │
        ▼
  Run diagnostics (DMV queries + toolbox plugins)
        │
        ▼
  AI analyzes results → categorized recommendations
        │
        ▼
  Optional: Ask follow-up questions (interactive Q&A)
        │
        ▼
  Generate fix scripts → review → execute (one-by-one or batch)
        │
        ▼
  Summary report (fixed / skipped / errors)
```

---

## Prerequisites

| Requirement | Version | Purpose |
|---|---|---|
| [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0 or later | Build and run the application |
| [GitHub Copilot](https://github.com/features/copilot) subscription | Active (Free, Pro, Business, or Enterprise) | AI analysis, fix generation, and interactive Q&A |
| [Node.js 18+](https://nodejs.org/) | 18 or later | Required for the `mssql-mcp` server (launched automatically via `npx`) |
| SQL Server instance | 2016+ recommended | Target database |

### Copilot authentication

The GitHub Copilot SDK authenticates independently via its own **OAuth device flow** — on first run it prompts you to visit a URL and enter a code, then stores a token locally. **No GitHub CLI (`gh`) is required.**

### Copilot subscription tiers

The Free tier works but has a **50 messages/month limit**. A single SQLPerfAgent run can consume 5–15+ messages depending on the number of recommendations and follow-up questions. **Copilot Pro or higher is recommended** for regular use.

| Tier | Monthly Cost | Works with SQLPerfAgent? | Notes |
|---|---|---|---|
| Free | $0 | Yes (limited) | ~50 messages/month; roughly 3–10 full runs |
| Pro | $10 | **Recommended** | Unlimited messages, full model catalog |
| Business | $19/user | Full support | Admin controls, audit logs |
| Enterprise | $39/user | Full support | Fine-tuned models, IP indemnity |

### Supported environments

- **SQL Server**: On-premises SQL Server 2016+, Azure SQL Database, Azure SQL Managed Instance
- **Authentication**: Windows Authentication (Integrated Security) and SQL Server Authentication (username/password)
- **OS**: Windows, macOS, Linux (anywhere .NET 10 runs)

### Verify prerequisites

```bash
# .NET SDK
dotnet --version          # Should output 10.0.x or later

# Node.js
node --version            # Should output v18.x or later
npx --version             # Should be available
```

---

## Setup

### 1. Clone the repository

```bash
git clone https://github.com/fatihdurgut/SQLPerfAgent.git
cd SQLPerfAgent
```

### 2. Restore dependencies and build

```bash
dotnet restore
dotnet build
```

This installs two NuGet packages:

- **GitHub.Copilot.SDK** (`0.1.26`) — the Copilot SDK for AI agent capabilities
- **Microsoft.Data.SqlClient** (`6.1.4`) — SQL Server connectivity

### 3. Run the agent

```bash
dotnet run
```

On first run, the Copilot SDK will prompt you to authenticate via an OAuth device flow — visit the displayed URL and enter the code. This is a one-time step; the token is cached locally for future runs.

The interactive CLI walks you through eight steps:

1. **Connection setup** — server address, authentication method, database selection
2. **Toolbox discovery** — automatic scan of `Toolbox/` for plugin diagnostics
3. **Tool selection** — run all, pick specific, get AI suggestions, or skip
4. **Diagnostics** — DMV queries + plugin SQL scripts execute against your instance
5. **AI analysis** — results are sent to Copilot, which returns categorized recommendations
6. **Q&A mode** (optional) — ask follow-up questions about your specific instance
7. **Fix generation** — AI-generated T-SQL scripts with safety checks and rollback instructions
8. **Execution** — apply fixes one-by-one or in batch, with confirmation prompts at every step

### 5. Add custom toolbox plugins (optional)

Create a subfolder under `Toolbox/` with:

- A `tool.md` file (manifest and AI context)
- One or more `.sql` files

No code changes required. See the [plugin authoring guide](../README.md#creating-your-own-tool) in the main README.

---

## Deployment

### Option A: Run from source (development)

```bash
git clone https://github.com/fatihdurgut/SQLPerfAgent.git
cd SQLPerfAgent
dotnet run
```

### Option B: Publish as a self-contained executable

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained -o ./publish

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -o ./publish
```

The `publish/` folder contains a standalone executable with all .NET dependencies bundled. Distribute it to machines that may not have the .NET SDK installed.

> **Note**: Node.js is still required on the target machine for the `mssql-mcp` server, which is launched via `npx` at runtime.

### Option C: Framework-dependent deployment

```bash
dotnet publish -c Release -o ./publish
```

Requires the .NET 10.0 runtime on the target machine. Produces a smaller output.

### Deployment checklist

- [ ] .NET 10.0 SDK or Runtime installed (depending on publish mode)
- [ ] Node.js 18+ installed (for `mssql-mcp`)
- [ ] Active GitHub Copilot subscription (Free works, Pro recommended)
- [ ] Network access from the deployment machine to the target SQL Server instance
- [ ] Network access from the deployment machine to GitHub APIs (for Copilot)
- [ ] Network access from the deployment machine to npm registry (for `npx mssql-mcp@latest`)
- [ ] `Toolbox/` folder present alongside the executable (auto-copied during build/publish)

### Environment considerations

| Scenario | Notes |
|---|---|
| **Air-gapped networks** | Not supported — requires internet access for GitHub Copilot API and `npx mssql-mcp@latest` |
| **Corporate proxies** | Configure `HTTP_PROXY` / `HTTPS_PROXY` environment variables for both `dotnet` and `npx` |
| **CI/CD pipelines** | Not designed for unattended use — the CLI is interactive and requires user input at every step |
| **Docker** | Possible with a multi-stage Dockerfile (.NET 10 SDK + Node.js); mount `gh` credentials into the container |

---

## Architecture

### High-level overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        SQLPerfAgent CLI                         │
│                         (Program.cs)                            │
│                                                                 │
│  ┌─────────────────┐ ┌──────────────────┐ ┌──────────────────┐  │
│  │  SqlQuery       │ │  Toolbox         │ │  CopilotFix      │  │
│  │  Service        │ │  Discovery &     │ │  Service         │  │
│  │                 │ │  Execution       │ │                  │  │
│  │ Runs 4 built-in │ │  Services        │ │ GitHub Copilot   │  │
│  │ DMV diagnostic  │ │                  │ │ SDK integration  │  │
│  │ queries via     │ │ Scans Toolbox/   │ │ (session mgmt,   │  │
│  │ Microsoft.Data  │ │ for plugins,     │ │  streaming,      │  │
│  │ .SqlClient      │ │ executes .sql    │ │  tool suggestions│  │
│  │                 │ │ scripts          │ │  fix generation) │  │
│  └────────┬────────┘ └────────┬─────────┘ └────────┬─────────┘  │
│           │                   │                     │            │
│  ┌────────┴───────────────────┴─────────────────────┴─────────┐  │
│  │                        ConsoleUI                           │  │
│  │            Interactive prompts, colored output,            │  │
│  │            multi-select, masked password input             │  │
│  └────────────────────────────────────────────────────────────┘  │
└───────────┬───────────────────┬─────────────────────┬───────────┘
            │                   │                     │
            ▼                   ▼                     ▼
      ┌──────────┐       ┌──────────┐          ┌──────────────┐
      │   SQL    │       │ Toolbox/ │          │   GitHub     │
      │  Server  │       │ Plugins  │          │   Copilot    │
      │          │       │          │          │   API        │
      │ DMV data │       │ tool.md  │          └──────┬───────┘
      │ returned │       │ + .sql   │                 │
      └──────────┘       └──────────┘                 ▼
                                               ┌──────────────┐
                                               │  mssql-mcp   │
                                               │  Server      │
                                               │  (via npx)   │
                                               └──────┬───────┘
                                                      │
                                                      ▼
                                                ┌──────────┐
                                                │   SQL    │
                                                │  Server  │
                                                │ (direct) │
                                                └──────────┘
```

### Project structure

```
SQLPerfAgent/
├── Program.cs                      # Main workflow orchestration (8-step pipeline)
├── Models/
│   ├── Recommendation.cs           # Data models (recommendations, connection config)
│   ├── ToolboxItem.cs              # Toolbox plugin model
│   └── TolerantEnumConverter.cs    # Graceful JSON enum parsing
├── Services/
│   ├── CopilotFixService.cs        # GitHub Copilot SDK integration
│   ├── SqlQueryService.cs          # Direct DMV query execution
│   ├── ToolboxDiscoveryService.cs  # Plugin auto-discovery
│   └── ToolboxExecutionService.cs  # Plugin SQL execution engine
├── Toolbox/                        # Extensible plugin directory
│   ├── BestPracticesChecks/        # Memory, backups, MaxDOP, IFI, deprecated features
│   ├── DuplicateIndexes/           # Exact duplicate, duplicate key, redundant indexes
│   ├── TempDBChecks/               # File count, size equality, autogrow, location
│   └── VLFCheck/                   # Virtual Log File count analysis
├── UI/
│   └── ConsoleUI.cs                # Interactive CLI helpers
└── docs/
    └── README.md                   # This file
```

### Component responsibilities

| Component | File(s) | Responsibility |
|---|---|---|
| **Program.cs** | `Program.cs` | Main workflow orchestration — connects all services into the 8-step interactive pipeline |
| **SqlQueryService** | `Services/SqlQueryService.cs` | Executes 4 built-in DMV queries: missing indexes, index fragmentation, expensive queries (CPU), unused indexes |
| **ToolboxDiscoveryService** | `Services/ToolboxDiscoveryService.cs` | Scans `Toolbox/` for valid plugins (subfolder with `tool.md` + `.sql` files), resolves script execution order |
| **ToolboxExecutionService** | `Services/ToolboxExecutionService.cs` | Runs plugin SQL scripts against SQL Server — supports single-execution and GO-batch-separated modes |
| **CopilotFixService** | `Services/CopilotFixService.cs` | Manages the GitHub Copilot SDK session — authentication check, AI analysis, tool suggestions, Q&A, fix generation, and script execution via MCP |
| **ConsoleUI** | `UI/ConsoleUI.cs` | Interactive CLI helpers — prompted choices, multi-select, password masking, colored output, SQL script display |
| **Models** | `Models/*.cs` | Data models: `Recommendation` (with category, severity, affected object), `SqlConnectionConfig` (connection string + MCP env var builder), `ToolboxItem`, `TolerantEnumConverter` |

### Technology stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10.0 (C# top-level statements) |
| AI Engine | GitHub Copilot SDK `0.1.26` |
| SQL Connectivity | Microsoft.Data.SqlClient `6.1.4` |
| MCP Server | `mssql-mcp` (launched via `npx` at runtime) |
| Transport | Copilot SDK stdio transport (SDK ↔ CLI process) |
| Plugin System | File-system convention: `Toolbox/{name}/tool.md` + `*.sql` |

### How the AI integration works

1. **Initialization** — `CopilotFixService` creates a `CopilotClient` and a streaming `CopilotSession` with a detailed system prompt covering SQL performance best practices, anti-patterns, index strategy, and fix script guidelines.

2. **MCP Server attachment** — The session launches an `mssql-mcp` server as a child process (via `npx -y mssql-mcp@latest`) and passes SQL Server connection details as environment variables. This gives Copilot direct SQL Server query capabilities during the session.

3. **Analysis** — Raw DMV output + toolbox results + each plugin's `tool.md` content are assembled into a single prompt. Copilot returns a JSON array of structured `Recommendation` objects (category, severity, title, description, affected object, source).

4. **Tool suggestions** — When users describe their problem in natural language, Copilot maps it to available toolbox plugins with per-tool explanations of relevance.

5. **Interactive Q&A** — Free-form questions are sent to the session with database context. Copilot can invoke `mssql-mcp` tools to run additional diagnostic queries before answering.

6. **Fix generation** — Each recommendation is sent back to Copilot with instructions to generate safe, commented T-SQL with `IF EXISTS` guards, `ONLINE = ON` for index operations, and rollback instructions as comments.

7. **Execution** — Fix scripts are executed through the `mssql-mcp` server, keeping all operations within the Copilot session context. Results (success/failure) are reported back to the user.

---

## Data Flow

```
 User                   SQLPerfAgent              SQL Server           GitHub Copilot
  │                          │                        │                      │
  │  1. Connect              │                        │                      │
  │─────────────────────────>│                        │                      │
  │                          │  2. Validate auth      │                      │
  │                          │───────────────────────────────────────────────>│
  │                          │<──────────────────────────────────────────────│
  │                          │                        │                      │
  │  3. Select databases     │                        │                      │
  │     & tools              │                        │                      │
  │─────────────────────────>│                        │                      │
  │                          │  4. Run DMV queries    │                      │
  │                          │───────────────────────>│                      │
  │                          │<──────────────────────│                      │
  │                          │  5. Run toolbox SQL    │                      │
  │                          │───────────────────────>│                      │
  │                          │<──────────────────────│                      │
  │                          │                        │                      │
  │                          │  6. Send DMV results   │                      │
  │                          │  + toolbox output      │                      │
  │                          │  + tool.md context     │                      │
  │                          │───────────────────────────────────────────────>│
  │                          │  7. JSON recommendations                      │
  │                          │<──────────────────────────────────────────────│
  │                          │                        │                      │
  │  8. View findings        │                        │                      │
  │<─────────────────────────│                        │                      │
  │                          │                        │                      │
  │  9. Ask questions        │                        │                      │
  │─────────────────────────>│  10. Q&A prompt        │                      │
  │                          │───────────────────────────────────────────────>│
  │                          │      (Copilot may run  │                      │
  │                          │       additional SQL)  │<────────────────────│
  │                          │                        │────────────────────>│
  │  11. Answers             │                        │                      │
  │<─────────────────────────│<──────────────────────────────────────────────│
  │                          │                        │                      │
  │  12. Approve fixes       │                        │                      │
  │─────────────────────────>│  13. Generate scripts  │                      │
  │                          │───────────────────────────────────────────────>│
  │                          │<──────────────────────────────────────────────│
  │                          │  14. Execute via MCP   │                      │
  │                          │───────────────────────────────────────────────>│
  │                          │                        │<────────────────────│
  │                          │                        │────────────────────>│
  │  15. Summary report      │                        │                      │
  │<─────────────────────────│<──────────────────────────────────────────────│
```

### What data is sent where

| Data | Sent to Copilot API? | Sent to local mssql-mcp? | Purpose |
|---|---|---|---|
| DMV query results (table names, index names, query text, statistics) | **Yes** | No | AI analysis of performance issues |
| Toolbox plugin output (diagnostic check results) | **Yes** | No | Extended analysis beyond built-in DMVs |
| `tool.md` content (plugin documentation) | **Yes** | No | Context for AI to interpret plugin output |
| Server hostname | No | **Yes** (env var) | MCP server connection to SQL Server |
| Database name | **Yes** (in prompt) | **Yes** (env var) | Scoping analysis; MCP connection |
| SQL Auth credentials (username/password) | No | **Yes** (env vars) | MCP server authentication |
| User table row data | No | No | Only metadata and DMV statistics are queried |

> **Important**: SQL Auth credentials are passed as environment variables to the locally-spawned `mssql-mcp` child process only. They are **not** sent to the Copilot API. However, DMV output — which may contain query text with table names, column names, or literal values — **is** sent to the Copilot API for analysis.

---

## Responsible AI (RAI) Notes

### Purpose and intended use

SQLPerfAgent is designed to **assist** database administrators and developers with SQL Server performance diagnostics and remediation. It is a productivity tool, not a replacement for professional DBA judgment.

**Intended users**:
- Database administrators
- Backend developers
- DevOps engineers and site reliability engineers
- Teams managing SQL Server instances without a dedicated DBA

**Intended scenarios**:
- Development and staging environment diagnostics
- Pre-production performance audits
- Learning tool for junior DBAs to understand SQL Server internals and best practices
- Rapid triage of production performance incidents (with human oversight)

**Not intended for**:
- Fully automated, unattended production remediation
- Compliance or audit reporting without human review
- Replacing professional database administration on mission-critical systems

### Human oversight requirements

> **All AI-generated fix scripts must be reviewed by a qualified human before execution against production databases.**

The agent is designed with multiple human-in-the-loop checkpoints:

1. **Confirmation prompts** — Every fix script requires explicit user approval before execution. Users choose `yes`, `skip`, or `abort` for each script.
2. **Script visibility** — Generated T-SQL is displayed in full before execution. Nothing runs silently or in the background.
3. **Batch review** — In batch mode, all scripts are shown together for review before any execution begins.
4. **Rollback instructions** — Each fix script includes commented rollback commands so changes can be reversed.
5. **Safety checks** — The AI system prompt instructs Copilot to include `IF EXISTS` guards, prefer non-blocking operations (`ONLINE = ON`), and never drop data without explicit confirmation.
6. **Quit at any time** — Users can type `quit` at any prompt to exit immediately.

### Limitations and known risks

| Risk | Description | Mitigation |
|---|---|---|
| **AI hallucination** | Copilot may generate incorrect, suboptimal, or syntactically invalid SQL | All scripts are displayed for review; nothing executes without explicit user confirmation |
| **Stale context** | DMV data is a point-in-time snapshot; server conditions may change between diagnosis and fix application | Run diagnostics close to when fixes will be applied; re-scan after applying changes |
| **Missing context** | The AI only sees DMV output and plugin results, not the full application workload or business context | Use Q&A mode to provide additional context; combine with application-level monitoring (APM, Query Store) |
| **Destructive operations** | Index drops, configuration changes, and log file operations can impact availability | Safety checks in system prompt; `ONLINE = ON` preferred; rollback instructions included; maintenance window recommended for VLF and TempDB changes |
| **Credential handling** | SQL Auth credentials are passed as environment variables to the local MCP child process | Prefer Windows Authentication when possible; credentials are not sent to the Copilot API; credentials exist only in-memory for the session duration |
| **Model knowledge cutoff** | The AI model may not know about very recent SQL Server features or cumulative updates | Cross-reference recommendations against official Microsoft documentation for your specific SQL Server version |
| **Over-indexing** | AI may suggest too many indexes without considering cumulative write overhead | Review index recommendations holistically; consider total index count and write patterns per table |
| **Query text exposure** | DMV output may include SQL query text that contains table names, column names, or embedded literal values | Be aware that this metadata is sent to the Copilot API; avoid running against instances where query text contains highly sensitive data unless acceptable under your data policies |

### Data privacy considerations

- **No data persistence** — SQLPerfAgent does not write diagnostic results, recommendations, fix scripts, or credentials to disk. All data is in-memory for the session duration only.
- **No telemetry** — The application itself does not collect or transmit any telemetry beyond what the GitHub Copilot SDK handles.
- **Copilot API data handling** — DMV output (metadata, statistics, query text) sent to the Copilot API is subject to [GitHub's Copilot data policies](https://docs.github.com/en/copilot/overview-of-github-copilot/about-github-copilot-individual#about-data-for-github-copilot). Review these policies to ensure they meet your organization's requirements.
- **Sensitive data in query text** — `sys.dm_exec_query_stats` and `sys.dm_exec_sql_text` may surface SQL statements that contain literal values (e.g., customer IDs, email addresses in `WHERE` clauses). Consider this before running against instances with sensitive workloads.
- **Local MCP process** — The `mssql-mcp` server runs as a local child process. Connection credentials are passed as environment variables and exist only for the process lifetime.

### Best practices for responsible use

1. **Start with non-production** — Run against development or staging environments first to build confidence in the tool's recommendations before targeting production.
2. **Review every script** — Treat AI-generated T-SQL the same way you would treat a pull request from a junior team member: read it carefully, question assumptions, and test it.
3. **Use Windows Auth when possible** — Avoids passing SQL credentials as environment variables entirely.
4. **Maintain backups** — Always have a current, verified backup before applying index changes, configuration changes, or log file operations.
5. **Schedule maintenance windows** — Apply impactful fixes (index rebuilds, VLF corrections, TempDB reconfiguration) during low-traffic periods.
6. **Validate with monitoring** — After applying fixes, verify improvements using your existing monitoring tools (Query Store, Extended Events, Performance Monitor, third-party APM).
7. **Don't blindly trust severity ratings** — A "High" severity finding in a development database may not warrant immediate action. Context and business impact always matter.
8. **Keep the toolbox curated** — Review custom plugins before adding them to `Toolbox/`. The AI trusts `tool.md` content for interpretation guidance — inaccurate documentation leads to inaccurate analysis.
9. **Limit scope on sensitive instances** — If your SQL Server contains highly sensitive workloads, consider running the tool against a non-production replica or limiting the databases scanned.

### Transparency

- **AI model** — The Copilot session is configured to use `Claude Opus 4.6` via the GitHub Copilot SDK. The model selection is visible in [`Services/CopilotFixService.cs`](../Services/CopilotFixService.cs).
- **System prompt** — The full system prompt — including all SQL best practices, anti-patterns, and fix script guidelines — is open source and visible in the [`CopilotFixService.InitializeAsync()`](../Services/CopilotFixService.cs) method. There are no hidden instructions.
- **Plugin logic** — All toolbox plugins, including their SQL scripts and interpretation guidance (`tool.md`), are open source and auditable in the [`Toolbox/`](../Toolbox/) directory.
- **No hidden network calls** — The only external services contacted are the GitHub Copilot API (via the SDK) and the npm registry (via `npx` for `mssql-mcp`). No other telemetry or analytics endpoints are used.
- **Recommendation quality** — The tool's recommendations are only as good as the DMV data collected and the AI model's capabilities at the time of analysis. Results may vary between runs.

---

## Further Reading

- [Main README](../README.md) — Feature overview, usage guide, and plugin authoring instructions
- [GitHub Copilot SDK](https://github.com/features/copilot) — AI engine documentation
- [Microsoft Tiger Toolbox](https://github.com/microsoft/tigertoolbox) — Source for the built-in diagnostic scripts
- [SQL Server DMV Reference](https://learn.microsoft.com/sql/relational-databases/system-dynamic-management-views/) — Dynamic Management Views documentation
- [mssql-mcp](https://www.npmjs.com/package/mssql-mcp) — MCP server for SQL Server connectivity
- [GitHub Copilot Privacy](https://docs.github.com/en/copilot/overview-of-github-copilot/about-github-copilot-individual#about-data-for-github-copilot) — Data handling policies
