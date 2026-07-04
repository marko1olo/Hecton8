using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Power;
using Hecton8.Construction;
using Hecton8.Core;

namespace Hecton8.Tests
{
    public class PowerNodeOnSpawnTests
    {
        private GameObject _powerManagerObj;
        private GameObject _nodeObj;
        private PowerNode _powerNode;
        private ModuleMarker _marker;
        private BuildableData _data;

        [SetUp]
        public void Setup()
        {
            // First, cleanly reset the static state since tests might run after other tests that dirtied it
            var resetMethod = typeof(PowerGridManager).GetMethod("ResetStaticState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (resetMethod != null)
                resetMethod.Invoke(null, null);

            _powerManagerObj = new GameObject("PowerManager");
            var pgm = _powerManagerObj.AddComponent<PowerGridManager>();

            var awakeMethod = typeof(PowerGridManager).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awakeMethod != null)
                awakeMethod.Invoke(pgm, null);

            _data = ScriptableObject.CreateInstance<BuildableData>();
            _data.powerRating = -30f;
            _data.powerPriority = 20;

            _nodeObj = new GameObject("TestNode");
            _marker = _nodeObj.AddComponent<ModuleMarker>();
            _marker.Initialize(_data);

            _powerNode = _nodeObj.AddComponent<PowerNode>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_nodeObj != null)
                Object.DestroyImmediate(_nodeObj);

            if (_powerManagerObj != null)
                Object.DestroyImmediate(_powerManagerObj);

            if (_data != null)
                Object.DestroyImmediate(_data);

            var resetMethod = typeof(PowerGridManager).GetMethod("ResetStaticState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (resetMethod != null)
                resetMethod.Invoke(null, null);
        }

        [Test]
        public void OnSpawn_InitializesComponentsAndReadsBuildableData()
        {
            _powerNode.OnSpawn();

            Assert.IsNotNull(_powerNode.Components, "Components list should be initialized");
            Assert.IsTrue(_powerNode.Components.Contains(_powerNode), "PowerNode should include itself as an IPowerComponent");

            Assert.AreEqual(-30f, _powerNode.PowerRating, "PowerRating should be read from BuildableData");
            Assert.AreEqual(20, _powerNode.PowerPriority, "PowerPriority should be read from BuildableData");
        }

        [Test]
        public void OnSpawn_CreatesOrJoinsPowerGrid()
        {
            _powerNode.OnSpawn();

            Assert.IsNotNull(_powerNode.Grid, "PowerNode should be assigned a Grid upon OnSpawn");
            Assert.IsTrue(_powerNode.Grid.ContainsNode(_powerNode), "Grid should contain the newly spawned node");
        }

        [Test]
        public void OnSpawn_ConnectsToAuthoredNeighbors()
        {
            // Setup neighbor
            GameObject neighborObj = new GameObject("NeighborNode");
            PowerNode neighborNode = neighborObj.AddComponent<PowerNode>();

            // Set authored neighbors BEFORE spawning
            var authoredField = typeof(PowerNode).GetField("authoredNeighborNodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            authoredField.SetValue(_powerNode, new PowerNode[] { neighborNode });

            neighborNode.OnSpawn();
            _powerNode.OnSpawn();

            var neighborsField = typeof(PowerNode).GetField("_neighbors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nodeNeighbors = neighborsField.GetValue(_powerNode) as System.Collections.Generic.List<PowerNode>;

            Assert.IsNotNull(nodeNeighbors);
            Assert.IsTrue(nodeNeighbors.Contains(neighborNode), "PowerNode should connect to authored neighbor");

            Assert.AreEqual(neighborNode.Grid, _powerNode.Grid, "PowerNode should join the neighbor's grid");

            Object.DestroyImmediate(neighborObj);
        }

        [Test]
        public void OnSpawn_FallsBackToInspectorValuesIfNoModuleMarker()
        {
            Object.DestroyImmediate(_marker); // Remove the marker

            var fallbackRatingField = typeof(PowerNode).GetField("fallbackPowerRating", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fallbackRatingField.SetValue(_powerNode, 15f);

            var fallbackPriorityField = typeof(PowerNode).GetField("fallbackPowerPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fallbackPriorityField.SetValue(_powerNode, 75);

            _powerNode.OnSpawn();

            Assert.AreEqual(15f, _powerNode.PowerRating, "Should use fallback rating when ModuleMarker is absent");
            Assert.AreEqual(75, _powerNode.PowerPriority, "Should use fallback priority when ModuleMarker is absent");
        }
    }
}
