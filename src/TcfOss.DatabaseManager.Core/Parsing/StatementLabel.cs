using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

public readonly struct StatementLabel(Word word, int sourceId)
{
    public Identifier Label { get; } = word.ToIdentifier(sourceId);
    public NonSqlList? PreNonSql { get; } = word.PreNonSql;
    public int StartPosition { get; } = word.Location.Position;
}
