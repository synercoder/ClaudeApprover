using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the TaskUpdate tool invocation.
/// </summary>
public class TaskUpdate : IToolInput
{
    /// <summary>
    /// Gets the ID of the task to update.
    /// </summary>
    [JsonPropertyName("taskId")]
    public required string TaskId { get; init; }

    /// <summary>
    /// Gets the new status for the task.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
