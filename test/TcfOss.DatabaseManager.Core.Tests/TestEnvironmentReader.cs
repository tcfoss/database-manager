using TcfOss.DatabaseManager.Core.App;

namespace TcfOss.DatabaseManager.Core.Tests;

public class TestEnvironmentReader(Dictionary<string, string> variables) : EnvironmentVariableReaderBase
{
    private readonly Dictionary<string, string> _variables = variables;

    public override string? TryGetValue(string name)
    {
        return _variables.TryGetValue(name, out var value) ? value : null;
    }
}
