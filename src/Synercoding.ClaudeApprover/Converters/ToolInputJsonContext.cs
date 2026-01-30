using Synercoding.ClaudeApprover.Input;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synercoding.ClaudeApprover.Converters;

/// <summary>
/// Source-generated JSON serializer context for tool input types.
/// </summary>
[JsonSerializable(typeof(ToolInput))]
[JsonSerializable(typeof(BashInput))]
[JsonSerializable(typeof(EditInput))]
[JsonSerializable(typeof(ReadInput))]
[JsonSerializable(typeof(WriteInput))]
[JsonSerializable(typeof(WebFetchInput))]
[JsonSerializable(typeof(WebSearchInput))]
[JsonSerializable(typeof(TodoWriteInput))]
[JsonSerializable(typeof(TaskInput))]
[JsonSerializable(typeof(GlobInput))]
[JsonSerializable(typeof(TaskCreate))]
[JsonSerializable(typeof(TaskUpdate))]
[JsonSerializable(typeof(ExitPlanMode))]
[JsonSerializable(typeof(UnknownToolInput))]
[JsonSerializable(typeof(JsonElement))]
public partial class ToolInputJsonContext : JsonSerializerContext
{
}
