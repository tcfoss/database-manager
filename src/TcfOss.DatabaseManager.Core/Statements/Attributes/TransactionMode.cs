using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("ISOLATION LEVEL SERIALIZABLE")]
[StringEnum("ISOLATION LEVEL REPEATABLE READ")]
[StringEnum("ISOLATION LEVEL READ COMMITTED")]
[StringEnum("ISOLATION LEVEL READ UNCOMMITTED")]
[StringEnum("READ WRITE")]
[StringEnum("READ ONLY")]
[StringEnum("DEFERRABLE")]
[StringEnum("NOT DEFERRABLE")]
[StringEnum("WITH CONSISTENT SNAPSHOT")]
public sealed partial class TransactionMode;
