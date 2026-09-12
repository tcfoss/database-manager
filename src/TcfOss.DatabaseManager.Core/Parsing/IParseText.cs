using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Parsing;

public interface IParseText
{
    /// <summary>
    /// Parses the provided text and returns a sequence of statements.
    /// Throws exceptions with useful context if possible.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="filename"></param>
    /// <param name="sourceId"></param>
    /// <returns></returns>
    public SqlValueList<Statement> ParseText(string text, string? filename = null, int sourceId = 0);
}
