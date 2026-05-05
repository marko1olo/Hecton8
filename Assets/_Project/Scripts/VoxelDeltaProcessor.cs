using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Physics;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct VoxelModifiedCell
    {
        public half Density;
        public byte MaterialId;
        public byte Flags;
        public ushort Reserved;
    }

    /// <summary>
    /// Authoritative absolute-universe thermal melt request produced by lava/vent gameplay.
    /// </summary>
    public struct ThermalMeltEvent
    {
        public Vector3 AbsoluteUniversePosition;
        public float RadiusMeters;
        public float Heat01;
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
        private const int InitialPendingCompactionCapacity = 16;
        private const int MaxActiveThermalMeltEvents = 16;
        private const int MaxScheduledCarveCommitWritesPerFrame = 64;
        private const int ChunkCompactionDirtyThreshold = (ChunkCellCount * 4) / 5;
        private const int MortonSignedOffset = 1 << 20;
        private const float MinRuntimeVoxelSize = 0.25f;
        private const float MinCarveRadiusMeters = 0.9f;
        private const float MaxCarveRadiusMeters = 4f;
        private const float ThermalMeltDurationSeconds = 5f;
        private const float ThermalMeltStepIntervalSeconds = 0.25f;
        private const float ThermalMeltMinimumHeat = 0.01f;
        private const float SphereVolumeFactor = 4f / 3f * math.PI;
        private const int LaserDebrisMinFragments = 3;
        private const int LaserDebrisMaxFragments = 5;
        private const float LaserDebrisLifetimeSeconds = 4f;
        private const int RecentCutHeatMax = 16;
        private const float LaserCutHeatLifetimeSeconds = 1.35f;
        private const float LaserCutHeatRadiusScale = 1.6f;
        private const float LaserCutHeatStrength = 1f;
        private const byte DefaultMaterialId = 0;
        private const byte ThermalMeltMaterialId = 2;
        private const byte DeltaModeAdditive = 1 << 0;
        private const byte DeltaModeReplace = 1 << 1;
        private const byte CarveSourceLaser = 1 << 0;
        private const byte DeltaShapeSphere = 0;
        private const byte DeltaShapeBox = 1;
        private const byte DeltaShapeCapsule = 2;
        private const int NativeSnapshotMagic = unchecked((int)0x48584432);
        private const string NativeMemoryOwner = nameof(VoxelDeltaProcessor);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly ProfilerMarker _carveScheduleProfilerMarker = new ProfilerMarker("H8.VoxelDelta.ScheduleCarve");
        private static readonly ProfilerMarker _carveCommitProfilerMarker = new ProfilerMarker("H8.VoxelDelta.CommitCarve");
        private static readonly int _recentCutHeatCountId = Shader.PropertyToID("_HectonRecentCutHeatCount");
        private static readonly int _recentCutHeatPositionRadiusId = Shader.PropertyToID("_HectonRecentCutHeatPositionRadius");
        private static readonly int _recentCutHeatStrengthTimeId = Shader.PropertyToID("_HectonRecentCutHeatStrengthTime");
        // COLD ALLOC: Vector4[16] - global laser cut heat stamp positions - owner: VoxelDeltaProcessor
        private static readonly Vector4[] s_recentCutHeatPositionRadius = new Vector4[RecentCutHeatMax];
        // COLD ALLOC: Vector4[16] - global laser cut heat stamp strengths/lifetimes - owner: VoxelDeltaProcessor
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
        private bool _saveRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;

        // COLD ALLOC: Dictionary<ChunkAddress, ChunkDeltaState>[InitialChunkRegistryCapacity] â€” persistent voxel delta chunk registry â€” owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, ChunkDeltaState> _chunkStates = new Dictionary<ChunkAddress, ChunkDeltaState>(InitialChunkRegistryCapacity);
        // COLD ALLOC: Dictionary<ChunkAddress, CompactedChunkState>[InitialChunkRegistryCapacity] - compacted replacement SDF chunk registry - owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, CompactedChunkState> _compactedChunkStates = new Dictionary<ChunkAddress, CompactedChunkState>(InitialChunkRegistryCapacity);
        // COLD ALLOC: Dictionary<ChunkAddress, int>[InitialChunkRegistryCapacity] - dirty chunk write version registry for compaction conflict checks - owner: VoxelDeltaProcessor
        private readonly Dictionary<ChunkAddress, int> _chunkWriteVersions = new Dictionary<ChunkAddress, int>(InitialChunkRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] â€” live voxel volume registry for load-time rebuild dispatch â€” owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _registeredVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: List<HectonVoxelVolume>[InitialVolumeRegistryCapacity] â€” pending volume rebuild queue after loaded delta application â€” owner: VoxelDeltaProcessor
        private readonly List<HectonVoxelVolume> _pendingRebuildVolumes = new List<HectonVoxelVolume>(InitialVolumeRegistryCapacity);
        // COLD ALLOC: PendingCarveRequest[InitialPendingCarveCapacity] â€” deferred plasma-cut carve staging buffer â€” owner: VoxelDeltaProcessor
        private readonly PendingCarveRequest[] _pendingCarves = new PendingCarveRequest[InitialPendingCarveCapacity];
        // COLD ALLOC: ThermalMeltRuntime[16] - bounded lava crater-expansion requests - owner: VoxelDeltaProcessor
        private readonly ThermalMeltRuntime[] _thermalMeltEvents = new ThermalMeltRuntime[MaxActiveThermalMeltEvents];
        private int _pendingCarveCount;
        private int _thermalMeltCount;
        private JobHandle _scheduledCarveHandle;
        private bool _scheduledCarveRunning;
        private PendingCarveRequest _scheduledCarveRequest;
        private int _scheduledCarveWriteCount;
        private bool _scheduledCarveCommitPending;
        private int _scheduledCarveCommitIndex;
        private int3 _scheduledCarveTouchedMinCell;
        private int3 _scheduledCarveTouchedMaxCell;
        private bool _scheduledCarveTouchedAnyCell;
        // COLD ALLOC: NativeArray<CarveCellWrite>[capacity] â€” staged Burst carve results before managed delta-chunk commit â€” owner: VoxelDeltaProcessor
        private NativeArray<CarveCellWrite> _scheduledCarveWrites;
        // COLD ALLOC: PendingCompactionRequest[16] - bounded background dirty-chunk compaction queue - owner: VoxelDeltaProcessor
        private readonly PendingCompactionRequest[] _pendingCompactions = new PendingCompactionRequest[InitialPendingCompactionCapacity];
        private int _pendingCompactionCount;
        private JobHandle _scheduledCompactionHandle;
        private bool _scheduledCompactionRunning;
        private ScheduledCompactionRequest _scheduledCompactionRequest;

        public int SavePriority => 40;

        public int LoadPriority => 30;

        private bool IsScheduledCarveBusy => _scheduledCarveRunning || _scheduledCarveCommitPending;

        private void OnEnable()
        {
            _engine = GetComponent<HectonVoxelEngine>();

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
            DisposeScheduledCarveBuffers();
            DisposeScheduledCompactionBuffers();
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
            _pendingCompactionCount = 0;
            _thermalMeltCount = 0;
            _pendingRebuildVolumes.Clear();
            _registeredVolumes.Clear();
            DisposeChunkStates();
            DisposeCompactedChunkStates();
        }

        /// <summary>
        /// Flushes staged carve requests and deferred load-time rebuild requests on the registry dispatcher lane.
        /// </summary>
        /// <param name="deltaTime">Unused dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            TryRegisterSaveService();
            AdvanceThermalMeltEvents(deltaTime);
            TrySchedulePendingCarve();
            TrySchedulePendingCompaction();
            FlushPendingRebuilds();
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

            HectonVoxelVolume targetVolume = ResolveThermalMeltVolume(meltEvent.AbsoluteUniversePosition, radius);
            if (targetVolume == null)
                return false;

            for (int i = 0; i < _thermalMeltCount; i++)
            {
                ThermalMeltRuntime existing = _thermalMeltEvents[i];
                if (!ReferenceEquals(existing.Volume, targetVolume))
                    continue;

                float mergeRadius = math.max(radius, existing.RadiusMeters);
                if ((existing.AbsoluteCenter - meltEvent.AbsoluteUniversePosition).sqrMagnitude > mergeRadius * mergeRadius)
                    continue;

                existing.AbsoluteCenter = Vector3.Lerp(existing.AbsoluteCenter, meltEvent.AbsoluteUniversePosition, 0.5f);
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
                AbsoluteCenter = meltEvent.AbsoluteUniversePosition,
                RadiusMeters = radius,
                ElapsedSeconds = 0f,
                StepAccumulatorSeconds = ThermalMeltStepIntervalSeconds
            };
            return true;
        }

        private HectonVoxelVolume ResolveThermalMeltVolume(Vector3 absoluteCenter, float radius)
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
                float distanceSq = (volume.GenerationAbsoluteUniversePosition - absoluteCenter).sqrMagnitude;
                if (distanceSq > acceptedRadius * acceptedRadius || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
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

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeHitPoint);
            float mergeDistance = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            float mergeDistanceSq = mergeDistance * mergeDistance;

            for (int i = 0; i < _pendingCarveCount; i++)
            {
                PendingCarveRequest existing = _pendingCarves[i];
                if (!ReferenceEquals(existing.Volume, volume))
                    continue;

                if ((existing.AbsoluteHitPoint - absoluteHitPoint).sqrMagnitude > mergeDistanceSq)
                    continue;

                existing.AbsoluteHitPoint = Vector3.Lerp(existing.AbsoluteHitPoint, absoluteHitPoint, 0.5f);
                existing.AccumulatedDamage += damage;
                existing.MaterialId = materialId;
                existing.SourceFlags |= CarveSourceLaser;
                _pendingCarves[i] = existing;
                return;
            }

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!IsScheduledCarveBusy)
                    TrySchedulePendingCarve();

                for (int i = 1; i < _pendingCarveCount; i++)
                    _pendingCarves[i - 1] = _pendingCarves[i];

                _pendingCarveCount = _pendingCarves.Length - 1;
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                AccumulatedDamage = damage,
                MaterialId = materialId,
                SourceFlags = CarveSourceLaser
            };
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

            Vector3 absoluteHitPoint = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimeHitPoint);
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
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!IsScheduledCarveBusy)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                {
                    for (int i = 1; i < _pendingCarveCount; i++)
                        _pendingCarves[i - 1] = _pendingCarves[i];

                    _pendingCarveCount = _pendingCarves.Length - 1;
                }
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                ExplicitRadiusMeters = radius,
                MaterialId = materialId,
                DeltaFlags = 0,
                SourceFlags = sourceFlags,
                AbsoluteImpulseDirection = absoluteImpulseDirection
            };
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

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!IsScheduledCarveBusy)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                {
                    for (int i = 1; i < _pendingCarveCount; i++)
                        _pendingCarves[i - 1] = _pendingCarves[i];

                    _pendingCarveCount = _pendingCarves.Length - 1;
                }
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteCenter,
                AbsoluteHalfExtents = resolvedHalfExtents,
                ExplicitBlendStrength = math.max(volume.VoxelSize, math.cmin(resolvedHalfExtents3) * 0.35f),
                MaterialId = materialId,
                DeltaFlags = 0,
                Shape = DeltaShapeBox
            };
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
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!IsScheduledCarveBusy)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                {
                    for (int i = 1; i < _pendingCarveCount; i++)
                        _pendingCarves[i - 1] = _pendingCarves[i];

                    _pendingCarveCount = _pendingCarves.Length - 1;
                }
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteHitPoint,
                ExplicitRadiusMeters = radius,
                ExplicitBlendStrength = math.max(volume.VoxelSize, strength),
                MaterialId = materialId,
                DeltaFlags = DeltaModeAdditive
            };
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
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            if ((absoluteEnd - absoluteStart).sqrMagnitude <= 0.0001f)
            {
                ApplyImmediateAbsoluteWeld(volume, absoluteStart, radius, strength, materialId);
                return;
            }

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!IsScheduledCarveBusy)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                {
                    for (int i = 1; i < _pendingCarveCount; i++)
                        _pendingCarves[i - 1] = _pendingCarves[i];

                    _pendingCarveCount = _pendingCarves.Length - 1;
                }
            }

            _pendingCarves[_pendingCarveCount++] = new PendingCarveRequest
            {
                Volume = volume,
                AbsoluteHitPoint = absoluteStart,
                AbsoluteSegmentEnd = absoluteEnd,
                ExplicitRadiusMeters = radius,
                ExplicitBlendStrength = math.max(volume.VoxelSize, strength),
                MaterialId = materialId,
                DeltaFlags = DeltaModeAdditive,
                Shape = DeltaShapeCapsule
            };
        }

        private bool TryEnqueuePendingCarve(in PendingCarveRequest request)
        {
            if (request.Volume == null || !request.Volume.HasRuntimeData)
                return false;

            if (_pendingCarveCount >= _pendingCarves.Length)
            {
                if (!IsScheduledCarveBusy)
                    TrySchedulePendingCarve();

                if (_pendingCarveCount >= _pendingCarves.Length)
                    return false;
            }

            _pendingCarves[_pendingCarveCount++] = request;
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
                                    Density = BitsToHalf(compactedState.SdfValueBits[flatIndex]),
                                    MaterialId = compactedState.MaterialIds[flatIndex],
                                    Flags = DeltaModeReplace
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
            chunkDto.EnsureCapacity(ChunkCellCount);
            chunkDto.cellCount = ChunkCellCount;

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
            float density = (float)BitsToHalf(compactedState.SdfValueBits[flatIndex]);
            materialId = compactedState.MaterialIds[flatIndex];
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
                bool hasDenseStorage = HasDenseStorage(in chunk);
                int denseCellCount = hasDenseStorage ? CountDirtyCells(chunk.dirtyMaskWords) : 0;
                int legacyCellCount = chunk.cells != null
                    ? math.min(chunk.cellCount, chunk.cells.Length)
                    : 0;

                if (denseCellCount <= 0 && legacyCellCount <= 0)
                    continue;

                int3 chunkCoord = new int3((int)chunk.chunkX, (int)chunk.chunkY, (int)chunk.chunkZ);
                ChunkAddress address = new ChunkAddress(chunkCoord, chunk.voxelSize);
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
            int bytesPerChunk = UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>()
                + (ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<ushort>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>());
            int totalBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            Dictionary<ChunkAddress, CompactedChunkState>.Enumerator compactedCountEnumerator = _compactedChunkStates.GetEnumerator();
            while (compactedCountEnumerator.MoveNext())
            {
                chunkCount++;
                totalDirtyCellCount += ChunkCellCount;
                totalBytes += bytesPerChunk;
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

                chunkCount++;
                totalDirtyCellCount += cellCount;
                totalBytes += bytesPerChunk;
            }

            countEnumerator.Dispose();
            if (chunkCount <= 0)
                return default;

            NativeArray<byte> snapshot = new NativeArray<byte>(totalBytes, allocator, NativeArrayOptions.UninitializedMemory);
            byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(snapshot);
            int cursor = 0;

            NativeSnapshotHeader header = new NativeSnapshotHeader
            {
                Version = NativeSnapshotMagic,
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
                WriteCompactedNativeSnapshotChunk(
                    snapshotPtr,
                    snapshot.Length,
                    ref cursor,
                    pair.Key,
                    in compactedState,
                    in overlayState,
                    overlayState.DirtyMaskWords.IsCreated);
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

                NativeSnapshotChunkHeader chunkHeader = new NativeSnapshotChunkHeader
                {
                    ChunkX = state.ChunkCoord.x,
                    ChunkY = state.ChunkCoord.y,
                    ChunkZ = state.ChunkCoord.z,
                    VoxelSize = state.VoxelSize,
                    DirtyCellCount = dirtyCellCount
                };

                UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + cursor);
                cursor += UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>();

                void* dirtyMaskPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.DirtyMaskWords);
                int dirtyMaskBytes = ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(snapshotPtr + cursor, snapshot.Length - cursor, dirtyMaskPtr, dirtyMaskBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor += dirtyMaskBytes;

                void* sdfPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.SdfValueBits);
                int sdfBytes = ChunkCellCount * UnsafeUtility.SizeOf<ushort>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(snapshotPtr + cursor, snapshot.Length - cursor, sdfPtr, sdfBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor += sdfBytes;

                void* materialPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.MaterialIds);
                int materialBytes = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(snapshotPtr + cursor, snapshot.Length - cursor, materialPtr, materialBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor += materialBytes;

                void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state.CellFlags);
                int flagsBytes = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(snapshotPtr + cursor, snapshot.Length - cursor, flagsPtr, flagsBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor += flagsBytes;
            }

            writeEnumerator.Dispose();
            return snapshot;
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
            NativeSnapshotChunkHeader chunkHeader = new NativeSnapshotChunkHeader
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = ChunkCellCount
            };

            UnsafeUtility.CopyStructureToPtr(ref chunkHeader, snapshotPtr + cursor);
            cursor += UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>();

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
            NativeSnapshotHeader header;

            if (snapshot.Length >= UnsafeUtility.SizeOf<NativeSnapshotHeader>())
            {
                NativeSnapshotHeader versionedHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotHeader>(snapshotPtr, 0);
                if (versionedHeader.Version == NativeSnapshotMagic)
                {
                    header = versionedHeader;
                    minimumHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
                    snapshotHasFlags = true;
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
            int chunkHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeader>();
            int loadedDirtyCellCount = 0;

            for (int chunkIndex = 0; chunkIndex < header.ChunkCount; chunkIndex++)
            {
                if (cursor > snapshot.Length - chunkHeaderBytes)
                {
                    error = "Voxel delta chunk header exceeds the snapshot bounds.";
                    return false;
                }

                NativeSnapshotChunkHeader chunkHeader = UnsafeUtility.ReadArrayElement<NativeSnapshotChunkHeader>(snapshotPtr + cursor, 0);
                cursor += chunkHeaderBytes;

                if (chunkHeader.VoxelSize <= 0f || chunkHeader.DirtyCellCount < 0)
                {
                    error = "Voxel delta chunk header contains invalid values.";
                    return false;
                }

                loadedDirtyCellCount += chunkHeader.DirtyCellCount;

                int chunkPayloadBytes = dirtyMaskByteLength + sdfByteLength + materialByteLength + (snapshotHasFlags ? flagsByteLength : 0);
                if (cursor > snapshot.Length - chunkPayloadBytes)
                {
                    error = "Voxel delta chunk payload exceeds the snapshot bounds.";
                    return false;
                }

                int3 chunkCoord = new int3(chunkHeader.ChunkX, chunkHeader.ChunkY, chunkHeader.ChunkZ);
                ChunkAddress address = new ChunkAddress(chunkCoord, chunkHeader.VoxelSize);
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
            }

            if (cursor != snapshot.Length)
            {
                error = "Voxel delta snapshot contains unread trailing bytes.";
                return false;
            }

            if (loadedDirtyCellCount != header.TotalDirtyCellCount)
            {
                error = "Voxel delta snapshot dirty-cell count does not match the header.";
                return false;
            }

            RequestRebuildsForLoadedState();
            return true;
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
                    _pendingRebuildVolumes.RemoveAt(i);
                    continue;
                }

                volume.RequestDeltaRebuild();
                _pendingRebuildVolumes.RemoveAt(i);
            }
        }

        private void TrySchedulePendingCarve()
        {
            if (IsScheduledCarveBusy || _pendingCarveCount <= 0)
                return;

            PendingCarveRequest request = _pendingCarves[0];
            for (int i = 1; i < _pendingCarveCount; i++)
                _pendingCarves[i - 1] = _pendingCarves[i];

            _pendingCarveCount--;
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
            float3 segmentStart = new float3(request.AbsoluteHitPoint.x, request.AbsoluteHitPoint.y, request.AbsoluteHitPoint.z);
            float3 segmentEnd = shape == DeltaShapeCapsule
                ? new float3(request.AbsoluteSegmentEnd.x, request.AbsoluteSegmentEnd.y, request.AbsoluteSegmentEnd.z)
                : segmentStart;
            float3 boundsMin = shape == DeltaShapeCapsule
                ? math.min(segmentStart, segmentEnd)
                : segmentStart - halfExtents;
            float3 boundsMax = shape == DeltaShapeCapsule
                ? math.max(segmentStart, segmentEnd)
                : segmentStart + halfExtents;
            float boundsPadding = shape == DeltaShapeCapsule ? radius + blendRadius : blendRadius;
            int3 minCell = new int3(
                Mathf.FloorToInt((boundsMin.x - boundsPadding) / voxelSize),
                Mathf.FloorToInt((boundsMin.y - boundsPadding) / voxelSize),
                Mathf.FloorToInt((boundsMin.z - boundsPadding) / voxelSize));
            int3 maxCell = new int3(
                Mathf.FloorToInt((boundsMax.x + boundsPadding) / voxelSize),
                Mathf.FloorToInt((boundsMax.y + boundsPadding) / voxelSize),
                Mathf.FloorToInt((boundsMax.z + boundsPadding) / voxelSize));
            ResolveVolumeCellBounds(volume, out int3 volumeMinCell, out int3 volumeMaxCell, out _, out _);
            if (!CellBoundsIntersect(minCell, maxCell, volumeMinCell, volumeMaxCell))
                return;

            minCell = math.max(minCell, volumeMinCell);
            maxCell = math.min(maxCell, volumeMaxCell);

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
                    Writes = _scheduledCarveWrites
                };

                using (_carveScheduleProfilerMarker.Auto())
                {
                    _scheduledCarveWriteCount = candidateCount;
                    _scheduledCarveHandle = carveJob.Schedule(candidateCount, 64);
                    _scheduledCarveRunning = true;
                    scheduled = true;
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

                        resolvedValue = ClampToHalf(SmoothMaxExp(currentDensity, (float)resolvedValue, math.max(voxelSize, write.BlendStrength)));
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

                EnqueueVolumeRebuild(volume);
                float resolvedCarveRadius = ResolveCarveRadius(in _scheduledCarveRequest, volume);
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
            Vector3 absoluteCenter = volume.GenerationAbsoluteUniversePosition;
            Vector3 minAbsolute = absoluteCenter - new Vector3(halfExtent, halfExtent, halfExtent);
            Vector3 maxAbsolute = absoluteCenter + new Vector3(halfExtent, halfExtent, halfExtent);

            minCell = new int3(
                Mathf.FloorToInt(minAbsolute.x / voxelSize),
                Mathf.FloorToInt(minAbsolute.y / voxelSize),
                Mathf.FloorToInt(minAbsolute.z / voxelSize));
            maxCell = new int3(
                Mathf.FloorToInt(maxAbsolute.x / voxelSize),
                Mathf.FloorToInt(maxAbsolute.y / voxelSize),
                Mathf.FloorToInt(maxAbsolute.z / voxelSize));
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
                PendingCompactionRequest pending = _pendingCompactions[i];
                if (!pending.Address.Equals(address))
                    continue;

                pending.Volume = volume;
                pending.RequiredSonarVersion = math.max(pending.RequiredSonarVersion, requiredSonarVersion);
                pending.WriteVersion = writeVersion;
                pending.DirtyCount = math.max(pending.DirtyCount, dirtyCount);
                _pendingCompactions[i] = pending;
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
                _pendingCompactions[_pendingCompactionCount++] = request;
                return true;
            }

            int replacementIndex = -1;
            int lowestDirtyCount = request.DirtyCount;
            for (int i = 0; i < _pendingCompactionCount; i++)
            {
                int candidateDirtyCount = _pendingCompactions[i].DirtyCount;
                if (!ShouldReplaceQueuedCompaction(lowestDirtyCount, candidateDirtyCount))
                    continue;

                lowestDirtyCount = candidateDirtyCount;
                replacementIndex = i;
            }

            if (replacementIndex < 0)
                return false;

            _pendingCompactions[replacementIndex] = request;
            return true;
        }

        private static bool ShouldReplaceQueuedCompaction(int requestDirtyCount, int candidateDirtyCount)
        {
            return candidateDirtyCount < requestDirtyCount;
        }

        private void TrySchedulePendingCompaction()
        {
            if (_scheduledCompactionRunning || _pendingCompactionCount <= 0)
                return;

            PendingCompactionRequest request = _pendingCompactions[0];
            for (int i = 1; i < _pendingCompactionCount; i++)
                _pendingCompactions[i - 1] = _pendingCompactions[i];

            _pendingCompactionCount--;
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
                RegisterTrackedNativeArray(dirtyMaskCopy, nameof(dirtyMaskCopy));
                RegisterTrackedNativeArray(deltaSdfCopy, nameof(deltaSdfCopy));
                RegisterTrackedNativeArray(materialCopy, nameof(materialCopy));
                RegisterTrackedNativeArray(flagsCopy, nameof(flagsCopy));
                RegisterTrackedNativeArray(outputSdf, nameof(outputSdf));
                RegisterTrackedNativeArray(outputMaterials, nameof(outputMaterials));
                RegisterTrackedNativeArray(outputFlags, nameof(outputFlags));
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
                    OutputCellFlags = outputFlags
                };

                VoxelDeltaCompactionJob job = new VoxelDeltaCompactionJob
                {
                    ChunkCoord = request.Address.ChunkCoord,
                    VoxelSize = math.max(request.Address.VoxelSize, MinRuntimeVoxelSize),
                    GridDimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z),
                    VolumeOrigin = new float3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z),
                    CellSize = new float3(voxelCellSize.x, voxelCellSize.y, voxelCellSize.z),
                    SdfRange = sdfRange,
                    EncodedSdf = sourceSdf,
                    DirtyMaskWords = dirtyMaskCopy,
                    DeltaSdfValueBits = deltaSdfCopy,
                    DeltaMaterialIds = materialCopy,
                    DeltaCellFlags = flagsCopy,
                    OutputSdfValueBits = outputSdf,
                    OutputMaterialIds = outputMaterials,
                    OutputCellFlags = outputFlags
                };
                _scheduledCompactionHandle = job.Schedule(ChunkCellCount, 64);
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
                request.OutputCellFlags);

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
                Vector3 absoluteCellCenter = new Vector3(
                    (absoluteCell.x + 0.5f) * voxelSize,
                    (absoluteCell.y + 0.5f) * voxelSize,
                    (absoluteCell.z + 0.5f) * voxelSize);
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
            return chunk.dirtyMaskWords != null &&
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

        private static float SmoothMaxExp(float a, float b, float k)
        {
            k = math.max(k, 0.0001f);
            float maxValue = math.max(a, b);
            float expA = math.exp(-math.clamp(k * (maxValue - a), 0f, 60f));
            float expB = math.exp(-math.clamp(k * (maxValue - b), 0f, 60f));
            return maxValue + math.log(expA + expB) / k;
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
            int spawnCount = math.clamp((int)math.round(removedVolume * carveDebrisPerCubicMeter), 0, carveDebrisMaxCount);
            if (spawnCount <= 0)
                return;

            uint state = (uint)math.hash(new int4(
                (int)math.round(request.AbsoluteHitPoint.x * 10f),
                (int)math.round(request.AbsoluteHitPoint.y * 10f),
                (int)math.round(request.AbsoluteHitPoint.z * 10f),
                math.max(1, (int)math.round(radius * 100f))));

            float spawnRadius = math.max(radius * 0.35f, MinRuntimeVoxelSize);
            for (int i = 0; i < spawnCount; i++)
            {
                float3 direction = NextBurstDirection(ref state);
                float distance01 = NextBurst01(ref state);
                float impulse01 = NextBurst01(ref state);
                Vector3 absoluteSpawnPosition = request.AbsoluteHitPoint + new Vector3(direction.x, direction.y, direction.z) * (spawnRadius * distance01);
                Vector3 runtimeSpawnPosition = HectonFloatingOrigin.ToRuntimePosition(absoluteSpawnPosition);
                Vector3 burstImpulse = new Vector3(direction.x, direction.y, direction.z) * math.lerp(carveDebrisImpulse * 0.55f, carveDebrisImpulse, impulse01);
                Vector3 sampledCurrent = CurrentVolume.SampleCombinedCurrent(runtimeSpawnPosition);
                float3 currentImpulse3 = new float3(sampledCurrent.x, sampledCurrent.y, sampledCurrent.z) * math.max(0.25f, carveDebrisImpulse * 0.35f);
                Vector3 currentImpulse = math.all(math.isfinite(currentImpulse3))
                    ? new Vector3(currentImpulse3.x, currentImpulse3.y, currentImpulse3.z)
                    : Vector3.zero;
                Vector3 initialImpulse = burstImpulse + currentImpulse;
                registry.TryRegisterDroppedItem(carveDebrisItem, 1, runtimeSpawnPosition, initialImpulse);
            }
        }

        private static float NextBurst01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float3 NextBurstDirection(ref uint state)
        {
            float z = math.lerp(-1f, 1f, NextBurst01(ref state));
            float angle = NextBurst01(ref state) * (math.PI * 2f);
            float radial = math.sqrt(math.max(0f, 1f - (z * z)));
            return new float3(radial * math.cos(angle), z, radial * math.sin(angle));
        }

        private void EnsureScheduledCarveWriteCapacity(int requiredCount)
        {
            if (_scheduledCarveWrites.IsCreated && _scheduledCarveWrites.Length >= requiredCount)
                return;

            if (_scheduledCarveWrites.IsCreated)
                DisposeTrackedNativeArray(ref _scheduledCarveWrites);

            // COLD ALLOC: NativeArray<CarveCellWrite>[requiredCount] â€” staged carve-write buffer for deferred voxel SDF mutation commits â€” owner: VoxelDeltaProcessor
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
            Vector3 impulseDirection = request.AbsoluteImpulseDirection;
            if (impulseDirection.sqrMagnitude <= 0.0001f)
                impulseDirection = Vector3.up;
            else
                impulseDirection.Normalize();

            Vector3 outwardNormal = -impulseDirection;
            Vector3 runtimeOrigin = runtimeHitPoint + outwardNormal * math.max(radius * 0.2f, MinRuntimeVoxelSize);
            uint seed = (uint)math.hash(new int4(
                (int)math.round(request.AbsoluteHitPoint.x * 8f),
                (int)math.round(request.AbsoluteHitPoint.y * 8f),
                (int)math.round(request.AbsoluteHitPoint.z * 8f),
                math.max(1, (int)math.round(radius * 64f))));
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

        private static void PushRecentCutHeat(in PendingCarveRequest request, float radius)
        {
            if (radius <= 0f)
                return;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(request.AbsoluteHitPoint);
            int slot = s_recentCutHeatCursor;
            s_recentCutHeatCursor = (slot + 1) % RecentCutHeatMax;
            s_recentCutHeatCount = math.min(s_recentCutHeatCount + 1, RecentCutHeatMax);
            float shaderRadius = math.max(radius * LaserCutHeatRadiusScale, MinRuntimeVoxelSize);
            s_recentCutHeatPositionRadius[slot] = new Vector4(runtimeHitPoint.x, runtimeHitPoint.y, runtimeHitPoint.z, shaderRadius);
            s_recentCutHeatStrengthTime[slot] = new Vector4(LaserCutHeatStrength, Time.time, LaserCutHeatLifetimeSeconds, 0f);
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

        private static void RemoveVolume(List<HectonVoxelVolume> list, HectonVoxelVolume volume)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i], volume))
                    continue;

                int last = list.Count - 1;
                list[i] = list[last];
                list.RemoveAt(last);
                break;
            }
        }

        private static half ClampToHalf(float value)
        {
            return (half)math.clamp(value, -8f, 8f);
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
        }

        private struct ThermalMeltRuntime
        {
            public HectonVoxelVolume Volume;
            public Vector3 AbsoluteCenter;
            public float RadiusMeters;
            public float ElapsedSeconds;
            public float StepAccumulatorSeconds;
        }

        private struct PendingCarveRequest
        {
            public HectonVoxelVolume Volume;
            public Vector3 AbsoluteHitPoint;
            public Vector3 AbsoluteSegmentEnd;
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
        private struct CarveSdfJob : IJobParallelFor
        {
            public int3 MinCell;
            public int3 Span;
            public float VoxelSize;
            public float Radius;
            public float BlendRadius;
            public float BlendStrength;
            public float3 Center;
            public float3 SegmentEnd;
            public float3 HalfExtents;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte Shape;
            public NativeArray<CarveCellWrite> Writes;

            public void Execute(int index)
            {
                int spanXY = Span.x * Span.y;
                int localZ = index / spanXY;
                int remainder = index - (localZ * spanXY);
                int localY = remainder / Span.x;
                int localX = remainder - (localY * Span.x);
                int3 absoluteCell = MinCell + new int3(localX, localY, localZ);
                float3 cellCenter = (new float3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5f) * VoxelSize;
                float signedDistance = Shape == DeltaShapeBox
                    ? BoxSdf(cellCenter - Center, HalfExtents)
                    : Shape == DeltaShapeCapsule
                        ? CapsuleSdf(cellCenter, Center, SegmentEnd, Radius)
                        : math.distance(cellCenter, Center) - Radius;
                if (signedDistance >= BlendRadius)
                {
                    Writes[index] = default;
                    return;
                }

                float densityValue = (DeltaFlags & DeltaModeAdditive) != 0
                    ? math.clamp(-signedDistance, -8f, 8f)
                    : math.clamp(signedDistance, -8f, 8f);

                Writes[index] = new CarveCellWrite
                {
                    AbsoluteCell = absoluteCell,
                    SdfValueBits = (ushort)math.f32tof16(densityValue),
                    MaterialId = MaterialId,
                    DeltaFlags = DeltaFlags,
                    BlendStrength = BlendStrength,
                    IsActive = 1
                };
            }

            private static float BoxSdf(float3 local, float3 halfExtents)
            {
                float3 q = math.abs(local) - math.max(halfExtents, new float3(0.001f));
                return math.length(math.max(q, 0f)) + math.min(math.cmax(q), 0f);
            }

            private static float CapsuleSdf(float3 point, float3 start, float3 end, float radius)
            {
                float3 segment = end - start;
                float segmentLengthSq = math.max(math.lengthsq(segment), 0.0001f);
                float t = math.saturate(math.dot(point - start, segment) / segmentLengthSq);
                return math.distance(point, start + segment * t) - math.max(radius, 0.001f);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CarveCellWrite
        {
            public int3 AbsoluteCell;
            public ushort SdfValueBits;
            public float BlendStrength;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte IsActive;
            public byte Reserved;
        }

        private struct CompactedChunkState : IDisposable
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            public NativeArray<ushort> SdfValueBits;
            public NativeArray<byte> MaterialIds;
            public NativeArray<byte> CellFlags;

            public CompactedChunkState(
                int3 chunkCoord,
                float voxelSize,
                NativeArray<ushort> sdfValueBits,
                NativeArray<byte> materialIds,
                NativeArray<byte> cellFlags)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                SdfValueBits = sdfValueBits;
                MaterialIds = materialIds;
                CellFlags = cellFlags;
            }

            public void Dispose()
            {
                DisposeTrackedNativeArray(ref SdfValueBits, default);
                DisposeTrackedNativeArray(ref MaterialIds, default);
                DisposeTrackedNativeArray(ref CellFlags, default);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct VoxelDeltaCompactionJob : IJobParallelFor
        {
            public int3 ChunkCoord;
            public float VoxelSize;
            public int3 GridDimensions;
            public float3 VolumeOrigin;
            public float3 CellSize;
            public float SdfRange;
            [ReadOnly] public NativeArray<byte> EncodedSdf;
            [ReadOnly] public NativeArray<uint> DirtyMaskWords;
            [ReadOnly] public NativeArray<ushort> DeltaSdfValueBits;
            [ReadOnly] public NativeArray<byte> DeltaMaterialIds;
            [ReadOnly] public NativeArray<byte> DeltaCellFlags;
            public NativeArray<ushort> OutputSdfValueBits;
            public NativeArray<byte> OutputMaterialIds;
            public NativeArray<byte> OutputCellFlags;

            public void Execute(int flatIndex)
            {
                int3 absoluteCell = AbsoluteCellFromFlatIndex(flatIndex);
                float3 absolutePosition = (new float3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5f) * VoxelSize;
                float sampledDensity = SampleEncodedSdf(absolutePosition);
                if (IsDirty(flatIndex))
                {
                    byte deltaFlags = DeltaCellFlags[flatIndex];
                    float deltaDensity = DecodeHalfToFloat(DeltaSdfValueBits[flatIndex]);
                    float bakedDensity = BakeDeltaIntoBaseDensity(sampledDensity, deltaDensity, deltaFlags);
                    OutputSdfValueBits[flatIndex] = (ushort)math.f32tof16(math.clamp(bakedDensity, -8f, 8f));
                    OutputMaterialIds[flatIndex] = DeltaMaterialIds[flatIndex];
                    OutputCellFlags[flatIndex] = DeltaModeReplace;
                    return;
                }

                OutputSdfValueBits[flatIndex] = (ushort)math.f32tof16(math.clamp(sampledDensity, -8f, 8f));
                OutputMaterialIds[flatIndex] = DefaultMaterialId;
                OutputCellFlags[flatIndex] = DeltaModeReplace;
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
                return (DirtyMaskWords[wordIndex] & bitMask) != 0u;
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
                float sampleX = math.clamp((absolutePosition.x - VolumeOrigin.x) / math.max(CellSize.x, 0.0001f), 0f, GridDimensions.x - 1.001f);
                float sampleY = math.clamp((absolutePosition.y - VolumeOrigin.y) / math.max(CellSize.y, 0.0001f), 0f, GridDimensions.y - 1.001f);
                float sampleZ = math.clamp((absolutePosition.z - VolumeOrigin.z) / math.max(CellSize.z, 0.0001f), 0f, GridDimensions.z - 1.001f);

                int x0 = (int)math.floor(sampleX);
                int y0 = (int)math.floor(sampleY);
                int z0 = (int)math.floor(sampleZ);
                int x1 = math.min(x0 + 1, GridDimensions.x - 1);
                int y1 = math.min(y0 + 1, GridDimensions.y - 1);
                int z1 = math.min(z0 + 1, GridDimensions.z - 1);
                float tx = sampleX - x0;
                float ty = sampleY - y0;
                float tz = sampleZ - z0;

                float c000 = Decode(GridIndex(x0, y0, z0));
                float c100 = Decode(GridIndex(x1, y0, z0));
                float c010 = Decode(GridIndex(x0, y1, z0));
                float c110 = Decode(GridIndex(x1, y1, z0));
                float c001 = Decode(GridIndex(x0, y0, z1));
                float c101 = Decode(GridIndex(x1, y0, z1));
                float c011 = Decode(GridIndex(x0, y1, z1));
                float c111 = Decode(GridIndex(x1, y1, z1));

                float c00 = math.lerp(c000, c100, tx);
                float c10 = math.lerp(c010, c110, tx);
                float c01 = math.lerp(c001, c101, tx);
                float c11 = math.lerp(c011, c111, tx);
                float c0 = math.lerp(c00, c10, ty);
                float c1 = math.lerp(c01, c11, ty);
                return math.lerp(c0, c1, tz);
            }

            private int GridIndex(int x, int y, int z)
            {
                return x + y * GridDimensions.x + z * GridDimensions.x * GridDimensions.y;
            }

            private float Decode(int index)
            {
                return ((EncodedSdf[index] * (1f / 255f)) * 2f - 1f) * SdfRange;
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

        private readonly struct ChunkAddress : IEquatable<ChunkAddress>
        {
            public readonly int3 ChunkCoord;
            public readonly float VoxelSize;
            private readonly int _voxelSizeBits;

            public ChunkAddress(int3 chunkCoord, float voxelSize)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                _voxelSizeBits = math.asint(voxelSize);
            }

            public bool Equals(ChunkAddress other)
            {
                return ChunkCoord.Equals(other.ChunkCoord) && _voxelSizeBits == other._voxelSizeBits;
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkAddress other && Equals(other);
            }

            public override int GetHashCode()
            {
                return unchecked((ChunkCoord.GetHashCode() * 397) ^ _voxelSizeBits);
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
