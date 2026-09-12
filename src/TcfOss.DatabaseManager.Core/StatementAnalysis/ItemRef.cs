using System.Diagnostics.CodeAnalysis;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public abstract record ItemRef(ItemType Type)
{
    public abstract SqlValueList<Identifier> Identifiers { get; }
    public abstract SourceRef? Source { get; }

    public bool IsTable => Type == ItemType.Table;

    [MemberNotNullWhen(true, nameof(ObjectHandle))]
    public bool IsObject => Type == ItemType.Table
            || Type == ItemType.View
            || Type == ItemType.Procedure
            || Type == ItemType.Function;

    public ObjectHandle? ObjectHandle { get; init; }

    [MemberNotNullWhen(true, nameof(ColumnIdentifier))]
    [MemberNotNullWhen(true, nameof(ObjectHandle))]
    public bool IsColumn => Type == ItemType.TableColumn || Type == ItemType.ViewColumn;

    public ColumnIdentifier? ColumnIdentifier { get; init; }

    public record ObjectNameRef(ItemType Type, ObjectName Name)
        : ItemRef(Type)
    {
        public override SqlValueList<Identifier> Identifiers => Name.Values;
        public override SourceRef? Source => Name.Source;
    }

    public record SingleIdentifierRef(ItemType Type, SingleIdentifier Identifier)
        : ItemRef(Type)
    {
        public override SqlValueList<Identifier> Identifiers => [Identifier.Identifier];
        public override SourceRef? Source => Identifier.Source;
    }

    public record CompoundIdentifierRef(ItemType Type, CompoundIdentifier Identifier)
        : ItemRef(Type)
    {
        public override SqlValueList<Identifier> Identifiers => Identifier.Identifiers;
        public override SourceRef? Source => Identifier.Source;
    }
}
