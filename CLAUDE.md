# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Synercoding.ClaudeApprover is a .NET library for building Claude Code pre-tool-use hook approver scripts. It provides a polymorphic approval system where consumers subclass `BaseApprover` and override per-tool handler methods to allow/deny/ask for each tool invocation. Communication with Claude Code happens via JSON over stdin/stdout.

## Build & Test Commands

```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~SomeTestName"   # run a single test
```

The library multi-targets **net8.0, net9.0, net10.0**. Tests target **net10.0** only.

## Architecture

### Core Approval Flow

1. Claude Code sends a `ToolInput` JSON object via stdin
2. `ToolInputConverter` deserializes it into a typed `IToolInput` (e.g., `BashInput`, `EditInput`)
3. `BaseApprover.Handle(ToolInput)` dispatches to the appropriate per-tool virtual method
4. The handler returns a `PreToolUseOutput` with a `PermissionDecision` (Allow/Deny/Ask) and optional reason
5. The output is serialized back to Claude Code via stdout

### Key Types

- **`BaseApprover`** (`Approver.cs`) — abstract base with virtual handlers per tool type and helper methods `Allow()`, `Deny()`, `Ask()`
- **`InsideProjectAllowedApprover`** (`InsideProjectAllowedApprover.cs`) — default implementation that restricts all file/bash operations to within the project root
- **`BashCommandParser`** (`BashParser/`) — recursive descent parser for bash command strings, producing `Pipeline` → `Command` → arguments/redirections
- **`ToolInput`** / `IToolInput` (`Input/`) — typed representations of each Claude Code tool invocation
- **`PreToolUseOutput`** (`Output/`) — hook response including permission decision and optional system messages

### Project Root Detection

`BaseApprover` resolves the project root by checking (in order): `CLAUDE_PROJECT_DIR` env var → directory containing `.claude` folder → directory containing `.sln`/`.slnx`.

## Code Style

- C# 14, strict nullable, warnings-as-errors
- File-scoped namespaces
- Comprehensive `.editorconfig` governs all style rules — follow existing patterns
- Private fields use `_camelCase`, no `this.` qualifier
