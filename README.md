# Synercoding.ClaudeApprover

A .NET library for building [Claude Code](https://docs.anthropic.com/en/docs/claude-code) `PreToolUse` hook approver scripts. It provides a polymorphic approval system where you subclass `BaseApprover` and override per-tool handler methods to allow, deny, or ask for each tool invocation. Communication with Claude Code happens via JSON over stdin/stdout.

## How It Works

Claude Code supports [hooks](https://docs.anthropic.com/en/docs/claude-code/hooks) that run before a tool is executed. This library lets you write a .NET script that acts as a `PreToolUse` hook, giving you programmatic control over which operations Claude Code is allowed to perform.

The core approval flow:

1. Claude Code sends a `ToolInput` JSON object via stdin
2. The library deserializes it into a typed `IToolInput` (e.g. `BashInput`, `EditInput`, `WriteInput`)
3. `BaseApprover.Handle()` dispatches to the appropriate per-tool virtual method
4. Your handler returns a `PreToolUseOutput` with a `PermissionDecision` (`Allow`, `Deny`, or `Ask`) and an optional reason
5. The output is serialized back to Claude Code via stdout

## Getting Started

The approver script uses `dotnet run` with C# file-based programs, which requires the **.NET 10 SDK** or later installed on the machine running the hook.

Install the NuGet package:

```bash
dotnet add package Synercoding.ClaudeApprover
```

The library targets **net10.0**.

## Usage

### 1. Create an approver script

Create a C# file (e.g. `.claude/hooks/Approver.cs`) that uses the library. You can use the built-in `InsideProjectAllowedApprover` which restricts all file and bash operations to within the project root, or subclass `BaseApprover` for full control.

A working example is available at [`.claude/hooks/Approver.cs`](.claude/hooks/Approver.cs):

```csharp
#:sdk Microsoft.NET.Sdk
#:package Synercoding.ClaudeApprover@*

using Synercoding.ClaudeApprover;
using Synercoding.ClaudeApprover.Converters;
using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;
using System.Text.Json;

var (json, input) = InputProcessor.Process(Console.OpenStandardInput());
if (input is null)
{
    Console.Error.WriteLine("Input could not be parsed...");
    return 1;
}

var approver = new InsideProjectAllowedApprover();

var output = approver.Handle(input);

if (output is not null)
{
    var outputJson = JsonSerializer.Serialize(output, ToolOutputJsonContext.Default.PreToolUseOutput);
    Console.WriteLine(outputJson);
}

return 0;
```

### 2. Configure the hook

Create a `.claude/hooks/run-approver.cjs` wrapper script that invokes the approver via `dotnet run`:

```javascript
const { execFileSync } = require("child_process");
const { join } = require("path");

const workDir = join(process.env.CLAUDE_PROJECT_DIR, ".claude", "hooks");

try {
  execFileSync("dotnet", ["run", "-v", "q", "./Approver.cs"], {
    cwd: workDir,
    stdio: "inherit",
  });
} catch (err) {
  process.exit(err.status ?? 1);
}
```

Then add a `PreToolUse` hook in your `.claude/settings.json` that runs the wrapper:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "*",
        "hooks": [
          {
            "type": "command",
            "command": "node -e \"require(require('path').join(process.env.CLAUDE_PROJECT_DIR,'.claude','hooks','run-approver.cjs'))\""
          }
        ]
      }
    ]
  }
}
```

The Node.js wrapper ensures the hook works correctly cross-platform. Windows and Linux have different syntax for referencing environment variables in shell commands (`%VAR%` vs `$VAR`), which is needed to resolve the hook script path via `CLAUDE_PROJECT_DIR`. Using a Node.js wrapper with `process.env` and `path.join` avoids this issue entirely. The `matcher` is set to `*` so the hook runs for every tool invocation. The `-v q` flag suppresses MSBuild output so only the JSON response is written to stdout.

If you only use the hook in a `.claude/settings.local.json` (which is not checked into source control and thus platform-specific), you can skip the wrapper and invoke `dotnet run` directly:

On Linux / macOS:
```json
"command": "cd \"$CLAUDE_PROJECT_DIR\"/.claude/hooks && dotnet run -v q ./Approver.cs"
```

On Windows:
```json
"command": "cd /d \"%CLAUDE_PROJECT_DIR%\\.claude\\hooks\" && dotnet run -v q Approver.cs"
```

### 3. Customize approval logic

To implement custom rules, subclass `BaseApprover` and override the handler methods you need:

```csharp
public class MyApprover : BaseApprover
{
    public override PreToolUseOutput? Handle(ToolInput input, WriteInput write)
    {
        if (write.FilePath.EndsWith(".csproj"))
            return Deny("Modifying project files is not allowed.");

        return Allow();
    }

    public override PreToolUseOutput? Handle(ToolInput input, BashInput bash)
    {
        return Ask(); // Always ask the user for bash commands
    }
}
```

Each handler can return:
- `Allow()` - permit the operation
- `Deny(reason)` - block the operation with an explanation
- `Ask(reason)` - prompt the user for confirmation
- `null` - no opinion (the default)

## Built-in Approvers

### `InsideProjectAllowedApprover`

The included `InsideProjectAllowedApprover` enforces that all file operations (read, edit, write) and bash commands stay within the project root directory. It also prevents access to `.git` directories and includes a bash command parser with per-command approval via the `CommandApprovers` dictionary.

The set of recognized bash commands can be extended by adding entries to the `CommandApprovers` dictionary property. Any command not in the dictionary will default to `Ask`. For example:

```csharp
var approver = new InsideProjectAllowedApprover();
approver.CommandApprovers["dotnet"] = InsideProjectAllowedApprover.AllowCommand;
```

#### Additional Directories

By default, all file and bash operations are restricted to the project root. You can allow access to additional directories (e.g. sibling projects) using the `AdditionalDirectories` property. Paths can be absolute or relative to the project root:

```csharp
var approver = new InsideProjectAllowedApprover();
approver.AdditionalDirectories.Add("../sister-project");
approver.AdditionalDirectories.Add("/opt/shared-libs");
```

The approver can also import additional directories from Claude's own settings files. When `ImportAdditionalDirsFromClaude` is `true` (the default), it reads the `additionalDirectories` JSON array from `.claude/settings.json` and `.claude/settings.local.json`:

```json
{
  "additionalDirectories": ["../sister-project", "../shared-utils"]
}
```

To disable importing from Claude settings:

```csharp
approver.ImportAdditionalDirsFromClaude = false;
```

#### MCP Approvers

You can also add custom logic for specific mcp servers:

```csharp
var approver = new InsideProjectAllowedApprover();
approver.McpApprovers["playwright"] = (tool, input) => BaseApprover.Allow();
```

## Project Root Detection

`BaseApprover` resolves the project root by checking (in order):

1. `CLAUDE_PROJECT_DIR` environment variable
2. Directory containing a `.claude` folder
3. Directory containing a `.sln` or `.slnx` file

## Build & Test

```bash
dotnet build
dotnet test
```

## License

See [LICENSE](LICENSE) for details.
