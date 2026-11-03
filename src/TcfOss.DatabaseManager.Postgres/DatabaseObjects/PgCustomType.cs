using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public abstract record PgCustomType(ObjectIdentifier Name) : IDatabaseObject<Statement>
{
    public ObjectType ObjectType => ObjectType.Type;

    public abstract Statement ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null);

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return ToCreateStatement(true).GetReferencedItems(context);
    }

    public record Enumerated(ObjectIdentifier Name, SqlValueList<string> Labels) : PgCustomType(Name)
    {
        public override CreateType.Enumerated ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
        {
            return new CreateType.Enumerated(Name.ToObjectName(includeSchema ? 2 : 1), [.. Labels]);
        }
    }

    public record Composite(ObjectIdentifier Name, SqlValueList<PgCompositeTypeField> Attributes) : PgCustomType(Name)
    {
        public override CreateType.Composite ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
        {
            return new CreateType.Composite(Name.ToObjectName(includeSchema ? 2 : 1),
                [.. Attributes.Select(field => new CompositeTypeField(field.Name, field.DataType, field.Collation))]);
        }
    }

    public record Domain(ObjectIdentifier Name, DataType BaseType) : PgCustomType(Name)
    {
        public ObjectName? Collation { get; init; }
        public Expression? Default { get; init; }
        public SqlValueList<PgDomainConstraint> Constraints { get; init; } = [];

        public override CreateDomain ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
        {
            return new CreateDomain(Name.ToObjectName(includeSchema ? 2 : 1), BaseType)
            {
                Collation = Collation,
                Default = Default,
                Constraints = [.. Constraints.Select(constraint => constraint switch
                {
                    PgDomainConstraint.Nullability nullability => (StatementDomainConstraint)new StatementDomainConstraint.Nullability(nullability.NotNull, nullability.Name),
                    PgDomainConstraint.Check check => new StatementDomainConstraint.Check(check.Expression, check.Name),
                    _ => throw new NotImplementedException()
                })]
            };
        }
    }
}
