namespace TcfOss.DatabaseManager.Core.Parsing;

public enum Precedence
{
    DoubleColon,
    AtTimezone,
    MultiplyDivide,
    AddSubtract,
    PgConcat,
    Xor,
    Ampersand,
    Caret,
    Pipe,
    Between,
    Equals,
    Like,
    Is,
    UnaryNot,
    And,
    Or
}
