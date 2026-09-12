using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("CONTAINS SQL")]
[StringEnum("NO SQL")]
[StringEnum("READS SQL DATA")]
[StringEnum("MODIFIES SQL DATA")]
public sealed partial class SqlDataRelation;
