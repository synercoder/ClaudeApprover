using Synercoding.ClaudeApprover.Input;
using System.Text;

namespace Synercoding.ClaudeApprover.Tests;

public class PowerShellExamplesIntegrationTests
{
    public static TheoryData<string> ExampleFiles
    {
        get
        {
            var data = new TheoryData<string>();
            var dir = _examplesDirectory();
            if (dir is null)
                return data;
            foreach (var file in Directory.EnumerateFiles(dir, "PowerShell-*.json"))
                data.Add(file);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void Examples_DeserializeAsPowerShellInput_AndProduceADecision(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var (_, input) = InputProcessor.Process(stream);

        input.Should().NotBeNull($"{Path.GetFileName(filePath)} should parse");
        input!.ToolName.Should().Be("PowerShell");
        input.Input.Should().BeOfType<PowerShellInput>();

        // The approver should not throw and should return a real decision for every example.
        var approver = new _FixedRootApprover(Path.GetDirectoryName(filePath)!);
        var output = approver.Handle(input);

        output.Should().NotBeNull($"{Path.GetFileName(filePath)} should produce a decision");
    }

    private static string? _examplesDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "examples");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private sealed class _FixedRootApprover : InsideProjectAllowedApprover
    {
        private readonly string _root;
        public _FixedRootApprover(string root) { _root = root; }
        protected override string? FindProjectFolder() => _root;
    }
}
