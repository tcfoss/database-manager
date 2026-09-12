using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("WITH CONSISTENT SNAPSHOT")]
[StringEnum("READ WRITE")]
[StringEnum("READ ONLY")]
public sealed partial class MySqlTransactionCharacteristic;
