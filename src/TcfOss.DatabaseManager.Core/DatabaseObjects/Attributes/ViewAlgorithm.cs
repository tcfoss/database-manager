using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("UNDEFINED")]
[StringEnum("MERGE")]
[StringEnum("TEMPTABLE", "TempTable")]
public sealed partial class ViewAlgorithm;
