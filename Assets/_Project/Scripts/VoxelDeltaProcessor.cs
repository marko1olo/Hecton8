using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
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
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct VoxelModifiedCell
    {
        [FieldOffset(0)] public half Density;
        [FieldOffset(2)] public byte MaterialId;
        [FieldOffset(3)] public byte Flags;
        [FieldOffset(4)] public ushort Reserved;
        [FieldOffset(6)] public ushort Reserved1;
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
        private const float SparseRleSdfByteScale = 127f / 8f;
        private const float SparseRleSdfByteInvScale = 8f / 127f;
        private const float LaserCutHeatLifetimeSeconds = 2f;
        private const float LaserCutHeatRadiusScale = 1.6f;
        private const float LaserCutHeatStrength = 1f;
        private const int RecentCutHeatMax = 16;
        private const double CarveCommitWarningMs = 0.2d;
        private const byte DefaultMaterialId = 0;
        private const byte ThermalMeltMaterialId = 2;
        private const byte TitaniumVoxelMaterialId = 4;
        private const byte ItemSourceVoxelCarve = 12;
        private const byte DeltaModeAdditive = 1 << 0;
        private const byte DeltaModeReplace = 1 << 1;
        private const byte CarveSourceLaser = 1 << 0;
        private const byte DeltaShapeSphere = 0;
        private const byte DeltaShapeBox = 1;
        private const byte DeltaShapeCapsule = 2;
        private const int NativeSnapshotMagic = unchecked((int)0x48584432);
        private const int NativeSnapshotRleMagic = unchecked((int)0x48584433);
        private const int NativeSnapshotDeltaRleMagic = unchecked((int)0x48584434);
        private const int NativeSnapshotDeltaRleAlignedMagic = unchecked((int)0x48584435);
        private const int NativeSnapshotVersionedHeaderBytes = 12;
        private const int NativeSnapshotLegacyChunkHeaderBytes = 20;
        private const int NativeSnapshotLegacyRleChunkHeaderBytes = 28;
        private const int NativeSnapshotLegacyDeltaRleChunkHeaderBytes = 36;
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
        private const string VoxelBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_05_VOXEL_CARVE.h8dump";
        private const string NativeMemoryOwner = nameof(VoxelDeltaProcessor);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const Allocator DataVaultExemptVoxelCarveSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptVoxelModifiedCellScratchAllocator = Allocator.Persistent;
        private const uint TitaniumOreHash = 0x61C51592u;
        private const uint TitaniumScrapItemHash = 0xD150482Eu;
        private static readonly uint _VoxelDebrisSignalHash = unchecked((uint)Hecton.Localization.LocHash.Compute("voxel.debris.carve"));
        private static readonly ProfilerMarker _carveScheduleProfilerMarker = new ProfilerMarker("H8.VoxelDelta.ScheduleCarve");
        private static readonly ProfilerMarker _carveCommitProfilerMarker = new ProfilerMarker("H8.VoxelDelta.CommitCarve");
        private static readonly uint _CarveCommitWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.CarveCommitBudgetExceeded"));
        private static readonly uint _CarveCommitTelemetryContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.TryCommitScheduledCarve"));
        private static readonly uint _SaveCorruptionHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SAVE_CORRUPTION_HASH"));
        private static readonly uint _SaveCorruptionContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.LoadSparseRle"));
        private static readonly uint _VoxelCarvedMassTelemetryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.TotalVoxelsCarved"));
        private static readonly uint _VoxelYieldContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.MaterialYield"));
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
        private static bool _carveSignalLaneConfigured;
        private HectonVoxelEngine _engine;
        private IDataVault _dataVault;
        private ISimulationBucketer _simulationBucketer;
        private ISaveService _saveService;
        private HectonQualityTier _cachedScalabilityTier;
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
        private VaultGenerationHandle<VoxelCarveTelemetryEntry> _blackBoxHandle;
        private int _blackBoxCursor;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _blackBoxDumpedThisActivation;
#endif
        private int3 _scheduledCarveTouchedMinCell;
        private int3 _scheduledCarveTouchedMaxCell;
        private bool _scheduledCarveTouchedAnyCell;
        private int _scheduledCarveDestroyedTitaniumCells;
        private int _scheduledCarveMassUnits;
        private int _totalVoxelsCarved;
        private VaultGenerationHandle<CarveCellWrite> _scheduledCarveWritesHandle;
        private int _scheduledCarveWritesCapacity;
        private bool _scheduledCarveWritesLocked;
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
            TryGetComponent(out _engine);
            _dataVault = GlobalRegistry.DataVault;
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
            _saveService = GlobalRegistry.Save;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
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

            if (_saveRegistered && _saveService != null)
            {
                _saveService.Unregister(this);
                _saveRegistered = false;
            }

            _saveService = null;
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
            EnsureCarveSignalLane();
            if (_queuedCarveEvents.IsCreated)
                return;

            _queuedCarveEvents = new NativeQueue<VoxelCarveEvent>(DataVaultExemptVoxelCarveSignalLaneAllocator); // COLD ALLOC: NativeQueue<VoxelCarveEvent>[64] - bounded async voxel carve ingress lane - owner: VoxelDeltaProcessor
            NativeMemorySentinel.RegisterNativeQueue(
                _queuedCarveEvents,
                InitialCarveEventQueueCapacity,
                NativeMemoryOwner,
                nameof(_queuedCarveEvents),
                NativeMemoryLifetime);
            PrewarmCarveEventQueue(ref _queuedCarveEvents, InitialCarveEventQueueCapacity);
            _queuedCarveEventCount = 0;
        }

        private static void EnsureCarveSignalLane()
        {
            if (_carveSignalLaneConfigured)
                return;

            GlobalSignals.InitializeAllQueues();
            _carveSignalLaneConfigured = true;
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

        private bool EnsureBlackBox()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            _blackBoxHandle = vault.GetGenerationHandle<VoxelCarveTelemetryEntry>(
                BufferID.ShinobuDeltaCrusherVoxelBlackBox,
                VoxelBlackBoxCapacity,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            _blackBoxCursor = 0;
            return TryResolveVaultBuffer(
                vault,
                in _blackBoxHandle,
                BufferID.ShinobuDeltaCrusherVoxelBlackBox,
                VoxelBlackBoxCapacity,
                out _);
        }

        private void DisposeBlackBox()
        {
            _blackBoxHandle = default;
            _blackBoxCursor = 0;
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private bool TryResolveBlackBox(out NativeArray<VoxelCarveTelemetryEntry> blackBox)
        {
            blackBox = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!IsExactVaultHandle(in _blackBoxHandle, BufferID.ShinobuDeltaCrusherVoxelBlackBox) && !EnsureBlackBox())
                return false;

            if (!TryResolveVaultBuffer(vault, in _blackBoxHandle, BufferID.ShinobuDeltaCrusherVoxelBlackBox, VoxelBlackBoxCapacity, out blackBox))
            {
                _blackBoxHandle = vault.GetGenerationHandle<VoxelCarveTelemetryEntry>(
                    BufferID.ShinobuDeltaCrusherVoxelBlackBox,
                    VoxelBlackBoxCapacity,
                    SystemID.TerrainSeams,
                    NativeArrayOptions.ClearMemory);
                if (!TryResolveVaultBuffer(vault, in _blackBoxHandle, BufferID.ShinobuDeltaCrusherVoxelBlackBox, VoxelBlackBoxCapacity, out blackBox))
                    return false;
            }

            return true;
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsExactVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) && handle.Generation != 0u;
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

        private int ResolveQueuedCarveDrainBudget()
        {
            return DebugResolveQueuedCarveDrainBudget(_cachedScalabilityTier);
        }

        private bool ShouldDeferQueuedCarveForFastBucket(in VoxelCarveEvent carveEvent)
        {
            ISimulationBucketer bucketer = _simulationBucketer;
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

            if (!TryResolveRuntimeAupDouble(runtimeHitPoint, out double3 absoluteHitPoint))
                return;

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

            if (!TryResolveRuntimeAupDouble(runtimeHitPoint, out double3 absoluteHitPoint))
                return;

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

            modifiedCells = new NativeParallelHashMap<int3, VoxelModifiedCell>(estimatedCount, DataVaultExemptVoxelModifiedCellScratchAllocator);

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
                    ? deltaChunkHeaderBytes + AlignSnapshotPayloadBytes4(NativeSnapshotUniformSdfRlePayloadBytes)
                    : deltaChunkHeaderBytes + AlignSnapshotPayloadBytes4(CountCompactedSparseRuns(in compactedState, in overlayState, hasOverlay) * runBytes);
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
                totalBytes += deltaChunkHeaderBytes + AlignSnapshotPayloadBytes4(runCount * runBytes);
            }

            countEnumerator.Dispose();
            if (chunkCount <= 0)
                return default;

            NativeArray<byte> snapshot = new NativeArray<byte>(totalBytes, allocator, NativeArrayOptions.UninitializedMemory);
            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            int cursor = 0;

            NativeSnapshotHeader header = new NativeSnapshotHeader
            {
                Version = NativeSnapshotDeltaRleAlignedMagic,
                ChunkCount = chunkCount,
                TotalDirtyCellCount = totalDirtyCellCount,
                Reserved0 = 0
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
            return compactedState.IsRleCompressed != 0 &&
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

            ulong payloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes);
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
                PayloadHashLow = (uint)payloadHash64,
                PayloadHashHigh = (uint)(payloadHash64 >> 32),
                Reserved2 = 0
            };
            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + headerCursor);
            PadNativeSnapshotCursor4(snapshotPtr, snapshotLength, ref cursor);
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

            ulong payloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes);
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
                PayloadHashLow = (uint)payloadHash64,
                PayloadHashHigh = (uint)(payloadHash64 >> 32),
                Reserved2 = 0
            };
            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + headerCursor);
            PadNativeSnapshotCursor4(snapshotPtr, snapshotLength, ref cursor);
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

            ulong payloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes);
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
                PayloadHashLow = (uint)payloadHash64,
                PayloadHashHigh = (uint)(payloadHash64 >> 32),
                Reserved2 = 0
            };
            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + headerCursor);
            PadNativeSnapshotCursor4(snapshotPtr, snapshotLength, ref cursor);
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
                Reserved0 = 0,
                Reserved1 = 0,
                PayloadByteLength = densePayloadBytes,
                Reserved2 = 0
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
            PadNativeSnapshotCursor4(snapshotPtr, snapshotLength, ref cursor);
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
            bool snapshotHasAlignedHeaders;
            NativeSnapshotHeader header;
            int snapshotVersion = ReadInt32(snapshotPtr, 0);

            if (snapshotVersion == NativeSnapshotMagic ||
                snapshotVersion == NativeSnapshotRleMagic ||
                snapshotVersion == NativeSnapshotDeltaRleMagic ||
                snapshotVersion == NativeSnapshotDeltaRleAlignedMagic)
            {
                snapshotHasAlignedHeaders = snapshotVersion == NativeSnapshotDeltaRleAlignedMagic;
                if (snapshotHasAlignedHeaders)
                {
                    if (snapshot.Length < UnsafeUtility.SizeOf<NativeSnapshotHeader>())
                    {
                        error = "Voxel delta aligned snapshot header is truncated.";
                        return false;
                    }

                    header = UnsafeUtility.ReadArrayElement<NativeSnapshotHeader>(snapshotPtr, 0);
                    minimumHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
                }
                else
                {
                    if (snapshot.Length < NativeSnapshotVersionedHeaderBytes)
                    {
                        error = "Voxel delta versioned snapshot header is truncated.";
                        return false;
                    }

                    header = new NativeSnapshotHeader
                    {
                        Version = snapshotVersion,
                        ChunkCount = ReadInt32(snapshotPtr, 4),
                        TotalDirtyCellCount = ReadInt32(snapshotPtr, 8),
                        Reserved0 = 0
                    };
                    minimumHeaderBytes = NativeSnapshotVersionedHeaderBytes;
                }

                snapshotHasFlags = true;
                snapshotHasRleChunks = snapshotVersion == NativeSnapshotRleMagic ||
                                       snapshotVersion == NativeSnapshotDeltaRleMagic ||
                                       snapshotVersion == NativeSnapshotDeltaRleAlignedMagic;
                snapshotHasDeltaRle = snapshotVersion == NativeSnapshotDeltaRleMagic ||
                                      snapshotVersion == NativeSnapshotDeltaRleAlignedMagic;
            }
            else
            {
                header = new NativeSnapshotHeader
                {
                    Version = 1,
                    ChunkCount = ReadInt32(snapshotPtr, 0),
                    TotalDirtyCellCount = ReadInt32(snapshotPtr, 4),
                    Reserved0 = 0
                };
                minimumHeaderBytes = legacyHeaderBytes;
                snapshotHasFlags = false;
                snapshotHasRleChunks = false;
                snapshotHasDeltaRle = false;
                snapshotHasAlignedHeaders = false;
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
            int chunkHeaderBytes = snapshotHasAlignedHeaders
                ? UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>()
                : snapshotHasDeltaRle
                ? NativeSnapshotLegacyDeltaRleChunkHeaderBytes
                : snapshotHasRleChunks
                ? NativeSnapshotLegacyRleChunkHeaderBytes
                : NativeSnapshotLegacyChunkHeaderBytes;
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
                    if (snapshotHasAlignedHeaders)
                    {
                        NativeSnapshotChunkHeaderDeltaRle deltaHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotChunkHeaderDeltaRle>(snapshotPtr + cursor, 0);
                        chunkHeader = new NativeSnapshotChunkHeader
                        {
                            ChunkX = deltaHeader.ChunkX,
                            ChunkY = deltaHeader.ChunkY,
                            ChunkZ = deltaHeader.ChunkZ,
                            VoxelSize = deltaHeader.VoxelSize,
                            DirtyCellCount = deltaHeader.DirtyCellCount,
                            Reserved0 = 0
                        };
                        storageFlags = deltaHeader.StorageFlags;
                        declaredPayloadBytes = deltaHeader.PayloadByteLength;
                        declaredPayloadHash64 = CombineHash64(deltaHeader.PayloadHashLow, deltaHeader.PayloadHashHigh);
                    }
                    else
                    {
                        ReadLegacyDeltaRleChunkHeader(
                            snapshotPtr + cursor,
                            out chunkHeader,
                            out storageFlags,
                            out declaredPayloadBytes,
                            out declaredPayloadHash64);
                    }
                }
                else if (snapshotHasRleChunks)
                {
                    ReadLegacyRleChunkHeader(snapshotPtr + cursor, out chunkHeader, out storageFlags, out declaredPayloadBytes);
                }
                else
                {
                    ReadLegacyChunkHeader(snapshotPtr + cursor, out chunkHeader);
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
                        if (snapshotHasAlignedHeaders)
                            cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
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
                        if (snapshotHasAlignedHeaders)
                            cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
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
                            if (snapshotHasAlignedHeaders)
                                cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
                            continue;
                        }

                        error = "Voxel delta RLE payload is invalid.";
                        return false;
                    }

                    ushort sdfBits = hasCurrentUniformPayload
                        ? DequantizeSdfByte((sbyte)(*(snapshotPtr + cursor)))
                        : UnsafeUtility.ReadArrayElement<ushort>(snapshotPtr + cursor, 0);
                    cursor += declaredPayloadBytes;
                    if (snapshotHasAlignedHeaders)
                        cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);

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
                            if (snapshotHasAlignedHeaders)
                                cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
                            continue;
                        }

                        error = "Voxel delta sparse RLE payload is invalid.";
                        return false;
                    }

                    cursor += declaredPayloadBytes;
                    if (snapshotHasAlignedHeaders)
                        cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
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
                        if (snapshotHasAlignedHeaders)
                            cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
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
                if (snapshotHasAlignedHeaders)
                    cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
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
            ISaveService saveService = _saveService;
            if (_saveRegistered || saveService == null)
                return;

            saveService.Register(this);
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
                if (!TryResolveScheduledCarveWriteBuffer(candidateCount, out NativeArray<CarveCellWrite> scheduledWrites))
                {
                    WriteBlackBoxSample(EntityId.ToULong(volume.GetEntityId()), VoxelBlackBoxInvalidPendingCarveFlag);
                    return;
                }

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
                    Writes = scheduledWrites,
                    WritesPtr = (CarveCellWrite*)NativeArrayUnsafeUtility.GetUnsafePtr(scheduledWrites)
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

                    UnlockScheduledCarveWrites();
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

                    if (!TryResolveScheduledCarveWrites(out NativeArray<CarveCellWrite> scheduledWrites))
                    {
                        WriteBlackBoxSample(ResolveScheduledCarveVolumeId(), VoxelBlackBoxInvalidPendingCarveFlag);
                        ResetScheduledCarveState();
                        return;
                    }

                    float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
                    int writeCount = math.min(_scheduledCarveWriteCount, scheduledWrites.Length);
                    int endIndex = math.min(_scheduledCarveCommitIndex + MaxScheduledCarveCommitWritesPerFrame, writeCount);
                    for (int i = _scheduledCarveCommitIndex; i < endIndex; i++)
                    {
                        CarveCellWrite write = scheduledWrites[i];
                        if (write.IsActive == 0)
                            continue;

                        int3 chunkCoord = FloorDiv(write.AbsoluteCell, ChunkResolution);
                        ChunkAddress address = new ChunkAddress(chunkCoord, voxelSize);
                        ChunkDeltaState state = GetOrCreateChunkState(chunkCoord, voxelSize);
                        if (!TryComputeLocalCellIndex(write.AbsoluteCell, state.ChunkCoord, out uint localIndex))
                            continue;

                        byte previousMaterialId = DefaultMaterialId;
                        if (state.MaterialIds.IsCreated && localIndex < (uint)state.MaterialIds.Length)
                            previousMaterialId = state.MaterialIds[(int)localIndex];

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
                        _scheduledCarveMassUnits++;
                        _totalVoxelsCarved++;
                        if ((_scheduledCarveRequest.DeltaFlags & DeltaModeAdditive) == 0 &&
                            previousMaterialId == TitaniumVoxelMaterialId)
                        {
                            _scheduledCarveDestroyedTitaniumCells++;
                        }

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
                    if ((_scheduledCarveRequest.SourceFlags & CarveSourceLaser) != 0 &&
                        (_scheduledCarveRequest.DeltaFlags & DeltaModeAdditive) == 0 &&
                        _scheduledCarveRequest.Shape != DeltaShapeBox)
                    {
                        PushRecentCutHeat(in _scheduledCarveRequest, resolvedCarveRadius);
                    }

                    PublishMaterialYieldIfNeeded();
                    PublishCarveMassTelemetryIfNeeded();
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
            _scheduledCarveDestroyedTitaniumCells = 0;
            _scheduledCarveMassUnits = 0;
        }

        private void ResetScheduledCarveState()
        {
            UnlockScheduledCarveWrites();
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
                    VolumeOrigin = new double3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z),
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
                DebrisKind = (request.SourceFlags & CarveSourceLaser) != 0
                    ? DebrisSpawnSignal.DebrisKindSparks
                    : DebrisSpawnSignal.DebrisKindRockShard,
                Flags = (byte)(request.SourceFlags | DebrisSpawnSignal.FlagComputeShard)
            };
            SignalBus<DebrisSpawnSignal>.Push(in signal);
        }

        private void PublishMaterialYieldIfNeeded()
        {
            if (_scheduledCarveDestroyedTitaniumCells <= 0)
                return;

            int quantity = math.clamp((_scheduledCarveDestroyedTitaniumCells + 63) >> 6, 1, ushort.MaxValue);
            ItemAcquiredSignal signal = new ItemAcquiredSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(_scheduledCarveRequest.AbsoluteHitPoint),
                ItemHash = TitaniumScrapItemHash,
                OreHash = TitaniumOreHash,
                Quantity = (ushort)quantity,
                SourceKind = ItemSourceVoxelCarve,
                Flags = _scheduledCarveRequest.SourceFlags,
                Frame = unchecked((uint)Time.frameCount)
            };
            SignalBus<ItemAcquiredSignal>.Push(in signal);
        }

        private void PublishCarveMassTelemetryIfNeeded()
        {
            if (_scheduledCarveMassUnits <= 0)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _VoxelCarvedMassTelemetryHash,
                _VoxelYieldContextHash,
                _totalVoxelsCarved);
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

        private static void EmitCaveInDustDecal(in PendingCarveRequest request, float radius)
        {
            if ((request.DeltaFlags & DeltaModeAdditive) != 0 || radius <= 0f)
                return;

            AbyssalFluidDecalManager fluidDecals = GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null)
                return;

            Vector3 impulseDirection = ResolveDominantAxisDirection(request.AbsoluteImpulseDirection);

            fluidDecals.RegisterVoxelCaveInDustAup(
                request.AbsoluteHitPoint,
                impulseDirection,
                math.saturate(radius / math.max(MaxCarveRadiusMeters, MinCarveRadiusMeters)));
        }

        private bool TryResolveScheduledCarveWriteBuffer(int requiredCount, out NativeArray<CarveCellWrite> writes)
        {
            writes = default;
            if (requiredCount <= 0)
                return false;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            if (!IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites) ||
                _scheduledCarveWritesCapacity < requiredCount)
            {
                _scheduledCarveWritesHandle = vault.GetGenerationHandle<CarveCellWrite>(
                    BufferID.ShinobuDeltaCrusherCarveWrites,
                    requiredCount,
                    SystemID.TerrainSeams,
                    NativeArrayOptions.ClearMemory);
                _scheduledCarveWritesCapacity = requiredCount;
            }

            if (!IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites) ||
                _scheduledCarveWritesCapacity < requiredCount ||
                !vault.TryLockBuffer(BufferID.ShinobuDeltaCrusherCarveWrites, SystemID.TerrainSeams))
            {
                return false;
            }

            _scheduledCarveWritesLocked = true;
            if (!TryResolveVaultBuffer(vault, in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites, requiredCount, out writes))
            {
                UnlockScheduledCarveWrites();
                _scheduledCarveWritesHandle = vault.GetGenerationHandle<CarveCellWrite>(
                    BufferID.ShinobuDeltaCrusherCarveWrites,
                    requiredCount,
                    SystemID.TerrainSeams,
                    NativeArrayOptions.ClearMemory);
                _scheduledCarveWritesCapacity = requiredCount;
                if (!IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites) ||
                    _scheduledCarveWritesCapacity < requiredCount ||
                    !vault.TryLockBuffer(BufferID.ShinobuDeltaCrusherCarveWrites, SystemID.TerrainSeams))
                {
                    writes = default;
                    return false;
                }

                _scheduledCarveWritesLocked = true;
                if (TryResolveVaultBuffer(vault, in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites, requiredCount, out writes))
                {
                    _scheduledCarveWritesCapacity = writes.Length;
                    return true;
                }

                UnlockScheduledCarveWrites();
                writes = default;
                return false;
            }

            _scheduledCarveWritesCapacity = writes.Length;
            return true;
        }

        private void DisposeScheduledCarveBuffers()
        {
            // [BLOCKING_SYNC_POINT] OnDisable teardown only: DataVault carve-write memory is persistent,
            // but the component must not leave a live writer lock behind during scene shutdown.
            if (_scheduledCarveRunning)
                DispatcherJobFence.TryComplete(ref _scheduledCarveHandle, forceComplete: true);

            _scheduledCarveRunning = false;
            UnlockScheduledCarveWrites();
            _scheduledCarveWritesHandle = default;
            _scheduledCarveWritesCapacity = 0;
            ResetScheduledCarveState();
        }

        private void UnlockScheduledCarveWrites()
        {
            if (!_scheduledCarveWritesLocked)
                return;

            IDataVault vault = ResolveDataVault();
            if (vault != null)
                vault.TryUnlockBuffer(BufferID.ShinobuDeltaCrusherCarveWrites, SystemID.TerrainSeams);

            _scheduledCarveWritesLocked = false;
        }

        private bool TryResolveScheduledCarveWrites(out NativeArray<CarveCellWrite> writes)
        {
            writes = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || !IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites))
                return false;

            if (!TryResolveVaultBuffer(
                    vault,
                    in _scheduledCarveWritesHandle,
                    BufferID.ShinobuDeltaCrusherCarveWrites,
                    math.max(1, _scheduledCarveWriteCount),
                    out writes) &&
                _scheduledCarveWriteCount > 0)
            {
                _scheduledCarveWritesHandle = vault.GetGenerationHandle<CarveCellWrite>(
                    BufferID.ShinobuDeltaCrusherCarveWrites,
                    _scheduledCarveWriteCount,
                    SystemID.TerrainSeams,
                    NativeArrayOptions.ClearMemory);
                _scheduledCarveWritesCapacity = _scheduledCarveWriteCount;
                if (!TryResolveVaultBuffer(vault, in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites, _scheduledCarveWriteCount, out writes))
                    return false;
            }

            _scheduledCarveWritesCapacity = math.max(_scheduledCarveWritesCapacity, writes.Length);
            return true;
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

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(request.AbsoluteHitPoint);
            float3 runtimeHitPoint3 = new float3(runtimeHitPoint.x, runtimeHitPoint.y, runtimeHitPoint.z);
            if (!math.all(math.isfinite(runtimeHitPoint3)))
                return;

            float shaderRadius = math.max(radius * LaserCutHeatRadiusScale, MinRuntimeVoxelSize);
            int slot = s_recentCutHeatCursor;
            s_recentCutHeatCursor = (slot + 1) % RecentCutHeatMax;
            s_recentCutHeatCount = math.min(s_recentCutHeatCount + 1, RecentCutHeatMax);
            s_recentCutHeatPositionRadius[slot] = new Vector4(
                runtimeHitPoint.x,
                runtimeHitPoint.y,
                runtimeHitPoint.z,
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
            if (!TryResolveBlackBox(out NativeArray<VoxelCarveTelemetryEntry> blackBox))
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

            double3 lastHitAup = IsFiniteDouble3(activeRequest.AbsoluteHitPoint)
                ? activeRequest.AbsoluteHitPoint
                : default;

            blackBox[_blackBoxCursor] = new VoxelCarveTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                Flags = flags,
                FocusVolumeId = focusVolumeId,
                LastHitAup = lastHitAup,
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

        private static bool TryResolveRuntimeAupDouble(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!IsFiniteVector3(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!resolvedAup.IsFinite())
                return false;

            absoluteAup = resolvedAup.ToAbsoluteDouble3();
            return IsFiniteDouble3(absoluteAup);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct VoxelBlackBoxDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Capacity;
            [FieldOffset(8)] public uint Stride;
            [FieldOffset(12)] public uint Cursor;
            [FieldOffset(16)] public uint ReasonFlags;
            [FieldOffset(20)] public uint _pad0;
            [FieldOffset(24)] public ulong _pad1;
        }

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
            if (!TryResolveBlackBox(out NativeArray<VoxelCarveTelemetryEntry> blackBox))
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VoxelBlackBoxDumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    unsafe
                    {
                        VoxelBlackBoxDumpHeader header = new VoxelBlackBoxDumpHeader
                        {
                            Magic = VoxelBlackBoxDumpMagic,
                            Capacity = (uint)VoxelBlackBoxCapacity,
                            Stride = (uint)UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>(),
                            Cursor = (uint)_blackBoxCursor,
                            ReasonFlags = reasonFlags,
                            _pad0 = 0u,
                            _pad1 = 0UL
                        };

                        WriteUnmanagedBytes(stream, &header, UnsafeUtility.SizeOf<VoxelBlackBoxDumpHeader>());
                        void* entries = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
                        WriteUnmanagedBytes(stream, entries, VoxelBlackBoxCapacity * UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>());
                    }
                }
            }
            catch
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
        }

        private static unsafe void WriteUnmanagedBytes(FileStream stream, void* source, int byteCount)
        {
            if (stream == null || source == null || byteCount <= 0)
                return;

            stream.Write(new ReadOnlySpan<byte>(source, byteCount));
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

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct VoxelCarveTelemetryEntry
        {
            [FieldOffset(0)] public double3 LastHitAup;
            [FieldOffset(24)] public ulong FocusVolumeId;
            [FieldOffset(32)] public uint Frame;
            [FieldOffset(36)] public uint Flags;
            [FieldOffset(40)] public int TouchedMinX;
            [FieldOffset(44)] public int TouchedMinY;
            [FieldOffset(48)] public int TouchedMinZ;
            [FieldOffset(52)] public int TouchedMaxX;
            [FieldOffset(56)] public int TouchedMaxY;
            [FieldOffset(60)] public int TouchedMaxZ;
            [FieldOffset(64)] public ushort QueuedCarves;
            [FieldOffset(66)] public ushort PendingCarves;
            [FieldOffset(68)] public ushort ScheduledWrites;
            [FieldOffset(70)] public ushort DirtyChunks;
            [FieldOffset(72)] public byte ScheduledState;
            [FieldOffset(73)] public byte DrainBudget;
            [FieldOffset(74)] public ushort StateHash16;
            [FieldOffset(76)] private uint _pad0;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CarveSdfJob : IJobParallelFor
        {
            public double3 Center;
            public double3 SegmentEnd;
            public int3 MinCell;
            public int3 Span;
            public float VoxelSize;
            public float Radius;
            public float BlendRadius;
            public float BlendStrength;
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

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct CarveCellWrite
        {
            [FieldOffset(0)] public int AbsoluteCellX;
            [FieldOffset(4)] public int AbsoluteCellY;
            [FieldOffset(8)] public int AbsoluteCellZ;
            [FieldOffset(12)] public float BlendStrength;
            [FieldOffset(16)] public ushort SdfValueBits;
            [FieldOffset(18)] public byte MaterialId;
            [FieldOffset(19)] public byte DeltaFlags;
            [FieldOffset(20)] public byte IsActive;
            [FieldOffset(21)] private byte _pad0;
            [FieldOffset(22)] private ushort _pad1;
            [FieldOffset(24)] private uint _pad2;
            [FieldOffset(28)] private uint _pad3;

            public int3 AbsoluteCell => new int3(AbsoluteCellX, AbsoluteCellY, AbsoluteCellZ);
        }

        private struct CompactedChunkState : IDisposable
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            public NativeArray<ushort> SdfValueBits;
            public NativeArray<byte> MaterialIds;
            public NativeArray<byte> CellFlags;
            public byte IsRleCompressed;
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
                IsRleCompressed = 0;
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
                IsRleCompressed = 1;
                RleSdfValueBits = rleSdfValueBits;
                RleMaterialId = rleMaterialId;
                RleCellFlags = rleCellFlags;
            }

            public ushort GetSdfValueBits(int flatIndex)
            {
                return IsRleCompressed != 0 ? RleSdfValueBits : SdfValueBits[flatIndex];
            }

            public byte GetMaterialId(int flatIndex)
            {
                return IsRleCompressed != 0 ? RleMaterialId : MaterialIds[flatIndex];
            }

            public byte GetCellFlags(int flatIndex)
            {
                return IsRleCompressed != 0 ? RleCellFlags : CellFlags[flatIndex];
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
                IsRleCompressed = 1;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct VoxelDeltaCompactionJob : IJobParallelFor
        {
            public double3 VolumeOrigin;
            public int3 ChunkCoord;
            public float VoxelSize;
            public int3 GridDimensions;
            public int GridStrideY;
            public int GridStrideZ;
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
                double3 absolutePosition = (new double3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5d) * VoxelSize;
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

            private float SampleEncodedSdf(double3 absolutePosition)
            {
                double localX = (absolutePosition.x - VolumeOrigin.x) * InvCellSize.x;
                double localY = (absolutePosition.y - VolumeOrigin.y) * InvCellSize.y;
                double localZ = (absolutePosition.z - VolumeOrigin.z) * InvCellSize.z;
                float sampleX = math.clamp(math.isfinite(localX) ? (float)localX : 0f, 0f, GridDimensions.x - 1.001f);
                float sampleY = math.clamp(math.isfinite(localY) ? (float)localY : 0f, 0f, GridDimensions.y - 1.001f);
                float sampleZ = math.clamp(math.isfinite(localZ) ? (float)localZ : 0f, 0f, GridDimensions.z - 1.001f);

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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        private static int AlignSnapshotPayloadBytes4(int payloadBytes)
        {
            return (math.max(0, payloadBytes) + 3) & ~3;
        }

        private static int AlignSnapshotCursor4Clamped(int cursor, int snapshotLength)
        {
            int aligned = (math.max(0, cursor) + 3) & ~3;
            return math.min(aligned, math.max(0, snapshotLength));
        }

        private static unsafe void PadNativeSnapshotCursor4(byte* snapshotPtr, int snapshotLength, ref int cursor)
        {
            int aligned = AlignSnapshotCursor4Clamped(cursor, snapshotLength);
            while (cursor < aligned)
            {
                *(snapshotPtr + cursor) = 0;
                cursor++;
            }
        }

        private static unsafe void ReadLegacyChunkHeader(byte* headerPtr, out NativeSnapshotChunkHeader header)
        {
            header = new NativeSnapshotChunkHeader
            {
                ChunkX = ReadInt32(headerPtr, 0),
                ChunkY = ReadInt32(headerPtr, 4),
                ChunkZ = ReadInt32(headerPtr, 8),
                VoxelSize = ReadSingle(headerPtr, 12),
                DirtyCellCount = ReadInt32(headerPtr, 16),
                Reserved0 = 0
            };
        }

        private static unsafe void ReadLegacyRleChunkHeader(
            byte* headerPtr,
            out NativeSnapshotChunkHeader header,
            out byte storageFlags,
            out int payloadByteLength)
        {
            ReadLegacyChunkHeader(headerPtr, out header);
            storageFlags = *(headerPtr + 20);
            payloadByteLength = ReadInt32(headerPtr, 24);
        }

        private static unsafe void ReadLegacyDeltaRleChunkHeader(
            byte* headerPtr,
            out NativeSnapshotChunkHeader header,
            out byte storageFlags,
            out int payloadByteLength,
            out ulong payloadHash64)
        {
            ReadLegacyRleChunkHeader(headerPtr, out header, out storageFlags, out payloadByteLength);
            payloadHash64 = CombineHash64(ReadUInt32(headerPtr, 28), ReadUInt32(headerPtr, 32));
        }

        private static unsafe int ReadInt32(byte* ptr, int byteOffset)
        {
            return UnsafeUtility.ReadArrayElement<int>(ptr + byteOffset, 0);
        }

        private static unsafe uint ReadUInt32(byte* ptr, int byteOffset)
        {
            return UnsafeUtility.ReadArrayElement<uint>(ptr + byteOffset, 0);
        }

        private static unsafe float ReadSingle(byte* ptr, int byteOffset)
        {
            return math.asfloat(ReadInt32(ptr, byteOffset));
        }

        private static ulong CombineHash64(uint low, uint high)
        {
            return ((ulong)high << 32) | low;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct NativeSnapshotHeader
        {
            [FieldOffset(0)] public int Version;
            [FieldOffset(4)] public int ChunkCount;
            [FieldOffset(8)] public int TotalDirtyCellCount;
            [FieldOffset(12)] public int Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct LegacyNativeSnapshotHeader
        {
            [FieldOffset(0)] public int ChunkCount;
            [FieldOffset(4)] public int TotalDirtyCellCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct NativeSnapshotChunkHeader
        {
            [FieldOffset(0)] public int ChunkX;
            [FieldOffset(4)] public int ChunkY;
            [FieldOffset(8)] public int ChunkZ;
            [FieldOffset(12)] public float VoxelSize;
            [FieldOffset(16)] public int DirtyCellCount;
            [FieldOffset(20)] public int Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct NativeSnapshotChunkHeaderRle
        {
            [FieldOffset(0)] public int ChunkX;
            [FieldOffset(4)] public int ChunkY;
            [FieldOffset(8)] public int ChunkZ;
            [FieldOffset(12)] public float VoxelSize;
            [FieldOffset(16)] public int DirtyCellCount;
            [FieldOffset(20)] public byte StorageFlags;
            [FieldOffset(21)] public byte Reserved0;
            [FieldOffset(22)] public ushort Reserved1;
            [FieldOffset(24)] public int PayloadByteLength;
            [FieldOffset(28)] public int Reserved2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct NativeSnapshotChunkHeaderDeltaRle
        {
            [FieldOffset(0)] public int ChunkX;
            [FieldOffset(4)] public int ChunkY;
            [FieldOffset(8)] public int ChunkZ;
            [FieldOffset(12)] public float VoxelSize;
            [FieldOffset(16)] public int DirtyCellCount;
            [FieldOffset(20)] public byte StorageFlags;
            [FieldOffset(21)] public byte Reserved0;
            [FieldOffset(22)] public ushort Reserved1;
            [FieldOffset(24)] public int PayloadByteLength;
            [FieldOffset(28)] public uint PayloadHashLow;
            [FieldOffset(32)] public uint PayloadHashHigh;
            [FieldOffset(36)] public uint Reserved2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
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

            [FieldOffset(0)] private readonly ulong _packedKey;

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
