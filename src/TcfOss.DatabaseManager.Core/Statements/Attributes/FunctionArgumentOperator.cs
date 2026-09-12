using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("=", "EqualSign")]
[StringEnum(":=", "Assignment")]
[StringEnum("=>", "FatArrow")]
public sealed partial class FunctionArgumentOperator;
