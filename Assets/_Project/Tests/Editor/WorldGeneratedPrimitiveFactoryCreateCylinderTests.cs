#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class WorldGeneratedPrimitiveFactoryCreateCylinderTests
    {
        [Test]
        public void CreateCylinder_SetsCorrectNameAndScale()
        {
            // Arrange
            string expectedName = "TestCylinder";
            float radius = 2.5f;
            float height = 10f;

            // Act
            GameObject cylinder = WorldGeneratedPrimitiveFactory.CreateCylinder(expectedName, radius, height);

            // Assert
            Assert.IsNotNull(cylinder);
            Assert.AreEqual(expectedName, cylinder.name);
            Assert.AreEqual(new Vector3(radius * 2f, height * 0.5f, radius * 2f), cylinder.transform.localScale);
            Assert.IsNull(cylinder.GetComponent<Collider>(), "Collider should have been removed.");

            // Cleanup
            Object.DestroyImmediate(cylinder);
        }
    }
}
#endif
