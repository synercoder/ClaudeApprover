using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Output;

/// <summary>
/// Base class for hook output containing common properties shared across all hook types.
/// </summary>
public class CommonOutput
{
    /// <summary>
    /// Whether Claude should continue after hook execution (default: true)
    /// </summary>
    [JsonPropertyName("continue")]
    public bool? Continue { get; init; }

    /// <summary>
    /// Message shown when continue is false
    /// </summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }

    /// <summary>
    /// Hide stdout from transcript mode (default: false)
    /// </summary>
    [JsonPropertyName("suppressOutput")]
    public bool? SuppressOutput { get; init; }

    /// <summary>
    /// Optional warning message shown to the user
    /// </summary>
    [JsonPropertyName("systemMessage")]
    public string? SystemMessage { get; init; }
}
