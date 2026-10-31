using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public abstract record PgCustomType(ObjectIdentifier Name) : IHaveObjectIdentifierName
{
    public record Enumerated(ObjectIdentifier Name, SqlValueList<string> Labels) : PgCustomType(Name);

    public record Composite(ObjectIdentifier Name, SqlValueList<PgCompositeTypeField> Attributes) : PgCustomType(Name);

    public record Domain(ObjectIdentifier Name, DataType BaseType) : PgCustomType(Name)
    {
        public ObjectName? Collation { get; init; }
        public Expression? Default { get; init; }
        public SqlValueList<PgDomainConstraint> Constraints { get; init; } = [];
    }
}
