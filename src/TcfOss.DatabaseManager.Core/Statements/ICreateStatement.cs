using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.Statements;

public interface ICreateStatement
{
    public ObjectName Name { get; }
}
