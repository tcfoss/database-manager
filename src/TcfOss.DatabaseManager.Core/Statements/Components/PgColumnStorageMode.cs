using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("PLAIN")]
[StringEnum("EXTERNAL")]
[StringEnum("EXTENDED")]
[StringEnum("MAIN")]
[StringEnum("DEFAULT")]
public sealed partial class PgColumnStorageMode;
