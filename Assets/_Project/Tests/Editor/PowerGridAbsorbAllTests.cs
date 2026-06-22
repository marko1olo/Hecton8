using NUnit.Framework;
using Hecton8.Power;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

public sealed class PowerGridAbsorbAllTests
{
    private PowerGrid _gridA;
    private PowerGrid _gridB;

    [SetUp]
    public void SetUp()
    {
        _gridA = new PowerGrid(16, null);
        _gridB = new PowerGrid(16, null);
    }

    [TearDown]
    public void TearDown()
    {
        _gridA?.Dispose();
        _gridB?.Dispose();
    }

    private PowerNode CreateMockNode()
    {
        return new GameObject("MockNode").AddComponent<PowerNode>();
    }

    [Test]
    public void AbsorbAll_NullOrSelf_DoesNothing()
    {
        _gridA.AbsorbAll(null);
        Assert.AreEqual(0, _gridA.NodeCount);

        _gridA.AbsorbAll(_gridA);
        Assert.AreEqual(0, _gridA.NodeCount);
    }

    [Test]
    public void AbsorbAll_ValidGrid_TransfersNodesAndClearsOther()
    {
        var node1 = CreateMockNode();
        var node2 = CreateMockNode();

        _gridB.AddNode(node1);
        _gridB.AddNode(node2);

        Assert.AreEqual(0, _gridA.NodeCount);
        Assert.AreEqual(2, _gridB.NodeCount);

        typeof(PowerGrid).GetField("_isDirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_gridA, false);

        _gridA.AbsorbAll(_gridB);

        Assert.AreEqual(2, _gridA.NodeCount);
        Assert.AreEqual(0, _gridB.NodeCount);
        Assert.IsTrue(_gridA.IsDirty);
        Assert.AreSame(_gridA, node1.Grid);
        Assert.AreSame(_gridA, node2.Grid);

        var nodesA = (HashSet<PowerNode>)typeof(PowerGrid).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_gridA);
        Assert.IsTrue(nodesA.Contains(node1));
        Assert.IsTrue(nodesA.Contains(node2));

        var nodesB = (HashSet<PowerNode>)typeof(PowerGrid).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_gridB);
        Assert.AreEqual(0, nodesB.Count);

        var cacheB = (Dictionary<PowerNode, int>)typeof(PowerGrid).GetField("_overloadServiceCache", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_gridB);
        Assert.AreEqual(0, cacheB.Count);

        Object.DestroyImmediate(node1.gameObject);
        Object.DestroyImmediate(node2.gameObject);
    }

    [Test]
    public void AbsorbAll_HandlesNullNodesInOtherGrid()
    {
        var node1 = CreateMockNode();

        _gridB.AddNode(node1);
        var nodesBField = typeof(PowerGrid).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        var nodesB = (HashSet<PowerNode>)nodesBField.GetValue(_gridB);
        nodesB.Add(null); // Explicitly adding a null to simulate corrupted state

        Assert.AreEqual(0, _gridA.NodeCount);

        _gridA.AbsorbAll(_gridB);

        Assert.AreEqual(1, _gridA.NodeCount);
        Assert.AreEqual(0, _gridB.NodeCount);

        var nodesA = (HashSet<PowerNode>)typeof(PowerGrid).GetField("_nodes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_gridA);
        Assert.IsTrue(nodesA.Contains(node1));
        Assert.IsFalse(nodesA.Contains(null));

        Object.DestroyImmediate(node1.gameObject);
    }
}
