namespace Synercoding.ClaudeApprover.PowerShellParser;

/// <summary>
/// Represents the permission decision for a PowerShell command.
/// </summary>
public enum CommandPermission
{
    /// <summary>
    /// The command is allowed to execute.
    /// </summary>
    Allow,

    /// <summary>
    /// The command is denied from executing.
    /// </summary>
    Deny,

    /// <summary>
    /// The user should be asked for confirmation before executing the command.
    /// </summary>
    Ask
}
