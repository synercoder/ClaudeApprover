using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for the ExitPlanMode tool invocation.
/// </summary>
public class ExitPlanMode : IToolInput
{
    /// <summary>
    /// Represents the plan that was formed
    /// </summary>
    [JsonPropertyName("plan")]
    public string? Plan { get; set; }
}
