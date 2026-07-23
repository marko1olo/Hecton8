#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core;
using Hecton8.Interaction;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HarpoonLauncherToolSecondaryUseTests
    {
        private class TestHarpoonLauncherTool : HarpoonLauncherTool
        {
            public bool IsEquippedOverride { get; set; } = true;
            public override bool IsEquipped => IsEquippedOverride;

            public bool TryQueueTargetHitOverrideResult { get; set; }
            public InteractionSurfaceHit TryQueueTargetHitResult { get; set; }

            protected override bool TryQueueTargetHit(out InteractionSurfaceHit hit, out Vector3 origin, out Vector3 forward)
            {
                hit = TryQueueTargetHitResult;
                origin = Vector3.zero;
                forward = Vector3.forward;
                return TryQueueTargetHitOverrideResult;
            }

            public void InvokeUseSecondary(float deltaTime)
            {
                base.UseSecondary(deltaTime);
            }
        }

        [Test]
        public void UseSecondary_WithHitButNoRigidbody_DoesNotThrow()
        {
            var go = new GameObject("HarpoonLauncherTool_Test_NoRigidbody");
            try
            {
                var tool = go.AddComponent<TestHarpoonLauncherTool>();

                var hitGo = new GameObject("HitObject");
                var collider = hitGo.AddComponent<BoxCollider>();

                tool.TryQueueTargetHitOverrideResult = true;
                tool.TryQueueTargetHitResult = InteractionSurfaceHit.FromSurface(Vector3.zero, Vector3.up, 10f, collider);

                Assert.DoesNotThrow(() => tool.InvokeUseSecondary(0.1f));

                Object.DestroyImmediate(hitGo);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UseSecondary_WithHitAndKinematicRigidbody_DoesNotThrow()
        {
            var go = new GameObject("HarpoonLauncherTool_Test_Kinematic");
            try
            {
                var tool = go.AddComponent<TestHarpoonLauncherTool>();

                var hitGo = new GameObject("HitObject");
                var collider = hitGo.AddComponent<BoxCollider>();
                var rb = hitGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                tool.TryQueueTargetHitOverrideResult = true;
                tool.TryQueueTargetHitResult = InteractionSurfaceHit.FromSurface(Vector3.zero, Vector3.up, 10f, collider);

                Assert.DoesNotThrow(() => tool.InvokeUseSecondary(0.1f));

                Object.DestroyImmediate(hitGo);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UseSecondary_WithHitAndMassiveRigidbody_DoesNotThrow()
        {
            var go = new GameObject("HarpoonLauncherTool_Test_Massive");
            try
            {
                var tool = go.AddComponent<TestHarpoonLauncherTool>();

                var hitGo = new GameObject("HitObject");
                var collider = hitGo.AddComponent<BoxCollider>();
                var rb = hitGo.AddComponent<Rigidbody>();
                rb.mass = 1000f; // Exceeds maxReelMass default of 55f

                tool.TryQueueTargetHitOverrideResult = true;
                tool.TryQueueTargetHitResult = InteractionSurfaceHit.FromSurface(Vector3.zero, Vector3.up, 10f, collider);

                Assert.DoesNotThrow(() => tool.InvokeUseSecondary(0.1f));

                Object.DestroyImmediate(hitGo);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
