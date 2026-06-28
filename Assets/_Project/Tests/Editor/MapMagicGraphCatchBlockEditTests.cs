using NUnit.Framework;
using UnityEngine;
using System;
using MapMagic.Nodes;

#if UNITY_EDITOR
namespace MapMagic.Nodes.Tests
{
    public class MapMagicGraphCatchBlockEditTests
    {
        [Test]
        public void OnAfterDeserialize_WhenExceptionThrown_CatchesAndSetsGeneratorsToNull()
        {
            // Arrange
            var graph = ScriptableObject.CreateInstance<Graph>();
            graph.generators = new MapMagic.Products.Generator[1]; // Set it to non-null initially to ensure it gets cleared

            graph.MockDeserialize = (g) => throw new InvalidOperationException("Simulated deserialize error");

            // Act & Assert
            var ex = Assert.Throws<Exception>(() => graph.OnAfterDeserialize());
            Assert.That(ex.Message, Does.Contain("Could not load graph data:"));
            Assert.That(ex.Message, Does.Contain("Simulated deserialize error"));
            Assert.IsNull(graph.generators);

            UnityEngine.Object.DestroyImmediate(graph);
        }
    }
}
#endif
