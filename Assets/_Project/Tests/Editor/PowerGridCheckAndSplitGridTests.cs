using NUnit.Framework;
using Hecton8.Power;
using System.Reflection;
using UnityEngine;

public sealed class PowerGridCheckAndSplitGridTests
{
    private PowerNode CreateMockNode()
    {
        return new GameObject("MockNode").AddComponent<PowerNode>();
    }

    [Test]
    public void CheckAndSplitGrid_NullGrid_DoesNothing()
    {
        // Should not throw
        PowerGridManager.CheckAndSplitGrid(null);
    }

    [Test]
    public void CheckAndSplitGrid_GridWithOneOrZeroNodes_DoesNothing()
    {
        PowerGrid grid = new PowerGrid(16, null);

        // 0 nodes
        PowerGridManager.CheckAndSplitGrid(grid);
        var splitCheckPendingField = typeof(PowerGrid).GetField("_splitCheckPending", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsFalse((bool)splitCheckPendingField.GetValue(grid));

        // 1 node
        PowerNode node = CreateMockNode();
        grid.AddNode(node);
        PowerGridManager.CheckAndSplitGrid(grid);
        Assert.IsFalse((bool)splitCheckPendingField.GetValue(grid));

        grid.Dispose();
        Object.DestroyImmediate(node.gameObject);
    }

    [Test]
    public void CheckAndSplitGrid_GridWithMultipleNodes_RequestsSplitCheck()
    {
        PowerGrid grid = new PowerGrid(16, null);
        PowerNode node1 = CreateMockNode();
        PowerNode node2 = CreateMockNode();

        grid.AddNode(node1);
        grid.AddNode(node2);

        var splitCheckPendingField = typeof(PowerGrid).GetField("_splitCheckPending", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsFalse((bool)splitCheckPendingField.GetValue(grid));

        PowerGridManager.CheckAndSplitGrid(grid);

        Assert.IsTrue((bool)splitCheckPendingField.GetValue(grid));

        grid.Dispose();
        Object.DestroyImmediate(node1.gameObject);
        Object.DestroyImmediate(node2.gameObject);
    }
}
