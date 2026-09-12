using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record struct NamedKeyPartList(Identifier? Name, SqlValueList<KeyPart> Columns);
