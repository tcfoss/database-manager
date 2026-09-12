using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface IResolveDependencies
{
    Dictionary<ObjectHandle, DbObjectNode> ConstructNodeMap(List<IDatabaseObject> databaseObjects, IHaveObjects objectProvider, PseudoTableSet pseudoTableSet, IFunctionNameProvider functionNameProvider, DifferFormatManager formatManager, NameHandling nameHandling);
}
