using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay.Loot.Contracts;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Gameplay.Loot
{
    /// <summary>
    /// Burst-backed loot magnet scheduler that keeps acquisition truth in AUP-space vault buffers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootMagnetSystem : MonoBehaviour, IFastTickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int _signalPushDropCount;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_ITEM_MAGNET_SOLVER.bin";
        private const string RuntimeObjectName = "[LootMagnetSystem]";
        private const uint PostSimulationSystemHash = 0x4C4D5053u; // LMPS
        private const uint TelemetryFaultFlag = 1u;
        private const uint TelemetryAcousticBudgetDropFlag = 1u << 1;
        private const uint TelemetryWakeBudgetDropFlag = 1u << 2;
        private const uint TelemetryInventoryMissingFlag = 1u << 3;
        private const uint TelemetryAcquisitionBudgetDeferFlag = 1u << 4;
        private const uint TelemetryPlayerPoseMissingFlag = 1u << 5;
        private const uint TelemetryVaultUnavailableFlag = 1u << 6;
        private const uint TelemetryPickupRegistrySaturatedFlag = 1u << 7;
        private const uint TelemetryPickupPoseNonFiniteFlag = 1u << 8;
        private const uint TelemetryPlayerPoseNonFiniteFlag = 1u << 9;
        private const uint TelemetryPickupProxyInvalidFlag = 1u << 10;
        private const uint TelemetryAuthoringClampFlag = 1u << 11;
        private const uint TelemetryStressRadiusClampFlag = 1u << 12;
        private const uint TelemetrySignalNonFiniteFlag = 1u << 13;
        private const uint TelemetryDeathCacheSaturatedFlag = 1u << 14;
        private const uint TelemetryDeathCacheNonFiniteFlag = 1u << 15;
        private const uint TelemetryDeathCacheDeferredFlag = 1u << 16;
        private const uint TelemetryDeathCacheRequeueRejectedFlag = 1u << 17;
        private const uint TelemetryAcousticSignalRejectedFlag = 1u << 18;
        private const uint TelemetryWakeSignalRejectedFlag = 1u << 19;
        private const uint TelemetryFluidImpulseSignalRejectedFlag = 1u << 20;
        private const uint TelemetryItemAcquiredSignalRejectedFlag = 1u << 21;
        private const uint TelemetryDebrisSignalRejectedFlag = 1u << 22;
        private const ushort DefaultRecoveredItemQualityMilli = 1000;
        private const uint TelemetryDumpMagic = 0x48384C4Du;
        private const uint TelemetryDumpVersion = 7u;
        private const uint TelemetryHashOffset = 2166136261u;
        private const uint TelemetryHashPrime = 16777619u;
        private const int TelemetryDumpFileBufferBytes = 64 * 1024;
        private const float QualityWeightHysteresisEpsilon = 0.01f;
        private const float FluidImpulseMinimumQuality01 = 0.25f;
        private static readonly ulong JobMutationGuardMask =
            MutationGuardBit(BufferID.EntityAUPs) |
            MutationGuardBit(BufferID.EntityFlags) |
            MutationGuardBit(BufferID.EntityVelocities) |
            MutationGuardBit(BufferID.EntityItemHashes) |
            MutationGuardBit(BufferID.EntityQuantities) |
            MutationGuardBit(BufferID.EntityLootMagnetSignalEvents) |
            MutationGuardBit(BufferID.EntityLootMagnetTelemetry);

        private static LootMagnetSystem _bootstrapRuntime;
        private static bool _sceneLoadedHooked;

        [Header("Pull")]
        [Tooltip("Maximum pickup proxies mirrored into loot vault buffers. Clamped again at runtime.")]
        [SerializeField, Range(1, LootMagnetConstants.MaxEntitiesHardCap)] private int maxLootEntities = LootMagnetConstants.DefaultMaxEntities;
        [Tooltip("Magnet acquisition radius in meters. Values are sanitized before Burst scheduling.")]
        [SerializeField, Range(LootMagnetConstants.AcquireDistanceMeters, LootMagnetConstants.MaxStablePullRadiusMeters)] private float pullRadiusMeters = LootMagnetConstants.DefaultPullRadiusMeters;
        [Tooltip("AUP-space pull acceleration scalar applied by the Burst job.")]
        [SerializeField, Range(0f, LootMagnetConstants.MaxStablePullStrength)] private float pullStrength = LootMagnetConstants.DefaultPullStrength;
        [Tooltip("Maximum loot velocity applied by the Burst job in meters per second.")]
        [SerializeField, Range(0.01f, LootMagnetConstants.MaxStableVelocityMetersPerSecond)] private float maxVelocityMetersPerSecond = LootMagnetConstants.DefaultMaxVelocityMetersPerSecond;

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private IPlayerInventoryService _inventoryService;
        private PlayerInventory _inventory;
        private Transform _playerTransform;

        // COLD ALLOC: managed sidecars mirror vault slots only for legacy visual proxy/inventory commit.
        private PickupItem[] _pickupRefs;
        private ulong[] _pickupEntityIds;
        private InventoryDeathLootCacheSignal[] _pendingDeathCacheAcquisitions;
        private LootMagnetSignalEvent[] _pendingDeathCacheAcquisitionEvents;
        private LootMagnetSignalEvent[] _pendingPresentationEvents;
        private ushort[] _pendingPresentationQuantities;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private JobHandle _pullHandle;
        private bool _pullScheduled;
        private bool _scheduledVaultGuardHeld;
        private bool _registeredFastTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredPostSimulationDispatcher;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
        private bool _dumpedFault;
        private int _activeCount;
        private int _scheduledCount;
        private int _scheduledCapacity;
        private int _telemetryIndex;
        private uint _telemetryFrameCounter;
        private uint _lastTelemetryRecordedFrame;
        private uint _frameCounter;
        private float _qualityWeight01;
        private float _pendingQualityWeight01;
        private byte _pendingQualityWeightTicks;
        private bool _qualityWeightInitialized;
        private AbsoluteUniversePosition _lastPlayerAup;
        private float _scheduledPullRadiusMeters;
        private float _scheduledPullRadiusSq;
        private uint _dependencyTelemetryFlags;
        private uint _registryTelemetryFlags;
        private uint _registryFlagsHash;
        private uint _lastActiveLootPullsCount;
        private float _lastPeakMagnetVelocity;
        private IDataVault _scheduledVaultGuardVault;
        private int _pendingDeathCacheAcquisitionCount;
        private int _pendingPresentationCount;
        private uint _slowPhaseAcquiredCountPendingTelemetry;

        private int Capacity => math.clamp(maxLootEntities, 1, LootMagnetConstants.MaxEntitiesHardCap);

        private int PendingAcquisitionCount => _pendingDeathCacheAcquisitionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBootstrapState()
        {
            if (_sceneLoadedHooked)
                SceneManager.sceneLoaded -= HandleSceneLoaded;

            _bootstrapRuntime = null;
            _sceneLoadedHooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstalled()
        {
            EnsureSceneLoadedHook();
            if (_bootstrapRuntime != null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameObject runtimeRoot = new GameObject(RuntimeObjectName); // COLD ALLOC: GameObject[1] - scene-owned loot magnet scheduler - owner: LootMagnetSystem
            _bootstrapRuntime = runtimeRoot.AddComponent<LootMagnetSystem>();
#endif
        }

        private static void EnsureSceneLoadedHook()
        {
            if (_sceneLoadedHooked)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneLoadedHooked = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureRuntimeInstalled();
        }

        private void Awake()
        {
            if (_bootstrapRuntime != null && !ReferenceEquals(_bootstrapRuntime, this))
            {
                enabled = false;
                return;
            }

            _bootstrapRuntime = this;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureManagedSidecars();
            TryRegisterHotSwapListener();
            RefreshDependenciesCold();
            if (TryResolveVaultViews(out LootMagnetVaultViews views, Capacity, allowAllocate: true))
                RefreshPickupVaultFromRegistry(views);
            else
                return;

            TryRegisterDispatcherPhases();
            if (!_registeredPostSimulationDispatcher)
                return;

            TryRegisterTicks();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            ForceCompleteAndCommitScheduledJobForBarrier();
            TryUnregisterDispatcherPhases();
            TryUnregisterTicks();
            TryUnregisterOriginShiftListener();
            ClearDataVaultRuntimeState();
            TryUnregisterHotSwapListener();
            ClearCachedDependencies();
        }

        private void OnDestroy()
        {
            TryUnregisterOriginShiftListener();
            TryUnregisterDispatcherPhases();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(_bootstrapRuntime, this))
                _bootstrapRuntime = null;
        }

        /// <inheritdoc />
        public void FastTick(float dt)
        {
            if (!_registeredPostSimulationDispatcher || _pullScheduled || _activeCount <= 0 || _inventory == null)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            SchedulePull(dt, playerAup);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            RefreshDependencySnapshotsFromCachedOwners();
            ApplyPendingAcquisitions();
            if (_pullScheduled)
            {
                RequeueInventoryDeathLootCacheSignalsWhileScheduled();
                return;
            }

            if (_inventory == null)
                return;

            TryDrainInventoryDeathLootCacheSignals();

            if (!TryResolveVaultViews(out LootMagnetVaultViews views, Capacity, allowAllocate: false))
                return;

            RefreshPickupVaultFromRegistry(views);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            uint presentationFlags = FlushPendingPresentationSignals();
            presentationFlags |= FlushPullPresentationFromVault();
            _lastCommittedFlags |= presentationFlags;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            _telemetryFrameCounter = timing.FrameId != 0u ? timing.FrameId : _telemetryFrameCounter + 1u;
            TryResolvePlayerAup(out _);

            uint postSimulationFlags = CompleteScheduledPullInPostSimulation();
            ApplyPendingAcquisitions();

            _lastCommittedAcquiredCount = _slowPhaseAcquiredCountPendingTelemetry;
            _slowPhaseAcquiredCountPendingTelemetry = 0u;
            _lastCommittedFlags = postSimulationFlags |
                                  _dependencyTelemetryFlags |
                                  _registryTelemetryFlags |
                                  (_inventory == null ? TelemetryInventoryMissingFlag : 0u);

            if (_lastTelemetryRecordedFrame != _telemetryFrameCounter)
                RecordTelemetry(_telemetryFrameCounter);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);
            float shiftSqrMagnitude = math.lengthsq(shiftOffset);
            if (!IsFiniteFloat3(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                _dependencyTelemetryFlags |= TelemetryPlayerPoseNonFiniteFlag;
                return;
            }

            if (shiftSqrMagnitude <= 0.000001f)
                return;

            ForceCompleteAndCommitScheduledJobForBarrier();
            if (TryResolveVaultViews(out LootMagnetVaultViews rebaseViews, _activeCount, allowAllocate: false))
                ReapplyPulledProxyRuntimePoses(in rebaseViews);
        }

        private void TryRegisterTicks()
        {
            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);

            if (!_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTicks()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _registeredFastTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrameTick = false;
            }
        }

        private void TryRegisterDispatcherPhases()
        {
            if (_registeredPostSimulationDispatcher)
                return;

            if (_postSimulationPhase == null)
                _postSimulationPhase = new PostSimulationPhaseSystem(this); // COLD ALLOC: IDispatcherSystem[1] - loot magnet post-simulation fence bridge - owner: LootMagnetSystem

            _registeredPostSimulationDispatcher = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
        }

        private void TryUnregisterDispatcherPhases()
        {
            if (!_registeredPostSimulationDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
            _registeredPostSimulationDispatcher = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = true;
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    RefreshDependencySnapshotsFromCachedOwners();
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _inventoryService = currentService as IPlayerInventoryService;
                    RefreshDependencySnapshotsFromCachedOwners();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterTicks();
                    TryUnregisterOriginShiftListener();
                    TryUnregisterDispatcherPhases();
                    TryRegisterDispatcherPhases();
                    if (_registeredPostSimulationDispatcher)
                    {
                        TryRegisterTicks();
                        TryRegisterOriginShiftListener();
                    }
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void RefreshDependenciesCold()
        {
            _vault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
            _inventoryService = GlobalRegistry.PlayerInventory;
            RefreshDependencySnapshotsFromCachedOwners();
        }

        private void RefreshDependencySnapshotsFromCachedOwners()
        {
            _inventory = _playerContext != null && _playerContext.Inventory != null
                ? _playerContext.Inventory
                : (_inventoryService != null ? _inventoryService.Inventory : null);
            Transform contextTransform = _playerContext != null ? _playerContext.PlayerTransform : null;
            _playerTransform = contextTransform != null
                ? contextTransform
                : (_inventory != null ? _inventory.transform : null);
            _dependencyTelemetryFlags = 0u;
            if (_vault == null)
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;

            if (_inventory == null)
                _dependencyTelemetryFlags |= TelemetryInventoryMissingFlag;

            if (_playerContext == null && _playerTransform == null)
                _dependencyTelemetryFlags |= TelemetryPlayerPoseMissingFlag;

            RefreshQualityWeight();
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
            {
                RefreshDependencySnapshotsFromCachedOwners();
                return;
            }

            ForceCompleteAndCommitScheduledJobForBarrier();
            ClearDataVaultRuntimeState();
            _vault = vault;
            RefreshDependencySnapshotsFromCachedOwners();
            if (_vault != null &&
                isActiveAndEnabled &&
                TryResolveVaultViews(out LootMagnetVaultViews views, Capacity, allowAllocate: true))
            {
                RefreshPickupVaultFromRegistry(views);
            }
        }

        private void ClearCachedDependencies()
        {
            _vault = null;
            _playerContext = null;
            _inventoryService = null;
            _inventory = null;
            _playerTransform = null;
            _dependencyTelemetryFlags = TelemetryVaultUnavailableFlag |
                                        TelemetryInventoryMissingFlag |
                                        TelemetryPlayerPoseMissingFlag;
        }

        private void RefreshQualityWeight()
        {
            float requestedWeight = ResolveLootQualityWeight01();
            if (!_qualityWeightInitialized)
            {
                _qualityWeight01 = requestedWeight;
                _pendingQualityWeight01 = requestedWeight;
                _pendingQualityWeightTicks = 0;
                _qualityWeightInitialized = true;
                return;
            }

            if (math.abs(requestedWeight - _qualityWeight01) <= QualityWeightHysteresisEpsilon)
            {
                _pendingQualityWeight01 = requestedWeight;
                _pendingQualityWeightTicks = 0;
                return;
            }

            if (math.abs(requestedWeight - _pendingQualityWeight01) > QualityWeightHysteresisEpsilon)
            {
                _pendingQualityWeight01 = requestedWeight;
                _pendingQualityWeightTicks = 1;
                return;
            }

            if (_pendingQualityWeightTicks < LootMagnetConstants.ScalabilityTierHysteresisSlowTicks)
            {
                _pendingQualityWeightTicks++;
                return;
            }

            _qualityWeight01 = requestedWeight;
            _pendingQualityWeightTicks = 0;
        }

        private bool TryResolveVaultViews(out LootMagnetVaultViews views, int requiredCapacity, bool allowAllocate)
        {
            views = default;
            IDataVault vault = _vault;
            if (vault == null)
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return false;
            }

            int capacity = math.clamp(
                allowAllocate ? math.max(requiredCapacity, Capacity) : math.max(requiredCapacity, 1),
                1,
                LootMagnetConstants.MaxEntitiesHardCap);
            if (allowAllocate)
            {
                if (!AreManagedSidecarsReady())
                {
                    _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                    return false;
                }

                if (!TryResolveVaultView(vault, BufferID.EntityAUPs, capacity, allowAllocate, out views.EntityAups) ||
                    !TryResolveVaultView(vault, BufferID.EntityFlags, capacity, allowAllocate, out views.EntityFlags) ||
                    !TryResolveVaultView(vault, BufferID.EntityVelocities, capacity, allowAllocate, out views.EntityVelocities) ||
                    !TryResolveVaultView(vault, BufferID.EntityItemHashes, capacity, allowAllocate, out views.EntityItemHashes) ||
                    !TryResolveVaultView(vault, BufferID.EntityQuantities, capacity, allowAllocate, out views.EntityQuantities) ||
                    !TryResolveVaultView(vault, BufferID.EntityLootMagnetSignalEvents, capacity, allowAllocate, out views.SignalEvents) ||
                    !TryResolveVaultView(vault, BufferID.EntityLootMagnetTelemetry, LootMagnetConstants.TelemetryFrameCount, allowAllocate, out views.Telemetry))
                {
                    _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                    return false;
                }
            }
            else if (!TryReadExistingVaultViews(vault, capacity, out views))
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return false;
            }

            if (!LootMagnetVaultViews.IsCreated(in views) ||
                views.Telemetry.Length < LootMagnetConstants.TelemetryFrameCount)
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return false;
            }

            if (allowAllocate && ResolveWritableCapacity(in views) < requiredCapacity)
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return false;
            }

            if (!allowAllocate && !ExistingVaultViewsCover(in views, requiredCapacity))
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return false;
            }

            return true;
        }

        private bool AreManagedSidecarsReady()
        {
            int capacity = Capacity;
            return _pickupRefs != null &&
                   _pickupRefs.Length == capacity &&
                   _pickupEntityIds != null &&
                   _pickupEntityIds.Length == capacity &&
                   _pendingDeathCacheAcquisitions != null &&
                   _pendingDeathCacheAcquisitions.Length == LootMagnetConstants.MaxAcquisitionsPerFrame &&
                   _pendingDeathCacheAcquisitionEvents != null &&
                   _pendingDeathCacheAcquisitionEvents.Length == LootMagnetConstants.MaxAcquisitionsPerFrame &&
                   _pendingPresentationEvents != null &&
                   _pendingPresentationEvents.Length == LootMagnetConstants.MaxAcquisitionsPerFrame &&
                   _pendingPresentationQuantities != null &&
                   _pendingPresentationQuantities.Length == LootMagnetConstants.MaxAcquisitionsPerFrame;
        }

        private static bool TryReadExistingVaultViews(IDataVault vault, int requiredCapacity, out LootMagnetVaultViews views)
        {
            views = default;
            return TryResolveVaultView(vault, BufferID.EntityAUPs, requiredCapacity, allowAllocate: false, out views.EntityAups) &&
                   TryResolveVaultView(vault, BufferID.EntityFlags, requiredCapacity, allowAllocate: false, out views.EntityFlags) &&
                   TryResolveVaultView(vault, BufferID.EntityVelocities, requiredCapacity, allowAllocate: false, out views.EntityVelocities) &&
                   TryResolveVaultView(vault, BufferID.EntityItemHashes, requiredCapacity, allowAllocate: false, out views.EntityItemHashes) &&
                   TryResolveVaultView(vault, BufferID.EntityQuantities, requiredCapacity, allowAllocate: false, out views.EntityQuantities) &&
                   TryResolveVaultView(vault, BufferID.EntityLootMagnetSignalEvents, requiredCapacity, allowAllocate: false, out views.SignalEvents) &&
                   TryResolveVaultView(vault, BufferID.EntityLootMagnetTelemetry, LootMagnetConstants.TelemetryFrameCount, allowAllocate: false, out views.Telemetry);
        }

        private static bool TryResolveVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            bool allowAllocate,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            VaultGenerationHandle<T> handle = allowAllocate
                ? vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.GameplayLoot,
                    NativeArrayOptions.ClearMemory)
                : default;
            if (!allowAllocate && !vault.TryGetGenerationHandle(bufferId, out handle))
                return false;

            return IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool ExistingVaultViewsCover(in LootMagnetVaultViews views, int requiredCapacity)
        {
            return requiredCapacity <= 0 ||
                   (views.EntityAups.Length >= requiredCapacity &&
                    views.EntityFlags.Length >= requiredCapacity &&
                    views.EntityVelocities.Length >= requiredCapacity &&
                    views.EntityItemHashes.Length >= requiredCapacity &&
                    views.EntityQuantities.Length >= requiredCapacity &&
                    views.SignalEvents.Length >= requiredCapacity);
        }

        private int ResolveWritableCapacity(in LootMagnetVaultViews views)
        {
            if (!LootMagnetVaultViews.IsCreated(in views) ||
                _pickupRefs == null ||
                _pickupEntityIds == null)
            {
                return 0;
            }

            int capacity = Capacity;
            capacity = math.min(capacity, views.EntityAups.Length);
            capacity = math.min(capacity, views.EntityFlags.Length);
            capacity = math.min(capacity, views.EntityVelocities.Length);
            capacity = math.min(capacity, views.EntityItemHashes.Length);
            capacity = math.min(capacity, views.EntityQuantities.Length);
            capacity = math.min(capacity, views.SignalEvents.Length);
            capacity = math.min(capacity, _pickupRefs.Length);
            capacity = math.min(capacity, _pickupEntityIds.Length);
            return capacity;
        }

        private void EnsureManagedSidecars()
        {
            int capacity = Capacity;
            bool pickupSidecarsValid = _pickupRefs != null &&
                                       _pickupRefs.Length == capacity &&
                                       _pickupEntityIds != null &&
                                       _pickupEntityIds.Length == capacity;
            bool pendingSidecarsValid = _pendingDeathCacheAcquisitions != null &&
                                        _pendingDeathCacheAcquisitions.Length == LootMagnetConstants.MaxAcquisitionsPerFrame &&
                                        _pendingDeathCacheAcquisitionEvents != null &&
                                        _pendingDeathCacheAcquisitionEvents.Length == LootMagnetConstants.MaxAcquisitionsPerFrame &&
                                        _pendingPresentationEvents != null &&
                                        _pendingPresentationEvents.Length == LootMagnetConstants.MaxAcquisitionsPerFrame &&
                                        _pendingPresentationQuantities != null &&
                                        _pendingPresentationQuantities.Length == LootMagnetConstants.MaxAcquisitionsPerFrame;
            if (pickupSidecarsValid && pendingSidecarsValid)
            {
                return;
            }

            if (!pickupSidecarsValid && (_pickupRefs != null || _pickupEntityIds != null))
                ClearRuntimeVaultState();

            if (!pickupSidecarsValid)
            {
                _pickupRefs = new PickupItem[capacity]; // COLD ALLOC: PickupItem[capacity] - managed pickup sidecar for vault commit - owner: LootMagnetSystem
                _pickupEntityIds = new ulong[capacity]; // COLD ALLOC: ulong[capacity] - pickup entity identity sidecar - owner: LootMagnetSystem
            }

            if (!pendingSidecarsValid)
            {
                ClearPendingAcquisitionQueues(requeueDeathCache: true);
                _pendingDeathCacheAcquisitions = new InventoryDeathLootCacheSignal[LootMagnetConstants.MaxAcquisitionsPerFrame]; // COLD ALLOC: pending death-cache acquisitions - owner: LootMagnetSystem
                _pendingDeathCacheAcquisitionEvents = new LootMagnetSignalEvent[LootMagnetConstants.MaxAcquisitionsPerFrame]; // COLD ALLOC: pending death-cache visual DTOs - owner: LootMagnetSystem
                _pendingPresentationEvents = new LootMagnetSignalEvent[LootMagnetConstants.MaxAcquisitionsPerFrame]; // COLD ALLOC: late-frame acquisition presentation queue - owner: LootMagnetSystem
                _pendingPresentationQuantities = new ushort[LootMagnetConstants.MaxAcquisitionsPerFrame]; // COLD ALLOC: late-frame acquisition visual quantities - owner: LootMagnetSystem
                _pendingDeathCacheAcquisitionCount = 0;
                _pendingPresentationCount = 0;
            }
        }

        private void RefreshPickupVaultFromRegistry(LootMagnetVaultViews views)
        {
            if (_pullScheduled)
                return;

            _registryTelemetryFlags = 0u;
            if (!LootMagnetVaultViews.IsCreated(in views) || _pickupRefs == null || _pickupEntityIds == null)
            {
                ClearRuntimeVaultState();
                return;
            }

            int capacity = ResolveWritableCapacity(in views);
            if (capacity <= 0)
            {
                ClearRuntimeVaultState();
                return;
            }

            int previousActiveCount = math.clamp(_activeCount, 0, capacity);
            int highestDataOnlyIndex = -1;
            for (int index = 0; index < previousActiveCount; index++)
            {
                if (IsDataOnlyDeathCacheSlot(in views, index))
                {
                    _pickupRefs[index] = null;
                    _pickupEntityIds[index] = 0UL;
                    highestDataOnlyIndex = index;
                    continue;
                }

                ClearVaultSlot(views, index);
            }

            int registryCount = PickupItem.WorldStateRegistryCount;
            _registryTelemetryFlags = registryCount >= capacity ? TelemetryPickupRegistrySaturatedFlag : 0u;
            uint registryFlagsHash = TelemetryHashOffset;
            int activeCount = 0;
            int writeIndex = 0;
            for (int registryIndex = 0; registryIndex < registryCount && writeIndex < capacity; registryIndex++)
            {
                PickupItem pickup = PickupItem.GetWorldStateRegistryAt(registryIndex);
                if (pickup == null ||
                    !pickup.isActiveAndEnabled ||
                    pickup.Quantity <= 0 ||
                    pickup.ItemHashId == 0)
                {
                    continue;
                }

                Transform pickupTransform = pickup.transform;
                Vector3 pickupPosition = pickupTransform.position;
                if (!IsFiniteRuntimePosition(pickupPosition) ||
                    !TryBuildFiniteAup(pickupPosition, out AbsoluteUniversePosition pickupAup))
                {
                    _registryTelemetryFlags |= TelemetryPickupPoseNonFiniteFlag;
                    continue;
                }

                ulong entityId = EntityId.ToULong(pickup.GetEntityId());
                uint itemHash = unchecked((uint)pickup.ItemHashId);
                const uint slotFlags = LootEntityFlags.Active | LootEntityFlags.IsLoot | LootEntityFlags.Bit_IsMagnetic;
                while (writeIndex < capacity && IsDataOnlyDeathCacheSlot(in views, writeIndex))
                {
                    highestDataOnlyIndex = math.max(highestDataOnlyIndex, writeIndex);
                    writeIndex++;
                }

                if (writeIndex >= capacity)
                    break;

                _pickupRefs[writeIndex] = pickup;
                _pickupEntityIds[writeIndex] = entityId;
                views.EntityAups[writeIndex] = pickupAup;
                views.EntityItemHashes[writeIndex] = itemHash;
                views.EntityQuantities[writeIndex] = (ushort)math.clamp(pickup.Quantity, 1, (int)ushort.MaxValue);
                views.EntityFlags[writeIndex] = slotFlags;
                registryFlagsHash = FoldEntityIdHash(
                    FoldTelemetryHash(FoldTelemetryHash(registryFlagsHash, slotFlags), itemHash),
                    entityId);
                activeCount = math.max(activeCount, writeIndex + 1);
                writeIndex++;
            }

            _activeCount = math.max(activeCount, highestDataOnlyIndex + 1);
            _registryFlagsHash = activeCount > 0 ? registryFlagsHash : 0u;
            _lastCommittedFlagsHash = _registryFlagsHash;
        }

        private void TryDrainInventoryDeathLootCacheSignals()
        {
            if (_pullScheduled)
            {
                RequeueInventoryDeathLootCacheSignalsWhileScheduled();
                return;
            }

            ReadOnlySpan<InventoryDeathLootCacheSignal> signals = SignalBus<InventoryDeathLootCacheSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            int requiredCapacity = math.clamp(_activeCount + signals.Length, 1, Capacity);
            if (!TryResolveVaultViews(out LootMagnetVaultViews views, requiredCapacity, allowAllocate: false))
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                RequeueInventoryDeathLootCacheSignals(signals);
                return;
            }

            DrainInventoryDeathLootCacheSignals(signals, views);
        }

        private void RequeueInventoryDeathLootCacheSignalsWhileScheduled()
        {
            ReadOnlySpan<InventoryDeathLootCacheSignal> signals = SignalBus<InventoryDeathLootCacheSignal>.GetFrameSnapshot();
            RequeueInventoryDeathLootCacheSignals(signals);
        }

        private void RequeueInventoryDeathLootCacheSignals(ReadOnlySpan<InventoryDeathLootCacheSignal> signals)
        {
            if (signals.Length == 0)
                return;

            _dependencyTelemetryFlags |= TelemetryDeathCacheDeferredFlag;
            for (int signalIndex = 0; signalIndex < signals.Length; signalIndex++)
            {
                InventoryDeathLootCacheSignal signal = signals[signalIndex];
                if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref _signalPushDropCount))
                    _dependencyTelemetryFlags |= TelemetryDeathCacheRequeueRejectedFlag;
            }
        }

        private void DrainInventoryDeathLootCacheSignals(ReadOnlySpan<InventoryDeathLootCacheSignal> signals, LootMagnetVaultViews views)
        {
            int capacity = ResolveWritableCapacity(in views);
            if (capacity <= 0)
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                RequeueInventoryDeathLootCacheSignals(signals);
                return;
            }

            int activeCount = math.clamp(_activeCount, 0, capacity);
            for (int signalIndex = 0; signalIndex < signals.Length; signalIndex++)
            {
                InventoryDeathLootCacheSignal signal = signals[signalIndex];
                if (!IsFiniteAup(in signal.PositionAup) ||
                    signal.ItemHash == 0u ||
                    signal.Quantity == 0)
                {
                    _dependencyTelemetryFlags |= TelemetryDeathCacheNonFiniteFlag;
                    continue;
                }

                int slot = FindInactiveLootCacheSlot(in views, capacity);
                if (slot < 0)
                {
                    _dependencyTelemetryFlags |= TelemetryDeathCacheSaturatedFlag | TelemetryDeathCacheDeferredFlag;
                    if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref _signalPushDropCount))
                        _dependencyTelemetryFlags |= TelemetryDeathCacheRequeueRejectedFlag;
                    continue;
                }

                _pickupRefs[slot] = null;
                _pickupEntityIds[slot] = 0UL;
                views.EntityAups[slot] = signal.PositionAup;
                views.EntityVelocities[slot] = float3.zero;
                views.EntityItemHashes[slot] = signal.ItemHash;
                views.EntityQuantities[slot] = (ushort)math.clamp((int)signal.Quantity, 1, (int)ushort.MaxValue);
                views.EntityFlags[slot] = LootEntityFlags.Active |
                                          LootEntityFlags.IsLoot |
                                          LootEntityFlags.Bit_IsMagnetic |
                                          LootEntityFlags.DataOnlyDeathCache;
                if (views.SignalEvents.IsCreated && slot < views.SignalEvents.Length)
                {
                    views.SignalEvents[slot] = new LootMagnetSignalEvent
                    {
                        PositionAup = signal.PositionAup,
                        ItemHash = signal.ItemHash,
                        Quantity = signal.Quantity,
                        Frame = signal.Frame,
                        GeneticsMask = signal.GeneticsMask,
                        QualityMilli = signal.QualityMilli,
                        StateFlags = signal.StateFlags
                    };
                }

                activeCount = math.max(activeCount, slot + 1);
            }

            _activeCount = activeCount;
        }

        private static int FindInactiveLootCacheSlot(in LootMagnetVaultViews views, int capacity)
        {
            int count = math.min(capacity, views.EntityFlags.IsCreated ? views.EntityFlags.Length : 0);
            for (int index = 0; index < count; index++)
            {
                uint flags = views.EntityFlags[index];
                if ((flags & LootEntityFlags.Active) != 0u)
                    continue;

                if ((flags & (LootEntityFlags.DataOnlyDeathCache | LootEntityFlags.Acquired)) != 0u)
                    continue;

                if (views.EntityItemHashes.IsCreated &&
                    index < views.EntityItemHashes.Length &&
                    views.EntityItemHashes[index] != 0u)
                    continue;

                if (views.EntityQuantities.IsCreated &&
                    index < views.EntityQuantities.Length &&
                    views.EntityQuantities[index] != 0)
                    continue;

                return index;
            }

            return -1;
        }

        private static bool IsDataOnlyDeathCacheSlot(in LootMagnetVaultViews views, int index)
        {
            if (!views.EntityFlags.IsCreated ||
                !views.EntityItemHashes.IsCreated ||
                !views.EntityQuantities.IsCreated ||
                !views.EntityAups.IsCreated ||
                (uint)index >= (uint)views.EntityFlags.Length ||
                (uint)index >= (uint)views.EntityItemHashes.Length ||
                (uint)index >= (uint)views.EntityQuantities.Length ||
                (uint)index >= (uint)views.EntityAups.Length)
            {
                return false;
            }

            uint flags = views.EntityFlags[index];
            const uint required = LootEntityFlags.IsLoot | LootEntityFlags.DataOnlyDeathCache;
            return (flags & required) == required &&
                   (flags & (LootEntityFlags.Active | LootEntityFlags.Acquired)) != 0u &&
                   views.EntityItemHashes[index] != 0u &&
                   views.EntityQuantities[index] != 0 &&
                   IsFiniteAupValue(views.EntityAups[index]);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                if (IsFiniteAup(in snapshot.Aup))
                {
                    playerAup = snapshot.Aup;
                    _lastPlayerAup = playerAup;
                    _dependencyTelemetryFlags &= ~TelemetryPlayerPoseMissingFlag;
                    return true;
                }

                _dependencyTelemetryFlags |= TelemetryPlayerPoseNonFiniteFlag;
            }

            playerAup = default;
            Transform playerTransform = _playerTransform;
            if (playerTransform != null)
            {
                Vector3 playerPosition = playerTransform.position;
                if (!IsFiniteRuntimePosition(playerPosition))
                {
                    _dependencyTelemetryFlags |= TelemetryPlayerPoseNonFiniteFlag;
                    _dependencyTelemetryFlags |= TelemetryPlayerPoseMissingFlag;
                    return false;
                }

                if (!TryBuildFiniteAup(playerPosition, out playerAup))
                {
                    _dependencyTelemetryFlags |= TelemetryPlayerPoseNonFiniteFlag;
                    _dependencyTelemetryFlags |= TelemetryPlayerPoseMissingFlag;
                    return false;
                }

                _lastPlayerAup = playerAup;
                _dependencyTelemetryFlags &= ~TelemetryPlayerPoseMissingFlag;
                return true;
            }

            _dependencyTelemetryFlags |= TelemetryPlayerPoseMissingFlag;
            return false;
        }

        private void SchedulePull(float dt, AbsoluteUniversePosition playerAup)
        {
            if (!TryAcquireScheduledVaultGuard())
                return;

            bool scheduled = false;
            try
            {
                if (!TryResolveVaultViews(out LootMagnetVaultViews views, Capacity, allowAllocate: false))
                    return;

                int scheduledCapacity = ResolveWritableCapacity(in views);
                int count = math.min(_activeCount, scheduledCapacity);
                if (count <= 0)
                    return;

                _scheduledCount = count;
                _scheduledCapacity = scheduledCapacity;
                _frameCounter++;
                uint authoringTelemetryFlags = 0u;
                float safeDeltaTime = SanitizeFiniteRange(
                    dt,
                    0.0001f,
                    0.0001f,
                    LootMagnetConstants.MaxIntegrationDeltaTimeSeconds,
                    ref authoringTelemetryFlags);
                float safeRadiusMeters = SanitizeFiniteRange(
                    pullRadiusMeters,
                    LootMagnetConstants.DefaultPullRadiusMeters,
                    LootMagnetConstants.AcquireDistanceMeters,
                    LootMagnetConstants.MaxStablePullRadiusMeters,
                    ref authoringTelemetryFlags);
                float systemStress01 = ResolveSystemStress01();
                if (systemStress01 > LootMagnetConstants.StressRadiusReductionThreshold01)
                {
                    safeRadiusMeters = math.max(
                        LootMagnetConstants.AcquireDistanceMeters,
                        safeRadiusMeters * LootMagnetConstants.StressRadiusMultiplier);
                    authoringTelemetryFlags |= TelemetryStressRadiusClampFlag;
                }

                _scheduledPullRadiusMeters = safeRadiusMeters;
                _scheduledPullRadiusSq = safeRadiusMeters * safeRadiusMeters;
                float safePullStrength = SanitizeFiniteRange(
                    pullStrength,
                    LootMagnetConstants.DefaultPullStrength,
                    0f,
                    LootMagnetConstants.MaxStablePullStrength,
                    ref authoringTelemetryFlags);
                float safeMaxVelocity = SanitizeFiniteRange(
                    maxVelocityMetersPerSecond,
                    LootMagnetConstants.DefaultMaxVelocityMetersPerSecond,
                    0.01f,
                    LootMagnetConstants.MaxStableVelocityMetersPerSecond,
                    ref authoringTelemetryFlags);
                _dependencyTelemetryFlags |= authoringTelemetryFlags;
                LootMagnetJob job = new LootMagnetJob
                {
                    PlayerAup = playerAup,
                    DeltaTimeSeconds = safeDeltaTime,
                    PullRadiusSq = _scheduledPullRadiusSq,
                    PullStrength = safePullStrength,
                    MaxVelocityMetersPerSecond = safeMaxVelocity,
                    Frame = _frameCounter,
                    EntityAups = views.EntityAups,
                    EntityFlags = views.EntityFlags,
                    EntityVelocities = views.EntityVelocities,
                    EntityItemHashes = views.EntityItemHashes,
                    EntityQuantities = views.EntityQuantities,
                    SignalEvents = views.SignalEvents
                };
                _pullHandle = job.Schedule(count, 64);
                _pullScheduled = true;
                scheduled = true;
                JobHandle.ScheduleBatchedJobs();
            }
            finally
            {
                if (!scheduled)
                {
                    _scheduledCount = 0;
                    _scheduledCapacity = 0;
                    ReleaseScheduledVaultGuard();
                }
            }
        }

        private bool TryAcquireScheduledVaultGuard()
        {
            if (_scheduledVaultGuardHeld)
                return true;

            IDataVault vault = _vault;
            if (vault == null)
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return false;
            }

            if (vault.TryAcquireMutationGuard(JobMutationGuardMask))
            {
                _scheduledVaultGuardVault = vault;
                _scheduledVaultGuardHeld = true;
                return true;
            }

            _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
            return false;
        }

        private void ReleaseScheduledVaultGuard()
        {
            if (!_scheduledVaultGuardHeld)
                return;

            IDataVault vault = _scheduledVaultGuardVault;
            _scheduledVaultGuardVault = null;
            _scheduledVaultGuardHeld = false;
            if (vault != null)
                vault.ReleaseMutationGuard(JobMutationGuardMask);
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private uint CompleteScheduledPullInPostSimulation()
        {
            if (!_pullScheduled)
                return 0u;

            if (!DispatcherJobSwap.TryComplete(ref _pullHandle, forceComplete: false))
                return 0u;

            uint telemetryFlags = 0u;
            _pullScheduled = false;
            try
            {
                if (TryResolveVaultViews(out LootMagnetVaultViews views, _scheduledCapacity, allowAllocate: false) &&
                    CanCommitCompletedJob(in views))
                {
                    telemetryFlags = CommitCompletedPullResultsPostSimulation(views);
                }
            }
            finally
            {
                ReleaseScheduledVaultGuard();
            }

            return telemetryFlags;
        }

        private uint CommitCompletedPullResultsPostSimulation(LootMagnetVaultViews views)
        {
            int count = math.min(_scheduledCount, _scheduledCapacity);
            uint flagsHash = count > 0 ? TelemetryHashOffset : 0u;
            int acquisitionBudget = math.max(0, LootMagnetConstants.MaxAcquisitionsPerFrame - PendingAcquisitionCount);
            uint telemetryFlags = _dependencyTelemetryFlags | _registryTelemetryFlags;
            if (_inventory == null)
                telemetryFlags |= TelemetryInventoryMissingFlag;

            bool fault = false;
            float peakVelocitySq = 0f;
            uint activePullsCount = 0u;
            int lastActiveIndex = -1;
            for (int index = 0; index < count; index++)
            {
                uint flags = views.EntityFlags[index];
                if ((flags & LootEntityFlags.NonFinite) != 0u)
                    fault = true;

                float3 slotVelocity = views.EntityVelocities[index];
                float velocitySq = math.lengthsq(slotVelocity);
                if (math.isfinite(velocitySq))
                {
                    peakVelocitySq = math.max(peakVelocitySq, velocitySq);
                }
                else
                {
                    fault = true;
                }

                if ((flags & (LootEntityFlags.Active | LootEntityFlags.Pulling)) == (LootEntityFlags.Active | LootEntityFlags.Pulling))
                    activePullsCount++;

                PickupItem pickup = _pickupRefs[index];
                LootMagnetSignalEvent signalEvent = views.SignalEvents.IsCreated ? views.SignalEvents[index] : default;
                if (pickup == null)
                {
                    if (TryCommitDataOnlyDeathCacheAcquisition(
                            views,
                            index,
                            flags,
                            in signalEvent,
                            ref acquisitionBudget,
                            ref telemetryFlags,
                            ref fault,
                            ref flagsHash,
                            ref lastActiveIndex))
                    {
                        continue;
                    }

                    telemetryFlags |= TelemetryPickupProxyInvalidFlag;
                    ClearVaultSlot(views, index);
                    continue;
                }

                ulong pickupEntityId = EntityId.ToULong(pickup.GetEntityId());
                uint pickupItemHash = unchecked((uint)pickup.ItemHashId);
                if (!pickup.isActiveAndEnabled ||
                    pickup.Quantity <= 0 ||
                    pickupItemHash == 0u ||
                    pickupItemHash != views.EntityItemHashes[index] ||
                    pickupEntityId != _pickupEntityIds[index])
                {
                    telemetryFlags |= TelemetryPickupProxyInvalidFlag;
                    ClearVaultSlot(views, index);
                    continue;
                }

                if ((flags & LootEntityFlags.Acquired) != 0u)
                {
                    if (_inventory == null)
                    {
                        telemetryFlags |= TelemetryInventoryMissingFlag;
                        views.EntityFlags[index] = LootEntityFlags.Active | LootEntityFlags.IsLoot;
                        pickup.RestoreLootMagnetRuntimeState();
                        FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                        continue;
                    }

                    if (acquisitionBudget <= 0)
                    {
                        telemetryFlags |= TelemetryAcquisitionBudgetDeferFlag;
                        RestoreDeferredAcquisition(views, index, flags);
                        pickup.RestoreLootMagnetRuntimeState();
                        FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                        continue;
                    }

                    acquisitionBudget--;
                    int quantityBefore = math.max(0, pickup.Quantity);
                    LootMagnetSignalEvent queuedSignal = signalEvent;
                    queuedSignal.PositionAup = views.EntityAups[index];
                    queuedSignal.ItemHash = pickupItemHash;
                    queuedSignal.Quantity = (uint)math.min(quantityBefore, (int)ushort.MaxValue);
                    queuedSignal.Flags |= LootMagnetEventFlags.Acquired |
                                          LootMagnetEventFlags.Acoustic |
                                          LootMagnetEventFlags.Wake;
                    if (queuedSignal.Frame == 0u)
                        queuedSignal.Frame = _frameCounter;

                    bool handled = pickup.TryHandleInventoryPickup(_inventory, _playerTransform, publishAcquiredSignal: false);
                    int quantityAfter = math.max(0, pickup.Quantity);
                    int addedQuantity = handled ? math.max(0, quantityBefore - quantityAfter) : 0;
                    if (addedQuantity <= 0)
                    {
                        telemetryFlags |= TelemetryAcquisitionBudgetDeferFlag;
                        RestoreDeferredAcquisition(views, index, flags);
                        pickup.RestoreLootMagnetRuntimeState();
                        FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                        continue;
                    }

                    _slowPhaseAcquiredCountPendingTelemetry++;
                    queuedSignal.Quantity = (uint)math.min(addedQuantity, (int)ushort.MaxValue);
                    telemetryFlags |= PublishItemAcquired(in queuedSignal, addedQuantity);
                    TryQueueAcquisitionPresentation(in queuedSignal, addedQuantity);

                    if (quantityAfter > 0)
                    {
                        pickup.RestoreLootMagnetRuntimeState();
                        views.EntityQuantities[index] = (ushort)math.min(quantityAfter, (int)ushort.MaxValue);
                        views.EntityFlags[index] = (flags & ~LootEntityFlags.Acquired) |
                                                   LootEntityFlags.Active |
                                                   LootEntityFlags.IsLoot |
                                                   LootEntityFlags.Bit_IsMagnetic;
                        FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                    }
                    else
                    {
                        ClearVaultSlot(views, index);
                    }

                    continue;
                }

                if ((flags & LootEntityFlags.Pulling) == 0u || (flags & LootEntityFlags.Active) == 0u)
                {
                    pickup.RestoreLootMagnetRuntimeState();
                    FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                    continue;
                }

                float3 runtime = views.EntityAups[index].ToRuntimeFloat3();
                if (!IsFiniteFloat3(runtime))
                {
                    fault = true;
                    telemetryFlags |= TelemetryPickupPoseNonFiniteFlag;
                    views.EntityFlags[index] = flags | LootEntityFlags.NonFinite;
                    pickup.RestoreLootMagnetRuntimeState();
                    FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                    continue;
                }

                FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
            }

            _activeCount = lastActiveIndex + 1;
            _registryFlagsHash = _activeCount > 0 ? flagsHash : 0u;
            _lastCommittedAcquiredCount = 0u;
            _lastCommittedFlagsHash = _registryFlagsHash;
            _lastActiveLootPullsCount = activePullsCount;
            _lastPeakMagnetVelocity = EstimatePeakVelocity(peakVelocitySq);
            _lastCommittedFlags = (fault ? TelemetryFaultFlag : 0u) | telemetryFlags;
            if (fault && !_dumpedFault)
            {
                RecordTelemetry(_telemetryFrameCounter, views);
                _dumpedFault = true;
                DumpTelemetryBuffer(in views);
            }

            return _lastCommittedFlags;
        }

        private bool TryCommitDataOnlyDeathCacheAcquisition(
            LootMagnetVaultViews views,
            int index,
            uint flags,
            in LootMagnetSignalEvent signalEvent,
            ref int acquisitionBudget,
            ref uint telemetryFlags,
            ref bool fault,
            ref uint flagsHash,
            ref int lastActiveIndex)
        {
            if ((flags & LootEntityFlags.DataOnlyDeathCache) == 0u)
                return false;

            if ((flags & LootEntityFlags.Acquired) == 0u)
            {
                if ((flags & LootEntityFlags.Active) == 0u)
                {
                    ClearVaultSlot(views, index);
                    return true;
                }

                FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                return true;
            }

            if (_inventory == null)
            {
                telemetryFlags |= TelemetryInventoryMissingFlag;
                RestoreDeferredAcquisition(views, index, flags);
                FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                return true;
            }

            if (acquisitionBudget <= 0)
            {
                telemetryFlags |= TelemetryAcquisitionBudgetDeferFlag;
                RestoreDeferredAcquisition(views, index, flags);
                FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                return true;
            }

            uint itemHash = views.EntityItemHashes[index];
            int quantity = math.clamp((int)views.EntityQuantities[index], 1, (int)ushort.MaxValue);
            if (itemHash == 0u || !IsFiniteAupValue(views.EntityAups[index]))
            {
                fault = true;
                telemetryFlags |= TelemetryDeathCacheNonFiniteFlag;
                ClearVaultSlot(views, index);
                return true;
            }

            acquisitionBudget--;
            LootMagnetSignalEvent queuedSignal = signalEvent;
            queuedSignal.PositionAup = views.EntityAups[index];
            queuedSignal.ItemHash = itemHash;
            queuedSignal.Quantity = (uint)quantity;
            queuedSignal.Flags |= LootMagnetEventFlags.Acquired |
                                  LootMagnetEventFlags.Acoustic |
                                  LootMagnetEventFlags.Wake;
            if (queuedSignal.Frame == 0u)
                queuedSignal.Frame = _frameCounter;

            InventoryDeathLootCacheSignal queuedCache = new InventoryDeathLootCacheSignal
            {
                PositionAup = views.EntityAups[index],
                GeneticsMask = signalEvent.GeneticsMask,
                ItemHash = itemHash,
                Frame = queuedSignal.Frame,
                Quantity = (ushort)quantity,
                QualityMilli = signalEvent.QualityMilli,
                Flags = queuedSignal.Flags,
                StateFlags = signalEvent.StateFlags
            };
            if (!TryQueueDeathCacheAcquisition(in queuedCache, in queuedSignal))
            {
                telemetryFlags |= TelemetryAcquisitionBudgetDeferFlag;
                RestoreDeferredAcquisition(views, index, flags);
                FoldActiveSlotHash(in views, ref flagsHash, ref lastActiveIndex, index);
                return true;
            }

            ClearVaultSlot(views, index);
            return true;
        }

        private bool TryQueueDeathCacheAcquisition(
            in InventoryDeathLootCacheSignal cacheSignal,
            in LootMagnetSignalEvent signalEvent)
        {
            if (_pendingDeathCacheAcquisitions == null ||
                _pendingDeathCacheAcquisitionEvents == null ||
                PendingAcquisitionCount >= LootMagnetConstants.MaxAcquisitionsPerFrame)
            {
                return false;
            }

            int index = _pendingDeathCacheAcquisitionCount;
            if (index < 0 ||
                index >= _pendingDeathCacheAcquisitions.Length ||
                index >= _pendingDeathCacheAcquisitionEvents.Length)
            {
                return false;
            }

            _pendingDeathCacheAcquisitions[index] = cacheSignal;
            _pendingDeathCacheAcquisitionEvents[index] = signalEvent;
            _pendingDeathCacheAcquisitionCount = index + 1;
            return true;
        }

        private bool TryQueueAcquisitionPresentation(in LootMagnetSignalEvent signalEvent, int addedQuantity)
        {
            if (_pendingPresentationEvents == null ||
                _pendingPresentationQuantities == null ||
                _pendingPresentationCount < 0 ||
                _pendingPresentationCount >= LootMagnetConstants.MaxAcquisitionsPerFrame ||
                _pendingPresentationCount >= _pendingPresentationEvents.Length ||
                _pendingPresentationCount >= _pendingPresentationQuantities.Length)
            {
                _dependencyTelemetryFlags |= TelemetryAcquisitionBudgetDeferFlag;
                return false;
            }

            int index = _pendingPresentationCount;
            _pendingPresentationEvents[index] = signalEvent;
            _pendingPresentationQuantities[index] = (ushort)math.clamp(addedQuantity, 1, (int)ushort.MaxValue);
            _pendingPresentationCount = index + 1;
            return true;
        }

        private void ApplyPendingAcquisitions()
        {
            if (_pendingDeathCacheAcquisitionCount <= 0)
                return;

            if (_inventory == null)
            {
                _dependencyTelemetryFlags |= TelemetryInventoryMissingFlag;
                return;
            }

            uint acquiredCount = 0u;
            ApplyPendingDeathCacheAcquisitions(ref acquiredCount);
            _slowPhaseAcquiredCountPendingTelemetry += acquiredCount;
        }

        private void ApplyPendingDeathCacheAcquisitions(ref uint acquiredCount)
        {
            if (_pendingDeathCacheAcquisitions == null || _pendingDeathCacheAcquisitionEvents == null)
            {
                _pendingDeathCacheAcquisitionCount = 0;
                return;
            }

            int count = _pendingDeathCacheAcquisitionCount;
            count = math.clamp(count, 0, _pendingDeathCacheAcquisitions.Length);
            count = math.min(count, _pendingDeathCacheAcquisitionEvents.Length);
            for (int index = 0; index < count; index++)
            {
                InventoryDeathLootCacheSignal cacheSignal = _pendingDeathCacheAcquisitions[index];
                LootMagnetSignalEvent signalEvent = _pendingDeathCacheAcquisitionEvents[index];
                _pendingDeathCacheAcquisitions[index] = default;
                _pendingDeathCacheAcquisitionEvents[index] = default;

                uint itemHash = cacheSignal.ItemHash;
                int quantity = math.clamp((int)cacheSignal.Quantity, 1, (int)ushort.MaxValue);
                if (itemHash == 0u ||
                    cacheSignal.Quantity == 0 ||
                    !IsFiniteAup(in cacheSignal.PositionAup))
                {
                    _dependencyTelemetryFlags |= TelemetryDeathCacheNonFiniteFlag;
                    continue;
                }

                ushort qualityMilli = cacheSignal.QualityMilli > 0
                    ? cacheSignal.QualityMilli
                    : DefaultRecoveredItemQualityMilli;
                bool allAdded = _inventory.TryAddItemWithState(
                    unchecked((int)itemHash),
                    new PlayerInventory.ItemState(cacheSignal.GeneticsMask, qualityMilli, cacheSignal.StateFlags),
                    quantity,
                    out int addedQuantity);
                if (addedQuantity <= 0)
                {
                    if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in cacheSignal, ref _signalPushDropCount))
                        _dependencyTelemetryFlags |= TelemetryDeathCacheRequeueRejectedFlag;
                    continue;
                }

                if (!allAdded && addedQuantity < quantity)
                {
                    InventoryDeathLootCacheSignal remainderSignal = cacheSignal;
                    remainderSignal.Quantity = (ushort)math.clamp(quantity - addedQuantity, 1, (int)ushort.MaxValue);
                    if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in remainderSignal, ref _signalPushDropCount))
                        _dependencyTelemetryFlags |= TelemetryDeathCacheRequeueRejectedFlag;
                }

                acquiredCount++;
                LootMagnetSignalEvent resolvedSignal = signalEvent;
                resolvedSignal.PositionAup = cacheSignal.PositionAup;
                resolvedSignal.ItemHash = itemHash;
                resolvedSignal.Quantity = (uint)addedQuantity;
                resolvedSignal.GeneticsMask = cacheSignal.GeneticsMask;
                resolvedSignal.QualityMilli = qualityMilli;
                resolvedSignal.StateFlags = cacheSignal.StateFlags;
                resolvedSignal.Flags |= LootMagnetEventFlags.Acquired |
                                        LootMagnetEventFlags.Acoustic |
                                        LootMagnetEventFlags.Wake;
                if (resolvedSignal.Frame == 0u)
                    resolvedSignal.Frame = cacheSignal.Frame != 0u ? cacheSignal.Frame : _frameCounter;

                _dependencyTelemetryFlags |= PublishItemAcquired(in resolvedSignal, quantity);
                TryQueueAcquisitionPresentation(in resolvedSignal, quantity);
            }

            _pendingDeathCacheAcquisitionCount = 0;
        }

        private uint FlushPendingPresentationSignals()
        {
            if (_pendingPresentationCount <= 0)
                return 0u;

            if (_pendingPresentationEvents == null || _pendingPresentationQuantities == null)
            {
                _pendingPresentationCount = 0;
                return TelemetryAcquisitionBudgetDeferFlag;
            }

            uint telemetryFlags = 0u;
            int acousticBudget = ResolveAcousticSignalBudget(_qualityWeight01);
            int wakeBudget = ResolveWakeSignalBudget(_qualityWeight01);
            int count = _pendingPresentationCount;
            count = math.clamp(count, 0, _pendingPresentationEvents.Length);
            count = math.min(count, _pendingPresentationQuantities.Length);
            for (int index = 0; index < count; index++)
            {
                LootMagnetSignalEvent signalEvent = _pendingPresentationEvents[index];
                int quantity = _pendingPresentationQuantities[index];
                _pendingPresentationEvents[index] = default;
                _pendingPresentationQuantities[index] = 0;
                if (quantity <= 0)
                    continue;

                telemetryFlags |= PublishItemSnapSpark(in signalEvent, quantity);
                telemetryFlags |= PublishPresentationSignals(in signalEvent, quantity, ref acousticBudget, ref wakeBudget);
            }

            _pendingPresentationCount = 0;
            return telemetryFlags;
        }

        private uint FlushPullPresentationFromVault()
        {
            if (_pullScheduled ||
                _activeCount <= 0 ||
                _pickupRefs == null ||
                !TryResolveVaultViews(out LootMagnetVaultViews views, _activeCount, allowAllocate: false))
            {
                return 0u;
            }

            uint telemetryFlags = 0u;
            int acousticBudget = ResolveAcousticSignalBudget(_qualityWeight01);
            int wakeBudget = ResolveWakeSignalBudget(_qualityWeight01);
            int count = math.min(_activeCount, ResolveWritableCapacity(in views));
            for (int index = 0; index < count; index++)
            {
                uint flags = views.EntityFlags[index];
                if ((flags & LootEntityFlags.Active) == 0u)
                    continue;

                LootMagnetSignalEvent signalEvent = views.SignalEvents.IsCreated && index < views.SignalEvents.Length
                    ? views.SignalEvents[index]
                    : default;
                telemetryFlags |= PublishPresentationSignals(in signalEvent, 0, ref acousticBudget, ref wakeBudget);

                if ((flags & LootEntityFlags.Pulling) == 0u)
                    continue;

                PickupItem pickup = _pickupRefs[index];
                if (pickup == null || !pickup.isActiveAndEnabled)
                    continue;

                float3 runtime = views.EntityAups[index].ToRuntimeFloat3();
                if (!IsFiniteFloat3(runtime))
                {
                    pickup.RestoreLootMagnetRuntimeState();
                    telemetryFlags |= TelemetryPickupPoseNonFiniteFlag;
                    continue;
                }

                pickup.ApplyLootMagnetPose(
                    new Vector3(runtime.x, runtime.y, runtime.z),
                    views.EntityVelocities[index],
                    LootMagnetConstants.MotionVectorVelocityThresholdSq);
            }

            return telemetryFlags;
        }

        private void ClearPendingAcquisitionQueues(bool requeueDeathCache)
        {
            if (requeueDeathCache)
                RequeuePendingDeathCacheAcquisitionsForBarrier();

            ClearPendingDeathCacheAcquisitionSlots();
            ClearPendingPresentationSlots();
            _slowPhaseAcquiredCountPendingTelemetry = 0u;
        }

        private void RequeuePendingDeathCacheAcquisitionsForBarrier()
        {
            if (_pendingDeathCacheAcquisitionCount <= 0 ||
                _pendingDeathCacheAcquisitions == null)
            {
                return;
            }

            int count = math.clamp(_pendingDeathCacheAcquisitionCount, 0, _pendingDeathCacheAcquisitions.Length);
            if (count <= 0)
                return;

            _dependencyTelemetryFlags |= TelemetryDeathCacheDeferredFlag;
            for (int index = 0; index < count; index++)
            {
                InventoryDeathLootCacheSignal signal = _pendingDeathCacheAcquisitions[index];
                if (signal.ItemHash == 0u ||
                    signal.Quantity == 0 ||
                    !IsFiniteAup(in signal.PositionAup))
                {
                    _dependencyTelemetryFlags |= TelemetryDeathCacheNonFiniteFlag;
                    continue;
                }

                if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref _signalPushDropCount))
                    _dependencyTelemetryFlags |= TelemetryDeathCacheRequeueRejectedFlag;
            }
        }

        private void ClearPendingDeathCacheAcquisitionSlots()
        {
            if (_pendingDeathCacheAcquisitions != null)
            {
                int count = math.clamp(_pendingDeathCacheAcquisitionCount, 0, _pendingDeathCacheAcquisitions.Length);
                for (int index = 0; index < count; index++)
                    _pendingDeathCacheAcquisitions[index] = default;
            }

            if (_pendingDeathCacheAcquisitionEvents != null)
            {
                int count = math.clamp(_pendingDeathCacheAcquisitionCount, 0, _pendingDeathCacheAcquisitionEvents.Length);
                for (int index = 0; index < count; index++)
                    _pendingDeathCacheAcquisitionEvents[index] = default;
            }

            _pendingDeathCacheAcquisitionCount = 0;
        }

        private void ClearPendingPresentationSlots()
        {
            if (_pendingPresentationEvents != null)
            {
                int count = math.clamp(_pendingPresentationCount, 0, _pendingPresentationEvents.Length);
                for (int index = 0; index < count; index++)
                    _pendingPresentationEvents[index] = default;
            }

            if (_pendingPresentationQuantities != null)
            {
                int count = math.clamp(_pendingPresentationCount, 0, _pendingPresentationQuantities.Length);
                for (int index = 0; index < count; index++)
                    _pendingPresentationQuantities[index] = 0;
            }

            _pendingPresentationCount = 0;
        }

        private void RestoreDeferredAcquisition(LootMagnetVaultViews views, int index, uint flags)
        {
            views.EntityFlags[index] = (flags & ~(LootEntityFlags.Acquired | LootEntityFlags.Pulling)) |
                                       LootEntityFlags.Active |
                                       LootEntityFlags.IsLoot |
                                       LootEntityFlags.Bit_IsMagnetic |
                                       (flags & LootEntityFlags.DataOnlyDeathCache);
        }

        private void ClearVaultSlot(LootMagnetVaultViews views, int index)
        {
            PickupItem pickup = _pickupRefs[index];
            if (pickup != null)
                pickup.RestoreLootMagnetRuntimeState();

            _pickupRefs[index] = null;
            _pickupEntityIds[index] = 0UL;
            views.EntityAups[index] = default;
            views.EntityFlags[index] = 0u;
            views.EntityVelocities[index] = float3.zero;
            views.EntityItemHashes[index] = 0u;
            views.EntityQuantities[index] = 0;
            if (views.SignalEvents.IsCreated && index < views.SignalEvents.Length)
                views.SignalEvents[index] = default;
        }

        private void ClearRuntimeVaultState()
        {
            ClearPendingAcquisitionQueues(requeueDeathCache: true);
            RequeueDataOnlyDeathCacheSlotsForBarrier();
            RestoreAllManagedProxyRuntimeStates();
            ClearKnownRuntimeVaultSlots();
            _activeCount = 0;
            _registryFlagsHash = 0u;
            _lastCommittedFlagsHash = 0u;
            _lastActiveLootPullsCount = 0u;
            _lastPeakMagnetVelocity = 0f;
        }

        private void ClearDataVaultRuntimeState()
        {
            ClearPendingAcquisitionQueues(requeueDeathCache: true);
            RequeueDataOnlyDeathCacheSlotsForBarrier();
            RestoreAllManagedProxyRuntimeStates();
            ClearKnownRuntimeVaultSlots();
            _activeCount = 0;
            _registryFlagsHash = 0u;
            _lastCommittedFlagsHash = 0u;
            _lastActiveLootPullsCount = 0u;
            _lastPeakMagnetVelocity = 0f;
            _scheduledCount = 0;
            _scheduledCapacity = 0;
            _telemetryIndex = 0;
            _lastTelemetryRecordedFrame = 0u;
        }

        private void RequeueDataOnlyDeathCacheSlotsForBarrier()
        {
            IDataVault vault = _vault;
            if (vault == null ||
                _activeCount <= 0 ||
                !TryReadExistingVaultViews(vault, math.max(_activeCount, 1), out LootMagnetVaultViews views) ||
                !LootMagnetVaultViews.IsCreated(in views))
            {
                return;
            }

            int count = math.clamp(_activeCount, 0, views.EntityFlags.Length);
            count = math.min(count, views.EntityItemHashes.Length);
            count = math.min(count, views.EntityQuantities.Length);
            count = math.min(count, views.EntityAups.Length);
            if (count <= 0)
                return;

            for (int index = 0; index < count; index++)
            {
                if (!IsDataOnlyDeathCacheSlot(in views, index))
                    continue;

                LootMagnetSignalEvent signalEvent = views.SignalEvents.IsCreated && index < views.SignalEvents.Length
                    ? views.SignalEvents[index]
                    : default;
                InventoryDeathLootCacheSignal signal = default;
                signal.PositionAup = views.EntityAups[index];
                signal.GeneticsMask = signalEvent.GeneticsMask;
                signal.ItemHash = views.EntityItemHashes[index];
                signal.Frame = signalEvent.Frame;
                signal.Quantity = views.EntityQuantities[index];
                signal.QualityMilli = signalEvent.QualityMilli;
                signal.Flags = signalEvent.Flags;
                signal.StateFlags = signalEvent.StateFlags;
                if (signal.ItemHash == 0u ||
                    signal.Quantity == 0 ||
                    !IsFiniteAup(in signal.PositionAup))
                {
                    _dependencyTelemetryFlags |= TelemetryDeathCacheNonFiniteFlag;
                    continue;
                }

                _dependencyTelemetryFlags |= TelemetryDeathCacheDeferredFlag;
                if (!SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref _signalPushDropCount))
                    _dependencyTelemetryFlags |= TelemetryDeathCacheRequeueRejectedFlag;

                ClearDataOnlyDeathCacheVaultSlot(views, index);
            }
        }

        private static void ClearDataOnlyDeathCacheVaultSlot(LootMagnetVaultViews views, int index)
        {
            views.EntityAups[index] = default;
            views.EntityFlags[index] = 0u;
            views.EntityVelocities[index] = float3.zero;
            views.EntityItemHashes[index] = 0u;
            views.EntityQuantities[index] = 0;
            if (views.SignalEvents.IsCreated && index < views.SignalEvents.Length)
                views.SignalEvents[index] = default;
        }

        private void ClearKnownRuntimeVaultSlots()
        {
            if (_pickupRefs == null || _pickupEntityIds == null)
                return;

            IDataVault vault = _vault;
            if (vault == null ||
                !TryReadExistingVaultViews(vault, math.max(_activeCount, 1), out LootMagnetVaultViews views) ||
                !LootMagnetVaultViews.IsCreated(in views))
            {
                return;
            }

            int count = _pickupRefs.Length;
            count = math.min(count, _pickupEntityIds.Length);
            count = math.min(count, views.EntityAups.Length);
            count = math.min(count, views.EntityFlags.Length);
            count = math.min(count, views.EntityVelocities.Length);
            count = math.min(count, views.EntityItemHashes.Length);
            count = math.min(count, views.EntityQuantities.Length);
            count = math.min(count, views.SignalEvents.Length);
            for (int index = 0; index < count; index++)
            {
                if (_pickupRefs[index] == null && _pickupEntityIds[index] == 0UL)
                    continue;

                ClearVaultSlot(views, index);
            }
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void RestoreAllManagedProxyRuntimeStates()
        {
            if (_pickupRefs == null)
                return;

            int count = math.clamp(_activeCount, 0, _pickupRefs.Length);
            for (int index = 0; index < count; index++)
            {
                PickupItem pickup = _pickupRefs[index];
                if (pickup != null)
                    pickup.RestoreLootMagnetRuntimeState();
            }
        }

        private void ReapplyPulledProxyRuntimePoses(in LootMagnetVaultViews views)
        {
            int capacity = ResolveWritableCapacity(in views);
            int count = math.min(_activeCount, capacity);
            for (int index = 0; index < count; index++)
            {
                uint flags = views.EntityFlags[index];
                if ((flags & (LootEntityFlags.Active | LootEntityFlags.Pulling)) != (LootEntityFlags.Active | LootEntityFlags.Pulling))
                    continue;

                PickupItem pickup = _pickupRefs[index];
                if (pickup == null || !pickup.isActiveAndEnabled)
                    continue;

                AbsoluteUniversePosition aup = views.EntityAups[index];
                if (!IsFiniteAup(in aup))
                    continue;

                float3 runtime = aup.ToRuntimeFloat3();
                if (!IsFiniteFloat3(runtime))
                {
                    pickup.RestoreLootMagnetRuntimeState();
                    continue;
                }

                pickup.ApplyLootMagnetPose(
                    new Vector3(runtime.x, runtime.y, runtime.z),
                    views.EntityVelocities[index],
                    LootMagnetConstants.MotionVectorVelocityThresholdSq);
            }
        }

        private void FoldActiveSlotHash(in LootMagnetVaultViews views, ref uint hash, ref int lastActiveIndex, int index)
        {
            uint flags = views.EntityFlags[index];
            if ((flags & LootEntityFlags.Active) == 0u)
                return;

            hash = FoldEntityIdHash(
                FoldTelemetryHash(FoldTelemetryHash(hash, flags), views.EntityItemHashes[index]),
                _pickupEntityIds[index]);
            lastActiveIndex = index;
        }

        private static uint FoldEntityIdHash(uint hash, ulong entityId)
        {
            return FoldTelemetryHash(
                FoldTelemetryHash(hash, unchecked((uint)entityId)),
                unchecked((uint)(entityId >> 32)));
        }

        private static uint FoldTelemetryHash(uint hash, uint value)
        {
            return (hash ^ value) * TelemetryHashPrime;
        }

        private static bool TryBuildFiniteAup(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in aup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static bool IsFiniteAupValue(AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z);
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float EstimatePeakVelocity(float peakVelocitySq)
        {
            if (!math.isfinite(peakVelocitySq) || peakVelocitySq <= 0f)
                return 0f;

            float velocity = peakVelocitySq * math.rsqrt(peakVelocitySq);
            return math.isfinite(velocity) ? velocity : 0f;
        }

        private static float SanitizeFiniteRange(
            float value,
            float fallback,
            float minimum,
            float maximum,
            ref uint telemetryFlags)
        {
            if (!math.isfinite(value))
            {
                telemetryFlags |= TelemetryAuthoringClampFlag;
                return math.clamp(fallback, minimum, maximum);
            }

            if (value < minimum)
            {
                telemetryFlags |= TelemetryAuthoringClampFlag;
                return minimum;
            }

            if (value > maximum)
            {
                telemetryFlags |= TelemetryAuthoringClampFlag;
                return maximum;
            }

            return value;
        }

        private static float ResolveSystemStress01()
        {
            float systemStress01 = HomeostasisBrain.SystemHealthIndex01;
            if (math.isfinite(systemStress01))
                return math.saturate(systemStress01);

            return math.saturate(HomeostasisBrain.PressureLevel * (1f / 3f));
        }

        private uint PublishItemAcquired(in LootMagnetSignalEvent signalEvent, int addedQuantity)
        {
            if ((signalEvent.Flags & LootMagnetEventFlags.Acquired) == 0u ||
                addedQuantity <= 0 ||
                !IsFiniteAup(in signalEvent.PositionAup))
            {
                return 0u;
            }

            uint itemHash = signalEvent.ItemHash;
            if (itemHash == 0u)
                return 0u;

            ItemAcquiredSignal itemSignal = new ItemAcquiredSignal
            {
                PositionAup = signalEvent.PositionAup,
                ItemHash = itemHash,
                OreHash = itemHash,
                Quantity = (ushort)math.min(addedQuantity, (int)ushort.MaxValue),
                SourceKind = LootMagnetConstants.ItemSourceLootMagnet,
                Flags = LootMagnetConstants.SignalFlagLootMagnet,
                Frame = signalEvent.Frame
            };
            return SignalBus<ItemAcquiredSignal>.TryPushTracked(in itemSignal, ref _signalPushDropCount)
                ? 0u
                : TelemetryItemAcquiredSignalRejectedFlag;
        }

        private static uint PublishItemSnapSpark(in LootMagnetSignalEvent signalEvent, int addedQuantity)
        {
            if ((signalEvent.Flags & LootMagnetEventFlags.Acquired) == 0u ||
                signalEvent.ItemHash == 0u ||
                addedQuantity <= 0 ||
                !IsFiniteAup(in signalEvent.PositionAup))
            {
                return 0u;
            }

            DebrisSpawnSignal debrisSignal = new DebrisSpawnSignal
            {
                PositionAup = signalEvent.PositionAup,
                SpeciesHash = LootMagnetConstants.ItemSnapSparkSpeciesHash,
                SourceEntityId = signalEvent.ItemHash,
                Intensity01 = 1f,
                DebrisKind = LootMagnetConstants.ItemSnapSparkDebrisKind,
                Flags = LootMagnetConstants.SignalFlagLootMagnet,
                Quantity = (ushort)math.min(
                    LootMagnetConstants.ItemSnapSparkQuantity * math.max(1, addedQuantity),
                    (int)ushort.MaxValue)
            };
            return SignalBus<DebrisSpawnSignal>.TryPushTracked(in debrisSignal, ref _signalPushDropCount)
                ? 0u
                : TelemetryDebrisSignalRejectedFlag;
        }

        private uint PublishPresentationSignals(
            in LootMagnetSignalEvent signalEvent,
            int addedQuantity,
            ref int acousticBudget,
            ref int wakeBudget)
        {
            if (signalEvent.Flags == 0u || signalEvent.ItemHash == 0u)
                return 0u;

            bool wantsAcoustic = (signalEvent.Flags & LootMagnetEventFlags.Acoustic) != 0u;
            bool wantsWake = (signalEvent.Flags & LootMagnetEventFlags.Wake) != 0u;
            uint droppedFlags = 0u;
            bool positionFinite = IsFiniteAup(in signalEvent.PositionAup);
            bool distanceFinite = math.isfinite(signalEvent.DistanceSq) && signalEvent.DistanceSq >= 0f;
            bool velocityFinite = math.all(math.isfinite(signalEvent.Velocity));
            if ((wantsAcoustic || wantsWake) && !positionFinite)
                droppedFlags |= TelemetrySignalNonFiniteFlag;

            if ((wantsAcoustic && !distanceFinite) || (wantsWake && !velocityFinite))
                droppedFlags |= TelemetrySignalNonFiniteFlag;

            if (wantsAcoustic && acousticBudget <= 0)
                droppedFlags |= TelemetryAcousticBudgetDropFlag;

            if (wantsWake && wakeBudget <= 0)
                droppedFlags |= TelemetryWakeBudgetDropFlag;

            bool publishAcoustic = wantsAcoustic && acousticBudget > 0 && distanceFinite && positionFinite;
            bool publishWake = wantsWake && wakeBudget > 0 && velocityFinite && positionFinite;
            if (!publishAcoustic && !publishWake)
                return droppedFlags;

            if (publishAcoustic)
            {
                acousticBudget--;
                float radiusMeters = _scheduledPullRadiusMeters;
                if (!math.isfinite(radiusMeters))
                    radiusMeters = LootMagnetConstants.AcquireDistanceMeters;

                radiusMeters = math.clamp(
                    radiusMeters,
                    LootMagnetConstants.AcquireDistanceMeters,
                    LootMagnetConstants.MaxStablePullRadiusMeters);
                float radiusSq = _scheduledPullRadiusSq;
                if (!math.isfinite(radiusSq))
                    radiusSq = radiusMeters * radiusMeters;

                radiusSq = math.clamp(
                    radiusSq,
                    LootMagnetConstants.MinDistanceSq,
                    LootMagnetConstants.MaxStablePullRadiusMeters * LootMagnetConstants.MaxStablePullRadiusMeters);
                float intensity = addedQuantity > 0
                    ? 1f
                    : math.saturate(1f - (signalEvent.DistanceSq * math.rcp(radiusSq)));
                AcousticPingSignal acousticSignal = new AcousticPingSignal
                {
                    PositionAup = signalEvent.PositionAup,
                    RadiusMeters = radiusMeters,
                    Intensity01 = intensity,
                    SourceId = signalEvent.ItemHash,
                    Channel = AcousticPingSignal.ChannelLootZip,
                    Flags = AcousticPingSignal.FlagLootZip
                };
                if (!SignalBus<AcousticPingSignal>.TryPushTracked(in acousticSignal, ref _signalPushDropCount))
                    droppedFlags |= TelemetryAcousticSignalRejectedFlag;
            }

            if (publishWake)
            {
                wakeBudget--;
                WakeGeneratedSignal wakeSignal = new WakeGeneratedSignal
                {
                    PositionAup = signalEvent.PositionAup,
                    Velocity = signalEvent.Velocity,
                    SourceFlags = LootMagnetConstants.WakeSourceLootZip
                };
                if (!SignalBus<WakeGeneratedSignal>.TryPushTracked(in wakeSignal, ref _signalPushDropCount))
                    droppedFlags |= TelemetryWakeSignalRejectedFlag;

                float fluidImpulseWeight01 = ResolveFluidImpulseWeight01(_qualityWeight01);
                if (fluidImpulseWeight01 > 0f)
                {
                    FluidImpulseSignal fluidImpulse = new FluidImpulseSignal
                    {
                        PositionAup = signalEvent.PositionAup,
                        Vector = signalEvent.Velocity * fluidImpulseWeight01,
                        Radius = math.lerp(
                            LootMagnetConstants.HighFidelityFluidImpulseRadiusMeters * 0.5f,
                            LootMagnetConstants.VisualOverkillFluidImpulseRadiusMeters,
                            fluidImpulseWeight01),
                        Lifetime = math.lerp(
                            LootMagnetConstants.HighFidelityFluidImpulseLifetimeSeconds * 0.5f,
                            LootMagnetConstants.VisualOverkillFluidImpulseLifetimeSeconds,
                            fluidImpulseWeight01),
                        Frame = signalEvent.Frame,
                        SourceHash = LootMagnetConstants.FluidImpulseSourceLootZip,
                        Flags = LootMagnetConstants.SignalFlagLootMagnet
                    };
                    if (!SignalBus<FluidImpulseSignal>.TryPushTracked(in fluidImpulse, ref _signalPushDropCount))
                        droppedFlags |= TelemetryFluidImpulseSignalRejectedFlag;
                }
            }

            return droppedFlags;
        }

        private static int ResolveAcousticSignalBudget(float qualityWeight01)
        {
            return ResolveContinuousQualityBudget(
                qualityWeight01,
                LootMagnetConstants.SurvivalAcousticSignalsPerFrame,
                LootMagnetConstants.StandardAcousticSignalsPerFrame,
                LootMagnetConstants.HighFidelityAcousticSignalsPerFrame,
                LootMagnetConstants.VisualOverkillAcousticSignalsPerFrame);
        }

        private static int ResolveWakeSignalBudget(float qualityWeight01)
        {
            return ResolveContinuousQualityBudget(
                qualityWeight01,
                LootMagnetConstants.SurvivalWakeSignalsPerFrame,
                LootMagnetConstants.StandardWakeSignalsPerFrame,
                LootMagnetConstants.HighFidelityWakeSignalsPerFrame,
                LootMagnetConstants.VisualOverkillWakeSignalsPerFrame);
        }

        private static int ResolveContinuousQualityBudget(
            float qualityWeight01,
            int survival,
            int standard,
            int highFidelity,
            int visualOverkill)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 0.5f);
            float survivalToStandard = math.lerp(
                (float)survival,
                (float)standard,
                SmoothRange01(quality, 0f, 0.42f));
            float highFidelityToOverkill = math.lerp(
                (float)highFidelity,
                (float)visualOverkill,
                SmoothRange01(quality, 0.74f, 1f));
            float budget = math.lerp(
                survivalToStandard,
                highFidelityToOverkill,
                SmoothRange01(quality, 0.42f, 0.74f));
            return math.max(0, (int)math.round(budget));
        }

        private static float SmoothRange01(float value, float edge0, float edge1)
        {
            float width = math.max(0.0001f, edge1 - edge0);
            return Smooth01((value - edge0) * math.rcp(width));
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static float ResolveFluidImpulseWeight01(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 0.5f);
            return math.saturate((quality - FluidImpulseMinimumQuality01) * math.rcp(1f - FluidImpulseMinimumQuality01));
        }

        private static float ResolveLootQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0.5f;
        }

        private uint _lastCommittedAcquiredCount;
        private uint _lastCommittedFlagsHash;
        private uint _lastCommittedFlags;

        private void RecordTelemetry(uint telemetryFrame)
        {
            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireMutationGuard(JobMutationGuardMask))
            {
                _dependencyTelemetryFlags |= TelemetryVaultUnavailableFlag;
                return;
            }

            try
            {
                if (!TryResolveVaultViews(out LootMagnetVaultViews views, requiredCapacity: 0, allowAllocate: false))
                    return;

                RecordTelemetry(telemetryFrame, views);
            }
            finally
            {
                vault.ReleaseMutationGuard(JobMutationGuardMask);
            }
        }

        private void RecordTelemetry(uint telemetryFrame, LootMagnetVaultViews views)
        {
            if (!views.Telemetry.IsCreated || views.Telemetry.Length <= 0)
                return;

            int writeIndex = _telemetryIndex >= 0 && _telemetryIndex < views.Telemetry.Length ? _telemetryIndex : 0;
            views.Telemetry[writeIndex] = new LootMagnetTelemetryEntry
            {
                PlayerAup = _lastPlayerAup,
                SampleLootAup = _activeCount > 0 && views.EntityAups.IsCreated && views.EntityAups.Length > 0 ? views.EntityAups[0] : default,
                Frame = telemetryFrame,
                ActiveCount = (uint)math.max(0, _activeCount),
                ActiveLootPullsCount = _lastActiveLootPullsCount,
                AcquiredCount = _lastCommittedAcquiredCount,
                FlagsHash = _lastCommittedFlagsHash,
                Flags = _lastCommittedFlags,
                PeakMagnetVelocity = _lastPeakMagnetVelocity
            };

            _telemetryIndex = (writeIndex + 1) % views.Telemetry.Length;
            _lastTelemetryRecordedFrame = telemetryFrame;
        }

        private bool CanCommitCompletedJob(in LootMagnetVaultViews views)
        {
            int count = math.min(_scheduledCount, _scheduledCapacity);
            return count >= 0 &&
                   views.EntityFlags.IsCreated &&
                   views.EntityFlags.Length >= count &&
                   views.EntityAups.IsCreated &&
                   views.EntityAups.Length >= count &&
                   views.EntityVelocities.IsCreated &&
                   views.EntityVelocities.Length >= count &&
                   views.EntityItemHashes.IsCreated &&
                   views.EntityItemHashes.Length >= count &&
                   views.EntityQuantities.IsCreated &&
                   views.EntityQuantities.Length >= count &&
                   views.SignalEvents.IsCreated &&
                   views.SignalEvents.Length >= count &&
                   views.Telemetry.IsCreated &&
                   _pickupRefs != null &&
                   _pickupRefs.Length >= count &&
                   _pickupEntityIds != null &&
                   _pickupEntityIds.Length >= count;
        }

        private bool ForceCompletePendingJobForBarrier()
        {
            if (!_pullScheduled)
                return false;

            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobSwap.TryComplete(ref _pullHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            _pullScheduled = false;
            return true;
        }

        private bool ForceCompleteAndCommitScheduledJobForBarrier()
        {
            if (!ForceCompletePendingJobForBarrier())
            {
                ReleaseScheduledVaultGuard();
                return false;
            }

            try
            {
                if (TryResolveVaultViews(out LootMagnetVaultViews views, _scheduledCapacity, allowAllocate: false) &&
                    CanCommitCompletedJob(in views))
                {
                    CommitCompletedPullResultsPostSimulation(views);
                }

                return true;
            }
            finally
            {
                ReleaseScheduledVaultGuard();
            }
        }

        private void DumpTelemetryBuffer()
        {
            if (!TryResolveVaultViews(out LootMagnetVaultViews views, requiredCapacity: 0, allowAllocate: false))
                return;

            DumpTelemetryBuffer(in views);
        }

        private void DumpTelemetryBuffer(in LootMagnetVaultViews views)
        {
            if (!views.Telemetry.IsCreated)
                return;

            const int HeaderBytes = 20;
            const int RowBytes = 128;
            int telemetryLength = views.Telemetry.Length;
            int totalBytes = HeaderBytes + telemetryLength * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(LootMagnetSystem),
                "lootMagnetTelemetryDumpPayload");
            try
            {
                WriteUInt32LittleEndian(payload, 0, TelemetryDumpMagic);
                WriteUInt32LittleEndian(payload, 4, TelemetryDumpVersion);
                WriteInt32LittleEndian(payload, 8, LootMagnetConstants.TelemetryEntrySizeBytes);
                WriteInt32LittleEndian(payload, 12, telemetryLength);
                WriteInt32LittleEndian(payload, 16, _telemetryIndex);
                for (int offset = 0; offset < telemetryLength; offset++)
                {
                    int index = (_telemetryIndex + offset) % telemetryLength;
                    LootMagnetTelemetryEntry entry = views.Telemetry[index];
                    int rowOffset = HeaderBytes + offset * RowBytes;
                    WriteAupPacked48(payload, rowOffset, entry.PlayerAup);
                    WriteAupPacked48(payload, rowOffset + 48, entry.SampleLootAup);
                    WriteUInt32LittleEndian(payload, rowOffset + 96, entry.Frame);
                    WriteUInt32LittleEndian(payload, rowOffset + 100, entry.ActiveCount);
                    WriteUInt32LittleEndian(payload, rowOffset + 104, entry.ActiveLootPullsCount);
                    WriteUInt32LittleEndian(payload, rowOffset + 108, entry.AcquiredCount);
                    WriteUInt32LittleEndian(payload, rowOffset + 112, entry.FlagsHash);
                    WriteUInt32LittleEndian(payload, rowOffset + 116, entry.Flags);
                    WriteFloat32LittleEndian(payload, rowOffset + 120, entry.PeakMagnetVelocity);
                    WriteUInt32LittleEndian(payload, rowOffset + 124, entry.Reserved);
                }

                NativeFaultDumpWriter.TryWriteAll(
                    DumpRelativePath,
                    payload,
                    totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(LootMagnetSystem),
                    "lootMagnetTelemetryDumpPayload");
            }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly LootMagnetSystem _owner;

            public PostSimulationPhaseSystem(LootMagnetSystem owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private static void WriteAupPacked48(NativeArray<byte> payload, int offset, AbsoluteUniversePosition aup)
        {
            WriteInt64LittleEndian(payload, offset, aup.GridX);
            WriteInt64LittleEndian(payload, offset + 8, aup.GridY);
            WriteInt64LittleEndian(payload, offset + 16, aup.GridZ);
            WriteFloat32LittleEndian(payload, offset + 24, aup.LocalX);
            WriteFloat32LittleEndian(payload, offset + 28, aup.LocalY);
            WriteFloat32LittleEndian(payload, offset + 32, aup.LocalZ);
            WriteFloat32LittleEndian(payload, offset + 36, 0f);
            WriteUInt64LittleEndian(payload, offset + 40, 0UL);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteInt64LittleEndian(NativeArray<byte> payload, int offset, long value)
        {
            WriteUInt64LittleEndian(payload, offset, unchecked((ulong)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> payload, int offset, ulong value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
            payload[offset + 4] = (byte)(value >> 32);
            payload[offset + 5] = (byte)(value >> 40);
            payload[offset + 6] = (byte)(value >> 48);
            payload[offset + 7] = (byte)(value >> 56);
        }
    }
}
