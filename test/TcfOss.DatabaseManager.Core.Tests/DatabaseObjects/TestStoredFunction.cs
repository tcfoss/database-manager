using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public record TestStoredFunction(ObjectIdentifier Name, SqlValueList<RoutineParameter> Parameters, DataType ReturnType, Statement Body)
    : StoredFunction(Name, Parameters, ReturnType, Body)
{
    public override CreateFunction ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateFunction(Name.ToObjectName(includeSchema ? 2 : 1), Parameters, ReturnType, Body)
        {
            Meta = new MetaData { RawText = RawBodyText }
        };
    }
}
