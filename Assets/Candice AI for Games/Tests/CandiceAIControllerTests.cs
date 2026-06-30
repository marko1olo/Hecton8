using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;

namespace CandiceAIforGames.AI.Tests
{
    public class CandiceAIControllerTests
    {
        private GameObject _go;
        private CandiceAIController _controller;
        private GameObject _targetGo;
        private CandiceModuleDetection _detectionModule;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("CandiceAI");
            _controller = _go.AddComponent<CandiceAIController>();

            _targetGo = new GameObject("Target");
            _controller.MainTarget = _targetGo;
            _controller.MovePoint = Vector3.zero;

            _detectionModule = new CandiceModuleDetection(_go.transform, null, "MockModule");
            _controller.detectionModule = _detectionModule;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_targetGo != null) Object.DestroyImmediate(_targetGo);
        }

        [Test]
        public void AvoidObstacles_WhenColIsNull_UsesLocalScale()
        {
            // Set scale to test calculation
            _go.transform.localScale = new Vector3(3f, 1f, 1f);

            // Ensure col is null
            SetPrivateField(_controller, "col", null);

            _controller.AvoidObstacles();

            // Expected: localScale.x * 2 = 6f
            Assert.That(_controller.HalfHeight, Is.EqualTo(6f));
        }

        [Test]
        public void AvoidObstacles_WhenIs3DAndColIsNotNull_UsesColliderExtents()
        {
            // Add a BoxCollider to act as col
            var boxCol = _go.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(4f, 4f, 4f); // bounds.extents.x will be 2

            SetPrivateField(_controller, "col", boxCol);
            _controller.Is3D = true;

            _controller.AvoidObstacles();

            // Expected: extents.x * 2 = 2 * 2 = 4f
            Assert.That(_controller.HalfHeight, Is.EqualTo(4f));
        }

        [Test]
        public void AvoidObstacles_WhenNotIs3DAndColIsNotNull_UsesCollider2DExtents()
        {
            // Add a BoxCollider2D to act as col
            var boxCol2D = _go.AddComponent<BoxCollider2D>();
            boxCol2D.size = new Vector2(5f, 5f); // bounds.extents.x will be 2.5

            SetPrivateField(_controller, "col", boxCol2D);
            _controller.Is3D = false;

            _controller.AvoidObstacles();

            // Expected: extents.x * 2 = 2.5 * 2 = 5f
            Assert.That(_controller.HalfHeight, Is.EqualTo(5f));
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(obj, value);
        }
    }
}
