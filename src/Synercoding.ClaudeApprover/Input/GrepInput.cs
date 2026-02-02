using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Grep tool invocation.
/// </summary>
public class GrepInput : IToolInput
{
    /// <summary>
    /// Gets the regular expression pattern to search for.
    /// </summary>
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    /// <summary>
    /// Gets the file or directory path to search in.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Gets the output mode (e.g., "content", "files_with_matches", "count").
    /// </summary>
    [JsonPropertyName("output_mode")]
    public string? OutputMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the search should be case insensitive.
    /// </summary>
    [JsonPropertyName("-i")]
    public string? IgnoreCase { get; init; }
}
