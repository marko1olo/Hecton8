#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Core.Contracts;

namespace Hecton8.Tests.Core
{
    public class FoveatedSimulationManagerTests
    {
        private class MockTarget : IFoveatedSimulationTarget
        {
            public int FoveatedTargetIndex { get; set; } = -1;
            public Transform SimulationTransform { get; }
            public Transform VisualTransform { get; }
            public AudioSource DopplerAudioSource { get; }
            public uint FoveatedEntityHash { get; } = 1;
            public ushort FoveatedEntityId { get; } = 1;

            public FoveatedTickRate LastTickRate;
            public float LastTickInterval;
            public float LastImportanceScore;
            public bool LastInsideFrustum;

            public FoveatedSimulationTier LastTier;
            public float LastDistanceMeters;
            public bool LastTier0Locked;

            public MockTarget()
            {
                var go = new GameObject("MockTarget");
                SimulationTransform = go.transform;
                VisualTransform = go.transform;
                DopplerAudioSource = go.AddComponent<AudioSource>();
            }

            public void Cleanup()
            {
                if (SimulationTransform != null) Object.DestroyImmediate(SimulationTransform.gameObject);
            }

            public void OnFoveatedCadenceResolved(FoveatedTickRate tickRate, float tickIntervalSeconds, float importanceScore, bool insideFrustum)
            {
                LastTickRate = tickRate;
                LastTickInterval = tickIntervalSeconds;
                LastImportanceScore = importanceScore;
                LastInsideFrustum = insideFrustum;
            }

            public void OnFoveatedTierResolved(FoveatedSimulationTier tier, float distanceMeters, bool tier0Locked)
            {
                LastTier = tier;
                LastDistanceMeters = distanceMeters;
                LastTier0Locked = tier0Locked;
            }

            public bool TryHandleFoveatedFrozenWrap(Vector3 cameraPosition, Vector3 cameraForward, float distanceMeters)
            {
                return false;
            }

            public void Tick(float deltaTime) {}
        }

        [Test]
        public void FoveatedSimulationManager_RegisterTarget_AssignsIndexAndResolvesInitialCadence()
        {
            // Arrange
            var manager = new FoveatedSimulationManager();
            var target = new MockTarget();

            // Act
            manager.RegisterTarget(target);

            // Assert
            Assert.AreEqual(0, target.FoveatedTargetIndex, "Target index should be assigned (0 for first target).");
            Assert.AreEqual(FoveatedTickRate.Center60Hz, target.LastTickRate, "Initial tick rate should be Center60Hz.");
            Assert.AreEqual(FoveatedSimulationTier.Active, target.LastTier, "Initial tier should be Active.");

            // Cleanup
            manager.Dispose();
            target.Cleanup();
        }

        [Test]
        public void FoveatedSimulationManager_RegisterTarget_IgnoresAlreadyRegisteredTarget()
        {
            // Arrange
            var manager = new FoveatedSimulationManager();
            var target = new MockTarget();

            // Act
            manager.RegisterTarget(target);
            int initialIndex = target.FoveatedTargetIndex;

            // Attempt double registration
            manager.RegisterTarget(target);

            // Assert
            Assert.AreEqual(initialIndex, target.FoveatedTargetIndex, "Target index should not change on duplicate registration.");

            // Try to resolve tier for the second index (should be false/empty)
            bool secondExists = manager.TryGetEntityTier(1, out _);
            Assert.IsFalse(secondExists, "No second target should be registered.");

            // Cleanup
            manager.Dispose();
            target.Cleanup();
        }

        [Test]
        public void FoveatedSimulationManager_RegisterTarget_IgnoresNullTarget()
        {
            // Arrange
            var manager = new FoveatedSimulationManager();

            // Act
            manager.RegisterTarget(null);

            // Assert
            bool anyExists = manager.TryGetEntityTier(0, out _);
            Assert.IsFalse(anyExists, "No target should be registered for null input.");

            // Cleanup
            manager.Dispose();
        }

        [Test]
        public void FoveatedSimulationManager_RegisterTarget_RespectsMaxTargetsLimit()
        {
            // Arrange
            var manager = new FoveatedSimulationManager();
            var targets = new MockTarget[513]; // MaxTargets is 512

            for (int i = 0; i < 513; i++)
            {
                targets[i] = new MockTarget();
            }

            // Act & Assert
            for (int i = 0; i < 512; i++)
            {
                manager.RegisterTarget(targets[i]);
                Assert.AreEqual(i, targets[i].FoveatedTargetIndex, $"Target {i} should be registered successfully.");
            }

            // Attempt to register the 513th target
            manager.RegisterTarget(targets[512]);

            // Assuming MaxTargets = 512, the 513th should be rejected
            Assert.AreEqual(-1, targets[512].FoveatedTargetIndex, "Target exceeding capacity should not be registered.");

            // Cleanup
            manager.Dispose();
            foreach (var target in targets) target.Cleanup();
        }
    }
}
#endif
