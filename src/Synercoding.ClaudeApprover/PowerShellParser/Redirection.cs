namespace Synercoding.ClaudeApprover.PowerShellParser;

/// <summary>
/// Represents a PowerShell I/O redirection.
/// </summary>
public class Redirection
{
    /// <summary>
    /// Gets the redirection type (e.g. <c>&gt;</c>, <c>&gt;&gt;</c>, <c>2&gt;</c>, <c>2&gt;&amp;1</c>, <c>*&gt;</c>).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the redirection target. For stream-merge forms such as <c>2&gt;&amp;1</c> this is the merge target (e.g. <c>&amp;1</c>).
    /// </summary>
    public required string Target { get; init; }
}
