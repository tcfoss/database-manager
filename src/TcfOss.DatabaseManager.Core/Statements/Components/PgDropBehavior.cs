using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("CASCADE")]
[StringEnum("RESTRICT")]
public sealed partial class PgDropBehavior;
