using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum(":=", "Assignment")]
[StringEnum("=", "Equal")]
public sealed partial class PgAssignmentOperator;
