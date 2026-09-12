using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("BEFORE")]
[StringEnum("AFTER")]
[StringEnum("INSTEAD OF")]
[StringEnum("FOR")]
public sealed partial class TriggerTime;
