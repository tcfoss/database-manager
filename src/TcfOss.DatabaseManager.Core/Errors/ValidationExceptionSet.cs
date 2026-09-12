namespace TcfOss.DatabaseManager.Core.Errors;

public class ValidationExceptionSet(List<SourcedException> exceptions) : SourcedException(GetMessage(exceptions))
{
    public List<SourcedException> Exceptions { get; } = exceptions;

    private static string GetMessage(IEnumerable<SourcedException> exceptions)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, exceptions.Select(x => x.Message));
    }
}
