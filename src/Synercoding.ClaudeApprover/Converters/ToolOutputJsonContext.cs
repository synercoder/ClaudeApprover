using Synercoding.ClaudeApprover.Output;
using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Converters;

/// <summary>
/// Source-generated JSON serializer context for tool output types.
/// </summary>
[JsonSerializable(typeof(CommonOutput))]
[JsonSerializable(typeof(PreToolUseOutput))]
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    Converters = [typeof(JsonStringEnumConverter<PermissionDecision>)]
)]
public partial class ToolOutputJsonContext : JsonSerializerContext
{
}
