
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public record TestStoredProcedure(ObjectIdentifier Name, SqlValueList<RoutineParameter> Parameters, Statement Body)
    : StoredProcedure(Name, Parameters, Body)
{
    public override CreateProcedure ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateProcedure(Name.ToObjectName(includeSchema ? 2 : 1), Parameters, Body)
        {
            Meta = new MetaData { RawText = RawBodyText }
        };
    }
}
