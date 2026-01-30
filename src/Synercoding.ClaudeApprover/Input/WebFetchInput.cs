using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the WebFetch tool invocation.
/// </summary>
public class WebFetchInput : IToolInput
{
    /// <summary>
    /// Gets the URL to fetch.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// Gets the optional prompt to process the fetched content with.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }
}
