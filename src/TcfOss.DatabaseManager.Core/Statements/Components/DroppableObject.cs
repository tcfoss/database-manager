using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

[StringEnum("TABLE")]
[StringEnum("PROCEDURE")]
[StringEnum("FUNCTION")]
[StringEnum("TRIGGER")]
[StringEnum("VIEW")]
[StringEnum("EVENT")]
[StringEnum("SCHEMA")]
[StringEnum("DATABASE")]
public sealed partial class DroppableObject;
