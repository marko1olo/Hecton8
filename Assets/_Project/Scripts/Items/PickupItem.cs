// ============================================================================
// HECTON-8 — PickupItem.cs
// Example IInteractable implementation showing all systems working together.
// ============================================================================

using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Gameplay;

namespace Hecton8.Interaction
{
    using Hecton8.World;
    using UnityEngine;

    [RequireComponent(typeof(InteractionHighlighter))]
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable, ISlowTickable, IFixedTickable
    {
        private const float LooseCurrentVelocityInfluence = 0.45f;
        private const float LooseCurrentSpinInfluence = 0.12f;
        private const float CurrentSimulationCullDistance = 100f;
        private const float CurrentSimulationCullDistanceSqr = CurrentSimulationCullDistance * CurrentSimulationCullDistance;

        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        [Header("World State")]
        [Tooltip("Persist this authored world pickup into WorldStateManager depletion storage.")]
        [SerializeField] private bool persistWorldState = true;

        private InteractionHighlighter _highlighter;
        private Rigidbody _rigidbody;
        private HectonPlayerMovement _playerMovement;
        private string _cachedInteractText;
        private int _spatialHandle;
        private bool _registeredToSlowTick;
        private bool _registeredToFixedTick;
        private Vector3 _lastSpatialPosition;
        private bool _worldStateIdentityResolved;
        private bool _worldStateIdentityAvailable;
        private long _worldStatePersistenceKey;
        private long _worldStateChunkKey;
        private Vector3 _worldStateAnchorPosition;
        private Transform _playerTransform;

        public ItemData ItemData => itemData;
        public int Quantity => quantity;

        public void Configure(ItemData data, int itemQuantity)
        {
            itemData = data;
            quantity = Mathf.Max(1, itemQuantity);
            InvalidateWorldStateIdentity();

            _cachedInteractText = itemData != null
                ? itemData.GetInteractText()
                : "Pick up Unknown";
        }

        private void Awake()
        {
            TryGetComponent(out _highlighter);
            TryGetComponent(out _rigidbody);

            _cachedInteractText = itemData != null
                ? itemData.GetInteractText()
                : "Pick up Unknown";
        }

        private void OnEnable()
        {
            ResolveWorldStateIdentity();

            WorldStateManager worldStateManager = WorldStateManager.Instance;
            if (_worldStateIdentityAvailable &&
                worldStateManager != null &&
                worldStateManager.IsPickupDepleted(_worldStatePersistenceKey))
            {
                gameObject.SetActive(false);
                return;
            }

            RegisterSpatialHandle();
            TryRegisterSlowTick();
            TryRegisterFixedTick();
        }

        private void Start()
        {
            TryRegisterSlowTick();
            TryRegisterFixedTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            TryUnregisterFixedTick();
            UnregisterSpatialHandle();
        }

        public void SlowTick()
        {
            if (_spatialHandle == 0)
                return;

            Vector3 currentPosition = transform.position;
            WorldSpatialHashGrid.UpdateGridPosition(_spatialHandle, _lastSpatialPosition, currentPosition);
            _lastSpatialPosition = currentPosition;
        }

        public void FixedTick(float fdt)
        {
            if (_rigidbody == null || _rigidbody.isKinematic || fdt <= 0f)
                return;

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);
            if (_playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - _rigidbody.worldCenterOfMass;
                if (toPlayer.sqrMagnitude > CurrentSimulationCullDistanceSqr)
                {
                    if (!_rigidbody.IsSleeping())
                        _rigidbody.Sleep();

                    return;
                }

                if (_rigidbody.IsSleeping())
                    _rigidbody.WakeUp();
            }

            if (!ResolveSubmergedState())
                return;

            Vector3 sampledCurrent = CurrentVolume.SampleCombinedCurrent(_rigidbody.worldCenterOfMass);
            if (sampledCurrent.sqrMagnitude <= 0.0001f)
                return;

            Vector3 velocityChange = Vector3.ClampMagnitude(sampledCurrent, 6f) * (LooseCurrentVelocityInfluence * fdt);
            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);

            Vector3 spinAxis = Vector3.Cross(Vector3.up, sampledCurrent);
            if (spinAxis.sqrMagnitude > 0.0001f)
                _rigidbody.AddTorque(spinAxis.normalized * (LooseCurrentSpinInfluence * velocityChange.magnitude), ForceMode.VelocityChange);

            if (_spatialHandle != 0)
            {
                WorldSpatialHashGrid.Refresh(_spatialHandle);
                _lastSpatialPosition = transform.position;
            }
        }

        internal bool TryGetWorldStatePersistenceIdentity(out long persistenceKey, out long chunkKey)
        {
            ResolveWorldStateIdentity();
            persistenceKey = _worldStatePersistenceKey;
            chunkKey = _worldStateChunkKey;
            return _worldStateIdentityAvailable;
        }

        private void RegisterSpatialHandle()
        {
            if (_spatialHandle != 0)
                return;

            _spatialHandle = WorldSpatialHashGrid.RegisterPickup(this);
            _lastSpatialPosition = transform.position;
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredToSlowTick)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register((ISlowTickable)this);
            _registeredToSlowTick = true;
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredToFixedTick)
                return;

            if (_rigidbody == null || _rigidbody.isKinematic)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register((IFixedTickable)this);
            _registeredToFixedTick = true;
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredToSlowTick)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister((ISlowTickable)this);

            _registeredToSlowTick = false;
        }

        private void TryUnregisterFixedTick()
        {
            if (!_registeredToFixedTick)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister((IFixedTickable)this);

            _registeredToFixedTick = false;
        }

        public void OnHoverStart()
        {
            _highlighter.SetHighlight(true);
        }

        public void OnHoverEnd()
        {
            _highlighter.SetHighlight(false);
        }

        public void Interact(Transform interactor)
        {
            WorldStateManager.Instance?.RegisterCollectedPickup(_worldStatePersistenceKey, _worldStateChunkKey);
            InteractionEvents.RaiseItemCollected(itemData, quantity, interactor);
            gameObject.SetActive(false);
        }

        public string GetInteractText()
        {
            return _cachedInteractText;
        }

        private void ResolveWorldStateIdentity()
        {
            if (_worldStateIdentityResolved)
                return;

            _worldStateAnchorPosition = transform.position;
            _worldStateIdentityResolved = true;
            bool isPooledRuntimeInstance = TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
            _worldStateIdentityAvailable = persistWorldState &&
                                           !isPooledRuntimeInstance &&
                                           WorldPickupStateCodec.TryBuildIdentity(
                                               transform,
                                               gameObject.scene,
                                               itemData,
                                               _worldStateAnchorPosition,
                                               out _worldStatePersistenceKey,
                                               out _worldStateChunkKey);

            if (_worldStateIdentityAvailable)
                return;

            _worldStatePersistenceKey = 0L;
            _worldStateChunkKey = 0L;
        }

        private void InvalidateWorldStateIdentity()
        {
            _worldStateIdentityResolved = false;
            _worldStateIdentityAvailable = false;
            _worldStatePersistenceKey = 0L;
            _worldStateChunkKey = 0L;
            _worldStateAnchorPosition = default;
        }

        private bool ResolveSubmergedState()
        {
            if (_playerMovement == null &&
                WorldStateManager.Instance != null &&
                WorldStateManager.Instance.PlayerTransform != null)
            {
                WorldStateManager.Instance.PlayerTransform.TryGetComponent(out _playerMovement);
            }

            if (_playerMovement == null)
                return true;

            float depth = Mathf.Max(0f, _playerMovement.CurrentWaterSurfaceY - transform.position.y);
            return SurfaceStateUtility.ResolveUnderwaterFromDepth(depth, true);
        }
    }
}
