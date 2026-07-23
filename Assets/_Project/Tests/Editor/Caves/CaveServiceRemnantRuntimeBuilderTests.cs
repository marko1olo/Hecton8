#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using UnityEngine;
using Hecton8.Caves;
using System.Reflection;

namespace Hecton8.Tests.Editor.Caves
{
    [TestFixture]
    public sealed class CaveServiceRemnantRuntimeBuilderTests
    {
        [Test]
        public void Prewarm_WithNullParent_ReturnsNull()
        {
            // Arrange
            Transform parent = null;

            // Act
            Transform result = CaveServiceRemnantRuntimeBuilder.Prewarm(parent);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Prewarm_WithValidParent_CreatesRemnantRootAndDisablesIt()
        {
            // Arrange
            GameObject parentObj = new GameObject("Parent");

            // Act
            Transform result = CaveServiceRemnantRuntimeBuilder.Prewarm(parentObj.transform);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.name, Is.EqualTo("_ServiceRemnants"));
            Assert.That(result.gameObject.activeSelf, Is.False);
            Assert.That(result.parent, Is.EqualTo(parentObj.transform));

            // Cleanup
            UnityEngine.Object.DestroyImmediate(parentObj);
        }

        [Test]
        public void Prewarm_PopulatesRootWithDisabledChildren()
        {
            // Arrange
            GameObject parentObj = new GameObject("Parent");

            // Act
            Transform result = CaveServiceRemnantRuntimeBuilder.Prewarm(parentObj.transform);

            // Assert
            Assert.That(result.childCount, Is.EqualTo(CaveServiceRemnantRuntimeBuilder.RuntimeCapacity));

            for (int i = 0; i < result.childCount; i++)
            {
                Transform child = result.GetChild(i);
                Assert.That(child.gameObject.activeSelf, Is.False, $"Child at index {i} should be disabled.");
                Assert.That(child.name, Is.EqualTo($"Remnant_{i}"));
            }

            // Cleanup
            UnityEngine.Object.DestroyImmediate(parentObj);
        }

        [Test]
        public void Prewarm_WithArrays_PopulatesCaches()
        {
            // Arrange
            GameObject parentObj = new GameObject("Parent");
            GameObject[] primitiveObjects = new GameObject[CaveServiceRemnantRuntimeBuilder.RuntimeCapacity];
            MeshFilter[] primitiveFilters = new MeshFilter[CaveServiceRemnantRuntimeBuilder.RuntimeCapacity];
            MeshRenderer[] primitiveRenderers = new MeshRenderer[CaveServiceRemnantRuntimeBuilder.RuntimeCapacity];

            // Act
            Transform result = CaveServiceRemnantRuntimeBuilder.Prewarm(
                parentObj.transform,
                primitiveObjects,
                primitiveFilters,
                primitiveRenderers);

            // Assert
            Assert.That(result, Is.Not.Null);
            for (int i = 0; i < CaveServiceRemnantRuntimeBuilder.RuntimeCapacity; i++)
            {
                Assert.That(primitiveObjects[i], Is.Not.Null, $"Primitive object at index {i} should be cached.");
                Assert.That(primitiveFilters[i], Is.Not.Null, $"MeshFilter at index {i} should be cached.");
                Assert.That(primitiveRenderers[i], Is.Not.Null, $"MeshRenderer at index {i} should be cached.");
            }

            // Cleanup
            UnityEngine.Object.DestroyImmediate(parentObj);
        }

        [Test]
        public void Prewarm_CalledTwice_ReusesExistingRootAndChildren()
        {
            // Arrange
            GameObject parentObj = new GameObject("Parent");
            Transform firstResult = CaveServiceRemnantRuntimeBuilder.Prewarm(parentObj.transform);

            // Activate the root and a child to ensure they get deactivated
            firstResult.gameObject.SetActive(true);
            firstResult.GetChild(0).gameObject.SetActive(true);

            // Act
            Transform secondResult = CaveServiceRemnantRuntimeBuilder.Prewarm(parentObj.transform);

            // Assert
            Assert.That(secondResult, Is.EqualTo(firstResult), "Should reuse the same root transform.");
            Assert.That(secondResult.gameObject.activeSelf, Is.False, "Root should be disabled again.");
            Assert.That(secondResult.GetChild(0).gameObject.activeSelf, Is.False, "Previously active child should be disabled.");
            Assert.That(secondResult.childCount, Is.EqualTo(CaveServiceRemnantRuntimeBuilder.RuntimeCapacity), "Child count should not exceed capacity.");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(parentObj);
        }

        [Test]
        public void Prewarm_WithParentNullArrays_ReturnsRootTransform()
        {
            // Arrange
            GameObject parentObj = new GameObject("Parent");

            // Act
            Transform result = CaveServiceRemnantRuntimeBuilder.Prewarm(
                parentObj.transform,
                null,
                null,
                null);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.name, Is.EqualTo("_ServiceRemnants"));
            Assert.That(result.parent, Is.EqualTo(parentObj.transform));

            // Cleanup
            UnityEngine.Object.DestroyImmediate(parentObj);
        }
    }
}
#endif
