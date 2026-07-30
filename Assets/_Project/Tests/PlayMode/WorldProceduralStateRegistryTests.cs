using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.World;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.World
{
    [TestFixture]
    public class WorldProceduralStateRegistryTests
    {
        private GameObject _gameObject;
        private WorldProceduralStateRegistry _registry;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("WorldProceduralStateRegistryTest");
            _registry = _gameObject.AddComponent<WorldProceduralStateRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void BlockFaunaAnchor_ValidKey_MarksAsBlockedAndSetsLargeThreatZone()
        {
            long runtimeKey = 12345L;
            bool isLargeThreatZone = true;

            _registry.BlockFaunaAnchor(runtimeKey, isLargeThreatZone);

            var saveData = new SaveData();
            saveData.proceduralWorldState = new ProceduralWorldStateDTO();
            saveData.proceduralWorldState.EnsureCapacity();

            _registry.PopulateSaveData(saveData);

            ProceduralFaunaStateDTO targetState = default;
            bool found = false;

            for (int i = 0; i < saveData.proceduralWorldState.faunaStateCount; i++)
            {
                if (saveData.proceduralWorldState.faunaStates[i].runtimeKey == runtimeKey)
                {
                    targetState = saveData.proceduralWorldState.faunaStates[i];
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "State should be recorded");
            Assert.IsTrue((targetState.flags & ProceduralFaunaStateDTO.FlagBlocked) != 0, "State should be marked as blocked");
            Assert.IsTrue((targetState.flags & ProceduralFaunaStateDTO.FlagLargeThreatZone) != 0, "State should be marked as large threat zone");
        }

        [Test]
        public void BlockFaunaAnchor_ZeroKey_Ignored()
        {
            long runtimeKey = 0L;

            _registry.BlockFaunaAnchor(runtimeKey, true);

            var saveData = new SaveData();
            saveData.proceduralWorldState = new ProceduralWorldStateDTO();
            saveData.proceduralWorldState.EnsureCapacity();

            _registry.PopulateSaveData(saveData);

            Assert.AreEqual(0, saveData.proceduralWorldState.faunaStateCount, "No state should be recorded for zero key");
        }
    }
}
