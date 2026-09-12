using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

[StringEnum("+", "Plus")]
[StringEnum("-", "Minus")]
[StringEnum("NOT")]
public sealed partial class UnaryOperator;
