using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("ENABLE")]
[StringEnum("DISABLE")]
[StringEnum("ENABLE ALWAYS", "Always")]
[StringEnum("ENABLE REPLICA", "Replica")]
public sealed partial class PgTriggerStateMode;
