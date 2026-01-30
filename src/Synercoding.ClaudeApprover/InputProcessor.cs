using Synercoding.ClaudeApprover.Converters;
using Synercoding.ClaudeApprover.Input;
using System.Text.Json;

namespace Synercoding.ClaudeApprover;

/// <summary>
/// Processes Claude Code tool input from a stream by deserializing the JSON into a typed <see cref="ToolInput"/>.
/// </summary>
public static class InputProcessor
{
    /// <summary>
    /// Reads JSON from the given stream and deserializes it into a <see cref="ToolInput"/>.
    /// </summary>
    /// <param name="inputStream">The stream to read JSON from.</param>
    /// <returns>A tuple containing the raw JSON string and the deserialized <see cref="ToolInput"/>, or <c>null</c> if deserialization fails.</returns>
    public static (string Json, ToolInput? ToolInput) Process(Stream inputStream)
    {
        using var reader = new StreamReader(inputStream, leaveOpen: true);

        var input = reader.ReadToEnd();

        try
        {
            return (input, JsonSerializer.Deserialize(input, ToolInputJsonContext.Default.ToolInput));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not parse the provided text as json representing a tool input. JsonSerializer error message: {ex.Message}");
            return (input, null);
        }
    }
}
