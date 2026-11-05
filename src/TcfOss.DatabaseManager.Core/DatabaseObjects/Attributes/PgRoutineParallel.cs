using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("UNSAFE")]
[StringEnum("RESTRICTED")]
[StringEnum("SAFE")]
public sealed partial class PgRoutineParallel;
