using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("STRICT")]
[StringEnum("CALLED ON NULL INPUT")]
[StringEnum("RETURNS NULL ON NULL INPUT")]
public sealed partial class PgRoutineNullHandling;
