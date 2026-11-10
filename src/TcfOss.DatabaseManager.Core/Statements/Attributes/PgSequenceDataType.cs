using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("SMALLINT", "SmallInt", "INT2")]
[StringEnum("INTEGER", "Integer", "INT", "INT4")]
[StringEnum("BIGINT", "BigInt", "INT8")]
public sealed partial class PgSequenceDataType;
