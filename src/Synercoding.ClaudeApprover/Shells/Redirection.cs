namespace Synercoding.ClaudeApprover.Shells;

/// <summary>
/// Represents a shell I/O redirection.
/// </summary>
public class Redirection
{
    /// <summary>
    /// Gets the redirection type. Common forms: <c>&gt;</c>, <c>&gt;&gt;</c>, <c>2&gt;</c>, <c>2&gt;&amp;1</c>, <c>*&gt;</c>, <c>&lt;</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the redirection target. For stream-merge forms such as <c>2&gt;&amp;1</c> this is the merge target (e.g. <c>&amp;1</c>).
    /// </summary>
    public required string Target { get; init; }
}
