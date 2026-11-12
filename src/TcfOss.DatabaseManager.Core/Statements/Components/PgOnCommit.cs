using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("PRESERVE ROWS")]
[StringEnum("DELETE ROWS")]
[StringEnum("DROP")]
public sealed partial class PgOnCommit;
