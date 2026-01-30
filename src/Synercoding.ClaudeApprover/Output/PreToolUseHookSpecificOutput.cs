using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Output;

/// <summary>
/// Contains the hook-specific output for a pre-tool-use hook, including the permission decision.
/// </summary>
public class PreToolUseHookSpecificOutput
{
    /// <summary>
    /// Gets the hook event name. Defaults to <c>PreToolUse</c>.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public string HookEventName { get; init; } = "PreToolUse";

    /// <summary>
    /// Gets the permission decision for the tool invocation.
    /// </summary>
    [JsonPropertyName("permissionDecision")]
    public PermissionDecision PermissionDecision { get; init; } = PermissionDecision.Ask;

    /// <summary>
    /// Gets the optional reason for the permission decision.
    /// </summary>
    [JsonPropertyName("permissionDecisionReason")]
    public string? PermissionDecisionReason { get; init; }
}
