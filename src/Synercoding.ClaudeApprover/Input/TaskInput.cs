using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Task tool invocation.
/// </summary>
public class TaskInput : IToolInput
{
    /// <summary>
    /// Gets the optional description of the task.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional prompt for the task.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>
    /// Gets the optional sub-agent type for the task.
    /// </summary>
    [JsonPropertyName("subagent_type")]
    public string? SubAgentType { get; init; }
}
