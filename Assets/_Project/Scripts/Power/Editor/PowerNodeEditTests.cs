#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Power; // Assuming PowerNode is in this namespace based on the source code snippet we saw before, wait no, let's keep it simple or just not use specific namespaces for components on same assembly if possible, but let's just use what we had.

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
    }
}
#endif
