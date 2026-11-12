using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("DEFAULT")]
[StringEnum(":=", "Assignment")]
[StringEnum("=", "Equal")]
public sealed partial class PgInitializerOperator;
