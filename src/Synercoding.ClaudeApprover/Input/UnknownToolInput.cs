using System.Text.Json;

namespace Synercoding.ClaudeApprover.Input;

/// <summary>
/// Represents input for an unrecognized tool, preserving the raw JSON data.
/// </summary>
public class UnknownToolInput : IToolInput
{
    /// <summary>
    /// Gets or sets the raw JSON data of the unknown tool input.
    /// </summary>
    public JsonElement RawData { get; set; }
}
