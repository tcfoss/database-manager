using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("MESSAGE")]
[StringEnum("DETAIL")]
[StringEnum("HINT")]
[StringEnum("ERRCODE")]
[StringEnum("COLUMN")]
[StringEnum("CONSTRAINT")]
[StringEnum("DATATYPE")]
[StringEnum("TABLE")]
[StringEnum("SCHEMA")]
public sealed partial class PgRaiseOptionName;
