using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("SCROLL")]
[StringEnum("NO SCROLL", "NoScroll")]
public sealed partial class PgCursorScrollMode;
