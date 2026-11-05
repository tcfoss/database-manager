using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("IMMUTABLE")]
[StringEnum("STABLE")]
[StringEnum("VOLATILE")]
public sealed partial class PgRoutineVolatility;
