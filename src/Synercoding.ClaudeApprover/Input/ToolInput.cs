using Synercoding.ClaudeApprover.Converters;
using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents the top-level tool input envelope received from Claude Code via stdin.
/// </summary>
[JsonConverter(typeof(ToolInputConverter))]
public class ToolInput
{
    /// <summary>
    /// Gets the session ID of the Claude Code session.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Gets the path to the conversation transcript.
    /// </summary>
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets the current working directory of the Claude Code session.
    /// </summary>
    public required string CurrentWorkingDirectory { get; init; }

    /// <summary>
    /// Gets the permission mode of the session.
    /// </summary>
    public string? PermissionMode { get; init; }

    /// <summary>
    /// Gets the name of the hook event (e.g. <c>PreToolUse</c>).
    /// </summary>
    public required string HookEventName { get; init; }

    /// <summary>
    /// Gets the name of the tool being invoked.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the typed tool input, or <c>null</c> if not present.
    /// </summary>
    public IToolInput? Input { get; init; }
}
