using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Postgres.BuiltIn;

[StringEnum("YEAR", "Year")]
[StringEnum("MONTH", "Month")]
[StringEnum("DAY", "Day")]
[StringEnum("HOUR", "Hour")]
[StringEnum("MINUTE", "Minute")]
[StringEnum("SECOND", "Second")]
public sealed partial class PgIntervalField;