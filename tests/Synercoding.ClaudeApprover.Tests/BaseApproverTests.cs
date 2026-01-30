using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;

namespace Synercoding.ClaudeApprover.Tests;

public class BaseApproverTests
{
    private static ToolInput _createToolInput(string toolName, IToolInput? input)
    {
        return new ToolInput
        {
            SessionId = Guid.Empty,
            TranscriptPath = "/tmp/transcript.txt",
            CurrentWorkingDirectory = "/home/user",
            HookEventName = "PreToolUse",
            ToolName = toolName,
            Input = input,
        };
    }

    [Fact]
    public void Handle_BashInput_DispatchesToBashHandler()
    {
        var approver = new TestApprover();
        var input = _createToolInput("Bash", new BashInput { Command = "ls" });

        approver.Handle(input);

        approver.LastHandledToolType.Should().Be("Bash");
    }

    [Fact]
    public void Handle_ReadInput_DispatchesToReadHandler()
    {
        var approver = new TestApprover();
        var input = _createToolInput("Read", new ReadInput { FilePath = "/tmp/file.txt" });

        approver.Handle(input);

        approver.LastHandledToolType.Should().Be("Read");
    }

    [Fact]
    public void Handle_NullInput_ReturnsNull()
    {
        var approver = new TestApprover();
        var input = _createToolInput("Unknown", null);

        var result = approver.Handle(input);

        result.Should().BeNull();
    }

    [Fact]
    public void Handle_UnknownToolInput_ReturnsNull()
    {
        var approver = new BaseApprover();
        var input = _createToolInput("Unknown", new UnknownToolInput { RawData = default });

        var result = approver.Handle(input);

        result.Should().BeNull();
    }

    [Fact]
    public void Handle_DefaultBaseApprover_ReturnsNullForAllTools()
    {
        var approver = new BaseApprover();
        var input = _createToolInput("Bash", new BashInput { Command = "ls" });

        var result = approver.Handle(input);

        result.Should().BeNull();
    }

    private class TestApprover : BaseApprover
    {
        public string? LastHandledToolType { get; private set; }

        public override PreToolUseOutput? Handle(ToolInput input, BashInput bash)
        {
            LastHandledToolType = "Bash";
            return Allow();
        }

        public override PreToolUseOutput? Handle(ToolInput input, ReadInput read)
        {
            LastHandledToolType = "Read";
            return Allow();
        }
    }
}
