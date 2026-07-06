#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Power;

namespace Hecton8.Tests.Editor
{
    public class PowerNodeDespawnTests
    {
        [Test]
        public void OnDespawn_ResetsFieldsAndDisconnects()
        {
            // Setup
            GameObject root = new GameObject("TestRoot");
            var node1 = new GameObject("Node1").AddComponent<Hecton8.Power.PowerNode>();
            var node2 = new GameObject("Node2").AddComponent<Hecton8.Power.PowerNode>();
            node1.transform.SetParent(root.transform);
            node2.transform.SetParent(root.transform);

            // Connect nodes so they have a grid
            node1.ConnectAuthoredNeighbor(node2);

            // Use reflection to explicitly set private fields to non-default values and check pre-conditions
            FieldInfo hasPowerField = typeof(Hecton8.Power.PowerNode).GetField("_hasPower", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo componentsField = typeof(Hecton8.Power.PowerNode).GetField("_components", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo neighborsField = typeof(Hecton8.Power.PowerNode).GetField("_neighbors", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo gridField = typeof(Hecton8.Power.PowerNode).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);

            hasPowerField.SetValue(node1, false);

            var components = (List<IPowerComponent>)componentsField.GetValue(node1);
            if (components.Count == 0) components.Add(node1);
            Assert.IsNotEmpty(components, "Components list should not be empty before OnDespawn");

            var neighbors = (List<Hecton8.Power.PowerNode>)neighborsField.GetValue(node1);
            if (neighbors.Count == 0) neighbors.Add(node2);
            Assert.IsNotEmpty(neighbors, "Neighbors list should not be empty before OnDespawn");

            var grid = gridField.GetValue(node1);
            Assert.IsNotNull(grid, "Grid should not be null before OnDespawn");

            // Execute
            node1.OnDespawn();

            // Verify internal state
            Assert.AreEqual(0, components.Count, "_components should be cleared");
            Assert.AreEqual(0, neighbors.Count, "_neighbors should be cleared");
            Assert.IsNull(gridField.GetValue(node1), "_grid should be null");
            Assert.AreEqual(true, (bool)hasPowerField.GetValue(node1), "_hasPower should be true");

            // Verify external state (node2 should not have node1 as a neighbor)
            var neighbors2 = (List<Hecton8.Power.PowerNode>)neighborsField.GetValue(node2);
            Assert.IsFalse(neighbors2.Contains(node1), "Node2 should not contain Node1 in neighbors after OnDespawn");

            // Teardown
            Object.DestroyImmediate(root);
        }
    }
}
#endif
