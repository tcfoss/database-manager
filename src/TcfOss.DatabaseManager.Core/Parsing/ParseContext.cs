namespace TcfOss.DatabaseManager.Core.Parsing;

[Flags]
public enum ParseContext
{
    None = 0,
    DomainCheck = 1 << 0,
    DefaultExpression = 1 << 1,
    StringLiteralAsStatement = 1 << 2,
    ExpectBeginAtomic = 1 << 3,
    BeginAtomicBody = 1 << 4,
    BetweenBound = 1 << 5,
    PgProceduralBlock = 1 << 6,
}
