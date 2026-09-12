using Microsoft.Extensions.Logging.Abstractions;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.DefinitionMapping;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Tests.DefinitionMapping;

public class DependencyResolverTests
{
    private readonly DependencyResolver _resolver = new(NullLogger.Instance);

    private static ObjectHandle Handle(string schema, string name)
    {
        ObjectIdentifier id = ObjectIdentifier.FromStrings("def", schema, name);
        return ObjectHandle.Create(id, NameHandling.None);
    }

    private static DbObjectNode Node(
        string schema,
        string name,
        ObjectType objectType,
        List<DependencyNode>? dependencies = null,
        HashSet<ObjectHandle>? hardDependencyHandles = null)
    {
        ObjectIdentifier objectId = ObjectIdentifier.FromStrings("def", schema, name);
        ObjectHandle handle = ObjectHandle.Create(objectId, NameHandling.None);

        dependencies ??= [];
        hardDependencyHandles ??= [];

        return new DbObjectNode
        {
            Handle = handle,
            Name = objectId,
            ObjectType = objectType,
            Dependencies = dependencies,
            HardDependencyHandles = hardDependencyHandles,
            CreateStatement = new StartTransaction(),
        };
    }

    private static DbObjectNode NodeWithHardDeps(
        string schema,
        string name,
        ObjectType objectType,
        params ObjectHandle[] hardDeps)
    {
        List<DependencyNode> dependencies = [.. hardDeps.Select(h => new DependencyNode
        {
            Handle = h,
            ItemType = ItemType.View,
            DependencyType = DependencyType.Hard,
        })];

        return Node(schema, name, objectType, dependencies, [.. hardDeps]);
    }

    private static DbObjectNode NodeWithSoftDeps(
        string schema,
        string name,
        ObjectType objectType,
        params ObjectHandle[] softDeps)
    {
        List<DependencyNode> dependencies = [.. softDeps.Select(h => new DependencyNode
        {
            Handle = h,
            ItemType = ItemType.Table,
            DependencyType = DependencyType.Soft,
        })];

        return Node(schema, name, objectType, dependencies);
    }

    #region TopologicalSort

    [Fact]
    public void TopologicalSort_EmptyMap_ReturnsEmpty()
    {
        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>();

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Empty(result);
    }

    [Fact]
    public void TopologicalSort_SingleNode_ReturnsThatNode()
    {
        DbObjectNode node = Node("s", "a", ObjectType.View);
        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [node.Handle] = node,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        var item = Assert.Single(result);
        Assert.Equal(node.Handle, item.Handle);
    }

    [Fact]
    public void TopologicalSort_IndependentNodes_OrderedByHandle()
    {
        // Nodes with no dependencies should be ordered deterministically by handle
        DbObjectNode nodeC = Node("s", "c_view", ObjectType.View);
        DbObjectNode nodeA = Node("s", "a_view", ObjectType.View);
        DbObjectNode nodeB = Node("s", "b_view", ObjectType.View);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeC.Handle] = nodeC,
            [nodeA.Handle] = nodeA,
            [nodeB.Handle] = nodeB,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Equal(3, result.Count);
        Assert.Equal(nodeA.Handle, result[0].Handle);
        Assert.Equal(nodeB.Handle, result[1].Handle);
        Assert.Equal(nodeC.Handle, result[2].Handle);
    }

    [Fact]
    public void TopologicalSort_LinearChain_DependenciesFirst()
    {
        // c_view depends on b_view depends on a_view
        // Expected order: a_view, b_view, c_view
        ObjectHandle handleA = Handle("s", "a_view");
        ObjectHandle handleB = Handle("s", "b_view");

        DbObjectNode nodeA = Node("s", "a_view", ObjectType.View);
        DbObjectNode nodeB = NodeWithHardDeps("s", "b_view", ObjectType.View, handleA);
        DbObjectNode nodeC = NodeWithHardDeps("s", "c_view", ObjectType.View, handleB);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeC.Handle] = nodeC,
            [nodeB.Handle] = nodeB,
            [nodeA.Handle] = nodeA,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Equal(3, result.Count);
        Assert.Equal(nodeA.Handle, result[0].Handle);
        Assert.Equal(nodeB.Handle, result[1].Handle);
        Assert.Equal(nodeC.Handle, result[2].Handle);
    }

    [Fact]
    public void TopologicalSort_DiamondDependency_OrderedCorrectly()
    {
        // d depends on b and c; b depends on a; c depends on a
        // Expected: a first, then b and c (alphabetical), then d
        ObjectHandle handleA = Handle("s", "a_view");
        ObjectHandle handleB = Handle("s", "b_view");
        ObjectHandle handleC = Handle("s", "c_view");

        DbObjectNode nodeA = Node("s", "a_view", ObjectType.View);
        DbObjectNode nodeB = NodeWithHardDeps("s", "b_view", ObjectType.View, handleA);
        DbObjectNode nodeC = NodeWithHardDeps("s", "c_view", ObjectType.View, handleA);
        DbObjectNode nodeD = NodeWithHardDeps("s", "d_view", ObjectType.View, handleB, handleC);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeD.Handle] = nodeD,
            [nodeC.Handle] = nodeC,
            [nodeB.Handle] = nodeB,
            [nodeA.Handle] = nodeA,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Equal(4, result.Count);
        Assert.Equal(nodeA.Handle, result[0].Handle);
        Assert.Equal(nodeB.Handle, result[1].Handle);
        Assert.Equal(nodeC.Handle, result[2].Handle);
        Assert.Equal(nodeD.Handle, result[3].Handle);
    }

    [Fact]
    public void TopologicalSort_CircularDependency_CyclicNodesAppendedAtEnd()
    {
        // a and b form a cycle; c is independent
        ObjectHandle handleA = Handle("s", "a_view");
        ObjectHandle handleB = Handle("s", "b_view");

        DbObjectNode nodeA = NodeWithHardDeps("s", "a_view", ObjectType.View, handleB);
        DbObjectNode nodeB = NodeWithHardDeps("s", "b_view", ObjectType.View, handleA);
        DbObjectNode nodeC = Node("s", "c_view", ObjectType.View);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeA.Handle] = nodeA,
            [nodeB.Handle] = nodeB,
            [nodeC.Handle] = nodeC,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        // c should be sorted normally first, then cyclic nodes are appended
        Assert.Equal(3, result.Count);
        Assert.Equal(nodeC.Handle, result[0].Handle);

        HashSet<ObjectHandle> cyclicHandles = [result[1].Handle, result[2].Handle];
        Assert.Contains(nodeA.Handle, cyclicHandles);
        Assert.Contains(nodeB.Handle, cyclicHandles);
    }

    [Fact]
    public void TopologicalSort_OnlySoftDependencies_NoOrderingEffect()
    {
        // b_view soft-depends on a_view — soft deps should not affect topological ordering
        ObjectHandle handleA = Handle("s", "a_view");

        DbObjectNode nodeA = Node("s", "a_view", ObjectType.View);
        DbObjectNode nodeB = NodeWithSoftDeps("s", "b_view", ObjectType.View, handleA);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeB.Handle] = nodeB,
            [nodeA.Handle] = nodeA,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        // Both have in-degree 0 (soft deps don't count), so alphabetical order
        Assert.Equal(2, result.Count);
        Assert.Equal(nodeA.Handle, result[0].Handle);
        Assert.Equal(nodeB.Handle, result[1].Handle);
    }

    [Fact]
    public void TopologicalSort_MixedHardAndSoftDependencies()
    {
        // c hard-depends on a, soft-depends on b
        // Only the hard dep should affect ordering
        ObjectHandle handleA = Handle("s", "a_func");
        ObjectHandle handleB = Handle("s", "b_func");

        DbObjectNode nodeA = Node("s", "a_func", ObjectType.Function);
        DbObjectNode nodeB = Node("s", "b_func", ObjectType.Function);

        List<DependencyNode> cDeps =
        [
            new() { Handle = handleA, ItemType = ItemType.Function, DependencyType = DependencyType.Hard },
            new() { Handle = handleB, ItemType = ItemType.Function, DependencyType = DependencyType.Soft },
        ];
        DbObjectNode nodeC = Node("s", "c_func", ObjectType.Function, cDeps, [handleA]);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeC.Handle] = nodeC,
            [nodeB.Handle] = nodeB,
            [nodeA.Handle] = nodeA,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Equal(3, result.Count);
        // a and b both have in-degree 0, so alphabetical. c must come after a.
        Assert.Equal(nodeA.Handle, result[0].Handle);
        Assert.Equal(nodeB.Handle, result[1].Handle);
        Assert.Equal(nodeC.Handle, result[2].Handle);
    }

    [Fact]
    public void TopologicalSort_MultipleSchemasOrderedByHandle()
    {
        // Objects across schemas, no dependencies — ordered by full handle (catalog.schema.name)
        DbObjectNode node1 = Node("alpha", "view1", ObjectType.View);
        DbObjectNode node2 = Node("beta", "view1", ObjectType.View);
        DbObjectNode node3 = Node("alpha", "view2", ObjectType.View);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [node2.Handle] = node2,
            [node3.Handle] = node3,
            [node1.Handle] = node1,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Equal(3, result.Count);
        // alpha.view1, alpha.view2, beta.view1
        Assert.Equal(node1.Handle, result[0].Handle);
        Assert.Equal(node3.Handle, result[1].Handle);
        Assert.Equal(node2.Handle, result[2].Handle);
    }

    [Fact]
    public void TopologicalSort_LargerCycle_AllCyclicNodesAppended()
    {
        // a→b→c→a forms a 3-node cycle; d is independent
        ObjectHandle handleA = Handle("s", "a_view");
        ObjectHandle handleB = Handle("s", "b_view");
        ObjectHandle handleC = Handle("s", "c_view");

        DbObjectNode nodeA = NodeWithHardDeps("s", "a_view", ObjectType.View, handleC);
        DbObjectNode nodeB = NodeWithHardDeps("s", "b_view", ObjectType.View, handleA);
        DbObjectNode nodeC = NodeWithHardDeps("s", "c_view", ObjectType.View, handleB);
        DbObjectNode nodeD = Node("s", "d_view", ObjectType.View);

        var nodeMap = new Dictionary<ObjectHandle, DbObjectNode>
        {
            [nodeA.Handle] = nodeA,
            [nodeB.Handle] = nodeB,
            [nodeC.Handle] = nodeC,
            [nodeD.Handle] = nodeD,
        };

        List<DbObjectNode> result = _resolver.TopologicalSort(nodeMap);

        Assert.Equal(4, result.Count);
        // d is non-cyclic, should come first
        Assert.Equal(nodeD.Handle, result[0].Handle);

        // Remaining 3 are the cycle, appended at the end
        HashSet<ObjectHandle> cyclicHandles = [result[1].Handle, result[2].Handle, result[3].Handle];
        Assert.Contains(nodeA.Handle, cyclicHandles);
        Assert.Contains(nodeB.Handle, cyclicHandles);
        Assert.Contains(nodeC.Handle, cyclicHandles);
    }

    #endregion

    #region ClassifyDependency

    [Theory]
    [InlineData(ObjectType.View, ItemType.View, DependencyType.Hard)]
    [InlineData(ObjectType.View, ItemType.Function, DependencyType.Hard)]
    [InlineData(ObjectType.View, ItemType.Table, DependencyType.Soft)]
    [InlineData(ObjectType.View, ItemType.Procedure, DependencyType.Soft)]
    [InlineData(ObjectType.View, ItemType.Unknown, DependencyType.Soft)]
    [InlineData(ObjectType.Function, ItemType.View, DependencyType.Hard)]
    [InlineData(ObjectType.Function, ItemType.Function, DependencyType.Hard)]
    [InlineData(ObjectType.Function, ItemType.Table, DependencyType.Soft)]
    [InlineData(ObjectType.Function, ItemType.Procedure, DependencyType.Soft)]
    [InlineData(ObjectType.Function, ItemType.Unknown, DependencyType.Soft)]
    [InlineData(ObjectType.Procedure, ItemType.View, DependencyType.Soft)]
    [InlineData(ObjectType.Procedure, ItemType.Function, DependencyType.Soft)]
    [InlineData(ObjectType.Procedure, ItemType.Table, DependencyType.Soft)]
    [InlineData(ObjectType.Procedure, ItemType.Procedure, DependencyType.Soft)]
    [InlineData(ObjectType.Table, ItemType.Table, DependencyType.Soft)]
    [InlineData(ObjectType.Table, ItemType.View, DependencyType.Soft)]
    [InlineData(ObjectType.Trigger, ItemType.Table, DependencyType.Soft)]
    [InlineData(ObjectType.Trigger, ItemType.Function, DependencyType.Soft)]
    [InlineData(ObjectType.Event, ItemType.Table, DependencyType.Soft)]
    [InlineData(ObjectType.Event, ItemType.Function, DependencyType.Soft)]
    public void ClassifyDependency_ReturnsExpectedType(
        ObjectType objectType, ItemType dependencyType, DependencyType expected)
    {
        DependencyType result = DependencyResolver.ClassifyDependency(objectType, dependencyType);

        Assert.Equal(expected, result);
    }

    #endregion
}
