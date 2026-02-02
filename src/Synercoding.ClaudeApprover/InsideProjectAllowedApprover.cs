using Synercoding.ClaudeApprover.BashParser;
using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;

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

    private const string NOT_ALLOWED_OUTSIDE_ROOT = "You are not allowed outside the root project folder.";
    private const string NOT_ALLOWED_IN_GIT_FOLDER = "You are not allowed inside the git folder.";
    private const string CANT_DETERMINE_PROJECT_ROOT = "Approver tool not configured correctly, can't determine project root.";

    /// <summary>
    /// Initializes a new instance of <see cref="InsideProjectAllowedApprover"/> with default command approvers.
    /// </summary>
    public InsideProjectAllowedApprover()
    {
        CommandApprovers.Add("ls", AllowCommand);
        CommandApprovers.Add("tree", AllowCommand);
        CommandApprovers.Add("echo", AllowCommand);
        CommandApprovers.Add("find", AllowCommand);
        CommandApprovers.Add("sort", AllowCommand);
        CommandApprovers.Add("grep", AllowCommand);
        CommandApprovers.Add("head", AllowCommand);
        CommandApprovers.Add("wc", AllowCommand);
        CommandApprovers.Add("cat", AllowCommand);
        CommandApprovers.Add("tail", AllowCommand);
        CommandApprovers.Add("jq", AllowCommand);
        CommandApprovers.Add("base64", AllowCommand);
        CommandApprovers.Add("awk", AllowCommand);
        CommandApprovers.Add("pwd", AllowCommand);
        CommandApprovers.Add("dotnet", AllowCommand);

        CommandApprovers.Add("cd", HandleCd);
        CommandApprovers.Add("sed", HandleSed);
        CommandApprovers.Add("rm", HandleRm);
    }

    /// <summary>
    /// Gets the dictionary mapping command names to their approval handlers.
    /// </summary>
    public IDictionary<string, CommandApprover> CommandApprovers { get; } = new Dictionary<string, CommandApprover>();

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
        var gitSegment = $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}";
        var gitEnd = $"{Path.DirectorySeparatorChar}.git";

        if (currentWorkingDirectory.Contains(gitSegment) || currentWorkingDirectory.EndsWith(gitEnd))
            return Deny(NOT_ALLOWED_IN_GIT_FOLDER);

        if (filePath.Contains(gitSegment))
            return Deny(NOT_ALLOWED_IN_GIT_FOLDER);

        if (!Path.IsPathRooted(filePath))
            filePath = Path.GetFullPath(Path.Combine(currentWorkingDirectory, filePath));

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Directory?.Name == "plans"
            && fileInfo.Directory?.Parent?.Name == ".claude"
            && fileInfo.Extension == ".md")
        {
            // Creating a plan is allowed even if the plan is located outside the project folder
            return Allow();
        }

        var projectFolder = FindProjectFolder();
        if (projectFolder is null)
            return Ask(CANT_DETERMINE_PROJECT_ROOT);

        if (!_isInsideRoot(filePath, projectFolder))
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
    protected virtual CommandPermission AllowCommand(CommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
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
        if (!_isInsideRoot(newPath, commandInfo.ProjectRoot))
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
            if (!_isInsideRoot(sedFile, commandInfo.ProjectRoot))
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
            if (!_isInsideRoot(rmOption, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return CommandPermission.Deny;
            }
        }

        return CommandPermission.Allow;
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

    private static bool _isInsideRoot(string fullPath, string projectRoot)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(projectRoot) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal)
            || fullPath == Path.TrimEndingDirectorySeparator(projectRoot);
    }

    private static string _buildPath(string currentWorkingDirectory, string pathArgument)
    {
        if (pathArgument[0] == '"' && pathArgument[^1] == '"')
            pathArgument = pathArgument[1..^1];

        return Path.GetFullPath(Path.Combine(currentWorkingDirectory, pathArgument));
    }
}
