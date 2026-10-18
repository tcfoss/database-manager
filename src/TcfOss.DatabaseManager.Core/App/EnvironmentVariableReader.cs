namespace TcfOss.DatabaseManager.Core.App;

public class EnvironmentVariableReader : EnvironmentVariableReaderBase
{
    public override string? TryGetValue(string name)
    {
        return Environment.GetEnvironmentVariable(name);
    }
}
