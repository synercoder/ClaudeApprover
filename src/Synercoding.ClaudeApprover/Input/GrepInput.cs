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
    public string? Path { get; init; }

    /// <summary>
    /// Gets the output mode (e.g., "content", "files_with_matches", "count").
    /// </summary>
    [JsonPropertyName("output_mode")]
    public string? OutputMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the search should be case insensitive.
    /// </summary>
    [JsonPropertyName("-i")]
    public bool? IgnoreCase { get; init; }

    /// <summary>
    /// Gets the glob pattern to filter files (e.g., "*.js", "*.{ts,tsx}").
    /// </summary>
    [JsonPropertyName("glob")]
    public string? Glob { get; init; }

    /// <summary>
    /// Gets the file type to search (e.g., "js", "py", "rust").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets the maximum number of results to return.
    /// </summary>
    [JsonPropertyName("head_limit")]
    public int? HeadLimit { get; init; }

    /// <summary>
    /// Gets the number of entries to skip before applying head limit.
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; init; }

    /// <summary>
    /// Gets the number of lines to show after each match.
    /// </summary>
    [JsonPropertyName("-A")]
    public int? AfterContext { get; init; }

    /// <summary>
    /// Gets the number of lines to show before each match.
    /// </summary>
    [JsonPropertyName("-B")]
    public int? BeforeContext { get; init; }

    /// <summary>
    /// Gets the number of lines to show before and after each match.
    /// </summary>
    [JsonPropertyName("-C")]
    public int? Context { get; init; }

    /// <summary>
    /// Gets the number of lines to show before and after each match (alias for <see cref="Context"/>).
    /// </summary>
    [JsonPropertyName("context")]
    public int? ContextAlias { get; init; }

    /// <summary>
    /// Gets a value indicating whether to show line numbers in output.
    /// </summary>
    [JsonPropertyName("-n")]
    public bool? LineNumbers { get; init; }

    /// <summary>
    /// Gets a value indicating whether to enable multiline matching.
    /// </summary>
    [JsonPropertyName("multiline")]
    public bool? Multiline { get; init; }
}
