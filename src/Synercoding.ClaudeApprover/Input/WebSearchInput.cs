using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the WebSearch tool invocation.
/// </summary>
public class WebSearchInput : IToolInput
{
    /// <summary>
    /// Gets the search query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }
}
