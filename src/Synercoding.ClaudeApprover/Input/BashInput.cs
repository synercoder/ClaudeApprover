using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the Bash tool invocation.
/// </summary>
public class BashInput : IToolInput
{
    /// <summary>
    /// Gets the bash command to execute.
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Gets the optional timeout in milliseconds.
    /// </summary>
    [JsonPropertyName("timeout")]
    public int? TimeOut { get; init; }

    /// <summary>
    /// Gets the optional description of the command.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
