using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

[StringEnum("SIGNED")]
[StringEnum("UNSIGNED")]
[StringEnum("ZEROFILL")]
public sealed partial class MySqlNumericAttribute;
