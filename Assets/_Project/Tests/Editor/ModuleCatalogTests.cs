using System.Reflection;
using Hecton8.Building;
using Hecton8.Construction;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public class ModuleCatalogTests
    {
        private ModuleCatalog catalog;
        private BuildableData validData;
        private BuildableData invalidPrefabData;
        private GameObject mockPrefab;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<ModuleCatalog>();

            mockPrefab = new GameObject("MockPrefab");

            validData = ScriptableObject.CreateInstance<BuildableData>();
            validData.name = "ValidModule";
            typeof(BuildableData).GetField("finalPrefab", BindingFlags.Public | BindingFlags.Instance).SetValue(validData, mockPrefab);

            invalidPrefabData = ScriptableObject.CreateInstance<BuildableData>();
            invalidPrefabData.name = "InvalidPrefabModule";
            typeof(BuildableData).GetField("finalPrefab", BindingFlags.Public | BindingFlags.Instance).SetValue(invalidPrefabData, null);

            var allModulesList = new System.Collections.Generic.List<BuildableData> { validData, invalidPrefabData };
            typeof(ModuleCatalog).GetField("allModules", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(catalog, allModulesList);

            typeof(ModuleCatalog).GetMethod("RebuildLookup", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(catalog, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(validData);
            Object.DestroyImmediate(invalidPrefabData);
            Object.DestroyImmediate(mockPrefab);
        }

        [Test]
        public void FindPrefabById_WithValidId_ReturnsFinalPrefab()
        {
            var result = catalog.FindPrefabById("ValidModule");
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(mockPrefab));
        }

        [Test]
        public void FindPrefabById_WithInvalidId_ReturnsNull()
        {
            var result = catalog.FindPrefabById("NonExistentModule");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindPrefabById_WithNullId_ReturnsNull()
        {
            var result = catalog.FindPrefabById(null);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindPrefabById_WithValidIdButNullPrefab_ReturnsNull()
        {
            var result = catalog.FindPrefabById("InvalidPrefabModule");
            Assert.That(result, Is.Null);
        }
    }
}
