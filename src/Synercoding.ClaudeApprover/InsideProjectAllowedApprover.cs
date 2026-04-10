using Synercoding.ClaudeApprover.BashParser;
using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;
using System.Text.Json;

namespace Synercoding.ClaudeApprover;

/// <summary>
/// An approver that restricts all file and bash operations to within the project root directory.
/// </summary>
public class InsideProjectAllowedApprover : BaseApprover
{
    /// <summary>
    /// Delegate for approving individual bash commands.
    /// </summary>
    /// <param name="commandInfo">Information about the command being evaluated.</param>
    /// <param name="reason">An optional reason for the decision.</param>
    /// <param name="newWorkingDirectory">An optional new working directory if the command changes it.</param>
    /// <returns>The permission decision for the command.</returns>
    public delegate CommandPermission CommandApprover(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory);

    /// <summary>
    /// Represents a method that determines whether a tool input should be approved for use, based on the provided input
    /// and additional context.
    /// </summary>
    /// <param name="input">The tool input to evaluate for approval.</param>
    /// <param name="mcpInput">A JSON element containing additional context or parameters relevant to the approval decision.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> instance that contains the output result.</returns>
    public delegate PreToolUseOutput? McpApprover(ToolInput input, JsonElement mcpInput);

    private const string NOT_ALLOWED_OUTSIDE_ROOT = "You are not allowed outside the root project folder.";
    private const string NOT_ALLOWED_IN_GIT_FOLDER = "You are not allowed inside the git folder.";
    private const string CANT_DETERMINE_PROJECT_ROOT = "Approver tool not configured correctly, can't determine project root.";

    /// <summary>
    /// Initializes a new instance of <see cref="InsideProjectAllowedApprover"/> with default command approvers.
    /// </summary>
    public InsideProjectAllowedApprover()
    {
        CommandApprovers.Add("awk", AllowCommand);
        CommandApprovers.Add("base32", AllowCommand);
        CommandApprovers.Add("base64", AllowCommand);
        CommandApprovers.Add("cat", AllowCommand);
        CommandApprovers.Add("cd", HandleCd);
        CommandApprovers.Add("cp", HandleCp);
        CommandApprovers.Add("echo", AllowCommand);
        CommandApprovers.Add("file", AllowCommand);
        CommandApprovers.Add("find", AllowCommand);
        CommandApprovers.Add("grep", AllowCommand);
        CommandApprovers.Add("head", AllowCommand);
        CommandApprovers.Add("ifconfig", AllowCommand);
        CommandApprovers.Add("jq", AllowCommand);
        CommandApprovers.Add("ls", AllowCommand);
        CommandApprovers.Add("mkdir", HandleMkdir);
        CommandApprovers.Add("pgrep", AllowCommand);
        CommandApprovers.Add("ps", AllowCommand);
        CommandApprovers.Add("pwd", AllowCommand);
        CommandApprovers.Add("rm", HandleRm);
        CommandApprovers.Add("rmdir", HandleRmdir);
        CommandApprovers.Add("sed", HandleSed);
        CommandApprovers.Add("sort", AllowCommand);
        CommandApprovers.Add("tree", AllowCommand);
        CommandApprovers.Add("tail", AllowCommand);
        CommandApprovers.Add("wc", AllowCommand);
        CommandApprovers.Add("which", AllowCommand);

    }

    /// <summary>
    /// Gets the dictionary mapping command names to their approval handlers.
    /// </summary>
    public IDictionary<string, CommandApprover> CommandApprovers { get; } = new Dictionary<string, CommandApprover>();

    /// <summary>
    /// Gets the dictionary mapping mcp names to their approval handlers.
    /// </summary>
    public IDictionary<string, McpApprover> McpApprovers { get; } = new Dictionary<string, McpApprover>();

    /// <summary>
    /// Gets the list of additional directories (absolute or relative to the project root) that should be allowed for file and bash operations.
    /// </summary>
    public List<string> AdditionalDirectories { get; } = new();

    /// <summary>
    /// Gets or sets whether to import additional directories from the Claude settings files
    /// (<c>.claude/settings.json</c> and <c>.claude/settings.local.json</c>).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool ImportAdditionalDirsFromClaude { get; set; } = true;

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, ReadInput read)
        => Handle(read.FilePath, input.CurrentWorkingDirectory);

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, EditInput edit)
        => Handle(edit.FilePath, input.CurrentWorkingDirectory);

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, WriteInput write)
        => Handle(write.FilePath, input.CurrentWorkingDirectory);

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, GlobInput glob)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, GrepInput grep)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, TodoWriteInput todo)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, WebFetchInput webFetch)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, WebSearchInput webSearch)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, TaskCreate taskCreate)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, TaskUpdate taskUpdate)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, ExitPlanMode exitPlanMode)
        => Allow();

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, UnknownToolInput unknownTool)
    {
        if (input.ToolName.StartsWith("mcp__"))
        {
            var mcp = input.ToolName.Split("__")[1];

            if (McpApprovers.TryGetValue(mcp, out var mcpApprover))
                return mcpApprover(input, unknownTool.RawData);
        }

        return null;
    }

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, BashInput bash)
    {
        var workingDir = input.CurrentWorkingDirectory;
        var projectRoot = FindProjectFolder();
        if (projectRoot is null)
            return Ask("Could not determine the project root, approver is not configured correctly.");

        var parser = new BashCommandParser();

        var pipeline = parser.Parse(bash.Command);

        while (pipeline != null)
        {
            foreach (var command in pipeline.Commands)
            {
                if (CommandApprovers.TryGetValue(command.Executable, out var commandApprover))
                {
                    var commandInfo = new CommandInfo(command, workingDir, projectRoot);
                    var approvalResult = commandApprover(commandInfo, out string? reason, out string? newWorkingDir);
                    if (newWorkingDir is not null)
                        workingDir = newWorkingDir;

                    if (approvalResult == CommandPermission.Ask)
                        return Ask(reason);
                    if (approvalResult == CommandPermission.Deny)
                        return Deny(reason);

                    // Move on to the next command in the pipeline
                    continue;
                }

                // Unknown/unlisted command, so ask user for confirmation
                return Ask($"Command {command.Executable} is unknown, you can add a approval process to the CommandApprovers property if you want to implement a permanent answer.");
            }

            pipeline = pipeline.NextPipeline;
        }

        // If we got here, all commands are allowed
        return Allow();
    }

    /// <summary>
    /// Handles file path-based tool invocations by checking the path is inside the project root and not in the .git folder.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <param name="currentWorkingDirectory">The current working directory.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision.</returns>
    public virtual PreToolUseOutput? Handle(string filePath, string currentWorkingDirectory)
    {
        // Normalize paths to use consistent directory separators and resolve relative segments
        currentWorkingDirectory = PathNormalizer.Normalize(currentWorkingDirectory);
        filePath = PathNormalizer.Normalize(currentWorkingDirectory, filePath);

        var gitSegment = $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}";
        var gitEnd = $"{Path.DirectorySeparatorChar}.git";

        if (currentWorkingDirectory.Contains(gitSegment) || currentWorkingDirectory.EndsWith(gitEnd))
            return Deny(NOT_ALLOWED_IN_GIT_FOLDER);

        if (filePath.Contains(gitSegment))
            return Deny(NOT_ALLOWED_IN_GIT_FOLDER);

        if (IsClaudeConfigDirFile(filePath))
        {
            // Accessing files in the Claude config directory is allowed even if located outside the project folder
            return Allow();
        }

        var projectFolder = FindProjectFolder();
        if (projectFolder is null)
            return Ask(CANT_DETERMINE_PROJECT_ROOT);

        if (!IsInsideAllowedRoot(filePath, projectFolder))
            return Deny(NOT_ALLOWED_OUTSIDE_ROOT);

        return Allow();
    }

    /// <summary>
    /// Default command approver that allows any command unconditionally.
    /// </summary>
    /// <param name="commandInfo">Information about the command being evaluated.</param>
    /// <param name="reason">Always <c>null</c>.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns><see cref="CommandPermission.Allow"/>.</returns>
    public static CommandPermission AllowCommand(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;
        return CommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>cd</c> commands by verifying the target directory is inside the project root.
    /// </summary>
    /// <param name="commandInfo">Information about the cd command being evaluated.</param>
    /// <param name="reason">An optional reason if the command is denied.</param>
    /// <param name="newWorkingDirectory">The new working directory if the command is allowed.</param>
    /// <returns>The permission decision for the command.</returns>
    protected virtual CommandPermission HandleCd(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        if (commandInfo.Command.Executable != "cd")
        {
            reason = "Approver incorrectly configured, HandleCd should only handle cd commands.";
            return CommandPermission.Deny;
        }

        if (commandInfo.Command.Arguments.Any(a => a.Contains(".git")))
        {
            reason = NOT_ALLOWED_IN_GIT_FOLDER;
            return CommandPermission.Deny;
        }

        var newPath = _buildPath(commandInfo.WorkingDirectory, commandInfo.Command.Arguments.Single());
        if (!IsInsideAllowedRoot(newPath, commandInfo.ProjectRoot))
        {
            reason = NOT_ALLOWED_OUTSIDE_ROOT;
            return CommandPermission.Deny;
        }

        newWorkingDirectory = newPath;
        return CommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>sed</c> commands by verifying in-place edit targets are inside the project root.
    /// </summary>
    /// <param name="commandInfo">Information about the sed command being evaluated.</param>
    /// <param name="reason">An optional reason if the command is denied.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns>The permission decision for the command.</returns>
    protected virtual CommandPermission HandleSed(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        if (commandInfo.Command.Executable != "sed")
        {
            reason = "Approver incorrectly configured, HandleSed should only handle sed commands.";
            return CommandPermission.Deny;
        }

        if (commandInfo.Command.Arguments.Any(a => a == "-i"))
        {
            var sedFile = _buildPath(commandInfo.WorkingDirectory, commandInfo.Command.Arguments.Last());
            if (sedFile.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                reason = NOT_ALLOWED_IN_GIT_FOLDER;
                return CommandPermission.Deny;
            }
            if (!IsInsideAllowedRoot(sedFile, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return CommandPermission.Deny;
            }
        }

        return CommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>rm</c> commands by verifying all targets are inside the project root.
    /// </summary>
    /// <param name="commandInfo">Information about the rm command being evaluated.</param>
    /// <param name="reason">An optional reason if the command is denied.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns>The permission decision for the command.</returns>
    protected virtual CommandPermission HandleRm(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        if (commandInfo.Command.Executable != "rm")
        {
            reason = "Approver incorrectly configured, HandleRm should only handle rm commands.";
            return CommandPermission.Deny;
        }

        var arguments = commandInfo.Command.Arguments;

        if (arguments.Contains("--no-preserve-root"))
        {
            reason = "That is a dangerous move, not allowed.";
            return CommandPermission.Deny;
        }

        arguments = arguments
            .Where(a => !_isRmFlag(a))
            .ToList();

        foreach (var argument in arguments)
        {
            var rmOption = _buildPath(commandInfo.WorkingDirectory, argument);
            if (rmOption.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                reason = NOT_ALLOWED_IN_GIT_FOLDER;
                return CommandPermission.Deny;
            }
            if (!IsInsideAllowedRoot(rmOption, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return CommandPermission.Deny;
            }
        }

        return CommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>cp</c> commands by verifying all source and destination paths are inside the project root.
    /// </summary>
    /// <param name="commandInfo">Information about the cp command being evaluated.</param>
    /// <param name="reason">An optional reason if the command is denied.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns>The permission decision for the command.</returns>
    protected virtual CommandPermission HandleCp(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        if (commandInfo.Command.Executable != "cp")
        {
            reason = "Approver incorrectly configured, HandleCp should only handle cp commands.";
            return CommandPermission.Deny;
        }

        var arguments = commandInfo.Command.Arguments
            .Where(a => !_isCpFlag(a))
            .ToList();

        foreach (var argument in arguments)
        {
            var cpPath = _buildPath(commandInfo.WorkingDirectory, argument);
            if (cpPath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                reason = NOT_ALLOWED_IN_GIT_FOLDER;
                return CommandPermission.Deny;
            }
            if (!IsInsideAllowedRoot(cpPath, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return CommandPermission.Deny;
            }
        }

        return CommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>mkdir</c> commands by verifying all targets are inside the project root.
    /// </summary>
    /// <param name="commandInfo">Information about the mkdir command being evaluated.</param>
    /// <param name="reason">An optional reason if the command is denied.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns>The permission decision for the command.</returns>
    protected virtual CommandPermission HandleMkdir(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        if (commandInfo.Command.Executable != "mkdir")
        {
            reason = "Approver incorrectly configured, HandleMkdir should only handle mkdir commands.";
            return CommandPermission.Deny;
        }

        var arguments = commandInfo.Command.Arguments
            .Where(a => !_isMkdirFlag(a))
            .ToList();

        foreach (var argument in arguments)
        {
            var mkdirPath = _buildPath(commandInfo.WorkingDirectory, argument);
            if (mkdirPath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                reason = NOT_ALLOWED_IN_GIT_FOLDER;
                return CommandPermission.Deny;
            }
            if (!IsInsideAllowedRoot(mkdirPath, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return CommandPermission.Deny;
            }
        }

        return CommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>rmdir</c> commands by verifying all targets are inside the project root.
    /// </summary>
    /// <param name="commandInfo">Information about the rmdir command being evaluated.</param>
    /// <param name="reason">An optional reason if the command is denied.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns>The permission decision for the command.</returns>
    protected virtual CommandPermission HandleRmdir(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        if (commandInfo.Command.Executable != "rmdir")
        {
            reason = "Approver incorrectly configured, HandleRmdir should only handle rmdir commands.";
            return CommandPermission.Deny;
        }

        var arguments = commandInfo.Command.Arguments
            .Where(a => !_isRmdirFlag(a))
            .ToList();

        foreach (var argument in arguments)
        {
            var rmdirPath = _buildPath(commandInfo.WorkingDirectory, argument);
            if (rmdirPath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                reason = NOT_ALLOWED_IN_GIT_FOLDER;
                return CommandPermission.Deny;
            }
            if (!IsInsideAllowedRoot(rmdirPath, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return CommandPermission.Deny;
            }
        }

        return CommandPermission.Allow;
    }

    /// <summary>
    /// Determines whether a path is inside the project root or any of the allowed additional directories.
    /// </summary>
    /// <param name="fullPath">The normalized path to check.</param>
    /// <param name="projectRoot">The project root directory.</param>
    /// <returns><c>true</c> if the path is inside any allowed root; otherwise, <c>false</c>.</returns>
    protected bool IsInsideAllowedRoot(string fullPath, string projectRoot)
    {
        if (PathNormalizer.IsInsideRoot(fullPath, projectRoot))
            return true;

        foreach (var dir in AdditionalDirectories)
        {
            var resolved = PathNormalizer.Normalize(projectRoot, dir);
            if (PathNormalizer.IsInsideRoot(fullPath, resolved))
                return true;
        }

        if (ImportAdditionalDirsFromClaude)
        {
            foreach (var dir in _getClaudeAdditionalDirectories(projectRoot))
            {
                var resolved = PathNormalizer.Normalize(projectRoot, dir);
                if (PathNormalizer.IsInsideRoot(fullPath, resolved))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified file path is inside the Claude configuration directory.
    /// </summary>
    /// <param name="filePath">The normalized absolute file path to check.</param>
    /// <returns><c>true</c> if the file is inside the Claude config directory; otherwise, <c>false</c>.</returns>
    protected virtual bool IsClaudeConfigDirFile(string filePath)
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrEmpty(configDir))
        {
            // Default: ~/.claude
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }

        configDir = PathNormalizer.Normalize(configDir);
        return PathNormalizer.IsInsideRoot(filePath, configDir);
    }

    private IReadOnlyList<string>? _claudeAdditionalDirs;

    private IReadOnlyList<string> _getClaudeAdditionalDirectories(string projectRoot)
    {
        if (_claudeAdditionalDirs is not null)
            return _claudeAdditionalDirs;

        var dirs = new HashSet<string>(StringComparer.Ordinal);

        var settingsFiles = new[]
        {
            Path.Combine(projectRoot, ".claude", "settings.json"),
            Path.Combine(projectRoot, ".claude", "settings.local.json"),
        };

        foreach (var settingsFile in settingsFiles)
        {
            if (!File.Exists(settingsFile))
                continue;

            try
            {
                var json = File.ReadAllText(settingsFile);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("additionalDirectories", out var additionalDirs)
                    && additionalDirs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in additionalDirs.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is string dir)
                            dirs.Add(dir);
                    }
                }
            }
            catch
            {
                // If the settings file can't be read or parsed, skip it
            }
        }

        _claudeAdditionalDirs = dirs.ToList();
        return _claudeAdditionalDirs;
    }

    private static bool _isRmFlag(string argument)
    {
        return argument == "-f"
            || argument == "--force"
            || argument == "-i"
            || argument == "-I"
            || argument.StartsWith("--interactive")
            || argument == "--one-file-system"
            || argument == "-r"
            || argument == "-R"
            || argument == "--recursive"
            || argument == "-d"
            || argument == "--dir"
            || argument == "-v"
            || argument == "--verbose"
            || argument == "--help"
            || argument == "--version";
    }

    private static bool _isCpFlag(string argument)
    {
        return argument is "-f" or "--force"
            or "-r" or "-R" or "--recursive"
            or "-v" or "--verbose"
            or "-i" or "--interactive"
            or "-n" or "--no-clobber"
            or "-u" or "--update"
            or "-p" or "--preserve"
            or "-a" or "--archive"
            or "-l" or "--link"
            or "-s" or "--symbolic-link"
            or "-d" or "--no-dereference"
            or "-L" or "--dereference"
            or "-H" or "-P"
            or "--help" or "--version"
            or "--strip-trailing-slashes"
            || argument.StartsWith("--backup")
            || argument.StartsWith("--reflink")
            || argument.StartsWith("--preserve=")
            || argument.StartsWith("--no-preserve");
    }

    private static bool _isMkdirFlag(string argument)
    {
        return argument is "-p" or "--parents"
            or "-v" or "--verbose"
            or "-Z"
            or "-m"
            or "--help" or "--version"
            || argument.StartsWith("--mode")
            || argument.StartsWith("--context");
    }

    private static bool _isRmdirFlag(string argument)
    {
        return argument is "--ignore-fail-on-non-empty"
            or "-p" or "--parents"
            or "-v" or "--verbose"
            or "--help" or "--version";
    }

    private static string _buildPath(string currentWorkingDirectory, string pathArgument)
    {
        if (pathArgument[0] == '"' && pathArgument[^1] == '"')
            pathArgument = pathArgument[1..^1];

        return PathNormalizer.Normalize(currentWorkingDirectory, pathArgument);
    }
}
