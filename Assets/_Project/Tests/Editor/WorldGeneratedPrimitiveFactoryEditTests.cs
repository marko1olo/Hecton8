using NUnit.Framework;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Tests.Editor
{
    #if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
    public sealed class WorldGeneratedPrimitiveFactoryEditTests
    {
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
    }
    #endif
}
