using NUnit.Framework;
using Hecton8.Power;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

public sealed class PowerGridAddNodeTests
{
    private PowerGrid _grid;

    [SetUp]
    public void SetUp()
    {
        _grid = new PowerGrid(16, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_grid != null)
        {
            _grid.Dispose();
        }
    }

    private PowerNode CreateMockNode()
    {
        return new GameObject("MockNode").AddComponent<PowerNode>();
    }

    [Test]
    public void AddNode_NullNode_ReturnsEarly()
    {
        var nodeCount = _grid.NodeCount;
        _grid.AddNode(null);
        Assert.That(_grid.NodeCount, Is.EqualTo(nodeCount));
    }

    [Test]
    public void AddNode_ValidNode_AddsToGridAndSetsDirty()
    {
        var node = CreateMockNode();

        Assert.That(_grid.IsDirty, Is.False);

        _grid.AddNode(node);

        Assert.That(_grid.NodeCount, Is.EqualTo(1));
        Assert.That(_grid.Nodes.Contains(node), Is.True);
        Assert.That(node.Grid, Is.EqualTo(_grid));
        Assert.That(_grid.IsDirty, Is.True);

        Object.DestroyImmediate(node.gameObject);
    }

    [Test]
    public void AddNode_DuplicateNode_DoesNotAddAgain()
    {
        var node = CreateMockNode();
        _grid.AddNode(node);

        // Reset dirty flag using reflection
        var dirtyField = typeof(PowerGrid).GetField("_isDirty", BindingFlags.NonPublic | BindingFlags.Instance);
        dirtyField.SetValue(_grid, false);

        _grid.AddNode(node);

        Assert.That(_grid.NodeCount, Is.EqualTo(1));
        Assert.That(_grid.IsDirty, Is.False);

        Object.DestroyImmediate(node.gameObject);
    }
}
