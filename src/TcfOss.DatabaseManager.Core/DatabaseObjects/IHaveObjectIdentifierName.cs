using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public interface IHaveObjectIdentifierName
{
    ObjectIdentifier Name { get; }
}
