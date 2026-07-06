using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using System.Collections.Generic;
using System.Reflection;

namespace CandiceAIforGames.AI.Tests
{
    public class CandiceAIControllerScanForObjects2DTests
    {
        private GameObject _go;
        private CandiceAIController _controller;

        // Use a wrapper/mock if needed, or check the state of the controller.
        // We'll verify that detectionRequest is updated properly by checking it via reflection.

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("CandiceAI");
            _controller = _go.AddComponent<CandiceAIController>();
            // Add a module so it doesn't null ref when calling ScanForObjects2D
            _controller.detectionModule = new CandiceModuleDetection(_go.transform, null, "MockModule");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void ScanForObjects2D_SetsDetectionRequestCorrectly()
        {
            // Arrange
            _controller.SensorType = SensorType.Sphere;
            var testTags = new List<string> { "Player", "Enemy" };
            _controller.ObjectTags = testTags;
            _controller.DetectionRadius = 15f;
            _controller.DetectionHeight = 5f;
            _controller.LineOfSight = 180f;
            _controller.Is3D = false;

            // Act
            _controller.ScanForObjects2D();

            // Assert
            var field = typeof(CandiceAIController).GetField("detectionRequest", BindingFlags.NonPublic | BindingFlags.Instance);
            var detectionRequest = (CandiceDetectionRequest)field.GetValue(_controller);

            Assert.That(detectionRequest.type, Is.EqualTo(SensorType.Sphere));
            Assert.That(detectionRequest.detectionTags, Is.EqualTo(testTags));
            Assert.That(detectionRequest.radius, Is.EqualTo(15f));
            Assert.That(detectionRequest.height, Is.EqualTo(5f));
            Assert.That(detectionRequest.lineOfSight, Is.EqualTo(180f));
            Assert.That(detectionRequest.is3D, Is.False);
        }
    }
}
