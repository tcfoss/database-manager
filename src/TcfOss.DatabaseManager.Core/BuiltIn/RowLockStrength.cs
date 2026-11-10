using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

[StringEnum("UPDATE")]
[StringEnum("NO KEY UPDATE")]
[StringEnum("SHARE")]
[StringEnum("KEY SHARE")]
public sealed partial class RowLockStrength;
