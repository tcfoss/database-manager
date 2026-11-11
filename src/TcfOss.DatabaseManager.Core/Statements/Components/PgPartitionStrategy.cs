using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("RANGE")]
[StringEnum("LIST")]
[StringEnum("HASH")]
public sealed partial class PgPartitionStrategy;
