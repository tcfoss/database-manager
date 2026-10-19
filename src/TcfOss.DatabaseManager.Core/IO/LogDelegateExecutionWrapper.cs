namespace TcfOss.DatabaseManager.Core.IO;

public readonly struct LogDelegateExecutionWrapper<T>(T source, Func<T, string>? selector)
{
    public override string ToString()
    {
        if (selector != null)
        {
            return selector.Invoke(source);
        }

        return source?.ToString() ?? string.Empty;
    }
}
