using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Output;

/// <summary>
/// Represents the output of a pre-tool-use hook, including common output properties and the hook-specific permission decision.
/// </summary>
public class PreToolUseOutput : CommonOutput
{
    /// <summary>
    /// Gets the hook-specific output containing the permission decision.
    /// </summary>
    [JsonPropertyName("hookSpecificOutput")]
    public required PreToolUseHookSpecificOutput HookSpecificOutput { get; init; }
}
