namespace Synercoding.ClaudeApprover;

/// <summary>
/// Provides methods for normalizing file system paths across different platforms and shell environments.
/// </summary>
internal static class PathNormalizer
{
    /// <summary>
    /// Normalizes a path for consistent comparison by resolving relative segments and converting
    /// MSYS/Git Bash-style paths (e.g. <c>/c/Git/...</c>) to Windows paths (e.g. <c>C:\Git\...</c>) on Windows.
    /// On non-Windows platforms, only resolves the full path.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized, fully-qualified path.</returns>
    public static string Normalize(string path)
        => Path.GetFullPath(ExpandTilde(ConvertMsysPath(path)));

    /// <summary>
    /// Normalizes a potentially relative path by combining it with a base directory, resolving relative segments,
    /// and converting MSYS/Git Bash-style paths on Windows.
    /// </summary>
    /// <param name="basePath">The base directory to resolve relative paths against.</param>
    /// <param name="path">The path to normalize, which may be relative or absolute.</param>
    /// <returns>The normalized, fully-qualified path.</returns>
    public static string Normalize(string basePath, string path)
    {
        path = ExpandTilde(ConvertMsysPath(path));
        basePath = ExpandTilde(ConvertMsysPath(basePath));

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }

    /// <summary>
    /// Determines whether <paramref name="fullPath"/> is inside or equal to <paramref name="root"/>.
    /// Uses case-insensitive comparison on Windows and case-sensitive comparison on other platforms.
    /// Both paths are normalized before comparison.
    /// </summary>
    /// <param name="fullPath">The path to check.</param>
    /// <param name="root">The root directory to check against.</param>
    /// <returns><c>true</c> if <paramref name="fullPath"/> is inside or equal to <paramref name="root"/>; otherwise, <c>false</c>.</returns>
    public static bool IsInsideRoot(string fullPath, string root)
    {
        var normalizedPath = Normalize(fullPath);
        var normalizedRoot = Normalize(root);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var rootWithSep = Path.TrimEndingDirectorySeparator(normalizedRoot) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(rootWithSep, comparison)
            || string.Equals(normalizedPath, Path.TrimEndingDirectorySeparator(normalizedRoot), comparison);
    }

    /// <summary>
    /// Expands a tilde (<c>~</c>) prefix in a path to the current user's home directory.
    /// If the path does not start with <c>~/</c> or is not exactly <c>~</c>, it is returned unchanged.
    /// </summary>
    /// <param name="path">The path to expand.</param>
    /// <returns>The expanded path, or the original path if no expansion was needed.</returns>
    public static string ExpandTilde(string path)
    {
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }

    /// <summary>
    /// Converts an MSYS/Git Bash-style path (e.g. <c>/c/Git/...</c>) to a Windows path (e.g. <c>C:\Git\...</c>).
    /// On non-Windows platforms, or when the path is not in MSYS format, returns the path unchanged.
    /// </summary>
    /// <param name="path">The path to convert.</param>
    /// <returns>The converted path, or the original path if no conversion was needed.</returns>
    public static string ConvertMsysPath(string path)
    {
        if (!OperatingSystem.IsWindows())
            return path;

        if (path.Length >= 3
            && path[0] == '/'
            && char.IsLetter(path[1])
            && path[2] == '/')
        {
            return $"{char.ToUpperInvariant(path[1])}:{path[2..]}".Replace('/', '\\');
        }

        return path;
    }
}
