using System.Text.RegularExpressions;
using TcfOss.DatabaseManager.Core.Errors;

namespace TcfOss.DatabaseManager.Core.App;

public partial class EnvironmentVariableReader : IReadEnvironmentVariables
{
    private static readonly Regex s_variableRegex = GetEnvironmentVariableRegex();

    public string? TryGetValue(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }

    public virtual string GetValue(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
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

        Match match = s_variableRegex.Match(input);
        while (match.Success)

        {
            string varName = match.Groups[1].Value;
            string varValue = GetValue(varName);
            input = input.Replace(match.Value, varValue);
            match = s_variableRegex.Match(input);
        }

        return input;
    }


    [GeneratedRegex(@"\$\{ENV:([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled)]
    private static partial Regex GetEnvironmentVariableRegex();
}
