using NUnit.Framework;
using Hecton8.Power;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Hecton8.Building;
using Hecton8.Core;

public sealed class PowerGridRemoveNodeTests
{
    private PowerGrid _grid;
    private GameObject _nodeObject;
    private PowerNode _node;

    [SetUp]
    public void SetUp()
    {
        _grid = new PowerGrid(16, null);
        _nodeObject = new GameObject("MockNode");
        _node = _nodeObject.AddComponent<PowerNode>();
    }

    [TearDown]
    public void TearDown()
    {
        _grid?.Dispose();
        if (_nodeObject != null)
        {
            Object.DestroyImmediate(_nodeObject);
        }
    }

    [Test]
    public void RemoveNode_NullNode_DoesNothing()
    {
        _grid.RemoveNode(null);
        Assert.AreEqual(0, _grid.NodeCount);
    }

    [Test]
    public void RemoveNode_ValidNode_RemovesFromNodesSet()
    {
        _grid.AddNode(_node);
        Assert.AreEqual(1, _grid.NodeCount);

        _grid.RemoveNode(_node);

        Assert.AreEqual(0, _grid.NodeCount);

        var nodes = (HashSet<PowerNode>)typeof(PowerGrid).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_grid);
        Assert.IsFalse(nodes.Contains(_node));
    }

    [Test]
    public void RemoveNode_ValidNode_ClearsGridReferenceIfOwned()
    {
        _grid.AddNode(_node);
        Assert.AreSame(_grid, _node.Grid);

        _grid.RemoveNode(_node);

        Assert.IsNull(_node.Grid);
    }

    [Test]
    public void RemoveNode_ValidNode_DoesNotClearGridReferenceIfNotOwned()
    {
        PowerGrid otherGrid = new PowerGrid(16, null);

        _grid.AddNode(_node);
        _node.SetGrid(otherGrid); // Force set grid to something else

        _grid.RemoveNode(_node);

        Assert.AreSame(otherGrid, _node.Grid); // Should not clear
        otherGrid.Dispose();
    }

    [Test]
    public void RemoveNode_ValidNode_SetsDirtyFlag()
    {
        _grid.AddNode(_node);
        typeof(PowerGrid).GetField("_isDirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_grid, false);

        _grid.RemoveNode(_node);

        Assert.IsTrue(_grid.IsDirty);
    }

    [Test]
    public void RemoveNode_ValidNode_RemovesOverloadServiceCache()
    {
        // Add BaseModule to node to populate overload service cache
        // We use a mock class to act as the base module
        var baseModule = _nodeObject.AddComponent<MockBaseModule>();
        _grid.AddNode(_node);

        // Force populate cache using reflection since we can't easily trigger the full flow
        var overloadCache = (Dictionary<BaseModule, PowerGrid.CachedOverloadServices>)typeof(PowerGrid).GetField("_overloadServiceCache", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_grid);

        overloadCache[baseModule] = new PowerGrid.CachedOverloadServices();
        Assert.AreEqual(1, overloadCache.Count);

        _grid.RemoveNode(_node);

        Assert.AreEqual(0, overloadCache.Count);
    }

    private class MockBaseModule : BaseModule
    {
        public override string ModuleName => "MockBaseModule";
    }
}
