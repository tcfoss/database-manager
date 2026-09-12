using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("INSERT")]
[StringEnum("UPDATE")]
[StringEnum("DELETE")]
public sealed partial class TriggerEvent;
