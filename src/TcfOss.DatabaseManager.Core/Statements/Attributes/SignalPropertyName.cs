using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("CLASS_ORIGIN")]
[StringEnum("SUBCLASS_ORIGIN")]
[StringEnum("MESSAGE_TEXT")]
[StringEnum("MYSQL_ERRNO", "MySqlErrno")]
[StringEnum("CONSTRAINT_CATALOG")]
[StringEnum("CONSTRAINT_SCHEMA")]
[StringEnum("CONSTRAINT_NAME")]
[StringEnum("CATALOG_NAME")]
[StringEnum("SCHEMA_NAME")]
[StringEnum("TABLE_NAME")]
[StringEnum("COLUMN_NAME")]
[StringEnum("CURSOR_NAME")]
public sealed partial class SignalPropertyName;
