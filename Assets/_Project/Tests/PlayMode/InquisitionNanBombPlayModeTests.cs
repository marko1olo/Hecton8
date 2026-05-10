using System.Collections;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.PlayMode
{
    public sealed class InquisitionNanBombPlayModeTests
    {
        private const string NonFiniteForceLog = "[PhysicsApplySystem] Non-finite force packet detected. Zeroing vector.";
        private const string NonFiniteTorqueLog = "[PhysicsApplySystem] Non-finite torque packet detected. Zeroing vector.";
        private const string NonFinitePointOffsetLog = "[PhysicsApplySystem] Non-finite point-offset packet detected. Zeroing offset.";

        [UnityTest]
        public IEnumerator ForcePacketIngress_RejectsNaNAndInfinityWithoutCrash()
        {
            IPhysicsService previousPhysics = GlobalRegistry.Physics;
            GameObject physicsObject = null;
            PhysicsApplySystem system = ResolvePhysicsApplySystem(out physicsObject);
            Assert.IsNotNull(system, "PhysicsApplySystem runtime could not be created for NaN-bomb ingress.");
            GameObject bodyObject = new GameObject("nan-bomb-rigidbody");
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.useGravity = false;

            LogAssert.Expect(LogType.Error, NonFiniteForceLog);
            bool nanForceAccepted = PhysicsForceRouter.QueueForce(
                body,
                new Vector3(float.NaN, 1f, 0f),
                ForceMode.Acceleration);

            LogAssert.Expect(LogType.Error, NonFiniteTorqueLog);
            bool infinityTorqueAccepted = PhysicsForceRouter.QueueTorque(
                body,
                new Vector3(0f, float.PositiveInfinity, 0f),
                ForceMode.Acceleration);

            LogAssert.Expect(LogType.Error, NonFiniteForceLog);
            bool infinityForceAtPointAccepted = PhysicsForceRouter.QueueForceAtPosition(
                body,
                new Vector3(0f, 0f, float.PositiveInfinity),
                Vector3.zero,
                ForceMode.Force);

            LogAssert.Expect(LogType.Error, NonFinitePointOffsetLog);
            bool infinityPointAccepted = system.QueueForceAtPosition(
                body,
                Vector3.up,
                new Vector3(float.PositiveInfinity, 0f, 0f),
                ForceMode.Force);

            Assert.IsFalse(nanForceAccepted, "NaN force packet was accepted.");
            Assert.IsFalse(infinityTorqueAccepted, "Infinity torque packet was accepted.");
            Assert.IsFalse(infinityForceAtPointAccepted, "Infinity force-at-position packet was accepted.");
            Assert.IsFalse(infinityPointAccepted, "Infinity force point packet was accepted.");

            yield return null;
            yield return null;

            AssertFinite(body.position, "Rigidbody position");
            AssertFinite(body.linearVelocity, "Rigidbody velocity");
            AssertFinite(body.angularVelocity, "Rigidbody angular velocity");

            Object.Destroy(bodyObject);
            if (previousPhysics == null && physicsObject != null)
                Object.Destroy(physicsObject);
            yield return null;
        }

        [Test]
        public void PlayerMovementRuntimeStateIngress_SanitizesNaNAndInfinity()
        {
            PlayerRuntimeContext context = new PlayerRuntimeContext();
            PlayerMovementRuntimeState fallback = default;
            fallback.WorldPosition = new float3(10f, 20f, 30f);
            fallback.PredictedWorldPosition = new float3(11f, 21f, 31f);
            fallback.PredictedAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(11d, 21d, 31d));
            fallback.Velocity = new float3(1f, 2f, 3f);
            fallback.Forward = new float3(0f, 0f, 1f);
            fallback.CameraForward = new float3(0f, 1f, 0f);
            fallback.DepthMeters = 42f;
            fallback.TransportSpeedMultiplier = 1f;
            fallback.UnderwaterStressIntensity01 = 0.25f;
            fallback.Flags = 7u;
            context.PublishMovementState(in fallback);

            PlayerMovementRuntimeState toxic = fallback;
            toxic.WorldPosition = new float3(float.NaN, 0f, 0f);
            toxic.PredictedWorldPosition = new float3(0f, float.PositiveInfinity, 0f);
            toxic.PredictedAup.LocalX = float.NaN;
            toxic.Velocity = new float3(0f, 0f, float.NegativeInfinity);
            toxic.Forward = new float3(float.NaN, 0f, 0f);
            toxic.CameraForward = new float3(0f, float.PositiveInfinity, 0f);
            toxic.DepthMeters = float.NaN;
            toxic.TransportSpeedMultiplier = float.NegativeInfinity;
            toxic.UnderwaterStressIntensity01 = float.PositiveInfinity;
            context.PublishMovementState(in toxic);

            PlayerMovementRuntimeState sanitized = context.MovementState;
            AssertFinite(sanitized.WorldPosition, "WorldPosition");
            AssertFinite(sanitized.PredictedWorldPosition, "PredictedWorldPosition");
            AssertFinite(sanitized.Velocity, "Velocity");
            AssertFinite(sanitized.Forward, "Forward");
            AssertFinite(sanitized.CameraForward, "CameraForward");
            Assert.IsTrue(MathGuard.IsFinite(in sanitized.PredictedAup), "PredictedAup is non-finite.");
            Assert.AreEqual(fallback.WorldPosition, sanitized.WorldPosition);
            Assert.AreEqual(fallback.PredictedAup.LocalX, sanitized.PredictedAup.LocalX);
            Assert.GreaterOrEqual(sanitized.DepthMeters, 0f);
            Assert.GreaterOrEqual(sanitized.TransportSpeedMultiplier, 0.01f);
            Assert.GreaterOrEqual(sanitized.UnderwaterStressIntensity01, 0f);
            Assert.LessOrEqual(sanitized.UnderwaterStressIntensity01, 1f);
        }

        private static void AssertFinite(Vector3 value, string label)
        {
            Assert.IsTrue(MathGuard.IsFinite(value), label + " is non-finite.");
        }

        private static void AssertFinite(float3 value, string label)
        {
            Assert.IsTrue(MathGuard.IsFinite(value), label + " is non-finite.");
        }

        private static PhysicsApplySystem ResolvePhysicsApplySystem(out GameObject ownedObject)
        {
            ownedObject = null;
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            if (system != null)
                return system;

            ownedObject = new GameObject("[NanBomb_PhysicsApplySystem]");
            system = ownedObject.AddComponent<PhysicsApplySystem>();
            system.InitializeService();
            return system;
        }
    }
}
