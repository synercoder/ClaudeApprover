using Synercoding.ClaudeApprover.Input;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Synercoding.ClaudeApprover.Converters;

/// <summary>
/// Custom JSON converter for <see cref="ToolInput"/> that deserializes the tool input based on the <c>tool_name</c> property.
/// </summary>
public class ToolInputConverter : JsonConverter<ToolInput>
{
    /// <inheritdoc />
    public override ToolInput Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Read the entire JSON object into a JsonDocument
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Extract the tool_name
        var toolName = root.GetProperty("tool_name").GetString() ?? throw new JsonException();

        // Determine the type based on tool_name
        JsonTypeInfo inputTypeInfo = toolName switch
        {
            "Bash" => ToolInputJsonContext.Default.BashInput,
            "Edit" => ToolInputJsonContext.Default.EditInput,
            "Read" => ToolInputJsonContext.Default.ReadInput,
            "Write" => ToolInputJsonContext.Default.WriteInput,
            "WebFetch" => ToolInputJsonContext.Default.WebFetchInput,
            "WebSearch" => ToolInputJsonContext.Default.WebSearchInput,
            "Task" => ToolInputJsonContext.Default.TaskInput,
            "TodoWrite" => ToolInputJsonContext.Default.TodoWriteInput,
            "Glob" => ToolInputJsonContext.Default.GlobInput,
            "TaskCreate" => ToolInputJsonContext.Default.TaskCreate,
            "TaskUpdate" => ToolInputJsonContext.Default.TaskUpdate,
            "ExitPlanMode" => ToolInputJsonContext.Default.ExitPlanMode,
            _ => ToolInputJsonContext.Default.UnknownToolInput
        };

        // Deserialize tool_input with the correct type
        IToolInput? toolInput;
        if (root.TryGetProperty("tool_input", out JsonElement inputElement))
        {
            toolInput = inputTypeInfo == ToolInputJsonContext.Default.UnknownToolInput
                    ? new UnknownToolInput() { RawData = inputElement }
                    : (IToolInput?)JsonSerializer.Deserialize(inputElement, inputTypeInfo);
        }
        else
        {
            toolInput = null;
        }

        // Create and populate the result
        return new ToolInput
        {
            SessionId = new Guid(root.GetProperty("session_id").GetString() ?? throw new JsonException()),
            TranscriptPath = root.GetProperty("transcript_path").GetString() ?? throw new JsonException(),
            CurrentWorkingDirectory = root.GetProperty("cwd").GetString() ?? throw new JsonException(),
            PermissionMode = root.GetProperty("permission_mode").GetString(),
            HookEventName = root.GetProperty("hook_event_name").GetString() ?? throw new JsonException(),
            ToolName = toolName,
            Input = toolInput
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ToolInput value,
        JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
