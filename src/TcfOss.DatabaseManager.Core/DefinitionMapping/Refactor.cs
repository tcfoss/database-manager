using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public abstract record Refactor(string UniqueId, SchemaIdentifier SchemaId)
{
    public record TableRename(string UniqueId, SchemaIdentifier SchemaId, ObjectIdentifier OldName, ObjectIdentifier NewName) : Refactor(UniqueId, SchemaId);

    public record ColumnRename(string UniqueId, SchemaIdentifier SchemaId, ColumnIdentifier OldName, ColumnIdentifier NewName) : Refactor(UniqueId, SchemaId);
}
