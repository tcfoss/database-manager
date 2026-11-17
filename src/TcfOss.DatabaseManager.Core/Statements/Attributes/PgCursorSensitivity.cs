using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("ASENSITIVE")]
[StringEnum("INSENSITIVE")]
public sealed partial class PgCursorSensitivity;
