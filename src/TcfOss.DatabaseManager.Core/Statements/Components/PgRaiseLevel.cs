using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("DEBUG")]
[StringEnum("LOG")]
[StringEnum("INFO")]
[StringEnum("NOTICE")]
[StringEnum("WARNING")]
[StringEnum("EXCEPTION")]
public sealed partial class PgRaiseLevel;
