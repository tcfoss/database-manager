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
[StringEnum("INDEX")]
public sealed partial class DroppableObject;
