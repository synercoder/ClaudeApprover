using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the TodoWrite tool invocation.
/// </summary>
public class TodoWriteInput : IToolInput
{
    /// <summary>
    /// Gets the array of todo items to write.
    /// </summary>
    [JsonPropertyName("todos")]
    public required TodoItem[] Todos { get; init; }
}
