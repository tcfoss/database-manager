using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("AUTO")]
[StringEnum("ON")]
[StringEnum("OFF")]
public sealed partial class PgIndexBufferingMode;
