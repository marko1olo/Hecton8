using NUnit.Framework;
using UnityEngine;
using Hecton8.World;
using Hecton8.Core;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS

namespace Hecton8.Tests.Editor
{
    public class WorldGeneratedPrimitiveFactoryEditTests
    {
        [Test]
        public void TryResolvePrimitiveComponentsCold_WithNullPrimitive_ReturnsFalseAndOutputsNulls()
        {
            // Act
            bool result = WorldGeneratedPrimitiveFactory.TryResolvePrimitiveComponentsCold(null, out MeshFilter filter, out MeshRenderer renderer);

            // Assert
            Assert.IsFalse(result);
            Assert.IsNull(filter);
            Assert.IsNull(renderer);
        }
    }
}

#endif
