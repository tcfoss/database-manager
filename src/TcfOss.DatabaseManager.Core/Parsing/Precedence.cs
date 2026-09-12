namespace TcfOss.DatabaseManager.Core.Parsing;

public enum Precedence
{
    DoubleColon,
    AtTimezone,
    MultiplyDivide,
    AddSubtract,
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
