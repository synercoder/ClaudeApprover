using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Edit tool invocation.
/// </summary>
public class EditInput : IToolInput
{
    /// <summary>
    /// Gets the file path to edit.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the old string to replace.
    /// </summary>
    [JsonPropertyName("old_string")]
    public string? Old { get; init; }

    /// <summary>
    /// Gets the new string to replace with.
    /// </summary>
    [JsonPropertyName("new_string")]
    public string? New { get; init; }
}
