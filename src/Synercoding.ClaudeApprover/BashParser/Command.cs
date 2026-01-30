using System.Diagnostics;

namespace Synercoding.ClaudeApprover.BashParser;

/// <summary>
/// Represents a single parsed bash command with its executable, arguments, and redirections.
/// </summary>
[DebuggerDisplay("{_toCommandLine()}")]
public class Command
{
    /// <summary>
    /// Gets the executable name of the command.
    /// </summary>
    public required string Executable { get; init; }

    /// <summary>
    /// Gets the list of arguments passed to the command.
    /// </summary>
    public List<string> Arguments { get; init; } = new();

    /// <summary>
    /// Gets the list of I/O redirections for the command.
    /// </summary>
    public List<Redirection> Redirections { get; init; } = new();

    private string _toCommandLine()
    {
        var parts = new List<string> { Executable };

        foreach (var arg in Arguments)
        {
            // Quote arguments that contain spaces
            parts.Add(arg.Contains(' ') ? $"\"{arg}\"" : arg);
        }

        foreach (var redir in Redirections)
        {
            parts.Add($"{redir.Type} {redir.Target}");
        }

        return string.Join(" ", parts);
    }
}
