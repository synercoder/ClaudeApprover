namespace Synercoding.ClaudeApprover.BashParser;

/// <summary>
/// Represents a bash I/O redirection.
/// </summary>
public class Redirection
{
    /// <summary>
    /// Gets the redirection type (e.g. <c>&gt;</c>, <c>2&gt;</c>, <c>&lt;</c>).
    /// </summary>
    public required string Type { get; init; } // ">" or "2>" or "<" etc.

    /// <summary>
    /// Gets the redirection target file path.
    /// </summary>
    public required string Target { get; init; }
}
