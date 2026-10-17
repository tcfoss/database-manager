using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public record TestTrigger(ObjectIdentifier Name, ObjectIdentifier OnTable, Statement Body)
    : Trigger(Name, OnTable, Body)
{
    public override CreateTrigger ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null) => throw new NotImplementedException();
}
