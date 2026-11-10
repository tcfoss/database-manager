using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

[StringEnum("CURRENT ROW")]
[StringEnum("GROUP")]
[StringEnum("TIES")]
[StringEnum("NO OTHERS")]
public sealed partial class WindowFrameExclusion;
