namespace Synercoding.ClaudeApprover.Shells;

/// <summary>
/// Represents a parsed shell pipeline (one or more commands connected by pipe operators), optionally chained to a following pipeline by a statement separator.
/// </summary>
public class Pipeline
{
    /// <summary>
    /// Gets the list of commands in the pipeline, connected by pipe (<c>|</c>) operators.
    /// </summary>
    public List<Command> Commands { get; init; } = new();

    /// <summary>
    /// Statement separator/operator connecting this pipeline to <see cref="NextPipeline"/>: <c>;</c>, <c>&amp;&amp;</c>, <c>||</c>, or <c>null</c> for the last segment.
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>
    /// Gets the next pipeline in the statement chain, or <c>null</c> if this is the last pipeline.
    /// </summary>
    public Pipeline? NextPipeline { get; set; }
}
