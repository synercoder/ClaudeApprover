using Synercoding.ClaudeApprover.BashParser;
using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;
using System.Text.Json;
using PSCommandInfo = Synercoding.ClaudeApprover.PowerShellParser.CommandInfo;
using PSCommandPermission = Synercoding.ClaudeApprover.PowerShellParser.CommandPermission;
using PSParser = Synercoding.ClaudeApprover.PowerShellParser.PowerShellCommandParser;

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
    /// Delegate for approving individual PowerShell cmdlet invocations.
    /// </summary>
    /// <param name="commandInfo">Information about the cmdlet being evaluated.</param>
    /// <param name="reason">An optional reason for the decision.</param>
    /// <param name="newWorkingDirectory">An optional new working directory if the cmdlet changes it.</param>
    /// <returns>The permission decision for the cmdlet.</returns>
    public delegate PSCommandPermission PowerShellApprover(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory);

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
        _registerDefaultBashApprovers();

        _registerDefaultPowerShellApprovers();
    }

    /// <summary>
    /// Gets the dictionary mapping command names to their approval handlers.
    /// </summary>
    public IDictionary<string, CommandApprover> CommandApprovers { get; } = new Dictionary<string, CommandApprover>();

    /// <summary>
    /// Gets the dictionary mapping PowerShell cmdlet names (case-insensitive) to their approval handlers.
    /// </summary>
    public IDictionary<string, PowerShellApprover> PowerShellApprovers { get; } = new Dictionary<string, PowerShellApprover>(StringComparer.OrdinalIgnoreCase);

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

    /// <inheritdoc />
    public override PreToolUseOutput? Handle(ToolInput input, PowerShellInput powerShell)
    {
        var workingDir = input.CurrentWorkingDirectory;
        var projectRoot = FindProjectFolder();
        if (projectRoot is null)
            return Ask(CANT_DETERMINE_PROJECT_ROOT);

        var parser = new PSParser();

        var pipeline = parser.Parse(powerShell.Command);

        while (pipeline != null)
        {
            foreach (var command in pipeline.Commands)
            {
                if (PowerShellApprovers.TryGetValue(command.Executable, out var approver))
                {
                    var info = new PSCommandInfo(command, workingDir, projectRoot);
                    var decision = approver(info, out var reason, out var newWorkingDir);
                    if (newWorkingDir is not null)
                        workingDir = newWorkingDir;

                    if (decision == PSCommandPermission.Ask)
                        return Ask(reason);
                    if (decision == PSCommandPermission.Deny)
                        return Deny(reason);

                    continue;
                }

                return Ask($"Cmdlet {command.Executable} is unknown, you can add an approval process to the PowerShellApprovers property if you want to implement a permanent answer.");
            }

            pipeline = pipeline.NextPipeline;
        }

        return Allow();
    }

    /// <summary>
    /// Default PowerShell approver that allows a cmdlet unconditionally.
    /// </summary>
    /// <param name="commandInfo">Information about the cmdlet being evaluated.</param>
    /// <param name="reason">Always <c>null</c>.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns><see cref="PSCommandPermission.Allow"/>.</returns>
    public static PSCommandPermission PowerShellAllowCommand(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;
        return PSCommandPermission.Allow;
    }

    /// <summary>
    /// Default PowerShell approver that always asks the user for confirmation.
    /// </summary>
    /// <param name="commandInfo">Information about the cmdlet being evaluated.</param>
    /// <param name="reason">Always <c>null</c>.</param>
    /// <param name="newWorkingDirectory">Always <c>null</c>.</param>
    /// <returns><see cref="PSCommandPermission.Ask"/>.</returns>
    public static PSCommandPermission PowerShellAskCommand(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;
        return PSCommandPermission.Ask;
    }

    private void _registerDefaultBashApprovers()
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

    private void _registerDefaultPowerShellApprovers()
    {
        // Variable assignments with pure literal right-hand sides.
        PowerShellApprovers.Add(PSParser.ASSIGNMENT_SENTINEL, PowerShellAllowCommand);

        // Read-only / safe cmdlets.
        foreach (var name in new[]
        {
            "Get-Content", "Get-ChildItem", "Get-Item", "Get-Location", "Get-Date", "Get-Command",
            "Get-Member", "Get-Help", "Get-Variable", "Get-Process", "Get-Host",
            "Write-Host", "Write-Output", "Write-Error", "Write-Warning", "Write-Debug", "Write-Verbose",
            "Select-Object", "Select-String", "Where-Object", "ForEach-Object", "Sort-Object",
            "Measure-Object", "Group-Object", "Compare-Object",
            "Format-Table", "Format-List", "Format-Wide",
            "Out-Host", "Out-String", "Out-Null", "Out-Default",
            "ConvertFrom-Json", "ConvertTo-Json", "ConvertFrom-Csv", "ConvertTo-Csv", "ConvertFrom-StringData",
            "Test-Path", "Resolve-Path", "Join-Path", "Split-Path",
        })
        {
            PowerShellApprovers.Add(name, PowerShellAllowCommand);
        }

        // Path-mutating cmdlets with their common aliases.
        foreach (var name in new[] { "Set-Location", "cd", "sl", "chdir" })
            PowerShellApprovers.Add(name, HandlePsSetLocation);
        foreach (var name in new[] { "Remove-Item", "rm", "del", "erase", "rd", "rmdir", "ri" })
            PowerShellApprovers.Add(name, HandlePsRemoveItem);
        foreach (var name in new[] { "Copy-Item", "cp", "copy", "cpi" })
            PowerShellApprovers.Add(name, HandlePsCopyItem);
        foreach (var name in new[] { "Move-Item", "mv", "move", "mi" })
            PowerShellApprovers.Add(name, HandlePsMoveItem);
        foreach (var name in new[] { "New-Item", "ni" })
            PowerShellApprovers.Add(name, HandlePsNewItem);
        foreach (var name in new[] { "Rename-Item", "ren", "rni" })
            PowerShellApprovers.Add(name, HandlePsRenameItem);
        foreach (var name in new[] { "Out-File", "Set-Content", "Add-Content", "Clear-Content" })
            PowerShellApprovers.Add(name, HandlePsWriteContent);

        // Cmdlets we never want to auto-approve without explicit opt-in.
        foreach (var name in new[] { "Invoke-Expression", "iex", "Invoke-WebRequest", "iwr", "Invoke-RestMethod", "irm" })
            PowerShellApprovers.Add(name, PowerShellAskCommand);
    }

    /// <summary>
    /// Handles approval for <c>Set-Location</c> (and aliases <c>cd</c>, <c>sl</c>, <c>chdir</c>) by verifying the target directory is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsSetLocation(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        var paths = _extractPowerShellPathArgs(commandInfo.Command.Arguments).ToList();
        if (paths.Count == 0)
        {
            // Set-Location with no arg resolves to $HOME; treat as an ambiguous case and ask.
            reason = "Set-Location without an explicit path is not auto-approved.";
            return PSCommandPermission.Ask;
        }

        var target = paths[0];
        if (target.Contains(".git", StringComparison.Ordinal))
        {
            reason = NOT_ALLOWED_IN_GIT_FOLDER;
            return PSCommandPermission.Deny;
        }

        var newPath = _buildPath(commandInfo.WorkingDirectory, target);
        if (!IsInsideAllowedRoot(newPath, commandInfo.ProjectRoot))
        {
            reason = NOT_ALLOWED_OUTSIDE_ROOT;
            return PSCommandPermission.Deny;
        }

        newWorkingDirectory = newPath;
        return PSCommandPermission.Allow;
    }

    /// <summary>
    /// Handles approval for <c>Remove-Item</c> (and common aliases) by verifying every target path is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsRemoveItem(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
        => _validatePsPaths(commandInfo, out reason, out newWorkingDirectory);

    /// <summary>
    /// Handles approval for <c>Copy-Item</c> (and aliases) by verifying every source and destination path is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsCopyItem(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
        => _validatePsPaths(commandInfo, out reason, out newWorkingDirectory);

    /// <summary>
    /// Handles approval for <c>Move-Item</c> (and aliases) by verifying every source and destination path is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsMoveItem(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
        => _validatePsPaths(commandInfo, out reason, out newWorkingDirectory);

    /// <summary>
    /// Handles approval for <c>New-Item</c> (and <c>ni</c>) by verifying every target path is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsNewItem(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
        => _validatePsPaths(commandInfo, out reason, out newWorkingDirectory);

    /// <summary>
    /// Handles approval for <c>Rename-Item</c> (and aliases) by verifying the source path is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsRenameItem(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
        => _validatePsPaths(commandInfo, out reason, out newWorkingDirectory);

    /// <summary>
    /// Handles approval for content-writing cmdlets (<c>Out-File</c>, <c>Set-Content</c>, <c>Add-Content</c>, <c>Clear-Content</c>) by verifying the target path is inside the project root.
    /// </summary>
    protected virtual PSCommandPermission HandlePsWriteContent(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
        => _validatePsPaths(commandInfo, out reason, out newWorkingDirectory);

    private PSCommandPermission _validatePsPaths(PSCommandInfo commandInfo, out string? reason, out string? newWorkingDirectory)
    {
        reason = null;
        newWorkingDirectory = null;

        foreach (var pathArg in _extractPowerShellPathArgs(commandInfo.Command.Arguments))
        {
            var resolved = _buildPath(commandInfo.WorkingDirectory, pathArg);
            if (resolved.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            {
                reason = NOT_ALLOWED_IN_GIT_FOLDER;
                return PSCommandPermission.Deny;
            }
            if (!IsInsideAllowedRoot(resolved, commandInfo.ProjectRoot))
            {
                reason = NOT_ALLOWED_OUTSIDE_ROOT;
                return PSCommandPermission.Deny;
            }
        }

        return PSCommandPermission.Allow;
    }

    private static IEnumerable<string> _extractPowerShellPathArgs(IList<string> arguments)
    {
        bool nextIsNamedValue = false;
        foreach (var arg in arguments)
        {
            if (string.IsNullOrEmpty(arg))
                continue;
            if (arg[0] == '-')
            {
                if (_isPowerShellSwitchParameter(arg))
                {
                    nextIsNamedValue = false;
                    continue;
                }
                nextIsNamedValue = !_isPowerShellPathParameterName(arg);
                continue;
            }
            if (nextIsNamedValue)
            {
                nextIsNamedValue = false;
                continue;
            }
            yield return arg;
        }
    }

    private static bool _isPowerShellPathParameterName(string arg)
    {
        var name = arg.AsSpan().TrimStart('-');
        return name.Equals("Path", StringComparison.OrdinalIgnoreCase)
            || name.Equals("LiteralPath", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Destination", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NewName", StringComparison.OrdinalIgnoreCase)
            || name.Equals("FilePath", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _isPowerShellSwitchParameter(string arg)
    {
        var name = arg.AsSpan().TrimStart('-');
        return name.Equals("Recurse", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Force", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Confirm", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WhatIf", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PassThru", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NoClobber", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NoNewline", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Append", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Container", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Verbose", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Debug", StringComparison.OrdinalIgnoreCase);
    }
}
