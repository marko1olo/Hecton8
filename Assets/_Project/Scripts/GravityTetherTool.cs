namespace Hecton8.Gameplay
{
    using System;
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Inventory;
    using Hecton8.Physics;
    using Hecton8.Tools;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// End-game loot vacuum mode that pulls nearby pickup proxies toward the player's chest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GravityTetherTool : PlayerTool
    {
        private const int TetherHitCapacity = 32;
        private const float MinimumPullDistanceSq = 0.000001f;
        private static readonly int _DefaultTetherLayerMask =
            HectonLayerMasks.DroppedItemLayerMask |
            HectonLayerMasks.InteractableLayerMask;

        [Header("Tether Query")]
        [Tooltip("Maximum tether reach in metres.")]
        [SerializeField, Min(0.1f)] private float rangeMeters = 8f;

        [Tooltip("Sphere radius used by the non-alloc tether query.")]
        [SerializeField, Min(0.05f)] private float sphereRadiusMeters = 1.25f;

        [Tooltip("Velocity change applied to loot toward the chest.")]
        [SerializeField, Min(0f)] private float pullVelocityChange = 4.5f;

        [Tooltip("Distance at which pulled loot is routed through the inventory pickup contract.")]
        [SerializeField, Min(0.05f)] private float pickupDistanceMeters = 0.65f;

        [Tooltip("Layer mask containing interactable pickup proxies.")]
        [SerializeField] private LayerMask interactableMask = HectonLayerMasks.DroppedItemLayerMask | HectonLayerMasks.InteractableLayerMask;

        [Tooltip("Optional chest target. Falls back to the registered player transform.")]
        [SerializeField] private Transform chestTarget;

        private Transform _cachedTransform;
        private Transform _resolvedChestTarget;
        private PlayerInventory _inventory;
        private readonly Collider[] _tetherOverlapCandidates = new Collider[TetherHitCapacity]; // COLD ALLOC: Collider[32] - gravity tether OverlapSphereNonAlloc candidate buffer - owner: GravityTetherTool

        private void Awake()
        {
            _cachedTransform = transform;
        }

        /// <inheritdoc />
        public override void OnSpawn()
        {
            base.OnSpawn();
            _resolvedChestTarget = null;
            _inventory = null;
        }

        /// <inheritdoc />
        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);
            if (!IsEquipped)
                return;

            Transform chest = ResolveChestTarget();
            if (chest == null)
                return;

            Vector3 origin = SanitizePosition(_cachedTransform.position, Vector3.zero);
            Vector3 direction = ResolveSafeDirection(_cachedTransform.forward, Vector3.forward);
            float runtimeRange = ResolveSafePositive(GetRuntimeMaxRange(rangeMeters), 0.1f, 0.1f);
            float sphereRadius = ResolveSafePositive(sphereRadiusMeters, 0.05f, 1.25f);
            float halfRange = runtimeRange * 0.5f;
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                origin + direction * halfRange,
                halfRange + sphereRadius,
                _tetherOverlapCandidates,
                ResolveTetherLayerMask(),
                QueryTriggerInteraction.Collide);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitCount >= TetherHitCapacity)
                Debug.LogWarning("[GravityTetherTool] Tether candidate buffer saturated; results truncated.");
#endif
            float rangeSq = runtimeRange * runtimeRange;
            float tubeRadiusSq = sphereRadius * sphereRadius;
            float pickupDistance = ResolveSafePositive(pickupDistanceMeters, 0.05f, 0.65f);
            float pickupDistanceSq = pickupDistance * pickupDistance;
            Vector3 chestPosition = SanitizePosition(chest.position, origin);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider candidate = _tetherOverlapCandidates[hitIndex];
                if (candidate == null)
                    continue;

                Vector3 candidatePosition = ResolveTetherCandidatePosition(candidate);
                if (!IsFinite(candidatePosition))
                    continue;

                Vector3 fromOrigin = candidatePosition - origin;
                float distanceSq = fromOrigin.sqrMagnitude;
                if (!math.isfinite(distanceSq) || distanceSq > rangeSq)
                    continue;

                float forwardMeters = math.dot((float3)fromOrigin, (float3)direction);
                if (!math.isfinite(forwardMeters) || forwardMeters < 0f || forwardMeters > runtimeRange)
                    continue;

                float radialSq = distanceSq - forwardMeters * forwardMeters;
                if (!math.isfinite(radialSq) || radialSq > tubeRadiusSq)
                    continue;

                ProcessTetherHit(candidate, candidatePosition, chest, chestPosition, pickupDistanceSq);
            }
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            AppendText(ref buffer, "GRAV TETHER // RNG ");
            buffer.AppendFloat(ResolveSafePositive(GetRuntimeMaxRange(rangeMeters), 0.1f, 0.1f), 1);
            AppendText(ref buffer, "M // FORCE ");
            buffer.AppendFloat(ResolveSafePositive(GetRuntimePowerScalar(pullVelocityChange), 0f, 0f), 1);
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (!IsEquipped)
            {
                AppendText(ref buffer, "Tether stowed. Equip before loot recovery.");
                return;
            }

            AppendText(ref buffer, "Hold primary: overlap broadphase, squared-distance gate, no sweep cast.");
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = ResolveSafePositive(rangeMeters, 0.1f, 0.1f);
            profile.PowerScalar = ResolveSafePositive(pullVelocityChange, 0f, 0f);
        }

        private void ProcessTetherHit(
            Collider hitCollider,
            Vector3 interactionPoint,
            Transform chest,
            Vector3 chestPosition,
            float pickupDistanceSq)
        {
            if (hitCollider == null)
                return;

            if (!IsFinite(interactionPoint) || !IsFinite(chestPosition))
                return;

            Vector3 toChest = chestPosition - interactionPoint;
            float sqrDistance = toChest.sqrMagnitude;
            if (!math.isfinite(sqrDistance))
                return;

            if (sqrDistance <= pickupDistanceSq &&
                TryResolvePickupSource(hitCollider, out IInventoryPickupSource pickupSource))
            {
                pickupSource.TryHandleInventoryPickup(ResolveInventory(), chest);
                return;
            }

            Rigidbody body = hitCollider.attachedRigidbody;
            if (body == null)
                return;

            if (sqrDistance <= MinimumPullDistanceSq)
                return;

            Vector3 pullDirection = toChest * math.rsqrt(sqrDistance);
            float pullPower = ResolveSafePositive(GetRuntimePowerScalar(pullVelocityChange), 0f, 0f);
            if (pullPower <= 0f)
                return;

            Vector3 velocityChange = pullDirection * pullPower;
            if (!IsFinite(velocityChange))
                return;

            PhysicsForceRouter.QueueForce(body, velocityChange, ForceMode.VelocityChange);
        }

        private static Vector3 ResolveTetherCandidatePosition(Collider hitCollider)
        {
            Rigidbody body = hitCollider.attachedRigidbody;
            Vector3 position = body != null ? body.worldCenterOfMass : hitCollider.transform.position;
            if (IsFinite(position))
                return position;

            Transform hitTransform = hitCollider.transform;
            return hitTransform != null && IsFinite(hitTransform.position) ? hitTransform.position : Vector3.zero;
        }

        private bool TryResolvePickupSource(Collider hitCollider, out IInventoryPickupSource pickupSource)
        {
            if (InteractableRegistry.TryResolve(hitCollider, out InteractableRegistry.TargetInfo targetInfo) &&
                targetInfo.PickupSource != null)
            {
                pickupSource = targetInfo.PickupSource;
                return true;
            }

            pickupSource = null;
            return false;
        }

        private Transform ResolveChestTarget()
        {
            if (_resolvedChestTarget != null)
                return _resolvedChestTarget;

            if (chestTarget != null)
            {
                _resolvedChestTarget = chestTarget;
                return _resolvedChestTarget;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _resolvedChestTarget = playerContext != null ? playerContext.PlayerTransform : null;
            return _resolvedChestTarget;
        }

        private PlayerInventory ResolveInventory()
        {
            if (_inventory != null)
                return _inventory;

            _inventory = Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime;
            return _inventory;
        }

        private int ResolveTetherLayerMask()
        {
            int mask = interactableMask.value;
            return HectonLayerMasks.IsEverythingLayerMask(mask) ? _DefaultTetherLayerMask : mask;
        }

        private static float ResolveSafePositive(float value, float minimum, float fallback)
        {
            return math.isfinite(value) ? math.max(minimum, value) : fallback;
        }

        private static Vector3 SanitizePosition(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            if (IsFinite(value))
            {
                float lengthSq = value.sqrMagnitude;
                if (math.isfinite(lengthSq) && lengthSq > MinimumPullDistanceSq)
                    return value * math.rsqrt(lengthSq);
            }

            if (IsFinite(fallback))
            {
                float fallbackLengthSq = fallback.sqrMagnitude;
                if (math.isfinite(fallbackLengthSq) && fallbackLengthSq > MinimumPullDistanceSq)
                    return fallback * math.rsqrt(fallbackLengthSq);
            }

            return Vector3.forward;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return buffer.Append(value.AsSpan());
        }
    }
}
