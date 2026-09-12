using TcfOss.StringEnumGenerator;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;

[StringEnum("DEFINER")]
[StringEnum("INVOKER")]
public sealed partial class SecurityContext;
