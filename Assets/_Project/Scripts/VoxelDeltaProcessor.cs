using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Caves
{
    [StructLayout(LayoutKind.Sequential, Pack = 2, Size = 8)]
    public struct VoxelModifiedCell
    {
        public half Density;
        public byte MaterialId;
        public byte Flags;
        public ushort Reserved;
        public ushort Reserved1;
    }

    /// <summary>
    /// Authoritative absolute-universe thermal melt request produced by lava/vent gameplay.
    /// </summary>
    public struct ThermalMeltEvent
    {
        public Vector3 AbsoluteUniversePosition;
        public double3 AbsoluteUniversePositionDouble;
        public float RadiusMeters;
        public float Heat01;
    }

    public enum VoxelCarveOperationType : byte
    {
        Subtract = 0,
        Add = 1,
        Replace = 2
    }

    public enum VoxelCarveShapeType : byte
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2
    }

    /// <summary>
    /// Blittable carve ingress packet. Coordinates are absolute-universe meters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct VoxelCarveEvent : ISignal
    {
        public ulong VolumeInstanceId;
        public float3 AbsoluteHitPoint;
        public float3 AbsoluteSegmentEnd;
        public float3 AbsoluteHalfExtents;
        public float3 AbsoluteImpulseDirection;
        public double3 AbsoluteHitPointDouble;
        public double3 AbsoluteSegmentEndDouble;
        public float RadiusMeters;
        public float BlendStrengthMeters;
        public byte Operation;
        public byte Shape;
        public byte MaterialId;
        public byte SourceFlags;
    }

    /// <summary>
    /// Owns carved voxel-cell deltas, save/load projection, and deferred carve batching for runtime voxel volumes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HectonVoxelEngine))]
    public sealed class VoxelDeltaProcessor : MonoBehaviour, ISaveable, IUpdatable, ILateFrameTickable
    {
        private const int ChunkResolution = 32;
        private const int ChunkCellCount = VoxelDeltaChunkDTO.CellCount;
        private const int ChunkDirtyMaskWordCount = VoxelDeltaChunkDTO.DirtyMaskWordCount;
        private const int InitialChunkRegistryCapacity = 256;
        private const int InitialVolumeRegistryCapacity = 16;
        private const int InitialPendingCarveCapacity = 32;
        private const int InitialCarveEventQueueCapacity = 64;
        private const int InitialPendingCompactionCapacity = 16;
        private const int VoxelBlackBoxCapacity = 300;
        private const int PendingCarveMask = InitialPendingCarveCapacity - 1;
        private const int PendingCompactionMask = InitialPendingCompactionCapacity - 1;
        private const int MaxActiveThermalMeltEvents = 16;
        private const int MaxScheduledCarveCommitWritesPerFrame = 64;
        private const int CompactionFrostTickIntervalFrames = 300;
        private const int MaxLaserCarveAxisCells = 8;
        private const int ChunkCompactionDirtyThreshold = (ChunkCellCount * 4) / 5;
        private const int MortonSignedOffset = 1 << 20;
        private const float MinRuntimeVoxelSize = 0.25f;
        private const float MinCarveRadiusMeters = 0.9f;
        private const float MaxCarveRadiusMeters = 4f;
        private const float ThermalMeltDurationSeconds = 5f;
        private const float ThermalMeltStepIntervalSeconds = 0.25f;
        private const float ThermalMeltMinimumHeat = 0.01f;
        private const float SphereVolumeFactor = 4f / 3f * math.PI;
        private const float SparseRleSdfByteScale = 127f / 8f;
        private const float SparseRleSdfByteInvScale = 8f / 127f;
        private const float DirectionDiagonal2 = 0.70710677f;
        private const float DirectionDiagonal3 = 0.57735026f;
        private const int LaserDebrisMinFragments = 3;
        private const int LaserDebrisMaxFragments = 5;
        private const float LaserDebrisLifetimeSeconds = 5f;
        private const float LaserCutHeatLifetimeSeconds = 2f;
        private const float LaserCutHeatRadiusScale = 1.6f;
        private const float LaserCutHeatStrength = 1f;
        private const int RecentCutHeatMax = 16;
        private const double CarveCommitWarningMs = 0.2d;
        private const byte DefaultMaterialId = 0;
        private const byte ThermalMeltMaterialId = 2;
        private const byte DeltaModeAdditive = 1 << 0;
        private const byte DeltaModeReplace = 1 << 1;
        private const byte CarveSourceLaser = 1 << 0;
        private const byte DeltaShapeSphere = 0;
        private const byte DeltaShapeBox = 1;
        private const byte DeltaShapeCapsule = 2;
        private const int NativeSnapshotMagic = unchecked((int)0x48584432);
        private const int NativeSnapshotRleMagic = unchecked((int)0x48584433);
        private const int NativeSnapshotDeltaRleMagic = unchecked((int)0x48584434);
        private const byte NativeSnapshotStorageDense = 0;
        private const byte NativeSnapshotStorageUniformSdfRle = 1 << 0;
        private const byte NativeSnapshotStorageSparseDeltaRle = 1 << 1;
        private const int NativeSnapshotUniformSdfRlePayloadBytes = sizeof(byte);
        private const int NativeSnapshotLegacyUniformSdfRlePayloadBytes = sizeof(ushort);
        private const uint SaveCorruptionHashMismatchAction = 1u;
        private const uint SaveCorruptionBoundsAction = 2u;
        private const uint SaveCorruptionMalformedRleAction = 3u;
        private const uint VoxelBlackBoxDumpMagic = 0x564F5844u; // "VOXD"
        private const uint VoxelBlackBoxInvalidCarveEventFlag = 1u << 0;
        private const uint VoxelBlackBoxQueueOverflowFlag = 1u << 1;
        private const uint VoxelBlackBoxInvalidPendingCarveFlag = 1u << 2;
        private const uint VoxelBlackBoxCommitBudgetFlag = 1u << 3;
        private const string VoxelBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin";
        private const string NativeMemoryOwner = nameof(VoxelDeltaProcessor);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly uint _VoxelDebrisSignalHash = unchecked((uint)Hecton.Localization.LocHash.Compute("voxel.debris.carve"));
        private static readonly ProfilerMarker _carveScheduleProfilerMarker = new ProfilerMarker("H8.VoxelDelta.ScheduleCarve");
        private static readonly ProfilerMarker _carveCommitProfilerMarker = new ProfilerMarker("H8.VoxelDelta.CommitCarve");
        private static readonly uint _CarveCommitWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.CarveCommitBudgetExceeded"));
        private static readonly uint _CarveCommitTelemetryContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.TryCommitScheduledCarve"));
        private static readonly uint _SaveCorruptionHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SAVE_CORRUPTION_HASH"));
        private static readonly uint _SaveCorruptionContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.LoadSparseRle"));
        private static readonly int _laserHitAupId = Shader.PropertyToID("_LaserHitAup");
        private static readonly int _laserHitHeatId = Shader.PropertyToID("_LaserHitHeat");
        private static readonly int _recentCutHeatPositionRadiusId = Shader.PropertyToID("_HectonRecentCutHeatPositionRadius");
        private static readonly int _recentCutHeatStrengthTimeId = Shader.PropertyToID("_HectonRecentCutHeatStrengthTime");
        private static readonly int _recentCutHeatCountId = Shader.PropertyToID("_HectonRecentCutHeatCount");
        // COLD ALLOC: Vector4[16] - shader heat ring position-radius upload - owner: VoxelDeltaProcessor
        private static readonly Vector4[] s_recentCutHeatPositionRadius = new Vector4[RecentCutHeatMax];
        // COLD ALLOC: Vector4[16] - shader heat ring strength-time upload - owner: VoxelDeltaProcessor
        private static readonly Vector4[] s_recentCutHeatStrengthTime = new Vector4[RecentCutHeatMax];
        private static int s_recentCutHeatCursor;
        private static int s_recentCutHeatCount;
        [Header("Debris Aftermath")]
        [Tooltip("Optional dropped-item payload spawned from carved voxel mass. Leave empty to disable persistent debris aftermath.")]
        [SerializeField] private ItemData carveDebrisItem;
        [Tooltip("Debris entities spawned per cubic meter of removed sphere volume.")]
        [SerializeField, Min(0f)] private float carveDebrisPerCubicMeter = 0.3f;
        [Tooltip("Upper bound on debris entities emitted from a single carve commit.")]
        [SerializeField, Range(0, 16)] private int carveDebrisMaxCount = 8;
        [Tooltip("Impulse magnitude applied to each debris entity when the carve aftermath hydrates nearby.")]
        [SerializeField, Min(0f)] private float carveDebrisImpulse = 2.5f;
        [Tooltip("Optional pooled transient debris profile for laser voxel cuts. Profile should author 3-5 small chunks.")]
        [SerializeField] private OrganicDebrisProfile laserCarveDebrisProfile;

        private HectonVoxelEngine _engine;
        private ISimulationBucketer _simulationBucketer;
        private bool _saveRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;

        // COLD ALLOC: Dictionary<ChunkAddress, ChunkDeltaState>[InitialChunkRegistryCapacity] - persistent voxel delta chunk registry - owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, ChunkDeltaState> _chunkStates = new Dictionary<ChunkAddress, ChunkDeltaState>(InitialChunkRegistryCapacity);
        // COLD ALLOC: Dictionary<ChunkAddress, CompactedChunkState>[InitialChunkRegistryCapacity] - compacted replacement SDF chunk registry - owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, CompactedChunkState> _compactedChunkStates = new Dictionary<ChunkAddress, CompactedChunkState>(InitialChunkRegistryCapacity);
        // COLD ALLOC: Dictionary<ChunkAddress, int>[InitialChunkRegistryCapacity] - dirty chunk write version registry for compaction conflict checks - owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, int> _chunkWriteVersions = new Dictionary<ChunkAddress, int>(InitialChunkRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] - live voxel volume registry for load-time rebuild dispatch - owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _registeredVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] - pending volume rebuild queue after loaded delta application - owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _pendingRebuildVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: PendingCarveRequest[InitialPendingCarveCapacity] - deferred plasma-cut carve staging buffer - owner: VoxelDeltaProcessor
        private readonly PendingCarveRequest[] _pendingCarves = new PendingCarveRequest[InitialPendingCarveCapacity];
        // COLD ALLOC: ThermalMeltRuntime[16] - bounded lava crater-expansion requests - owner: VoxelDeltaProcessor
        private readonly ThermalMeltRuntime[] _thermalMeltEvents = new ThermalMeltRuntime[MaxActiveThermalMeltEvents];
        private int _pendingCarveHead;
        private int _pendingCarveCount;
        private NativeQueue<VoxelCarveEvent> _queuedCarveEvents;
        private int _queuedCarveEventCount;
        private int _thermalMeltCount;
        private JobHandle _scheduledCarveHandle;
        private bool _scheduledCarveRunning;
        private PendingCarveRequest _scheduledCarveRequest;
        private int _scheduledCarveWriteCount;
        private bool _scheduledCarveCommitPending;
        private int _scheduledCarveCommitIndex;
        private bool _carveCommitWarningArmed;
        // COLD ALLOC: NativeArray<VoxelCarveTelemetryEntry>[300] - fixed voxel carving black-box ring - owner: VoxelDeltaProcessor
        private NativeArray<VoxelCarveTelemetryEntry> _blackBox;
        private int _blackBoxCursor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _blackBoxDumpedThisActivation;
#endif
        private int3 _scheduledCarveTouchedMinCell;
        private int3 _scheduledCarveTouchedMaxCell;
        private bool _scheduledCarveTouchedAnyCell;
        // COLD ALLOC: NativeArray<CarveCellWrite>[capacity] - staged Burst carve results before managed delta-chunk commit - owner: VoxelDeltaProcessor
        private NativeArray<CarveCellWrite> _scheduledCarveWrites;
        // COLD ALLOC: PendingCompactionRequest[16] - bounded background dirty-chunk compaction queue - owner: VoxelDeltaProcessor
        private readonly PendingCompactionRequest[] _pendingCompactions = new PendingCompactionRequest[InitialPendingCompactionCapacity];
        private int _pendingCompactionHead;
        private int _pendingCompactionCount;
        private int _compactionFrostTickCounter;
        private JobHandle _scheduledCompactionHandle;
        private bool _scheduledCompactionRunning;
        private ScheduledCompactionRequest _scheduledCompactionRequest;
        public int SavePriority => 40;

        public int LoadPriority => 30;

        private bool IsScheduledCarveBusy => _scheduledCarveRunning || _scheduledCarveCommitPending;

        private void OnEnable()
        {
            _engine = GetComponent<HectonVoxelEngine>();
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
            EnsureCarveEventQueue();
            EnsureBlackBox();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _blackBoxDumpedThisActivation = false;
#endif

            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
            {
                TryRegisterSaveService();
                return;
            }

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }

            TryRegisterSaveService();
        }

        private void OnDisable()
        {
            DisposeCarveEventQueue();
            DisposeBlackBox();
            DisposeScheduledCarveBuffers();
            DisposeScheduledCompactionBuffers();
            _simulationBucketer = null;
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_saveRegistered && GlobalRegistry.Save != null)
            {
                GlobalRegistry.Save.Unregister(this);
                _saveRegistered = false;
            }

            _pendingCarveCount = 0;
            _pendingCarveHead = 0;
            _queuedCarveEventCount = 0;
            _pendingCompactionCount = 0;
            _pendingCompactionHead = 0;
            _compactionFrostTickCounter = 0;
            _thermalMeltCount = 0;
            _pendingRebuildVolumes.Clear();
            _registeredVolumes.Clear();
            DisposeChunkStates();
            DisposeCompactedChunkStates();
            ResetRecentCutHeatState();
        }

        /// <summary>
        /// Flushes staged carve requests and deferred load-time rebuild requests on the registry dispatcher lane.
        /// </summary>
        /// <param name="deltaTime">Unused dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            TryRegisterSaveService();
            DrainQueuedCarveEvents();
            AdvanceThermalMeltEvents(deltaTime);
            TrySchedulePendingCarve();
            TrySchedulePendingCompactionFrostTick();
            FlushPendingRebuilds();
            WriteBlackBoxSample(0ul, 0u);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryCommitScheduledCarve();
            TryCommitScheduledCompaction();
        }

        internal static byte DebugSubtractiveDeltaMode => 0;
        internal static byte DebugAdditiveDeltaMode => DeltaModeAdditive;

        internal static float DebugMergeSdfDensity(
            float existingValue,
            byte existingFlags,
            float nextValue,
            byte nextFlags)
        {
            return MergeSdfDeltaDensity(existingValue, existingFlags, nextValue, nextFlags);
        }

        internal static float DebugBakeDeltaIntoBaseDensity(float baseValue, float deltaValue, byte deltaFlags)
        {
            return BakeDeltaIntoBaseDensity(baseValue, deltaValue, deltaFlags);
        }

        internal static bool DebugShouldReplaceQueuedCompaction(int requestDirtyCount, int candidateDirtyCount)
        {
            return ShouldReplaceQueuedCompaction(requestDirtyCount, candidateDirtyCount);
        }

        internal static float DebugResolveThermalMeltProgress(float elapsedSeconds)
        {
            return ResolveThermalMeltProgress(elapsedSeconds);
        }

        internal static int DebugResolveQueuedCarveDrainBudget(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return 4;
                case HectonQualityTier.Mid:
                    return 2;
                default:
                    return 1;
            }
        }

        internal static int DebugVoxelBlackBoxCapacity => VoxelBlackBoxCapacity;
        internal static int DebugVoxelBlackBoxEntryBytes => UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>();
        internal static bool DebugIsFiniteCarveEvent(in VoxelCarveEvent carveEvent)
        {
            return IsFiniteCarveEvent(in carveEvent);
        }

        /// <summary>
        /// Registers a live voxel volume for load-time delta rebuild dispatch.
        /// </summary>
        /// <param name="volume">Runtime volume.</param>
        public void RegisterVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                if (ReferenceEquals(_registeredVolumes[i], volume))
                    return;
            }

            _registeredVolumes.Add(volume);
            if (HasOverlappingDelta(volume))
                volume.RequestDeltaRebuild();
        }

        /// <summary>
        /// Unregisters a live voxel volume from delta rebuild dispatch.
        /// </summary>
        /// <param name="volume">Runtime volume.</param>
        public void UnregisterVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            RemoveVolume(_registeredVolumes, volume);
            RemoveVolume(_pendingRebuildVolumes, volume);
        }

        /// <summary>
        /// Applies a validated mod SDF operation through the registered live-volume lane.
        /// </summary>
        /// <param name="runtimeCenter">Frame-space command center.</param>
        /// <param name="radius">Sphere radius in meters.</param>
        /// <param name="additive">True for Add/weld; false for Subtract/carve.</param>
        /// <returns>True when one registered volume accepted the operation.</returns>
        public bool TryApplyModSdfModify(Vector3 runtimeCenter, float radius, bool additive)
        {
            if (radius <= 0f || _registeredVolumes.Count <= 0)
                return false;

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume == null || !volume.HasRuntimeData)
                    continue;

                if (volume.TryApplyModSdfModify(runtimeCenter, radius, additive))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Queues a bounded five-second lava/vent crater expansion in absolute-universe coordinates.
        /// </summary>
        /// <param name="meltEvent">Absolute melt request.</param>
        /// <returns>True when one live volume accepted the melt request.</returns>
        public bool AcceptThermalMeltEvent(in ThermalMeltEvent meltEvent)
        {
            float radius = math.max(MinCarveRadiusMeters, meltEvent.RadiusMeters);
            float heat01 = math.saturate(meltEvent.Heat01);
            if (radius <= 0f || heat01 < ThermalMeltMinimumHeat || _registeredVolumes.Count <= 0)
                return false;

            double3 absolutePosition = ResolveThermalMeltPositionDouble(in meltEvent);
            HectonVoxelVolume targetVolume = ResolveThermalMeltVolume(absolutePosition, radius);
            if (targetVolume == null)
                return false;

            for (int i = 0; i < _thermalMeltCount; i++)
            {
                ThermalMeltRuntime existing = _thermalMeltEvents[i];
                if (!ReferenceEquals(existing.Volume, targetVolume))
                    continue;

                float mergeRadius = math.max(radius, existing.RadiusMeters);
                if (math.lengthsq(existing.AbsoluteCenter - absolutePosition) > (double)mergeRadius * mergeRadius)
                    continue;

                existing.AbsoluteCenter = (existing.AbsoluteCenter + absolutePosition) * 0.5d;
                existing.RadiusMeters = math.max(existing.RadiusMeters, radius);
                existing.ElapsedSeconds = math.min(existing.ElapsedSeconds, ThermalMeltStepIntervalSeconds);
                _thermalMeltEvents[i] = existing;
                return true;
            }

            if (_thermalMeltCount >= _thermalMeltEvents.Length)
                return false;

            _thermalMeltEvents[_thermalMeltCount++] = new ThermalMeltRuntime
            {
                Volume = targetVolume,
                AbsoluteCenter = absolutePosition,
                RadiusMeters = radius,
                ElapsedSeconds = 0f,
                StepAccumulatorSeconds = ThermalMeltStepIntervalSeconds
            };
            return true;
        }

        private HectonVoxelVolume ResolveThermalMeltVolume(double3 absoluteCenter, float radius)
        {
            float bestDistanceSq = float.MaxValue;
            HectonVoxelVolume bestVolume = null;
            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume == null || !volume.HasRuntimeData || volume.GridDimension <= 0 || volume.VoxelSize <= 0f)
                    continue;

                float halfExtent = volume.GridDimension * volume.VoxelSize * 0.5f;
                float acceptedRadius = halfExtent + radius;
                double3 delta = volume.GenerationAbsoluteUniversePositionDouble - absoluteCenter;
                double distanceSq = math.lengthsq(delta);
                double acceptedRadiusSq = (double)acceptedRadius * acceptedRadius;
                if (distanceSq > acceptedRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = (float)math.min(distanceSq, float.MaxValue);
                bestVolume = volume;
            }

            return bestVolume;
        }

        private void AdvanceThermalMeltEvents(float deltaTime)
        {
            if (_thermalMeltCount <= 0)
                return;

            float safeDelta = math.max(0f, deltaTime);
            for (int i = 0; i < _thermalMeltCount;)
            {
                ThermalMeltRuntime melt = _thermalMeltEvents[i];
                if (melt.Volume == null || !melt.Volume.HasRuntimeData)
                {
                    RemoveThermalMeltAt(i);
                    continue;
                }

                melt.ElapsedSeconds += safeDelta;
                melt.StepAccumulatorSeconds += safeDelta;
                bool expired = melt.ElapsedSeconds >= ThermalMeltDurationSeconds;
                if (melt.StepAccumulatorSeconds >= ThermalMeltStepIntervalSeconds && !expired)
                {
                    if (TryStageThermalMeltStep(in melt))
                        melt.StepAccumulatorSeconds = 0f;
                }

                if (expired)
                    RemoveThermalMeltAt(i);
                else
                {
                    _thermalMeltEvents[i] = melt;
                    i++;
                }
            }
        }

        private bool TryStageThermalMeltStep(in ThermalMeltRuntime melt)
        {
            float progress = ResolveThermalMeltProgress(melt.ElapsedSeconds);
            float radius = math.max(MinCarveRadiusMeters, melt.RadiusMeters * progress);
            float strength = math.max(MinRuntimeVoxelSize, radius * 0.35f);
            return TryEnqueuePendingCarve(new PendingCarveRequest
            {
                Volume = melt.Volume,
                AbsoluteHitPoint = melt.AbsoluteCenter,
                ExplicitRadiusMeters = radius,
                ExplicitBlendStrength = strength,
                MaterialId = ThermalMeltMaterialId,
                DeltaFlags = 0,
                Shape = DeltaShapeSphere
            });
        }

        private void RemoveThermalMeltAt(int index)
        {
            if (index < 0 || index >= _thermalMeltCount)
                return;

            for (int i = index + 1; i < _thermalMeltCount; i++)
                _thermalMeltEvents[i - 1] = _thermalMeltEvents[i];

            _thermalMeltEvents[_thermalMeltCount - 1] = default;
            _thermalMeltCount--;
        }

        private static float ResolveThermalMeltProgress(float elapsedSeconds)
        {
            float t = math.saturate(elapsedSeconds / ThermalMeltDurationSeconds);
            return t * t * (3f - 2f * t);
        }

        private static int ResolvePendingCarveSlot(int head, int logicalIndex)
        {
            return (head + logicalIndex) & PendingCarveMask;
        }

        private static int ResolvePendingCompactionSlot(int head, int logicalIndex)
        {
            return (head + logicalIndex) & PendingCompactionMask;
        }

        private void DropOldestPendingCarve()
        {
            if (_pendingCarveCount <= 0)
                return;

            _pendingCarves[_pendingCarveHead] = default;
            _pendingCarveHead = (_pendingCarveHead + 1) & PendingCarveMask;
            _pendingCarveCount--;
        }

        private bool TryReservePendingCarveSlot(bool dropOldestWhenFull)
        {
            if (_pendingCarveCount < _pendingCarves.Length)
                return true;

            if (!IsScheduledCarveBusy)
                TrySchedulePendingCarve();

            if (_pendingCarveCount < _pendingCarves.Length)
                return true;

            if (!dropOldestWhenFull)
                return false;

            DropOldestPendingCarve();
            return _pendingCarveCount < _pendingCarves.Length;
        }

        private void EnqueuePendingCarveUnchecked(in PendingCarveRequest request)
        {
            int slot = ResolvePendingCarveSlot(_pendingCarveHead, _pendingCarveCount);
            _pendingCarves[slot] = request;
            _pendingCarveCount++;
        }

        private PendingCarveRequest PopPendingCarve()
        {
            PendingCarveRequest request = _pendingCarves[_pendingCarveHead];
            _pendingCarves[_pendingCarveHead] = default;
            _pendingCarveHead = (_pendingCarveHead + 1) & PendingCarveMask;
            _pendingCarveCount--;
            return request;
        }

        private void EnsureCarveEventQueue()
        {
            if (_queuedCarveEvents.IsCreated)
                return;

            _queuedCarveEvents = new NativeQueue<VoxelCarveEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<VoxelCarveEvent>[64] - bounded async voxel carve ingress lane - owner: VoxelDeltaProcessor
            NativeMemorySentinel.RegisterNativeQueue(
                _queuedCarveEvents,
                InitialCarveEventQueueCapacity,
                NativeMemoryOwner,
                nameof(_queuedCarveEvents),
                NativeMemoryLifetime);
            PrewarmCarveEventQueue(ref _queuedCarveEvents, InitialCarveEventQueueCapacity);
            _queuedCarveEventCount = 0;
        }

        private void DisposeCarveEventQueue()
        {
            if (!_queuedCarveEvents.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_queuedCarveEvents));
            _queuedCarveEvents.Dispose();
            _queuedCarveEvents = default;
            _queuedCarveEventCount = 0;
        }

        private void EnsureBlackBox()
        {
            if (_blackBox.IsCreated)
                return;

            _blackBox = new NativeArray<VoxelCarveTelemetryEntry>(
                VoxelBlackBoxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            RegisterTrackedNativeArray(_blackBox, nameof(_blackBox));
            _blackBoxCursor = 0;
        }

        private void DisposeBlackBox()
        {
            DisposeTrackedNativeArray(ref _blackBox);
            _blackBoxCursor = 0;
        }

        private static void PrewarmCarveEventQueue(ref NativeQueue<VoxelCarveEvent> queue, int capacity)
        {
            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            for (int i = 0; i < capacity; i++)
                queue.TryDequeue(out _);
        }

        private void DrainQueuedCarveEvents()
        {
            if (!_queuedCarveEvents.IsCreated || _queuedCarveEventCount <= 0)
                return;

            int budget = ResolveQueuedCarveDrainBudget();
            int scanBudget = math.min(_queuedCarveEventCount, InitialCarveEventQueueCapacity);
            while (budget-- > 0 &&
                   scanBudget-- > 0 &&
                   _queuedCarveEventCount > 0 &&
                   _queuedCarveEvents.TryDequeue(out VoxelCarveEvent carveEvent))
            {
                _queuedCarveEventCount--;
                if (ShouldDeferQueuedCarveForFastBucket(in carveEvent))
                {
                    _queuedCarveEvents.Enqueue(carveEvent);
                    _queuedCarveEventCount++;
                    budget++;
                    continue;
                }

                if (TryEnqueuePendingCarveFromEvent(in carveEvent))
                    continue;

                _queuedCarveEvents.Enqueue(carveEvent);
                _queuedCarveEventCount++;
                break;
            }
        }

        private static int ResolveQueuedCarveDrainBudget()
        {
            return DebugResolveQueuedCarveDrainBudget(GlobalRegistry.ScalabilityTier);
        }

        private bool ShouldDeferQueuedCarveForFastBucket(in VoxelCarveEvent carveEvent)
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer == null || !bucketer.IsInitialized)
            {
                bucketer = GlobalRegistry.SimulationBucketer;
                _simulationBucketer = bucketer;
            }

            if (bucketer == null || !bucketer.IsInitialized)
                return false;

            uint hash = ResolveQueuedCarveBucketHash(in carveEvent);
            return !bucketer.IsFastBucketActive(bucketer.ResolveFastBucket(hash));
        }

        private static uint ResolveQueuedCarveBucketHash(in VoxelCarveEvent carveEvent)
        {
            uint volumeLo = unchecked((uint)carveEvent.VolumeInstanceId);
            uint volumeHi = unchecked((uint)(carveEvent.VolumeInstanceId >> 32));
            uint packedFlags =
                carveEvent.Operation |
                ((uint)carveEvent.Shape << 8) |
                ((uint)carveEvent.MaterialId << 16) |
                ((uint)carveEvent.SourceFlags << 24);
            return math.hash(new uint4(volumeLo, volumeHi, packedFlags, 0xB4C0D4u));
        }

        private bool TryEnqueuePendingCarveFromEvent(in VoxelCarveEvent carveEvent)
        {
            VoxelCarveEvent hydratedEvent = carveEvent;
            NormalizeCarveEventDoubleCoordinates(ref hydratedEvent);
            if (!IsFiniteCarveEvent(in hydratedEvent))
            {
                WriteBlackBoxSample(hydratedEvent.VolumeInstanceId, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return true;
            }

            HectonVoxelVolume volume = ResolveQueuedCarveVolume(hydratedEvent.VolumeInstanceId);
            if (volume == null || !volume.HasRuntimeData)
                return true;

            byte deltaFlags = 0;
            if (hydratedEvent.Operation == (byte)VoxelCarveOperationType.Add)
                deltaFlags = DeltaModeAdditive;
            else if (hydratedEvent.Operation == (byte)VoxelCarveOperationType.Replace)
                deltaFlags = DeltaModeReplace;

            byte shape = hydratedEvent.Shape == (byte)VoxelCarveShapeType.Box
                ? DeltaShapeBox
                : hydratedEvent.Shape == (byte)VoxelCarveShapeType.Capsule
                    ? DeltaShapeCapsule
                    : DeltaShapeSphere;

            PendingCarveRequest request = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = ResolveCarveHitPointDouble(in hydratedEvent),
                AbsoluteSegmentEnd = ResolveCarveSegmentEndDouble(in hydratedEvent),
                AbsoluteHalfExtents = ToVector3(hydratedEvent.AbsoluteHalfExtents),
                ExplicitRadiusMeters = hydratedEvent.RadiusMeters,
                ExplicitBlendStrength = hydratedEvent.BlendStrengthMeters,
                MaterialId = hydratedEvent.MaterialId,
                DeltaFlags = deltaFlags,
                SourceFlags = hydratedEvent.SourceFlags,
                Shape = shape,
                AbsoluteImpulseDirection = ToVector3(hydratedEvent.AbsoluteImpulseDirection)
            };

            return TryEnqueuePendingCarve(in request);
        }

        private HectonVoxelVolume ResolveQueuedCarveVolume(ulong volumeInstanceId)
        {
            if (volumeInstanceId == 0)
                return null;

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && EntityId.ToULong(volume.GetEntityId()) == volumeInstanceId)
                    return volume;
            }

            return null;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static float3 ToFloat3(double3 value)
        {
            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static double3 ToDouble3(float3 value)
        {
            return new double3(value.x, value.y, value.z);
        }

        private static double3 ResolveThermalMeltPositionDouble(in ThermalMeltEvent meltEvent)
        {
            if (HasAuthoritativeDoubleCoordinate(meltEvent.AbsoluteUniversePositionDouble, meltEvent.AbsoluteUniversePosition))
                return meltEvent.AbsoluteUniversePositionDouble;

            return ToDouble3(meltEvent.AbsoluteUniversePosition);
        }

        private static double3 ResolveCarveHitPointDouble(in VoxelCarveEvent carveEvent)
        {
            return ResolveCarveCoordinateDouble(carveEvent.AbsoluteHitPointDouble, carveEvent.AbsoluteHitPoint);
        }

        private static double3 ResolveCarveSegmentEndDouble(in VoxelCarveEvent carveEvent)
        {
            return ResolveCarveCoordinateDouble(carveEvent.AbsoluteSegmentEndDouble, carveEvent.AbsoluteSegmentEnd);
        }

        private static double3 ResolveCarveCoordinateDouble(double3 preciseCoordinate, float3 legacyCoordinate)
        {
            if (HasAuthoritativeDoubleCoordinate(preciseCoordinate, legacyCoordinate))
                return preciseCoordinate;

            return ToDouble3(legacyCoordinate);
        }

        private static bool HasAuthoritativeDoubleCoordinate(double3 preciseCoordinate, Vector3 legacyCoordinate)
        {
            return math.all(math.isfinite(preciseCoordinate)) &&
                   (math.any(preciseCoordinate != double3.zero) ||
                    (legacyCoordinate.x == 0f && legacyCoordinate.y == 0f && legacyCoordinate.z == 0f));
        }

        private static bool HasAuthoritativeDoubleCoordinate(double3 preciseCoordinate, float3 legacyCoordinate)
        {
            return math.all(math.isfinite(preciseCoordinate)) &&
                   (math.any(preciseCoordinate != double3.zero) || math.all(legacyCoordinate == float3.zero));
        }

        private static void NormalizeCarveEventDoubleCoordinates(ref VoxelCarveEvent carveEvent)
        {
            double3 absoluteHitPoint = ResolveCarveHitPointDouble(in carveEvent);
            double3 absoluteSegmentEnd = ResolveCarveSegmentEndDouble(in carveEvent);
            carveEvent.AbsoluteHitPointDouble = absoluteHitPoint;
            carveEvent.AbsoluteSegmentEndDouble = absoluteSegmentEnd;
            carveEvent.AbsoluteHitPoint = ToFloat3(absoluteHitPoint);
            carveEvent.AbsoluteSegmentEnd = ToFloat3(absoluteSegmentEnd);
        }

        private void EnqueuePendingCompactionUnchecked(in PendingCompactionRequest request)
        {
            int slot = ResolvePendingCompactionSlot(_pendingCompactionHead, _pendingCompactionCount);
            _pendingCompactions[slot] = request;
            _pendingCompactionCount++;
        }

        private PendingCompactionRequest PopPendingCompaction()
        {
            PendingCompactionRequest request = _pendingCompactions[_pendingCompactionHead];
            _pendingCompactions[_pendingCompactionHead] = default;
            _pendingCompactionHead = (_pendingCompactionHead + 1) & PendingCompactionMask;
            _pendingCompactionCount--;
            return request;
        }

        /// <summary>
        /// Stages a plasma-cut carve request for batch processing on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="runtimeHitPoint">Runtime-space hit position.</param>
        /// <param name="damage">Accumulated plasma damage.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void StagePlasmaCut(HectonVoxelVolume volume, Vector3 runtimeHitPoint, float damage, byte materialId = DefaultMaterialId)
        {
            if (volume == null || damage <= 0f || !volume.HasRuntimeData)
                return;

            double3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeHitPoint);
            float mergeDistance = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            double mergeDistanceSq = (double)mergeDistance * mergeDistance;

            for (int i = 0; i < _pendingCarveCount; i++)
            {
                int slot = ResolvePendingCarveSlot(_pendingCarveHead, i);
                PendingCarveRequest existing = _pendingCarves[slot];
                if (!ReferenceEquals(existing.Volume, volume))
                    continue;

                if (math.lengthsq(existing.AbsoluteHitPoint - absoluteHitPoint) > mergeDistanceSq)
                    continue;

                existing.AbsoluteHitPoint = (existing.AbsoluteHitPoint + absoluteHitPoint) * 0.5d;
                existing.AccumulatedDamage += damage;
                existing.MaterialId = materialId;
                existing.SourceFlags |= CarveSourceLaser;
                _pendingCarves[slot] = existing;
                return;
            }

            if (!TryReservePendingCarveSlot(true))
                return;

            PendingCarveRequest request = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                AccumulatedDamage = damage,
                MaterialId = materialId,
                SourceFlags = CarveSourceLaser
            };
            EnqueuePendingCarveUnchecked(in request);
        }

        /// <summary>
        /// Applies an explicit crater carve immediately and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="runtimeHitPoint">Runtime-space impact point.</param>
        /// <param name="radius">Requested crater radius in meters.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateCrater(HectonVoxelVolume volume, Vector3 runtimeHitPoint, float radius, byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            double3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimeHitPoint);
            ApplyImmediateAbsoluteCrater(volume, absoluteHitPoint, radius, materialId);
        }

        /// <summary>
        /// Applies an explicit crater carve in absolute-universe space and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteHitPoint">Absolute-universe impact point.</param>
        /// <param name="radius">Requested crater radius in meters.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteCrater(
            HectonVoxelVolume volume,
            Vector3 absoluteHitPoint,
            float radius,
            byte materialId = DefaultMaterialId,
            byte sourceFlags = 0,
            Vector3 absoluteImpulseDirection = default)
        {
            ApplyImmediateAbsoluteCrater(volume, ToDouble3(absoluteHitPoint), radius, materialId, sourceFlags, absoluteImpulseDirection);
        }

        public void ApplyImmediateAbsoluteCrater(
            HectonVoxelVolume volume,
            double3 absoluteHitPoint,
            float radius,
            byte materialId = DefaultMaterialId,
            byte sourceFlags = 0,
            Vector3 absoluteImpulseDirection = default)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = ToFloat3(absoluteHitPoint),
                AbsoluteHitPointDouble = absoluteHitPoint,
                AbsoluteImpulseDirection = new float3(absoluteImpulseDirection.x, absoluteImpulseDirection.y, absoluteImpulseDirection.z),
                RadiusMeters = radius,
                MaterialId = materialId,
                Operation = (byte)VoxelCarveOperationType.Subtract,
                Shape = (byte)VoxelCarveShapeType.Sphere,
                SourceFlags = sourceFlags
            };
            TryQueueCarveEvent(volume, in carveEvent);
        }

        /// <summary>
        /// Applies an explicit laser-origin crater carve in absolute-universe space.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteHitPoint">Absolute-universe impact point.</param>
        /// <param name="radius">Requested crater radius in meters.</param>
        /// <param name="absoluteImpulseDirection">Laser travel direction in absolute-universe space.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteLaserCrater(
            HectonVoxelVolume volume,
            Vector3 absoluteHitPoint,
            float radius,
            Vector3 absoluteImpulseDirection,
            byte materialId = DefaultMaterialId)
        {
            ApplyImmediateAbsoluteCrater(volume, ToDouble3(absoluteHitPoint), radius, materialId, CarveSourceLaser, absoluteImpulseDirection);
        }

        public void ApplyImmediateAbsoluteLaserCrater(
            HectonVoxelVolume volume,
            double3 absoluteHitPoint,
            float radius,
            Vector3 absoluteImpulseDirection,
            byte materialId = DefaultMaterialId)
        {
            ApplyImmediateAbsoluteCrater(volume, absoluteHitPoint, radius, materialId, CarveSourceLaser, absoluteImpulseDirection);
        }

        /// <summary>
        /// Applies an explicit subtractive box carve in absolute-universe space and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteCenter">Absolute-universe box center.</param>
        /// <param name="halfExtents">Axis-aligned absolute half-extents in meters.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteBoxCrater(
            HectonVoxelVolume volume,
            Vector3 absoluteCenter,
            Vector3 halfExtents,
            byte materialId = DefaultMaterialId)
        {
            ApplyImmediateAbsoluteBoxCrater(volume, ToDouble3(absoluteCenter), halfExtents, materialId);
        }

        public void ApplyImmediateAbsoluteBoxCrater(
            HectonVoxelVolume volume,
            double3 absoluteCenter,
            Vector3 halfExtents,
            byte materialId = DefaultMaterialId)
        {
            if (volume == null || !volume.HasRuntimeData)
                return;

            float3 resolvedHalfExtents3 = new float3(
                math.max(volume.VoxelSize, math.abs(halfExtents.x)),
                math.max(volume.VoxelSize, math.abs(halfExtents.y)),
                math.max(volume.VoxelSize, math.abs(halfExtents.z)));
            Vector3 resolvedHalfExtents = new Vector3(
                resolvedHalfExtents3.x,
                resolvedHalfExtents3.y,
                resolvedHalfExtents3.z);
            if (resolvedHalfExtents.sqrMagnitude <= 0.0001f)
                return;

            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = ToFloat3(absoluteCenter),
                AbsoluteHitPointDouble = absoluteCenter,
                AbsoluteHalfExtents = resolvedHalfExtents3,
                BlendStrengthMeters = math.max(volume.VoxelSize, math.cmin(resolvedHalfExtents3) * 0.35f),
                MaterialId = materialId,
                Operation = (byte)VoxelCarveOperationType.Subtract,
                Shape = (byte)VoxelCarveShapeType.Box
            };
            TryQueueCarveEvent(volume, in carveEvent);
        }

        /// <summary>
        /// Applies an explicit additive weld stamp in absolute-universe space and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteHitPoint">Absolute-universe impact point.</param>
        /// <param name="radius">Requested weld radius in meters.</param>
        /// <param name="strength">Smooth-union strength scalar.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteWeld(HectonVoxelVolume volume, Vector3 absoluteHitPoint, float radius, float strength, byte materialId = DefaultMaterialId)
        {
            ApplyImmediateAbsoluteWeld(volume, ToDouble3(absoluteHitPoint), radius, strength, materialId);
        }

        public void ApplyImmediateAbsoluteWeld(HectonVoxelVolume volume, double3 absoluteHitPoint, float radius, float strength, byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = ToFloat3(absoluteHitPoint),
                AbsoluteHitPointDouble = absoluteHitPoint,
                RadiusMeters = radius,
                BlendStrengthMeters = math.max(volume.VoxelSize, strength),
                MaterialId = materialId,
                Operation = (byte)VoxelCarveOperationType.Add,
                Shape = (byte)VoxelCarveShapeType.Sphere
            };
            TryQueueCarveEvent(volume, in carveEvent);
        }

        /// <summary>
        /// Applies an explicit additive capsule weld in absolute-universe space and queues a rebuild on the dispatcher lane.
        /// </summary>
        /// <param name="volume">Target runtime volume.</param>
        /// <param name="absoluteStart">Absolute-universe segment start.</param>
        /// <param name="absoluteEnd">Absolute-universe segment end.</param>
        /// <param name="radius">Requested capsule radius in meters.</param>
        /// <param name="strength">Smooth-union strength scalar.</param>
        /// <param name="materialId">Material palette index for the modified cells.</param>
        public void ApplyImmediateAbsoluteCapsuleWeld(
            HectonVoxelVolume volume,
            Vector3 absoluteStart,
            Vector3 absoluteEnd,
            float radius,
            float strength,
            byte materialId = DefaultMaterialId)
        {
            ApplyImmediateAbsoluteCapsuleWeld(volume, ToDouble3(absoluteStart), ToDouble3(absoluteEnd), radius, strength, materialId);
        }

        public void ApplyImmediateAbsoluteCapsuleWeld(
            HectonVoxelVolume volume,
            double3 absoluteStart,
            double3 absoluteEnd,
            float radius,
            float strength,
            byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            if (math.lengthsq(absoluteEnd - absoluteStart) <= 0.0001d)
            {
                ApplyImmediateAbsoluteWeld(volume, absoluteStart, radius, strength, materialId);
                return;
            }

            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = ToFloat3(absoluteStart),
                AbsoluteSegmentEnd = ToFloat3(absoluteEnd),
                AbsoluteHitPointDouble = absoluteStart,
                AbsoluteSegmentEndDouble = absoluteEnd,
                RadiusMeters = radius,
                BlendStrengthMeters = math.max(volume.VoxelSize, strength),
                MaterialId = materialId,
                Operation = (byte)VoxelCarveOperationType.Add,
                Shape = (byte)VoxelCarveShapeType.Capsule
            };
            TryQueueCarveEvent(volume, in carveEvent);
        }

        /// <summary>
        /// Queues an absolute-universe carve event through the bounded async ingress lane.
        /// </summary>
        public bool TryQueueCarveEvent(HectonVoxelVolume volume, in VoxelCarveEvent carveEvent)
        {
            if (volume == null || !volume.HasRuntimeData)
                return false;

            ulong volumeId = EntityId.ToULong(volume.GetEntityId());
            VoxelCarveEvent queuedEvent = carveEvent;
            queuedEvent.VolumeInstanceId = volumeId;
            NormalizeCarveEventDoubleCoordinates(ref queuedEvent);
            if (!IsFiniteCarveEvent(in queuedEvent))
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return false;
            }

            EnsureCarveEventQueue();
            if (!_queuedCarveEvents.IsCreated)
                return false;

            if (_queuedCarveEventCount >= InitialCarveEventQueueCapacity)
            {
                if (!_queuedCarveEvents.TryDequeue(out _))
                    return false;

                _queuedCarveEventCount--;
                WriteBlackBoxSample(volumeId, VoxelBlackBoxQueueOverflowFlag);
            }

            _queuedCarveEvents.Enqueue(queuedEvent);
            _queuedCarveEventCount++;
            SignalBus<VoxelCarveEvent>.Push(in queuedEvent);
            return true;
        }

        private bool TryEnqueuePendingCarve(in PendingCarveRequest request)
        {
            if (request.Volume == null || !request.Volume.HasRuntimeData)
                return false;

            if (!IsFinitePendingCarve(in request))
            {
                WriteBlackBoxSample(EntityId.ToULong(request.Volume.GetEntityId()), VoxelBlackBoxInvalidPendingCarveFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidPendingCarveFlag);
#endif
                return false;
            }

            if (!TryReservePendingCarveSlot(false))
                return false;

            EnqueuePendingCarveUnchecked(in request);
            return true;
        }

        /// <summary>
        /// Builds a persistent native delta map for the provided volume bounds.
        /// Caller owns disposal of the returned map.
        /// </summary>
        /// <param name="volume">Target volume.</param>
        /// <param name="modifiedCells">Merged delta map covering the volume bounds.</param>
        /// <returns>True when persistent deltas overlap the target volume.</returns>
        public bool TryBuildDeltaMapForVolume(HectonVoxelVolume volume, out NativeParallelHashMap<int3, VoxelModifiedCell> modifiedCells)
        {
            modifiedCells = default;
            if (volume == null || !volume.HasRuntimeData || (_chunkStates.Count == 0 && _compactedChunkStates.Count == 0))
                return false;

            ResolveVolumeCellBounds(volume, out int3 minCell, out int3 maxCell, out int3 minChunk, out int3 maxChunk);
            int estimatedCount = 0;

            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        ChunkAddress address = new ChunkAddress(new int3(x, y, z), volume.VoxelSize);
                        if (_compactedChunkStates.ContainsKey(address))
                            estimatedCount += ChunkCellCount;

                        if (_chunkStates.TryGetValue(address, out ChunkDeltaState state))
                            estimatedCount += CountDirtyCells(in state);
                    }
                }
            }

            if (estimatedCount <= 0)
                return false;

            modifiedCells = new NativeParallelHashMap<int3, VoxelModifiedCell>(estimatedCount, Allocator.Persistent);

            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        ChunkAddress address = new ChunkAddress(new int3(x, y, z), volume.VoxelSize);
                        if (_compactedChunkStates.TryGetValue(address, out CompactedChunkState compactedState))
                        {
                            for (int flatIndex = 0; flatIndex < ChunkCellCount; flatIndex++)
                            {
                                int3 cell = AbsoluteCellFromLocalIndex(compactedState.ChunkCoord, flatIndex);
                                if (math.any(cell < minCell) || math.any(cell > maxCell))
                                    continue;

                                modifiedCells.TryAdd(cell, new VoxelModifiedCell
                                {
                                    Density = BitsToHalf(compactedState.GetSdfValueBits(flatIndex)),
                                    MaterialId = compactedState.GetMaterialId(flatIndex),
                                    Flags = compactedState.GetCellFlags(flatIndex)
                                });
                            }
                        }

                        if (!_chunkStates.TryGetValue(address, out ChunkDeltaState state))
                            continue;

                        for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)
                        {
                            uint dirtyWord = state.DirtyMaskWords[wordIndex];
                            if (dirtyWord == 0u)
                                continue;

                            int baseIndex = wordIndex << 5;
                            for (int bitIndex = 0; bitIndex < 32; bitIndex++)
                            {
                                uint bitMask = 1u << bitIndex;
                                if ((dirtyWord & bitMask) == 0u)
                                    continue;

                                int flatIndex = baseIndex + bitIndex;
                                int3 cell = AbsoluteCellFromLocalIndex(state.ChunkCoord, flatIndex);
                                if (math.any(cell < minCell) || math.any(cell > maxCell))
                                    continue;

                                modifiedCells.Remove(cell);
                                modifiedCells.TryAdd(cell, new VoxelModifiedCell
                                {
                                    Density = BitsToHalf(state.SdfValueBits[flatIndex]),
                                    MaterialId = state.MaterialIds[flatIndex],
                                    Flags = state.CellFlags[flatIndex]
                                });
                            }
                        }
                    }
                }
            }

            if (modifiedCells.Count() <= 0)
            {
                modifiedCells.Dispose();
                modifiedCells = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copies the current voxel delta snapshot into the save DTO.
        /// </summary>
        /// <param name="data">Target save container.</param>
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.voxelDeltaPersistence.EnsureCapacity(_chunkStates.Count + _compactedChunkStates.Count);
            data.voxelDeltaPersistence.chunkCount = 0;
            data.voxelDeltaPersistence.totalCellCount = 0;

            Dictionary<ChunkAddress, CompactedChunkState>.Enumerator compactedEnumerator = _compactedChunkStates.GetEnumerator();
            while (compactedEnumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, CompactedChunkState> pair = compactedEnumerator.Current;
                CompactedChunkState compactedState = pair.Value;
                _chunkStates.TryGetValue(pair.Key, out ChunkDeltaState overlayState);
                WriteCompactedSaveChunk(data, pair.Key, in compactedState, in overlayState, overlayState.DirtyMaskWords.IsCreated);
            }

            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator enumerator = _chunkStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, ChunkDeltaState> pair = enumerator.Current;
                if (_compactedChunkStates.ContainsKey(pair.Key))
                    continue;

                ChunkDeltaState state = pair.Value;
                WriteDirtySaveChunk(data, pair.Key, in state);
            }

            for (int i = data.voxelDeltaPersistence.chunkCount; i < data.voxelDeltaPersistence.chunks.Length; i++)
            {
                VoxelDeltaChunkDTO staleChunk = data.voxelDeltaPersistence.chunks[i];
                staleChunk.EnsureCapacity(0);
                data.voxelDeltaPersistence.chunks[i] = staleChunk;
            }
        }

        private static void WriteDirtySaveChunk(SaveData data, ChunkAddress address, in ChunkDeltaState state)
        {
            int cellCount = CountDirtyCells(in state);
            if (cellCount <= 0)
                return;

            int chunkIndex = data.voxelDeltaPersistence.chunkCount;
            VoxelDeltaChunkDTO chunkDto = data.voxelDeltaPersistence.chunks[chunkIndex];
            chunkDto.chunkX = address.ChunkCoord.x;
            chunkDto.chunkY = address.ChunkCoord.y;
            chunkDto.chunkZ = address.ChunkCoord.z;
            chunkDto.voxelSize = address.VoxelSize;
            chunkDto.EnsureCapacity(cellCount);
            chunkDto.cellCount = cellCount;
            chunkDto.storageFlags = VoxelDeltaChunkDTO.StorageDense;
            chunkDto.uniformSdfValueBits = 0;

            for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                chunkDto.dirtyMaskWords[i] = state.DirtyMaskWords[i];

            for (int i = 0; i < ChunkCellCount; i++)
            {
                chunkDto.sdfValueBits[i] = state.SdfValueBits[i];
                chunkDto.materialIds[i] = state.MaterialIds[i];
                chunkDto.cellFlags[i] = state.CellFlags[i];
            }

            chunkDto.cells = Array.Empty<VoxelDeltaCellDTO>();
            data.voxelDeltaPersistence.chunks[chunkIndex] = chunkDto;
            data.voxelDeltaPersistence.chunkCount = chunkIndex + 1;
            data.voxelDeltaPersistence.totalCellCount += cellCount;
        }

        private static void WriteCompactedSaveChunk(
            SaveData data,
            ChunkAddress address,
            in CompactedChunkState compactedState,
            in ChunkDeltaState overlayState,
            bool hasOverlay)
        {
            int chunkIndex = data.voxelDeltaPersistence.chunkCount;
            VoxelDeltaChunkDTO chunkDto = data.voxelDeltaPersistence.chunks[chunkIndex];
            chunkDto.chunkX = address.ChunkCoord.x;
            chunkDto.chunkY = address.ChunkCoord.y;
            chunkDto.chunkZ = address.ChunkCoord.z;
            chunkDto.voxelSize = address.VoxelSize;

            if (IsUniformSdfRleSnapshotEligible(compactedState, hasOverlay))
            {
                chunkDto.EnsureCapacity(0);
                chunkDto.cellCount = ChunkCellCount;
                chunkDto.storageFlags = VoxelDeltaChunkDTO.StorageUniformSdfRle;
                chunkDto.uniformSdfValueBits = compactedState.RleSdfValueBits;
                chunkDto.cells = Array.Empty<VoxelDeltaCellDTO>();
                data.voxelDeltaPersistence.chunks[chunkIndex] = chunkDto;
                data.voxelDeltaPersistence.chunkCount = chunkIndex + 1;
                data.voxelDeltaPersistence.totalCellCount += ChunkCellCount;
                return;
            }

            chunkDto.EnsureCapacity(ChunkCellCount);
            chunkDto.cellCount = ChunkCellCount;
            chunkDto.storageFlags = VoxelDeltaChunkDTO.StorageDense;
            chunkDto.uniformSdfValueBits = 0;

            for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                chunkDto.dirtyMaskWords[i] = uint.MaxValue;

            for (int i = 0; i < ChunkCellCount; i++)
            {
                ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, i, out ushort sdfBits, out byte materialId);
                chunkDto.sdfValueBits[i] = sdfBits;
                chunkDto.materialIds[i] = materialId;
                chunkDto.cellFlags[i] = DeltaModeReplace;
            }

            chunkDto.cells = Array.Empty<VoxelDeltaCellDTO>();
            data.voxelDeltaPersistence.chunks[chunkIndex] = chunkDto;
            data.voxelDeltaPersistence.chunkCount = chunkIndex + 1;
            data.voxelDeltaPersistence.totalCellCount += ChunkCellCount;
        }

        private static void ResolveCompactedMergedCell(
            in CompactedChunkState compactedState,
            in ChunkDeltaState overlayState,
            bool hasOverlay,
            int flatIndex,
            out ushort sdfBits,
            out byte materialId)
        {
            float density = (float)BitsToHalf(compactedState.GetSdfValueBits(flatIndex));
            materialId = compactedState.GetMaterialId(flatIndex);
            if (hasOverlay)
            {
                uint localIndex = (uint)flatIndex;
                if (IsDirty(in overlayState, localIndex))
                {
                    float overlayDensity = (float)BitsToHalf(overlayState.SdfValueBits[flatIndex]);
                    byte overlayFlags = overlayState.CellFlags[flatIndex];
                    if ((overlayFlags & DeltaModeReplace) != 0)
                        density = overlayDensity;
                    else if ((overlayFlags & DeltaModeAdditive) != 0)
                        density = math.max(density, overlayDensity);
                    else
                        density = math.min(density, overlayDensity);

                    materialId = overlayState.MaterialIds[flatIndex];
                }
            }

            sdfBits = HalfToBits(ClampToHalf(density));
        }

        /// <summary>
        /// Restores voxel delta chunks from the loaded save DTO.
        /// </summary>
        /// <param name="data">Loaded save container.</param>
        public void LoadFromSaveData(SaveData data)
        {
            DisposeChunkStates();
            DisposeCompactedChunkStates();
            _chunkWriteVersions.Clear();
            _pendingRebuildVolumes.Clear();

            if (data == null || data.voxelDeltaPersistence.chunkCount <= 0 || data.voxelDeltaPersistence.chunks == null)
                return;

            int chunkCount = math.min(data.voxelDeltaPersistence.chunkCount, data.voxelDeltaPersistence.chunks.Length);
            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                VoxelDeltaChunkDTO chunk = data.voxelDeltaPersistence.chunks[chunkIndex];
                bool hasUniformStorage = (chunk.storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) != 0;
                bool hasDenseStorage = HasDenseStorage(in chunk);
                int denseCellCount = hasDenseStorage ? CountDirtyCells(chunk.dirtyMaskWords) : 0;
                int legacyCellCount = chunk.cells != null
                    ? math.min(chunk.cellCount, chunk.cells.Length)
                    : 0;

                int3 chunkCoord = new int3((int)chunk.chunkX, (int)chunk.chunkY, (int)chunk.chunkZ);
                ChunkAddress address = new ChunkAddress(chunkCoord, chunk.voxelSize);

                if (hasUniformStorage)
                {
                    _compactedChunkStates[address] = new CompactedChunkState(
                        chunkCoord,
                        chunk.voxelSize,
                        chunk.uniformSdfValueBits,
                        DefaultMaterialId,
                        DeltaModeReplace);
                    continue;
                }

                if (denseCellCount <= 0 && legacyCellCount <= 0)
                    continue;

                ChunkDeltaState state = GetOrCreateChunkState(chunkCoord, chunk.voxelSize);

                if (hasDenseStorage && denseCellCount > 0)
                {
                    for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                        state.DirtyMaskWords[i] = chunk.dirtyMaskWords[i];

                    for (int i = 0; i < ChunkCellCount; i++)
                    {
                        state.SdfValueBits[i] = chunk.sdfValueBits[i];
                        state.MaterialIds[i] = chunk.materialIds[i];
                        state.CellFlags[i] = chunk.cellFlags != null && chunk.cellFlags.Length == ChunkCellCount
                            ? chunk.cellFlags[i]
                            : (byte)0;
                    }

                    state.DirtyCellCount = denseCellCount;
                }
                else
                {
                    for (int cellIndex = 0; cellIndex < legacyCellCount; cellIndex++)
                    {
                        VoxelDeltaCellDTO cell = chunk.cells[cellIndex];
                        int3 absoluteCell = MortonDecodeSigned(cell.universeKey);
                        if (!TryComputeLocalCellIndex(absoluteCell, state.ChunkCoord, out uint localIndex))
                            continue;

                        SetCell(ref state, localIndex, ClampToHalf(cell.sdfValue), cell.materialId, cell.flags);
                    }
                }

                _chunkStates[address] = state;
            }

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
            }
        }

        public unsafe NativeArray<byte> CaptureNativeSnapshot(Allocator allocator)
        {
            if (_chunkStates.Count <= 0 && _compactedChunkStates.Count <= 0)
                return default;

            int chunkCount = 0;
            int totalDirtyCellCount = 0;
            int deltaChunkHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            int runBytes = UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
            int totalBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            Dictionary<ChunkAddress, CompactedChunkState>.Enumerator compactedCountEnumerator = _compactedChunkStates.GetEnumerator();
            while (compactedCountEnumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, CompactedChunkState> pair = compactedCountEnumerator.Current;
                _chunkStates.TryGetValue(pair.Key, out ChunkDeltaState overlayState);
                bool hasOverlay = overlayState.DirtyMaskWords.IsCreated;
                chunkCount++;
                totalDirtyCellCount += ChunkCellCount;
                CompactedChunkState compactedState = pair.Value;
                totalBytes += IsUniformSdfRleSnapshotEligible(compactedState, hasOverlay)
                    ? deltaChunkHeaderBytes + NativeSnapshotUniformSdfRlePayloadBytes
                    : deltaChunkHeaderBytes + (CountCompactedSparseRuns(in compactedState, in overlayState, hasOverlay) * runBytes);
            }

            compactedCountEnumerator.Dispose();
            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator countEnumerator = _chunkStates.GetEnumerator();
            while (countEnumerator.MoveNext())
            {
                if (_compactedChunkStates.ContainsKey(countEnumerator.Current.Key))
                    continue;

                ChunkDeltaState state = countEnumerator.Current.Value;
                int cellCount = CountDirtyCells(in state);
                if (cellCount <= 0)
                    continue;

                int runCount = CountSparseDirtyRuns(in state);
                if (runCount <= 0)
                    continue;

                chunkCount++;
                totalDirtyCellCount += cellCount;
                totalBytes += deltaChunkHeaderBytes + (runCount * runBytes);
            }

            countEnumerator.Dispose();
            if (chunkCount <= 0)
                return default;

            NativeArray<byte> snapshot = new NativeArray<byte>(totalBytes, allocator, NativeArrayOptions.UninitializedMemory);
            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            int cursor = 0;

            NativeSnapshotHeader header = new NativeSnapshotHeader
            {
                Version = NativeSnapshotDeltaRleMagic,
                ChunkCount = chunkCount,
                TotalDirtyCellCount = totalDirtyCellCount
            };

            UnsafeUtility.CopyStructureToPtr(ref header, snapshotPtr);
            cursor += UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            Dictionary<ChunkAddress, CompactedChunkState>.Enumerator compactedWriteEnumerator = _compactedChunkStates.GetEnumerator();
            while (compactedWriteEnumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, CompactedChunkState> pair = compactedWriteEnumerator.Current;
                CompactedChunkState compactedState = pair.Value;
                _chunkStates.TryGetValue(pair.Key, out ChunkDeltaState overlayState);
                bool hasOverlay = overlayState.DirtyMaskWords.IsCreated;
                if (IsUniformSdfRleSnapshotEligible(compactedState, hasOverlay))
                {
                    WriteUniformSdfRleNativeSnapshotChunk(snapshotPtr, snapshot.Length, ref cursor, pair.Key, in compactedState);
                }
                else
                {
                    WriteCompactedSparseRleNativeSnapshotChunk(
                        snapshotPtr,
                        snapshot.Length,
                        ref cursor,
                        pair.Key,
                        in compactedState,
                        in overlayState,
                        hasOverlay);
                }
            }

            compactedWriteEnumerator.Dispose();
            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator writeEnumerator = _chunkStates.GetEnumerator();
            while (writeEnumerator.MoveNext())
            {
                KeyValuePair<ChunkAddress, ChunkDeltaState> pair = writeEnumerator.Current;
                if (_compactedChunkStates.ContainsKey(pair.Key))
                    continue;

                ChunkDeltaState state = pair.Value;
                int dirtyCellCount = CountDirtyCells(in state);
                if (dirtyCellCount <= 0)
                    continue;

                WriteDirtySparseRleNativeSnapshotChunk(snapshotPtr, snapshot.Length, ref cursor, pair.Key, in state, dirtyCellCount);
            }

            writeEnumerator.Dispose();
            return snapshot;
        }

        private static bool IsUniformSdfRleSnapshotEligible(
            CompactedChunkState compactedState,
            bool hasOverlay)
        {
            return compactedState.IsRleCompressed &&
                   compactedState.RleMaterialId == DefaultMaterialId &&
                   compactedState.RleCellFlags == DeltaModeReplace &&
                   !hasOverlay;
        }

        private static unsafe void WriteUniformSdfRleNativeSnapshotChunk(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            ChunkAddress address,
            in CompactedChunkState compactedState)
        {
            int headerBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            int payloadBytes = NativeSnapshotUniformSdfRlePayloadBytes;
            if (cursor > snapshotLength - headerBytes - payloadBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int headerCursor = cursor;
            cursor += headerBytes;
            int payloadCursor = cursor;
            *(snapshotPtr + payloadCursor) = unchecked((byte)QuantizeSdfByte(compactedState.RleSdfValueBits));
            cursor += payloadBytes;

            NativeSnapshotChunkHeaderDeltaRle chunkHeader = new NativeSnapshotChunkHeaderDeltaRle
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = ChunkCellCount,
                StorageFlags = NativeSnapshotStorageUniformSdfRle,
                Reserved0 = 0,
                Reserved1 = 0,
                PayloadByteLength = payloadBytes,
                PayloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes)
            };
            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + headerCursor);
        }

        private static int CountSparseDirtyRuns(in ChunkDeltaState state)
        {
            if (!state.DirtyMaskWords.IsCreated)
                return 0;

            int runCount = 0;
            int index = 0;
            while (index < ChunkCellCount)
            {
                if (!IsDirty(in state, (uint)index))
                {
                    index++;
                    continue;
                }

                runCount++;
                sbyte sdfValue = QuantizeSdfByte(state.SdfValueBits[index]);
                byte materialId = state.MaterialIds[index];
                byte flags = state.CellFlags[index];
                index++;
                while (index < ChunkCellCount &&
                       IsDirty(in state, (uint)index) &&
                       QuantizeSdfByte(state.SdfValueBits[index]) == sdfValue &&
                       state.MaterialIds[index] == materialId &&
                       state.CellFlags[index] == flags)
                {
                    index++;
                }
            }

            return runCount;
        }

        private static int CountCompactedSparseRuns(
            in CompactedChunkState compactedState,
            in ChunkDeltaState overlayState,
            bool hasOverlay)
        {
            int runCount = 0;
            int index = 0;
            while (index < ChunkCellCount)
            {
                ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, index, out ushort sdfBits, out byte materialId);
                sbyte sdfValue = QuantizeSdfByte(sdfBits);
                runCount++;
                index++;
                while (index < ChunkCellCount)
                {
                    ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, index, out ushort nextSdfBits, out byte nextMaterialId);
                    if (QuantizeSdfByte(nextSdfBits) != sdfValue || nextMaterialId != materialId)
                        break;

                    index++;
                }
            }

            return runCount;
        }

        private static unsafe void WriteDirtySparseRleNativeSnapshotChunk(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            ChunkAddress address,
            in ChunkDeltaState state,
            int dirtyCellCount)
        {
            int headerBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            if (dirtyCellCount <= 0 || cursor > snapshotLength - headerBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int headerCursor = cursor;
            cursor += headerBytes;
            int payloadCursor = cursor;

            int cellIndex = 0;
            while (cellIndex < ChunkCellCount)
            {
                if (!IsDirty(in state, (uint)cellIndex))
                {
                    cellIndex++;
                    continue;
                }

                int startIndex = cellIndex;
                ushort sdfBits = state.SdfValueBits[cellIndex];
                sbyte sdfValue = QuantizeSdfByte(sdfBits);
                byte materialId = state.MaterialIds[cellIndex];
                byte flags = state.CellFlags[cellIndex];
                cellIndex++;
                while (cellIndex < ChunkCellCount &&
                       IsDirty(in state, (uint)cellIndex) &&
                       QuantizeSdfByte(state.SdfValueBits[cellIndex]) == sdfValue &&
                       state.MaterialIds[cellIndex] == materialId &&
                       state.CellFlags[cellIndex] == flags)
                {
                    cellIndex++;
                }

                SaveVoxelDeltaRun8 run = new SaveVoxelDeltaRun8(
                    (ushort)startIndex,
                    (ushort)(cellIndex - startIndex),
                    sdfValue,
                    materialId,
                    flags);
                if (!TryWriteSparseRleRun(snapshotPtr, snapshotLength, ref cursor, in run))
                    return;
            }

            int payloadBytes = cursor - payloadCursor;
            if (payloadBytes <= 0)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            NativeSnapshotChunkHeaderDeltaRle chunkHeader = new NativeSnapshotChunkHeaderDeltaRle
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = dirtyCellCount,
                StorageFlags = NativeSnapshotStorageSparseDeltaRle,
                Reserved0 = 0,
                Reserved1 = 0,
                PayloadByteLength = payloadBytes,
                PayloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes)
            };
            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + headerCursor);
        }

        private static unsafe void WriteCompactedSparseRleNativeSnapshotChunk(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            ChunkAddress address,
            in CompactedChunkState compactedState,
            in ChunkDeltaState overlayState,
            bool hasOverlay)
        {
            int headerBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            if (cursor > snapshotLength - headerBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int headerCursor = cursor;
            cursor += headerBytes;
            int payloadCursor = cursor;

            int cellIndex = 0;
            while (cellIndex < ChunkCellCount)
            {
                int startIndex = cellIndex;
                ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, cellIndex, out ushort sdfBits, out byte materialId);
                sbyte sdfValue = QuantizeSdfByte(sdfBits);
                cellIndex++;
                while (cellIndex < ChunkCellCount)
                {
                    ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, cellIndex, out ushort nextSdfBits, out byte nextMaterialId);
                    if (QuantizeSdfByte(nextSdfBits) != sdfValue || nextMaterialId != materialId)
                        break;

                    cellIndex++;
                }

                SaveVoxelDeltaRun8 run = new SaveVoxelDeltaRun8(
                    (ushort)startIndex,
                    (ushort)(cellIndex - startIndex),
                    sdfValue,
                    materialId,
                    DeltaModeReplace);
                if (!TryWriteSparseRleRun(snapshotPtr, snapshotLength, ref cursor, in run))
                    return;
            }

            int payloadBytes = cursor - payloadCursor;
            if (payloadBytes <= 0)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            NativeSnapshotChunkHeaderDeltaRle chunkHeader = new NativeSnapshotChunkHeaderDeltaRle
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = ChunkCellCount,
                StorageFlags = NativeSnapshotStorageSparseDeltaRle,
                Reserved0 = 0,
                Reserved1 = 0,
                PayloadByteLength = payloadBytes,
                PayloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes)
            };
            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + headerCursor);
        }

        private static unsafe bool TryWriteSparseRleRun(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            in SaveVoxelDeltaRun8 run)
        {
            int runBytes = UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
            if (cursor > snapshotLength - runBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return false;
            }

            UnsafeUtility.WriteArrayElement(snapshotPtr + cursor, 0, run);
            cursor += runBytes;
            return true;
        }

        private static unsafe void WriteCompactedNativeSnapshotChunk(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            ChunkAddress address,
            in CompactedChunkState compactedState,
            in ChunkDeltaState overlayState,
            bool hasOverlay)
        {
            int densePayloadBytes = (ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<ushort>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>());
            NativeSnapshotChunkHeaderRle chunkHeader = new NativeSnapshotChunkHeaderRle
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = ChunkCellCount,
                StorageFlags = NativeSnapshotStorageDense,
                PayloadByteLength = densePayloadBytes
            };

            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + cursor);
            cursor += UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderRle>();

            int dirtyMaskBytes = ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();
            for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                UnsafeUtility.WriteArrayElement(snapshotPtr + cursor, i, uint.MaxValue);
            cursor += dirtyMaskBytes;

            int sdfCursor = cursor;
            int sdfBytes = ChunkCellCount * UnsafeUtility.SizeOf<ushort>();
            int materialCursor = sdfCursor + sdfBytes;
            int materialBytes = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            int flagsCursor = materialCursor + materialBytes;
            int flagsBytes = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            if (flagsCursor > snapshotLength - flagsBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            for (int i = 0; i < ChunkCellCount; i++)
            {
                ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, i, out ushort sdfBits, out byte materialId);
                UnsafeUtility.WriteArrayElement(snapshotPtr + sdfCursor, i, sdfBits);
                UnsafeUtility.WriteArrayElement(snapshotPtr + materialCursor, i, materialId);
                UnsafeUtility.WriteArrayElement(snapshotPtr + flagsCursor, i, DeltaModeReplace);
            }

            cursor += sdfBytes + materialBytes + flagsBytes;
        }

        public unsafe bool TryLoadNativeSnapshot(NativeArray<byte> snapshot, out string error)
        {
            error = string.Empty;

            DisposeChunkStates();
            DisposeCompactedChunkStates();
            _chunkWriteVersions.Clear();
            _pendingRebuildVolumes.Clear();

            if (!snapshot.IsCreated || snapshot.Length <= 0)
            {
                RequestRebuildsForLoadedState();
                return true;
            }

            int legacyHeaderBytes = UnsafeUtility.SizeOf<LegacyNativeSnapshotHeader>();
            if (snapshot.Length < legacyHeaderBytes)
            {
                error = "Voxel delta snapshot is truncated.";
                return false;
            }

            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            int minimumHeaderBytes;
            bool snapshotHasFlags;
            bool snapshotHasRleChunks;
            bool snapshotHasDeltaRle;
            NativeSnapshotHeader header;

            if (snapshot.Length >= UnsafeUtility.SizeOf<NativeSnapshotHeader>())
            {
                NativeSnapshotHeader versionedHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotHeader>(snapshotPtr, 0);
                if (versionedHeader.Version == NativeSnapshotMagic ||
                    versionedHeader.Version == NativeSnapshotRleMagic ||
                    versionedHeader.Version == NativeSnapshotDeltaRleMagic)
                {
                    header = versionedHeader;
                    minimumHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
                    snapshotHasFlags = true;
                    snapshotHasRleChunks = versionedHeader.Version == NativeSnapshotRleMagic ||
                                           versionedHeader.Version == NativeSnapshotDeltaRleMagic;
                    snapshotHasDeltaRle = versionedHeader.Version == NativeSnapshotDeltaRleMagic;
                }
                else
                {
                    LegacyNativeSnapshotHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacyNativeSnapshotHeader>(snapshotPtr, 0);
                    header = new NativeSnapshotHeader
                    {
                        Version = 1,
                        ChunkCount = legacyHeader.ChunkCount,
                        TotalDirtyCellCount = legacyHeader.TotalDirtyCellCount
                    };
                    minimumHeaderBytes = legacyHeaderBytes;
                    snapshotHasFlags = false;
                    snapshotHasRleChunks = false;
                    snapshotHasDeltaRle = false;
                }
            }
            else
            {
                LegacyNativeSnapshotHeader legacyHeader = UnsafeUtility.ReadArrayElement<LegacyNativeSnapshotHeader>(snapshotPtr, 0);
                header = new NativeSnapshotHeader
                {
                    Version = 1,
                    ChunkCount = legacyHeader.ChunkCount,
                    TotalDirtyCellCount = legacyHeader.TotalDirtyCellCount
                };
                minimumHeaderBytes = legacyHeaderBytes;
                snapshotHasFlags = false;
                snapshotHasRleChunks = false;
                snapshotHasDeltaRle = false;
            }

            if (header.ChunkCount < 0 || header.TotalDirtyCellCount < 0)
            {
                error = "Voxel delta snapshot header is invalid.";
                return false;
            }

            int cursor = minimumHeaderBytes;
            int dirtyMaskByteLength = ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();
            int sdfByteLength = ChunkCellCount * UnsafeUtility.SizeOf<ushort>();
            int materialByteLength = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            int flagsByteLength = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            int chunkHeaderBytes = snapshotHasDeltaRle
                ? UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>()
                : snapshotHasRleChunks
                ? UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderRle>()
                : UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>();
            int loadedDirtyCellCount = 0;
            bool skippedCorruptChunk = false;

            for (int chunkIndex = 0; chunkIndex < header.ChunkCount; chunkIndex++)
            {
                if (cursor > snapshot.Length - chunkHeaderBytes)
                {
                    error = "Voxel delta chunk header exceeds the snapshot bounds.";
                    return false;
                }

                NativeSnapshotChunkHeader chunkHeader;
                byte storageFlags = NativeSnapshotStorageDense;
                int declaredPayloadBytes = 0;
                ulong declaredPayloadHash64 = 0UL;
                if (snapshotHasDeltaRle)
                {
                    NativeSnapshotChunkHeaderDeltaRle deltaHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotChunkHeaderDeltaRle>(snapshotPtr + cursor, 0);
                    chunkHeader = new NativeSnapshotChunkHeader
                    {
                        ChunkX = deltaHeader.ChunkX,
                        ChunkY = deltaHeader.ChunkY,
                        ChunkZ = deltaHeader.ChunkZ,
                        VoxelSize = deltaHeader.VoxelSize,
                        DirtyCellCount = deltaHeader.DirtyCellCount
                    };
                    storageFlags = deltaHeader.StorageFlags;
                    declaredPayloadBytes = deltaHeader.PayloadByteLength;
                    declaredPayloadHash64 = deltaHeader.PayloadHash64;
                }
                else if (snapshotHasRleChunks)
                {
                    NativeSnapshotChunkHeaderRle rleHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotChunkHeaderRle>(snapshotPtr + cursor, 0);
                    chunkHeader = new NativeSnapshotChunkHeader
                    {
                        ChunkX = rleHeader.ChunkX,
                        ChunkY = rleHeader.ChunkY,
                        ChunkZ = rleHeader.ChunkZ,
                        VoxelSize = rleHeader.VoxelSize,
                        DirtyCellCount = rleHeader.DirtyCellCount
                    };
                    storageFlags = rleHeader.StorageFlags;
                    declaredPayloadBytes = rleHeader.PayloadByteLength;
                }
                else
                {
                    chunkHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotChunkHeader>(snapshotPtr + cursor, 0);
                }

                cursor += chunkHeaderBytes;

                if (chunkHeader.VoxelSize <= 0f || chunkHeader.DirtyCellCount < 0)
                {
                    if (snapshotHasDeltaRle &&
                        declaredPayloadBytes >= 0 &&
                        cursor <= snapshot.Length - declaredPayloadBytes)
                    {
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);
                        cursor += declaredPayloadBytes;
                        skippedCorruptChunk = true;
                        continue;
                    }

                    error = "Voxel delta chunk header contains invalid values.";
                    return false;
                }

                int chunkPayloadBytes = dirtyMaskByteLength + sdfByteLength + materialByteLength + (snapshotHasFlags ? flagsByteLength : 0);
                int3 chunkCoord = new int3(chunkHeader.ChunkX, chunkHeader.ChunkY, chunkHeader.ChunkZ);
                ChunkAddress address = new ChunkAddress(chunkCoord, chunkHeader.VoxelSize);

                if (snapshotHasDeltaRle)
                {
                    if (declaredPayloadBytes < 0 || cursor > snapshot.Length - declaredPayloadBytes)
                    {
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionBoundsAction, chunkHeader.DirtyCellCount);
                        skippedCorruptChunk = true;
                        cursor = snapshot.Length;
                        break;
                    }

                    ulong computedPayloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + cursor, declaredPayloadBytes);
                    if (computedPayloadHash64 != declaredPayloadHash64)
                    {
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionHashMismatchAction, chunkHeader.DirtyCellCount);
                        cursor += declaredPayloadBytes;
                        skippedCorruptChunk = true;
                        continue;
                    }
                }

                if ((storageFlags & NativeSnapshotStorageUniformSdfRle) != 0)
                {
                    bool hasCurrentUniformPayload = declaredPayloadBytes == NativeSnapshotUniformSdfRlePayloadBytes;
                    bool hasLegacyUniformPayload = declaredPayloadBytes == NativeSnapshotLegacyUniformSdfRlePayloadBytes;
                    if ((!hasCurrentUniformPayload && !hasLegacyUniformPayload) ||
                        cursor > snapshot.Length - declaredPayloadBytes ||
                        chunkHeader.DirtyCellCount != ChunkCellCount)
                    {
                        if (snapshotHasDeltaRle)
                        {
                            ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);
                            skippedCorruptChunk = true;
                            cursor = math.min(snapshot.Length, cursor + math.max(0, declaredPayloadBytes));
                            continue;
                        }

                        error = "Voxel delta RLE payload is invalid.";
                        return false;
                    }

                    ushort sdfBits = hasCurrentUniformPayload
                        ? DequantizeSdfByte((sbyte)(*(snapshotPtr + cursor)))
                        : UnsafeUtility.ReadArrayElement<ushort>(snapshotPtr + cursor, 0);
                    cursor += declaredPayloadBytes;

                    _compactedChunkStates[address] = new CompactedChunkState(
                        chunkCoord,
                        chunkHeader.VoxelSize,
                        sdfBits,
                        DefaultMaterialId,
                        DeltaModeReplace);
                    loadedDirtyCellCount += chunkHeader.DirtyCellCount;
                    continue;
                }

                if ((storageFlags & NativeSnapshotStorageSparseDeltaRle) != 0)
                {
                    if (!TryLoadSparseRlePayload(
                            snapshotPtr + cursor,
                            declaredPayloadBytes,
                            chunkCoord,
                            chunkHeader.VoxelSize,
                            chunkHeader.DirtyCellCount,
                            address))
                    {
                        if (snapshotHasDeltaRle)
                        {
                            ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);
                            skippedCorruptChunk = true;
                            cursor += declaredPayloadBytes;
                            continue;
                        }

                        error = "Voxel delta sparse RLE payload is invalid.";
                        return false;
                    }

                    cursor += declaredPayloadBytes;
                    loadedDirtyCellCount += chunkHeader.DirtyCellCount;
                    continue;
                }

                if (snapshotHasRleChunks && declaredPayloadBytes != chunkPayloadBytes)
                {
                    if (snapshotHasDeltaRle)
                    {
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);
                        skippedCorruptChunk = true;
                        cursor += declaredPayloadBytes;
                        continue;
                    }

                    error = "Voxel delta dense payload length mismatch.";
                    return false;
                }

                if (cursor > snapshot.Length - chunkPayloadBytes)
                {
                    if (snapshotHasDeltaRle)
                    {
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionBoundsAction, chunkHeader.DirtyCellCount);
                        skippedCorruptChunk = true;
                        cursor = snapshot.Length;
                        break;
                    }

                    error = "Voxel delta chunk payload exceeds the snapshot bounds.";
                    return false;
                }

                ChunkDeltaState state = GetOrCreateChunkState(chunkCoord, chunkHeader.VoxelSize);

                void* dirtyMaskPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.DirtyMaskWords);
                if (!UnsafeMemoryCopyGuard.SafeCopy(dirtyMaskPtr, state.DirtyMaskWords.Length * UnsafeUtility.SizeOf<uint>(), snapshotPtr + cursor, dirtyMaskByteLength))
                {
                    error = "Voxel delta dirty-mask copy exceeded destination bounds.";
                    return false;
                }
                cursor += dirtyMaskByteLength;

                void* sdfPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.SdfValueBits);
                if (!UnsafeMemoryCopyGuard.SafeCopy(sdfPtr, state.SdfValueBits.Length * UnsafeUtility.SizeOf<ushort>(), snapshotPtr + cursor, sdfByteLength))
                {
                    error = "Voxel delta SDF copy exceeded destination bounds.";
                    return false;
                }
                cursor += sdfByteLength;

                void* materialPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.MaterialIds);
                if (!UnsafeMemoryCopyGuard.SafeCopy(materialPtr, state.MaterialIds.Length * UnsafeUtility.SizeOf<byte>(), snapshotPtr + cursor, materialByteLength))
                {
                    error = "Voxel delta material copy exceeded destination bounds.";
                    return false;
                }
                cursor += materialByteLength;

                if (snapshotHasFlags)
                {
                    void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.CellFlags);
                    if (!UnsafeMemoryCopyGuard.SafeCopy(flagsPtr, state.CellFlags.Length * UnsafeUtility.SizeOf<byte>(), snapshotPtr + cursor, flagsByteLength))
                    {
                        error = "Voxel delta flag copy exceeded destination bounds.";
                        return false;
                    }
                    cursor += flagsByteLength;
                }
                else
                {
                    void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.CellFlags);
                    UnsafeUtility.MemClear(flagsPtr, flagsByteLength);
                }

                state.DirtyCellCount = chunkHeader.DirtyCellCount;
                _chunkStates[address] = state;
                loadedDirtyCellCount += chunkHeader.DirtyCellCount;
            }

            if (cursor != snapshot.Length && !skippedCorruptChunk)
            {
                error = "Voxel delta snapshot contains unread trailing bytes.";
                return false;
            }

            if (loadedDirtyCellCount != header.TotalDirtyCellCount && !skippedCorruptChunk)
            {
                error = "Voxel delta snapshot dirty-cell count does not match the header.";
                return false;
            }

            RequestRebuildsForLoadedState();
            return true;
        }

        private unsafe bool TryLoadSparseRlePayload(
            byte* payloadPtr,
            int payloadBytes,
            int3 chunkCoord,
            float voxelSize,
            int dirtyCellCount,
            ChunkAddress address)
        {
            if (!TryValidateSparseRlePayload(payloadPtr, payloadBytes, dirtyCellCount))
                return false;

            ChunkDeltaState state = new ChunkDeltaState(chunkCoord, voxelSize);
            int runBytes = UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
            int runCount = payloadBytes / runBytes;
            int loadedDirtyCellCount = 0;
            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                SaveVoxelDeltaRun8 run = UnsafeUtility.ReadArrayElement<SaveVoxelDeltaRun8>(payloadPtr, runIndex);
                int startIndex = run.StartIndex;
                int runLength = run.RunLength;
                int endIndex = startIndex + runLength;
                ushort sdfBits = DequantizeSdfByte(run.SdfValue);
                SetDirtyRunBits(ref state, startIndex, runLength);
                for (int flatIndex = startIndex; flatIndex < endIndex; flatIndex++)
                {
                    state.SdfValueBits[flatIndex] = sdfBits;
                    state.MaterialIds[flatIndex] = run.MaterialId;
                    state.CellFlags[flatIndex] = run.Flags;
                }

                loadedDirtyCellCount += runLength;
            }

            state.DirtyCellCount = loadedDirtyCellCount;
            if (_chunkStates.TryGetValue(address, out ChunkDeltaState existing))
                existing.Dispose();

            _chunkStates[address] = state;
            return true;
        }

        private static unsafe bool TryValidateSparseRlePayload(byte* payloadPtr, int payloadBytes, int dirtyCellCount)
        {
            if (payloadPtr == null || payloadBytes < 0 || dirtyCellCount < 0)
                return false;

            int runBytes = UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
            if (runBytes <= 0 || payloadBytes % runBytes != 0)
                return false;

            int runCount = payloadBytes / runBytes;
            int decodedDirtyCells = 0;
            int previousEnd = 0;
            for (int runIndex = 0; runIndex < runCount; runIndex++)
            {
                SaveVoxelDeltaRun8 run = UnsafeUtility.ReadArrayElement<SaveVoxelDeltaRun8>(payloadPtr, runIndex);
                int startIndex = run.StartIndex;
                int runLength = run.RunLength;
                if (runLength <= 0 ||
                    startIndex < previousEnd ||
                    startIndex > ChunkCellCount - runLength)
                {
                    return false;
                }

                previousEnd = startIndex + runLength;
                decodedDirtyCells += runLength;
                if (decodedDirtyCells > dirtyCellCount)
                    return false;
            }

            return decodedDirtyCells == dirtyCellCount;
        }

        private static void SetDirtyRunBits(
            ref ChunkDeltaState state,
            int startIndex,
            int runLength)
        {
            int endExclusive = startIndex + runLength;
            int firstWord = startIndex >> 5;
            int lastWord = (endExclusive - 1) >> 5;
            int startBit = startIndex & 31;
            int endBit = (endExclusive - 1) & 31;
            if (firstWord == lastWord)
            {
                uint mask = (uint.MaxValue << startBit) & (uint.MaxValue >> (31 - endBit));
                state.DirtyMaskWords[firstWord] |= mask;
                return;
            }

            state.DirtyMaskWords[firstWord] |= uint.MaxValue << startBit;
            for (int wordIndex = firstWord + 1; wordIndex < lastWord; wordIndex++)
                state.DirtyMaskWords[wordIndex] = uint.MaxValue;

            state.DirtyMaskWords[lastWord] |= uint.MaxValue >> (31 - endBit);
        }

        private static void ReportVoxelDeltaChunkCorruption(uint actionMask, int dirtyCellCount)
        {
            uint context = _SaveCorruptionContextHash ^ actionMask;
            GlobalTelemetryBus.PublishSystemDegradation(_SaveCorruptionHash, context, math.max(0, dirtyCellCount));
        }

        private void TryRegisterSaveService()
        {
            if (_saveRegistered || GlobalRegistry.Save == null)
                return;

            GlobalRegistry.Save.Register(this);
            _saveRegistered = true;
        }

        private void FlushPendingRebuilds()
        {
            for (int i = _pendingRebuildVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume volume = _pendingRebuildVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasRuntimeData)
                {
                    RemoveVolumeAtSwapBack(_pendingRebuildVolumes, i);
                    continue;
                }

                volume.RequestDeltaRebuild();
                RemoveVolumeAtSwapBack(_pendingRebuildVolumes, i);
            }
        }

        private unsafe void TrySchedulePendingCarve()
        {
            if (IsScheduledCarveBusy || _pendingCarveCount <= 0)
                return;

            PendingCarveRequest request = PopPendingCarve();
            HectonVoxelVolume volume = request.Volume;
            if (volume == null || !volume.HasRuntimeData)
                return;

            float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
            byte shape = request.Shape == DeltaShapeBox
                ? DeltaShapeBox
                : request.Shape == DeltaShapeCapsule
                    ? DeltaShapeCapsule
                    : DeltaShapeSphere;
            float radius = shape == DeltaShapeBox ? 0f : ResolveCarveRadius(in request, volume);
            if (shape != DeltaShapeBox && radius <= 0f)
                return;

            float blendRadius = shape == DeltaShapeBox
                ? math.max(voxelSize, ResolveBlendStrength(in request, voxelSize))
                : math.max(voxelSize, radius * 0.35f);
            float3 halfExtents = shape == DeltaShapeBox
                ? new float3(
                    math.max(voxelSize, math.abs(request.AbsoluteHalfExtents.x)),
                    math.max(voxelSize, math.abs(request.AbsoluteHalfExtents.y)),
                    math.max(voxelSize, math.abs(request.AbsoluteHalfExtents.z)))
                : new float3(radius);
            double3 segmentStart = request.AbsoluteHitPoint;
            double3 segmentEnd = shape == DeltaShapeCapsule
                ? request.AbsoluteSegmentEnd
                : segmentStart;
            double3 boundsMin = shape == DeltaShapeCapsule
                ? math.min(segmentStart, segmentEnd)
                : segmentStart - new double3(halfExtents.x, halfExtents.y, halfExtents.z);
            double3 boundsMax = shape == DeltaShapeCapsule
                ? math.max(segmentStart, segmentEnd)
                : segmentStart + new double3(halfExtents.x, halfExtents.y, halfExtents.z);
            float boundsPadding = shape == DeltaShapeCapsule ? radius + blendRadius : blendRadius;
            int3 minCell = new int3(
                FastFloorToInt((boundsMin.x - boundsPadding) / voxelSize),
                FastFloorToInt((boundsMin.y - boundsPadding) / voxelSize),
                FastFloorToInt((boundsMin.z - boundsPadding) / voxelSize));
            int3 maxCell = new int3(
                FastFloorToInt((boundsMax.x + boundsPadding) / voxelSize),
                FastFloorToInt((boundsMax.y + boundsPadding) / voxelSize),
                FastFloorToInt((boundsMax.z + boundsPadding) / voxelSize));
            ResolveVolumeCellBounds(volume, out int3 volumeMinCell, out int3 volumeMaxCell, out _, out _);
            if (!CellBoundsIntersect(minCell, maxCell, volumeMinCell, volumeMaxCell))
                return;

            minCell = math.max(minCell, volumeMinCell);
            maxCell = math.min(maxCell, volumeMaxCell);
            if ((request.SourceFlags & CarveSourceLaser) != 0)
                ClampLocalizedLaserCarveBounds(ref minCell, ref maxCell, volumeMinCell, volumeMaxCell, segmentStart, voxelSize);

            int3 span = (maxCell - minCell) + 1;
            int candidateCount = math.max(0, span.x) * math.max(0, span.y) * math.max(0, span.z);
            if (candidateCount <= 0)
                return;

            bool scheduled = false;
            try
            {
                EnsureScheduledCarveWriteCapacity(candidateCount);
                _scheduledCarveRequest = request;
                ResetScheduledCarveCommitProgress();

                CarveSdfJob carveJob = new CarveSdfJob
                {
                    MinCell = minCell,
                    Span = span,
                    VoxelSize = voxelSize,
                    Radius = radius,
                    BlendRadius = blendRadius,
                    BlendStrength = ResolveBlendStrength(in request, voxelSize),
                    Center = segmentStart,
                    SegmentEnd = segmentEnd,
                    HalfExtents = halfExtents,
                    MaterialId = request.MaterialId,
                    DeltaFlags = request.DeltaFlags,
                    Shape = shape,
                    Writes = _scheduledCarveWrites,
                    WritesPtr = (CarveCellWrite*)NativeArrayUnsafeUtility.GetUnsafePtr(_scheduledCarveWrites)
                };

                using (_carveScheduleProfilerMarker.Auto())
                {
                    _scheduledCarveWriteCount = candidateCount;
                    _scheduledCarveHandle = carveJob.Schedule(candidateCount, 64);
                    _scheduledCarveRunning = true;
                    scheduled = true;
                    PublishDebrisSpawnSignal(in request, radius);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[VoxelDeltaProcessor] Failed to schedule voxel CSG carve: " + exception.Message, this);
#endif
            }
            finally
            {
                if (!scheduled)
                {
                    _scheduledCarveHandle = default;
                    _scheduledCarveRunning = false;
                    _scheduledCarveCommitPending = false;
                    _scheduledCarveCommitIndex = 0;
                    _scheduledCarveWriteCount = 0;
                    _scheduledCarveRequest = default;

                    if (_scheduledCarveWrites.IsCreated)
                    {
                        DisposeTrackedNativeArray(ref _scheduledCarveWrites);
                    }
                }
            }
        }

        private void TryCommitScheduledCarve()
        {
            if (!_scheduledCarveRunning && !_scheduledCarveCommitPending)
                return;

            using (_carveCommitProfilerMarker.Auto())
            {
                long commitStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    if (_scheduledCarveRunning)
                    {
                        if (!DispatcherJobSwap.TryComplete(ref _scheduledCarveHandle, false))
                            return;

                        _scheduledCarveRunning = false;
                        _scheduledCarveCommitPending = true;
                        ResetScheduledCarveCommitProgress();
                    }

                    HectonVoxelVolume volume = _scheduledCarveRequest.Volume;
                    if (volume == null || !volume.HasRuntimeData)
                    {
                        ResetScheduledCarveState();
                        return;
                    }

                    float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
                    int writeCount = math.min(_scheduledCarveWriteCount, _scheduledCarveWrites.Length);
                    int endIndex = math.min(_scheduledCarveCommitIndex + MaxScheduledCarveCommitWritesPerFrame, writeCount);
                    for (int i = _scheduledCarveCommitIndex; i < endIndex; i++)
                    {
                        CarveCellWrite write = _scheduledCarveWrites[i];
                        if (write.IsActive == 0)
                            continue;

                        int3 chunkCoord = FloorDiv(write.AbsoluteCell, ChunkResolution);
                        ChunkAddress address = new ChunkAddress(chunkCoord, voxelSize);
                        ChunkDeltaState state = GetOrCreateChunkState(chunkCoord, voxelSize);
                        if (!TryComputeLocalCellIndex(write.AbsoluteCell, state.ChunkCoord, out uint localIndex))
                            continue;

                        half resolvedValue = BitsToHalf(write.SdfValueBits);
                        if ((write.DeltaFlags & DeltaModeAdditive) != 0)
                        {
                            float currentDensity;
                            if (!TryResolveCurrentCellDensity(volume, in state, localIndex, write.AbsoluteCell, voxelSize, out currentDensity))
                                currentDensity = 0f;

                            resolvedValue = ClampToHalf(SmoothMaxQuadratic(currentDensity, (float)resolvedValue, math.max(voxelSize, write.BlendStrength)));
                        }

                        SetCell(ref state, localIndex, resolvedValue, write.MaterialId, write.DeltaFlags);
                        _chunkStates[address] = state;
                        _scheduledCarveTouchedMinCell = math.min(_scheduledCarveTouchedMinCell, write.AbsoluteCell);
                        _scheduledCarveTouchedMaxCell = math.max(_scheduledCarveTouchedMaxCell, write.AbsoluteCell);
                        _scheduledCarveTouchedAnyCell = true;
                        IncrementChunkWriteVersion(address);
                        TryQueueCompaction(volume, address, in state, state.DirtyCellCount);
                    }

                    _scheduledCarveCommitIndex = endIndex;
                    if (_scheduledCarveCommitIndex < writeCount)
                        return;

                    if (!_scheduledCarveTouchedAnyCell)
                    {
                        ResetScheduledCarveState();
                        return;
                    }

                    VoxelDynamicNavGridRuntime.QueueLocalizedSdfPatch(volume, _scheduledCarveTouchedMinCell, _scheduledCarveTouchedMaxCell, voxelSize);
                    PublishVoxelChunkModifiedEvent(volume, voxelSize);

                    EnqueueVolumeRebuild(volume);
                    float resolvedCarveRadius = ResolveCarveRadius(in _scheduledCarveRequest, volume);
                    EmitCaveInDustDecal(in _scheduledCarveRequest, resolvedCarveRadius);
                    bool emittedTransientLaserDebris = false;
                    if ((_scheduledCarveRequest.SourceFlags & CarveSourceLaser) != 0 &&
                        (_scheduledCarveRequest.DeltaFlags & DeltaModeAdditive) == 0 &&
                        _scheduledCarveRequest.Shape != DeltaShapeBox)
                    {
                        PushRecentCutHeat(in _scheduledCarveRequest, resolvedCarveRadius);
                        emittedTransientLaserDebris = EmitLaserCarveDebris(
                            in _scheduledCarveRequest,
                            resolvedCarveRadius);
                    }

                    if (!emittedTransientLaserDebris &&
                        (_scheduledCarveRequest.DeltaFlags & DeltaModeAdditive) == 0 &&
                        _scheduledCarveRequest.Shape != DeltaShapeBox)
                    {
                        EmitCarveDebris(in _scheduledCarveRequest, resolvedCarveRadius);
                    }

                    ResetScheduledCarveState();
                }
                finally
                {
                    PublishCarveCommitWarningIfNeeded(commitStartTimestamp);
                }
            }
        }

        private void PublishCarveCommitWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = global::System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMs = elapsedTicks * 1000d / global::System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMs <= CarveCommitWarningMs)
            {
                _carveCommitWarningArmed = false;
                return;
            }

            if (_carveCommitWarningArmed)
                return;

            _carveCommitWarningArmed = true;
            WriteBlackBoxSample(ResolveScheduledCarveVolumeId(), VoxelBlackBoxCommitBudgetFlag);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _CarveCommitWarningHash,
                _CarveCommitTelemetryContextHash,
                (float)elapsedMs);
        }

        private void RequestRebuildsForLoadedState()
        {
            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
            }
        }

        private bool HasOverlappingDelta(HectonVoxelVolume volume)
        {
            if (volume == null || (_chunkStates.Count == 0 && _compactedChunkStates.Count == 0))
                return false;

            ResolveVolumeCellBounds(volume, out _, out _, out int3 minChunk, out int3 maxChunk);
            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        ChunkAddress address = new ChunkAddress(new int3(x, y, z), volume.VoxelSize);
                        if (_compactedChunkStates.ContainsKey(address))
                            return true;

                        if (_chunkStates.TryGetValue(address, out ChunkDeltaState state) &&
                            CountDirtyCells(in state) > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private void ResolveVolumeCellBounds(
            HectonVoxelVolume volume,
            out int3 minCell,
            out int3 maxCell,
            out int3 minChunk,
            out int3 maxChunk)
        {
            float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
            float halfExtent = volume.GridDimension * voxelSize * 0.5f;
            double3 absoluteCenter = volume.GenerationAbsoluteUniversePositionDouble;
            double3 minAbsolute = absoluteCenter - new double3(halfExtent, halfExtent, halfExtent);
            double3 maxAbsolute = absoluteCenter + new double3(halfExtent, halfExtent, halfExtent);

            minCell = new int3(
                FastFloorToInt(minAbsolute.x / voxelSize),
                FastFloorToInt(minAbsolute.y / voxelSize),
                FastFloorToInt(minAbsolute.z / voxelSize));
            maxCell = new int3(
                FastFloorToInt(maxAbsolute.x / voxelSize),
                FastFloorToInt(maxAbsolute.y / voxelSize),
                FastFloorToInt(maxAbsolute.z / voxelSize));
            minChunk = FloorDiv(minCell, ChunkResolution);
            maxChunk = FloorDiv(maxCell, ChunkResolution);
        }

        private void EnqueueVolumeRebuild(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            for (int i = 0; i < _pendingRebuildVolumes.Count; i++)
            {
                if (ReferenceEquals(_pendingRebuildVolumes[i], volume))
                    return;
            }

            if (_pendingRebuildVolumes.Count >= _pendingRebuildVolumes.Capacity)
            {
                if (volume.isActiveAndEnabled && volume.HasRuntimeData)
                    volume.RequestDeltaRebuild();
                return;
            }

            _pendingRebuildVolumes.Add(volume);
        }

        private ChunkDeltaState GetOrCreateChunkState(int3 chunkCoord, float voxelSize)
        {
            ChunkAddress address = new ChunkAddress(chunkCoord, voxelSize);
            if (_chunkStates.TryGetValue(address, out ChunkDeltaState existing))
                return existing;

            _chunkStates.EnsureCapacity(_chunkStates.Count + 1);
            ChunkDeltaState created = new ChunkDeltaState(chunkCoord, voxelSize);
            _chunkStates.Add(address, created);
            return created;
        }

        private void ResetScheduledCarveCommitProgress()
        {
            _scheduledCarveCommitIndex = 0;
            _scheduledCarveTouchedMinCell = new int3(int.MaxValue);
            _scheduledCarveTouchedMaxCell = new int3(int.MinValue);
            _scheduledCarveTouchedAnyCell = false;
        }

        private void ResetScheduledCarveState()
        {
            _scheduledCarveHandle = default;
            _scheduledCarveRunning = false;
            _scheduledCarveRequest = default;
            _scheduledCarveWriteCount = 0;
            _scheduledCarveCommitPending = false;
            ResetScheduledCarveCommitProgress();
        }

        private void IncrementChunkWriteVersion(ChunkAddress address)
        {
            _chunkWriteVersions.TryGetValue(address, out int version);
            _chunkWriteVersions[address] = version + 1;
        }

        private int ResolveChunkWriteVersion(ChunkAddress address)
        {
            return _chunkWriteVersions.TryGetValue(address, out int version) ? version : 0;
        }

        private void TryQueueCompaction(HectonVoxelVolume volume, ChunkAddress address, in ChunkDeltaState state, int dirtyCount)
        {
            if (volume == null ||
                dirtyCount < ChunkCompactionDirtyThreshold)
            {
                return;
            }

            int requiredSonarVersion = volume.PublishedSonarVersion + 1;
            int writeVersion = ResolveChunkWriteVersion(address);
            for (int i = 0; i < _pendingCompactionCount; i++)
            {
                int slot = ResolvePendingCompactionSlot(_pendingCompactionHead, i);
                PendingCompactionRequest pending = _pendingCompactions[slot];
                if (!pending.Address.Equals(address))
                    continue;

                pending.Volume = volume;
                pending.RequiredSonarVersion = math.max(pending.RequiredSonarVersion, requiredSonarVersion);
                pending.WriteVersion = writeVersion;
                pending.DirtyCount = math.max(pending.DirtyCount, dirtyCount);
                _pendingCompactions[slot] = pending;
                return;
            }

            PendingCompactionRequest request = new PendingCompactionRequest
            {
                Volume = volume,
                Address = address,
                RequiredSonarVersion = requiredSonarVersion,
                WriteVersion = writeVersion,
                DirtyCount = dirtyCount
            };

            TryEnqueueCompaction(in request);
        }

        private bool TryEnqueueCompaction(in PendingCompactionRequest request)
        {
            if (_pendingCompactionCount < _pendingCompactions.Length)
            {
                EnqueuePendingCompactionUnchecked(in request);
                return true;
            }

            int replacementSlot = -1;
            int lowestDirtyCount = request.DirtyCount;
            for (int i = 0; i < _pendingCompactionCount; i++)
            {
                int slot = ResolvePendingCompactionSlot(_pendingCompactionHead, i);
                int candidateDirtyCount = _pendingCompactions[slot].DirtyCount;
                if (!ShouldReplaceQueuedCompaction(lowestDirtyCount, candidateDirtyCount))
                    continue;

                lowestDirtyCount = candidateDirtyCount;
                replacementSlot = slot;
            }

            if (replacementSlot < 0)
                return false;

            _pendingCompactions[replacementSlot] = request;
            return true;
        }

        private static bool ShouldReplaceQueuedCompaction(int requestDirtyCount, int candidateDirtyCount)
        {
            return candidateDirtyCount < requestDirtyCount;
        }

        private unsafe void TrySchedulePendingCompaction()
        {
            if (_scheduledCompactionRunning || _pendingCompactionCount <= 0)
                return;

            PendingCompactionRequest request = PopPendingCompaction();
            HectonVoxelVolume volume = request.Volume;
            if (volume == null || !volume.HasRuntimeData)
                return;

            if (volume.PublishedSonarVersion < request.RequiredSonarVersion)
            {
                RequeueCompaction(in request);
                return;
            }

            if (!_chunkStates.TryGetValue(request.Address, out ChunkDeltaState state))
                return;

            int currentDirtyCount = state.DirtyCellCount;
            if (currentDirtyCount < ChunkCompactionDirtyThreshold)
                return;

            if (!volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out int publishedSonarVersion))
            {
                volume.RequestDeltaRebuild();
                request.RequiredSonarVersion = volume.PublishedSonarVersion + 1;
                request.DirtyCount = currentDirtyCount;
                RequeueCompaction(in request);
                return;
            }

            if (publishedSonarVersion < request.RequiredSonarVersion)
            {
                RequeueCompaction(in request);
                return;
            }

            int snapshotWriteVersion = ResolveChunkWriteVersion(request.Address);
            NativeArray<byte> sourceSdf = default;
            NativeArray<uint> dirtyMaskCopy = default;
            NativeArray<ushort> deltaSdfCopy = default;
            NativeArray<byte> materialCopy = default;
            NativeArray<byte> flagsCopy = default;
            NativeArray<ushort> outputSdf = default;
            NativeArray<byte> outputMaterials = default;
            NativeArray<byte> outputFlags = default;
            NativeArray<byte> rleUniformFlag = default;
            bool scheduled = false;
            try
            {
                // COLD ALLOC: NativeArray<byte>[encodedSdf.Length] - isolated SDF source for async compaction - owner: VoxelDeltaProcessor
                sourceSdf = new NativeArray<byte>(encodedSdf.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                RegisterTrackedNativeArray(sourceSdf, nameof(sourceSdf));
                NativeArray<byte>.Copy(encodedSdf, sourceSdf, encodedSdf.Length);

                // COLD ALLOC: NativeArray<uint>[1024] - dirty mask snapshot for async compaction - owner: VoxelDeltaProcessor
                dirtyMaskCopy = new NativeArray<uint>(ChunkDirtyMaskWordCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<ushort>[32768] - delta SDF snapshot for async compaction - owner: VoxelDeltaProcessor
                deltaSdfCopy = new NativeArray<ushort>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<byte>[32768] - delta material snapshot for async compaction - owner: VoxelDeltaProcessor
                materialCopy = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<byte>[32768] - delta flag snapshot for async compaction - owner: VoxelDeltaProcessor
                flagsCopy = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<ushort>[32768] - compacted replacement SDF output - owner: VoxelDeltaProcessor
                outputSdf = new NativeArray<ushort>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<byte>[32768] - compacted replacement material output - owner: VoxelDeltaProcessor
                outputMaterials = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<byte>[32768] - compacted replacement flag output - owner: VoxelDeltaProcessor
                outputFlags = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                // COLD ALLOC: NativeArray<byte>[1] - worker-detected uniform RLE replacement flag - owner: VoxelDeltaProcessor
                rleUniformFlag = new NativeArray<byte>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                RegisterTrackedNativeArray(dirtyMaskCopy, nameof(dirtyMaskCopy));
                RegisterTrackedNativeArray(deltaSdfCopy, nameof(deltaSdfCopy));
                RegisterTrackedNativeArray(materialCopy, nameof(materialCopy));
                RegisterTrackedNativeArray(flagsCopy, nameof(flagsCopy));
                RegisterTrackedNativeArray(outputSdf, nameof(outputSdf));
                RegisterTrackedNativeArray(outputMaterials, nameof(outputMaterials));
                RegisterTrackedNativeArray(outputFlags, nameof(outputFlags));
                RegisterTrackedNativeArray(rleUniformFlag, nameof(rleUniformFlag));
                NativeArray<uint>.Copy(state.DirtyMaskWords, dirtyMaskCopy, ChunkDirtyMaskWordCount);
                NativeArray<ushort>.Copy(state.SdfValueBits, deltaSdfCopy, ChunkCellCount);
                NativeArray<byte>.Copy(state.MaterialIds, materialCopy, ChunkCellCount);
                NativeArray<byte>.Copy(state.CellFlags, flagsCopy, ChunkCellCount);

                _scheduledCompactionRequest = new ScheduledCompactionRequest
                {
                    Volume = volume,
                    Address = request.Address,
                    RequiredSonarVersion = request.RequiredSonarVersion,
                    WriteVersion = snapshotWriteVersion,
                    SourceEncodedSdf = sourceSdf,
                    DirtyMaskWords = dirtyMaskCopy,
                    DeltaSdfValueBits = deltaSdfCopy,
                    DeltaMaterialIds = materialCopy,
                    DeltaCellFlags = flagsCopy,
                    OutputSdfValueBits = outputSdf,
                    OutputMaterialIds = outputMaterials,
                    OutputCellFlags = outputFlags,
                    RleUniformFlag = rleUniformFlag
                };

                VoxelDeltaCompactionJob job = new VoxelDeltaCompactionJob
                {
                    ChunkCoord = request.Address.ChunkCoord,
                    VoxelSize = math.max(request.Address.VoxelSize, MinRuntimeVoxelSize),
                    GridDimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z),
                    GridStrideY = gridDimensions.x,
                    GridStrideZ = gridDimensions.x * gridDimensions.y,
                    VolumeOrigin = new float3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z),
                    InvCellSize = new float3(
                        1f / math.max(voxelCellSize.x, 0.0001f),
                        1f / math.max(voxelCellSize.y, 0.0001f),
                        1f / math.max(voxelCellSize.z, 0.0001f)),
                    SdfDecodeScale = sdfRange * (2f / 255f),
                    SdfDecodeBias = -sdfRange,
                    EncodedSdf = sourceSdf,
                    DirtyMaskWords = dirtyMaskCopy,
                    DeltaSdfValueBits = deltaSdfCopy,
                    DeltaMaterialIds = materialCopy,
                    DeltaCellFlags = flagsCopy,
                    OutputSdfValueBits = outputSdf,
                    OutputMaterialIds = outputMaterials,
                    OutputCellFlags = outputFlags,
                    EncodedSdfPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceSdf),
                    DirtyMaskWordsPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dirtyMaskCopy),
                    DeltaSdfValueBitsPtr = (ushort*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(deltaSdfCopy),
                    DeltaMaterialIdsPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(materialCopy),
                    DeltaCellFlagsPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(flagsCopy),
                    OutputSdfValueBitsPtr = (ushort*)NativeArrayUnsafeUtility.GetUnsafePtr(outputSdf),
                    OutputMaterialIdsPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(outputMaterials),
                    OutputCellFlagsPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(outputFlags)
                };
                JobHandle compactionHandle = job.Schedule(ChunkCellCount, 64);
                _scheduledCompactionHandle = new VoxelDeltaUniformRunDetectJob
                {
                    SdfValueBits = outputSdf,
                    MaterialIds = outputMaterials,
                    CellFlags = outputFlags,
                    UniformFlag = rleUniformFlag
                }.Schedule(compactionHandle);
                _scheduledCompactionRunning = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                {
                    DisposeTrackedNativeArray(ref sourceSdf);
                    DisposeTrackedNativeArray(ref dirtyMaskCopy);
                    DisposeTrackedNativeArray(ref deltaSdfCopy);
                    DisposeTrackedNativeArray(ref materialCopy);
                    DisposeTrackedNativeArray(ref flagsCopy);
                    DisposeTrackedNativeArray(ref outputSdf);
                    DisposeTrackedNativeArray(ref outputMaterials);
                    DisposeTrackedNativeArray(ref outputFlags);
                    DisposeTrackedNativeArray(ref rleUniformFlag);
                    _scheduledCompactionRequest = default;
                    _scheduledCompactionHandle = default;
                    _scheduledCompactionRunning = false;
                }
            }
        }

        private void RequeueCompaction(in PendingCompactionRequest request)
        {
            TryEnqueueCompaction(in request);
        }

        private void TrySchedulePendingCompactionFrostTick()
        {
            if (_scheduledCompactionRunning)
                return;

            if (_pendingCompactionCount <= 0)
            {
                _compactionFrostTickCounter = 0;
                return;
            }

            _compactionFrostTickCounter++;
            if (_compactionFrostTickCounter < CompactionFrostTickIntervalFrames)
                return;

            _compactionFrostTickCounter = 0;
            TrySchedulePendingCompaction();
        }

        private void TryCommitScheduledCompaction()
        {
            if (!_scheduledCompactionRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledCompactionHandle, false))
                return;

            _scheduledCompactionRunning = false;
            ScheduledCompactionRequest request = _scheduledCompactionRequest;
            DisposeTrackedNativeArray(ref request.SourceEncodedSdf);
            DisposeTrackedNativeArray(ref request.DirtyMaskWords);
            DisposeTrackedNativeArray(ref request.DeltaSdfValueBits);
            DisposeTrackedNativeArray(ref request.DeltaMaterialIds);
            DisposeTrackedNativeArray(ref request.DeltaCellFlags);

            if (_compactedChunkStates.TryGetValue(request.Address, out CompactedChunkState existingCompacted))
                existingCompacted.Dispose();

            _compactedChunkStates[request.Address] = new CompactedChunkState(
                request.Address.ChunkCoord,
                request.Address.VoxelSize,
                request.OutputSdfValueBits,
                request.OutputMaterialIds,
                request.OutputCellFlags,
                request.RleUniformFlag);
            DisposeTrackedNativeArray(ref request.RleUniformFlag);

            if (ResolveChunkWriteVersion(request.Address) == request.WriteVersion &&
                _chunkStates.TryGetValue(request.Address, out ChunkDeltaState dirtyState))
            {
                dirtyState.Dispose();
                _chunkStates.Remove(request.Address);
                _chunkWriteVersions.Remove(request.Address);
            }

            _scheduledCompactionRequest = default;
            _scheduledCompactionHandle = default;
        }

        private static float ResolveBlendStrength(in PendingCarveRequest request, float voxelSize)
        {
            return request.ExplicitBlendStrength > 0f
                ? math.max(voxelSize, request.ExplicitBlendStrength)
                : math.max(voxelSize, request.ExplicitRadiusMeters * 0.35f);
        }

        private static bool TryResolveCurrentCellDensity(
            HectonVoxelVolume volume,
            in ChunkDeltaState state,
            uint localIndex,
            int3 absoluteCell,
            float voxelSize,
            out float density)
        {
            if (IsDirty(in state, localIndex))
            {
                density = (float)BitsToHalf(state.SdfValueBits[(int)localIndex]);
                return true;
            }

            if (volume != null)
            {
                double3 absoluteCellCenter = (new double3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5d) * voxelSize;
                Vector3 runtimeCellCenter = HectonFloatingOrigin.ToRuntimePosition(absoluteCellCenter);
                if (volume.TrySampleDensity(runtimeCellCenter, out density))
                    return true;
            }

            density = 0f;
            return false;
        }

        private static int CountDirtyCells(in ChunkDeltaState state)
        {
            if (!state.DirtyMaskWords.IsCreated)
                return 0;

            if (state.DirtyCellCount > 0)
                return state.DirtyCellCount;

            int dirtyCount = 0;
            for (int i = 0; i < state.DirtyMaskWords.Length; i++)
                dirtyCount += math.countbits(state.DirtyMaskWords[i]);

            return dirtyCount;
        }

        private static int CountDirtyCells(uint[] dirtyMaskWords)
        {
            if (dirtyMaskWords == null)
                return 0;

            int dirtyCount = 0;
            int wordCount = math.min(dirtyMaskWords.Length, ChunkDirtyMaskWordCount);
            for (int i = 0; i < wordCount; i++)
                dirtyCount += math.countbits(dirtyMaskWords[i]);

            return dirtyCount;
        }

        private static bool HasDenseStorage(in VoxelDeltaChunkDTO chunk)
        {
            return (chunk.storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) == 0 &&
                   chunk.dirtyMaskWords != null &&
                   chunk.dirtyMaskWords.Length == ChunkDirtyMaskWordCount &&
                   chunk.sdfValueBits != null &&
                   chunk.sdfValueBits.Length == ChunkCellCount &&
                   chunk.materialIds != null &&
                   chunk.materialIds.Length == ChunkCellCount;
        }

        private static bool TryComputeLocalCellIndex(int3 absoluteCell, int3 chunkCoord, out uint localIndex)
        {
            int3 localCell = absoluteCell - (chunkCoord * ChunkResolution);
            if (localCell.x < 0 || localCell.x >= ChunkResolution ||
                localCell.y < 0 || localCell.y >= ChunkResolution ||
                localCell.z < 0 || localCell.z >= ChunkResolution)
            {
                localIndex = 0u;
                return false;
            }

            localIndex = (uint)(localCell.x | (localCell.y << 5) | (localCell.z << 10));
            return true;
        }

        private static int3 AbsoluteCellFromLocalIndex(int3 chunkCoord, int flatIndex)
        {
            int localX = flatIndex & (ChunkResolution - 1);
            int localY = (flatIndex >> 5) & (ChunkResolution - 1);
            int localZ = flatIndex >> 10;
            return (chunkCoord * ChunkResolution) + new int3(localX, localY, localZ);
        }

        private static bool IsDirty(in ChunkDeltaState state, uint localIndex)
        {
            int wordIndex = (int)(localIndex >> 5);
            uint bitMask = 1u << ((int)localIndex & 31);
            return (state.DirtyMaskWords[wordIndex] & bitMask) != 0u;
        }

        private static void SetDirtyBit(ref ChunkDeltaState state, uint localIndex)
        {
            int wordIndex = (int)(localIndex >> 5);
            uint bitMask = 1u << ((int)localIndex & 31);
            state.DirtyMaskWords[wordIndex] |= bitMask;
        }

        private static void SetCell(ref ChunkDeltaState state, uint localIndex, half value, byte materialId, byte cellFlags)
        {
            int flatIndex = (int)localIndex;
            bool isDirty = IsDirty(in state, localIndex);
            if (!isDirty)
            {
                SetDirtyBit(ref state, localIndex);
                state.DirtyCellCount++;
                state.SdfValueBits[flatIndex] = HalfToBits(value);
                state.CellFlags[flatIndex] = cellFlags;
            }
            else
            {
                byte existingFlags = state.CellFlags[flatIndex];
                bool replace = (cellFlags & DeltaModeReplace) != 0;
                bool existingReplace = (existingFlags & DeltaModeReplace) != 0;
                float existingValue = (float)BitsToHalf(state.SdfValueBits[flatIndex]);
                float nextValue = (float)value;

                if (replace || existingReplace)
                {
                    state.SdfValueBits[flatIndex] = HalfToBits(value);
                    state.CellFlags[flatIndex] = cellFlags;
                }
                else
                {
                    float mergedValue = MergeSdfDeltaDensity(existingValue, existingFlags, nextValue, cellFlags);
                    state.SdfValueBits[flatIndex] = HalfToBits(ClampToHalf(mergedValue));
                    if (((existingFlags ^ cellFlags) & DeltaModeAdditive) != 0)
                        state.CellFlags[flatIndex] = cellFlags;
                }
            }

            state.MaterialIds[flatIndex] = materialId;
        }

        private static float MergeSdfDeltaDensity(
            float existingValue,
            byte existingFlags,
            float nextValue,
            byte nextFlags)
        {
            if (((existingFlags | nextFlags) & DeltaModeReplace) != 0)
                return nextValue;

            bool existingAdditive = (existingFlags & DeltaModeAdditive) != 0;
            bool nextAdditive = (nextFlags & DeltaModeAdditive) != 0;
            if (existingAdditive != nextAdditive)
                return nextValue;

            return nextAdditive
                ? math.max(existingValue, nextValue)
                : math.min(existingValue, nextValue);
        }

        private static float BakeDeltaIntoBaseDensity(float baseValue, float deltaValue, byte deltaFlags)
        {
            if ((deltaFlags & DeltaModeReplace) != 0)
                return deltaValue;

            return (deltaFlags & DeltaModeAdditive) != 0
                ? math.max(baseValue, deltaValue)
                : math.min(baseValue, deltaValue);
        }

        private static ushort HalfToBits(half value)
        {
            return UnsafeUtility.As<half, ushort>(ref value);
        }

        private static half BitsToHalf(ushort bits)
        {
            return UnsafeUtility.As<ushort, half>(ref bits);
        }

        private static sbyte QuantizeSdfByte(ushort sdfBits)
        {
            float density = (float)BitsToHalf(sdfBits);
            return (sbyte)math.clamp((int)math.round(density * SparseRleSdfByteScale), -127, 127);
        }

        private static ushort DequantizeSdfByte(sbyte value)
        {
            float density = value * SparseRleSdfByteInvScale;
            return HalfToBits(ClampToHalf(density));
        }

        private static float SmoothMaxQuadratic(float a, float b, float k)
        {
            float width = math.max(k, 0.0001f);
            float blend = math.max(0f, width - math.abs(a - b));
            float smoothLift = (blend * blend) * (0.25f / width);
            return math.max(a, b) + smoothLift;
        }

        private static int CastBiasInt(float value)
        {
            return value >= 0f ? (int)(value + 0.5f) : (int)(value - 0.5f);
        }

        private static int CastBiasInt(double value)
        {
            if (!math.isfinite(value))
                return 0;

            double rounded = value >= 0d ? value + 0.5d : value - 0.5d;
            if (rounded >= int.MaxValue)
                return int.MaxValue;
            if (rounded <= int.MinValue)
                return int.MinValue;

            return (int)rounded;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            if (ax <= 0.0001f && ay <= 0.0001f && az <= 0.0001f)
                return Vector3.up;

            if (ax >= ay && ax >= az)
                return new Vector3(value.x < 0f ? -1f : 1f, 0f, 0f);

            if (ay >= az)
                return new Vector3(0f, value.y < 0f ? -1f : 1f, 0f);

            return new Vector3(0f, 0f, value.z < 0f ? -1f : 1f);
        }

        private static void PublishDebrisSpawnSignal(in PendingCarveRequest request, float radius)
        {
            if ((request.DeltaFlags & DeltaModeAdditive) != 0 ||
                request.Shape == DeltaShapeBox ||
                radius <= 0f)
            {
                return;
            }

            float intensity01 = math.saturate(radius / math.max(MaxCarveRadiusMeters, MinCarveRadiusMeters));
            uint sourceId = (uint)math.hash(new int4(
                CastBiasInt(request.AbsoluteHitPoint.x * 8d),
                CastBiasInt(request.AbsoluteHitPoint.y * 8d),
                CastBiasInt(request.AbsoluteHitPoint.z * 8d),
                request.Shape | (request.SourceFlags << 8)));
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(request.AbsoluteHitPoint),
                SpeciesHash = _VoxelDebrisSignalHash,
                SourceEntityId = sourceId,
                Intensity01 = intensity01,
                DebrisKind = (request.SourceFlags & CarveSourceLaser) != 0 ? (byte)1 : (byte)0,
                Flags = request.SourceFlags
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishVoxelChunkModifiedEvent(HectonVoxelVolume volume, float voxelSize)
        {
            if (volume == null || !_scheduledCarveTouchedAnyCell || voxelSize <= 0f)
            {
                return;
            }

            uint stateHash = 2166136261u;
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveTouchedMinCell.x);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveTouchedMinCell.y);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveTouchedMinCell.z);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveTouchedMaxCell.x);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveTouchedMaxCell.y);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveTouchedMaxCell.z);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveRequest.Shape);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveRequest.SourceFlags);

            VoxelChunkModifiedEvent modifiedEvent = new VoxelChunkModifiedEvent
            {
                VolumeInstanceId = EntityId.ToULong(volume.GetEntityId()),
                MinAbsoluteCell = _scheduledCarveTouchedMinCell,
                MaxAbsoluteCell = _scheduledCarveTouchedMaxCell,
                VoxelSize = voxelSize,
                Frame = (uint)Time.frameCount,
                Operation = ResolveVoxelChunkModifiedOperation(_scheduledCarveRequest.DeltaFlags),
                Shape = _scheduledCarveRequest.Shape,
                Flags = _scheduledCarveRequest.SourceFlags,
                StateHash = stateHash
            };

            VoxelChunkModifiedEvents.Publish(in modifiedEvent);
        }

        private static byte ResolveVoxelChunkModifiedOperation(byte deltaFlags)
        {
            if ((deltaFlags & DeltaModeAdditive) != 0)
            {
                return (byte)VoxelCarveOperationType.Add;
            }

            if ((deltaFlags & DeltaModeReplace) != 0)
            {
                return (byte)VoxelCarveOperationType.Replace;
            }

            return (byte)VoxelCarveOperationType.Subtract;
        }

        private float ResolveCarveRadius(in PendingCarveRequest request, HectonVoxelVolume volume)
        {
            if (request.ExplicitRadiusMeters > 0f)
                return math.max(math.max(volume.VoxelSize * 1.25f, MinCarveRadiusMeters), request.ExplicitRadiusMeters);

            float baseRadius = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            return math.clamp(baseRadius + request.AccumulatedDamage * 0.08f, baseRadius, math.max(baseRadius, MaxCarveRadiusMeters));
        }

        private void EmitCarveDebris(in PendingCarveRequest request, float radius)
        {
            if (carveDebrisItem == null || carveDebrisPerCubicMeter <= 0f || carveDebrisMaxCount <= 0 || radius <= 0f)
                return;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return;

            float removedVolume = SphereVolumeFactor * radius * radius * radius;
            int spawnCount = math.clamp((int)((removedVolume * carveDebrisPerCubicMeter) + 0.5f), 0, carveDebrisMaxCount);
            if (spawnCount <= 0)
                return;

            uint state = (uint)math.hash(new int4(
                CastBiasInt(request.AbsoluteHitPoint.x * 10d),
                CastBiasInt(request.AbsoluteHitPoint.y * 10d),
                CastBiasInt(request.AbsoluteHitPoint.z * 10d),
                math.max(1, (int)((radius * 100f) + 0.5f))));

            float spawnRadius = math.max(radius * 0.35f, MinRuntimeVoxelSize);
            for (int i = 0; i < spawnCount; i++)
            {
                float3 direction = NextBurstDirection(ref state);
                float distance01 = NextBurst01(ref state);
                float impulse01 = NextBurst01(ref state);
                double3 absoluteSpawnPosition = request.AbsoluteHitPoint + new double3(direction.x, direction.y, direction.z) * (spawnRadius * distance01);
                Vector3 runtimeSpawnPosition = HectonFloatingOrigin.ToRuntimePosition(absoluteSpawnPosition);
                Vector3 burstImpulse = new Vector3(direction.x, direction.y, direction.z) * math.lerp(carveDebrisImpulse * 0.55f, carveDebrisImpulse, impulse01);
                float3 currentImpulse3 = ResolveCinematicDebrisDriftImpulse(ref state, carveDebrisImpulse);
                Vector3 currentImpulse = new Vector3(currentImpulse3.x, currentImpulse3.y, currentImpulse3.z);
                Vector3 initialImpulse = burstImpulse + currentImpulse;
                registry.TryRegisterDroppedItem(carveDebrisItem, 1, runtimeSpawnPosition, initialImpulse);
            }
        }

        private static void EmitCaveInDustDecal(in PendingCarveRequest request, float radius)
        {
            if ((request.DeltaFlags & DeltaModeAdditive) != 0 || radius <= 0f)
                return;

            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null)
                return;

            Vector3 impulseDirection = ResolveDominantAxisDirection(request.AbsoluteImpulseDirection);

            fluidDecals.RegisterVoxelCaveInDustAup(
                ToVector3(request.AbsoluteHitPoint),
                impulseDirection,
                math.saturate(radius / math.max(MaxCarveRadiusMeters, MinCarveRadiusMeters)));
        }

        private static float NextBurst01(ref uint state)
        {
            return (NextBurstBits(ref state) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static uint NextBurstBits(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static float3 NextBurstDirection(ref uint state)
        {
            switch ((NextBurstBits(ref state) >> 28) & 15u)
            {
                case 0u: return new float3(1f, 0f, 0f);
                case 1u: return new float3(-1f, 0f, 0f);
                case 2u: return new float3(0f, 1f, 0f);
                case 3u: return new float3(0f, -1f, 0f);
                case 4u: return new float3(0f, 0f, 1f);
                case 5u: return new float3(0f, 0f, -1f);
                case 6u: return new float3(DirectionDiagonal2, DirectionDiagonal2, 0f);
                case 7u: return new float3(-DirectionDiagonal2, DirectionDiagonal2, 0f);
                case 8u: return new float3(DirectionDiagonal2, -DirectionDiagonal2, 0f);
                case 9u: return new float3(-DirectionDiagonal2, -DirectionDiagonal2, 0f);
                case 10u: return new float3(DirectionDiagonal2, 0f, DirectionDiagonal2);
                case 11u: return new float3(-DirectionDiagonal2, 0f, DirectionDiagonal2);
                case 12u: return new float3(DirectionDiagonal2, 0f, -DirectionDiagonal2);
                case 13u: return new float3(-DirectionDiagonal2, 0f, -DirectionDiagonal2);
                case 14u: return new float3(DirectionDiagonal3, DirectionDiagonal3, DirectionDiagonal3);
                default: return new float3(-DirectionDiagonal3, DirectionDiagonal3, -DirectionDiagonal3);
            }
        }

        private static float3 ResolveCinematicDebrisDriftImpulse(ref uint state, float impulseMagnitude)
        {
            float3 planarDirection = NextBurstPlanarDirection(ref state);
            float sinkStrength = math.lerp(0.08f, 0.18f, NextBurst01(ref state));
            float driftMagnitude = math.max(0.15f, impulseMagnitude * math.lerp(0.18f, 0.25f, NextBurst01(ref state)));
            return new float3(planarDirection.x, -sinkStrength, planarDirection.z) * driftMagnitude;
        }

        private static float3 NextBurstPlanarDirection(ref uint state)
        {
            switch ((NextBurstBits(ref state) >> 29) & 7u)
            {
                case 0u: return new float3(1f, 0f, 0f);
                case 1u: return new float3(-1f, 0f, 0f);
                case 2u: return new float3(0f, 0f, 1f);
                case 3u: return new float3(0f, 0f, -1f);
                case 4u: return new float3(DirectionDiagonal2, 0f, DirectionDiagonal2);
                case 5u: return new float3(-DirectionDiagonal2, 0f, DirectionDiagonal2);
                case 6u: return new float3(DirectionDiagonal2, 0f, -DirectionDiagonal2);
                default: return new float3(-DirectionDiagonal2, 0f, -DirectionDiagonal2);
            }
        }

        private void EnsureScheduledCarveWriteCapacity(int requiredCount)
        {
            if (_scheduledCarveWrites.IsCreated && _scheduledCarveWrites.Length >= requiredCount)
                return;

            if (_scheduledCarveWrites.IsCreated)
                DisposeTrackedNativeArray(ref _scheduledCarveWrites);

            // COLD ALLOC: NativeArray<CarveCellWrite>[requiredCount] - staged carve-write buffer for deferred voxel SDF mutation commits - owner: VoxelDeltaProcessor
            _scheduledCarveWrites = new NativeArray<CarveCellWrite>(requiredCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterTrackedNativeArray(_scheduledCarveWrites, nameof(_scheduledCarveWrites));
        }

        private void DisposeScheduledCarveBuffers()
        {
            JobHandle dependency = _scheduledCarveRunning ? _scheduledCarveHandle : default;
            if (_scheduledCarveWrites.IsCreated)
            {
                if (_scheduledCarveRunning)
                    DisposeTrackedNativeArray(ref _scheduledCarveWrites, dependency);
                else
                    DisposeTrackedNativeArray(ref _scheduledCarveWrites);
            }

            _scheduledCarveHandle = default;
            _scheduledCarveRunning = false;
            ResetScheduledCarveState();
        }

        private bool EmitLaserCarveDebris(in PendingCarveRequest request, float radius)
        {
            if (laserCarveDebrisProfile == null || !laserCarveDebrisProfile.IsValid || radius <= 0f)
                return false;

            IDebrisService debris = GlobalRegistry.Debris;
            if (debris == null || !debris.IsInitialized)
                return false;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(request.AbsoluteHitPoint);
            Vector3 impulseDirection = ResolveDominantAxisDirection(request.AbsoluteImpulseDirection);

            Vector3 outwardNormal = -impulseDirection;
            Vector3 runtimeOrigin = runtimeHitPoint + outwardNormal * math.max(radius * 0.2f, MinRuntimeVoxelSize);
            uint seed = (uint)math.hash(new int4(
                CastBiasInt(request.AbsoluteHitPoint.x * 8d),
                CastBiasInt(request.AbsoluteHitPoint.y * 8d),
                CastBiasInt(request.AbsoluteHitPoint.z * 8d),
                    math.max(1, (int)((radius * 64f) + 0.5f))));
            Quaternion rotation = Quaternion.Euler(
                (seed & 0xFFu) * (360f / 255f),
                ((seed >> 8) & 0xFFu) * (360f / 255f),
                ((seed >> 16) & 0xFFu) * (360f / 255f));
            float power01 = math.saturate(radius / math.max(MaxCarveRadiusMeters, MinCarveRadiusMeters));
            int requestedFragments = LaserDebrisMinFragments + (int)(seed % (uint)((LaserDebrisMaxFragments - LaserDebrisMinFragments) + 1));
            requestedFragments = math.min(requestedFragments, laserCarveDebrisProfile.ChunkCount);
            return requestedFragments >= LaserDebrisMinFragments &&
                   debris.SpawnBurst(
                       laserCarveDebrisProfile,
                       runtimeOrigin,
                       rotation,
                       runtimeHitPoint,
                       outwardNormal,
                        power01,
                        seed != 0u ? seed : 1u,
                        requestedFragments,
                        LaserDebrisLifetimeSeconds);
        }

        private static void ResetRecentCutHeatState()
        {
            s_recentCutHeatCursor = 0;
            s_recentCutHeatCount = 0;
            Shader.SetGlobalInt(_recentCutHeatCountId, 0);
        }

        private static void PushRecentCutHeat(in PendingCarveRequest request, float radius)
        {
            if (radius <= 0f)
                return;

            int slot = s_recentCutHeatCursor;
            s_recentCutHeatCursor = (slot + 1) % RecentCutHeatMax;
            s_recentCutHeatCount = math.min(s_recentCutHeatCount + 1, RecentCutHeatMax);
            float shaderRadius = math.max(radius * LaserCutHeatRadiusScale, MinRuntimeVoxelSize);
            s_recentCutHeatPositionRadius[slot] = new Vector4(
                (float)request.AbsoluteHitPoint.x,
                (float)request.AbsoluteHitPoint.y,
                (float)request.AbsoluteHitPoint.z,
                shaderRadius);
            s_recentCutHeatStrengthTime[slot] = new Vector4(LaserCutHeatStrength, Time.time, LaserCutHeatLifetimeSeconds, 0f);
            Shader.SetGlobalVector(_laserHitAupId, s_recentCutHeatPositionRadius[slot]);
            Shader.SetGlobalVector(_laserHitHeatId, s_recentCutHeatStrengthTime[slot]);
            Shader.SetGlobalVectorArray(_recentCutHeatPositionRadiusId, s_recentCutHeatPositionRadius);
            Shader.SetGlobalVectorArray(_recentCutHeatStrengthTimeId, s_recentCutHeatStrengthTime);
            Shader.SetGlobalInt(_recentCutHeatCountId, s_recentCutHeatCount);
        }

        private void DisposeScheduledCompactionBuffers()
        {
            JobHandle dependency = _scheduledCompactionRunning ? _scheduledCompactionHandle : default;
            ScheduledCompactionRequest request = _scheduledCompactionRequest;
            if (request.SourceEncodedSdf.IsCreated)
                DisposeTrackedNativeArray(ref request.SourceEncodedSdf, dependency);
            if (request.DirtyMaskWords.IsCreated)
                DisposeTrackedNativeArray(ref request.DirtyMaskWords, dependency);
            if (request.DeltaSdfValueBits.IsCreated)
                DisposeTrackedNativeArray(ref request.DeltaSdfValueBits, dependency);
            if (request.DeltaMaterialIds.IsCreated)
                DisposeTrackedNativeArray(ref request.DeltaMaterialIds, dependency);
            if (request.DeltaCellFlags.IsCreated)
                DisposeTrackedNativeArray(ref request.DeltaCellFlags, dependency);
            if (request.OutputSdfValueBits.IsCreated)
                DisposeTrackedNativeArray(ref request.OutputSdfValueBits, dependency);
            if (request.OutputMaterialIds.IsCreated)
                DisposeTrackedNativeArray(ref request.OutputMaterialIds, dependency);
            if (request.OutputCellFlags.IsCreated)
                DisposeTrackedNativeArray(ref request.OutputCellFlags, dependency);
            if (request.RleUniformFlag.IsCreated)
                DisposeTrackedNativeArray(ref request.RleUniformFlag, dependency);

            _scheduledCompactionRequest = default;
            _scheduledCompactionHandle = default;
            _scheduledCompactionRunning = false;
        }

        private void DisposeChunkStates()
        {
            Dictionary<ChunkAddress, ChunkDeltaState>.Enumerator enumerator = _chunkStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value.Dispose();

            _chunkStates.Clear();
            _chunkWriteVersions.Clear();
        }

        private void DisposeCompactedChunkStates()
        {
            Dictionary<ChunkAddress, CompactedChunkState>.Enumerator enumerator = _compactedChunkStates.GetEnumerator();
            while (enumerator.MoveNext())
                enumerator.Current.Value.Dispose();

            _compactedChunkStates.Clear();
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(default);
            array = default;
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private ulong ResolveScheduledCarveVolumeId()
        {
            HectonVoxelVolume volume = _scheduledCarveRequest.Volume;
            return volume != null ? EntityId.ToULong(volume.GetEntityId()) : 0ul;
        }

        private void WriteBlackBoxSample(ulong focusVolumeId, uint flags)
        {
            if (!_blackBox.IsCreated)
                return;

            PendingCarveRequest activeRequest = _scheduledCarveRequest;
            bool hasTouchedCells = _scheduledCarveTouchedAnyCell;
            int3 minCell = hasTouchedCells ? _scheduledCarveTouchedMinCell : default;
            int3 maxCell = hasTouchedCells ? _scheduledCarveTouchedMaxCell : default;
            byte scheduledState = 0;
            if (_scheduledCarveRunning)
                scheduledState |= 1;
            if (_scheduledCarveCommitPending)
                scheduledState |= 2;

            uint stateHash = 2166136261u;
            stateHash = HashBlackBox(stateHash, (uint)_queuedCarveEventCount);
            stateHash = HashBlackBox(stateHash, (uint)_pendingCarveCount);
            stateHash = HashBlackBox(stateHash, (uint)_scheduledCarveWriteCount);
            stateHash = HashBlackBox(stateHash, (uint)_chunkStates.Count);
            stateHash = HashBlackBox(stateHash, (uint)_compactedChunkStates.Count);
            stateHash = HashBlackBox(stateHash, (uint)_thermalMeltCount);
            stateHash = HashBlackBox(stateHash, (uint)VoxelChunkModifiedEvents.PendingCount);
            stateHash = HashBlackBox(stateHash, (uint)VoxelChunkModifiedEvents.DebugDroppedCount);
            stateHash = HashBlackBox(stateHash, (uint)VoxelChunkModifiedEvents.DebugRejectedCount);

            _blackBox[_blackBoxCursor] = new VoxelCarveTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                Flags = flags,
                FocusVolumeId = focusVolumeId,
                LastHitAup = new float3(
                    (float)activeRequest.AbsoluteHitPoint.x,
                    (float)activeRequest.AbsoluteHitPoint.y,
                    (float)activeRequest.AbsoluteHitPoint.z),
                TouchedMinX = minCell.x,
                TouchedMinY = minCell.y,
                TouchedMinZ = minCell.z,
                TouchedMaxX = maxCell.x,
                TouchedMaxY = maxCell.y,
                TouchedMaxZ = maxCell.z,
                QueuedCarves = (ushort)math.min(ushort.MaxValue, _queuedCarveEventCount),
                PendingCarves = (ushort)math.min(ushort.MaxValue, _pendingCarveCount),
                ScheduledWrites = (ushort)math.min(ushort.MaxValue, _scheduledCarveWriteCount),
                DirtyChunks = (ushort)math.min(ushort.MaxValue, _chunkStates.Count),
                ScheduledState = scheduledState,
                DrainBudget = (byte)math.min(byte.MaxValue, ResolveQueuedCarveDrainBudget()),
                StateHash16 = (ushort)(stateHash ^ (stateHash >> 16))
            };

            _blackBoxCursor++;
            if (_blackBoxCursor >= VoxelBlackBoxCapacity)
                _blackBoxCursor = 0;
        }

        private static uint HashBlackBox(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private static bool IsFiniteCarveEvent(in VoxelCarveEvent carveEvent)
        {
            return math.all(math.isfinite(carveEvent.AbsoluteHitPoint)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteSegmentEnd)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteHalfExtents)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteImpulseDirection)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteHitPointDouble)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteSegmentEndDouble)) &&
                   math.isfinite(carveEvent.RadiusMeters) &&
                   math.isfinite(carveEvent.BlendStrengthMeters);
        }

        private static bool IsFinitePendingCarve(in PendingCarveRequest request)
        {
            return IsFiniteDouble3(request.AbsoluteHitPoint) &&
                   IsFiniteDouble3(request.AbsoluteSegmentEnd) &&
                   IsFiniteVector3(request.AbsoluteHalfExtents) &&
                   IsFiniteVector3(request.AbsoluteImpulseDirection) &&
                   math.isfinite(request.AccumulatedDamage) &&
                   math.isfinite(request.ExplicitRadiusMeters) &&
                   math.isfinite(request.ExplicitBlendStrength);
        }

        private static bool IsFiniteDouble3(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (_blackBoxDumpedThisActivation)
                return;

            _blackBoxDumpedThisActivation = true;
            DumpBlackBox(reasonFlags);
        }

        private void DumpBlackBox(uint reasonFlags)
        {
            WriteBlackBoxSample(0ul, reasonFlags);
            if (!_blackBox.IsCreated)
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VoxelBlackBoxDumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(VoxelBlackBoxDumpMagic);
                    writer.Write((uint)VoxelBlackBoxCapacity);
                    writer.Write((uint)UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>());
                    writer.Write((uint)_blackBoxCursor);
                    writer.Write(reasonFlags);

                    for (int i = 0; i < VoxelBlackBoxCapacity; i++)
                    {
                        int index = (_blackBoxCursor + i) % VoxelBlackBoxCapacity;
                        WriteBlackBoxEntry(writer, _blackBox[index]);
                    }
                }
            }
            catch
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
        }

        private static void WriteBlackBoxEntry(BinaryWriter writer, VoxelCarveTelemetryEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.Flags);
            writer.Write(entry.FocusVolumeId);
            writer.Write(entry.LastHitAup.x);
            writer.Write(entry.LastHitAup.y);
            writer.Write(entry.LastHitAup.z);
            writer.Write(entry.TouchedMinX);
            writer.Write(entry.TouchedMinY);
            writer.Write(entry.TouchedMinZ);
            writer.Write(entry.TouchedMaxX);
            writer.Write(entry.TouchedMaxY);
            writer.Write(entry.TouchedMaxZ);
            writer.Write(entry.QueuedCarves);
            writer.Write(entry.PendingCarves);
            writer.Write(entry.ScheduledWrites);
            writer.Write(entry.DirtyChunks);
            writer.Write(entry.ScheduledState);
            writer.Write(entry.DrainBudget);
            writer.Write(entry.StateHash16);
        }
#endif

        private static void RemoveVolume(List<HectonVoxelVolume> list, HectonVoxelVolume volume)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i], volume))
                    continue;

                RemoveVolumeAtSwapBack(list, i);
                break;
            }
        }

        private static void RemoveVolumeAtSwapBack(List<HectonVoxelVolume> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }

        private static half ClampToHalf(float value)
        {
            return (half)math.clamp(value, -8f, 8f);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        private struct VoxelCarveTelemetryEntry
        {
            public uint Frame;
            public uint Flags;
            public ulong FocusVolumeId;
            public float3 LastHitAup;
            public int TouchedMinX;
            public int TouchedMinY;
            public int TouchedMinZ;
            public int TouchedMaxX;
            public int TouchedMaxY;
            public int TouchedMaxZ;
            public ushort QueuedCarves;
            public ushort PendingCarves;
            public ushort ScheduledWrites;
            public ushort DirtyChunks;
            public byte ScheduledState;
            public byte DrainBudget;
            public ushort StateHash16;
        }

        private static int3 FloorDiv(int3 value, int divisor)
        {
            return new int3(FloorDiv(value.x, divisor), FloorDiv(value.y, divisor), FloorDiv(value.z, divisor));
        }

        private static bool CellBoundsIntersect(int3 minA, int3 maxA, int3 minB, int3 maxB)
        {
            return minA.x <= maxB.x && maxA.x >= minB.x &&
                   minA.y <= maxB.y && maxA.y >= minB.y &&
                   minA.z <= maxB.z && maxA.z >= minB.z;
        }

        private static void ClampLocalizedLaserCarveBounds(
            ref int3 minCell,
            ref int3 maxCell,
            int3 volumeMinCell,
            int3 volumeMaxCell,
            double3 center,
            float voxelSize)
        {
            float safeVoxelSize = math.max(voxelSize, MinRuntimeVoxelSize);
            int3 centerCell = new int3(
                FastFloorToInt(center.x / safeVoxelSize),
                FastFloorToInt(center.y / safeVoxelSize),
                FastFloorToInt(center.z / safeVoxelSize));
            centerCell = math.clamp(centerCell, volumeMinCell, volumeMaxCell);

            int lowerHalf = MaxLaserCarveAxisCells / 2;
            int upperHalf = MaxLaserCarveAxisCells - lowerHalf - 1;
            int3 localizedMin = centerCell - new int3(lowerHalf);
            int3 localizedMax = centerCell + new int3(upperHalf);

            minCell = math.max(math.max(minCell, localizedMin), volumeMinCell);
            maxCell = math.min(math.min(maxCell, localizedMax), volumeMaxCell);
        }

        private static int FastFloorToInt(double value)
        {
            if (!math.isfinite(value))
                return 0;
            if (value >= int.MaxValue)
                return int.MaxValue;
            if (value <= int.MinValue)
                return int.MinValue;

            return (int)math.floor(value);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && ((remainder < 0) ^ (divisor < 0)))
                quotient--;

            return quotient;
        }

        private static ulong MortonEncodeSigned(int x, int y, int z)
        {
            ulong ux = (uint)(x + MortonSignedOffset);
            ulong uy = (uint)(y + MortonSignedOffset);
            ulong uz = (uint)(z + MortonSignedOffset);
            return ExpandBits(ux) | (ExpandBits(uy) << 1) | (ExpandBits(uz) << 2);
        }

        private static int3 MortonDecodeSigned(ulong morton)
        {
            int x = (int)CompactBits(morton) - MortonSignedOffset;
            int y = (int)CompactBits(morton >> 1) - MortonSignedOffset;
            int z = (int)CompactBits(morton >> 2) - MortonSignedOffset;
            return new int3(x, y, z);
        }

        private static ulong ExpandBits(ulong value)
        {
            value = (value | (value << 32)) & 0x001F00000000FFFFUL;
            value = (value | (value << 16)) & 0x001F0000FF0000FFUL;
            value = (value | (value << 8)) & 0x100F00F00F00F00FUL;
            value = (value | (value << 4)) & 0x10C30C30C30C30C3UL;
            value = (value | (value << 2)) & 0x1249249249249249UL;
            return value;
        }

        private static ulong CompactBits(ulong value)
        {
            value &= 0x1249249249249249UL;
            value = (value ^ (value >> 2)) & 0x10C30C30C30C30C3UL;
            value = (value ^ (value >> 4)) & 0x100F00F00F00F00FUL;
            value = (value ^ (value >> 8)) & 0x001F0000FF0000FFUL;
            value = (value ^ (value >> 16)) & 0x001F00000000FFFFUL;
            value = (value ^ (value >> 32)) & 0x1FFFFFUL;
            return value;
        }

        private struct PendingCompactionRequest
        {
            public HectonVoxelVolume Volume;
            public ChunkAddress Address;
            public int RequiredSonarVersion;
            public int WriteVersion;
            public int DirtyCount;
        }

        private struct ScheduledCompactionRequest
        {
            public HectonVoxelVolume Volume;
            public ChunkAddress Address;
            public int RequiredSonarVersion;
            public int WriteVersion;
            public NativeArray<byte> SourceEncodedSdf;
            public NativeArray<uint> DirtyMaskWords;
            public NativeArray<ushort> DeltaSdfValueBits;
            public NativeArray<byte> DeltaMaterialIds;
            public NativeArray<byte> DeltaCellFlags;
            public NativeArray<ushort> OutputSdfValueBits;
            public NativeArray<byte> OutputMaterialIds;
            public NativeArray<byte> OutputCellFlags;
            public NativeArray<byte> RleUniformFlag;
        }

        private struct ThermalMeltRuntime
        {
            public HectonVoxelVolume Volume;
            public double3 AbsoluteCenter;
            public float RadiusMeters;
            public float ElapsedSeconds;
            public float StepAccumulatorSeconds;
        }

        private struct PendingCarveRequest
        {
            public HectonVoxelVolume Volume;
            public double3 AbsoluteHitPoint;
            public double3 AbsoluteSegmentEnd;
            public Vector3 AbsoluteHalfExtents;
            public float AccumulatedDamage;
            public float ExplicitRadiusMeters;
            public float ExplicitBlendStrength;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte SourceFlags;
            public byte Shape;
            public Vector3 AbsoluteImpulseDirection;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CarveSdfJob : IJobParallelFor
        {
            public int3 MinCell;
            public int3 Span;
            public float VoxelSize;
            public float Radius;
            public float BlendRadius;
            public float BlendStrength;
            public double3 Center;
            public double3 SegmentEnd;
            public float3 HalfExtents;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte Shape;
            [NativeDisableParallelForRestriction] public NativeArray<CarveCellWrite> Writes;
            [NativeDisableUnsafePtrRestriction] public CarveCellWrite* WritesPtr;

            public void Execute(int index)
            {
                CarveCellWrite* write = WritesPtr + index;
                int spanXY = Span.x * Span.y;
                int localZ = index / spanXY;
                int remainder = index - (localZ * spanXY);
                int localY = remainder / Span.x;
                int localX = remainder - (localY * Span.x);
                int3 absoluteCell = MinCell + new int3(localX, localY, localZ);
                double3 cellCenter = (new double3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5d) * VoxelSize;
                double signedDistance = Shape == DeltaShapeBox
                    ? BoxSdf(cellCenter - Center, HalfExtents)
                    : Shape == DeltaShapeCapsule
                        ? CapsuleSdf(cellCenter, Center, SegmentEnd, Radius)
                        : SphereSdfApprox(cellCenter - Center, Radius);
                if (signedDistance >= BlendRadius)
                {
                    *write = default;
                    return;
                }

                float densityValue = (float)((DeltaFlags & DeltaModeAdditive) != 0
                    ? math.clamp(-signedDistance, -8d, 8d)
                    : math.clamp(signedDistance, -8d, 8d));

                *write = new CarveCellWrite
                {
                    AbsoluteCellX = absoluteCell.x,
                    AbsoluteCellY = absoluteCell.y,
                    AbsoluteCellZ = absoluteCell.z,
                    SdfValueBits = (ushort)math.f32tof16(densityValue),
                    MaterialId = MaterialId,
                    DeltaFlags = DeltaFlags,
                    BlendStrength = BlendStrength,
                    IsActive = 1
                };
            }

            private static double BoxSdf(double3 local, float3 halfExtents)
            {
                double3 q = math.abs(local) - new double3(
                    math.max(halfExtents.x, 0.001f),
                    math.max(halfExtents.y, 0.001f),
                    math.max(halfExtents.z, 0.001f));
                return AxisWeightedLengthApprox(math.max(q, 0d)) + math.min(math.cmax(q), 0d);
            }

            private static double CapsuleSdf(double3 point, double3 start, double3 end, float radius)
            {
                double3 segment = end - start;
                double segmentLengthSq = math.max(math.lengthsq(segment), 0.0001d);
                double t = math.saturate(math.dot(point - start, segment) / segmentLengthSq);
                return AxisWeightedLengthApprox(point - (start + segment * t)) - math.max(radius, 0.001f);
            }

            private static double SphereSdfApprox(double3 local, float radius)
            {
                return AxisWeightedLengthApprox(local) - math.max(radius, 0.001f);
            }

            private static double AxisWeightedLengthApprox(double3 value)
            {
                double3 axis = math.abs(value);
                return math.cmax(axis) + (axis.x + axis.y + axis.z) * 0.33f;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct CarveCellWrite
        {
            public int AbsoluteCellX;
            public int AbsoluteCellY;
            public int AbsoluteCellZ;
            public ushort SdfValueBits;
            public byte MaterialId;
            public byte DeltaFlags;
            public float BlendStrength;
            public byte IsActive;
            public byte Reserved;
            public ushort Reserved1;
            public uint Reserved2;
            public uint Reserved3;

            public int3 AbsoluteCell => new int3(AbsoluteCellX, AbsoluteCellY, AbsoluteCellZ);
        }

        private struct CompactedChunkState : IDisposable
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            public NativeArray<ushort> SdfValueBits;
            public NativeArray<byte> MaterialIds;
            public NativeArray<byte> CellFlags;
            public bool IsRleCompressed;
            public ushort RleSdfValueBits;
            public byte RleMaterialId;
            public byte RleCellFlags;

            public CompactedChunkState(
                int3 chunkCoord,
                float voxelSize,
                NativeArray<ushort> sdfValueBits,
                NativeArray<byte> materialIds,
                NativeArray<byte> cellFlags,
                NativeArray<byte> rleUniformFlag = default)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                SdfValueBits = sdfValueBits;
                MaterialIds = materialIds;
                CellFlags = cellFlags;
                IsRleCompressed = false;
                RleSdfValueBits = 0;
                RleMaterialId = DefaultMaterialId;
                RleCellFlags = DeltaModeReplace;

                TryPromoteUniformRun(in rleUniformFlag);
            }

            public CompactedChunkState(
                int3 chunkCoord,
                float voxelSize,
                ushort rleSdfValueBits,
                byte rleMaterialId,
                byte rleCellFlags)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                SdfValueBits = default;
                MaterialIds = default;
                CellFlags = default;
                IsRleCompressed = true;
                RleSdfValueBits = rleSdfValueBits;
                RleMaterialId = rleMaterialId;
                RleCellFlags = rleCellFlags;
            }

            public ushort GetSdfValueBits(int flatIndex)
            {
                return IsRleCompressed ? RleSdfValueBits : SdfValueBits[flatIndex];
            }

            public byte GetMaterialId(int flatIndex)
            {
                return IsRleCompressed ? RleMaterialId : MaterialIds[flatIndex];
            }

            public byte GetCellFlags(int flatIndex)
            {
                return IsRleCompressed ? RleCellFlags : CellFlags[flatIndex];
            }

            private bool TryPromoteUniformRun(in NativeArray<byte> rleUniformFlag)
            {
                if (!rleUniformFlag.IsCreated ||
                    rleUniformFlag.Length < 1 ||
                    rleUniformFlag[0] == 0 ||
                    !SdfValueBits.IsCreated ||
                    !MaterialIds.IsCreated ||
                    !CellFlags.IsCreated ||
                    SdfValueBits.Length <= 0 ||
                    MaterialIds.Length <= 0 ||
                    CellFlags.Length <= 0)
                {
                    return false;
                }

                RleSdfValueBits = SdfValueBits[0];
                RleMaterialId = MaterialIds[0];
                RleCellFlags = CellFlags[0];
                IsRleCompressed = true;
                DisposeTrackedNativeArray(ref SdfValueBits, default);
                DisposeTrackedNativeArray(ref MaterialIds, default);
                DisposeTrackedNativeArray(ref CellFlags, default);
                return true;
            }

            public void Dispose()
            {
                DisposeTrackedNativeArray(ref SdfValueBits, default);
                DisposeTrackedNativeArray(ref MaterialIds, default);
                DisposeTrackedNativeArray(ref CellFlags, default);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct VoxelDeltaCompactionJob : IJobParallelFor
        {
            public int3 ChunkCoord;
            public float VoxelSize;
            public int3 GridDimensions;
            public int GridStrideY;
            public int GridStrideZ;
            public float3 VolumeOrigin;
            public float3 InvCellSize;
            public float SdfDecodeScale;
            public float SdfDecodeBias;
            [ReadOnly] public NativeArray<byte> EncodedSdf;
            [ReadOnly] public NativeArray<uint> DirtyMaskWords;
            [ReadOnly] public NativeArray<ushort> DeltaSdfValueBits;
            [ReadOnly] public NativeArray<byte> DeltaMaterialIds;
            [ReadOnly] public NativeArray<byte> DeltaCellFlags;
            [WriteOnly] public NativeArray<ushort> OutputSdfValueBits;
            [WriteOnly] public NativeArray<byte> OutputMaterialIds;
            [WriteOnly] public NativeArray<byte> OutputCellFlags;
            [NativeDisableUnsafePtrRestriction] public byte* EncodedSdfPtr;
            [NativeDisableUnsafePtrRestriction] public uint* DirtyMaskWordsPtr;
            [NativeDisableUnsafePtrRestriction] public ushort* DeltaSdfValueBitsPtr;
            [NativeDisableUnsafePtrRestriction] public byte* DeltaMaterialIdsPtr;
            [NativeDisableUnsafePtrRestriction] public byte* DeltaCellFlagsPtr;
            [NativeDisableUnsafePtrRestriction] public ushort* OutputSdfValueBitsPtr;
            [NativeDisableUnsafePtrRestriction] public byte* OutputMaterialIdsPtr;
            [NativeDisableUnsafePtrRestriction] public byte* OutputCellFlagsPtr;

            public void Execute(int flatIndex)
            {
                int3 absoluteCell = AbsoluteCellFromFlatIndex(flatIndex);
                float3 absolutePosition = (new float3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5f) * VoxelSize;
                float sampledDensity = SampleEncodedSdf(absolutePosition);
                if (IsDirty(flatIndex))
                {
                    byte deltaFlags = *(DeltaCellFlagsPtr + flatIndex);
                    float deltaDensity = DecodeHalfToFloat(*(DeltaSdfValueBitsPtr + flatIndex));
                    float bakedDensity = BakeDeltaIntoBaseDensity(sampledDensity, deltaDensity, deltaFlags);
                    *(OutputSdfValueBitsPtr + flatIndex) = (ushort)math.f32tof16(math.clamp(bakedDensity, -8f, 8f));
                    *(OutputMaterialIdsPtr + flatIndex) = *(DeltaMaterialIdsPtr + flatIndex);
                    *(OutputCellFlagsPtr + flatIndex) = DeltaModeReplace;
                    return;
                }

                *(OutputSdfValueBitsPtr + flatIndex) = (ushort)math.f32tof16(math.clamp(sampledDensity, -8f, 8f));
                *(OutputMaterialIdsPtr + flatIndex) = DefaultMaterialId;
                *(OutputCellFlagsPtr + flatIndex) = DeltaModeReplace;
            }

            private static float DecodeHalfToFloat(ushort bits)
            {
                half value = UnsafeUtility.As<ushort, half>(ref bits);
                return (float)value;
            }

            private bool IsDirty(int flatIndex)
            {
                int wordIndex = flatIndex >> 5;
                uint bitMask = 1u << (flatIndex & 31);
                return (*(DirtyMaskWordsPtr + wordIndex) & bitMask) != 0u;
            }

            private int3 AbsoluteCellFromFlatIndex(int flatIndex)
            {
                int localX = flatIndex & (ChunkResolution - 1);
                int localY = (flatIndex >> 5) & (ChunkResolution - 1);
                int localZ = flatIndex >> 10;
                return (ChunkCoord * ChunkResolution) + new int3(localX, localY, localZ);
            }

            private float SampleEncodedSdf(float3 absolutePosition)
            {
                float sampleX = math.clamp((absolutePosition.x - VolumeOrigin.x) * InvCellSize.x, 0f, GridDimensions.x - 1.001f);
                float sampleY = math.clamp((absolutePosition.y - VolumeOrigin.y) * InvCellSize.y, 0f, GridDimensions.y - 1.001f);
                float sampleZ = math.clamp((absolutePosition.z - VolumeOrigin.z) * InvCellSize.z, 0f, GridDimensions.z - 1.001f);

                int x = (int)math.clamp(sampleX + 0.5f, 0f, GridDimensions.x - 1f);
                int y = (int)math.clamp(sampleY + 0.5f, 0f, GridDimensions.y - 1f);
                int z = (int)math.clamp(sampleZ + 0.5f, 0f, GridDimensions.z - 1f);
                return Decode(GridIndex(x, y, z));
            }

            private int GridIndex(int x, int y, int z)
            {
                return x + y * GridStrideY + z * GridStrideZ;
            }

            private float Decode(int index)
            {
                return (*(EncodedSdfPtr + index) * SdfDecodeScale) + SdfDecodeBias;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct VoxelDeltaUniformRunDetectJob : IJob
        {
            [ReadOnly] public NativeArray<ushort> SdfValueBits;
            [ReadOnly] public NativeArray<byte> MaterialIds;
            [ReadOnly] public NativeArray<byte> CellFlags;
            public NativeArray<byte> UniformFlag;

            public void Execute()
            {
                if (!SdfValueBits.IsCreated ||
                    !MaterialIds.IsCreated ||
                    !CellFlags.IsCreated ||
                    !UniformFlag.IsCreated ||
                    SdfValueBits.Length < ChunkCellCount ||
                    MaterialIds.Length < ChunkCellCount ||
                    CellFlags.Length < ChunkCellCount ||
                    UniformFlag.Length < 1)
                {
                    return;
                }

                ushort sdf = SdfValueBits[0];
                byte material = MaterialIds[0];
                byte flags = CellFlags[0];
                for (int i = 1; i < ChunkCellCount; i++)
                {
                    if (SdfValueBits[i] != sdf || MaterialIds[i] != material || CellFlags[i] != flags)
                    {
                        UniformFlag[0] = 0;
                        return;
                    }
                }

                UniformFlag[0] = 1;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelDeltaRleByteMaskEncodeJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Source;
            [WriteOnly] public NativeArray<byte> EncodedPairs;
            public NativeArray<int> EncodedLength;

            public void Execute()
            {
                if (!Source.IsCreated ||
                    !EncodedPairs.IsCreated ||
                    !EncodedLength.IsCreated ||
                    EncodedLength.Length < 1)
                {
                    return;
                }

                int sourceLength = Source.Length;
                if (sourceLength <= 0)
                {
                    EncodedLength[0] = 0;
                    return;
                }

                int write = 0;
                int read = 0;
                while (read < sourceLength)
                {
                    byte value = Source[read];
                    int runLength = 1;
                    while (read + runLength < sourceLength && Source[read + runLength] == value)
                        runLength++;

                    if (runLength == sourceLength)
                    {
                        if (EncodedPairs.Length < 2)
                        {
                            EncodedLength[0] = 0;
                            return;
                        }

                        EncodedPairs[0] = value;
                        EncodedPairs[1] = 0;
                        EncodedLength[0] = 2;
                        return;
                    }

                    int remaining = runLength;
                    while (remaining > 0)
                    {
                        int emittedCount = math.min(remaining, 255);
                        if (write > EncodedPairs.Length - 2)
                        {
                            EncodedLength[0] = 0;
                            return;
                        }

                        EncodedPairs[write++] = value;
                        EncodedPairs[write++] = (byte)emittedCount;
                        remaining -= emittedCount;
                    }

                    read += runLength;
                }

                EncodedLength[0] = write;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct VoxelDeltaRleByteMaskDecodeJob : IJob
        {
            [ReadOnly] public NativeArray<byte> EncodedPairs;
            [ReadOnly] public NativeArray<int> EncodedLength;
            public NativeArray<byte> Destination;
            public NativeArray<int> DecodedLength;

            public void Execute()
            {
                if (!EncodedPairs.IsCreated ||
                    !EncodedLength.IsCreated ||
                    !Destination.IsCreated ||
                    EncodedLength.Length < 1)
                {
                    return;
                }

                int encodedLength = EncodedLength[0];
                int read = 0;
                int write = 0;
                while (read < encodedLength)
                {
                    if (read > EncodedPairs.Length - 2)
                    {
                        write = 0;
                        break;
                    }

                    byte value = EncodedPairs[read++];
                    byte countByte = EncodedPairs[read++];
                    int count = countByte == 0 ? Destination.Length - write : countByte;
                    if (count < 0 || write > Destination.Length - count)
                    {
                        write = 0;
                        break;
                    }

                    for (int i = 0; i < count; i++)
                        Destination[write + i] = value;
                    write += count;
                }

                if (DecodedLength.IsCreated && DecodedLength.Length > 0)
                    DecodedLength[0] = write;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NativeSnapshotHeader
        {
            public int Version;
            public int ChunkCount;
            public int TotalDirtyCellCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct LegacyNativeSnapshotHeader
        {
            public int ChunkCount;
            public int TotalDirtyCellCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NativeSnapshotChunkHeader
        {
            public int ChunkX;
            public int ChunkY;
            public int ChunkZ;
            public float VoxelSize;
            public int DirtyCellCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]
        private struct NativeSnapshotChunkHeaderRle
        {
            public int ChunkX;
            public int ChunkY;
            public int ChunkZ;
            public float VoxelSize;
            public int DirtyCellCount;
            public byte StorageFlags;
            public byte Reserved0;
            public ushort Reserved1;
            public int PayloadByteLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
        private struct NativeSnapshotChunkHeaderDeltaRle
        {
            public int ChunkX;
            public int ChunkY;
            public int ChunkZ;
            public float VoxelSize;
            public int DirtyCellCount;
            public byte StorageFlags;
            public byte Reserved0;
            public ushort Reserved1;
            public int PayloadByteLength;
            public ulong PayloadHash64;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
        private readonly struct ChunkAddress : IEquatable<ChunkAddress>
        {
            private const int CoordBits = 19;
            private const int CoordMask = (1 << CoordBits) - 1;
            private const int CoordOffset = 1 << (CoordBits - 1);
            private const int YShift = CoordBits;
            private const int ZShift = CoordBits * 2;
            private const int VoxelShift = CoordBits * 3;
            private const int VoxelMask = 0x7F;
            private const float VoxelUnitMeters = 0.25f;
            private const float VoxelPackScale = 1f / VoxelUnitMeters;

            private readonly ulong _packedKey;

            public int3 ChunkCoord => new int3(
                DecodeCoord(_packedKey),
                DecodeCoord(_packedKey >> YShift),
                DecodeCoord(_packedKey >> ZShift));

            public float VoxelSize => ((float)DecodeVoxelUnits(_packedKey >> VoxelShift)) * VoxelUnitMeters;

            public ChunkAddress(int3 chunkCoord, float voxelSize)
            {
                _packedKey = PackCoord(chunkCoord.x)
                    | (PackCoord(chunkCoord.y) << YShift)
                    | (PackCoord(chunkCoord.z) << ZShift)
                    | ((ulong)(uint)(PackVoxelUnits(voxelSize) - 1) << VoxelShift);
            }

            public bool Equals(ChunkAddress other)
            {
                return _packedKey == other._packedKey;
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkAddress other && Equals(other);
            }

            public override int GetHashCode()
            {
                return unchecked((int)_packedKey ^ (int)(_packedKey >> 32));
            }

            private static ulong PackCoord(int value)
            {
                return ((ulong)(uint)(value + CoordOffset)) & CoordMask;
            }

            private static int DecodeCoord(ulong value)
            {
                return (int)(value & CoordMask) - CoordOffset;
            }

            private static int PackVoxelUnits(float voxelSize)
            {
                float scaled = math.max(voxelSize, MinRuntimeVoxelSize) * VoxelPackScale;
                int units = scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
                return math.clamp(units, 1, VoxelMask + 1);
            }

            private static int DecodeVoxelUnits(ulong value)
            {
                return (int)(value & VoxelMask) + 1;
            }
        }

        private struct ChunkDeltaState : IDisposable
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            public NativeArray<uint> DirtyMaskWords;
            public NativeArray<ushort> SdfValueBits;
            public NativeArray<byte> MaterialIds;
            public NativeArray<byte> CellFlags;
            public int DirtyCellCount;

            public ChunkDeltaState(int3 chunkCoord, float voxelSize)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                DirtyMaskWords = new NativeArray<uint>(ChunkDirtyMaskWordCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                SdfValueBits = new NativeArray<ushort>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                MaterialIds = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                CellFlags = new NativeArray<byte>(ChunkCellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                DirtyCellCount = 0;
                RegisterTrackedNativeArray(DirtyMaskWords, nameof(DirtyMaskWords));
                RegisterTrackedNativeArray(SdfValueBits, nameof(SdfValueBits));
                RegisterTrackedNativeArray(MaterialIds, nameof(MaterialIds));
                RegisterTrackedNativeArray(CellFlags, nameof(CellFlags));
            }

            public void Dispose()
            {
                DisposeTrackedNativeArray(ref DirtyMaskWords, default);
                DisposeTrackedNativeArray(ref SdfValueBits, default);
                DisposeTrackedNativeArray(ref MaterialIds, default);
                DisposeTrackedNativeArray(ref CellFlags, default);
            }
        }

    }
}
