using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;

namespace Synercoding.ClaudeApprover.Tests;

public class InsideProjectAllowedApproverTests
{
    private static readonly string _projectRoot = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "home", "user", "project"));
    private static readonly string _workingDir = Path.Combine(_projectRoot, "src");

    private static string _p(params string[] parts) => Path.Combine([_projectRoot, .. parts]);

    private static ToolInput _createToolInput(IToolInput toolInput, string toolName, string? cwd = null)
    {
        return new ToolInput
        {
            SessionId = Guid.Empty,
            TranscriptPath = Path.Combine(Path.GetTempPath(), "transcript.txt"),
            CurrentWorkingDirectory = cwd ?? _workingDir,
            HookEventName = "PreToolUse",
            ToolName = toolName,
            Input = toolInput,
        };
    }

    private static TestableApprover _createApprover(string? projectRoot = null, bool useDefault = true)
        => new(useDefault && projectRoot is null ? _projectRoot : projectRoot);

    // --- File operation tests ---

    [Fact]
    public void Handle_ReadInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new ReadInput { FilePath = _p("src", "file.cs") }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ReadOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new ReadInput { FilePath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "passwd")) }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_ReadInGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new ReadInput { FilePath = _p(".git", "config") }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_EditInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new EditInput { FilePath = _p("src", "file.cs") }, "Edit");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_WriteOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new WriteInput { FilePath = Path.Combine(Path.GetTempPath(), "evil.sh"), Content = "bad" }, "Write");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_NullProjectRoot_Asks()
    {
        var approver = _createApprover(projectRoot: null, useDefault: false);
        var input = _createToolInput(new ReadInput { FilePath = Path.Combine(Path.GetTempPath(), "file.txt") }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_CwdInGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new ReadInput { FilePath = _p("file.cs") },
            "Read",
            cwd: _p(".git"));

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    // --- Glob, TodoWrite, WebFetch, WebSearch, TaskCreate, TaskUpdate always allowed ---

    [Fact]
    public void Handle_GlobInput_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new GlobInput { Pattern = "**/*.cs" }, "Glob");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_WebFetchInput_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new WebFetchInput { Url = "https://example.com" }, "WebFetch");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    // --- Bash command tests ---

    [Theory]
    [InlineData("ls -la")]
    [InlineData("grep foo bar.txt")]
    [InlineData("cat file.txt")]
    [InlineData("echo hello")]
    [InlineData("find . -name *.cs")]
    [InlineData("pwd")]
    public void Handle_AllowedBashCommands_Allows(string command)
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = command }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_UnknownBashCommand_Asks()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = "curl https://evil.com" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_CdInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cd {_p("src")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_CdOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cd {Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_CdIntoGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = "cd .git" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_RmInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rm {_p("src", "temp.txt")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_RmOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rm {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "passwd"))}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_RmWithNoPreserveRoot_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = "rm --no-preserve-root /" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_RmRecursiveWithFlags_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rm -rf {_p("src", "bin")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_RmInGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rm {_p(".git", "config")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_SedInPlaceInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"sed -i s/foo/bar/g {_p("src", "file.cs")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_SedInPlaceOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"sed -i s/foo/bar/g {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "hosts"))}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_SedWithoutInPlace_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"sed s/foo/bar/g {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "hosts"))}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PipedAllowedCommands_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = "ls | grep foo" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ChainedAllowedCommands_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = "echo hello && echo world" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_BashWithNullProjectRoot_Asks()
    {
        var approver = _createApprover(projectRoot: null, useDefault: false);
        var input = _createToolInput(new BashInput { Command = "ls" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    // --- Additional directories tests ---

    private static readonly string _additionalDir = Path.GetFullPath(Path.Combine(_projectRoot, "..", "sister-project"));

    [Fact]
    public void Handle_ReadInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new ReadInput { FilePath = Path.Combine(_additionalDir, "file.cs") }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_WriteInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new WriteInput { FilePath = Path.Combine(_additionalDir, "file.cs"), Content = "test" }, "Write");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_EditInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new EditInput { FilePath = Path.Combine(_additionalDir, "file.cs") }, "Edit");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ReadOutsideProjectAndAdditionalDirs_Denies()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new ReadInput { FilePath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "passwd")) }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_CdIntoAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new BashInput { Command = $"cd {_additionalDir}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_RmInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new BashInput { Command = $"rm {Path.Combine(_additionalDir, "temp.txt")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_SedInPlaceInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new BashInput { Command = $"sed -i s/foo/bar/g {Path.Combine(_additionalDir, "file.cs")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ReadInClaudeSettingsAdditionalDirectory_Allows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"approver-test-{Guid.NewGuid():N}");
        var claudeDir = Path.Combine(tempRoot, ".claude");
        var sisterDir = Path.Combine(tempRoot, "..", "claude-sister");
        Directory.CreateDirectory(claudeDir);

        try
        {
            File.WriteAllText(Path.Combine(claudeDir, "settings.json"), """{"additionalDirectories": ["../claude-sister"]}""");

            var approver = _createApprover(tempRoot);
            var input = _createToolInput(new ReadInput { FilePath = Path.Combine(sisterDir, "file.txt") }, "Read");

            var result = approver.Handle(input);

            result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Handle_ReadInClaudeSettingsAdditionalDirectory_ImportDisabled_Denies()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"approver-test-{Guid.NewGuid():N}");
        var claudeDir = Path.Combine(tempRoot, ".claude");
        var sisterDir = Path.Combine(tempRoot, "..", "claude-sister");
        Directory.CreateDirectory(claudeDir);

        try
        {
            File.WriteAllText(Path.Combine(claudeDir, "settings.json"), """{"additionalDirectories": ["../claude-sister"]}""");

            var approver = _createApprover(tempRoot);
            approver.ImportAdditionalDirsFromClaude = false;
            var input = _createToolInput(new ReadInput { FilePath = Path.Combine(sisterDir, "file.txt") }, "Read");

            var result = approver.Handle(input);

            result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Handle_ReadInSettingsLocalAdditionalDirectory_Allows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"approver-test-{Guid.NewGuid():N}");
        var claudeDir = Path.Combine(tempRoot, ".claude");
        var sisterDir = Path.Combine(tempRoot, "..", "local-sister");
        Directory.CreateDirectory(claudeDir);

        try
        {
            File.WriteAllText(Path.Combine(claudeDir, "settings.local.json"), """{"additionalDirectories": ["../local-sister"]}""");

            var approver = _createApprover(tempRoot);
            var input = _createToolInput(new ReadInput { FilePath = Path.Combine(sisterDir, "file.txt") }, "Read");

            var result = approver.Handle(input);

            result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // --- Testable subclass ---

    private class TestableApprover : InsideProjectAllowedApprover
    {
        private readonly string? _projectRoot;

        public TestableApprover(string? projectRoot)
        {
            _projectRoot = projectRoot;
        }

        protected override string? FindProjectFolder() => _projectRoot;
    }
}
