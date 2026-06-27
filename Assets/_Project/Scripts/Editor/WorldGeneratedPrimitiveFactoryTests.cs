#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class WorldGeneratedPrimitiveFactoryTests
    {
        private GameObject _primitive;

        [TearDown]
        public void Teardown()
        {
            if (_primitive != null)
            {
                Object.DestroyImmediate(_primitive);
                _primitive = null;
            }
        }

        [Test]
        public void ConfigurePrimitiveVisualHot_ValidObject_AppliesTransformsAndVisuals()
        {
            // Arrange
            _primitive = WorldGeneratedPrimitiveFactory.CreateCachedPrimitiveShell(null, "TestShell", out MeshFilter filter, out MeshRenderer renderer);

            Vector3 expectedPosition = new Vector3(1f, 2f, 3f);
            Quaternion expectedRotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 expectedScale = new Vector3(2f, 2f, 2f);

            // Act
            Renderer resultRenderer = WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualHot(
                _primitive,
                PrimitiveType.Cube,
                "TestCube",
                expectedPosition,
                expectedRotation,
                expectedScale
            );

            // Assert
            Assert.IsNotNull(resultRenderer);
            Assert.AreEqual(expectedPosition, _primitive.transform.localPosition);
            Assert.AreEqual(expectedScale, _primitive.transform.localScale);
        }

        [Test]
        public void ConfigurePrimitiveVisualHot_NullPrimitive_ReturnsNull()
        {
            Renderer resultRenderer = WorldGeneratedPrimitiveFactory.ConfigurePrimitiveVisualHot(
                null,
                PrimitiveType.Cube,
                "NullCube",
                Vector3.zero,
                Quaternion.identity,
                Vector3.one
            );
            Assert.IsNull(resultRenderer);
        }
    }
}
#endif
