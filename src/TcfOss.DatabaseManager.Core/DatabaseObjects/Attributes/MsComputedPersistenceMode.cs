using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("PERSISTED")]
[StringEnum("PERSISTED NOT NULL")]
public sealed partial class MsComputedPersistenceMode;
