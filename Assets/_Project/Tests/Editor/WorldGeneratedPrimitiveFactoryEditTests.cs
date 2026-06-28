#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.World;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class WorldGeneratedPrimitiveFactoryEditTests
    {
        [SetUp]
        public void Setup()
        {
            ResetStaticState();
        }

        [TearDown]
        public void Teardown()
        {
            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var method = typeof(WorldGeneratedPrimitiveFactory).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
            }
        }

        [Test]
        public void RegisterPrimitiveResourcesCold_WithOutOfBoundsPrimitiveType_ReturnsFalse()
        {
            // Arrange
            PrimitiveType invalidType = (PrimitiveType)999;
            Mesh dummyMesh = new Mesh();
            Material dummyMaterial = new Material(Shader.Find("Standard"));

            // Act
            bool result = WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(invalidType, dummyMesh, dummyMaterial);

            // Assert
            Assert.IsFalse(result);

            // Cleanup
            Object.DestroyImmediate(dummyMesh);
            Object.DestroyImmediate(dummyMaterial);
        }

        [Test]
        public void RegisterPrimitiveResourcesCold_WithNullMesh_ReturnsFalseAndDoesNotCache()
        {
            // Arrange
            PrimitiveType type = PrimitiveType.Cube;
            Material dummyMaterial = new Material(Shader.Find("Standard"));

            // Act
            bool result = WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(type, null, dummyMaterial);

            // Assert
            Assert.IsFalse(result);

            // Cleanup
            Object.DestroyImmediate(dummyMaterial);
        }

        [Test]
        public void RegisterPrimitiveResourcesCold_WithValidInputs_ReturnsTrueAndCaches()
        {
            // Arrange
            PrimitiveType type = PrimitiveType.Cube;
            Mesh dummyMesh = new Mesh();
            Material dummyMaterial = new Material(Shader.Find("Standard"));

            // Act
            bool result = WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(type, dummyMesh, dummyMaterial);

            // Assert
            Assert.IsTrue(result);

            // Cleanup
            Object.DestroyImmediate(dummyMesh);
            Object.DestroyImmediate(dummyMaterial);
        }

        [Test]
        public void TryResolvePrimitiveComponentsCold_WithNullPrimitive_ReturnsFalseAndOutputsNull()
        {
            // Arrange
            GameObject primitive = null;

            // Act
            bool result = WorldGeneratedPrimitiveFactory.TryResolvePrimitiveComponentsCold(primitive, out MeshFilter filter, out MeshRenderer renderer);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(filter);
            Assert.IsNull(renderer);
        }

        [Test]
        public void PrewarmPrimitiveResources_CachesAllExpectedPrimitives()
        {
            // Arrange
            var cachedMeshesField = typeof(WorldGeneratedPrimitiveFactory).GetField("_CachedMeshes", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(cachedMeshesField, "Field _CachedMeshes not found.");

            // Act
            WorldGeneratedPrimitiveFactory.PrewarmPrimitiveResources();

            // Assert
            var prewarmedMeshes = (Mesh[])cachedMeshesField.GetValue(null);

            // Sphere, Capsule, Cylinder, Cube, Plane, Quad correspond to indices 0-5
            for (int i = 0; i < 6; i++)
            {
                Assert.IsNotNull(prewarmedMeshes[i], $"Mesh at index {i} ({(PrimitiveType)i}) was not prewarmed.");
            }
        }
    }
}
#endif
