using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Hecton8.AI;

namespace Hecton8.Tests
{
    [TestFixture]
    public class FaunaBiomeDataTests
    {
        private FaunaBiomeData _biomeData;
        private GameObject _dummyPrefab1;
        private GameObject _dummyPrefab2;
        private CreatureArchetypeData _archetype1;
        private CreatureArchetypeData _archetype2;

        [SetUp]
        public void SetUp()
        {
            _biomeData = ScriptableObject.CreateInstance<FaunaBiomeData>();
            _biomeData.possibleCreatures = new List<FaunaEntry>();
            _dummyPrefab1 = new GameObject("DummyPrefab1");
            _dummyPrefab2 = new GameObject("DummyPrefab2");

            _archetype1 = ScriptableObject.CreateInstance<CreatureArchetypeData>();
            _archetype1.prefab = _dummyPrefab1;
            _archetype1.spawnWeight = 10f;
            _archetype1.maxAlivePerBiome = 5;

            _archetype2 = ScriptableObject.CreateInstance<CreatureArchetypeData>();
            _archetype2.prefab = _dummyPrefab2;
            _archetype2.spawnWeight = 20f;
            _archetype2.maxAlivePerBiome = 10;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_biomeData);
            Object.DestroyImmediate(_dummyPrefab1);
            Object.DestroyImmediate(_dummyPrefab2);
            Object.DestroyImmediate(_archetype1);
            Object.DestroyImmediate(_archetype2);
        }

        [Test]
        public void TrySelectCreature_EmptyList_ReturnsFalse()
        {
            bool result = _biomeData.TrySelectCreature(null, out var entry);
            Assert.IsFalse(result);
        }

        [Test]
        public void TrySelectCreature_OneItem_ReturnsItem()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab1, spawnWeight = 10f, maxAlive = 5 });

            bool result = _biomeData.TrySelectCreature(null, out var entry);

            Assert.IsTrue(result);
            Assert.AreEqual(_dummyPrefab1, entry.prefab);
        }

        [Test]
        public void TrySelectCreature_MaxAliveReached_ReturnsFalse()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab1, spawnWeight = 10f, maxAlive = 2 });
            int[] currentCounts = new[] { 2 }; // Reached max alive

            bool result = _biomeData.TrySelectCreature(currentCounts, out var entry);

            Assert.IsFalse(result);
        }

        [Test]
        public void TrySelectCreature_FirstItemMaxAliveReached_ReturnsSecondItem()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab1, spawnWeight = 10f, maxAlive = 2 });
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab2, spawnWeight = 10f, maxAlive = 5 });
            int[] currentCounts = new[] { 2, 0 }; // First item reached max alive

            bool result = _biomeData.TrySelectCreature(currentCounts, out var entry);

            Assert.IsTrue(result);
            Assert.AreEqual(_dummyPrefab2, entry.prefab);
        }

        [Test]
        public void TrySelectCreature_WithArchetype_UsesArchetypeData()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { archetype = _archetype1, spawnWeight = 1f, maxAlive = 1 });

            bool result = _biomeData.TrySelectCreature(null, out var entry);

            Assert.IsTrue(result);
            Assert.AreEqual(_archetype1, entry.archetype);
        }

        [Test]
        public void TrySelectCreature_DeterministicWithSeed()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab1, spawnWeight = 10f, maxAlive = 5 });
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab2, spawnWeight = 10f, maxAlive = 5 });

            int[] currentCounts = new[] { 0, 0 };

            // Multiple calls with the same input should yield the same result since it recreates the random each time
            _biomeData.TrySelectCreature(currentCounts, out var entry1);
            _biomeData.TrySelectCreature(currentCounts, out var entry2);

            Assert.AreEqual(entry1.prefab, entry2.prefab);
        }

        [Test]
        public void TrySelectCreature_RandomRef_DifferentResults()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab1, spawnWeight = 10f, maxAlive = 5 });
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab2, spawnWeight = 10f, maxAlive = 5 });

            Unity.Mathematics.Random random = new Unity.Mathematics.Random(12345);

            Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>
            {
                { _dummyPrefab1, 0 },
                { _dummyPrefab2, 0 }
            };

            for (int i = 0; i < 100; i++)
            {
                bool result = _biomeData.TrySelectCreature(ref random, null, out var entry);
                Assert.IsTrue(result);
                counts[entry.prefab]++;
            }

            Assert.Greater(counts[_dummyPrefab1], 0);
            Assert.Greater(counts[_dummyPrefab2], 0);
        }

        [Test]
        public void TrySelectCreature_MissingPrefab_Ignored()
        {
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = null, spawnWeight = 10f, maxAlive = 5 });
            _biomeData.possibleCreatures.Add(new FaunaEntry { prefab = _dummyPrefab1, spawnWeight = 10f, maxAlive = 5 });

            bool result = _biomeData.TrySelectCreature(null, out var entry);

            Assert.IsTrue(result);
            Assert.AreEqual(_dummyPrefab1, entry.prefab);
        }
    }
}
