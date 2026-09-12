using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("BTREE")]
[StringEnum("HASH")]
[StringEnum("GIST")]
[StringEnum("SPGIST", "SpGist")]
[StringEnum("GIN")]
[StringEnum("BRIN")]
[StringEnum("RTREE")]
public sealed partial class IndexMethod;
