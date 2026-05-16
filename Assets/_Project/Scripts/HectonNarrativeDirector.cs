// ============================================================================
// HECTON-8 - HectonNarrativeDirector.cs
// Stateless narrative tracker for discoveries and depth-tier milestones.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.World;
using UnityEngine;
using Hecton8.Systems.AI;
using Hecton8.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class HectonNarrativeDirector : MonoBehaviour, ISaveable, ISlowTickable, INarrativeEventListener, INarrativePointOfInterestListener, IDirectorAIEventListener, ISpatialTriggerSystem, IOriginShiftListener, IDisposable
    {
        private struct NarrativeNode
        {
            public uint DiscoveryHash;
            public ushort Reserved0;
            public ushort Reserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NarrativeTriggerTelemetryEntry
        {
            public uint Frame;
            public uint PoiHash;
            public ulong StateMask;
            public long PlayerGridX;
            public long PlayerGridY;
            public long PlayerGridZ;
            public float3 PlayerRuntime;
            public float3 PoiRuntime;
            public byte Flags;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct NarrativePoiSpatialCheckJob : IJob
        {
            [ReadOnly] public NativeArray<float3> PoiAups;
            [ReadOnly] public NativeArray<float> PoiRadiiSq;
            [ReadOnly] public NativeArray<uint> PoiHashes;
            [ReadOnly] public NativeArray<long> PoiGridX;
            [ReadOnly] public NativeArray<long> PoiGridZ;
            public NativeArray<byte> PoiState;
            public NativeArray<int> TriggeredIndices;
            public NativeArray<uint> TriggeredHashes;
            public NativeArray<int> TriggeredCount;
            public NativeArray<int> FaultCount;
            public float3 PlayerRuntime;
            public long PlayerGridX;
            public long PlayerGridZ;
            public byte UseDominantAxisPreCull;
            public int PoiCount;

            public void Execute()
            {
                TriggeredCount[0] = 0;
                FaultCount[0] = 0;

                for (int i = 0; i < PoiCount; i++)
                {
                    if (PoiState[i] != 0)
                        continue;

                    long sectorDx = PoiGridX[i] - PlayerGridX;
                    long sectorDz = PoiGridZ[i] - PlayerGridZ;
                    if (sectorDx < -1L || sectorDx > 1L || sectorDz < -1L || sectorDz > 1L)
                        continue;

                    float radiusSq = PoiRadiiSq[i];
                    float3 poiRuntime = PoiAups[i];
                    if (!math.isfinite(radiusSq) || radiusSq <= 0f || !math.all(math.isfinite(poiRuntime)))
                    {
                        FaultCount[0] = FaultCount[0] + 1;
                        continue;
                    }

                    if (UseDominantAxisPreCull != 0)
                    {
                        float3 delta = PlayerRuntime - poiRuntime;
                        float dominantAxis = math.cmax(math.abs(delta));
                        if (dominantAxis * dominantAxis >= radiusSq)
                            continue;
                    }

                    float distanceSq = math.distancesq(PlayerRuntime, poiRuntime);
                    if (!math.isfinite(distanceSq))
                    {
                        FaultCount[0] = FaultCount[0] + 1;
                        continue;
                    }

                    if (distanceSq >= radiusSq)
                        continue;

                    PoiState[i] = 1;
                    int writeIndex = TriggeredCount[0];
                    if (writeIndex < TriggeredIndices.Length)
                    {
                        TriggeredIndices[writeIndex] = i;
                        TriggeredHashes[writeIndex] = PoiHashes[i];
                    }

                    TriggeredCount[0] = writeIndex + 1;
                }
            }
        }

        [Header("Settings")]
#pragma warning disable CS0414 // slowTickRate reserved for future SlowTick throttling
        [SerializeField, UnityEngine.Range(1, 10)] private int slowTickRate = 2;
#pragma warning restore CS0414

        [Header("State")]
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private List<string> discoveredIds = new List<string>(); // COLD ALLOC: 64 for [N] discoveries (reasonable number of narrative discoveries)
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private int currentDepthTier;
        [SerializeField, Sirenix.OdinInspector.ReadOnly] private ulong narrativeAupTriggeredMask;

        private const float SlowTickDeltaSeconds = 0.5f;
        private const float HighTierAupScanIntervalSeconds = 0.5f;
        private const float DefaultAupScanIntervalSeconds = 1f;
        private const float LowTierAupScanIntervalSeconds = 1f;
        private const int NativePoiCapacity = 64;
        private const int BlackBoxCapacity = 300;
        private const int BlackBoxDumpCooldownFrames = 120;
        private const int NarrativeWaypointIdBase = 0x4E545247;
        private const float NarrativeFocusDefaultDurationSeconds = 3.5f;
        private const float NarrativeFocusSubtitleFadeDistanceSq = 1600f;
        private const byte PoiStateUntriggered = 0;
        private const byte PoiStateTriggered = 1;
        private const byte ProgressionSourceNarrativePoi = 1;
        private const byte SaveStateOperationSnapshot = 0;
        private const byte SaveStateOperationTriggered = 1;
        private const string NarrativeWaypointLabel = "Objective";
        private static readonly Color NarrativeWaypointColor = new Color(0.28f, 0.86f, 1f, 1f);
        private static readonly uint _blackBoxFaultWarningHash = NarrativeEvents.ComputeDiscoveryHash("NarrativeTrigger.BlackBoxFault");
        private static readonly uint _blackBoxFaultContextHash = NarrativeEvents.ComputeDiscoveryHash("NARRATIVE_TRIGGER_SYS");
        private static readonly uint _registryOverflowWarningHash = NarrativeEvents.ComputeDiscoveryHash("NarrativeTrigger.RegistryOverflow");

        // COLD ALLOC: string[64] - authored POI ids for hash-to-id narrative event resolution - owner: HectonNarrativeDirector
        private readonly string[] _poiDiscoveryIds = new string[NativePoiCapacity];
        // Runtime-only collection for active POIs in the world.
        // COLD ALLOC: 64 for initial capacity (reasonable number of POIs per scene).
        private readonly List<NarrativeDiscovery> _activePOIs = new List<NarrativeDiscovery>(64);
        private readonly HashSet<string> _discoveredIdLookup = new HashSet<string>(64, System.StringComparer.Ordinal);
        private readonly HashSet<uint> _discoveredHashLookup = new HashSet<uint>(64);
        private NativeHashMap<uint, NarrativeNode> _narrativeNodesByHash;
        private NativeArray<float3> _poiAups;
        private NativeArray<float> _poiRadiiSq;
        private NativeArray<uint> _poiHashes;
        private NativeArray<uint> _poiQuestHashes;
        private NativeArray<uint> _poiBiomeHashes;
        private NativeArray<uint> _poiSoundscapeHashes;
        private NativeArray<uint> _poiLoreHashes;
        private NativeArray<long> _poiGridX;
        private NativeArray<long> _poiGridZ;
        private NativeArray<byte> _poiState;
        private NativeArray<byte> _poiTriggerBits;
        private NativeArray<byte> _poiFlags;
        private NativeArray<int> _triggeredIndices;
        private NativeArray<uint> _triggeredHashes;
        private NativeArray<int> _triggeredCount;
        private NativeArray<int> _faultCount;
        private NativeArray<NarrativeTriggerTelemetryEntry> _blackBox;
        private JobHandle _scanJobHandle;

        private Transform _playerTransform;
        private bool _registeredNarrativeRuntime;
        private bool _registeredSpatialTriggerRuntime;
        private bool _registeredSlowTick;
        private float _aupScanAccumulator = LowTierAupScanIntervalSeconds;
        private bool _aupScannerDisabledForCurrentPoiSet;
        private bool _scanJobScheduled;
        private int _poiCount;
        private int _blackBoxCursor;
        private int _spatialTriggerTickCount;
        private int _lastBlackBoxDumpFrame = -BlackBoxDumpCooldownFrames;
        private uint _lastBiomeHash;
        private AbsoluteUniversePosition _lastScheduledPlayerAup;
        private float3 _lastScheduledPlayerRuntime;
        private bool _hasLastScheduledPlayerPose;

        // Flag: Director zaprosil redkuyu nahodku v tekuschem tike
        // Chitaetsya v diagnostike i buduschey sisteme spavna
#pragma warning disable CS0414
        private bool _rareDiscoveryRequested;
#pragma warning restore CS0414

        public int SavePriority => 5;
        public int LoadPriority => 5;
        public int RegisteredPoiCount => _poiCount;
        public ulong PoiStateMask => narrativeAupTriggeredMask;
        int ISystem.TickCount => _spatialTriggerTickCount;

        public bool TryGetPoiStateMask(out ulong stateMask)
        {
            stateMask = narrativeAupTriggeredMask;
            return true;
        }

        public void PopulateSaveData(SaveData data)
        {
            data.narrativeDepthTier = currentDepthTier;
            data.narrativeAupTriggeredMask = narrativeAupTriggeredMask;
            data.narrativeDiscoveryCount = Mathf.Min(discoveredIds.Count, SaveData.MaxNarrativeDiscoveries);
            
            if (data.narrativeDiscoveryIds == null || data.narrativeDiscoveryIds.Length != SaveData.MaxNarrativeDiscoveries)
            {
                data.narrativeDiscoveryIds = new string[SaveData.MaxNarrativeDiscoveries];
            }

            for (int i = 0; i < data.narrativeDiscoveryCount; i++)
            {
                data.narrativeDiscoveryIds[i] = discoveredIds[i];
            }

            for (int i = data.narrativeDiscoveryCount; i < SaveData.MaxNarrativeDiscoveries; i++)
            {
                data.narrativeDiscoveryIds[i] = null;
            }
        }


        public void LoadFromSaveData(SaveData data)
        {
            currentDepthTier = data.narrativeDepthTier;
            narrativeAupTriggeredMask = data.narrativeAupTriggeredMask;
            _aupScannerDisabledForCurrentPoiSet = false;
            _aupScanAccumulator = LowTierAupScanIntervalSeconds;
            discoveredIds.Clear();

            if (data.narrativeDiscoveryIds != null)
            {
                int count = Mathf.Min(data.narrativeDiscoveryCount, data.narrativeDiscoveryIds.Length);
                for (int i = 0; i < count; i++)
                {
                    string discoveryId = data.narrativeDiscoveryIds[i];
                    if (!string.IsNullOrWhiteSpace(discoveryId))
                        discoveredIds.Add(discoveryId);
                }
            }

            RebuildDiscoveryLookup();
            ApplySavedPoiStateToNativeRegistry();
            PublishPoiStateSignal(0u, -1, SaveStateOperationSnapshot);
            
            // Re-sync world state from loaded data
            NarrativeEvents.RaiseDepthTierReached(currentDepthTier);
        }

        private void Awake()
        {
            HectonNarrativeDirector activeRuntime = GlobalRegistry.NarrativeDirector;
            if (activeRuntime != null && activeRuntime != this)
            {
                Destroy(gameObject);
                return;
            }

            _narrativeNodesByHash = new NativeHashMap<uint, NarrativeNode>(128, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,NarrativeNode>[128] - narrative hash lookup for discovery routing - owner: HectonNarrativeDirector
            NativeMemorySentinel.RegisterNativeHashMap(_narrativeNodesByHash, nameof(HectonNarrativeDirector), nameof(_narrativeNodesByHash), NativeAllocationLifetime.Scene);
            InitializeSpatialNativeStorage();
            RebuildDiscoveryLookup();
        }

        private void OnEnable()
        {
            TryRegister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);

            NarrativeEvents.Register(this);
            NarrativeEvents.RegisterPointOfInterestListener(this);
            DirectorAIEvents.Register(this);
            HectonFloatingOrigin.RegisterListener(this);

            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (Hecton8.Core.GlobalRegistry.SaveRuntime != null)
                Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);

            NarrativeEvents.Unregister(this);
            NarrativeEvents.UnregisterPointOfInterestListener(this);
            DirectorAIEvents.Unregister(this);
            HectonFloatingOrigin.UnregisterListener(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            DirectorAIEvents.Unregister(this);
            HectonFloatingOrigin.UnregisterListener(this);

            if (_narrativeNodesByHash.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(HectonNarrativeDirector), nameof(_narrativeNodesByHash));
                _narrativeNodesByHash.Dispose();
            }

            DisposeSpatialNativeStorage();
        }

        private void TryRegister()
        {
            if (!_registeredNarrativeRuntime)
            {
                GlobalRegistry.RegisterNarrativeDirectorRuntime(this);
                _registeredNarrativeRuntime = ReferenceEquals(GlobalRegistry.NarrativeDirector, this);
            }

            if (!_registeredSpatialTriggerRuntime)
            {
                GlobalRegistry.RegisterSpatialTriggerSystem(this);
                _registeredSpatialTriggerRuntime = ReferenceEquals(GlobalRegistry.SpatialTriggerSystem, this);
            }

            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (_registeredNarrativeRuntime)
            {
                GlobalRegistry.UnregisterNarrativeDirectorRuntime(this);
                _registeredNarrativeRuntime = false;
            }

            if (_registeredSpatialTriggerRuntime)
            {
                GlobalRegistry.UnregisterSpatialTriggerSystem(this);
                _registeredSpatialTriggerRuntime = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }
        }

        public NarrativeDiscovery GetNearestUndiscoveredPOI(Vector3 center, float maxDistance)
        {
            NarrativeDiscovery nearest = null;
            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromRuntimePosition(center);
            double minSqrDist = (double)maxDistance * maxDistance;

            for (int i = 0; i < _activePOIs.Count; i++)
            {
                var poi = _activePOIs[i];
                if (poi == null) continue;

                // Check if already discovered
                uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(poi.DiscoveryId);
                if (discoveryHash != 0u && _discoveredHashLookup.Contains(discoveryHash))
                    continue;

                AbsoluteUniversePosition poiAup = AbsoluteUniversePosition.FromRuntimePosition(poi.transform.position);
                double sqrDist = DistanceSqAup(in poiAup, in centerAup);
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = poi;
                }
            }

            return nearest;
        }

        public void SlowTick()
        {
            _spatialTriggerTickCount = unchecked(_spatialTriggerTickCount + 1);

            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            Vector3 playerPosition = _playerTransform.position;
            float3 playerRuntime = new float3(playerPosition.x, playerPosition.y, playerPosition.z);
            if (!math.all(math.isfinite(playerRuntime)))
            {
                DumpBlackBoxToDisk();
                GlobalTelemetryBus.PublishPerformanceWarning(_blackBoxFaultWarningHash, _blackBoxFaultContextHash, 1f);
                return;
            }

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
            CompleteSpatialJobIfReady(in playerAup, playerRuntime);

            if (ShouldScanAupNarrativeTriggers() && !_scanJobScheduled)
                ScheduleSpatialCheck(in playerAup, playerRuntime);

            float depth = Mathf.Max(0f, (float)-playerAup.ToAbsoluteDouble3().y);
            int newTier = CalculateDepthTier(depth);

            if (newTier <= currentDepthTier)
                return;

            currentDepthTier = newTier;
            NarrativeEvents.RaiseDepthTierReached(currentDepthTier);

            LogDepthTierReached();
        }

        private void InitializeSpatialNativeStorage()
        {
            if (_poiAups.IsCreated)
                return;

            _poiAups = new NativeArray<float3>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[64] - runtime AUP POI positions for Burst spatial checks - owner: HectonNarrativeDirector
            _poiRadiiSq = new NativeArray<float>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[64] - squared POI radii - owner: HectonNarrativeDirector
            _poiHashes = new NativeArray<uint>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[64] - POI progression hashes - owner: HectonNarrativeDirector
            _poiQuestHashes = new NativeArray<uint>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[64] - POI quest hashes - owner: HectonNarrativeDirector
            _poiBiomeHashes = new NativeArray<uint>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[64] - POI biome hashes - owner: HectonNarrativeDirector
            _poiSoundscapeHashes = new NativeArray<uint>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[64] - POI soundscape hashes - owner: HectonNarrativeDirector
            _poiLoreHashes = new NativeArray<uint>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[64] - POI lore hashes - owner: HectonNarrativeDirector
            _poiGridX = new NativeArray<long>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<long>[64] - POI AUP X sectors - owner: HectonNarrativeDirector
            _poiGridZ = new NativeArray<long>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<long>[64] - POI AUP Z sectors - owner: HectonNarrativeDirector
            _poiState = new NativeArray<byte>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[64] - one-shot POI latch state - owner: HectonNarrativeDirector
            _poiTriggerBits = new NativeArray<byte>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[64] - POI save bit indices - owner: HectonNarrativeDirector
            _poiFlags = new NativeArray<byte>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[64] - POI dispatch flags - owner: HectonNarrativeDirector
            _triggeredIndices = new NativeArray<int>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[64] - triggered POI result indices - owner: HectonNarrativeDirector
            _triggeredHashes = new NativeArray<uint>(NativePoiCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[64] - triggered POI result hashes - owner: HectonNarrativeDirector
            _triggeredCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - triggered result counter - owner: HectonNarrativeDirector
            _faultCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - spatial job fault counter - owner: HectonNarrativeDirector
            _blackBox = new NativeArray<NarrativeTriggerTelemetryEntry>(BlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<NarrativeTriggerTelemetryEntry>[300] - POI trigger blackbox ring - owner: HectonNarrativeDirector

            RegisterNativeArray(_poiAups, nameof(_poiAups));
            RegisterNativeArray(_poiRadiiSq, nameof(_poiRadiiSq));
            RegisterNativeArray(_poiHashes, nameof(_poiHashes));
            RegisterNativeArray(_poiQuestHashes, nameof(_poiQuestHashes));
            RegisterNativeArray(_poiBiomeHashes, nameof(_poiBiomeHashes));
            RegisterNativeArray(_poiSoundscapeHashes, nameof(_poiSoundscapeHashes));
            RegisterNativeArray(_poiLoreHashes, nameof(_poiLoreHashes));
            RegisterNativeArray(_poiGridX, nameof(_poiGridX));
            RegisterNativeArray(_poiGridZ, nameof(_poiGridZ));
            RegisterNativeArray(_poiState, nameof(_poiState));
            RegisterNativeArray(_poiTriggerBits, nameof(_poiTriggerBits));
            RegisterNativeArray(_poiFlags, nameof(_poiFlags));
            RegisterNativeArray(_triggeredIndices, nameof(_triggeredIndices));
            RegisterNativeArray(_triggeredHashes, nameof(_triggeredHashes));
            RegisterNativeArray(_triggeredCount, nameof(_triggeredCount));
            RegisterNativeArray(_faultCount, nameof(_faultCount));
            RegisterNativeArray(_blackBox, nameof(_blackBox));
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, nameof(HectonNarrativeDirector), label, NativeAllocationLifetime.Scene);
        }

        private void DisposeSpatialNativeStorage()
        {
            if (_scanJobScheduled)
            {
                _scanJobHandle.Complete();
                _scanJobScheduled = false;
                HandleSpatialJobCompletionFaults();
                if (_triggeredCount.IsCreated)
                    _triggeredCount[0] = 0;
            }

            DisposeNativeArray(ref _poiAups);
            DisposeNativeArray(ref _poiRadiiSq);
            DisposeNativeArray(ref _poiHashes);
            DisposeNativeArray(ref _poiQuestHashes);
            DisposeNativeArray(ref _poiBiomeHashes);
            DisposeNativeArray(ref _poiSoundscapeHashes);
            DisposeNativeArray(ref _poiLoreHashes);
            DisposeNativeArray(ref _poiGridX);
            DisposeNativeArray(ref _poiGridZ);
            DisposeNativeArray(ref _poiState);
            DisposeNativeArray(ref _poiTriggerBits);
            DisposeNativeArray(ref _poiFlags);
            DisposeNativeArray(ref _triggeredIndices);
            DisposeNativeArray(ref _triggeredHashes);
            DisposeNativeArray(ref _triggeredCount);
            DisposeNativeArray(ref _faultCount);
            DisposeNativeArray(ref _blackBox);
            _scanJobScheduled = false;
            _scanJobHandle = default;
            _poiCount = 0;
            _hasLastScheduledPlayerPose = false;
            Array.Clear(_poiDiscoveryIds, 0, _poiDiscoveryIds.Length);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        public void Dispose()
        {
            DisposeSpatialNativeStorage();
        }

        private void RebuildNativePoiRegistry()
        {
            InitializeSpatialNativeStorage();
            _poiCount = 0;
            Array.Clear(_poiDiscoveryIds, 0, _poiDiscoveryIds.Length);
            int validSpatialPoiCount = 0;

            for (int i = 0; i < _activePOIs.Count; i++)
            {
                NarrativeDiscovery poi = _activePOIs[i];
                if (poi == null || !poi.TryGetSpatialTrigger(out NarrativeSpatialTriggerAuthoring trigger))
                    continue;

                validSpatialPoiCount++;
                if (_poiCount >= NativePoiCapacity)
                    continue;

                _poiDiscoveryIds[_poiCount] = poi.DiscoveryId;
                WritePoiSlot(_poiCount, in trigger);
                _poiCount++;
            }

            if (validSpatialPoiCount > NativePoiCapacity)
                GlobalTelemetryBus.PublishPerformanceWarning(_registryOverflowWarningHash, _blackBoxFaultContextHash, validSpatialPoiCount);

            _aupScannerDisabledForCurrentPoiSet = !HasPendingNativePoi();
        }

        private void WritePoiSlot(int index, in NarrativeSpatialTriggerAuthoring trigger)
        {
            AbsoluteUniversePosition aup = trigger.PositionAup;
            _poiAups[index] = aup.ToRuntimeFloat3();
            _poiRadiiSq[index] = math.max(0f, trigger.RadiusSq);
            _poiHashes[index] = trigger.PoiHash;
            _poiQuestHashes[index] = trigger.QuestHash;
            _poiBiomeHashes[index] = trigger.BiomeHash;
            _poiSoundscapeHashes[index] = trigger.SoundscapeHash;
            _poiLoreHashes[index] = trigger.LoreHash;
            _poiGridX[index] = aup.GridX;
            _poiGridZ[index] = aup.GridZ;
            _poiTriggerBits[index] = (byte)math.clamp(trigger.BitIndex, 0, 63);
            _poiFlags[index] = (byte)trigger.Flags;
            _poiState[index] = IsPoiAlreadyTriggered(trigger.PoiHash, trigger.BitIndex)
                ? PoiStateTriggered
                : PoiStateUntriggered;
        }

        private bool IsPoiAlreadyTriggered(uint poiHash, int bitIndex)
        {
            ulong triggerBit = 1UL << math.clamp(bitIndex, 0, 63);
            return (narrativeAupTriggeredMask & triggerBit) != 0UL ||
                   (poiHash != 0u && _discoveredHashLookup.Contains(poiHash));
        }

        private void ApplySavedPoiStateToNativeRegistry()
        {
            if (!_poiState.IsCreated)
                return;

            CompleteSpatialJobForStateOverwrite();

            for (int i = 0; i < _poiCount; i++)
            {
                int bitIndex = _poiTriggerBits[i];
                ulong triggerBit = 1UL << bitIndex;
                uint poiHash = _poiHashes[i];
                bool triggered = (narrativeAupTriggeredMask & triggerBit) != 0UL ||
                                 (poiHash != 0u && _discoveredHashLookup.Contains(poiHash));

                _poiState[i] = triggered ? PoiStateTriggered : PoiStateUntriggered;
                if (triggered)
                    narrativeAupTriggeredMask |= triggerBit;
            }

            _aupScannerDisabledForCurrentPoiSet = !HasPendingNativePoi();
        }

        private bool HasPendingNativePoi()
        {
            if (!_poiState.IsCreated || _poiCount <= 0)
                return false;

            for (int i = 0; i < _poiCount; i++)
            {
                if (_poiState[i] == PoiStateUntriggered)
                    return true;
            }

            return false;
        }

        private void ScheduleSpatialCheck(in AbsoluteUniversePosition playerAup, float3 playerRuntime)
        {
            if (!_poiAups.IsCreated || _poiCount <= 0 || _scanJobScheduled)
                return;

            _lastScheduledPlayerAup = playerAup;
            _lastScheduledPlayerRuntime = playerRuntime;
            _hasLastScheduledPlayerPose = true;

            _scanJobHandle = new NarrativePoiSpatialCheckJob
            {
                PoiAups = _poiAups,
                PoiRadiiSq = _poiRadiiSq,
                PoiHashes = _poiHashes,
                PoiGridX = _poiGridX,
                PoiGridZ = _poiGridZ,
                PoiState = _poiState,
                TriggeredIndices = _triggeredIndices,
                TriggeredHashes = _triggeredHashes,
                TriggeredCount = _triggeredCount,
                FaultCount = _faultCount,
                PlayerRuntime = playerRuntime,
                PlayerGridX = playerAup.GridX,
                PlayerGridZ = playerAup.GridZ,
                UseDominantAxisPreCull = ShouldUseDominantAxisPreCull() ? (byte)1 : (byte)0,
                PoiCount = _poiCount
            }.Schedule();
            _scanJobScheduled = true;
        }

        private static bool ShouldUseDominantAxisPreCull()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
        }

        private void CompleteSpatialJobIfReady(in AbsoluteUniversePosition playerAup, float3 playerRuntime)
        {
            if (!_scanJobScheduled || !_scanJobHandle.IsCompleted)
                return;

            _scanJobHandle.Complete();
            _scanJobScheduled = false;
            HandleSpatialJobCompletionFaults();
            DispatchTriggeredSpatialResults(in playerAup, playerRuntime);
        }

        private void CompleteSpatialJobForRegistryMutation()
        {
            if (!CompleteSpatialJobForDeferredDispatch(out AbsoluteUniversePosition playerAup, out float3 playerRuntime))
                return;

            DispatchTriggeredSpatialResults(in playerAup, playerRuntime);
        }

        private void CompleteSpatialJobForStateOverwrite()
        {
            if (!_scanJobScheduled)
                return;

            _scanJobHandle.Complete();
            _scanJobScheduled = false;
            HandleSpatialJobCompletionFaults();
            if (_triggeredCount.IsCreated)
                _triggeredCount[0] = 0;
        }

        private bool CompleteSpatialJobForDeferredDispatch(
            out AbsoluteUniversePosition playerAup,
            out float3 playerRuntime)
        {
            playerAup = _hasLastScheduledPlayerPose
                ? _lastScheduledPlayerAup
                : default;
            playerRuntime = _hasLastScheduledPlayerPose
                ? _lastScheduledPlayerRuntime
                : float3.zero;

            if (!_scanJobScheduled)
                return false;

            _scanJobHandle.Complete();
            _scanJobScheduled = false;
            HandleSpatialJobCompletionFaults();
            return true;
        }

        private void HandleSpatialJobCompletionFaults()
        {
            if (_faultCount.IsCreated && _faultCount[0] > 0)
            {
                DumpBlackBoxToDisk();
                GlobalTelemetryBus.PublishPerformanceWarning(_blackBoxFaultWarningHash, _blackBoxFaultContextHash, _faultCount[0]);
            }
        }

        private void DispatchTriggeredSpatialResults(in AbsoluteUniversePosition playerAup, float3 playerRuntime)
        {
            if (!_triggeredCount.IsCreated)
                return;

            int triggerCount = math.min(_triggeredCount[0], _triggeredIndices.Length);
            for (int i = 0; i < triggerCount; i++)
            {
                int poiIndex = _triggeredIndices[i];
                if ((uint)poiIndex >= (uint)_poiCount)
                    continue;

                uint poiHash = _triggeredHashes[i] != 0u ? _triggeredHashes[i] : _poiHashes[poiIndex];
                DispatchSpatialTrigger(poiIndex, poiHash, in playerAup, playerRuntime);
            }

            _triggeredCount[0] = 0;
            _aupScannerDisabledForCurrentPoiSet = !HasPendingNativePoi();
        }

        private void DispatchSpatialTrigger(int poiIndex, uint poiHash, in AbsoluteUniversePosition playerAup, float3 playerRuntime)
        {
            if (poiHash == 0u)
                return;

            int bitIndex = _poiTriggerBits[poiIndex];
            narrativeAupTriggeredMask |= 1UL << bitIndex;
            string discoveryId = _poiDiscoveryIds[poiIndex];
            if (string.IsNullOrWhiteSpace(discoveryId))
                NarrativeEvents.RaiseDiscoveryMade(poiHash);
            else
                NarrativeEvents.RaiseDiscoveryMade(discoveryId);
            RecordDiscoveryIdentity(poiHash, discoveryId);

            LoreDatabaseManager loreDatabase = GlobalRegistry.LoreDatabase;
            uint loreHash = _poiLoreHashes[poiIndex];
            if (loreDatabase != null && loreHash != 0u)
                loreDatabase.TryUnlockByHash(loreHash);

            PublishProgressionSignal(poiIndex, poiHash);
            PublishBiomeSignal(poiIndex, poiHash);
            PublishSoundscapeSignal(poiIndex, poiHash);
            PublishNarrativeFocusSignal(poiIndex, poiHash);
            PublishHudWaypointSignal(poiIndex, poiHash);
            PublishPoiStateSignal(poiHash, poiIndex, SaveStateOperationTriggered);
            WriteBlackBoxSample(poiIndex, poiHash, in playerAup, playerRuntime);
        }

        private void PublishProgressionSignal(int poiIndex, uint poiHash)
        {
            ProgressionEventSignal signal = new ProgressionEventSignal
            {
                PositionAup = CreateAupFromPoiSlot(poiIndex),
                PoiHash = poiHash,
                QuestHash = _poiQuestHashes[poiIndex],
                Frame = unchecked((uint)Time.frameCount),
                Source = ProgressionSourceNarrativePoi,
                Flags = _poiFlags[poiIndex]
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishBiomeSignal(int poiIndex, uint poiHash)
        {
            uint biomeHash = _poiBiomeHashes[poiIndex];
            if (biomeHash == 0u || biomeHash == _lastBiomeHash)
                return;

            BiomeChangedSignal signal = new BiomeChangedSignal
            {
                PositionAup = CreateAupFromPoiSlot(poiIndex),
                PreviousBiomeHash = _lastBiomeHash,
                CurrentBiomeHash = biomeHash,
                PoiHash = poiHash,
                Frame = unchecked((uint)Time.frameCount)
            };
            _lastBiomeHash = biomeHash;
            GlobalSignals.Publish(in signal);
        }

        private void PublishSoundscapeSignal(int poiIndex, uint poiHash)
        {
            uint profileHash = _poiSoundscapeHashes[poiIndex];
            if (profileHash == 0u)
                return;

            SoundscapeProfileSignal signal = new SoundscapeProfileSignal
            {
                PositionAup = CreateAupFromPoiSlot(poiIndex),
                ProfileHash = profileHash,
                PoiHash = poiHash,
                Intensity01 = 1f,
                Frame = unchecked((uint)Time.frameCount)
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishNarrativeFocusSignal(int poiIndex, uint poiHash)
        {
            NarrativeFocusSignal signal = new NarrativeFocusSignal
            {
                TargetAup = CreateAupFromPoiSlot(poiIndex),
                FocusHash = poiHash,
                SubtitleHash = _poiLoreHashes[poiIndex] != 0u ? _poiLoreHashes[poiIndex] : poiHash,
                Intensity01 = 1f,
                DurationSeconds = NarrativeFocusDefaultDurationSeconds,
                SubtitleFadeDistanceSq = NarrativeFocusSubtitleFadeDistanceSq,
                Frame = unchecked((uint)Time.frameCount),
                Flags = (byte)(NarrativeFocusSignal.FlagArtifactTarget | NarrativeFocusSignal.FlagWorldSubtitle),
                BoneTarget = 0
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishHudWaypointSignal(int poiIndex, uint poiHash)
        {
            byte flags = _poiFlags[poiIndex];
            if ((flags & (byte)NarrativeSpatialTriggerFlags.HudBreadcrumb) == 0)
                return;

            uint questHash = _poiQuestHashes[poiIndex];
            if (!IsQuestActiveForHud(questHash))
                return;

            AbsoluteUniversePosition poiAup = CreateAupFromPoiSlot(poiIndex);
            NarrativeHudWaypointSignal signal = new NarrativeHudWaypointSignal
            {
                PositionAup = poiAup,
                PoiHash = poiHash,
                QuestHash = questHash,
                Frame = unchecked((uint)Time.frameCount),
                Priority = 1,
                Flags = flags
            };
            GlobalSignals.Publish(in signal);

            IARWaypointService waypoints = GlobalRegistry.ARWaypoints;
            if (waypoints != null && waypoints.IsInitialized)
            {
                float3 runtime = _poiAups[poiIndex];
                waypoints.SetWaypoint(
                    NarrativeWaypointIdBase ^ unchecked((int)poiHash),
                    new Vector3(runtime.x, runtime.y, runtime.z),
                    NarrativeWaypointLabel,
                    NarrativeWaypointColor);
            }
        }

        private bool IsQuestActiveForHud(uint questHash)
        {
            if (questHash == 0u)
                return true;

            IQuestSystem questSystem = GlobalRegistry.QuestSystem;
            if (questSystem == null || !questSystem.IsInitialized)
                return false;

            return questSystem.TryGetQuestIdByHash(questHash, out string questId) &&
                   questSystem.IsActive(questId);
        }

        private void PublishPoiStateSignal(uint poiHash, int poiIndex, byte operation)
        {
            NarrativePoiStateSignal signal = new NarrativePoiStateSignal
            {
                StateMask = narrativeAupTriggeredMask,
                PoiHash = poiHash,
                Frame = unchecked((uint)Time.frameCount),
                PoiIndex = poiIndex >= 0 ? (ushort)poiIndex : (ushort)0,
                Operation = operation,
                Flags = poiIndex >= 0 && _poiFlags.IsCreated ? _poiFlags[poiIndex] : (byte)0
            };
            GlobalSignals.Publish(in signal);
        }

        private AbsoluteUniversePosition CreateAupFromPoiSlot(int poiIndex)
        {
            float3 runtime = _poiAups[poiIndex];
            return AbsoluteUniversePosition.FromRuntimePosition(new Vector3(runtime.x, runtime.y, runtime.z));
        }

        private void WriteBlackBoxSample(int poiIndex, uint poiHash, in AbsoluteUniversePosition playerAup, float3 playerRuntime)
        {
            if (!_blackBox.IsCreated)
                return;

            _blackBox[_blackBoxCursor] = new NarrativeTriggerTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                PoiHash = poiHash,
                StateMask = narrativeAupTriggeredMask,
                PlayerGridX = playerAup.GridX,
                PlayerGridY = playerAup.GridY,
                PlayerGridZ = playerAup.GridZ,
                PlayerRuntime = playerRuntime,
                PoiRuntime = _poiAups[poiIndex],
                Flags = _poiFlags[poiIndex]
            };
            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;
        }

        private void DumpBlackBoxToDisk()
        {
            if (!_blackBox.IsCreated)
                return;

            int frame = Time.frameCount;
            if (frame - _lastBlackBoxDumpFrame < BlackBoxDumpCooldownFrames)
                return;

            _lastBlackBoxDumpFrame = frame;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_NARRATIVE_TRIGGER_SYS.bin");
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BlackBoxCapacity);
                    writer.Write(_blackBoxCursor);
                    for (int i = 0; i < BlackBoxCapacity; i++)
                    {
                        NarrativeTriggerTelemetryEntry entry = _blackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.PoiHash);
                        writer.Write(entry.StateMask);
                        writer.Write(entry.PlayerGridX);
                        writer.Write(entry.PlayerGridY);
                        writer.Write(entry.PlayerGridZ);
                        writer.Write(entry.PlayerRuntime.x);
                        writer.Write(entry.PlayerRuntime.y);
                        writer.Write(entry.PlayerRuntime.z);
                        writer.Write(entry.PoiRuntime.x);
                        writer.Write(entry.PoiRuntime.y);
                        writer.Write(entry.PoiRuntime.z);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (IOException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[Narrative] Failed to dump spatial trigger blackbox.");
#endif
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!_poiAups.IsCreated || _poiCount <= 0)
                return;

            bool dispatchCompletedScan = CompleteSpatialJobForDeferredDispatch(
                out AbsoluteUniversePosition playerAup,
                out float3 playerRuntime);

            float3 shift = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);
            for (int i = 0; i < _poiCount; i++)
            {
                float3 shifted = _poiAups[i] - shift;
                if (!math.all(math.isfinite(shifted)))
                {
                    _poiAups[i] = float3.zero;
                    DumpBlackBoxToDisk();
                    GlobalTelemetryBus.PublishPerformanceWarning(_blackBoxFaultWarningHash, _blackBoxFaultContextHash, i);
                    continue;
                }

                _poiAups[i] = shifted;
            }

            if (dispatchCompletedScan)
            {
                playerRuntime -= shift;
                DispatchTriggeredSpatialResults(in playerAup, playerRuntime);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDepthTierReached()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[Narrative] New depth tier reached.");
#endif
        }

        public bool HasDiscovery(string id)
        {
            uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(id);
            if (discoveryHash != 0u)
                return _discoveredHashLookup.Contains(discoveryHash);

            return !string.IsNullOrWhiteSpace(id) && _discoveredIdLookup.Contains(id);
        }

        public bool HasDiscovery(uint discoveryHash)
        {
            return discoveryHash != 0u && _discoveredHashLookup.Contains(discoveryHash);
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)
                return;

            HandleDiscovery(payload.DiscoveryHash);
        }

        public void OnNarrativePointOfInterestRegistered(NarrativeDiscovery poi)
        {
            if (poi == null)
                return;

            CompleteSpatialJobForRegistryMutation();

            if (!_activePOIs.Contains(poi))
            {
                _activePOIs.Add(poi);
                _aupScannerDisabledForCurrentPoiSet = false;
                _aupScanAccumulator = LowTierAupScanIntervalSeconds;
            }

            RegisterNarrativeNode(poi.DiscoveryId);
            RebuildNativePoiRegistry();
        }

        public void OnNarrativePointOfInterestDisposed(NarrativeDiscovery poi)
        {
            if (poi == null)
                return;

            CompleteSpatialJobForRegistryMutation();
            _activePOIs.Remove(poi);
            _aupScannerDisabledForCurrentPoiSet = false;
            RebuildNativePoiRegistry();
        }

        private bool ShouldScanAupNarrativeTriggers()
        {
            if (_aupScannerDisabledForCurrentPoiSet || _poiCount <= 0)
                return false;

            _aupScanAccumulator += SlowTickDeltaSeconds;
            float scanInterval = ResolveAupScanIntervalSeconds();
            if (_aupScanAccumulator < scanInterval)
                return false;

            _aupScanAccumulator = 0f;
            return true;
        }

        private static float ResolveAupScanIntervalSeconds()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return HighTierAupScanIntervalSeconds;

                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                    return LowTierAupScanIntervalSeconds;

                default:
                    return DefaultAupScanIntervalSeconds;
            }
        }

        private int CalculateDepthTier(float depth)
        {
            if (depth >= 1000f)
                return 4;

            if (depth >= 300f)
                return 3;

            if (depth >= 100f)
                return 2;

            if (depth >= 0f)
                return 1;

            return 0;
        }

        private static double DistanceSqAup(
            in AbsoluteUniversePosition a,
            in AbsoluteUniversePosition b)
        {
            double3 origin = a.ToAbsoluteDouble3();
            double3 target = b.ToAbsoluteDouble3();
            return math.distancesq(origin, target);
        }

        private void HandleDiscovery(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return;

            if (_narrativeNodesByHash.IsCreated && !_narrativeNodesByHash.ContainsKey(discoveryHash))
            {
                if (NarrativeEvents.TryResolveDiscoveryId(discoveryHash, out string unresolvedId))
                    RegisterNarrativeNode(unresolvedId);
            }

            NarrativeEvents.TryResolveDiscoveryId(discoveryHash, out string id);
            RecordDiscoveryIdentity(discoveryHash, id);
        }

        private void RecordDiscoveryIdentity(uint discoveryHash, string discoveryId)
        {
            if (discoveryHash == 0u)
                return;

            if (_discoveredHashLookup.Add(discoveryHash))
                MarkNativePoiTriggeredByHash(discoveryHash);

            if (string.IsNullOrWhiteSpace(discoveryId) || !_discoveredIdLookup.Add(discoveryId))
                return;

            RegisterNarrativeNode(discoveryId);
            discoveredIds.Add(discoveryId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Discovery Logged: {discoveryId}");
#endif
        }

        private void HandleRareDiscoveryRequest(Vector3 hintPosition)
        {
            _rareDiscoveryRequested = true;

            FirstHourDirector firstHourDirector = Hecton8.Core.GlobalRegistry.FirstHour;
            if (firstHourDirector != null &&
                !firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstModule))
            {
                return;
            }

            AtlasSignalSystem atlasSignalSystem = Hecton8.Core.GlobalRegistry.AtlasSignal;
            bool rebroadcastedPulse = CanRebroadcastAtlasRareDiscoveryPulse(atlasSignalSystem);
            if (rebroadcastedPulse)
                AtlasSignalEvents.RaisePulse(atlasSignalSystem.CurrentStrength);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Rare Discovery requested near {hintPosition}. " +
                      $"Atlas-6 signal {(rebroadcastedPulse ? "pulse triggered" : "pulse skipped")} (strength: " +
                      $"{(Hecton8.Core.GlobalRegistry.AtlasSignal != null ? Hecton8.Core.GlobalRegistry.AtlasSignal.CurrentStrength : 0f):F2}).");
#endif
        }

        void IDirectorAIEventListener.OnDirectorSpawnHordeRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorEquipmentGlitchRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorRareDiscoveryRequested(Vector3 position)
        {
            HandleRareDiscoveryRequest(position);
        }

        void IDirectorAIEventListener.OnDirectorWeatherShiftRequested(float intensity)
        {
        }

        void IDirectorAIEventListener.OnDirectorMissionTriggerRequested(Vector3 position)
        {
        }

        void IDirectorAIEventListener.OnDirectorPredatorPressureChanged(bool pressureEnabled)
        {
        }

        void IDirectorAIEventListener.OnDirectorThreatSpike(Vector3 position, float intensity)
        {
        }

        private static bool CanRebroadcastAtlasRareDiscoveryPulse(AtlasSignalSystem atlasSignalSystem)
        {
            return atlasSignalSystem != null &&
                   atlasSignalSystem.CurrentRevealStage >= 3 &&
                   atlasSignalSystem.CurrentStrength > 0f;
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            _playerTransform = playerTransform;
            return true;
        }

        private void RebuildDiscoveryLookup()
        {
            _discoveredIdLookup.Clear();
            _discoveredHashLookup.Clear();
            if (_narrativeNodesByHash.IsCreated)
                _narrativeNodesByHash.Clear();
            if (discoveredIds == null)
                return;

            int writeIndex = 0;
            for (int i = 0; i < discoveredIds.Count; i++)
            {
                string discoveryId = discoveredIds[i];
                if (string.IsNullOrWhiteSpace(discoveryId) || !_discoveredIdLookup.Add(discoveryId))
                    continue;

                uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
                if (discoveryHash != 0u)
                {
                    _discoveredHashLookup.Add(discoveryHash);
                    RegisterNarrativeNode(discoveryId);
                }

                if (writeIndex != i)
                    discoveredIds[writeIndex] = discoveryId;

                writeIndex++;
            }

            if (writeIndex < discoveredIds.Count)
                discoveredIds.RemoveRange(writeIndex, discoveredIds.Count - writeIndex);
        }

        private void RegisterNarrativeNode(string discoveryId)
        {
            if (!_narrativeNodesByHash.IsCreated)
                return;

            uint discoveryHash = NarrativeEvents.ComputeDiscoveryHash(discoveryId);
            if (discoveryHash == 0u || _narrativeNodesByHash.ContainsKey(discoveryHash))
                return;

            _narrativeNodesByHash.TryAdd(discoveryHash, new NarrativeNode
            {
                DiscoveryHash = discoveryHash,
                Reserved0 = 0,
                Reserved1 = 0
            });
        }

        private void MarkNativePoiTriggeredByHash(uint discoveryHash)
        {
            if (!_poiHashes.IsCreated || discoveryHash == 0u)
                return;

            CompleteSpatialJobForRegistryMutation();

            for (int i = 0; i < _poiCount; i++)
            {
                if (_poiHashes[i] != discoveryHash || _poiState[i] == PoiStateTriggered)
                    continue;

                _poiState[i] = PoiStateTriggered;
                narrativeAupTriggeredMask |= 1UL << _poiTriggerBits[i];
                PublishPoiStateSignal(discoveryHash, i, SaveStateOperationTriggered);
                break;
            }
        }
    }
}
