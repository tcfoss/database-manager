using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("ENABLE")]
[StringEnum("DISABLE")]
[StringEnum("FORCE")]
[StringEnum("NO FORCE", "NoForce")]
public sealed partial class PgRowSecurityMode;
