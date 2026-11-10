namespace TcfOss.DatabaseManager.Core.Parsing;

[Flags]
public enum ParseContext
{
    None = 0,
    DomainCheck = 1 << 0,
    DefaultExpression = 1 << 1,
    StringLiteralAsStatement = 1 << 2,
    BeginAtomic = 1 << 3,
    BetweenBound = 1 << 4,
    NarrowSqlOnly = 1 << 5
}
