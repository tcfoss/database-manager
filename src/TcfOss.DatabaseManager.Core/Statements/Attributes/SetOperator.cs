using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("UNION")]
[StringEnum("EXCEPT")]
[StringEnum("INTERSECT")]
public sealed partial class SetOperator;
