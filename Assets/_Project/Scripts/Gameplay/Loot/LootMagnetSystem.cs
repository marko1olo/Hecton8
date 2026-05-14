using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Signals;
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
    public sealed class LootMagnetSystem : MonoBehaviour, IFastTickable, ISlowTickable, ILateFrameTickable
    {
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_PHYS_MAGNETIC_LOOT_ACQUISITION.bin";
        private const string RuntimeObjectName = "[LootMagnetSystem]";
        private const uint TelemetryFaultFlag = 1u;
        private const uint TelemetryAcousticBudgetDropFlag = 1u << 1;
        private const uint TelemetryWakeBudgetDropFlag = 1u << 2;

        private static LootMagnetSystem _bootstrapRuntime;
        private static bool _sceneLoadedHooked;

        [Header("Pull")]
        [SerializeField] private int maxLootEntities = LootMagnetConstants.DefaultMaxEntities;
        [SerializeField] private float pullRadiusMeters = LootMagnetConstants.DefaultPullRadiusMeters;
        [SerializeField] private float pullStrength = LootMagnetConstants.DefaultPullStrength;
        [SerializeField] private float maxVelocityMetersPerSecond = LootMagnetConstants.DefaultMaxVelocityMetersPerSecond;

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private PlayerInventory _inventory;
        private Transform _playerTransform;

        private NativeArray<AbsoluteUniversePosition> _entityAups;
        private NativeArray<uint> _entityFlags;
        private NativeArray<float3> _entityVelocities;
        private NativeArray<uint> _entityItemHashes;
        private NativeArray<ushort> _entityQuantities;
        private NativeArray<LootMagnetSignalEvent> _signalEvents;
        private NativeArray<LootMagnetTelemetryEntry> _telemetry;

        // COLD ALLOC: managed sidecars mirror vault slots only for legacy visual proxy/inventory commit.
        private PickupItem[] _pickupRefs;
        private ulong[] _pickupEntityIds;
        private JobHandle _pullHandle;
        private bool _pullScheduled;
        private bool _registeredFastTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _dumpedFault;
        private int _activeCount;
        private int _scheduledCount;
        private int _scheduledCapacity;
        private int _telemetryIndex;
        private uint _telemetryFrameCounter;
        private uint _lastTelemetryRecordedFrame;
        private uint _frameCounter;
        private byte _scalabilityTier;
        private byte _pendingScalabilityTier;
        private byte _pendingScalabilityTierTicks;
        private bool _scalabilityTierInitialized;
        private AbsoluteUniversePosition _lastPlayerAup;
        private float _scheduledPullRadiusMeters;
        private float _scheduledPullRadiusSq;

        private int Capacity => math.clamp(maxLootEntities, 1, LootMagnetConstants.MaxEntitiesHardCap);

        private bool IsLowTier => _scalabilityTier == 0;

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

            GameObject runtimeRoot = new GameObject(RuntimeObjectName); // COLD ALLOC: GameObject[1] - scene-owned loot magnet scheduler - owner: LootMagnetSystem
            _bootstrapRuntime = runtimeRoot.AddComponent<LootMagnetSystem>();
        }

        private static void EnsureSceneLoadedHook()
        {
            if (_sceneLoadedHooked)
                return;

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
            EnsureSignalEvents();
            EnsureTelemetry();
            if (!_signalEvents.IsCreated || !_telemetry.IsCreated)
                return;

            RefreshDependencies();
            if (EnsureVaultBuffers())
                RefreshPickupVaultFromRegistry();
            TryRegisterTicks();
        }

        private void OnDisable()
        {
            if (ForceCompletePendingJob() && CanCommitCompletedJob())
                CommitVaultResultsToManagedProxies();

            TryUnregisterTicks();
            DisposeSignalEvents();
            DisposeTelemetry();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_bootstrapRuntime, this))
                _bootstrapRuntime = null;
        }

        public void FastTick(float dt)
        {
            if (IsLowTier || _pullScheduled || _activeCount <= 0)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            SchedulePull(math.max(0.0001f, dt), playerAup, lowTierSnap: false);
        }

        public void SlowTick()
        {
            RefreshDependencies();
            if (_pullScheduled)
                return;

            if (!EnsureVaultBuffers())
                return;

            RefreshPickupVaultFromRegistry();
            if (!IsLowTier || _pullScheduled || _activeCount <= 0)
                return;

            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                SchedulePull(0.1f, playerAup, lowTierSnap: true);
        }

        public void LateFrameTick()
        {
            _telemetryFrameCounter++;
            if (!_pullScheduled)
            {
                TryResolvePlayerAup(out _);
                _lastCommittedAcquiredCount = 0u;
                _lastCommittedFlags = 0u;
                RecordTelemetry(_telemetryFrameCounter);
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _pullHandle, forceComplete: false))
                return;

            _pullScheduled = false;
            CommitVaultResultsToManagedProxies();
            if (_lastTelemetryRecordedFrame != _telemetryFrameCounter)
                RecordTelemetry(_telemetryFrameCounter);
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

        private void RefreshDependencies()
        {
            _vault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
            _inventory = _playerContext != null && _playerContext.Inventory != null
                ? _playerContext.Inventory
                : GlobalRegistry.PlayerInventoryRuntime;
            _playerTransform = _playerContext != null ? _playerContext.PlayerTransform : null;
            RefreshScalabilityTier();
        }

        private void RefreshScalabilityTier()
        {
            byte requestedTier = GlobalRegistry.ScalabilityTierProfileByte;
            if (!_scalabilityTierInitialized)
            {
                _scalabilityTier = requestedTier;
                _pendingScalabilityTier = requestedTier;
                _pendingScalabilityTierTicks = 0;
                _scalabilityTierInitialized = true;
                return;
            }

            if (requestedTier == _scalabilityTier)
            {
                _pendingScalabilityTier = requestedTier;
                _pendingScalabilityTierTicks = 0;
                return;
            }

            if (requestedTier != _pendingScalabilityTier)
            {
                _pendingScalabilityTier = requestedTier;
                _pendingScalabilityTierTicks = 1;
                return;
            }

            if (_pendingScalabilityTierTicks < LootMagnetConstants.ScalabilityTierHysteresisSlowTicks)
            {
                _pendingScalabilityTierTicks++;
                return;
            }

            _scalabilityTier = requestedTier;
            _pendingScalabilityTierTicks = 0;
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            int capacity = Capacity;
            EnsureManagedSidecars();
            EnsureSignalEvents();
            _entityAups = vault.GetBuffer<AbsoluteUniversePosition>(
                BufferID.EntityAUPs,
                capacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.ClearMemory);
            _entityFlags = vault.GetBuffer<uint>(
                BufferID.EntityFlags,
                capacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.ClearMemory);
            _entityVelocities = vault.GetBuffer<float3>(
                BufferID.EntityVelocities,
                capacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.ClearMemory);
            _entityItemHashes = vault.GetBuffer<uint>(
                BufferID.EntityItemHashes,
                capacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.ClearMemory);
            _entityQuantities = vault.GetBuffer<ushort>(
                BufferID.EntityQuantities,
                capacity,
                SystemID.GameplayLoot,
                NativeArrayOptions.ClearMemory);
            return _entityAups.IsCreated &&
                   _entityFlags.IsCreated &&
                   _entityVelocities.IsCreated &&
                   _entityItemHashes.IsCreated &&
                   _entityQuantities.IsCreated &&
                   _signalEvents.IsCreated &&
                   _telemetry.IsCreated;
        }

        private void EnsureManagedSidecars()
        {
            int capacity = Capacity;
            if (_pickupRefs != null &&
                _pickupRefs.Length == capacity &&
                _pickupEntityIds != null &&
                _pickupEntityIds.Length == capacity)
            {
                return;
            }

            _pickupRefs = new PickupItem[capacity];
            _pickupEntityIds = new ulong[capacity];
        }

        private void EnsureTelemetry()
        {
            if (_telemetry.IsCreated)
                return;

            _telemetry = H8Memory.Allocate<LootMagnetTelemetryEntry>(
                LootMagnetConstants.TelemetryFrameCount,
                SystemID.GameplayLoot,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LootMagnetTelemetryEntry>[300] - loot magnet black-box ring - owner: LootMagnetSystem
        }

        private void DisposeTelemetry()
        {
            if (!_telemetry.IsCreated)
                return;

            H8Memory.Release(ref _telemetry, SystemID.GameplayLoot);
            _telemetryIndex = 0;
            _lastTelemetryRecordedFrame = 0u;
        }

        private void EnsureSignalEvents()
        {
            int capacity = Capacity;
            if (_signalEvents.IsCreated && _signalEvents.Length == capacity)
                return;

            if (_signalEvents.IsCreated)
            {
                H8Memory.Release(ref _signalEvents, SystemID.GameplayLoot);
            }

            _signalEvents = H8Memory.Allocate<LootMagnetSignalEvent>(
                capacity,
                SystemID.GameplayLoot,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LootMagnetSignalEvent>[capacity] - Burst-to-managed loot signal lane - owner: LootMagnetSystem
        }

        private void DisposeSignalEvents()
        {
            if (!_signalEvents.IsCreated)
                return;

            H8Memory.Release(ref _signalEvents, SystemID.GameplayLoot);
        }

        private void RefreshPickupVaultFromRegistry()
        {
            if (_pullScheduled || !_entityAups.IsCreated || _pickupRefs == null || _pickupEntityIds == null)
                return;

            int capacity = Capacity;
            int registryCount = PickupItem.WorldStateRegistryCount;
            int activeCount = 0;
            for (int registryIndex = 0; registryIndex < registryCount && activeCount < capacity; registryIndex++)
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
                ulong entityId = EntityId.ToULong(pickup.GetEntityId());
                if (_pickupEntityIds[activeCount] != entityId)
                    _entityVelocities[activeCount] = float3.zero;

                _pickupRefs[activeCount] = pickup;
                _pickupEntityIds[activeCount] = entityId;
                _entityAups[activeCount] = AbsoluteUniversePosition.FromRuntimePosition(pickupTransform.position);
                _entityItemHashes[activeCount] = unchecked((uint)pickup.ItemHashId);
                _entityQuantities[activeCount] = (ushort)math.clamp(pickup.Quantity, 1, (int)ushort.MaxValue);
                _entityFlags[activeCount] = LootEntityFlags.Active | LootEntityFlags.IsLoot | LootEntityFlags.PullEnabled;
                activeCount++;
            }

            for (int index = activeCount; index < _activeCount && index < capacity; index++)
            {
                _pickupRefs[index] = null;
                _pickupEntityIds[index] = 0UL;
                _entityAups[index] = default;
                _entityFlags[index] = 0u;
                _entityVelocities[index] = float3.zero;
                _entityItemHashes[index] = 0u;
                _entityQuantities[index] = 0;
            }

            _activeCount = activeCount;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                _lastPlayerAup = playerAup;
                return true;
            }

            playerAup = default;
            Transform playerTransform = _playerTransform;
            if (playerTransform != null)
            {
                playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
                _lastPlayerAup = playerAup;
                return true;
            }

            return false;
        }

        private void SchedulePull(float dt, AbsoluteUniversePosition playerAup, bool lowTierSnap)
        {
            int scheduledCapacity = Capacity;
            int count = math.min(_activeCount, scheduledCapacity);
            if (count <= 0)
                return;

            _scheduledCount = count;
            _scheduledCapacity = scheduledCapacity;
            _frameCounter++;
            float safeRadiusMeters = math.max(LootMagnetConstants.AcquireDistanceMeters, pullRadiusMeters);
            _scheduledPullRadiusMeters = safeRadiusMeters;
            _scheduledPullRadiusSq = safeRadiusMeters * safeRadiusMeters;
            float safeMaxVelocity = math.max(0.01f, maxVelocityMetersPerSecond);
            LootMagnetPullJob job = new LootMagnetPullJob
            {
                PlayerAup = playerAup,
                DeltaTimeSeconds = math.min(
                    math.max(0.0001f, dt),
                    LootMagnetConstants.MaxIntegrationDeltaTimeSeconds),
                PullRadiusSq = _scheduledPullRadiusSq,
                PullStrength = math.max(0f, pullStrength),
                MaxVelocityMetersPerSecond = safeMaxVelocity,
                Frame = _frameCounter,
                LowTierSnap = lowTierSnap ? (byte)1 : (byte)0,
                EntityAups = _entityAups,
                EntityFlags = _entityFlags,
                EntityVelocities = _entityVelocities,
                EntityItemHashes = _entityItemHashes,
                EntityQuantities = _entityQuantities,
                SignalEvents = _signalEvents
            };
            _pullHandle = job.Schedule(count, 64);
            _pullScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private void CommitVaultResultsToManagedProxies()
        {
            int count = math.min(_scheduledCount, _scheduledCapacity);
            uint acquiredCount = 0u;
            uint flagsHash = 2166136261u;
            int acquisitionBudget = LootMagnetConstants.MaxAcquisitionsPerFrame;
            int acousticBudget = ResolveAcousticSignalBudget(_scalabilityTier);
            int wakeBudget = ResolveWakeSignalBudget(_scalabilityTier);
            uint telemetryFlags = 0u;
            bool fault = false;
            for (int index = 0; index < count; index++)
            {
                uint flags = _entityFlags[index];
                flagsHash = (flagsHash ^ flags) * 16777619u;
                flagsHash = (flagsHash ^ _entityItemHashes[index]) * 16777619u;
                if ((flags & LootEntityFlags.NonFinite) != 0u)
                    fault = true;

                PickupItem pickup = _pickupRefs[index];
                LootMagnetSignalEvent signalEvent = _signalEvents.IsCreated ? _signalEvents[index] : default;
                if (pickup == null)
                {
                    ClearVaultSlot(index);
                    continue;
                }

                if ((flags & LootEntityFlags.Acquired) != 0u)
                {
                    if (acquisitionBudget <= 0)
                    {
                        RestoreDeferredAcquisition(index, flags);
                        continue;
                    }

                    acquisitionBudget--;
                    int quantityBefore = math.max(0, pickup.Quantity);
                    pickup.TryHandleInventoryPickup(_inventory, _playerTransform);
                    int quantityAfter = math.max(0, pickup.Quantity);
                    int addedQuantity = math.max(0, quantityBefore - quantityAfter);
                    if (addedQuantity > 0)
                    {
                        acquiredCount++;
                        PublishItemAcquired(in signalEvent, addedQuantity);
                        telemetryFlags |= PublishPresentationSignals(in signalEvent, addedQuantity, ref acousticBudget, ref wakeBudget);
                    }

                    if (quantityAfter > 0)
                    {
                        _entityQuantities[index] = (ushort)math.min(quantityAfter, (int)ushort.MaxValue);
                        _entityFlags[index] = addedQuantity > 0
                            ? (flags & ~LootEntityFlags.Acquired) |
                              LootEntityFlags.Active |
                              LootEntityFlags.IsLoot |
                              LootEntityFlags.PullEnabled
                            : LootEntityFlags.Active | LootEntityFlags.IsLoot;
                    }
                    else
                    {
                        ClearVaultSlot(index);
                    }

                    continue;
                }

                telemetryFlags |= PublishPresentationSignals(in signalEvent, 0, ref acousticBudget, ref wakeBudget);
                if ((flags & LootEntityFlags.Pulling) == 0u || (flags & LootEntityFlags.Active) == 0u)
                    continue;

                float3 runtime = _entityAups[index].ToRuntimeFloat3();
                pickup.transform.position = new Vector3(runtime.x, runtime.y, runtime.z);
            }

            _lastCommittedAcquiredCount = acquiredCount;
            _lastCommittedFlagsHash = flagsHash;
            _lastCommittedFlags = (fault ? TelemetryFaultFlag : 0u) | telemetryFlags;
            if (fault && !_dumpedFault)
            {
                RecordTelemetry(_telemetryFrameCounter);
                _dumpedFault = true;
                DumpTelemetryBuffer();
            }
        }

        private void RestoreDeferredAcquisition(int index, uint flags)
        {
            _entityFlags[index] = (flags & ~(LootEntityFlags.Acquired | LootEntityFlags.Pulling)) |
                                  LootEntityFlags.Active |
                                  LootEntityFlags.IsLoot |
                                  LootEntityFlags.PullEnabled;
        }

        private void ClearVaultSlot(int index)
        {
            _pickupRefs[index] = null;
            _pickupEntityIds[index] = 0UL;
            _entityAups[index] = default;
            _entityFlags[index] = 0u;
            _entityVelocities[index] = float3.zero;
            _entityItemHashes[index] = 0u;
            _entityQuantities[index] = 0;
            if (_signalEvents.IsCreated && index < _signalEvents.Length)
                _signalEvents[index] = default;
        }

        private void PublishItemAcquired(in LootMagnetSignalEvent signalEvent, int addedQuantity)
        {
            if ((signalEvent.Flags & LootMagnetEventFlags.Acquired) == 0u || addedQuantity <= 0)
                return;

            uint itemHash = signalEvent.ItemHash;
            if (itemHash == 0u)
                return;

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
            GlobalSignals.Publish(in itemSignal);
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
            if (wantsAcoustic && acousticBudget <= 0)
                droppedFlags |= TelemetryAcousticBudgetDropFlag;

            if (wantsWake && wakeBudget <= 0)
                droppedFlags |= TelemetryWakeBudgetDropFlag;

            bool publishAcoustic = wantsAcoustic && acousticBudget > 0;
            bool publishWake = wantsWake && wakeBudget > 0;
            if (!publishAcoustic && !publishWake)
                return droppedFlags;

            if (publishAcoustic)
            {
                acousticBudget--;
                float radiusMeters = math.max(LootMagnetConstants.AcquireDistanceMeters, _scheduledPullRadiusMeters);
                float radiusSq = math.max(_scheduledPullRadiusSq, LootMagnetConstants.MinDistanceSq);
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
                GlobalSignals.Publish(in acousticSignal);
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
                GlobalSignals.Publish(in wakeSignal);
            }

            return droppedFlags;
        }

        private static int ResolveAcousticSignalBudget(byte tier)
        {
            if (tier == 0)
                return LootMagnetConstants.LowTierAcousticSignalsPerFrame;

            if (tier >= 3)
                return LootMagnetConstants.UltraTierAcousticSignalsPerFrame;

            return tier >= 2
                ? LootMagnetConstants.HighTierAcousticSignalsPerFrame
                : LootMagnetConstants.DefaultAcousticSignalsPerFrame;
        }

        private static int ResolveWakeSignalBudget(byte tier)
        {
            if (tier == 0)
                return LootMagnetConstants.LowTierWakeSignalsPerFrame;

            if (tier >= 3)
                return LootMagnetConstants.UltraTierWakeSignalsPerFrame;

            return tier >= 2
                ? LootMagnetConstants.HighTierWakeSignalsPerFrame
                : LootMagnetConstants.DefaultWakeSignalsPerFrame;
        }

        private uint _lastCommittedAcquiredCount;
        private uint _lastCommittedFlagsHash;
        private uint _lastCommittedFlags;

        private void RecordTelemetry(uint telemetryFrame)
        {
            if (!_telemetry.IsCreated)
                return;

            int writeIndex = _telemetryIndex;
            _telemetry[writeIndex] = new LootMagnetTelemetryEntry
            {
                PlayerAup = _lastPlayerAup,
                SampleLootAup = _activeCount > 0 && _entityAups.IsCreated && _entityAups.Length > 0 ? _entityAups[0] : default,
                Frame = telemetryFrame,
                ActiveCount = (uint)math.max(0, _activeCount),
                AcquiredCount = _lastCommittedAcquiredCount,
                FlagsHash = _lastCommittedFlagsHash,
                Flags = _lastCommittedFlags
            };

            _telemetryIndex = (writeIndex + 1) % _telemetry.Length;
            _lastTelemetryRecordedFrame = telemetryFrame;
        }

        private bool CanCommitCompletedJob()
        {
            int count = math.min(_scheduledCount, _scheduledCapacity);
            return count >= 0 &&
                   _entityFlags.IsCreated &&
                   _entityFlags.Length >= count &&
                   _entityAups.IsCreated &&
                   _entityAups.Length >= count &&
                   _entityVelocities.IsCreated &&
                   _entityVelocities.Length >= count &&
                   _entityItemHashes.IsCreated &&
                   _entityItemHashes.Length >= count &&
                   _entityQuantities.IsCreated &&
                   _entityQuantities.Length >= count &&
                   _signalEvents.IsCreated &&
                   _signalEvents.Length >= count &&
                   _telemetry.IsCreated &&
                   _pickupRefs != null &&
                   _pickupRefs.Length >= count &&
                   _pickupEntityIds != null &&
                   _pickupEntityIds.Length >= count;
        }

        private bool ForceCompletePendingJob()
        {
            if (!_pullScheduled)
                return false;

            DispatcherJobSwap.TryComplete(ref _pullHandle, forceComplete: true);
            _pullScheduled = false;
            return true;
        }

        private void DumpTelemetryBuffer()
        {
            if (!_telemetry.IsCreated)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(_telemetry.Length);
                writer.Write(_telemetryIndex);
                for (int index = 0; index < _telemetry.Length; index++)
                {
                    LootMagnetTelemetryEntry entry = _telemetry[index];
                    WriteAup(writer, entry.PlayerAup);
                    WriteAup(writer, entry.SampleLootAup);
                    writer.Write(entry.Frame);
                    writer.Write(entry.ActiveCount);
                    writer.Write(entry.AcquiredCount);
                    writer.Write(entry.FlagsHash);
                    writer.Write(entry.Flags);
                }
            }
        }

        private static void WriteAup(BinaryWriter writer, AbsoluteUniversePosition aup)
        {
            writer.Write(aup.GridX);
            writer.Write(aup.GridY);
            writer.Write(aup.GridZ);
            writer.Write(aup.LocalX);
            writer.Write(aup.LocalY);
            writer.Write(aup.LocalZ);
        }
    }
}
