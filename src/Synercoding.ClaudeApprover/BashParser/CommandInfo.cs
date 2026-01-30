namespace Synercoding.ClaudeApprover.BashParser;

/// <summary>
/// Contains information about a command being evaluated for approval, including the parsed command, working directory, and project root.
/// </summary>
/// <param name="Command">The parsed command.</param>
/// <param name="WorkingDirectory">The current working directory.</param>
/// <param name="ProjectRoot">The project root directory.</param>
public record CommandInfo(Command Command, string WorkingDirectory, string ProjectRoot);
