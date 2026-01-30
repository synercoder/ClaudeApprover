using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Output;

/// <summary>
/// Represents the permission decision for a tool invocation.
/// </summary>
public enum PermissionDecision
{
    /// <summary>
    /// The tool invocation is allowed.
    /// </summary>
    [JsonStringEnumMemberName("allow")] Allow,

    /// <summary>
    /// The tool invocation is denied.
    /// </summary>
    [JsonStringEnumMemberName("deny")] Deny,

    /// <summary>
    /// The user should be asked for confirmation.
    /// </summary>
    [JsonStringEnumMemberName("ask")] Ask
}
