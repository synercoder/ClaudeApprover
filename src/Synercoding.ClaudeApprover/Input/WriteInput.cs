using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Write tool invocation.
/// </summary>
public class WriteInput : IToolInput
{
    /// <summary>
    /// Gets the file path to write to.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the content to write to the file.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
