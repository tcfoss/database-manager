using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("ASC", "Ascending")]
[StringEnum("DESC", "Descending")]
public sealed partial class Direction;
