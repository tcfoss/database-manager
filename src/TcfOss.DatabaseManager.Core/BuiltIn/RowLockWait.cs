using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

[StringEnum("NOWAIT")]
[StringEnum("SKIP LOCKED")]
public sealed partial class RowLockWait;
