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
    StringConcat,
    PgOperator,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    Exponentiation,
    Between,
    Comparison,
    Like,
    Escape,
    Is,
    UnaryNot,
    LogicalAnd,
    LogicalOr,
    LogicalXor,
}
