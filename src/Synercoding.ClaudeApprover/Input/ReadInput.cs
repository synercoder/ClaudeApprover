using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Read tool invocation.
/// </summary>
public class ReadInput : IToolInput
{
    /// <summary>
    /// Gets the file path to read.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }
}
