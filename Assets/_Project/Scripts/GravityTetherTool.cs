namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Interaction;
    using Hecton8.Inventory;
    using Hecton8.Items;
    using Hecton8.Physics;
    using Hecton8.Tools;
    using UnityEngine;

    /// <summary>
    /// End-game loot vacuum mode that pulls nearby pickup proxies toward the player's chest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GravityTetherTool : PlayerTool
    {
        private const int TetherHitCapacity = 32;
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
        private readonly RaycastHit[] _tetherHits = new RaycastHit[TetherHitCapacity]; // COLD ALLOC: RaycastHit[32] — gravity tether SphereCastNonAlloc result buffer — owner: GravityTetherTool

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

            Vector3 origin = _cachedTransform.position;
            Vector3 direction = _cachedTransform.forward;
            float runtimeRange = GetRuntimeMaxRange(rangeMeters);
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                sphereRadiusMeters,
                direction,
                _tetherHits,
                runtimeRange,
                ResolveTetherLayerMask(),
                QueryTriggerInteraction.Collide);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                ProcessTetherHit(_tetherHits[hitIndex], chest);
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, rangeMeters);
            profile.PowerScalar = Mathf.Max(0.1f, pullVelocityChange);
        }

        private void ProcessTetherHit(RaycastHit hit, Transform chest)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return;

            Vector3 chestPosition = chest.position;
            Vector3 toChest = chestPosition - hit.point;
            float sqrDistance = toChest.sqrMagnitude;
            if (sqrDistance <= pickupDistanceMeters * pickupDistanceMeters &&
                TryResolvePickupSource(hitCollider, out IInventoryPickupSource pickupSource))
            {
                pickupSource.TryHandleInventoryPickup(ResolveInventory(), chest);
                return;
            }

            Rigidbody body = hit.rigidbody;
            if (body == null)
                return;

            Vector3 pullDirection = sqrDistance > 0.000001f ? toChest / Mathf.Sqrt(sqrDistance) : chest.forward;
            PhysicsForceRouter.QueueForce(body, pullDirection * GetRuntimePowerScalar(pullVelocityChange), ForceMode.VelocityChange);
        }

        private bool TryResolvePickupSource(Collider hitCollider, out IInventoryPickupSource pickupSource)
        {
            if (hitCollider.TryGetComponent(out PickupItem pickupItem))
            {
                pickupSource = pickupItem;
                return true;
            }

            if (hitCollider.TryGetComponent(out HectonItem hectonItem))
            {
                pickupSource = hectonItem;
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

            _inventory = PlayerInventory.Instance;
            return _inventory;
        }

        private int ResolveTetherLayerMask()
        {
            int mask = interactableMask.value;
            return HectonLayerMasks.IsEverythingLayerMask(mask) ? _DefaultTetherLayerMask : mask;
        }
    }
}
