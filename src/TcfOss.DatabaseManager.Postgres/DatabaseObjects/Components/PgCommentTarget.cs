using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects.Components;

public abstract record PgCommentTarget
{
    public abstract PgStatementCommentTarget ToStatementTarget();

    public record DatabaseObject(PgCommentObjectType ObjectType, ObjectIdentifier Name) : PgCommentTarget
    {
        public override PgStatementCommentTarget ToStatementTarget()
        {
            return new PgStatementCommentTarget.DatabaseObject(ObjectType, Name.ToObjectName(2));
        }
    }

    public record Column(ObjectIdentifier Table, Identifier Name) : PgCommentTarget
    {
        public override PgStatementCommentTarget ToStatementTarget()
        {
            return new PgStatementCommentTarget.Column(new ObjectName([.. Table.ToObjectName(2).Values, Name]));
        }
    }

    public record Routine(PgCommentObjectType ObjectType, ObjectIdentifier Name, SqlValueList<DataType> ArgumentTypes) : PgCommentTarget
    {
        public override PgStatementCommentTarget ToStatementTarget()
        {
            return new PgStatementCommentTarget.Routine(ObjectType, Name.ToObjectName(2),
                [.. ArgumentTypes.Select(dataType => new StatementRoutineParameter.General(null, dataType, null, null))]);
        }
    }

    public record Trigger(ObjectIdentifier Table, Identifier Name) : PgCommentTarget
    {
        public override PgStatementCommentTarget ToStatementTarget()
        {
            return new PgStatementCommentTarget.Trigger(Name, Table.ToObjectName(2));
        }
    }
}
