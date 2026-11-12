using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("DEFAULT")]
[StringEnum("FULL")]
[StringEnum("NOTHING")]
public sealed partial class PgReplicaIdentityMode;
