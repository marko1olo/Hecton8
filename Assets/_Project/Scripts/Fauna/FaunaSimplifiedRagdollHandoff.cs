using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Fauna/Fauna Simplified Ragdoll Handoff")]
    public sealed class FaunaSimplifiedRagdollHandoff : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        [SerializeField] private Renderer vatRenderer;
        [SerializeField] private Rigidbody jointRoot;
        [SerializeField] private Rigidbody jointMidA;
        [SerializeField] private Rigidbody jointMidB;
        [SerializeField] private Rigidbody jointTip;
        [SerializeField, Range(0f, 2f)] private float inheritedVelocityScale = 1f;
        [SerializeField, Range(0f, 8f)] private float deterministicAngularVelocity = 2.4f;

        private Vector3 _initialVelocity;
        private IPhysicsService _physicsService;
        private bool _handoffActive;
        private bool _hotSwapRegistered;

        public bool IsActive => _handoffActive;
        public Vector3 InitialVelocity => _initialVelocity;

        private void OnEnable()
        {
            _physicsService = GlobalRegistry.Physics;
            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void OnDisable()
        {
            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            _physicsService = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
                _physicsService = currentService as IPhysicsService;
        }

        public void BeginHandoff(Renderer fallbackVatRenderer, Vector3 lastVertexVelocity)
        {
            if (_handoffActive)
                return;

            _handoffActive = true;
            _initialVelocity = ProjectInitialVelocity(lastVertexVelocity) * math.max(0f, inheritedVelocityScale);

            Renderer rendererToDisable = vatRenderer != null ? vatRenderer : fallbackVatRenderer;
            if (rendererToDisable != null)
                rendererToDisable.enabled = false;

            uint handoffSeed = ResolveHandoffSeed();
            ApplyJoint(jointRoot, _initialVelocity, handoffSeed, 0);
            ApplyJoint(jointMidA, _initialVelocity, handoffSeed, 1);
            ApplyJoint(jointMidB, _initialVelocity, handoffSeed, 2);
            ApplyJoint(jointTip, _initialVelocity, handoffSeed, 3);
        }

        private void ApplyJoint(Rigidbody body, Vector3 initialVelocity, uint handoffSeed, int ordinal)
        {
            if (body == null)
                return;

            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = true;
            IPhysicsService physicsService = _physicsService;
            if (physicsService != null)
            {
                physicsService.QueueLinearVelocitySet(body, initialVelocity);
                physicsService.QueueAngularVelocitySet(body, ResolveAngularVelocity(handoffSeed, ordinal));
            }
            body.WakeUp();
        }

        private uint ResolveHandoffSeed()
        {
            uint entityHash = unchecked((uint)EntityId.ToULong(GetEntityId()));
            return Hash(entityHash ^ 0xD15EA5E5u);
        }

        private Vector3 ResolveAngularVelocity(uint handoffSeed, int ordinal)
        {
            uint hash = Hash(handoffSeed ^ ((uint)ordinal * 0x9E3779B9u));
            float x = (((hash >> 0) & 255u) * 0.00784313726f) - 1f;
            float y = (((hash >> 8) & 255u) * 0.00784313726f) - 1f;
            float z = (((hash >> 16) & 255u) * 0.00784313726f) - 1f;
            return ProjectInitialVelocity(new Vector3(x, y, z)) * math.max(0f, deterministicAngularVelocity);
        }

        private static Vector3 ProjectInitialVelocity(Vector3 velocity)
        {
            float speedSq = velocity.sqrMagnitude;
            if (speedSq <= 0.0001f)
                return Vector3.zero;

            float speed = speedSq * math.rsqrt(math.max(speedSq, 0.0001f));
            float ax = math.abs(velocity.x);
            float ay = math.abs(velocity.y);
            float az = math.abs(velocity.z);

            if (ax >= ay && ax >= az)
                return new Vector3(math.select(1f, -1f, velocity.x < 0f) * speed, 0f, 0f);

            if (ay >= az)
                return new Vector3(0f, math.select(1f, -1f, velocity.y < 0f) * speed, 0f);

            return new Vector3(0f, 0f, math.select(1f, -1f, velocity.z < 0f) * speed);
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
