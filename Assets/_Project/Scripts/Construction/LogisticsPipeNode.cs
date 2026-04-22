using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Delayed point-to-point logistics pipe between two storage crates.
    /// Uses a local two-phase transfer: source slot reservation, in-flight transit, then destination commit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Logistics Pipe Node")]
    public sealed class LogisticsPipeNode : MonoBehaviour, ISlowTickable, IPoolable, IPowerComponent
    {
        private const float SlowTickDeltaTime = 0.5f;
        private const float PositionRefreshEpsilonSqr = 0.0004f;

        private static int s_NextReservationId = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_NextReservationId = 1;
        }

        [Header("── Endpoints ──────────────────────────────")]
        [Tooltip("Source storage crate that exports items into the pipe.")]
        [SerializeField] private StorageCrate sourceCrate;

        [Tooltip("Destination storage crate that receives items after transit completes.")]
        [SerializeField] private StorageCrate destinationCrate;

        [Tooltip("Optional item filter. When empty, the pipe exports the first available unreserved item.")]
        [SerializeField] private ItemData filterItem;

        [Tooltip("Optional line renderer used to visualize the cargo pipe between the source and destination.")]
        [SerializeField] private LineRenderer cableRenderer;

        [Header("── Throughput ─────────────────────────────")]
        [Tooltip("Seconds between export attempts while the pipe has power and no item is currently in transit.")]
        [SerializeField, Range(0.5f, 30f)] private float exportIntervalSeconds = 2f;

        [Tooltip("Transit speed in meters per second for staged cargo travelling through the pipe.")]
        [SerializeField, Range(0.5f, 50f)] private float transitSpeedMetersPerSecond = 8f;

        [Header("── Power ──────────────────────────────────")]
        [Tooltip("Continuous draw while the pipe is actively moving or staging a cargo packet.")]
        [SerializeField, Range(0f, 100f)] private float activePowerDraw = 8f;

        [Tooltip("Priority used when the power grid starts shedding non-critical logistics links.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 48;

        [Header("── Diagnostics ───────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private string _debugInFlightItemId;
        [SerializeField] private float _debugTransitRemaining;
        [SerializeField] private int _debugReservationId;

        private PowerNode _powerNode;
        private Transform _cachedTransform;
        private bool _registered;
        private bool _hasPower = true;
        private float _exportTimer;
        private int _activeReservationId;
        private ItemData _inFlightItem;
        private float _transitRemaining;
        private Vector3 _lastSourcePosition;
        private Vector3 _lastDestinationPosition;

        public float PowerRating => _inFlightItem != null ? -activePowerDraw : 0f;
        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;

        private void Awake()
        {
            _cachedTransform = transform;
            _powerNode = GetComponent<PowerNode>();
        }

        private void OnEnable()
        {
            TryRegister();
            RefreshCableVisuals(true);
        }

        private void OnDisable()
        {
            RollbackInFlightTransfer();
            TryUnregister();
            ClearCableVisuals();
        }

        private void OnDestroy()
        {
            RollbackInFlightTransfer();
            TryUnregister();
            ClearCableVisuals();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            _exportTimer = 0f;
            TryRegister();
            RefreshCableVisuals(true);
        }

        public void OnDespawn()
        {
            RollbackInFlightTransfer();
            _hasPower = true;
            _debugHasPower = true;
            _exportTimer = 0f;
            TryUnregister();
            ClearCableVisuals();
        }

        public void SlowTick()
        {
            RefreshCableVisuals(false);

            if (_inFlightItem != null)
            {
                AdvanceInFlightTransfer();
                return;
            }

            if (!_hasPower || sourceCrate == null || destinationCrate == null || ReferenceEquals(sourceCrate, destinationCrate))
                return;

            _exportTimer += SlowTickDeltaTime;
            if (_exportTimer < exportIntervalSeconds)
                return;

            _exportTimer = 0f;
            TryStageTransfer();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        internal void PopulateSaveData(ref ModuleDTO dto)
        {
            dto.pipeExportTimerSeconds = Mathf.Max(0f, _exportTimer);
            if (_inFlightItem == null)
                return;

            if (_activeReservationId > 0)
            {
                sourceCrate?.CommitReservation(_activeReservationId);
                _activeReservationId = 0;
                _debugReservationId = 0;
            }

            dto.pipeInFlightItemId = _inFlightItem.PersistentId;
            dto.pipeInFlightAmount = 1;

            float duration = ResolveTransitDuration();
            dto.pipeTransitProgress = duration > 0.0001f
                ? Mathf.Clamp01(1f - (_transitRemaining / duration))
                : 1f;
        }

        internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)
        {
            ClearInFlightState();
            _exportTimer = Mathf.Clamp(dto.pipeExportTimerSeconds, 0f, Mathf.Max(exportIntervalSeconds, SlowTickDeltaTime));

            if (itemCatalog == null || string.IsNullOrWhiteSpace(dto.pipeInFlightItemId) || dto.pipeInFlightAmount <= 0)
                return;

            ItemData item = itemCatalog.FindById(dto.pipeInFlightItemId);
            if (item == null)
                return;

            _inFlightItem = item;
            _transitRemaining = Mathf.Max(0f, ResolveTransitDuration() * (1f - Mathf.Clamp01(dto.pipeTransitProgress)));
            _debugInFlightItemId = item.PersistentId;
            _debugTransitRemaining = _transitRemaining;
        }

        internal bool TryExtractInFlightCargoForDeconstruct(out ItemData item, out int amount)
        {
            item = _inFlightItem;
            amount = item != null ? 1 : 0;
            if (item == null)
                return false;

            if (_activeReservationId > 0)
                sourceCrate?.CommitReservation(_activeReservationId);

            ClearInFlightState();
            return true;
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register((ISlowTickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister((ISlowTickable)this);
            _registered = false;
        }

        private void TryStageTransfer()
        {
            int reservationId = GetNextReservationId();
            ItemData item = null;

            bool reserved = filterItem != null
                ? sourceCrate.TryReserveItem(filterItem, reservationId)
                : sourceCrate.TryReserveAnyItem(reservationId, out item);

            if (filterItem != null)
                item = reserved ? filterItem : null;

            if (!reserved || item == null)
                return;

            _activeReservationId = reservationId;
            _inFlightItem = item;
            _transitRemaining = ResolveTransitDuration();
            _debugInFlightItemId = item.PersistentId;
            _debugTransitRemaining = _transitRemaining;
            _debugReservationId = reservationId;
            NotifyGridBalanceChanged();
        }

        private void AdvanceInFlightTransfer()
        {
            if (_inFlightItem == null)
                return;

            if (_transitRemaining > 0f)
            {
                _transitRemaining -= SlowTickDeltaTime;
                if (_transitRemaining > 0f)
                {
                    _debugTransitRemaining = _transitRemaining;
                    return;
                }

                _transitRemaining = 0f;
            }

            if (destinationCrate == null || !destinationCrate.HasAutomatedCapacity() || !destinationCrate.TryAddAutomatedItem(_inFlightItem))
            {
                _debugTransitRemaining = 0f;
                return;
            }

            if (_activeReservationId > 0)
                sourceCrate?.CommitReservation(_activeReservationId);
            ClearInFlightState();
            NotifyGridBalanceChanged();
        }

        private void RollbackInFlightTransfer()
        {
            if (_activeReservationId > 0)
                sourceCrate?.ReleaseReservation(_activeReservationId);

            ClearInFlightState();
        }

        private void ClearInFlightState()
        {
            _activeReservationId = 0;
            _inFlightItem = null;
            _transitRemaining = 0f;
            _debugInFlightItemId = string.Empty;
            _debugTransitRemaining = 0f;
            _debugReservationId = 0;
        }

        private float ResolveTransitDuration()
        {
            if (sourceCrate == null || destinationCrate == null)
                return SlowTickDeltaTime;

            float distance = Vector3.Distance(sourceCrate.transform.position, destinationCrate.transform.position);
            return Mathf.Max(SlowTickDeltaTime, distance / Mathf.Max(0.1f, transitSpeedMetersPerSecond));
        }

        private void RefreshCableVisuals(bool force)
        {
            if (cableRenderer == null)
                return;

            Vector3 sourcePosition = ResolveSourcePoint();
            Vector3 destinationPosition = ResolveDestinationPoint();
            bool moved = force ||
                         (sourcePosition - _lastSourcePosition).sqrMagnitude > PositionRefreshEpsilonSqr ||
                         (destinationPosition - _lastDestinationPosition).sqrMagnitude > PositionRefreshEpsilonSqr;

            if (!moved)
                return;

            _lastSourcePosition = sourcePosition;
            _lastDestinationPosition = destinationPosition;
            cableRenderer.positionCount = 2;
            cableRenderer.SetPosition(0, sourcePosition);
            cableRenderer.SetPosition(1, destinationPosition);
            cableRenderer.enabled = true;
        }

        private Vector3 ResolveSourcePoint()
        {
            if (sourceCrate != null)
                return sourceCrate.transform.position;

            return _cachedTransform != null ? _cachedTransform.position : transform.position;
        }

        private Vector3 ResolveDestinationPoint()
        {
            if (destinationCrate != null)
                return destinationCrate.transform.position;

            return _cachedTransform != null ? _cachedTransform.position : transform.position;
        }

        private void ClearCableVisuals()
        {
            if (cableRenderer == null)
                return;

            cableRenderer.positionCount = 0;
            cableRenderer.enabled = false;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.UpdateBalance();
        }

        private static int GetNextReservationId()
        {
            int nextId = s_NextReservationId++;
            if (nextId > 0)
                return nextId;

            s_NextReservationId = 1;
            return s_NextReservationId++;
        }
    }
}
