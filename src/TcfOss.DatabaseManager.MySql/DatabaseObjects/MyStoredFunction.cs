using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public record MyStoredFunction(ObjectIdentifier Name, SqlValueList<RoutineParameter> Parameters, DataType ReturnType, Statement Body)
    : StoredFunction(Name, Parameters, ReturnType, Body)
{
    public required Definer Definer { get; init; }
    public required SecurityContext SecurityContext { get; init; }
    public bool Aggregate { get; init; }
    public Comment? Comment { get; init; }
    public bool Deterministic { get; init; }
    public SqlDataRelation? SqlDataRelation { get; init; }

    public virtual bool Equals(MyStoredFunction? other)
    {
        // Exclude RawBodyText
        return base.Equals(other)
            && Aggregate == other.Aggregate
            && Definer == other.Definer
            && SecurityContext == other.SecurityContext
            && Comment == other.Comment
            && Deterministic == other.Deterministic
            && SqlDataRelation == other.SqlDataRelation;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Aggregate, Definer, SecurityContext, Comment, Deterministic, SqlDataRelation);
    }

    public override CreateFunction ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateFunction(Name.ToObjectName(includeSchema ? 2 : 1), Parameters, ReturnType, Body)
        {
            Definer = Definer,
            MyCharacteristic = new MyRoutineCharacteristic()
            {
                SecurityContext = SecurityContext,
                Deterministic = Deterministic,
                Relation = SqlDataRelation,
                Comment = Comment,
                Language = "SQL",
            },
            Aggregate = Aggregate,
            Meta = new MetaData { RawText = RawBodyText }
        };
    }
}
