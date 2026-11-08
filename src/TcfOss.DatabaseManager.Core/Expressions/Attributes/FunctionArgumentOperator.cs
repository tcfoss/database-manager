using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Expressions.Attributes;

[StringEnum("=", "EqualSign")]
[StringEnum(":=", "Assignment")]
[StringEnum("=>", "FatArrow")]
public sealed partial class FunctionArgumentOperator;
