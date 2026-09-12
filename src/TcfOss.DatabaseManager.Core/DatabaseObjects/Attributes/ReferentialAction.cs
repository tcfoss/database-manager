using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("RESTRICT")]
[StringEnum("CASCADE")]
[StringEnum("SET NULL")]
[StringEnum("NO ACTION")]
[StringEnum("SET DEFAULT")]
public sealed partial class ReferentialAction;
