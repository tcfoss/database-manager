using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

[StringEnum("INPUT")]
[StringEnum("OUTPUT")]
[StringEnum("RECEIVE")]
[StringEnum("SEND")]
[StringEnum("TYPMOD_IN")]
[StringEnum("TYPMOD_OUT")]
[StringEnum("ANALYZE", "Analyze", "ANALYSE")]
[StringEnum("SUBSCRIPT")]
[StringEnum("INTERNALLENGTH", "InternalLength")]
[StringEnum("PASSEDBYVALUE", "PassedByValue")]
[StringEnum("ALIGNMENT")]
[StringEnum("STORAGE")]
[StringEnum("LIKE")]
[StringEnum("CATEGORY")]
[StringEnum("PREFERRED")]
[StringEnum("DEFAULT")]
[StringEnum("ELEMENT")]
[StringEnum("DELIMITER")]
[StringEnum("COLLATABLE")]
public sealed partial class PgBaseTypeOptionName;
