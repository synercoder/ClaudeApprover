namespace Synercoding.ClaudeApprover.BashParser;

/// <summary>
/// Represents a parsed bash pipeline containing one or more commands linked by operators.
/// </summary>
public class Pipeline
{
    /// <summary>
    /// Gets the list of commands in this pipeline segment.
    /// </summary>
    public List<Command> Commands { get; init; } = new();

    /// <summary>
    /// Gets or sets the operator linking this pipeline to the next (<c>||</c>, <c>&amp;&amp;</c>, or <c>null</c>).
    /// </summary>
    public string? Operator { get; set; } // "||" or "&&" or null

    /// <summary>
    /// Gets or sets the next pipeline in the chain, if any.
    /// </summary>
    public Pipeline? NextPipeline { get; set; }
}
