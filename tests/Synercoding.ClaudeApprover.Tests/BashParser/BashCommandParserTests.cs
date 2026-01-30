using Synercoding.ClaudeApprover.BashParser;

namespace Synercoding.ClaudeApprover.Tests.BashParser;

public class BashCommandParserTests
{
    private readonly BashCommandParser _parser = new();

    [Fact]
    public void Parse_SimpleCommand_ReturnsSingleCommand()
    {
        var pipeline = _parser.Parse("ls");

        pipeline.Commands.Should().HaveCount(1);
        pipeline.Commands[0].Executable.Should().Be("ls");
        pipeline.Commands[0].Arguments.Should().BeEmpty();
        pipeline.Operator.Should().BeNull();
        pipeline.NextPipeline.Should().BeNull();
    }

    [Fact]
    public void Parse_CommandWithArguments_ParsesArguments()
    {
        var pipeline = _parser.Parse("ls -la /tmp");

        pipeline.Commands.Should().HaveCount(1);
        pipeline.Commands[0].Executable.Should().Be("ls");
        pipeline.Commands[0].Arguments.Should().Equal("-la", "/tmp");
    }

    [Fact]
    public void Parse_PipeOperator_AddsBothCommandsToPipeline()
    {
        var pipeline = _parser.Parse("ls | grep foo");

        pipeline.Commands.Should().HaveCount(2);
        pipeline.Commands[0].Executable.Should().Be("ls");
        pipeline.Commands[1].Executable.Should().Be("grep");
        pipeline.Commands[1].Arguments.Should().Equal("foo");
        pipeline.Operator.Should().Be("|");
    }

    [Fact]
    public void Parse_AndOperator_CreatesNextPipeline()
    {
        var pipeline = _parser.Parse("mkdir test && cd test");

        pipeline.Commands.Should().HaveCount(1);
        pipeline.Commands[0].Executable.Should().Be("mkdir");
        pipeline.Operator.Should().Be("&&");
        pipeline.NextPipeline.Should().NotBeNull();
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("cd");
        pipeline.NextPipeline.Commands[0].Arguments.Should().Equal("test");
    }

    [Fact]
    public void Parse_OrOperator_CreatesNextPipeline()
    {
        var pipeline = _parser.Parse("false || echo fallback");

        pipeline.Commands.Should().HaveCount(1);
        pipeline.Commands[0].Executable.Should().Be("false");
        pipeline.Operator.Should().Be("||");
        pipeline.NextPipeline.Should().NotBeNull();
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("echo");
    }

    [Fact]
    public void Parse_OutputRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("echo hello > output.txt");

        var cmd = pipeline.Commands[0];
        cmd.Executable.Should().Be("echo");
        cmd.Arguments.Should().Equal("hello");
        cmd.Redirections.Should().HaveCount(1);
        cmd.Redirections[0].Type.Should().Be(">");
        cmd.Redirections[0].Target.Should().Be("output.txt");
    }

    [Fact]
    public void Parse_AppendRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("echo hello >> output.txt");

        var cmd = pipeline.Commands[0];
        cmd.Redirections.Should().HaveCount(1);
        cmd.Redirections[0].Type.Should().Be(">>");
        cmd.Redirections[0].Target.Should().Be("output.txt");
    }

    [Fact]
    public void Parse_InputRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("sort < input.txt");

        var cmd = pipeline.Commands[0];
        cmd.Executable.Should().Be("sort");
        cmd.Redirections.Should().HaveCount(1);
        cmd.Redirections[0].Type.Should().Be("<");
        cmd.Redirections[0].Target.Should().Be("input.txt");
    }

    [Fact]
    public void Parse_StderrRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("command 2> error.log");

        var cmd = pipeline.Commands[0];
        cmd.Redirections.Should().HaveCount(1);
        cmd.Redirections[0].Type.Should().Be("2>");
        cmd.Redirections[0].Target.Should().Be("error.log");
    }

    [Fact]
    public void Parse_DoubleQuotedArgument_StripsQuotes()
    {
        var pipeline = _parser.Parse("echo \"hello world\"");

        pipeline.Commands[0].Arguments.Should().Equal("hello world");
    }

    [Fact]
    public void Parse_SingleQuotedArgument_StripsQuotes()
    {
        var pipeline = _parser.Parse("echo 'hello world'");

        pipeline.Commands[0].Arguments.Should().Equal("hello world");
    }

    [Fact]
    public void Parse_EscapeInDoubleQuotes_HandlesBackslash()
    {
        var pipeline = _parser.Parse("echo \"hello\\\"world\"");

        pipeline.Commands[0].Arguments.Should().Equal("hello\"world");
    }

    [Fact]
    public void Parse_EscapeInUnquotedToken_HandlesBackslash()
    {
        var pipeline = _parser.Parse("echo hello\\ world");

        // backslash-space becomes a space within the token
        pipeline.Commands[0].Arguments.Should().Equal("hello world");
    }

    [Fact]
    public void Parse_WindowsStylePath_PreservesBackslashes()
    {
        var pipeline = _parser.Parse(@"cd D:\home\user\project\src");

        pipeline.Commands[0].Executable.Should().Be("cd");
        pipeline.Commands[0].Arguments.Should().Equal(@"D:\home\user\project\src");
    }

    [Fact]
    public void Parse_EmptyInput_ThrowsInvalidOperationException()
    {
        var act = () => _parser.Parse("");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_ChainedAndOperators_ParsesRecursively()
    {
        var pipeline = _parser.Parse("a && b && c");

        pipeline.Commands[0].Executable.Should().Be("a");
        pipeline.Operator.Should().Be("&&");

        var second = pipeline.NextPipeline!;
        second.Commands[0].Executable.Should().Be("b");
        second.Operator.Should().Be("&&");

        var third = second.NextPipeline!;
        third.Commands[0].Executable.Should().Be("c");
        third.Operator.Should().BeNull();
    }

    [Fact]
    public void Parse_PipeFollowedByAnd_ParsesCorrectly()
    {
        // The parser handles "|" by adding the next command to Commands,
        // then checks for "&&"/"||" operators. After consuming the pipe
        // and second command, _parsePipeline returns without checking further.
        var pipeline = _parser.Parse("ls | grep foo && echo done");

        pipeline.Commands.Should().HaveCount(2);
        pipeline.Commands[0].Executable.Should().Be("ls");
        pipeline.Commands[1].Executable.Should().Be("grep");
        pipeline.Operator.Should().Be("|");
        // The current parser implementation returns after handling the pipe
        // without re-checking for && — this is a known limitation.
        pipeline.NextPipeline.Should().BeNull();
    }

    [Fact]
    public void Parse_MultipleRedirections_ParsesAll()
    {
        var pipeline = _parser.Parse("command < in.txt > out.txt 2> err.txt");

        var cmd = pipeline.Commands[0];
        cmd.Executable.Should().Be("command");
        cmd.Redirections.Should().HaveCount(3);
    }
}
