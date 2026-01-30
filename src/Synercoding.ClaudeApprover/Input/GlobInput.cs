using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Glob tool invocation.
/// </summary>
public class GlobInput : IToolInput
{
    /// <summary>
    /// Gets the glob pattern to match files against.
    /// </summary>
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }
}
