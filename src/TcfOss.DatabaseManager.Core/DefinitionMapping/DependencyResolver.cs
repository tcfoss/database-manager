using Microsoft.Extensions.Logging;
using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public partial class DependencyResolver(ILogger logger)
    : IResolveDependencies
{
    private readonly ILogger _logger = logger;

    public Dictionary<ObjectHandle, DbObjectNode> ConstructNodeMap(List<IDatabaseObject> databaseObjects, IHaveObjects objectProvider, PseudoTableSet pseudoTableSet, IFunctionNameProvider functionNameProvider, DifferFormatManager formatManager, NameHandling nameHandling)
    {
        var allHandles = new HashSet<ObjectHandle>(databaseObjects.Select(o => ObjectHandle.Create(o.Name, nameHandling)));

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>(databaseObjects.Count);
        var manager = new ReferencedItemsManager()
        {
            NameHandling = nameHandling,
            Definition = objectProvider,
            PseudoTables = pseudoTableSet,
            FunctionNameProvider = functionNameProvider,
            Filters = ObjectNameFilters.AnyObject,
        };

        foreach (IDatabaseObject dbObject in databaseObjects)
        {
            var handle = ObjectHandle.Create(dbObject.Name, nameHandling);
            manager.ActiveSchema = dbObject.Name.Schema;

            List<DependencyNode> dependencies = [];

            foreach (ItemRef itemRef in dbObject.GetReferencedItems(manager))
            {
                if (itemRef.ObjectHandle == null || itemRef.ObjectHandle == handle || !allHandles.Contains(itemRef.ObjectHandle.Value))
                {
                    continue;
                }

                dependencies.Add(new DependencyNode
                {
                    Handle = itemRef.ObjectHandle.Value,
                    ItemType = itemRef.Type,
                    DependencyType = ClassifyDependency(dbObject.ObjectType, itemRef.Type)
                });
            }

            nodeMap[handle] = new DbObjectNode
            {
                Handle = handle,
                Name = dbObject.Name,
                ObjectType = dbObject.ObjectType,
                CreateStatement = dbObject.ToCreateStatement(formatManager.Formatting.ObjectNamePrefixWithSchema, formatManager),
                Dependencies = dependencies,
                HardDependencyHandles = [.. dependencies.Where(d => d.DependencyType == DependencyType.Hard).Select(d => d.Handle)]
            };
        }

        return nodeMap;
    }

    public List<DbObjectNode> TopologicalSort(Dictionary<ObjectHandle, DbObjectNode> nodeMap)
    {
        var inDegreeMap = nodeMap.ToDictionary(kvp => kvp.Key, _ => 0);
        foreach (DbObjectNode node in nodeMap.Values)
        {
            foreach (ObjectHandle _ in node.HardDependencyHandles)
            {
                inDegreeMap[node.Handle]++;
            }
        }

        var reverseMap = nodeMap.ToDictionary(kvp => kvp.Key, _ => new List<ObjectHandle>());
        foreach (DbObjectNode node in nodeMap.Values)
        {
            foreach (ObjectHandle dependency in node.HardDependencyHandles)
            {
                reverseMap[dependency].Add(node.Handle);
            }
        }

        var queue = new PriorityQueue<ObjectHandle, ObjectHandle>();

        foreach ((ObjectHandle handle, int degree) in inDegreeMap)
        {
            if (degree == 0)
            {
                queue.Enqueue(handle, handle);
            }
        }

        var result = new List<DbObjectNode>();

        while (queue.Count > 0)
        {
            ObjectHandle handle = queue.Dequeue();
            DbObjectNode node = nodeMap[handle];
            result.Add(node);

            foreach (ObjectHandle dependent in reverseMap[handle])
            {
                inDegreeMap[dependent]--;
                if (inDegreeMap[dependent] == 0)
                {
                    queue.Enqueue(dependent, dependent);
                }
            }
        }

        if (result.Count != nodeMap.Count)
        {
            var cyclicNodes = nodeMap.Values
                .Where(n => result.All(r => r.Handle != n.Handle))
                .ToList();

            LogCircularDependency(cyclicNodes);

            foreach (DbObjectNode node in cyclicNodes)
            {
                result.Add(node);
            }
        }

        return result;
    }

    public static DependencyType ClassifyDependency(ObjectType itemRefType, ItemType dependencyRefType)
    {
        if (itemRefType == ObjectType.View)
        {
            if (dependencyRefType is ItemType.View or ItemType.Function)
            {
                return DependencyType.Hard;
            }
        }

        if (itemRefType == ObjectType.Function)
        {
            if (dependencyRefType is ItemType.View or ItemType.Function)
            {
                return DependencyType.Hard;
            }
        }

        // if (itemRefType == ObjectType.Procedure)
        // {
        //     return DependencyType.Soft;
        // }

        return DependencyType.Soft;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Circular dependency detected: {CyclicObjects}"
    )]
    public partial void LogCircularDependency(IEnumerable<DbObjectNode> cyclicObjects);
}
