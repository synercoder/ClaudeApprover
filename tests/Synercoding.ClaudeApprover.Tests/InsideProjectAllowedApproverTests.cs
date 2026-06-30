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
    public void Handle_CdFollowedByNewlineSeparatedCommands_DoesNotThrow()
    {
        // Regression: a multi-line bash command (cd on its own line, followed by
        // further commands) used to be parsed as a single cd command with many
        // arguments, making HandleCd's Arguments.Single() throw.
        var approver = _createApprover();
        var command =
            $"cd {_p("src")}\n" +
            "sed -i 's/foo/bar/; s/baz/qux/' file.cs\n" +
            "echo done";
        var input = _createToolInput(new BashInput { Command = command }, "Bash");

        var act = () => approver.Handle(input);

        act.Should().NotThrow();
    }

    [Fact]
    public void Handle_CdInsideProjectFollowedBySemicolonCommand_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cd {_p("src")}; ls" }, "Bash");

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
    public void Handle_CpInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cp {_p("src", "file.cs")} {_p("src", "file.bak")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_CpSourceOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cp {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "passwd"))} {_p("src", "file.txt")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_CpDestinationOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cp {_p("src", "file.cs")} {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "tmp", "stolen.cs"))}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_CpFromGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cp {_p(".git", "config")} {_p("src", "config.bak")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_CpToGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cp {_p("src", "file.cs")} {_p(".git", "hooks", "pre-commit")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_CpRecursiveWithFlags_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"cp -r -v {_p("src", "dir1")} {_p("src", "dir2")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_CpInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new BashInput { Command = $"cp {Path.Combine(_additionalDir, "file.cs")} {_p("src", "file.cs")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_MkdirInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"mkdir {_p("src", "newdir")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_MkdirOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"mkdir {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "tmp", "newdir"))}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_MkdirInGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"mkdir {_p(".git", "hooks")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_MkdirWithFlags_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"mkdir -p -v {_p("src", "deep", "nested")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_MkdirInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new BashInput { Command = $"mkdir {Path.Combine(_additionalDir, "newdir")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_RmdirInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rmdir {_p("src", "emptydir")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_RmdirOutsideProject_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rmdir {Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "tmp", "emptydir"))}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_RmdirInGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rmdir {_p(".git", "refs")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_RmdirWithFlags_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = $"rmdir -p --verbose {_p("src", "emptydir")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_RmdirInAdditionalDirectory_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("../sister-project");
        var input = _createToolInput(new BashInput { Command = $"rmdir {Path.Combine(_additionalDir, "emptydir")}" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
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

    // --- Tilde path in AdditionalDirectories tests ---

    [Fact]
    public void Handle_ClaudeProfileDir_NotInAdditionalDirs_AllowedViaConfigDirCheck()
    {
        var approver = _createApprover();
        var claudePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "screenshots", "test.png");
        var input = _createToolInput(new ReadInput { FilePath = claudePath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ClaudeProfileDir_AddedViaTildePath_Allows()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("~/.claude");
        var claudePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "screenshots", "test.png");
        var input = _createToolInput(new ReadInput { FilePath = claudePath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_OutsideProjectAndClaudeProfile_WithTildeAdditionalDir_StillDenies()
    {
        var approver = _createApprover();
        approver.AdditionalDirectories.Add("~/.claude");
        var outsidePath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "passwd"));
        var input = _createToolInput(new ReadInput { FilePath = outsidePath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    // --- Claude config dir tests ---

    [Fact]
    public void Handle_ReadFileInsideClaudeConfigDir_Allows()
    {
        var approver = _createApprover();
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
        var input = _createToolInput(new ReadInput { FilePath = configPath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_WriteFileInsideClaudeConfigDir_Allows()
    {
        var approver = _createApprover();
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "memory", "some_memory.md");
        var input = _createToolInput(new WriteInput { FilePath = configPath, Content = "test" }, "Write");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_EditFileInsideClaudeConfigDir_Allows()
    {
        var approver = _createApprover();
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "plans", "my-plan.md");
        var input = _createToolInput(new EditInput { FilePath = configPath, Old = "a", New = "b" }, "Edit");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_FileInsideClaudeConfigSubdir_Allows()
    {
        var approver = _createApprover();
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "some", "deep", "nested", "file.txt");
        var input = _createToolInput(new ReadInput { FilePath = configPath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_FileInsideCustomClaudeConfigDir_Allows()
    {
        var customConfigDir = Path.Combine(Path.GetTempPath(), "custom-claude-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(customConfigDir);
        try
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", customConfigDir);
            var approver = _createApprover();
            var configPath = Path.Combine(customConfigDir, "settings.json");
            var input = _createToolInput(new ReadInput { FilePath = configPath }, "Read");

            var result = approver.Handle(input);

            result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
            Directory.Delete(customConfigDir, true);
        }
    }

    [Fact]
    public void Handle_FileOutsideClaudeConfigDir_Denies()
    {
        var approver = _createApprover();
        var outsidePath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "some", "random", "file.txt"));
        var input = _createToolInput(new ReadInput { FilePath = outsidePath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_SubclassCanOverrideIsClaudeConfigDirFile()
    {
        var approver = new ConfigDirOverrideApprover(_projectRoot);
        var outsidePath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "custom", "config", "file.txt"));
        var input = _createToolInput(new ReadInput { FilePath = outsidePath }, "Read");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    // --- PowerShell tool tests ---

    [Fact]
    public void Handle_PowerShellSimpleCmdletAllowedByDefault_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(new PowerShellInput { Command = "Get-ChildItem" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellUnknownCmdlet_Asks()
    {
        var approver = _createApprover();
        var input = _createToolInput(new PowerShellInput { Command = "Do-SomethingCustom -Arg value" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_PowerShellSetLocationInsideProject_AllowsAndTracksCwd()
    {
        var approver = _createApprover();
        var targetDir = _p("src", "subdir");
        var input = _createToolInput(
            new PowerShellInput { Command = $"Set-Location \"{targetDir}\"; Get-ChildItem" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellSetLocationOutsideProject_Denies()
    {
        var approver = _createApprover();
        var outside = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc"));
        var input = _createToolInput(
            new PowerShellInput { Command = $"Set-Location \"{outside}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_PowerShellSetLocationIntoGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"cd \"{_p(".git")}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_PowerShellCdAlias_TreatedAsSetLocation()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"cd \"{_p("src")}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellRemoveItemInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"Remove-Item \"{_p("src", "old.cs")}\" -Force" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellRemoveItemOutsideProject_Denies()
    {
        var approver = _createApprover();
        var outside = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "hosts"));
        var input = _createToolInput(
            new PowerShellInput { Command = $"Remove-Item \"{outside}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_PowerShellRemoveItemInGitFolder_Denies()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"Remove-Item \"{_p(".git", "refs", "heads")}\" -Recurse -Force" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_PowerShellRemoveItemWithPathParameter_ValidatesValue()
    {
        var approver = _createApprover();
        var outside = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "hosts"));
        var input = _createToolInput(
            new PowerShellInput { Command = $"Remove-Item -Path \"{outside}\" -Recurse" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_PowerShellCopyItemInsideProject_Allows()
    {
        var approver = _createApprover();
        var src = _p("src", "a.cs");
        var dst = _p("src", "b.cs");
        var input = _createToolInput(
            new PowerShellInput { Command = $"Copy-Item \"{src}\" \"{dst}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellCopyItemDestinationOutside_Denies()
    {
        var approver = _createApprover();
        var src = _p("src", "a.cs");
        var dst = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "etc", "a.cs"));
        var input = _createToolInput(
            new PowerShellInput { Command = $"Copy-Item \"{src}\" -Destination \"{dst}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Deny);
    }

    [Fact]
    public void Handle_PowerShellNewItemInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"New-Item -Path \"{_p("src", "NewDir")}\" -ItemType Directory" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellOutFileInsideProject_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"Out-File -FilePath \"{_p("src", "log.txt")}\"" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellPipelineOfAllowedCmdlets_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"Get-Content \"{_p("data.json")}\" -Raw | ConvertFrom-Json | Select-Object -Last 5" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellVariableAssignmentOfLiteral_Allows()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"$f = 'C:/Git/project/test.json'" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellAssignmentFromCmdlet_ExtractsAndAllowsCmdlet()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = $"$j = Get-Content \"{_p("data.json")}\" -Raw | ConvertFrom-Json" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellInvokeExpression_Asks()
    {
        var approver = _createApprover();
        var input = _createToolInput(
            new PowerShellInput { Command = "Invoke-Expression $scriptContent" },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_PowerShellSetLocationThenScriptInvocation_AsksForUnknownScript()
    {
        // Real example: user cds into a dir and runs .\Run-Tests.ps1 — the script
        // is not a registered cmdlet, so we should Ask rather than Allow blindly.
        var approver = _createApprover();
        var postmanDir = _p("tests", "web", "PrintApi.Web.Api.Postman");
        var input = _createToolInput(
            new PowerShellInput
            {
                Command = $"Set-Location \"{postmanDir}\"; .\\Run-Tests.ps1 -Environment 'local-docker' 2>&1 | Select-Object -Last 20"
            },
            "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    // --- ExecutableApprovers (shared bash/PowerShell) tests ---

    [Fact]
    public void Handle_BashCommandInExecutableApprovers_Allows()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["dotnet"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new BashInput { Command = "dotnet build" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_PowerShellCommandInExecutableApprovers_Allows()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["dotnet"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new PowerShellInput { Command = "dotnet build" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_BashApproversOverridesExecutableApprovers()
    {
        var approver = _createApprover();
        approver.BashApprovers["mytool"] = InsideProjectAllowedApprover.AskCommand;
        approver.ExecutableApprovers["mytool"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new BashInput { Command = "mytool" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_PowerShellApproversOverridesExecutableApprovers()
    {
        var approver = _createApprover();
        approver.PowerShellApprovers["mycmdlet"] = InsideProjectAllowedApprover.AskCommand;
        approver.ExecutableApprovers["mycmdlet"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new PowerShellInput { Command = "mycmdlet" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_ExecutableApproversCaseInsensitive_MatchesDifferentCaseFromPowerShell()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["DotNet"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new PowerShellInput { Command = "dotnet build" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ExecutableApproversBash_RegisteredWithoutExe_MatchesExeCommand()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["git"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new BashInput { Command = "git.exe status" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ExecutableApproversBash_RegisteredWithExe_MatchesPlainCommand()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["git.exe"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new BashInput { Command = "git status" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ExecutableApproversPowerShell_RegisteredWithoutExe_MatchesExeCommand()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["git"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new PowerShellInput { Command = "git.exe status" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ExecutableApproversPowerShell_RegisteredWithExe_MatchesPlainCommand()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["git.exe"] = InsideProjectAllowedApprover.AllowCommand;
        var input = _createToolInput(new PowerShellInput { Command = "git status" }, "PowerShell");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Allow);
    }

    [Fact]
    public void Handle_ExecutableApproversExactMatch_WinsOverExeFallback()
    {
        var approver = _createApprover();
        approver.ExecutableApprovers["git"] = InsideProjectAllowedApprover.AllowCommand;
        approver.ExecutableApprovers["git.exe"] = InsideProjectAllowedApprover.AskCommand;
        var input = _createToolInput(new BashInput { Command = "git.exe status" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    [Fact]
    public void Handle_BashApproversDoesNotApplyExeFallback()
    {
        var approver = _createApprover();
        var input = _createToolInput(new BashInput { Command = "ls.exe" }, "Bash");

        var result = approver.Handle(input);

        result!.HookSpecificOutput.PermissionDecision.Should().Be(PermissionDecision.Ask);
    }

    // --- Testable subclasses ---

    private class TestableApprover : InsideProjectAllowedApprover
    {
        private readonly string? _projectRoot;

        public TestableApprover(string? projectRoot)
        {
            _projectRoot = projectRoot;
        }

        protected override string? FindProjectFolder() => _projectRoot;
    }

    private class ConfigDirOverrideApprover : InsideProjectAllowedApprover
    {
        private readonly string? _projectRoot;

        public ConfigDirOverrideApprover(string? projectRoot)
        {
            _projectRoot = projectRoot;
        }

        protected override string? FindProjectFolder() => _projectRoot;

        protected override bool IsClaudeConfigDirFile(string filePath)
        {
            // Allow everything in /custom/config/ for testing purposes
            var customDir = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "custom", "config"));
            return PathNormalizer.IsInsideRoot(filePath, customDir);
        }
    }
}
