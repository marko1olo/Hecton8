using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Power;
using Unity.Mathematics;
using Hecton8.SaveSystem;
using Hecton8.World;
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
    public sealed class LogisticsPipeNode : MonoBehaviour, ISlowTickable, IPoolable, IPowerComponent, IGlobalRegistryHotSwapListener
    {
        private static int s_x001LogisticsPipeNodeSignalPushDropCount;
        private const float SlowTickDeltaTime = 0.5f;
        private const float PositionRefreshEpsilonSqr = 0.0004f;
        private const float ThermalDamageThresholdCelsius = 100f;
        private const byte MaxPayloadIntegrity = byte.MaxValue;
        private static readonly Color PipeSplineColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);

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

        [Header("── Throughput ─────────────────────────────")]
        [Tooltip("Seconds between export attempts while the pipe has power and no item is currently in transit.")]
        [SerializeField, Range(0.5f, 30f)] private float exportIntervalSeconds = 2f;

        [Tooltip("Transit speed in meters per second for staged cargo travelling through the pipe.")]
        [SerializeField, Range(0.5f, 50f)] private float transitSpeedMetersPerSecond = 8f;

        [Tooltip("Logical transport capacity used by the overpressure solver. Sustained blocked deliveries beyond this budget rupture the pipe.")]
        [SerializeField, Range(1, 8)] private int maxCapacityUnits = 1;

        [Tooltip("Stress added each SlowTick when the payload reaches the destination but the downstream crate still cannot accept it.")]
        [SerializeField, Range(0.1f, 8f)] private float blockedDeliveryStress = 1f;

        [Tooltip("Stress removed during healthy SlowTicks with no downstream blockage.")]
        [SerializeField, Range(0f, 4f)] private float stressRecoveryPerTick = 0.25f;

        [Tooltip("Pipe ruptures once accumulated overpressure stress crosses this threshold.")]
        [SerializeField, Range(0.1f, 16f)] private float ruptureStressThreshold = 3f;

        [Header("── Thermal Transit ─────────────────────")]
        [Tooltip("Ambient temperature above which in-flight cargo starts cooking inside the pipe.")]
        [SerializeField, Min(ThermalDamageThresholdCelsius)] private float thermalDamageStartCelsius = ThermalDamageThresholdCelsius;

        [Tooltip("Integrity removed from the in-flight payload per SlowTick for each Celsius above the thermal threshold.")]
        [SerializeField, Range(0.01f, 4f)] private float thermalDamagePerDegreePerTick = 0.5f;

        [Header("── Power ──────────────────────────────────")]
        [Tooltip("Continuous draw while the pipe is actively moving or staging a cargo packet.")]
        [SerializeField, Range(0f, 100f)] private float activePowerDraw = 8f;

        [Tooltip("Priority used when the power grid starts shedding non-critical logistics links.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 48;

        [Header("── Diagnostics ───────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private string _debugInFlightItemId;
        [SerializeField] private int _debugInFlightItemHashId;
        [SerializeField] private float _debugTransitRemaining;
        [SerializeField] private int _debugReservationId;
        [SerializeField] private float _debugOverpressureStress;
        [SerializeField] private bool _debugRuptured;
        [SerializeField] private byte _debugPayloadIntegrity = MaxPayloadIntegrity;
        [SerializeField] private int _debugEncodedFlowRate;

        private PowerNode _powerNode;
        private ISubmarineAtmosphereRoomReadModel _atmosphereSystem;
        private Transform _cachedTransform;
        private Transform _cachedSourceTransform;
        private Transform _cachedDestinationTransform;
        private StorageCrate _cachedSourceIdentityOwner;
        private StorageCrate _cachedDestinationIdentityOwner;
        private int _cachedSourceIdentity;
        private int _cachedDestinationIdentity;
        private uint _cachedSourceNodeId;
        private uint _cachedDestinationNodeId;
        private bool _registered;
        private bool _registeredHotSwap;
        private bool _hasSubmittedPipeLink;
        private bool _despawning;
        private bool _hasPower = true;
        private float _exportTimer;
        private int _activeReservationId;
        private long _pipeLinkId;
        private long _submittedPipeLinkId;
        private ItemData _inFlightItem;
        private int _inFlightItemHashId;
        private float _transitRemaining;
        private float _overpressureStress;
        private float _cachedPathDistanceMeters;
        private Vector3 _cachedSourcePosition;
        private Vector3 _cachedDestinationPosition;
        private Vector3 _lastSourcePosition;
        private Vector3 _lastDestinationPosition;
        private int _cachedRoomIndex = -1;
        private byte _payloadIntegrity = MaxPayloadIntegrity;
        private IFluidDecalPresentationSink _fluidDecals;
        private IPersistentDroppedItemRegistry _persistentWorldRegistry;
        private int _schedulerTopologyKey;

        public float PowerRating => _inFlightItem != null ? -activePowerDraw : 0f;
        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;
        internal StorageCrate SourceCrate => sourceCrate;
        internal StorageCrate DestinationCrate => destinationCrate;
        internal sbyte EncodedFlowRate => EncodeFlowRate(ResolveCurrentFlowUnitsPerSecond(), maxCapacityUnits);
        internal int AmbientRoomIndex => _cachedRoomIndex;
        internal bool CanEmergencyVent => !IsRuptured() && _cachedRoomIndex >= 0;
        internal int SchedulerTopologyKey => _schedulerTopologyKey;
        internal bool ParticipatesInSchedulerDag => !IsRuptured() &&
                                                    sourceCrate != null &&
                                                    destinationCrate != null &&
                                                    !ReferenceEquals(sourceCrate, destinationCrate);

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _powerNode);
            ConstructionParentLookup.TryCaptureSelfOrParent(this, out _atmosphereSystem);
            _pipeLinkId = ComposePipeLinkId(0u, unchecked((uint)EntityId.ToULong(GetEntityId())));
            CacheRegistryServicesCold();
            RefreshEndpointCache(true);
        }

        private void OnEnable()
        {
            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                ConstructionParentLookup.TryCaptureSelfOrParent(this, out _atmosphereSystem);

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            RefreshEndpointCache(true);
            RefreshCableVisuals(true);
        }

        private void OnDisable()
        {
            if (!_despawning)
                ResolveInFlightLossToWorldOrRollback(_cachedTransform != null ? _cachedTransform.position : Vector3.zero);

            _despawning = false;
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearCableVisuals();
        }

        private void OnDestroy()
        {
            ResolveInFlightLossToWorldOrRollback(_cachedTransform != null ? _cachedTransform.position : Vector3.zero);
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearCableVisuals();
        }

        public void OnSpawn()
        {
            _despawning = false;
            _hasPower = true;
            _debugHasPower = true;
            _exportTimer = 0f;
            _overpressureStress = 0f;
            _debugOverpressureStress = 0f;
            _debugRuptured = false;
            _inFlightItemHashId = 0;
            _debugInFlightItemHashId = 0;
            _payloadIntegrity = MaxPayloadIntegrity;
            _debugPayloadIntegrity = MaxPayloadIntegrity;
            _debugEncodedFlowRate = 0;
            RefreshSchedulerTopologyKey();
            if (_powerNode != null)
            {
                _powerNode.SetRuptured(false);
                _powerNode.SetShortCircuited(false);
            }
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            RefreshEndpointCache(true);
            RefreshCableVisuals(true);
        }

        public void OnDespawn()
        {
            _despawning = true;
            RollbackInFlightTransfer();
            _hasPower = true;
            _debugHasPower = true;
            _exportTimer = 0f;
            _overpressureStress = 0f;
            _debugOverpressureStress = 0f;
            _debugRuptured = false;
            _inFlightItemHashId = 0;
            _debugInFlightItemHashId = 0;
            _payloadIntegrity = MaxPayloadIntegrity;
            _debugPayloadIntegrity = MaxPayloadIntegrity;
            _debugEncodedFlowRate = 0;
            RefreshSchedulerTopologyKey();
            if (_powerNode != null)
            {
                _powerNode.SetRuptured(false);
                _powerNode.SetShortCircuited(false);
            }
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearCableVisuals();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime:
                    _fluidDecals = currentService as IFluidDecalPresentationSink;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as IPersistentDroppedItemRegistry;
                    break;
            }
        }

        public void SlowTick()
        {
            LogisticsPipeTransportScheduler.TryRunSlowTick(this);
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        internal void PopulateSaveData(ref ModuleDTO dto)
        {
            dto.pipeExportTimerSeconds = math.max(0f, _exportTimer);
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
                ? math.saturate(1f - (_transitRemaining / duration))
                : 1f;
        }

        internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)
        {
            float restoredExportTimer = math.clamp(
                dto.pipeExportTimerSeconds,
                0f,
                math.max(exportIntervalSeconds, SlowTickDeltaTime));
            bool hasSavedInFlightItem =
                dto.pipeInFlightAmount > 0 &&
                !string.IsNullOrWhiteSpace(dto.pipeInFlightItemId);

            if (!hasSavedInFlightItem)
            {
                ClearInFlightState();
                _exportTimer = restoredExportTimer;
                return;
            }

            if (itemCatalog == null)
                return;

            ItemData item = itemCatalog.FindById(dto.pipeInFlightItemId);
            if (item == null)
                return;

            ClearInFlightState();
            _exportTimer = restoredExportTimer;
            _inFlightItem = item;
            _inFlightItemHashId = ItemData.ResolvePersistentHashId(item);
            _transitRemaining = math.max(0f, ResolveTransitDuration() * (1f - math.saturate(dto.pipeTransitProgress)));
            _payloadIntegrity = MaxPayloadIntegrity;
            _debugInFlightItemId = item.PersistentId;
            _debugInFlightItemHashId = _inFlightItemHashId;
            _debugTransitRemaining = _transitRemaining;
            _debugPayloadIntegrity = _payloadIntegrity;
        }

        internal bool TryExtractInFlightCargoHashForDeconstruct(out int itemHashId, out int amount)
        {
            if (!TryPeekInFlightCargoHashForDeconstruct(out itemHashId, out amount))
                return false;

            if (_activeReservationId > 0)
                sourceCrate?.CommitReservation(_activeReservationId);

            ClearInFlightState();
            return true;
        }

        internal bool TryPeekInFlightCargoHashForDeconstruct(out int itemHashId, out int amount)
        {
            itemHashId = _inFlightItemHashId;
            amount = itemHashId != 0 ? 1 : 0;
            return itemHashId != 0;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                return;

            LogisticsPipeTransportScheduler.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            LogisticsPipeTransportScheduler.Unregister(this);
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheRegistryServicesCold()
        {
            _fluidDecals = GlobalRegistry.FluidDecalPresentation;
            _persistentWorldRegistry = GlobalRegistry.PersistentDroppedItems;
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
            _inFlightItemHashId = ItemData.ResolvePersistentHashId(item);
            _transitRemaining = ResolveTransitDuration();
            _payloadIntegrity = MaxPayloadIntegrity;
            _debugInFlightItemId = item.PersistentId;
            _debugInFlightItemHashId = _inFlightItemHashId;
            _debugTransitRemaining = _transitRemaining;
            _debugReservationId = reservationId;
            _debugPayloadIntegrity = _payloadIntegrity;
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
                AccumulateOverpressureStress();
                return;
            }

            if (_activeReservationId > 0)
                sourceCrate?.CommitReservation(_activeReservationId);
            RecoverOverpressureStress(blockageResolved: true);
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
            _inFlightItemHashId = 0;
            _transitRemaining = 0f;
            _payloadIntegrity = MaxPayloadIntegrity;
            _debugInFlightItemId = string.Empty;
            _debugInFlightItemHashId = 0;
            _debugTransitRemaining = 0f;
            _debugReservationId = 0;
            _debugPayloadIntegrity = MaxPayloadIntegrity;
            RefreshEncodedFlowRateDebug();
        }

        internal void SchedulerRefresh()
        {
            RefreshEndpointCache(false);
            RefreshCableVisuals(false);
            RefreshAmbientRoomIndex();
            RefreshEncodedFlowRateDebug();
        }

        internal void ExecuteCoordinatedSlowTick()
        {
            if (IsRuptured())
            {
                RefreshEncodedFlowRateDebug();
                return;
            }

            RecoverOverpressureStress(blockageResolved: false);

            if (_inFlightItem != null)
            {
                ApplyInFlightThermalDamage();
                if (_inFlightItem == null)
                {
                    RefreshEncodedFlowRateDebug();
                    return;
                }

                AdvanceInFlightTransfer();
                RefreshEncodedFlowRateDebug();
                return;
            }

            if (!_hasPower || sourceCrate == null || destinationCrate == null || ReferenceEquals(sourceCrate, destinationCrate))
            {
                RefreshEncodedFlowRateDebug();
                return;
            }

            _exportTimer += SlowTickDeltaTime;
            if (_exportTimer < exportIntervalSeconds)
            {
                RefreshEncodedFlowRateDebug();
                return;
            }

            _exportTimer = 0f;
            TryStageTransfer();
            RefreshEncodedFlowRateDebug();
        }

        private float ResolveTransitDuration()
        {
            RefreshEndpointCache(false);
            if (sourceCrate == null || destinationCrate == null)
                return SlowTickDeltaTime;

            return math.max(SlowTickDeltaTime, _cachedPathDistanceMeters / math.max(0.1f, transitSpeedMetersPerSecond));
        }

        private void RefreshCableVisuals(bool force)
        {
            if (sourceCrate == null || destinationCrate == null || ReferenceEquals(sourceCrate, destinationCrate))
            {
                ClearCableVisuals();
                return;
            }

            Vector3 sourcePosition = _cachedSourcePosition;
            Vector3 destinationPosition = _cachedDestinationPosition;
            long linkId = _pipeLinkId;
            if (_hasSubmittedPipeLink && _submittedPipeLinkId != linkId)
            {
                ConnectionSplineBatchRenderer.RemovePipeLink(_submittedPipeLinkId);
                _hasSubmittedPipeLink = false;
                force = true;
            }

            bool moved = force ||
                         (sourcePosition - _lastSourcePosition).sqrMagnitude > PositionRefreshEpsilonSqr ||
                         (destinationPosition - _lastDestinationPosition).sqrMagnitude > PositionRefreshEpsilonSqr;

            if (!moved)
                return;

            _lastSourcePosition = sourcePosition;
            _lastDestinationPosition = destinationPosition;
            ConnectionSplineBatchRenderer.SubmitPipeLink(linkId, sourcePosition, destinationPosition, PipeSplineColor);
            _submittedPipeLinkId = linkId;
            _hasSubmittedPipeLink = true;
        }

        private void RefreshEndpointCache(bool force)
        {
            Transform sourceTransform = sourceCrate != null ? sourceCrate.transform : _cachedTransform;
            Transform destinationTransform = destinationCrate != null ? destinationCrate.transform : _cachedTransform;
            if (force || _cachedSourceTransform != sourceTransform)
                _cachedSourceTransform = sourceTransform;

            if (force || _cachedDestinationTransform != destinationTransform)
                _cachedDestinationTransform = destinationTransform;

            if (force || !ReferenceEquals(_cachedSourceIdentityOwner, sourceCrate))
            {
                _cachedSourceIdentityOwner = sourceCrate;
                _cachedSourceNodeId = sourceCrate != null ? unchecked((uint)EntityId.ToULong(sourceCrate.GetEntityId())) : 0u;
                _cachedSourceIdentity = unchecked((int)_cachedSourceNodeId);
            }

            if (force || !ReferenceEquals(_cachedDestinationIdentityOwner, destinationCrate))
            {
                _cachedDestinationIdentityOwner = destinationCrate;
                _cachedDestinationNodeId = destinationCrate != null ? unchecked((uint)EntityId.ToULong(destinationCrate.GetEntityId())) : 0u;
                _cachedDestinationIdentity = unchecked((int)_cachedDestinationNodeId);
            }

            _cachedSourcePosition = _cachedSourceTransform != null ? _cachedSourceTransform.position : Vector3.zero;
            _cachedDestinationPosition = _cachedDestinationTransform != null ? _cachedDestinationTransform.position : _cachedSourcePosition;
            Vector3 pathDelta = _cachedSourcePosition - _cachedDestinationPosition;
            _cachedPathDistanceMeters = math.sqrt(math.max(pathDelta.sqrMagnitude, 0f));
            RefreshPipeLinkId();
            RefreshSchedulerTopologyKey();
        }

        private void RefreshPipeLinkId()
        {
            uint sourceNodeId = _cachedSourceNodeId;
            uint destinationNodeId = _cachedDestinationNodeId;
            if (sourceNodeId != 0u && destinationNodeId != 0u && sourceNodeId != destinationNodeId)
            {
                _pipeLinkId = ComposePipeLinkId(sourceNodeId, destinationNodeId);
                return;
            }

            _pipeLinkId = ComposePipeLinkId(0u, unchecked((uint)EntityId.ToULong(GetEntityId())));
        }

        private void RefreshSchedulerTopologyKey()
        {
            unchecked
            {
                int key = 17;
                key = (key * 31) + _cachedSourceIdentity;
                key = (key * 31) + _cachedDestinationIdentity;
                key = (key * 31) + (IsRuptured() ? 1 : 0);
                _schedulerTopologyKey = key;
            }
        }

        private void RefreshAmbientRoomIndex()
        {
            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                return;

            Vector3 midpoint = (_cachedSourcePosition + _cachedDestinationPosition) * 0.5f;
            _cachedRoomIndex = _atmosphereSystem.ResolveNearestRoomIndexForWorldPosition(midpoint);
        }

        internal int ResolveAmbientRoomIndex()
        {
            RefreshEndpointCache(force: false);
            RefreshAmbientRoomIndex();
            return _cachedRoomIndex;
        }

        private void ApplyInFlightThermalDamage()
        {
            if (_inFlightItem == null || _payloadIntegrity == 0 || _atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive || _cachedRoomIndex < 0)
                return;

            float thresholdTemperature = math.max(ThermalDamageThresholdCelsius, thermalDamageStartCelsius);
            float roomTemperature = _atmosphereSystem.GetRoomTemperatureCelsius(_cachedRoomIndex);
            if (roomTemperature <= thresholdTemperature)
                return;

            float overshootCelsius = roomTemperature - thresholdTemperature;
            int thermalDamage = (int)math.ceil(math.max(1f, overshootCelsius * math.max(0.01f, thermalDamagePerDegreePerTick)));
            _payloadIntegrity = (byte)math.max(0, _payloadIntegrity - thermalDamage);
            _debugPayloadIntegrity = _payloadIntegrity;
            if (_payloadIntegrity > 0)
                return;

            DestroyInFlightPayloadByHeat();
        }

        private void DestroyInFlightPayloadByHeat()
        {
            if (_activeReservationId > 0)
                sourceCrate?.CommitReservation(_activeReservationId);

            ClearInFlightState();
            NotifyGridBalanceChanged();
        }

        private void ClearCableVisuals()
        {
            if (!_hasSubmittedPipeLink)
                return;

            ConnectionSplineBatchRenderer.RemovePipeLink(_submittedPipeLinkId);
            _submittedPipeLinkId = 0L;
            _hasSubmittedPipeLink = false;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private bool IsRuptured()
        {
            return _debugRuptured || (_powerNode != null && _powerNode.IsRuptured);
        }

        private void RecoverOverpressureStress(bool blockageResolved)
        {
            if (_overpressureStress <= 0f)
                return;

            float recovery = blockageResolved
                ? math.max(stressRecoveryPerTick, blockedDeliveryStress)
                : math.max(0f, stressRecoveryPerTick);
            if (recovery <= 0f)
                return;

            _overpressureStress = math.max(0f, _overpressureStress - recovery);
            _debugOverpressureStress = _overpressureStress;
        }

        private void AccumulateOverpressureStress()
        {
            float capacityScale = math.max(1, maxCapacityUnits);
            _overpressureStress += math.max(0.1f, blockedDeliveryStress) / capacityScale;
            _debugOverpressureStress = _overpressureStress;
            if (_overpressureStress < math.max(0.1f, ruptureStressThreshold))
                return;

            TriggerOverpressureRupture();
        }

        private void TriggerOverpressureRupture()
        {
            if (IsRuptured())
                return;

            _debugRuptured = true;

            if (_powerNode != null)
            {
                _powerNode.SetRuptured(true);
                _powerNode.SetShortCircuited(true);
            }
            RefreshSchedulerTopologyKey();

            Vector3 rupturePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            PublishRuptureSignals(rupturePosition);

            ResolveInFlightLossToWorldOrRollback(rupturePosition);
            NotifyGridBalanceChanged();
        }

        private void PublishRuptureSignals(Vector3 rupturePosition)
        {
            float ruptureSeverity = math.saturate(_overpressureStress / math.max(0.1f, ruptureStressThreshold));
            DecodePipeLinkId(_pipeLinkId, out uint leftNodeId, out uint rightNodeId);
            if (leftNodeId != 0u)
                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(leftNodeId, true);

            if (rightNodeId != 0u && rightNodeId != leftNodeId)
                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(rightNodeId, true);

            if (!TryResolveAupFromRuntimeOrigin(rupturePosition, out AbsoluteUniversePosition ruptureAup))
                return;

            PipeRuptureSignal ruptureSignal = new PipeRuptureSignal
            {
                RuptureAup = ruptureAup,
                NetworkId = 0u,
                NodeId = rightNodeId != 0u ? rightNodeId : leftNodeId,
                PressureKPa = _overpressureStress,
                ContentKind = 0,
                Flags = 1,
                RoomIndex = (short)math.clamp(_cachedRoomIndex, short.MinValue, short.MaxValue)
            };
            SignalBus<PipeRuptureSignal>.TryPushTracked(in ruptureSignal, ref s_x001LogisticsPipeNodeSignalPushDropCount);

            ImpactSignal impactSignal = new ImpactSignal
            {
                PointAup = ruptureAup,
                Force = _overpressureStress,
                Intensity = ruptureSeverity,
                MaterialHash = 0x50495045u,
                WeightClass = 1,
                Flags = 1
            };
            SignalBus<ImpactSignal>.TryPushTracked(in impactSignal, ref s_x001LogisticsPipeNodeSignalPushDropCount);
        }

        internal void TriggerExternalRupture()
        {
            TriggerOverpressureRupture();
        }

        internal Vector3 ResolveVentRuntimePosition()
        {
            RefreshEndpointCache(force: false);
            return (_cachedSourcePosition + _cachedDestinationPosition) * 0.5f;
        }

        internal Vector3 ResolveVentDirection(Vector3 vesselCenter)
        {
            Vector3 ventPosition = ResolveVentRuntimePosition();
            Vector3 outward = ventPosition - vesselCenter;
            if (outward.sqrMagnitude <= 0.0001f)
                outward = _cachedTransform != null ? _cachedTransform.right : Vector3.right;

            return FastDirectionOrFallback(outward, Vector3.right);
        }

        private static Vector3 FastDirectionOrFallback(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || float.IsNaN(lengthSq) || float.IsInfinity(lengthSq))
                return fallback.sqrMagnitude > 0.000001f ? fallback : Vector3.right;

            float invLength = math.rsqrt(lengthSq);
            return new Vector3(value.x * invLength, value.y * invLength, value.z * invLength);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return positionAup.IsFinite();
        }

        internal void RegisterEmergencyVentVisual(float normalizedIntensity)
        {
            IFluidDecalPresentationSink fluidDecals = _fluidDecals;
            if (fluidDecals == null)
                return;

            float radiusScale = math.clamp(normalizedIntensity, 0.1f, 1f);
            fluidDecals.RegisterRuptureFluid(ResolveVentRuntimePosition(), radiusScale);
        }

        private void ResolveInFlightLossToWorldOrRollback(Vector3 spillPosition)
        {
            if (TrySpillInFlightItemToWorld(spillPosition))
                return;

            if (TryReturnCommittedInFlightItemToSource())
                return;

            RollbackInFlightTransfer();
        }

        private bool TryReturnCommittedInFlightItemToSource()
        {
            if (_inFlightItem == null ||
                _activeReservationId > 0 ||
                sourceCrate == null ||
                !sourceCrate.HasAutomatedCapacity())
            {
                return false;
            }

            if (!sourceCrate.TryAddAutomatedItem(_inFlightItem))
                return false;

            ClearInFlightState();
            NotifyGridBalanceChanged();
            return true;
        }

        private bool TrySpillInFlightItemToWorld(Vector3 spillPosition)
        {
            if (_inFlightItem == null)
                return false;

            IPersistentDroppedItemRegistry persistentWorldRegistry = _persistentWorldRegistry;
            if (persistentWorldRegistry == null)
                return false;

            if (!persistentWorldRegistry.TryRegisterDroppedItem(_inFlightItem, 1, spillPosition))
                return false;

            if (_activeReservationId > 0)
                sourceCrate?.CommitReservation(_activeReservationId);

            ClearInFlightState();
            return true;
        }

        private static int GetNextReservationId()
        {
            int nextId = s_NextReservationId++;
            if (nextId > 0)
                return nextId;

            s_NextReservationId = 1;
            return s_NextReservationId++;
        }

        private float ResolveCurrentFlowUnitsPerSecond()
        {
            if (IsRuptured() || _inFlightItem == null)
                return 0f;

            return 1f / math.max(SlowTickDeltaTime, ResolveTransitDuration());
        }

        private void RefreshEncodedFlowRateDebug()
        {
            _debugEncodedFlowRate = EncodedFlowRate;
        }

        internal static sbyte EncodeFlowRate(float flowUnitsPerSecond, int capacityUnits)
        {
            float safeCapacity = math.max(1f, capacityUnits);
            float normalizedFlow = math.clamp(flowUnitsPerSecond / safeCapacity, -1f, 1f);
            int encodedFlow = (int)math.round(normalizedFlow * 127f);
            encodedFlow = math.clamp(encodedFlow, -127, 127);
            return (sbyte)encodedFlow;
        }

        private static long ComposePipeLinkId(uint left, uint right)
        {
            uint min = math.min(left, right);
            uint max = math.max(left, right);
            return ((long)min << 32) | max;
        }

        private static void DecodePipeLinkId(long linkId, out uint leftNodeId, out uint rightNodeId)
        {
            leftNodeId = (uint)(linkId >> 32);
            rightNodeId = unchecked((uint)linkId);
        }
    }
}
