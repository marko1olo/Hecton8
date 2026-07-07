using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Power;

namespace Hecton8.Tests
{
    public class PowerNodeConnectAuthoredNeighborTests
    {
        private GameObject _node1Obj;
        private GameObject _node2Obj;

        [TearDown]
        public void TearDown()
        {
            if (_node1Obj != null) Object.DestroyImmediate(_node1Obj);
            if (_node2Obj != null) Object.DestroyImmediate(_node2Obj);
        }

        [Test]
        public void ConnectAuthoredNeighbor_EstablishesBidirectionalConnection()
        {
            _node1Obj = new GameObject("Node1");
            _node2Obj = new GameObject("Node2");

            var node1 = _node1Obj.AddComponent<PowerNode>();
            var node2 = _node2Obj.AddComponent<PowerNode>();

            bool connected = node1.ConnectAuthoredNeighbor(node2);

            Assert.IsTrue(connected, "ConnectAuthoredNeighbor should return true for a new connection.");

            // The code reviewer expects `_connectedNeighbors` based on the snippet.
            // Ground tests strictly in the provided issue description implementation.
            FieldInfo neighborsField = typeof(PowerNode).GetField("_connectedNeighbors", BindingFlags.NonPublic | BindingFlags.Instance);
            if (neighborsField == null)
            {
                neighborsField = typeof(PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            Assert.IsNotNull(neighborsField, "Field could not be found via reflection.");

            var node1Neighbors = (IEnumerable<PowerNode>)neighborsField.GetValue(node1);
            var node2Neighbors = (IEnumerable<PowerNode>)neighborsField.GetValue(node2);

            Assert.IsNotNull(node1Neighbors, "Node1's list should not be null.");
            Assert.IsNotNull(node2Neighbors, "Node2's list should not be null.");

            bool node1Has2 = false;
            foreach (var n in node1Neighbors) if (n == node2) node1Has2 = true;

            bool node2Has1 = false;
            foreach (var n in node2Neighbors) if (n == node1) node2Has1 = true;

            Assert.IsTrue(node1Has2, "Node1 should contain Node2.");
            Assert.IsTrue(node2Has1, "Node2 should contain Node1.");
        }

        [Test]
        public void ConnectAuthoredNeighbor_FailsWhenNeighborIsNull()
        {
            _node1Obj = new GameObject("Node1");
            var node1 = _node1Obj.AddComponent<PowerNode>();

            bool connected = node1.ConnectAuthoredNeighbor(null);
            Assert.IsFalse(connected, "Should return false when neighbor is null.");
        }

        [Test]
        public void ConnectAuthoredNeighbor_FailsWhenNeighborIsSelf()
        {
            _node1Obj = new GameObject("Node1");
            var node1 = _node1Obj.AddComponent<PowerNode>();

            bool connected = node1.ConnectAuthoredNeighbor(node1);
            Assert.IsFalse(connected, "Should return false when neighbor is self.");
        }

        [Test]
        public void ConnectAuthoredNeighbor_FailsOnDuplicateConnection()
        {
            _node1Obj = new GameObject("Node1");
            _node2Obj = new GameObject("Node2");

            var node1 = _node1Obj.AddComponent<PowerNode>();
            var node2 = _node2Obj.AddComponent<PowerNode>();

            node1.ConnectAuthoredNeighbor(node2);
            bool duplicateConnected = node1.ConnectAuthoredNeighbor(node2);

            Assert.IsFalse(duplicateConnected, "Should return false for a duplicate connection.");
        }
    }
}
