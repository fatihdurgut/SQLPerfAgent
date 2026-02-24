---
name: copilot-sdk
description: Build agentic applications with GitHub Copilot SDK. Use when embedding AI agents in apps, creating custom tools, implementing streaming responses, managing sessions, connecting to MCP servers, or creating custom agents. Triggers on Copilot SDK, GitHub SDK, agentic app, embed Copilot, programmable agent, MCP server, custom agent.
---

# GitHub Copilot SDK

Embed Copilot's agentic workflows in any application using Python, TypeScript, Go, or .NET.

## Overview

The GitHub Copilot SDK exposes the same engine behind Copilot CLI: a production-tested agent runtime you can invoke programmatically. No need to build your own orchestration - you define agent behavior, Copilot handles planning, tool invocation, file edits, and more.

## Prerequisites

1. **GitHub Copilot CLI** installed and authenticated ([Installation guide](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli))
2. **Language runtime**: Node.js 18+, Python 3.8+, Go 1.21+, or .NET 8.0+

Verify CLI: `copilot --version`

## Installation

### Node.js/TypeScript
```bash
mkdir copilot-demo && cd copilot-demo
npm init -y --init-type module
npm install @github/copilot-sdk tsx
```

### Python
```bash
pip install github-copilot-sdk
```

### Go
```bash
mkdir copilot-demo && cd copilot-demo
go mod init copilot-demo
go get github.com/github/copilot-sdk/go
```

### .NET
```bash
dotnet new console -n CopilotDemo && cd CopilotDemo
dotnet add package GitHub.Copilot.SDK
```

## Quick Start

### TypeScript
```typescript
import { CopilotClient } from "@github/copilot-sdk";

const client = new CopilotClient();
const session = await client.createSession({ model: "gpt-4.1" });

const response = await session.sendAndWait({ prompt: "What is 2 + 2?" });
console.log(response?.data.content);

await client.stop();
process.exit(0);
```

Run: `npx tsx index.ts`

### Python
```python
import asyncio
from copilot import CopilotClient

async def main():
    client = CopilotClient()
    await client.start()

    session = await client.create_session({"model": "gpt-4.1"})
    response = await session.send_and_wait({"prompt": "What is 2 + 2?"})

    print(response.data.content)
    await client.stop()

asyncio.run(main())
```

### Go
```go
package main

import (
    "fmt"
    "log"
    "os"
    copilot "github.com/github/copilot-sdk/go"
)

func main() {
    client := copilot.NewClient(nil)
    if err := client.Start(); err != nil {
        log.Fatal(err)
    }
    defer client.Stop()

    session, err := client.CreateSession(&copilot.SessionConfig{Model: "gpt-4.1"})
    if err != nil {
        log.Fatal(err)
    }

    response, err := session.SendAndWait(copilot.MessageOptions{Prompt: "What is 2 + 2?"}, 0)
    if err != nil {
        log.Fatal(err)
    }

    fmt.Println(*response.Data.Content)
    os.Exit(0)
}
```

### .NET (C#)
```csharp
using GitHub.Copilot.SDK;

await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig { Model = "gpt-4.1" });

var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = "What is 2 + 2?" });
Console.WriteLine(response?.Data.Content);
```

Run: `dotnet run`

## Streaming Responses

Enable real-time output for better UX:

### TypeScript
```typescript
import { CopilotClient, SessionEvent } from "@github/copilot-sdk";

const client = new CopilotClient();
const session = await client.createSession({
    model: "gpt-4.1",
    streaming: true,
});

session.on((event: SessionEvent) => {
    if (event.type === "assistant.message_delta") {
        process.stdout.write(event.data.deltaContent);
    }
    if (event.type === "session.idle") {
        console.log(); // New line when done
    }
});

await session.sendAndWait({ prompt: "Tell me a short joke" });

await client.stop();
process.exit(0);
```

### Python
```python
import asyncio
import sys
from copilot import CopilotClient
from copilot.generated.session_events import SessionEventType

async def main():
    client = CopilotClient()
    await client.start()

    session = await client.create_session({
        "model": "gpt-4.1",
        "streaming": True,
    })

    def handle_event(event):
        if event.type == SessionEventType.ASSISTANT_MESSAGE_DELTA:
            sys.stdout.write(event.data.delta_content)
            sys.stdout.flush()
        if event.type == SessionEventType.SESSION_IDLE:
            print()

    session.on(handle_event)
    await session.send_and_wait({"prompt": "Tell me a short joke"})
    await client.stop()

asyncio.run(main())
```

### Go
```go
session, err := client.CreateSession(&copilot.SessionConfig{
    Model:     "gpt-4.1",
    Streaming: true,
})
if err != nil {
    log.Fatal(err)
}

session.On(func(event copilot.SessionEvent) {
    if event.Type == copilot.AssistantMessageDelta {
        fmt.Print(*event.Data.DeltaContent)
    }
    if event.Type == copilot.SessionIdle {
        fmt.Println()
    }
})

_, err = session.SendAndWait(copilot.MessageOptions{Prompt: "Tell me a short joke"}, 0)
```

### .NET
```csharp
using GitHub.Copilot.SDK;

await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    Streaming = true
});

var done = new TaskCompletionSource();

session.On(evt =>
{
    switch (evt)
    {
        case AssistantMessageDeltaEvent delta:
            Console.Write(delta.Data.DeltaContent);
            break;
        case SessionIdleEvent:
            Console.WriteLine();
            done.SetResult();
            break;
    }
});

await session.SendAsync(new MessageOptions { Prompt = "Tell me a short joke" });
await done.Task;
```

## Custom Tools

Extend agents with domain-specific capabilities:

### TypeScript
```typescript
import { z } from "zod";
import { CopilotClient } from "@github/copilot-sdk";

const client = new CopilotClient();
const session = await client.createSession({
    model: "gpt-4.1",
    tools: [
        {
            name: "get_weather",
            description: "Get current weather for a city",
            parameters: z.object({
                city: z.string().describe("City name"),
            }),
            execute: async ({ city }) => {
                return { temperature: "72°F", condition: "Sunny" };
            },
        },
    ],
});

const response = await session.sendAndWait({
    prompt: "What's the weather in San Francisco?",
});
console.log(response?.data.content);

await client.stop();
process.exit(0);
```

### Python
```python
import asyncio
from copilot import CopilotClient

def get_weather(city: str) -> dict:
    """Get current weather for a city.

    Args:
        city: City name
    """
    return {"temperature": "72°F", "condition": "Sunny"}

async def main():
    client = CopilotClient()
    await client.start()

    session = await client.create_session({
        "model": "gpt-4.1",
        "tools": [get_weather],
    })

    response = await session.send_and_wait({
        "prompt": "What's the weather in San Francisco?",
    })
    print(response.data.content)
    await client.stop()

asyncio.run(main())
```

### Go
```go
weatherTool := copilot.Tool{
    Name:        "get_weather",
    Description: "Get current weather for a city",
    Parameters: map[string]interface{}{
        "type": "object",
        "properties": map[string]interface{}{
            "city": map[string]interface{}{
                "type":        "string",
                "description": "City name",
            },
        },
        "required": []string{"city"},
    },
    Execute: func(params map[string]interface{}) (interface{}, error) {
        return map[string]string{
            "temperature": "72°F",
            "condition":   "Sunny",
        }, nil
    },
}

session, _ := client.CreateSession(&copilot.SessionConfig{
    Model: "gpt-4.1",
    Tools: []copilot.Tool{weatherTool},
})
```

### .NET
```csharp
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using System.ComponentModel;

await using var client = new CopilotClient();
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    Tools = [
        AIFunctionFactory.Create(
            ([Description("City name")] string city) =>
                new { Temperature = "72°F", Condition = "Sunny" },
            "get_weather",
            "Get current weather for a city"
        )
    ]
});

var response = await session.SendAndWaitAsync(new MessageOptions
{
    Prompt = "What's the weather in San Francisco?"
});
Console.WriteLine(response?.Data.Content);
```

## MCP Server Integration

Connect to external tool servers using the Model Context Protocol:

### TypeScript
```typescript
const session = await client.createSession({
    model: "gpt-4.1",
    mcpServers: {
        github: {
            command: "npx",
            args: ["-y", "@modelcontextprotocol/server-github"],
            env: { GITHUB_PERSONAL_ACCESS_TOKEN: process.env.GITHUB_TOKEN },
        },
    },
});
```

### Python
```python
session = await client.create_session({
    "model": "gpt-4.1",
    "mcp_servers": {
        "github": {
            "command": "npx",
            "args": ["-y", "@modelcontextprotocol/server-github"],
            "env": {"GITHUB_PERSONAL_ACCESS_TOKEN": os.environ["GITHUB_TOKEN"]},
        }
    },
})
```

### Go
```go
session, _ := client.CreateSession(&copilot.SessionConfig{
    Model: "gpt-4.1",
    MCPServers: map[string]copilot.MCPServerConfig{
        "github": {
            Command: "npx",
            Args:    []string{"-y", "@modelcontextprotocol/server-github"},
            Env:     map[string]string{"GITHUB_PERSONAL_ACCESS_TOKEN": os.Getenv("GITHUB_TOKEN")},
        },
    },
})
```

### .NET
```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["github"] = new McpServerConfig
        {
            Command = "npx",
            Args = ["-y", "@modelcontextprotocol/server-github"],
            Env = new Dictionary<string, string>
            {
                ["GITHUB_PERSONAL_ACCESS_TOKEN"] = Environment.GetEnvironmentVariable("GITHUB_TOKEN")!
            }
        }
    }
});
```

## Custom Agents

Create specialized agents with persona and instructions:

### TypeScript
```typescript
const session = await client.createSession({
    model: "gpt-4.1",
    customAgents: [
        {
            name: "code-reviewer",
            instructions: `You are an expert code reviewer.
                Focus on: security, performance, maintainability.
                Be concise. Suggest specific fixes.`,
            tools: ["read_file", "list_directory"],
        },
    ],
});
```

### Python
```python
session = await client.create_session({
    "model": "gpt-4.1",
    "custom_agents": [
        {
            "name": "code-reviewer",
            "instructions": """You are an expert code reviewer.
                Focus on: security, performance, maintainability.
                Be concise. Suggest specific fixes.""",
            "tools": ["read_file", "list_directory"],
        }
    ],
})
```

### Go
```go
session, _ := client.CreateSession(&copilot.SessionConfig{
    Model: "gpt-4.1",
    CustomAgents: []copilot.CustomAgent{
        {
            Name: "code-reviewer",
            Instructions: `You are an expert code reviewer.
                Focus on: security, performance, maintainability.
                Be concise. Suggest specific fixes.`,
            Tools: []string{"read_file", "list_directory"},
        },
    },
})
```

### .NET
```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    CustomAgents = [
        new CustomAgentConfig
        {
            Name = "code-reviewer",
            Instructions = """
                You are an expert code reviewer.
                Focus on: security, performance, maintainability.
                Be concise. Suggest specific fixes.
                """,
            Tools = ["read_file", "list_directory"]
        }
    ]
});
```

## System Messages

Customize the agent's behavior while preserving safety:

### TypeScript
```typescript
const session = await client.createSession({
    model: "gpt-4.1",
    systemMessage: {
        content: "You are a SQL optimization expert. Focus on query performance.",
        mode: "append", // Preserves default safety instructions
    },
});
```

### .NET
```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-4.1",
    SystemMessage = new SystemMessageConfig
    {
        Content = "You are a SQL optimization expert. Focus on query performance.",
        Mode = SystemMessageMode.Append
    }
});
```

**Modes:**
- `append` (recommended): Adds your message to default system prompt
- `override`: Replaces default system prompt entirely

## Bring Your Own Key (BYOK)

Use external API providers:

### TypeScript
```typescript
const session = await client.createSession({
    provider: {
        type: "openai",
        baseUrl: "https://api.openai.com/v1",
        apiKey: process.env.OPENAI_API_KEY,
    },
});
```

### .NET
```csharp
await using var session = await client.CreateSessionAsync(new SessionConfig
{
    Provider = new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
    }
});
```

## Connecting to an Existing Server

Skip auto-starting a CLI server by connecting to an already-running instance:

### TypeScript
```typescript
const client = new CopilotClient({ cliUrl: "localhost:4321" });
const session = await client.createSession({ model: "gpt-4.1" });
```

### .NET
```csharp
using var client = new CopilotClient(new CopilotClientOptions
{
    CliUrl = "localhost:4321"
});

await using var session = await client.CreateSessionAsync(new SessionConfig { Model = "gpt-4.1" });
```

**Note:** When `cliUrl` is provided, the SDK will not spawn or manage a CLI process - it only connects to the existing server.

## Event Types

| Event | Description |
|-------|-------------|
| `user.message` | User input added |
| `assistant.message` | Complete model response |
| `assistant.message_delta` | Streaming response chunk |
| `assistant.reasoning` | Model reasoning (model-dependent) |
| `assistant.reasoning_delta` | Streaming reasoning chunk |
| `tool.execution_start` | Tool invocation started |
| `tool.execution_complete` | Tool execution finished |
| `session.idle` | No active processing |
| `session.error` | Error occurred |

## Client Configuration

| Option | Description | Default |
|--------|-------------|---------|
| `cliPath` | Path to Copilot CLI executable | System PATH |
| `cliUrl` | Connect to existing server (e.g., "localhost:4321") | None |
| `port` | Server communication port | Random |
| `useStdio` | Use stdio transport instead of TCP | true |
| `logLevel` | Logging verbosity | "info" |
| `autoStart` | Launch server automatically | true |
| `autoRestart` | Restart on crashes | true |
| `cwd` | Working directory for CLI process | Inherited |

## Session Configuration

| Option | Description |
|--------|-------------|
| `model` | LLM to use ("gpt-4.1", "claude-sonnet-4.5", etc.) |
| `sessionId` | Custom session identifier |
| `tools` | Custom tool definitions |
| `mcpServers` | MCP server connections |
| `customAgents` | Custom agent personas |
| `systemMessage` | Override default system prompt |
| `streaming` | Enable incremental response chunks |
| `availableTools` | Whitelist of permitted tools |
| `excludedTools` | Blacklist of disabled tools |

## Session Persistence

Save and resume conversations across restarts:

### Create with Custom ID
```typescript
const session = await client.createSession({
    sessionId: "user-123-conversation",
    model: "gpt-4.1"
});
```

### Resume Session
```typescript
const session = await client.resumeSession("user-123-conversation");
await session.send({ prompt: "What did we discuss earlier?" });
```

### List and Delete Sessions
```typescript
const sessions = await client.listSessions();
await client.deleteSession("old-session-id");
```

## Error Handling

```typescript
try {
    const client = new CopilotClient();
    const session = await client.createSession({ model: "gpt-4.1" });
    const response = await session.sendAndWait(
        { prompt: "Hello!" },
        30000 // timeout in ms
    );
} catch (error) {
    if (error.code === "ENOENT") {
        console.error("Copilot CLI not installed");
    } else if (error.code === "ECONNREFUSED") {
        console.error("Cannot connect to Copilot server");
    } else {
        console.error("Error:", error.message);
    }
} finally {
    await client.stop();
}
```

## Graceful Shutdown

```typescript
process.on("SIGINT", async () => {
    console.log("Shutting down...");
    await client.stop();
    process.exit(0);
});
```

## Common Patterns

### Multi-turn Conversation
```typescript
const session = await client.createSession({ model: "gpt-4.1" });

await session.sendAndWait({ prompt: "My name is Alice" });
await session.sendAndWait({ prompt: "What's my name?" });
// Response: "Your name is Alice"
```

### File Attachments
```typescript
await session.send({
    prompt: "Analyze this file",
    attachments: [{
        type: "file",
        path: "./data.csv",
        displayName: "Sales Data"
    }]
});
```

### Abort Long Operations
```typescript
const timeoutId = setTimeout(() => {
    session.abort();
}, 60000);

session.on((event) => {
    if (event.type === "session.idle") {
        clearTimeout(timeoutId);
    }
});
```

## Available Models

Query available models at runtime:

```typescript
const models = await client.getModels();
// Returns: ["gpt-4.1", "gpt-4o", "claude-sonnet-4.5", ...]
```

## Best Practices

1. **Always cleanup**: Use `try-finally` or `defer` to ensure `client.stop()` is called
2. **Set timeouts**: Use `sendAndWait` with timeout for long operations
3. **Handle events**: Subscribe to error events for robust error handling
4. **Use streaming**: Enable streaming for better UX on long responses
5. **Persist sessions**: Use custom session IDs for multi-turn conversations
6. **Define clear tools**: Write descriptive tool names and descriptions

## Architecture

```
Your Application
       |
  SDK Client
       | JSON-RPC
  Copilot CLI (server mode)
       |
  GitHub (models, auth)
```

The SDK manages the CLI process lifecycle automatically. All communication happens via JSON-RPC over stdio or TCP.

## Resources

- **GitHub Repository**: https://github.com/github/copilot-sdk
- **Getting Started Tutorial**: https://github.com/github/copilot-sdk/blob/main/docs/tutorials/first-app.md
- **GitHub MCP Server**: https://github.com/github/github-mcp-server
- **MCP Servers Directory**: https://github.com/modelcontextprotocol/servers
- **Cookbook**: https://github.com/github/copilot-sdk/tree/main/cookbook
- **Samples**: https://github.com/github/copilot-sdk/tree/main/samples

## Status

This SDK is in **Technical Preview** and may have breaking changes. Not recommended for production use yet.
