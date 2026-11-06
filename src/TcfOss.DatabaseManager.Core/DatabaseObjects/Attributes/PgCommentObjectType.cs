using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("TABLE")]
[StringEnum("VIEW")]
[StringEnum("INDEX")]
[StringEnum("TYPE")]
[StringEnum("DOMAIN")]
[StringEnum("SEQUENCE")]
[StringEnum("FUNCTION")]
[StringEnum("PROCEDURE")]
public sealed partial class PgCommentObjectType;
