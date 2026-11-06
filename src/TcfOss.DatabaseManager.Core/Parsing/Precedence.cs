namespace TcfOss.DatabaseManager.Core.Parsing;

public enum Precedence
{
    DoubleColon,
    UnarySign,
    Collate,
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
