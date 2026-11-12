namespace TcfOss.DatabaseManager.Core.Parsing;

public static class GrammarExtensions
{
    public static bool Any(this Grammar grammar, Grammar flags)
    {
        return (grammar & flags) != 0;
    }
}
