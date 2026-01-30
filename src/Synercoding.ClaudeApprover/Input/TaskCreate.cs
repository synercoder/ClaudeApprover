using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the TaskCreate tool invocation.
/// </summary>
public class TaskCreate : IToolInput
{
    /// <summary>
    /// Gets the subject of the task to create.
    /// </summary>
    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    /// <summary>
    /// Gets the description of the task to create.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Gets the active form text shown while the task is in progress.
    /// </summary>
    [JsonPropertyName("activeForm")]
    public required string ActiveForm { get; init; }
}
