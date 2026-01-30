#:sdk Microsoft.NET.Sdk
#:project ../src/Synercoding.ClaudeApprover

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
    // Check if it is an unknown bash command we want to perhaps integrate later, so log the input
    if (input.Input is BashInput && output.HookSpecificOutput.PermissionDecision == PermissionDecision.Ask)
        _logInput(json, input.ToolName);

    var outputJson = JsonSerializer.Serialize(output, ToolOutputJsonContext.Default.PreToolUseOutput);

    Console.WriteLine(outputJson);
}
else
{
    // Output was null, lets log the input, perhaps integrate it later
    _logInput(json, input.ToolName);
}

return 0;


static void _logInput(string jsonInput, string toolName)
{
    var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logDirectory);
    var logPath = Path.Combine(logDirectory, $"{toolName}-{DateTime.UtcNow:yyyyMMddTHHmmssffff}.json");
    File.WriteAllText(logPath, jsonInput);
}
