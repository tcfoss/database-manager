using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("CONTINUE")]
[StringEnum("EXIT")]
[StringEnum("UNDO")]
public sealed partial class HandlerAction;
