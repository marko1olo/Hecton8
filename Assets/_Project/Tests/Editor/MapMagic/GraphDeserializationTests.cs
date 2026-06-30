#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MapMagic.Nodes;
using System;
using System.Reflection;

namespace MapMagic.Tests
{
    public class GraphDeserializationTests
    {
        private Graph _graph;

        [SetUp]
        public void SetUp()
        {
            _graph = ScriptableObject.CreateInstance<Graph>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_graph != null)
            {
                UnityEngine.Object.DestroyImmediate(_graph);
            }
        }

        private class MockThrowingSerializer : GraphSerializer200Beta
        {
            public override void Deserialize(Graph graph)
            {
                throw new InvalidOperationException("Simulated deserialization failure");
            }
        }

        [Test]
        public void Graph_OnAfterDeserialize_CatchesException_SetsGeneratorsNull_AndThrowsException()
        {
            // Arrange
            _graph.generators = new Generator[] { }; // Initialize with an empty array

            var serializerField = typeof(Graph).GetField("serializer200beta", BindingFlags.NonPublic | BindingFlags.Instance);
            serializerField.SetValue(_graph, new MockThrowingSerializer());

            // Ensure it starts as not null
            Assert.IsNotNull(_graph.generators, "Generators should start out non-null");

            // Act
            Exception thrownException = null;
            try
            {
                _graph.OnAfterDeserialize();
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            // Assert
            Assert.IsNotNull(thrownException, "An exception should have been re-thrown");
            Assert.IsTrue(thrownException.Message.StartsWith("Could not load graph data:"), "Exception message should indicate failure to load graph data");
            Assert.IsTrue(thrownException.Message.Contains("Simulated deserialization failure"), "Exception message should contain the original exception message");
            Assert.IsNull(_graph.generators, "Generators should be set to null after a deserialization failure");
        }
    }
}
#endif
