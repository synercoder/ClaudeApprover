using Synercoding.ClaudeApprover.PowerShellParser;

namespace Synercoding.ClaudeApprover.Tests.PowerShellParser;

public class PowerShellCommandParserTests
{
    private readonly PowerShellCommandParser _parser = new();

    [Fact]
    public void Parse_SimpleCmdlet_ReturnsSingleCommand()
    {
        var pipeline = _parser.Parse("Get-Location");

        pipeline.Commands.Should().HaveCount(1);
        pipeline.Commands[0].Executable.Should().Be("Get-Location");
        pipeline.Commands[0].Arguments.Should().BeEmpty();
        pipeline.Operator.Should().BeNull();
        pipeline.NextPipeline.Should().BeNull();
    }

    [Fact]
    public void Parse_CmdletWithParameters_ParsesArguments()
    {
        var pipeline = _parser.Parse("Select-Object -Last 20");

        pipeline.Commands[0].Executable.Should().Be("Select-Object");
        pipeline.Commands[0].Arguments.Should().Equal("-Last", "20");
    }

    [Fact]
    public void Parse_PipeOperator_AddsBothCommandsToPipeline()
    {
        var pipeline = _parser.Parse("Get-Content x.txt | ConvertFrom-Json");

        pipeline.Commands.Should().HaveCount(2);
        pipeline.Commands[0].Executable.Should().Be("Get-Content");
        pipeline.Commands[1].Executable.Should().Be("ConvertFrom-Json");
        pipeline.Operator.Should().Be("|");
    }

    [Fact]
    public void Parse_SemicolonStatementChain_CreatesNextPipeline()
    {
        var pipeline = _parser.Parse("Set-Location foo; Get-ChildItem");

        pipeline.Commands[0].Executable.Should().Be("Set-Location");
        pipeline.Operator.Should().Be(";");
        pipeline.NextPipeline.Should().NotBeNull();
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("Get-ChildItem");
    }

    [Fact]
    public void Parse_NewlineStatementChain_CreatesNextPipeline()
    {
        var pipeline = _parser.Parse("Write-Host first\nWrite-Host second");

        pipeline.Commands[0].Executable.Should().Be("Write-Host");
        pipeline.Commands[0].Arguments.Should().Equal("first");
        pipeline.Operator.Should().Be(";");
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("Write-Host");
        pipeline.NextPipeline.Commands[0].Arguments.Should().Equal("second");
    }

    [Fact]
    public void Parse_AndOperator_CreatesNextPipelineWithAndOperator()
    {
        var pipeline = _parser.Parse("dotnet build && dotnet test");

        pipeline.Commands[0].Executable.Should().Be("dotnet");
        pipeline.Operator.Should().Be("&&");
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("dotnet");
    }

    [Fact]
    public void Parse_OrOperator_CreatesNextPipelineWithOrOperator()
    {
        var pipeline = _parser.Parse("dotnet build || Write-Host failed");

        pipeline.Commands[0].Executable.Should().Be("dotnet");
        pipeline.Operator.Should().Be("||");
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("Write-Host");
    }

    [Fact]
    public void Parse_DoubleQuotedArgument_StripsQuotes()
    {
        var pipeline = _parser.Parse("Write-Host \"hello world\"");

        pipeline.Commands[0].Arguments.Should().Equal("hello world");
    }

    [Fact]
    public void Parse_SingleQuotedArgument_StripsQuotes()
    {
        var pipeline = _parser.Parse("Write-Host 'hello world'");

        pipeline.Commands[0].Arguments.Should().Equal("hello world");
    }

    [Fact]
    public void Parse_SingleQuotedEscapedQuote_DoubledSingleQuote()
    {
        var pipeline = _parser.Parse("Write-Host 'it''s here'");

        pipeline.Commands[0].Arguments.Should().Equal("it's here");
    }

    [Fact]
    public void Parse_DoubleQuotedBacktickEscape_TranslatesEscape()
    {
        var pipeline = _parser.Parse("Write-Host \"hello`nworld\"");

        pipeline.Commands[0].Arguments.Should().Equal("hello\nworld");
    }

    [Fact]
    public void Parse_ArraySubexpression_PreservedAsSingleToken()
    {
        var pipeline = _parser.Parse("./script.ps1 -Files @('a.json','b.json')");

        pipeline.Commands[0].Executable.Should().Be("./script.ps1");
        pipeline.Commands[0].Arguments.Should().Equal("-Files", "@('a.json','b.json')");
    }

    [Fact]
    public void Parse_HashtableLiteral_PreservedAsSingleTokenAndSemicolonInsideIsNotASeparator()
    {
        var pipeline = _parser.Parse("$x = @{ A = 1; B = 2 }");

        pipeline.Commands[0].Executable.Should().Be(PowerShellCommandParser.ASSIGNMENT_SENTINEL);
        pipeline.Commands[0].Arguments.Should().ContainSingle()
            .Which.Should().Be("@{ A = 1; B = 2 }");
        pipeline.NextPipeline.Should().BeNull();
    }

    [Fact]
    public void Parse_ScriptBlock_PreservedAsSingleTokenAndPipeInsideIsNotASeparator()
    {
        var pipeline = _parser.Parse("ForEach-Object { $_ | Write-Host }");

        pipeline.Commands.Should().HaveCount(1);
        pipeline.Commands[0].Executable.Should().Be("ForEach-Object");
        pipeline.Commands[0].Arguments.Should().ContainSingle()
            .Which.Should().Be("{ $_ | Write-Host }");
    }

    [Fact]
    public void Parse_HereStringSingleQuoted_CollectsMultilineContentVerbatim()
    {
        var script = "$x = @'\nfirst\nsecond;third | fourth\n'@\nWrite-Host done";

        var pipeline = _parser.Parse(script);

        pipeline.Commands[0].Executable.Should().Be(PowerShellCommandParser.ASSIGNMENT_SENTINEL);
        pipeline.Commands[0].Arguments.Should().ContainSingle()
            .Which.Should().Be("first\nsecond;third | fourth");
        pipeline.NextPipeline.Should().NotBeNull();
        pipeline.NextPipeline!.Commands[0].Executable.Should().Be("Write-Host");
    }

    [Fact]
    public void Parse_OutputRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("Write-Host hello > output.txt");

        pipeline.Commands[0].Executable.Should().Be("Write-Host");
        pipeline.Commands[0].Arguments.Should().Equal("hello");
        pipeline.Commands[0].Redirections.Should().ContainSingle();
        pipeline.Commands[0].Redirections[0].Type.Should().Be(">");
        pipeline.Commands[0].Redirections[0].Target.Should().Be("output.txt");
    }

    [Fact]
    public void Parse_AppendRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("Write-Host hello >> output.txt");

        pipeline.Commands[0].Redirections[0].Type.Should().Be(">>");
    }

    [Fact]
    public void Parse_StderrRedirection_ParsesCorrectly()
    {
        var pipeline = _parser.Parse("command 2> error.log");

        pipeline.Commands[0].Redirections[0].Type.Should().Be("2>");
        pipeline.Commands[0].Redirections[0].Target.Should().Be("error.log");
    }

    [Fact]
    public void Parse_StreamMergeRedirection_CapturesMergeTarget()
    {
        var pipeline = _parser.Parse("command 2>&1");

        pipeline.Commands[0].Redirections.Should().ContainSingle();
        pipeline.Commands[0].Redirections[0].Type.Should().Be("2>");
        pipeline.Commands[0].Redirections[0].Target.Should().Be("&1");
    }

    [Fact]
    public void Parse_AllStreamsRedirection_CapturesStarForm()
    {
        var pipeline = _parser.Parse("command *>&1");

        pipeline.Commands[0].Redirections[0].Type.Should().Be("*>");
        pipeline.Commands[0].Redirections[0].Target.Should().Be("&1");
    }

    [Fact]
    public void Parse_VariableAssignmentWithCmdletRhs_ExtractsCmdletAsExecutable()
    {
        var pipeline = _parser.Parse("$j = Get-Content $f -Raw");

        pipeline.Commands[0].Executable.Should().Be("Get-Content");
        pipeline.Commands[0].Arguments.Should().Equal("$f", "-Raw");
    }

    [Fact]
    public void Parse_VariableAssignmentWithCmdletRhsAndPipe_PipesExtractedCmdlet()
    {
        var pipeline = _parser.Parse("$j = Get-Content $f -Raw | ConvertFrom-Json");

        pipeline.Commands.Should().HaveCount(2);
        pipeline.Commands[0].Executable.Should().Be("Get-Content");
        pipeline.Commands[1].Executable.Should().Be("ConvertFrom-Json");
    }

    [Fact]
    public void Parse_VariableAssignmentWithStringLiteralRhs_EmitsAssignmentSentinel()
    {
        var pipeline = _parser.Parse("$f = 'some/path.json'");

        pipeline.Commands[0].Executable.Should().Be(PowerShellCommandParser.ASSIGNMENT_SENTINEL);
        pipeline.Commands[0].Arguments.Should().Equal("some/path.json");
    }

    [Fact]
    public void Parse_VariableAssignmentNoSpaces_StillDetected()
    {
        var pipeline = _parser.Parse("$x=Get-Date");

        pipeline.Commands[0].Executable.Should().Be("Get-Date");
        pipeline.Commands[0].Arguments.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EqualityComparisonNotMistakenForAssignment()
    {
        // $x -eq 1 is a comparison expression; our assignment detector must NOT swallow $x.
        var pipeline = _parser.Parse("Write-Host ($x -eq 1)");

        pipeline.Commands[0].Executable.Should().Be("Write-Host");
        pipeline.Commands[0].Arguments.Should().Equal("($x -eq 1)");
    }

    [Fact]
    public void Parse_ScopedVariableAssignment_DetectedCorrectly()
    {
        var pipeline = _parser.Parse("$script:x = 1");

        pipeline.Commands[0].Executable.Should().Be(PowerShellCommandParser.ASSIGNMENT_SENTINEL);
    }

    [Fact]
    public void Parse_CallOperatorPrefix_UsesNextTokenAsExecutable()
    {
        var pipeline = _parser.Parse("& myScript.ps1 -Arg value");

        pipeline.Commands[0].Executable.Should().Be("myScript.ps1");
        pipeline.Commands[0].Arguments.Should().Equal("-Arg", "value");
    }

    [Fact]
    public void Parse_WindowsQuotedPath_PreservesBackslashes()
    {
        var pipeline = _parser.Parse("Set-Location \"C:\\Git\\project\"");

        pipeline.Commands[0].Executable.Should().Be("Set-Location");
        pipeline.Commands[0].Arguments.Should().Equal(@"C:\Git\project");
    }

    [Fact]
    public void Parse_RelativeScriptInvocation_ParsesAsExecutable()
    {
        var pipeline = _parser.Parse(".\\Run-Tests.ps1 -Environment 'local-docker'");

        pipeline.Commands[0].Executable.Should().Be(".\\Run-Tests.ps1");
        pipeline.Commands[0].Arguments.Should().Equal("-Environment", "local-docker");
    }

    [Fact]
    public void Parse_TypeCastAndMethodCall_StaysSingleToken()
    {
        var pipeline = _parser.Parse("Write-Output [System.Text.Encoding]::UTF8.GetString([byte[]]$data)");

        pipeline.Commands[0].Executable.Should().Be("Write-Output");
        pipeline.Commands[0].Arguments.Should().Equal("[System.Text.Encoding]::UTF8.GetString([byte[]]$data)");
    }

    [Fact]
    public void Parse_EmptyInput_ThrowsInvalidOperationException()
    {
        var act = () => _parser.Parse("");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_CommentOnly_ThrowsInvalidOperationException()
    {
        var act = () => _parser.Parse("# just a comment");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_CommentBeforeCommand_IgnoresComment()
    {
        var pipeline = _parser.Parse("# header comment\nGet-ChildItem");

        pipeline.Commands[0].Executable.Should().Be("Get-ChildItem");
    }

    [Fact]
    public void Parse_InlineCommentAfterCommand_IgnoresComment()
    {
        var pipeline = _parser.Parse("Get-ChildItem -Force # list hidden too");

        pipeline.Commands[0].Executable.Should().Be("Get-ChildItem");
        pipeline.Commands[0].Arguments.Should().Equal("-Force");
    }

    [Fact]
    public void Parse_ChainedSemicolons_ParsesRecursively()
    {
        var pipeline = _parser.Parse("cd a; cd b; cd c");

        pipeline.Commands[0].Executable.Should().Be("cd");
        pipeline.Operator.Should().Be(";");

        var second = pipeline.NextPipeline!;
        second.Commands[0].Executable.Should().Be("cd");
        second.Operator.Should().Be(";");

        var third = second.NextPipeline!;
        third.Commands[0].Executable.Should().Be("cd");
        third.Operator.Should().BeNull();
    }

    [Fact]
    public void Parse_TrailingSemicolon_DoesNotCreateEmptyNextPipeline()
    {
        var pipeline = _parser.Parse("Get-ChildItem;");

        pipeline.Commands[0].Executable.Should().Be("Get-ChildItem");
        pipeline.NextPipeline.Should().BeNull();
    }

    [Fact]
    public void Parse_RealWorldCollectionExample_ParsesPipelineAndChain()
    {
        var command = "Set-Location \"C:\\Git\\Print_API_Core\\Print_API_New\\tests\\web\\PrintApi.Web.Api.Postman\"; "
                    + ".\\Run-Tests.ps1 -Environment 'local-docker' "
                    + "-CollectionFiles @('Collections/Auth/Auth-Valid-Token.json','Collections/Auth/Auth-No-Token.json') "
                    + "-OutputFormat 'Concise' 2>&1 | Select-Object -Last 20";

        var pipeline = _parser.Parse(command);

        pipeline.Commands[0].Executable.Should().Be("Set-Location");
        pipeline.Operator.Should().Be(";");

        var next = pipeline.NextPipeline!;
        next.Commands.Should().HaveCount(2);
        next.Commands[0].Executable.Should().Be(".\\Run-Tests.ps1");
        next.Commands[0].Redirections.Should().ContainSingle();
        next.Commands[0].Redirections[0].Type.Should().Be("2>");
        next.Commands[0].Redirections[0].Target.Should().Be("&1");
        next.Commands[1].Executable.Should().Be("Select-Object");
        next.Commands[1].Arguments.Should().Equal("-Last", "20");
    }

    [Fact]
    public void Parse_RealWorldForeachJsonExample_ParsesEachStatement()
    {
        var command = "$f='C:/some/file.json'\n"
                    + "$j=Get-Content $f -Raw | ConvertFrom-Json\n"
                    + "foreach($ex in $j.run.executions){ Write-Output $ex.name }";

        var pipeline = _parser.Parse(command);

        pipeline.Commands[0].Executable.Should().Be(PowerShellCommandParser.ASSIGNMENT_SENTINEL);

        var second = pipeline.NextPipeline!;
        second.Commands.Should().HaveCount(2);
        second.Commands[0].Executable.Should().Be("Get-Content");
        second.Commands[1].Executable.Should().Be("ConvertFrom-Json");

        var third = second.NextPipeline!;
        // `foreach(...)` has no space before the paren, so the keyword and grouped
        // body fuse into one token — approvers see an unknown executable and can
        // ask the user, which is the desired safe default for control-flow blocks.
        third.Commands[0].Executable.Should().StartWith("foreach(");
    }
}
