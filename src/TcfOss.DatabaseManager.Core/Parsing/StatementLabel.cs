using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Lexing.Tokens;

namespace TcfOss.DatabaseManager.Core.Parsing;

public readonly record struct StatementLabel(
    Identifier Label,
    int StartPosition,
    StatementLabelDialect Dialect,
    NonSqlList? PreNonSql,
    NonSqlList? PgMiddleNonSql,
    NonSqlList? PgClosingNonSql);

public enum StatementLabelDialect
{
    My,
    Pg,
}
