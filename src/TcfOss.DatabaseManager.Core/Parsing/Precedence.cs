namespace TcfOss.DatabaseManager.Core.Parsing;

public enum Precedence
{
    ArraySubscript,
    DoubleColon,
    UnarySign,
    Collate,
    AtTimezone,
    MultiplyDivide,
    AddSubtract,
    PgConcat,
    PgOperator,
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
