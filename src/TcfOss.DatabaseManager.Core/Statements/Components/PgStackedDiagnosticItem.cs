using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("RETURNED_SQLSTATE")]
[StringEnum("MESSAGE_TEXT")]
[StringEnum("PG_EXCEPTION_DETAIL")]
[StringEnum("PG_EXCEPTION_HINT")]
[StringEnum("PG_EXCEPTION_CONTEXT")]
[StringEnum("SCHEMA_NAME")]
[StringEnum("TABLE_NAME")]
[StringEnum("COLUMN_NAME")]
[StringEnum("CONSTRAINT_NAME")]
[StringEnum("PG_DATATYPE_NAME")]
public sealed partial class PgStackedDiagnosticItem;
