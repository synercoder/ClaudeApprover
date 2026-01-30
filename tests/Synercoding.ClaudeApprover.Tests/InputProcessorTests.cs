using Synercoding.ClaudeApprover.Input;
using System.Text;

namespace Synercoding.ClaudeApprover.Tests;

public class InputProcessorTests
{
    private static Stream _toStream(string content)
        => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Process_ValidJson_ReturnsToolInput()
    {
        var json = """
        {
            "session_id": "00000000-0000-0000-0000-000000000001",
            "transcript_path": "/tmp/transcript.txt",
            "cwd": "/home/user",
            "permission_mode": "default",
            "hook_event_name": "PreToolUse",
            "tool_name": "Bash",
            "tool_input": {"command":"ls"}
        }
        """;

        var (rawJson, toolInput) = InputProcessor.Process(_toStream(json));

        rawJson.Should().Contain("tool_name");
        toolInput.Should().NotBeNull();
        toolInput!.ToolName.Should().Be("Bash");
        toolInput.Input.Should().BeOfType<BashInput>();
    }

    [Fact]
    public void Process_InvalidJson_ReturnsNullToolInput()
    {
        var (rawJson, toolInput) = InputProcessor.Process(_toStream("not json"));

        rawJson.Should().Be("not json");
        toolInput.Should().BeNull();
    }
}
