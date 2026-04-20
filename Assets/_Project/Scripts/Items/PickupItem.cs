// ============================================================================
// HECTON-8 — PickupItem.cs
// Example IInteractable implementation showing all systems working together.
// ============================================================================

using Hecton8.Core;
using Hecton8.Items;

namespace Hecton8.Interaction
{
    using Hecton8.World;
    using UnityEngine;

    [RequireComponent(typeof(InteractionHighlighter))]
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour, IInteractable, ISlowTickable
    {
        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        [Header("World State")]
        [Tooltip("Persist this authored world pickup into WorldStateManager depletion storage.")]
        [SerializeField] private bool persistWorldState = true;

        private InteractionHighlighter _highlighter;
        private string _cachedInteractText;
        private int _spatialHandle;
        private bool _registeredToSlowTick;
        private Vector3 _lastSpatialPosition;
        private bool _worldStateIdentityResolved;
        private bool _worldStateIdentityAvailable;
        private long _worldStatePersistenceKey;
        private long _worldStateChunkKey;
        private Vector3 _worldStateAnchorPosition;

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
        }

        private void Start()
        {
            TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            UnregisterSpatialHandle();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
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

        private void TryUnregisterSlowTick()
        {
            if (!_registeredToSlowTick)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister((ISlowTickable)this);

            _registeredToSlowTick = false;
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
    }
}
