using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("TRIGGER", "Trigger")]
[StringEnum("VOID", "Void")]
[StringEnum("RECORD", "Record")]
public sealed partial class PgPseudoType;
