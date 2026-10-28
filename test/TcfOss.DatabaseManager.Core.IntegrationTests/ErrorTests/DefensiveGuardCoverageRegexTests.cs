using System.Text.RegularExpressions;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.ErrorTests;

public partial class DefensiveGuardCoverageRegexTests
{
    [Fact]
    public void AllSourceGuards_MatchCoverageExclusionRegex()
    {
        string root = FindRepositoryRoot();

        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "active-pr.yml"));
        Match workflowMatch = WorkflowCoverageRegex().Match(workflow);
        Assert.True(workflowMatch.Success, "unreachable-coverage-regex not found in active-pr.yml");
        string pattern = workflowMatch.Groups["regex"].Value;

        string script = File.ReadAllText(Path.Combine(root, "tools", "dbman-dev", "src", "dbman_dev", "coverage", "generate_report_mtp.py"));
        Assert.Contains($"r\"{pattern}\"", script);
        var regex = new Regex(pattern);

        string definitionFile = Path.Combine("Errors", "DefensiveGuardException.cs");
        List<string> unmatched = [];
        int guardCount = 0;
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith(definitionFile, StringComparison.Ordinal) || IsBuildOutput(file))
            {
                continue;
            }
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("DefensiveGuardException(", StringComparison.Ordinal))
                {
                    continue;
                }
                guardCount++;
                if (!regex.IsMatch(lines[i]))
                {
                    unmatched.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }
        }

        Assert.NotEqual(0, guardCount);
        Assert.Empty(unmatched);
    }

    private static bool IsBuildOutput(string file)
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        return file.Contains($"{separator}obj{separator}", StringComparison.Ordinal)
            || file.Contains($"{separator}bin{separator}", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DatabaseManager.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory.FullName;
    }

    [GeneratedRegex(@"unreachable-coverage-regex:\s*'(?<regex>[^']+)'")]
    private static partial Regex WorkflowCoverageRegex();
}
