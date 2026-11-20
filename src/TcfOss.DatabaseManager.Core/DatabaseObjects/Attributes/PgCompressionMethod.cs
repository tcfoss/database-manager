using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("default")]
[StringEnum("pglz")]
[StringEnum("lz4")]
public sealed partial class PgCompressionMethod;
