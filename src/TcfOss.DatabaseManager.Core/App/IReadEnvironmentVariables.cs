namespace TcfOss.DatabaseManager.Core.App;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface IReadEnvironmentVariables
{
    string? TryGetValue(string name);
    string GetValue(string name);
    string SubstituteVariables(string input);
}
