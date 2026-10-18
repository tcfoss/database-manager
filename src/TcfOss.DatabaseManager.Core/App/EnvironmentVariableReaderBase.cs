using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.App;

public abstract partial class EnvironmentVariableReaderBase
{
    public abstract string? TryGetValue(string name);

    public string GetValue(string name)
    {
        string? value = TryGetValue(name);
        if (value is not null)
        {
            return value;
        }
        throw new ConfigurationException.EnvironmentVariableNotSet(name);
    }

    public string SubstituteVariables(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return GetEnvironmentVariableRegex().Replace(input, match => GetValue(match.Groups[1].Value));
    }

    [GeneratedRegex(@"\$\{ENV:([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled)]
    private static partial Regex GetEnvironmentVariableRegex();
}
