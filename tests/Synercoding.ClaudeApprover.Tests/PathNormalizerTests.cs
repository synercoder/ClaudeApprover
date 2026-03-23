namespace Synercoding.ClaudeApprover.Tests;

public class PathNormalizerTests
{
    private static readonly string _root = Path.GetPathRoot(Environment.CurrentDirectory)!;

    private static string _abs(params string[] parts) => Path.GetFullPath(Path.Combine([_root, .. parts]));

    // --- ConvertMsysPath ---

    [Fact]
    public void ConvertMsysPath_NonMsysPath_ReturnsUnchanged()
    {
        var windowsPath = _abs("Git", "project");

        var result = PathNormalizer.ConvertMsysPath(windowsPath);

        result.Should().Be(windowsPath);
    }

    [Fact]
    public void ConvertMsysPath_RelativePath_ReturnsUnchanged()
    {
        var result = PathNormalizer.ConvertMsysPath("src/file.cs");

        result.Should().Be("src/file.cs");
    }

    [Fact]
    public void ConvertMsysPath_MsysDrivePath_ConvertsOnWindows()
    {
        var result = PathNormalizer.ConvertMsysPath("/c/Git/project/file.cs");

        if (OperatingSystem.IsWindows())
            result.Should().Be(@"C:\Git\project\file.cs");
        else
            result.Should().Be("/c/Git/project/file.cs");
    }

    [Fact]
    public void ConvertMsysPath_MsysLowercaseDrive_UppercasesDriveLetterOnWindows()
    {
        var result = PathNormalizer.ConvertMsysPath("/d/some/path");

        if (OperatingSystem.IsWindows())
            result.Should().Be(@"D:\some\path");
        else
            result.Should().Be("/d/some/path");
    }

    [Fact]
    public void ConvertMsysPath_MsysUppercaseDrive_PreservesDriveLetterOnWindows()
    {
        var result = PathNormalizer.ConvertMsysPath("/C/Users/test");

        if (OperatingSystem.IsWindows())
            result.Should().Be(@"C:\Users\test");
        else
            result.Should().Be("/C/Users/test");
    }

    [Fact]
    public void ConvertMsysPath_TooShort_ReturnsUnchanged()
    {
        var result = PathNormalizer.ConvertMsysPath("/c");

        result.Should().Be("/c");
    }

    [Fact]
    public void ConvertMsysPath_SlashNonLetterSlash_ReturnsUnchanged()
    {
        var result = PathNormalizer.ConvertMsysPath("/1/some/path");

        result.Should().Be("/1/some/path");
    }

    [Fact]
    public void ConvertMsysPath_SlashLetterNoTrailingSlash_ReturnsUnchanged()
    {
        var result = PathNormalizer.ConvertMsysPath("/cx");

        result.Should().Be("/cx");
    }

    // --- Normalize (single path) ---

    [Fact]
    public void Normalize_AbsolutePath_ReturnsFullPath()
    {
        var path = _abs("Git", "project", "file.cs");

        var result = PathNormalizer.Normalize(path);

        result.Should().Be(Path.GetFullPath(path));
    }

    [Fact]
    public void Normalize_ForwardSlashesOnWindows_NormalizesToBackslashes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = PathNormalizer.Normalize("C:/Git/project/file.cs");

        result.Should().Be(@"C:\Git\project\file.cs");
    }

    [Fact]
    public void Normalize_MsysPath_NormalizesToWindowsPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = PathNormalizer.Normalize("/c/Git/project/file.cs");

        result.Should().Be(@"C:\Git\project\file.cs");
    }

    // --- Normalize (base + relative) ---

    [Fact]
    public void Normalize_RelativePathWithBase_CombinesAndResolves()
    {
        var basePath = _abs("Git", "project");

        var result = PathNormalizer.Normalize(basePath, "src/file.cs");

        result.Should().Be(_abs("Git", "project", "src", "file.cs"));
    }

    [Fact]
    public void Normalize_AbsolutePathWithBase_IgnoresBase()
    {
        var basePath = _abs("Git", "project");
        var absolutePath = _abs("other", "file.cs");

        var result = PathNormalizer.Normalize(basePath, absolutePath);

        result.Should().Be(absolutePath);
    }

    [Fact]
    public void Normalize_MsysRelativeToMsysBase_NormalizesToWindowsPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = PathNormalizer.Normalize("/c/Git/project", "src/file.cs");

        result.Should().Be(@"C:\Git\project\src\file.cs");
    }

    [Fact]
    public void Normalize_MsysAbsoluteWithBase_IgnoresBase()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = PathNormalizer.Normalize("/c/Git/project", "/c/other/file.cs");

        result.Should().Be(@"C:\other\file.cs");
    }

    // --- IsInsideRoot ---

    [Fact]
    public void IsInsideRoot_FileInsideRoot_ReturnsTrue()
    {
        var root = _abs("Git", "project");
        var file = _abs("Git", "project", "src", "file.cs");

        PathNormalizer.IsInsideRoot(file, root).Should().BeTrue();
    }

    [Fact]
    public void IsInsideRoot_FileOutsideRoot_ReturnsFalse()
    {
        var root = _abs("Git", "project");
        var file = _abs("other", "file.cs");

        PathNormalizer.IsInsideRoot(file, root).Should().BeFalse();
    }

    [Fact]
    public void IsInsideRoot_FileIsRoot_ReturnsTrue()
    {
        var root = _abs("Git", "project");

        PathNormalizer.IsInsideRoot(root, root).Should().BeTrue();
    }

    [Fact]
    public void IsInsideRoot_SimilarPrefix_ReturnsFalse()
    {
        var root = _abs("Git", "project");
        var file = _abs("Git", "project-other", "file.cs");

        PathNormalizer.IsInsideRoot(file, root).Should().BeFalse();
    }

    [Fact]
    public void IsInsideRoot_DifferentCaseOnWindows_ReturnsTrue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = @"C:\Git\Project";
        var file = @"c:\git\project\src\file.cs";

        PathNormalizer.IsInsideRoot(file, root).Should().BeTrue();
    }

    [Fact]
    public void IsInsideRoot_ForwardSlashesOnWindows_ReturnsTrue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = @"C:\Git\project";
        var file = "C:/Git/project/src/file.cs";

        PathNormalizer.IsInsideRoot(file, root).Should().BeTrue();
    }

    [Fact]
    public void IsInsideRoot_MsysPathOnWindows_ReturnsTrue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = @"C:\Git\project";
        var file = "/c/Git/project/src/file.cs";

        PathNormalizer.IsInsideRoot(file, root).Should().BeTrue();
    }

    [Fact]
    public void IsInsideRoot_MsysPathOutsideRoot_ReturnsFalse()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = @"C:\Git\project";
        var file = "/c/other/file.cs";

        PathNormalizer.IsInsideRoot(file, root).Should().BeFalse();
    }

    [Fact]
    public void IsInsideRoot_MixedMsysAndWindows_ReturnsTrue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = "/c/Git/project";
        var file = @"C:\Git\project\src\file.cs";

        PathNormalizer.IsInsideRoot(file, root).Should().BeTrue();
    }
}
