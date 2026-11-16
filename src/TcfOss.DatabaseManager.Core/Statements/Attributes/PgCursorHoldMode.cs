using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("WITH HOLD")]
[StringEnum("WITHOUT HOLD")]
public sealed partial class PgCursorHoldMode;
