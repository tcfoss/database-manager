using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("ENABLE")]
[StringEnum("DISABLE")]
[StringEnum("DISABLE ON REPLICA")]
[StringEnum("DISABLE ON SLAVE")]
public sealed partial class EventEnabledStatus;

