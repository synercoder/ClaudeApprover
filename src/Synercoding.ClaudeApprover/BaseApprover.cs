using Synercoding.ClaudeApprover.Input;
using Synercoding.ClaudeApprover.Output;

namespace Synercoding.ClaudeApprover;

/// <summary>
/// Abstract base class for building Claude Code pre-tool-use hook approvers.
/// Subclass and override per-tool handler methods to allow, deny, or ask for each tool invocation.
/// </summary>
public class BaseApprover
{
    /// <summary>
    /// Dispatches a tool input to the appropriate typed handler method.
    /// </summary>
    /// <param name="input">The tool input received from Claude Code.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input)
    {
        return input.Input switch
        {
            BashInput bash => Handle(input, bash),
            GrepInput grep => Handle(input, grep),
            GlobInput glob => Handle(input, glob),
            ReadInput read => Handle(input, read),
            EditInput edit => Handle(input, edit),
            WriteInput write => Handle(input, write),
            TaskInput task => Handle(input, task),
            TodoWriteInput todo => Handle(input, todo),
            WebFetchInput webFetch => Handle(input, webFetch),
            WebSearchInput webSearch => Handle(input, webSearch),
            TaskCreate taskCreate => Handle(input, taskCreate),
            TaskUpdate taskUpdate => Handle(input, taskUpdate),
            ExitPlanMode exitPlanMode => Handle(input, exitPlanMode),
            UnknownToolInput unknownToolInput => Handle(input, unknownToolInput),
            _ => null
        };
    }

    /// <summary>
    /// Handles a <see cref="UnknownToolInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="unknownTool">The unknown tool input (wraps a <see cref="System.Text.Json.JsonElement"/>).</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, UnknownToolInput unknownTool)
        => null;

    /// <summary>
    /// Handles a <see cref="WebFetchInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="webFetch">The web fetch input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, WebFetchInput webFetch)
        => null;

    /// <summary>
    /// Handles a <see cref="BashInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="bash">The bash input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, BashInput bash)
        => null;

    /// <summary>
    /// Handles a <see cref="GrepInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="grep">The grep input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, GrepInput grep)
        => null;

    /// <summary>
    /// Handles a <see cref="WebSearchInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="webSearch">The web search input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, WebSearchInput webSearch)
        => null;

    /// <summary>
    /// Handles a <see cref="ReadInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="read">The read input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, ReadInput read)
        => null;

    /// <summary>
    /// Handles an <see cref="EditInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="edit">The edit input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, EditInput edit)
        => null;

    /// <summary>
    /// Handles a <see cref="WriteInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="write">The write input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, WriteInput write)
        => null;

    /// <summary>
    /// Handles a <see cref="TaskInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="task">The task input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, TaskInput task)
        => null;

    /// <summary>
    /// Handles a <see cref="TodoWriteInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="todo">The todo write input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, TodoWriteInput todo)
        => null;

    /// <summary>
    /// Handles a <see cref="GlobInput"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="glob">The glob input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, GlobInput glob)
        => null;

    /// <summary>
    /// Handles a <see cref="TaskCreate"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="taskCreate">The task create input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, TaskCreate taskCreate)
        => null;

    /// <summary>
    /// Handles a <see cref="TaskUpdate"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="taskUpdate">The task update input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, TaskUpdate taskUpdate)
        => null;

    /// <summary>
    /// Handles an <see cref="ExitPlanMode"/> tool invocation.
    /// </summary>
    /// <param name="input">The tool input envelope.</param>
    /// <param name="exitPlanMode">The exit plan mode input.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with the permission decision, or <c>null</c> to take no action.</returns>
    public virtual PreToolUseOutput? Handle(ToolInput input, ExitPlanMode exitPlanMode)
        => null;

    /// <summary>
    /// Resolves the project root folder by checking (in order): <c>CLAUDE_PROJECT_DIR</c> environment variable,
    /// directory containing a <c>.claude</c> folder, or directory containing <c>.sln</c>/<c>.slnx</c> files.
    /// </summary>
    /// <returns>The project root path, or <c>null</c> if it could not be determined.</returns>
    protected virtual string? FindProjectFolder()
    {
        // Find project root from environment variable
        if (Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") is string envProjectDir
            && !string.IsNullOrEmpty(envProjectDir)
            && Directory.Exists(envProjectDir))
        {
            return envProjectDir;
        }

        // Find project root by locating a .claude folder in it
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        do
        {
            if (directory.Name == ".claude")
            {
                var projectDir = directory.Parent?.FullName;
                if (projectDir is null)
                    return null;

                return Path.TrimEndingDirectorySeparator(projectDir);
            }

            directory = directory.Parent;
        }
        while (directory != null);

        // Find project root by finding solution files in it
        directory = new DirectoryInfo(AppContext.BaseDirectory);
        do
        {
            if (directory.GetFiles("*.sln", SearchOption.TopDirectoryOnly).Length > 0
                || directory.GetFiles("*.slnx", SearchOption.TopDirectoryOnly).Length > 0)
            {
                return Path.TrimEndingDirectorySeparator(directory.FullName);
            }

            directory = directory.Parent;
        }
        while (directory != null);

        return null;
    }

    /// <summary>
    /// Creates a <see cref="PreToolUseOutput"/> that asks the user for confirmation.
    /// </summary>
    /// <param name="reason">An optional reason for asking.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with <see cref="PermissionDecision.Ask"/>.</returns>
    public static PreToolUseOutput Ask(string? reason = null)
    {
        return new PreToolUseOutput()
        {
            HookSpecificOutput = new PreToolUseHookSpecificOutput()
            {
                PermissionDecision = PermissionDecision.Ask,
                PermissionDecisionReason = reason
            }
        };
    }

    /// <summary>
    /// Creates a <see cref="PreToolUseOutput"/> that denies the tool invocation.
    /// </summary>
    /// <param name="reason">An optional reason for denying.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with <see cref="PermissionDecision.Deny"/>.</returns>
    public static PreToolUseOutput Deny(string? reason = null)
    {
        return new PreToolUseOutput()
        {
            HookSpecificOutput = new PreToolUseHookSpecificOutput()
            {
                PermissionDecision = PermissionDecision.Deny,
                PermissionDecisionReason = reason
            }
        };
    }

    /// <summary>
    /// Creates a <see cref="PreToolUseOutput"/> that allows the tool invocation.
    /// </summary>
    /// <param name="reason">An optional reason for allowing.</param>
    /// <returns>A <see cref="PreToolUseOutput"/> with <see cref="PermissionDecision.Allow"/>.</returns>
    public static PreToolUseOutput Allow(string? reason = null)
    {
        return new PreToolUseOutput()
        {
            HookSpecificOutput = new PreToolUseHookSpecificOutput()
            {
                PermissionDecision = PermissionDecision.Allow,
                PermissionDecisionReason = reason
            },
        };
    }
}
