using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Postgres.DatabaseObjects;

public record PgStoredFunction(ObjectIdentifier Name, SqlValueList<RoutineParameter> Parameters,
    FunctionReturn ReturnType, Statement Body) : StoredFunction(Name, Parameters, ReturnType, Body)
{
    public PgRoutineOptions? Options { get; init; }

    public override CreateFunction ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateFunction(Name.ToObjectName(includeSchema ? 2 : 1),
            [.. Parameters.Select(parameter => parameter.ToStatementParameter())], ReturnType.ToStatementReturn(), Body)
        {
            PgOptions = Options,
            UseAsBeforeBody = Body is RoutineBodyLiteral,
            Meta = new MetaData { RawText = RawBodyText }
        };
    }
}
