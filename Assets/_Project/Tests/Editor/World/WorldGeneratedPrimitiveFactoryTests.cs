using NUnit.Framework;
using UnityEngine;
using Hecton8.World;
using System.Reflection;

namespace Hecton8.Tests.World
{
    [TestFixture]
    public class WorldGeneratedPrimitiveFactoryTests
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
        public void RegisterPrimitiveResourcesCold_ValidInputs_ReturnsTrueAndCachesResources()
        {
            // Arrange
            var cachedMeshesField = typeof(WorldGeneratedPrimitiveFactory).GetField("_CachedMeshes", BindingFlags.NonPublic | BindingFlags.Static);
            var cachedMaterialsField = typeof(WorldGeneratedPrimitiveFactory).GetField("_CachedMaterials", BindingFlags.NonPublic | BindingFlags.Static);

            Mesh testMesh = new Mesh();
            Material testMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            // Act
            bool result = WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(PrimitiveType.Cube, testMesh, testMaterial);

            // Assert
            Assert.IsTrue(result);

            var cachedMeshes = (Mesh[])cachedMeshesField.GetValue(null);
            var cachedMaterials = (Material[])cachedMaterialsField.GetValue(null);

            Assert.AreEqual(testMesh, cachedMeshes[(int)PrimitiveType.Cube]);
            Assert.AreEqual(testMaterial, cachedMaterials[(int)PrimitiveType.Cube]);
        }

        [Test]
        public void RegisterPrimitiveResourcesCold_NullMesh_ReturnsFalseAndDoesNotCache()
        {
            // Arrange
            var cachedMeshesField = typeof(WorldGeneratedPrimitiveFactory).GetField("_CachedMeshes", BindingFlags.NonPublic | BindingFlags.Static);
            var cachedMaterialsField = typeof(WorldGeneratedPrimitiveFactory).GetField("_CachedMaterials", BindingFlags.NonPublic | BindingFlags.Static);

            Material testMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            // Act
            bool result = WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(PrimitiveType.Cube, null, testMaterial);

            // Assert
            Assert.IsFalse(result);

            var cachedMeshes = (Mesh[])cachedMeshesField.GetValue(null);
            var cachedMaterials = (Material[])cachedMaterialsField.GetValue(null);

            Assert.IsNull(cachedMeshes[(int)PrimitiveType.Cube]);
            Assert.IsNull(cachedMaterials[(int)PrimitiveType.Cube]);
        }

        [Test]
        public void RegisterPrimitiveResourcesCold_OutOfBoundsPrimitiveType_ReturnsFalse()
        {
            // Arrange
            Mesh testMesh = new Mesh();
            Material testMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            PrimitiveType outOfBoundsType = (PrimitiveType)99;

            // Act
            bool result = WorldGeneratedPrimitiveFactory.RegisterPrimitiveResourcesCold(outOfBoundsType, testMesh, testMaterial);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void PrewarmPrimitiveResources_CachesAllExpectedPrimitives()
        {
            // Arrange
            var cachedMeshesField = typeof(WorldGeneratedPrimitiveFactory).GetField("_CachedMeshes", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(cachedMeshesField, "Field _CachedMeshes not found.");

            // Verify empty initially
            var initialMeshes = (Mesh[])cachedMeshesField.GetValue(null);
            foreach (var mesh in initialMeshes)
            {
                Assert.IsNull(mesh, "Meshes should be null before prewarm.");
            }

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
