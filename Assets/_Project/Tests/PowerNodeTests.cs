using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Building;

namespace Hecton8.Building.Tests
{
    public class PowerNodeTests
    {
        private GameObject go1;
        private GameObject go2;

        [TearDown]
        public void TearDown()
        {
            if (go1 != null) Object.DestroyImmediate(go1);
            if (go2 != null) Object.DestroyImmediate(go2);
        }

        [Test]
        public void ConnectAuthoredNeighbor_AddsToBothLists()
        {
            // Arrange
            go1 = new GameObject("Node1");
            go2 = new GameObject("Node2");
            var node1 = go1.AddComponent<PowerNode>();
            var node2 = go2.AddComponent<PowerNode>();

            // Act
            bool result = node1.ConnectAuthoredNeighbor(node2);

            // Assert
            Assert.IsTrue(result, "ConnectAuthoredNeighbor should return true when connecting new neighbors");

            var neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);
            var neighbors2 = (List<PowerNode>)typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node2);

            if (neighbors1 == null)
            {
                neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);
                neighbors2 = (List<PowerNode>)typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node2);
            }

            Assert.IsNotNull(neighbors1, "node1 neighbors should not be null");
            Assert.IsNotNull(neighbors2, "node2 neighbors should not be null");

            Assert.AreEqual(1, neighbors1.Count, "node1 should have exactly 1 neighbor");
            Assert.AreEqual(1, neighbors2.Count, "node2 should have exactly 1 neighbor");

            Assert.IsTrue(neighbors1.Contains(node2), "node1 neighbors should contain node2");
            Assert.IsTrue(neighbors2.Contains(node1), "node2 neighbors should contain node1");
        }

        [Test]
        public void ConnectAuthoredNeighbor_DuplicateConnection_ReturnsFalse()
        {
            // Arrange
            go1 = new GameObject("Node1");
            go2 = new GameObject("Node2");
            var node1 = go1.AddComponent<PowerNode>();
            var node2 = go2.AddComponent<PowerNode>();
            node1.ConnectAuthoredNeighbor(node2); // Connect once

            // Act
            bool result = node1.ConnectAuthoredNeighbor(node2); // Connect again

            // Assert
            Assert.IsFalse(result, "ConnectAuthoredNeighbor should return false for duplicate connection");

            var neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);
            var neighbors2 = (List<PowerNode>)typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node2);

            if (neighbors1 == null)
            {
                neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);
                neighbors2 = (List<PowerNode>)typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node2);
            }

            Assert.AreEqual(1, neighbors1.Count, "node1 should still have exactly 1 neighbor");
            Assert.AreEqual(1, neighbors2.Count, "node2 should still have exactly 1 neighbor");
        }

        [Test]
        public void ConnectAuthoredNeighbor_NullNeighbor_ReturnsFalse()
        {
            // Arrange
            go1 = new GameObject("Node1");
            var node1 = go1.AddComponent<PowerNode>();

            // Act
            bool result = node1.ConnectAuthoredNeighbor(null);

            // Assert
            Assert.IsFalse(result, "ConnectAuthoredNeighbor should return false when connecting null");

            var neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);

            if (neighbors1 == null && typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance) != null)
            {
                neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);
            }

            if (neighbors1 != null)
            {
                Assert.AreEqual(0, neighbors1.Count, "node1 neighbors count should be 0");
            }
            else
            {
                Assert.IsNull(neighbors1, "node1 neighbors should remain null");
            }
        }

        [Test]
        public void ConnectAuthoredNeighbor_SelfConnection_ReturnsFalse()
        {
            // Arrange
            go1 = new GameObject("Node1");
            var node1 = go1.AddComponent<PowerNode>();

            // Act
            bool result = node1.ConnectAuthoredNeighbor(node1);

            // Assert
            Assert.IsFalse(result, "ConnectAuthoredNeighbor should return false when connecting to self");

            var neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);

            if (neighbors1 == null && typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance) != null)
            {
                neighbors1 = (List<PowerNode>)typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(node1);
            }

            if (neighbors1 != null)
            {
                Assert.AreEqual(0, neighbors1.Count, "node1 neighbors count should be 0");
            }
            else
            {
                Assert.IsNull(neighbors1, "node1 neighbors should remain null");
            }
        }
    }
}
