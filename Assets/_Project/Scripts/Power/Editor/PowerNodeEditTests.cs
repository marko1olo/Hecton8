#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Hecton8.Power;

namespace Hecton8.Tests.Editor
{
    public class PowerNodeEditTests
    {
        [Test]
        public void DisconnectFromGrid_RemovesNodeAndChecksSplit()
        {
            // Create a parent GameObject to hold our test objects
            GameObject root = new GameObject("TestRoot");

            // Create THREE PowerNodes to ensure NodeCount > 1 when one disconnects
            GameObject node1Obj = new GameObject("Node1");
            node1Obj.transform.SetParent(root.transform);
            var node1 = node1Obj.AddComponent<Hecton8.Power.PowerNode>();

            GameObject node2Obj = new GameObject("Node2");
            node2Obj.transform.SetParent(root.transform);
            var node2 = node2Obj.AddComponent<Hecton8.Power.PowerNode>();

            GameObject node3Obj = new GameObject("Node3");
            node3Obj.transform.SetParent(root.transform);
            var node3 = node3Obj.AddComponent<Hecton8.Power.PowerNode>();

            // Simulate connections: 1-2, 2-3
            node1.ConnectAuthoredNeighbor(node2);
            node2.ConnectAuthoredNeighbor(node3);

            // Use reflection to access the internal state
            FieldInfo gridField = typeof(Hecton8.Power.PowerNode).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            object grid1 = gridField.GetValue(node1);
            object grid2 = gridField.GetValue(node2);
            object grid3 = gridField.GetValue(node3);

            Assert.IsNotNull(grid1, "Grid1 should not be null after connection.");
            Assert.IsNotNull(grid2, "Grid2 should not be null after connection.");
            Assert.IsNotNull(grid3, "Grid3 should not be null after connection.");
            Assert.AreEqual(grid1, grid2, "Nodes should be in the same grid after connection.");
            Assert.AreEqual(grid2, grid3, "Nodes should be in the same grid after connection.");

            PropertyInfo nodeCountProp = grid1.GetType().GetProperty("NodeCount");
            int initialCount = (int)nodeCountProp.GetValue(grid1);

            // Verify initial node count is 3
            Assert.AreEqual(3, initialCount, "Grid should have exactly 3 nodes.");

            // Invoke DisconnectFromGrid on node1 via reflection
            MethodInfo disconnectMethod = typeof(Hecton8.Power.PowerNode).GetMethod("DisconnectFromGrid", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(disconnectMethod, "DisconnectFromGrid method not found.");
            disconnectMethod.Invoke(node1, null);

            // After disconnection, node1 should have no grid
            object disconnectedGrid = gridField.GetValue(node1);
            Assert.IsNull(disconnectedGrid, "Node1 should have null grid after disconnection.");

            // The original grid should now have 2 nodes (node2, node3)
            int finalCount = (int)nodeCountProp.GetValue(grid2);
            Assert.AreEqual(2, finalCount, "Grid should have 2 nodes after disconnection, triggering the CheckAndSplitGrid branch.");

            // Cleanup
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ConnectAuthoredNeighbor_EstablishesConnectionsAndHandlesGrids()
        {
            GameObject root = new GameObject("TestRoot");
            var node1 = new GameObject("Node1").AddComponent<Hecton8.Power.PowerNode>();
            var node2 = new GameObject("Node2").AddComponent<Hecton8.Power.PowerNode>();
            node1.transform.SetParent(root.transform);
            node2.transform.SetParent(root.transform);

            FieldInfo gridField = typeof(Hecton8.Power.PowerNode).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo neighborsField = typeof(Hecton8.Power.PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo revisionField = typeof(Hecton8.Power.PowerNode).GetField("_topologyRevision", BindingFlags.NonPublic | BindingFlags.Instance);

            int initialRevision1 = (int)revisionField.GetValue(node1);
            int initialRevision2 = (int)revisionField.GetValue(node2);

            // Test null and self
            Assert.IsFalse(node1.ConnectAuthoredNeighbor(null), "Should return false for null neighbor");
            Assert.IsFalse(node1.ConnectAuthoredNeighbor(node1), "Should return false for self connection");

            // Test connection
            bool result = node1.ConnectAuthoredNeighbor(node2);

            Assert.IsTrue(result, "Should return true for new connection");

            var neighbors1 = (List<Hecton8.Power.PowerNode>)neighborsField.GetValue(node1);
            var neighbors2 = (List<Hecton8.Power.PowerNode>)neighborsField.GetValue(node2);

            Assert.IsTrue(neighbors1.Contains(node2), "Node1 should contain Node2 in neighbors");
            Assert.IsTrue(neighbors2.Contains(node1), "Node2 should contain Node1 in neighbors");

            int newRevision1 = (int)revisionField.GetValue(node1);
            int newRevision2 = (int)revisionField.GetValue(node2);

            Assert.Greater(newRevision1, initialRevision1, "Node1 topology revision should increase");
            Assert.Greater(newRevision2, initialRevision2, "Node2 topology revision should increase");

            object grid1 = gridField.GetValue(node1);
            object grid2 = gridField.GetValue(node2);

            Assert.IsNotNull(grid1, "Grid should be created for Node1");
            Assert.IsNotNull(grid2, "Grid should be assigned to Node2");
            Assert.AreEqual(grid1, grid2, "Nodes should share the same grid");

            // Test duplicate connection
            bool duplicateResult = node1.ConnectAuthoredNeighbor(node2);
            Assert.IsFalse(duplicateResult, "Should return false for duplicate connection");

            Object.DestroyImmediate(root);
        }

        [Test]
        public void SetGrid_AssignsGridCorrectly()
        {
            var node = new GameObject("TestNode").AddComponent<Hecton8.Power.PowerNode>();
            var grid = new Hecton8.Power.PowerGrid();

            node.SetGrid(grid);
            Assert.AreEqual(grid, node.Grid, "SetGrid should assign the provided grid.");

            node.SetGrid(null);
            Assert.IsNull(node.Grid, "SetGrid should set the grid to null when null is provided.");

            Object.DestroyImmediate(node.gameObject);
        }

        [Test]
        public void OnSpawn_InitializesOrClearsComponentsAndNeighbors()
        {
            // Setup
            GameObject root = new GameObject("TestRoot");
            var node = new GameObject("Node1").AddComponent<Hecton8.Power.PowerNode>();
            node.transform.SetParent(root.transform);

            FieldInfo componentsField = typeof(Hecton8.Power.PowerNode).GetField("_components", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo neighborsField = typeof(Hecton8.Power.PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance);

            // 1. Edge Case: Lists are initially null
            componentsField.SetValue(node, null);
            neighborsField.SetValue(node, null);

            node.OnSpawn();

            var components = (List<IPowerComponent>)componentsField.GetValue(node);
            var neighbors = (List<Hecton8.Power.PowerNode>)neighborsField.GetValue(node);

            Assert.IsNotNull(components, "Components list should be initialized from null.");
            Assert.IsNotNull(neighbors, "Neighbors list should be initialized from null.");
            Assert.AreEqual(1, components.Count, "Components should contain the PowerNode itself.");
            Assert.AreEqual(0, neighbors.Count, "Neighbors should be empty if no authored neighbors exist.");

            // 2. Edge Case: Lists are already initialized but contain old data
            var oldComponents = new List<IPowerComponent>();
            var oldNeighbors = new List<Hecton8.Power.PowerNode>();
            oldNeighbors.Add(node); // Dummy data

            componentsField.SetValue(node, oldComponents);
            neighborsField.SetValue(node, oldNeighbors);

            node.OnSpawn();

            var newComponents = (List<IPowerComponent>)componentsField.GetValue(node);
            var newNeighbors = (List<Hecton8.Power.PowerNode>)neighborsField.GetValue(node);

            Assert.AreSame(oldComponents, newComponents, "Components list should be reused.");
            Assert.AreSame(oldNeighbors, newNeighbors, "Neighbors list should be reused.");
            Assert.AreEqual(1, newComponents.Count, "Components should contain only the PowerNode itself.");
            Assert.AreEqual(0, newNeighbors.Count, "Neighbors should be cleared and empty.");

            // Teardown
            Object.DestroyImmediate(root);
        }
}
}
#endif
