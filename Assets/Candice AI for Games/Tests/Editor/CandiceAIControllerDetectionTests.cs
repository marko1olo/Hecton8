using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using System.Collections.Generic;

namespace CandiceAIforGames.AI.Tests
{
    public class MockCandiceModuleDetection : CandiceModuleDetection
    {
        public bool ScanForObjectsCalled { get; private set; }
        public CandiceDetectionRequest LastRequest { get; private set; }

        public MockCandiceModuleDetection(Transform transform, System.Action<CandiceDetectionResults> onObjectDetectedCallback)
            : base(transform, onObjectDetectedCallback)
        {
        }

        public override void ScanForObjects(CandiceDetectionRequest request)
        {
            ScanForObjectsCalled = true;
            LastRequest = request;
        }
    }

    public class CandiceAIControllerDetectionTests
    {
        [Test]
        public void ScanForObjects_PopulatesAndPassesRequest()
        {
            var go = new GameObject("TestAI");
            try
            {
                // Arrange
                var controller = go.AddComponent<CandiceAIController>();

                var mockModule = new MockCandiceModuleDetection(go.transform, null);
                controller.detectionModule = mockModule;

                // Set up test data
                controller.SensorType = SensorType.Sphere;
                controller.ObjectTags = new List<string> { "Player", "Enemy" };
                controller.DetectionRadius = 15f;
                controller.DetectionHeight = 3f;
                controller.LineOfSight = 5f;
                controller.Is3D = true;

                // Act
                controller.ScanForObjects();

                // Assert
                Assert.That(mockModule.ScanForObjectsCalled, Is.True, "ScanForObjects should be called on the detection module.");

                var req = mockModule.LastRequest;
                Assert.That(req.type, Is.EqualTo(SensorType.Sphere));
                Assert.That(req.detectionTags, Is.EqualTo(controller.ObjectTags));
                Assert.That(req.radius, Is.EqualTo(15f));
                Assert.That(req.height, Is.EqualTo(3f));
                Assert.That(req.lineOfSight, Is.EqualTo(5f));
                Assert.That(req.is3D, Is.EqualTo(true));
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(go);
            }
        }
    }
}
