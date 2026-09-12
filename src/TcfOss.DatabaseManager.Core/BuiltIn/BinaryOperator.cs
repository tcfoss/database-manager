using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

[StringEnum("+", "Add")]
[StringEnum("-", "Subtract")]
[StringEnum("*", "Multiply")]
[StringEnum("/", "Divide")]
[StringEnum("%", "Modulo")]
[StringEnum("=", "Equal")]
[StringEnum("<>", "NotEqual")]
[StringEnum("||", "StringConcat")]
[StringEnum(">", "GreaterThan")]
[StringEnum(">=", "GreaterThanOrEqual")]
[StringEnum("<", "LessThan")]
[StringEnum("<=", "LessThanOrEqual")]
[StringEnum("AND", "And")]
[StringEnum("OR", "Or")]
[StringEnum("XOR", "Xor")]
[StringEnum("&", "BitwiseAnd")]
[StringEnum("|", "BitwiseOr")]
[StringEnum("^", "BitwiseXor")]
public sealed partial class BinaryOperator;
