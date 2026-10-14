
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.DatabaseObjects;

public record TestView(ObjectIdentifier Name, Select Body) : View(Name, Body)
{
    public override CreateView ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateView(Name.ToObjectName(includeSchema ? 2 : 1), Body);
    }
}
