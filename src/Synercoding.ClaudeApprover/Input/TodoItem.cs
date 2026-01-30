using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents a single todo item within a <see cref="TodoWriteInput"/>.
/// </summary>
public class TodoItem
{
    /// <summary>
    /// Gets the content of the todo item.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Gets the status of the todo item.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Gets the optional active form text shown while the item is in progress.
    /// </summary>
    [JsonPropertyName("activeForm")]
    public string? ActiveForm { get; init; }
}
