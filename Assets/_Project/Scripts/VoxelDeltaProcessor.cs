using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
        [FieldOffset(2)] public ushort Reserved;
        [FieldOffset(4)] public ushort Reserved1;
        [FieldOffset(6)] public byte MaterialId;
        [FieldOffset(7)] public byte Flags;
    }

    /// <summary>
    /// Authoritative absolute-universe thermal melt request produced by lava/vent gameplay.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct ThermalMeltEvent
    {
        [FieldOffset(0)]
        public double3 AbsoluteUniversePositionDouble;

        [FieldOffset(24)]
        public Vector3 AbsoluteUniversePosition;

        [FieldOffset(36)]
        public float RadiusMeters;

        [FieldOffset(40)]
        public float Heat01;

        [FieldOffset(44)]
        private uint _pad0;
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
    public sealed class VoxelDeltaProcessor : MonoBehaviour, ISaveable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001VoxelDeltaProcessorSignalPushDropCount;
        private const int ChunkResolution = 32;
        private const int ChunkCellCount = VoxelDeltaChunkDTO.CellCount;
        private const int ChunkDirtyMaskWordCount = VoxelDeltaChunkDTO.DirtyMaskWordCount;
        private const int InitialChunkRegistryCapacity = 256;
        private const int InitialVolumeRegistryCapacity = 64;
        private const int InitialPendingCarveCapacity = 32;
        private const int InitialCarveEventQueueCapacity = 64;
        private const int QueuedCarveMask = InitialCarveEventQueueCapacity - 1;
        private const int InitialPendingCompactionCapacity = 16;
        private const int DirtyChunkStatePoolCapacity = InitialChunkRegistryCapacity;
        private const int CompactionSourceSdfMaxGridDimension = 129;
        private const int CompactionSourceSdfCapacity = CompactionSourceSdfMaxGridDimension * CompactionSourceSdfMaxGridDimension * CompactionSourceSdfMaxGridDimension;
        private const int VoxelBlackBoxCapacity = 300;
        private const int PendingCarveMask = InitialPendingCarveCapacity - 1;
        private const int PendingCompactionMask = InitialPendingCompactionCapacity - 1;
        private const int MaxActiveThermalMeltEvents = 16;
        private const double InvalidAupCoordinateComponent = double.NaN;
        private const float InvalidRuntimeCoordinateComponent = float.NaN;
        private const double RuntimeAupLocalClampMeters = 1048576d;
        private const int MinQueuedCarveDrainBudgetPerFrame = 1;
        private const int MaxQueuedCarveDrainBudgetPerFrame = 4;
        private const int MinScheduledCarveJobCandidatesPerSlice = 2048;
        private const int MaxScheduledCarveJobCandidatesPerSlice = 8192;
        private const int MinScheduledCarveCommitWritesPerFrame = 64;
        private const int MaxScheduledCarveCommitWritesPerFrame = 512;
        private const int MinScheduledCarveCommitScansPerFrame = 512;
        private const int MaxScheduledCarveCommitScansPerFrame = 4096;
        private const float ScheduledCarveBacklogPressureBoost = 0.5f;
        private const int ScheduledCarveWriteCapacity = MaxScheduledCarveJobCandidatesPerSlice;
        private const int CompactionFrostTickIntervalFrames = 300;
        private const int CompactionPressurePendingThreshold = InitialPendingCompactionCapacity / 2;
        private const int CompactionPressureFreeSlotThreshold = DirtyChunkStatePoolCapacity / 8;
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
        private const byte DeltaModeAdditive = VoxelDeltaChunkDTO.CellFlagAdditive;
        private const byte DeltaModeReplace = VoxelDeltaChunkDTO.CellFlagReplace;
        private const byte CarveSourceLaser = 1 << 0;
        private const byte DeltaShapeSphere = 0;
        private const byte DeltaShapeBox = 1;
        private const byte DeltaShapeCapsule = 2;
        private const byte PendingCarveRuntimeFlagSliced = 1 << 0;
        private const byte PendingCarveRuntimeFlagSuppressPresentation = 1 << 1;
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
        private const int PagerSectorPayloadBytes = (256 * 1024) - 64;
        private const int PagerVoxelDeltaHeaderBytes = 32;
        private const int MaxSparseDeltaRunsPerPagerPayload = (PagerSectorPayloadBytes - PagerVoxelDeltaHeaderBytes) / SaveDeltaCompressionLayout.SaveVoxelDeltaRun8StrideBytes;
        private const uint SaveCorruptionHashMismatchAction = 1u;
        private const uint SaveCorruptionBoundsAction = 2u;
        private const uint SaveCorruptionMalformedRleAction = 3u;
        private const uint VoxelBlackBoxDumpMagic = 0x564F5844u; // "VOXD"
        private const uint VoxelBlackBoxInvalidCarveEventFlag = 1u << 0;
        private const uint VoxelBlackBoxQueueOverflowFlag = 1u << 1;
        private const uint VoxelBlackBoxInvalidPendingCarveFlag = 1u << 2;
        private const uint VoxelBlackBoxCommitBudgetFlag = 1u << 3;
        private const uint VoxelBlackBoxPendingQueueCorruptionFlag = 1u << 4;
        private const uint VoxelBlackBoxChunkStatePoolCorruptionFlag = 1u << 5;
        private const uint VoxelBlackBoxScheduledCarveJobOverrunFlag = 1u << 6;
        private const uint VoxelBlackBoxScheduledCarveSlicedFlag = 1u << 7;
        private const uint VoxelBlackBoxCarvedMassTelemetryFlag = 1u << 8;
        private const uint VoxelBlackBoxChunkStatePoolExhaustedFlag = 1u << 9;
        private const string VoxelPagingBlackBoxDumpRelativePath1312 = "Docs/AgentLogs/Dump_1312_VoxelPaging.bin";
        private const string VoxelBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1304_Voxel.bin";
        private const string NativeMemoryOwner = nameof(VoxelDeltaProcessor);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const uint TitaniumOreHash = 0x61C51592u;
        private const uint TitaniumScrapItemHash = 0xD150482Eu;
        private static readonly uint _VoxelDebrisSignalHash = unchecked((uint)Hecton.Localization.LocHash.Compute("voxel.debris.carve"));
        private static readonly ProfilerMarker _carveScheduleProfilerMarker = new ProfilerMarker("H8.VoxelDelta.ScheduleCarve");
        private static readonly ProfilerMarker _carveCommitProfilerMarker = new ProfilerMarker("H8.VoxelDelta.CommitCarve");
        private static readonly uint _DataVaultRebindWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.DataVaultRebindDeferred"));
        private static readonly uint _DataVaultRebindTelemetryContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.RebindDataVaultCold"));
        private static readonly uint _SaveCorruptionHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SAVE_CORRUPTION_HASH"));
        private static readonly uint _SaveCorruptionContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("VoxelDeltaProcessor.LoadSparseRle"));
        private static readonly int _laserHitAupId = Shader.PropertyToID("_LaserHitAup");
        private static readonly int _laserHitHeatId = Shader.PropertyToID("_LaserHitHeat");
        private static readonly int _recentCutHeatPositionRadiusId = Shader.PropertyToID("_HectonRecentCutHeatPositionRadius");
        private static readonly int _recentCutHeatStrengthTimeId = Shader.PropertyToID("_HectonRecentCutHeatStrengthTime");
        private static readonly int _recentCutHeatCountId = Shader.PropertyToID("_HectonRecentCutHeatCount");
        private const uint CompactionScratchPinSourceSdf = 1u << 0;
        private const uint CompactionScratchPinDirtyMask = 1u << 1;
        private const uint CompactionScratchPinDeltaSdf = 1u << 2;
        private const uint CompactionScratchPinMaterial = 1u << 3;
        private const uint CompactionScratchPinFlags = 1u << 4;
        private const uint CompactionScratchPinOutputSdf = 1u << 5;
        private const uint CompactionScratchPinOutputMaterials = 1u << 6;
        private const uint CompactionScratchPinOutputFlags = 1u << 7;
        private const uint CompactionScratchPinUniformFlag = 1u << 8;
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
        private ISaveService _registeredSaveService;
        private IFluidDecalPresentationSink _fluidDecals;
        private bool _saveRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _pendingDataVaultRebind;
        private IDataVault _pendingDataVaultPrevious;
        private IDataVault _pendingDataVaultNext;

        private FixedChunkRegistry<ChunkDeltaState> _chunkStates;
        private FixedChunkRegistry<CompactedChunkState> _compactedChunkStates;
        private FixedChunkRegistry<int> _chunkWriteVersions;
        private FixedList4096Bytes<ChunkDeltaState> _chunkStatePoolBank0;
        private FixedList4096Bytes<ChunkDeltaState> _chunkStatePoolBank1;
        private FixedList4096Bytes<ChunkDeltaState> _chunkStatePoolBank2;
        private FixedList4096Bytes<int> _chunkStateFreeStack;
        private int _chunkStateFreeCount;
        private bool _chunkStatePoolCreated;
        private bool _chunkStatePoolVaultBacked;
        private bool _chunkStatePoolExhaustedWarningArmed;
        private VaultGenerationHandle<uint> _chunkStateDirtyMaskPoolHandle;
        private VaultGenerationHandle<ushort> _chunkStateSdfBitsPoolHandle;
        private VaultGenerationHandle<byte> _chunkStateMaterialPoolHandle;
        private VaultGenerationHandle<byte> _chunkStateCellFlagsPoolHandle;
        private FixedVolumeRegistry _registeredVolumes = new FixedVolumeRegistry(InitialVolumeRegistryCapacity, VolumeRegistryLane.Registered);
        private FixedVolumeRegistry _pendingRebuildVolumes = new FixedVolumeRegistry(InitialVolumeRegistryCapacity, VolumeRegistryLane.PendingRebuild);
        // COLD ALLOC: PendingCarveRequest[InitialPendingCarveCapacity] - deferred plasma-cut carve staging buffer - owner: VoxelDeltaProcessor
        private readonly PendingCarveRequest[] _pendingCarves = new PendingCarveRequest[InitialPendingCarveCapacity];
        // COLD ALLOC: ThermalMeltRuntime[16] - bounded lava crater-expansion requests - owner: VoxelDeltaProcessor
        private readonly ThermalMeltRuntime[] _thermalMeltEvents = new ThermalMeltRuntime[MaxActiveThermalMeltEvents];
        private int _pendingCarveHead;
        private int _pendingCarveCount;
        private VaultGenerationHandle<VoxelCarveEvent> _queuedCarveEventsHandle;
        private int _queuedCarveEventHead;
        private int _queuedCarveEventCount;
        private int _queuedCarveDrainFrame = -1;
        private float _queuedCarveDrainBudgetTokens;
        private int _thermalMeltCount;
        private JobHandle _scheduledCarveHandle;
        private bool _scheduledCarveRunning;
        private PendingCarveRequest _scheduledCarveRequest;
        private int _scheduledCarveWriteCount;
        private bool _scheduledCarveCommitPending;
        private int _scheduledCarveCommitIndex;
        private int _scheduledCarveCommitFrame = -1;
        private float _scheduledCarveCommitWriteTokens;
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
        private IDataVault _scheduledCarveWritesPinVault;
        private ulong _deferredScheduledCarveBlackBoxVolumeId;
        private uint _deferredScheduledCarveBlackBoxFlags;
        // COLD ALLOC: PendingCompactionRequest[16] - bounded background dirty-chunk compaction queue - owner: VoxelDeltaProcessor
        private readonly PendingCompactionRequest[] _pendingCompactions = new PendingCompactionRequest[InitialPendingCompactionCapacity];
        private int _pendingCompactionHead;
        private int _pendingCompactionCount;
        private int _compactionFrostTickCounter;
        private JobHandle _scheduledCompactionHandle;
        private bool _scheduledCompactionRunning;
        private ScheduledCompactionRequest _scheduledCompactionRequest;
        private VaultGenerationHandle<byte> _compactionSourceSdfScratchHandle;
        private VaultGenerationHandle<uint> _compactionDirtyMaskScratchHandle;
        private VaultGenerationHandle<ushort> _compactionDeltaSdfScratchHandle;
        private VaultGenerationHandle<byte> _compactionMaterialScratchHandle;
        private VaultGenerationHandle<byte> _compactionFlagsScratchHandle;
        private VaultGenerationHandle<ushort> _compactionOutputSdfScratchHandle;
        private VaultGenerationHandle<byte> _compactionOutputMaterialsScratchHandle;
        private VaultGenerationHandle<byte> _compactionOutputFlagsScratchHandle;
        private VaultGenerationHandle<byte> _compactionUniformFlagScratchHandle;
        private bool _compactionScratchCreated;
        private bool _compactionScratchLeased;
        private uint _compactionScratchPinMask;
        private IDataVault _compactionScratchPinVault;
        private VaultGenerationHandle<byte> _nativeSnapshotScratchHandle;
        private int _nativeSnapshotScratchCapacityBytes;
        private int _nativeSnapshotScratchLeaseCount;
        private bool _nativeSnapshotScratchDisposeDeferred;
        private IDataVault _nativeSnapshotScratchDeferredVault;
        public int SavePriority => 40;

        public int LoadPriority => 30;

        private bool IsScheduledCarveBusy => _scheduledCarveRunning || _scheduledCarveCommitPending;

        private void OnEnable()
        {
            TryGetComponent(out _engine);
            _dataVault = GlobalRegistry.DataVault;
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
            _saveService = GlobalRegistry.Save;
            _fluidDecals = GlobalRegistry.FluidDecalPresentation;
            TryRegisterHotSwapListener();
            EnsureCarveEventQueue();
            EnsureBlackBox();
            EnsureChunkStatePool();
            EnsureScheduledCarveWriteBuffer();
            EnsureCompactionScratchBuffers();
            EnsureNativeSnapshotScratchBuffer();
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
                _dispatcherRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_lateFrameRegistered)
            {
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            TryRegisterSaveService();
        }

        private void OnDisable()
        {
            DisposeCarveEventQueue();
            DisposeBlackBox();
            DisposeScheduledCarveBuffersForShutdownOnly();
            DisposeScheduledCompactionBuffersForShutdownOnly();
            DisposeCompactionScratchBuffers();
            DisposeNativeSnapshotScratchBuffer();
            _simulationBucketer = null;
            TryUnregisterHotSwapListener();
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

            TryUnregisterSaveService();

            _saveService = null;
            _fluidDecals = null;
            _pendingDataVaultRebind = false;
            _pendingDataVaultPrevious = null;
            _pendingDataVaultNext = null;
            _pendingCarveCount = 0;
            _pendingCarveHead = 0;
            _queuedCarveEventHead = 0;
            _queuedCarveEventCount = 0;
            _queuedCarveDrainFrame = -1;
            _queuedCarveDrainBudgetTokens = 0f;
            _pendingCompactionCount = 0;
            _pendingCompactionHead = 0;
            _compactionFrostTickCounter = 0;
            _thermalMeltCount = 0;
            _pendingRebuildVolumes.Clear();
            _registeredVolumes.Clear();
            DisposeChunkStates();
            DisposeCompactedChunkStates();
            DisposeChunkStatePool();
            ResetRecentCutHeatState();
        }

        /// <summary>
        /// Flushes staged carve requests and deferred load-time rebuild requests on the registry dispatcher lane.
        /// </summary>
        /// <param name="deltaTime">Unused dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            if (_pendingDataVaultRebind && !TryApplyPendingDataVaultRebind())
            {
                WriteBlackBoxSample(0ul, VoxelBlackBoxQueueOverflowFlag);
                return;
            }

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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                ReplaceSaveService(currentService as ISaveService);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SimulationBucketerRuntime)
            {
                _simulationBucketer = currentService as ISimulationBucketer;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime)
            {
                _fluidDecals = currentService as IFluidDecalPresentationSink;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVaultCold(previousService as IDataVault, currentService as IDataVault);
        }

        private void RebindDataVaultCold(IDataVault previousVault, IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault) &&
                (!_chunkStatePoolCreated || _chunkStatePoolVaultBacked || nextVault == null))
            {
                return;
            }

            if (IsScheduledCarveBusy ||
                _scheduledCarveWritesLocked ||
                _scheduledCompactionRunning ||
                _compactionScratchLeased ||
                _compactionScratchPinMask != 0u ||
                _nativeSnapshotScratchLeaseCount > 0)
            {
                DeferDataVaultRebind(previousVault, nextVault, _chunkStates.Count + _pendingCompactionCount);
                return;
            }

            bool hasLiveVoxelState = _chunkStates.Count > 0 || _compactedChunkStates.Count > 0;
            if (hasLiveVoxelState && nextVault == null)
            {
                DeferDataVaultRebind(previousVault, nextVault, _chunkStates.Count);
                return;
            }

            NativeArray<byte> borrowedSnapshot = default;
            bool borrowedSnapshotAcquired = false;

            try
            {
                if (hasLiveVoxelState)
                {
                    if (!TryCopyNativeSnapshotToBorrowedScratch(out borrowedSnapshot, out int snapshotByteCount) ||
                        snapshotByteCount <= 0 ||
                        !borrowedSnapshot.IsCreated)
                    {
                        WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
                        GlobalTelemetryBus.PublishPerformanceWarning(
                            _DataVaultRebindWarningHash,
                            _DataVaultRebindTelemetryContextHash,
                            snapshotByteCount);
                        return;
                    }

                    borrowedSnapshotAcquired = true;
                }

                IDataVault oldVault = previousVault ?? _dataVault;
                _pendingCompactionCount = 0;
                _pendingCompactionHead = 0;
                _compactionFrostTickCounter = 0;
                DisposeChunkStates();
                DisposeCompactedChunkStates();
                DisposeChunkStatePool(oldVault);
                ReleaseScheduledCarveWriteHandle(oldVault);
                DisposeCompactionScratchBuffers(oldVault);
                DisposeNativeSnapshotScratchBuffer(oldVault);
                DisposeBlackBox();
                DisposeCarveEventQueue(oldVault);
                _dataVault = nextVault;
                EnsureCarveEventQueue();
                EnsureBlackBox();
                EnsureChunkStatePool();
                EnsureScheduledCarveWriteBuffer();
                EnsureCompactionScratchBuffers();
                EnsureNativeSnapshotScratchBuffer();

                if (borrowedSnapshotAcquired &&
                    !TryLoadNativeSnapshot(borrowedSnapshot, out _))
                {
                    WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
                    Hecton8.Core.H8Debug.LogError("[VoxelDeltaProcessor] DataVault rebind failed to restore voxel snapshot.", this);
                    if (oldVault != null && !ReferenceEquals(oldVault, nextVault))
                        RestoreDataVaultAfterFailedRebind(oldVault, nextVault, borrowedSnapshot);
                }
            }
            finally
            {
                if (borrowedSnapshotAcquired)
                    ReleaseBorrowedNativeSnapshotScratch();
            }
        }

        private bool TryApplyPendingDataVaultRebind()
        {
            if (!_pendingDataVaultRebind)
                return true;

            if (IsScheduledCarveBusy ||
                _scheduledCarveWritesLocked ||
                _scheduledCompactionRunning ||
                _compactionScratchLeased ||
                _compactionScratchPinMask != 0u ||
                _nativeSnapshotScratchLeaseCount > 0)
            {
                return false;
            }

            if (_pendingDataVaultNext == null && (_chunkStates.Count > 0 || _compactedChunkStates.Count > 0))
                return false;

            IDataVault previousVault = _pendingDataVaultPrevious;
            IDataVault nextVault = _pendingDataVaultNext;
            _pendingDataVaultRebind = false;
            _pendingDataVaultPrevious = null;
            _pendingDataVaultNext = null;
            RebindDataVaultCold(previousVault, nextVault);
            return !_pendingDataVaultRebind;
        }

        private void DeferDataVaultRebind(IDataVault previousVault, IDataVault nextVault, int pressureMetric)
        {
            _pendingDataVaultRebind = true;
            _pendingDataVaultPrevious = previousVault ?? _dataVault;
            _pendingDataVaultNext = nextVault;
            WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DataVaultRebindWarningHash,
                _DataVaultRebindTelemetryContextHash,
                pressureMetric);
        }

        private void RestoreDataVaultAfterFailedRebind(
            IDataVault oldVault,
            IDataVault failedVault,
            NativeArray<byte> borrowedSnapshot)
        {
            DisposeChunkStates();
            DisposeCompactedChunkStates();
            DisposeChunkStatePool(failedVault);
            ReleaseScheduledCarveWriteHandle(failedVault);
            DisposeCompactionScratchBuffers(failedVault);
            DisposeNativeSnapshotScratchBuffer(failedVault);
            DisposeBlackBox();
            DisposeCarveEventQueue(failedVault);
            _dataVault = oldVault;
            EnsureCarveEventQueue();
            EnsureBlackBox();
            EnsureChunkStatePool();
            EnsureScheduledCarveWriteBuffer();
            EnsureCompactionScratchBuffers();
            EnsureNativeSnapshotScratchBuffer();

            if (!TryLoadNativeSnapshot(borrowedSnapshot, out _))
            {
                WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
                Hecton8.Core.H8Debug.LogError("[VoxelDeltaProcessor] DataVault rebind rollback failed to restore voxel snapshot.", this);
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        internal static float DebugResolveThermalMeltProgress(float elapsedSeconds)
        {
            return ResolveThermalMeltProgress(elapsedSeconds);
        }

        internal static int DebugResolveQueuedCarveDrainBudget(float qualityWeight01)
        {
            return ResolveQueuedCarveDrainBudget(qualityWeight01);
        }

        private static int ResolveQueuedCarveDrainBudget(float qualityWeight01)
        {
            return math.clamp(
                (int)math.ceil(ResolveQueuedCarveDrainBudgetPerFrame(qualityWeight01)),
                MinQueuedCarveDrainBudgetPerFrame,
                MaxQueuedCarveDrainBudgetPerFrame);
        }

        private static float ResolveQueuedCarveDrainBudgetPerFrame(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float smooth = quality * quality * (3f - 2f * quality);
            return math.lerp(
                MinQueuedCarveDrainBudgetPerFrame,
                MaxQueuedCarveDrainBudgetPerFrame,
                smooth);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1f;
        }

        internal static int DebugVoxelBlackBoxCapacity => VoxelBlackBoxCapacity;
        internal static int DebugVoxelBlackBoxEntryBytes => UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>();
        internal static bool DebugIsFiniteCarveEvent(in VoxelCarveEvent carveEvent)
        {
            return IsFiniteCarveEvent(in carveEvent);
        }

        internal static VoxelCarveEvent DebugResolveOverflowQueuedCarveEvent(
            in VoxelCarveEvent overflowEvent,
            in VoxelCarveEvent newestEvent)
        {
            return ResolveOverflowQueuedCarveEvent(in overflowEvent, in newestEvent);
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

            if (!_registeredVolumes.TryAdd(volume))
            {
                if (HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
                return;
            }

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

            _registeredVolumes.Remove(volume);
            _pendingRebuildVolumes.Remove(volume);
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
            float requestedRadius = meltEvent.RadiusMeters;
            float requestedHeat = meltEvent.Heat01;
            if (!math.isfinite(requestedRadius) ||
                requestedRadius <= 0f ||
                !math.isfinite(requestedHeat))
            {
                WriteBlackBoxSample(0UL, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return false;
            }

            float heat01 = math.saturate(requestedHeat);
            if (heat01 < ThermalMeltMinimumHeat || _registeredVolumes.Count <= 0)
                return false;

            float radius = ClampCarveRadiusMeters(requestedRadius, MinRuntimeVoxelSize);

            double3 absolutePosition = ResolveThermalMeltPositionDouble(in meltEvent);
            if (!IsFiniteDouble3(absolutePosition))
            {
                WriteBlackBoxSample(0UL, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return false;
            }

            HectonVoxelVolume targetVolume = ResolveThermalMeltVolume(absolutePosition, radius);
            if (targetVolume == null)
                return false;

            if (!ValidateThermalMeltQueueState())
                return false;

            for (int i = 0; i < _thermalMeltCount;)
            {
                ThermalMeltRuntime existing = _thermalMeltEvents[i];
                if (existing.Volume == null || !existing.Volume.HasRuntimeData)
                {
                    RemoveThermalMeltAt(i);
                    continue;
                }

                if (!IsFiniteThermalMeltRuntime(in existing))
                {
                    WriteBlackBoxSample(0UL, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                    RemoveThermalMeltAt(i);
                    continue;
                }

                if (!ReferenceEquals(existing.Volume, targetVolume))
                {
                    i++;
                    continue;
                }

                float mergeRadius = math.max(radius, existing.RadiusMeters);
                if (math.lengthsq(existing.AbsoluteCenter - absolutePosition) > (double)mergeRadius * mergeRadius)
                {
                    i++;
                    continue;
                }

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
            if (!ValidateThermalMeltQueueState() || _thermalMeltCount <= 0)
                return;

            float safeDelta = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            for (int i = 0; i < _thermalMeltCount;)
            {
                ThermalMeltRuntime melt = _thermalMeltEvents[i];
                if (melt.Volume == null || !melt.Volume.HasRuntimeData)
                {
                    RemoveThermalMeltAt(i);
                    continue;
                }

                if (!IsFiniteThermalMeltRuntime(in melt))
                {
                    WriteBlackBoxSample(0UL, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
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
            if (!IsFiniteThermalMeltRuntime(in melt))
            {
                WriteBlackBoxSample(0UL, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return false;
            }

            float progress = ResolveThermalMeltProgress(melt.ElapsedSeconds);
            float requestedRadius = melt.RadiusMeters * progress;
            if (!math.isfinite(requestedRadius) || requestedRadius <= 0f)
                return false;

            float radius = ClampCarveRadiusMeters(requestedRadius, MinRuntimeVoxelSize);
            float strength = ClampCarveBlendStrengthMeters(radius * 0.35f, MinRuntimeVoxelSize);
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
            if (!ValidateThermalMeltQueueState() || index < 0 || index >= _thermalMeltCount)
                return;

            for (int i = index + 1; i < _thermalMeltCount; i++)
                _thermalMeltEvents[i - 1] = _thermalMeltEvents[i];

            _thermalMeltEvents[_thermalMeltCount - 1] = default;
            _thermalMeltCount--;
        }

        private static float ResolveThermalMeltProgress(float elapsedSeconds)
        {
            if (!math.isfinite(elapsedSeconds) || elapsedSeconds <= 0f)
                return 0f;

            float t = math.saturate(elapsedSeconds / ThermalMeltDurationSeconds);
            return t * t * (3f - 2f * t);
        }

        private static bool IsFiniteThermalMeltRuntime(in ThermalMeltRuntime melt)
        {
            return IsFiniteDouble3(melt.AbsoluteCenter) &&
                   math.isfinite(melt.RadiusMeters) &&
                   melt.RadiusMeters > 0f &&
                   math.isfinite(melt.ElapsedSeconds) &&
                   melt.ElapsedSeconds >= 0f &&
                   math.isfinite(melt.StepAccumulatorSeconds) &&
                   melt.StepAccumulatorSeconds >= 0f;
        }

        private static int ResolvePendingCarveSlot(int head, int logicalIndex)
        {
            return (head + logicalIndex) & PendingCarveMask;
        }

        private static int ResolvePendingCompactionSlot(int head, int logicalIndex)
        {
            return (head + logicalIndex) & PendingCompactionMask;
        }

        private bool ValidateThermalMeltQueueState()
        {
            if ((uint)_thermalMeltCount <= (uint)_thermalMeltEvents.Length)
                return true;

            WritePendingQueueCorruptionSample(1, 0, _thermalMeltCount, _thermalMeltEvents.Length);
            ClearThermalMeltQueue();
            return false;
        }

        private void ClearThermalMeltQueue()
        {
            for (int i = 0; i < _thermalMeltEvents.Length; i++)
                _thermalMeltEvents[i] = default;

            _thermalMeltCount = 0;
        }

        private bool ValidatePendingCarveQueueState()
        {
            if ((uint)_pendingCarveHead < (uint)_pendingCarves.Length &&
                (uint)_pendingCarveCount <= (uint)_pendingCarves.Length)
            {
                return true;
            }

            WritePendingQueueCorruptionSample(2, _pendingCarveHead, _pendingCarveCount, _pendingCarves.Length);
            ClearPendingCarveQueue();
            return false;
        }

        private void ClearPendingCarveQueue()
        {
            for (int i = 0; i < _pendingCarves.Length; i++)
                _pendingCarves[i] = default;

            _pendingCarveHead = 0;
            _pendingCarveCount = 0;
        }

        private bool ValidatePendingCompactionQueueState()
        {
            if ((uint)_pendingCompactionHead < (uint)_pendingCompactions.Length &&
                (uint)_pendingCompactionCount <= (uint)_pendingCompactions.Length)
            {
                return true;
            }

            WritePendingQueueCorruptionSample(3, _pendingCompactionHead, _pendingCompactionCount, _pendingCompactions.Length);
            ClearPendingCompactionQueue();
            return false;
        }

        private void ClearPendingCompactionQueue()
        {
            for (int i = 0; i < _pendingCompactions.Length; i++)
                _pendingCompactions[i] = default;

            _pendingCompactionHead = 0;
            _pendingCompactionCount = 0;
        }

        private void WritePendingQueueCorruptionSample(int queueId, int head, int count, int capacity)
        {
            int safeHead = math.clamp(head, 0, 0xFFFF);
            int safeCount = math.clamp(count, 0, 0xFFFFFF);
            int safeCapacity = math.clamp(capacity, 0, 0xFF);
            ulong encodedState = ((ulong)(uint)safeHead << 40) |
                                 ((ulong)(uint)safeCount << 8) |
                                 (uint)safeCapacity;
            uint flags = VoxelBlackBoxPendingQueueCorruptionFlag | ((uint)(queueId & 0xF) << 8);
            ReportBlackBoxSample(encodedState, flags);
        }

        private void DropOldestPendingCarve()
        {
            if (!ValidatePendingCarveQueueState() || _pendingCarveCount <= 0)
                return;

            _pendingCarves[_pendingCarveHead] = default;
            _pendingCarveHead = (_pendingCarveHead + 1) & PendingCarveMask;
            _pendingCarveCount--;
        }

        private bool TryReservePendingCarveSlot(bool dropOldestWhenFull)
        {
            if (!ValidatePendingCarveQueueState())
                return false;

            if (_pendingCarveCount < _pendingCarves.Length)
                return true;

            if (!IsScheduledCarveBusy)
                TrySchedulePendingCarve();

            if (!ValidatePendingCarveQueueState())
                return false;

            if (_pendingCarveCount < _pendingCarves.Length)
                return true;

            if (!dropOldestWhenFull)
                return false;

            DropOldestPendingCarve();
            return _pendingCarveCount < _pendingCarves.Length;
        }

        private void EnqueuePendingCarveUnchecked(in PendingCarveRequest request)
        {
            if (!ValidatePendingCarveQueueState() || _pendingCarveCount >= _pendingCarves.Length)
                return;

            int slot = ResolvePendingCarveSlot(_pendingCarveHead, _pendingCarveCount);
            _pendingCarves[slot] = request;
            _pendingCarveCount++;
        }

        private PendingCarveRequest PopPendingCarve()
        {
            if (!ValidatePendingCarveQueueState() || _pendingCarveCount <= 0)
                return default;

            PendingCarveRequest request = _pendingCarves[_pendingCarveHead];
            _pendingCarves[_pendingCarveHead] = default;
            _pendingCarveHead = (_pendingCarveHead + 1) & PendingCarveMask;
            _pendingCarveCount--;
            return request;
        }

        private bool TryCoalesceOverflowPendingCarve(in PendingCarveRequest request)
        {
            if (!ValidatePendingCarveQueueState() || _pendingCarveCount <= 0)
                return false;

            for (int i = _pendingCarveCount - 1; i >= 0; i--)
            {
                int slot = ResolvePendingCarveSlot(_pendingCarveHead, i);
                PendingCarveRequest existing = _pendingCarves[slot];
                if (!TryCoalescePendingCarve(ref existing, in request))
                    continue;

                _pendingCarves[slot] = existing;
                WriteBlackBoxSample(EntityId.ToULong(request.Volume.GetEntityId()), VoxelBlackBoxQueueOverflowFlag);
                return true;
            }

            return false;
        }

        private static bool TryCoalescePendingCarve(ref PendingCarveRequest existing, in PendingCarveRequest incoming)
        {
            if (existing.Volume == null ||
                incoming.Volume == null ||
                !ReferenceEquals(existing.Volume, incoming.Volume) ||
                existing.MaterialId != incoming.MaterialId ||
                existing.DeltaFlags != incoming.DeltaFlags ||
                IsSlicedPendingCarve(in existing) ||
                IsSlicedPendingCarve(in incoming) ||
                !CanCoalesceCarveShape(existing.Shape) ||
                !CanCoalesceCarveShape(incoming.Shape))
            {
                return false;
            }

            existing.AbsoluteSegmentEnd = ResolvePendingCarveSegmentEnd(in incoming);
            existing.AccumulatedDamage = math.min(float.MaxValue * 0.25f, existing.AccumulatedDamage + incoming.AccumulatedDamage);
            existing.ExplicitRadiusMeters = math.max(existing.ExplicitRadiusMeters, incoming.ExplicitRadiusMeters);
            existing.ExplicitBlendStrength = math.max(existing.ExplicitBlendStrength, incoming.ExplicitBlendStrength);
            existing.SourceFlags |= incoming.SourceFlags;
            existing.Shape = DeltaShapeCapsule;

            float incomingImpulseSq = incoming.AbsoluteImpulseDirection.sqrMagnitude;
            float existingImpulseSq = existing.AbsoluteImpulseDirection.sqrMagnitude;
            if (incomingImpulseSq > existingImpulseSq)
                existing.AbsoluteImpulseDirection = incoming.AbsoluteImpulseDirection;

            return true;
        }

        private static double3 ResolvePendingCarveSegmentEnd(in PendingCarveRequest request)
        {
            return request.Shape == DeltaShapeCapsule
                ? request.AbsoluteSegmentEnd
                : request.AbsoluteHitPoint;
        }

        private static bool CanCoalesceCarveShape(byte shape)
        {
            return shape != DeltaShapeBox;
        }

        private static bool IsSlicedPendingCarve(in PendingCarveRequest request)
        {
            return (request.RuntimeFlags & PendingCarveRuntimeFlagSliced) != 0;
        }

        private static bool ShouldSuppressCarvePresentation(in PendingCarveRequest request)
        {
            return (request.RuntimeFlags & PendingCarveRuntimeFlagSuppressPresentation) != 0;
        }

        private bool EnsureCarveEventQueue()
        {
            EnsureCarveSignalLane();

            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _queuedCarveEventsHandle = default;
                _queuedCarveEventHead = 0;
                _queuedCarveEventCount = 0;
                return false;
            }

            if (IsExactVaultHandle(in _queuedCarveEventsHandle, BufferID.ShinobuDeltaCrusherCarveEventQueue) &&
                TryResolveVaultBuffer(vault, in _queuedCarveEventsHandle, BufferID.ShinobuDeltaCrusherCarveEventQueue, InitialCarveEventQueueCapacity, out _))
            {
                return true;
            }

            if (vault.IsCompactionFenceActive)
                return false;

            _queuedCarveEventsHandle = vault.EnsureGenerationHandle<VoxelCarveEvent>(
                BufferID.ShinobuDeltaCrusherCarveEventQueue,
                InitialCarveEventQueueCapacity,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            _queuedCarveEventHead = 0;
            _queuedCarveEventCount = 0;
            return TryResolveVaultBuffer(
                vault,
                in _queuedCarveEventsHandle,
                BufferID.ShinobuDeltaCrusherCarveEventQueue,
                InitialCarveEventQueueCapacity,
                out _);
        }

        private static void EnsureCarveSignalLane()
        {
            if (_carveSignalLaneConfigured)
                return;

            SignalCorridorRuntime.EnsureInitialized();
            _carveSignalLaneConfigured = true;
        }

        private void DisposeCarveEventQueue()
        {
            DisposeCarveEventQueue(ResolveDataVault());
        }

        private void DisposeCarveEventQueue(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _queuedCarveEventsHandle, BufferID.ShinobuDeltaCrusherCarveEventQueue);
            _queuedCarveEventHead = 0;
            _queuedCarveEventCount = 0;
        }

        private bool EnsureBlackBox()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (vault.IsCompactionFenceActive)
                return false;

            _blackBoxHandle = vault.EnsureGenerationHandle<VoxelCarveTelemetryEntry>(
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

        private bool TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<VoxelCarveTelemetryEntry> blackBox)
        {
            vault = default;
            blackBox = default;
            vault = ResolveDataVault();
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in _blackBoxHandle, BufferID.ShinobuDeltaCrusherVoxelBlackBox) ||
                !TryResolveVaultBuffer(vault, in _blackBoxHandle, BufferID.ShinobuDeltaCrusherVoxelBlackBox, VoxelBlackBoxCapacity, out _) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _blackBoxHandle, SystemID.TerrainSeams, out blackBox))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (blackBox.IsCreated && blackBox.Length >= VoxelBlackBoxCapacity)
                {
                    keepLock = true;
                    return true;
                }

                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.TerrainSeams);
                    blackBox = default;
                }
            }
        }

        private void ReleaseBlackBoxBuffer(IDataVault vault)
        {
            if (vault != null && IsExactVaultHandle(in _blackBoxHandle, BufferID.ShinobuDeltaCrusherVoxelBlackBox))
                vault.ReleaseWriteLock(in _blackBoxHandle, SystemID.TerrainSeams);
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
                   !vault.IsCompactionFenceActive &&
                   IsExactVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.TerrainSeams &&
                   handle.Generation != 0u;
        }

        private bool TryAcquireQueuedCarveEventBuffer(out IDataVault vault, out NativeArray<VoxelCarveEvent> queue)
        {
            vault = default;
            queue = default;

            vault = ResolveDataVault();
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in _queuedCarveEventsHandle, BufferID.ShinobuDeltaCrusherCarveEventQueue) ||
                !TryResolveVaultBuffer(vault, in _queuedCarveEventsHandle, BufferID.ShinobuDeltaCrusherCarveEventQueue, InitialCarveEventQueueCapacity, out _) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _queuedCarveEventsHandle, SystemID.TerrainSeams, out queue))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (queue.IsCreated && queue.Length >= InitialCarveEventQueueCapacity)
                {
                    keepLock = true;
                    return true;
                }

                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _queuedCarveEventsHandle, SystemID.TerrainSeams);
                    queue = default;
                }
            }
        }

        private void ReleaseQueuedCarveEventBuffer(IDataVault vault)
        {
            if (vault != null && IsExactVaultHandle(in _queuedCarveEventsHandle, BufferID.ShinobuDeltaCrusherCarveEventQueue))
                vault.ReleaseWriteLock(in _queuedCarveEventsHandle, SystemID.TerrainSeams);
        }

        private static int ResolveQueuedCarveSlot(int head, int offset)
        {
            return (head + offset) & QueuedCarveMask;
        }

        private bool TryPopQueuedCarveEvent(NativeArray<VoxelCarveEvent> queue, out VoxelCarveEvent carveEvent)
        {
            carveEvent = default;
            if (_queuedCarveEventCount <= 0 || !queue.IsCreated || queue.Length < InitialCarveEventQueueCapacity)
                return false;

            int slot = _queuedCarveEventHead & QueuedCarveMask;
            carveEvent = queue[slot];
            queue[slot] = default;
            _queuedCarveEventHead = (_queuedCarveEventHead + 1) & QueuedCarveMask;
            _queuedCarveEventCount--;
            return true;
        }

        private bool TryPushQueuedCarveEvent(NativeArray<VoxelCarveEvent> queue, in VoxelCarveEvent carveEvent)
        {
            if (_queuedCarveEventCount >= InitialCarveEventQueueCapacity ||
                !queue.IsCreated ||
                queue.Length < InitialCarveEventQueueCapacity)
            {
                return false;
            }

            int slot = ResolveQueuedCarveSlot(_queuedCarveEventHead, _queuedCarveEventCount);
            queue[slot] = carveEvent;
            _queuedCarveEventCount++;
            return true;
        }

        private void DrainQueuedCarveEvents()
        {
            if (_queuedCarveEventCount <= 0)
                return;

            int budget = ConsumeQueuedCarveDrainBudgetThisFrame();
            if (budget <= 0 ||
                !TryAcquireQueuedCarveEventBuffer(out IDataVault vault, out NativeArray<VoxelCarveEvent> queuedCarves))
            {
                return;
            }

            ulong deferredFaultVolumeId = 0UL;
            uint deferredFaultFlags = 0u;
            try
            {
                int scanBudget = math.min(_queuedCarveEventCount, InitialCarveEventQueueCapacity);
                while (budget-- > 0 &&
                       scanBudget-- > 0 &&
                       _queuedCarveEventCount > 0 &&
                       TryPopQueuedCarveEvent(queuedCarves, out VoxelCarveEvent carveEvent))
                {
                    if (ShouldDeferQueuedCarveForFastBucket(in carveEvent))
                    {
                        if (!TryPushQueuedCarveEvent(queuedCarves, in carveEvent))
                        {
                            deferredFaultVolumeId = carveEvent.VolumeInstanceId;
                            deferredFaultFlags |= VoxelBlackBoxQueueOverflowFlag;
                        }
                        budget++;
                        continue;
                    }

                    if (TryEnqueuePendingCarveFromEvent(in carveEvent))
                        continue;

                    if (!TryPushQueuedCarveEvent(queuedCarves, in carveEvent))
                    {
                        deferredFaultVolumeId = carveEvent.VolumeInstanceId;
                        deferredFaultFlags |= VoxelBlackBoxQueueOverflowFlag;
                    }
                    break;
                }
            }
            finally
            {
                ReleaseQueuedCarveEventBuffer(vault);
            }

            if (deferredFaultFlags != 0u)
                WriteBlackBoxSample(deferredFaultVolumeId, deferredFaultFlags);
        }

        private int ResolveQueuedCarveDrainBudget()
        {
            return ResolveQueuedCarveDrainBudget(ResolveGlobalQualityWeight01());
        }

        private int ConsumeQueuedCarveDrainBudgetThisFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_queuedCarveDrainFrame != frame)
            {
                _queuedCarveDrainFrame = frame;
                float perFrame = ResolveQueuedCarveDrainBudgetPerFrame(ResolveGlobalQualityWeight01());
                float frameCap = math.ceil(perFrame);
                _queuedCarveDrainBudgetTokens = math.min(frameCap, _queuedCarveDrainBudgetTokens + perFrame);
            }

            int budget = (int)math.floor(_queuedCarveDrainBudgetTokens);
            budget = math.clamp(budget, 0, MaxQueuedCarveDrainBudgetPerFrame);
            if (budget > 0)
                _queuedCarveDrainBudgetTokens -= budget;

            return budget;
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
            if (!IsFiniteCarveEvent(in hydratedEvent) ||
                HasInvalidQueuedCarveShapeBudget(in hydratedEvent))
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

        private static float3 ToRuntimeFloat3(double3 absoluteUniversePosition)
        {
            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double3 deltaAup = absoluteUniversePosition - originAup;
            deltaAup = math.clamp(
                deltaAup,
                new double3(-RuntimeAupLocalClampMeters),
                new double3(RuntimeAupLocalClampMeters));
            float3 result;
            result.x = (float)deltaAup.x;
            result.y = (float)deltaAup.y;
            result.z = (float)deltaAup.z;
            return result;
        }

        private static Vector3 ToRuntimeVector3(double3 absoluteUniversePosition)
        {
            float3 runtimePosition = ToRuntimeFloat3(absoluteUniversePosition);
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }

        private static double3 InvalidAupCoordinate()
        {
            double3 value;
            value.x = InvalidAupCoordinateComponent;
            value.y = InvalidAupCoordinateComponent;
            value.z = InvalidAupCoordinateComponent;
            return value;
        }

        private static float3 InvalidRuntimeFloat3()
        {
            float3 value;
            value.x = InvalidRuntimeCoordinateComponent;
            value.y = InvalidRuntimeCoordinateComponent;
            value.z = InvalidRuntimeCoordinateComponent;
            return value;
        }

        private static double3 ResolveThermalMeltPositionDouble(in ThermalMeltEvent meltEvent)
        {
            if (HasAuthoritativeDoubleCoordinate(meltEvent.AbsoluteUniversePositionDouble, meltEvent.AbsoluteUniversePosition))
                return meltEvent.AbsoluteUniversePositionDouble;

            return InvalidAupCoordinate();
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

            return InvalidAupCoordinate();
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
            carveEvent.AbsoluteHitPoint = IsFiniteDouble3(absoluteHitPoint)
                ? ToRuntimeFloat3(absoluteHitPoint)
                : InvalidRuntimeFloat3();
            carveEvent.AbsoluteSegmentEnd = IsFiniteDouble3(absoluteSegmentEnd)
                ? ToRuntimeFloat3(absoluteSegmentEnd)
                : InvalidRuntimeFloat3();
        }

        private static void ClampQueuedCarveEventShapeBudget(HectonVoxelVolume volume, ref VoxelCarveEvent carveEvent)
        {
            float voxelSize = volume != null ? math.max(volume.VoxelSize, MinRuntimeVoxelSize) : MinRuntimeVoxelSize;
            if (carveEvent.RadiusMeters > 0f)
                carveEvent.RadiusMeters = ClampCarveRadiusMeters(carveEvent.RadiusMeters, voxelSize);

            if (carveEvent.BlendStrengthMeters > 0f)
                carveEvent.BlendStrengthMeters = ClampCarveBlendStrengthMeters(carveEvent.BlendStrengthMeters, voxelSize);

            if (carveEvent.Shape == (byte)VoxelCarveShapeType.Box)
            {
                carveEvent.AbsoluteHalfExtents = new float3(
                    ClampCarveExtentMeters(carveEvent.AbsoluteHalfExtents.x, voxelSize),
                    ClampCarveExtentMeters(carveEvent.AbsoluteHalfExtents.y, voxelSize),
                    ClampCarveExtentMeters(carveEvent.AbsoluteHalfExtents.z, voxelSize));
            }
        }

        private static bool HasInvalidQueuedCarveShapeBudget(in VoxelCarveEvent carveEvent)
        {
            if (carveEvent.RadiusMeters < 0f || carveEvent.BlendStrengthMeters < 0f)
                return true;

            return carveEvent.Shape == (byte)VoxelCarveShapeType.Box &&
                   math.any(carveEvent.AbsoluteHalfExtents < float3.zero);
        }

        private static VoxelCarveEvent ResolveOverflowQueuedCarveEvent(
            in VoxelCarveEvent overflowEvent,
            in VoxelCarveEvent newestEvent)
        {
            if (!CanCoalesceQueuedCarveEvent(in overflowEvent, in newestEvent))
                return newestEvent;

            double3 start = ResolveCarveHitPointDouble(in overflowEvent);
            double3 end = ResolveCarveSegmentEndDouble(in newestEvent);
            if (!math.all(math.isfinite(start)) || !math.all(math.isfinite(end)))
                return newestEvent;

            VoxelCarveEvent coalesced = newestEvent;
            coalesced.AbsoluteHitPointDouble = start;
            coalesced.AbsoluteSegmentEndDouble = end;
            coalesced.AbsoluteHitPoint = ToRuntimeFloat3(start);
            coalesced.AbsoluteSegmentEnd = ToRuntimeFloat3(end);
            coalesced.RadiusMeters = math.max(overflowEvent.RadiusMeters, newestEvent.RadiusMeters);
            coalesced.BlendStrengthMeters = math.max(overflowEvent.BlendStrengthMeters, newestEvent.BlendStrengthMeters);
            coalesced.Shape = (byte)VoxelCarveShapeType.Capsule;
            coalesced.SourceFlags = (byte)(overflowEvent.SourceFlags | newestEvent.SourceFlags);
            coalesced.AbsoluteHalfExtents = math.max(math.abs(overflowEvent.AbsoluteHalfExtents), math.abs(newestEvent.AbsoluteHalfExtents));
            coalesced.AbsoluteImpulseDirection = math.lengthsq(newestEvent.AbsoluteImpulseDirection) >= math.lengthsq(overflowEvent.AbsoluteImpulseDirection)
                ? newestEvent.AbsoluteImpulseDirection
                : overflowEvent.AbsoluteImpulseDirection;
            return coalesced;
        }

        private static bool CanCoalesceQueuedCarveEvent(in VoxelCarveEvent first, in VoxelCarveEvent second)
        {
            return first.VolumeInstanceId != 0UL &&
                   first.VolumeInstanceId == second.VolumeInstanceId &&
                   first.Operation == second.Operation &&
                   first.MaterialId == second.MaterialId &&
                   first.Shape != (byte)VoxelCarveShapeType.Box &&
                   second.Shape != (byte)VoxelCarveShapeType.Box;
        }

        private void EnqueuePendingCompactionUnchecked(in PendingCompactionRequest request)
        {
            if (!ValidatePendingCompactionQueueState() || _pendingCompactionCount >= _pendingCompactions.Length)
                return;

            int slot = ResolvePendingCompactionSlot(_pendingCompactionHead, _pendingCompactionCount);
            _pendingCompactions[slot] = request;
            _pendingCompactionCount++;
        }

        private PendingCompactionRequest PopPendingCompaction()
        {
            if (!ValidatePendingCompactionQueueState() || _pendingCompactionCount <= 0)
                return default;

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

            if (!ValidatePendingCarveQueueState())
                return;

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
            ApplyImmediateAbsoluteCrater(volume, global::Hecton8.World.AUPMath.ToDouble3(absoluteHitPoint), radius, materialId, sourceFlags, absoluteImpulseDirection);
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
                AbsoluteHitPoint = ToRuntimeFloat3(absoluteHitPoint),
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
            ApplyImmediateAbsoluteCrater(volume, global::Hecton8.World.AUPMath.ToDouble3(absoluteHitPoint), radius, materialId, CarveSourceLaser, absoluteImpulseDirection);
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
            ApplyImmediateAbsoluteBoxCrater(volume, global::Hecton8.World.AUPMath.ToDouble3(absoluteCenter), halfExtents, materialId);
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
                ClampCarveExtentMeters(halfExtents.x, volume.VoxelSize),
                ClampCarveExtentMeters(halfExtents.y, volume.VoxelSize),
                ClampCarveExtentMeters(halfExtents.z, volume.VoxelSize));
            Vector3 resolvedHalfExtents = new Vector3(
                resolvedHalfExtents3.x,
                resolvedHalfExtents3.y,
                resolvedHalfExtents3.z);
            if (resolvedHalfExtents.sqrMagnitude <= 0.0001f)
                return;

            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = ToRuntimeFloat3(absoluteCenter),
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
            ApplyImmediateAbsoluteWeld(volume, global::Hecton8.World.AUPMath.ToDouble3(absoluteHitPoint), radius, strength, materialId);
        }

        public void ApplyImmediateAbsoluteWeld(HectonVoxelVolume volume, double3 absoluteHitPoint, float radius, float strength, byte materialId = DefaultMaterialId)
        {
            if (volume == null || radius <= 0f || !volume.HasRuntimeData)
                return;

            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = ToRuntimeFloat3(absoluteHitPoint),
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
            ApplyImmediateAbsoluteCapsuleWeld(volume, global::Hecton8.World.AUPMath.ToDouble3(absoluteStart), global::Hecton8.World.AUPMath.ToDouble3(absoluteEnd), radius, strength, materialId);
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
                AbsoluteHitPoint = ToRuntimeFloat3(absoluteStart),
                AbsoluteSegmentEnd = ToRuntimeFloat3(absoluteEnd),
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
            if (_pendingDataVaultRebind)
            {
                WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
                return false;
            }

            if (volume == null || !volume.HasRuntimeData)
                return false;

            ulong volumeId = EntityId.ToULong(volume.GetEntityId());
            VoxelCarveEvent queuedEvent = carveEvent;
            queuedEvent.VolumeInstanceId = volumeId;
            NormalizeCarveEventDoubleCoordinates(ref queuedEvent);
            if (HasInvalidQueuedCarveShapeBudget(in queuedEvent))
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return false;
            }

            ClampQueuedCarveEventShapeBudget(volume, ref queuedEvent);
            if (!IsFiniteCarveEvent(in queuedEvent))
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxInvalidCarveEventFlag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DumpBlackBoxOnce(VoxelBlackBoxInvalidCarveEventFlag);
#endif
                return false;
            }

            if (!TryAcquireQueuedCarveEventBuffer(out IDataVault vault, out NativeArray<VoxelCarveEvent> queuedCarves))
                return false;

            bool queued = false;
            uint deferredFaultFlags = 0u;
            try
            {
                if (_queuedCarveEventCount >= InitialCarveEventQueueCapacity)
                {
                    if (!TryPopQueuedCarveEvent(queuedCarves, out VoxelCarveEvent overflowEvent))
                    {
                        deferredFaultFlags |= VoxelBlackBoxQueueOverflowFlag;
                    }
                    else
                    {
                        queuedEvent = ResolveOverflowQueuedCarveEvent(in overflowEvent, in queuedEvent);
                        deferredFaultFlags |= VoxelBlackBoxQueueOverflowFlag;
                    }
                }

                if (deferredFaultFlags == 0u || _queuedCarveEventCount < InitialCarveEventQueueCapacity)
                {
                    queued = TryPushQueuedCarveEvent(queuedCarves, in queuedEvent);
                    if (!queued)
                        deferredFaultFlags |= VoxelBlackBoxQueueOverflowFlag;
                }
            }
            finally
            {
                ReleaseQueuedCarveEventBuffer(vault);
            }

            if (deferredFaultFlags != 0u)
                WriteBlackBoxSample(volumeId, deferredFaultFlags);

            if (!queued)
                return false;

            SignalBus<VoxelCarveEvent>.TryPushTracked(in queuedEvent, ref s_x001VoxelDeltaProcessorSignalPushDropCount);
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
                return TryCoalesceOverflowPendingCarve(in request);

            EnqueuePendingCarveUnchecked(in request);
            return true;
        }

        public bool TryMeasureDeltaMapForVolume(HectonVoxelVolume volume, out int estimatedCellCount)
        {
            estimatedCellCount = 0;
            if (volume == null || !volume.HasRuntimeData || (_chunkStates.Count == 0 && _compactedChunkStates.Count == 0))
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
                            estimatedCellCount += ChunkCellCount;

                        if (_chunkStates.TryGetValue(address, out ChunkDeltaState state))
                            estimatedCellCount += CountDirtyCells(in state);
                    }
                }
            }

            return estimatedCellCount > 0;
        }

        public bool TryFillDeltaArrayForVolume(
            HectonVoxelVolume volume,
            NativeArray<global::VoxelModifiedCellEntry> modifiedCells,
            NativeArray<int> modifiedCellCount,
            NativeArray<int> modifiedCellBucketHeads,
            NativeArray<int> modifiedCellNext,
            int modifiedCellBucketCount)
        {
            if (!modifiedCells.IsCreated ||
                !modifiedCellCount.IsCreated ||
                modifiedCellCount.Length <= 0 ||
                !modifiedCellBucketHeads.IsCreated ||
                !modifiedCellNext.IsCreated ||
                modifiedCellBucketCount <= 0)
            {
                return false;
            }

            modifiedCellCount[0] = 0;
            int bucketCount = math.min(modifiedCellBucketCount, modifiedCellBucketHeads.Length);
            if (bucketCount <= 0 ||
                (bucketCount & (bucketCount - 1)) != 0 ||
                modifiedCellNext.Length < modifiedCells.Length)
            {
                modifiedCellCount[0] = -1;
                return false;
            }

            for (int i = 0; i < bucketCount; i++)
                modifiedCellBucketHeads[i] = -1;

            for (int i = 0; i < modifiedCells.Length; i++)
                modifiedCellNext[i] = -1;

            if (volume == null ||
                !volume.HasRuntimeData ||
                (_chunkStates.Count == 0 && _compactedChunkStates.Count == 0))
            {
                return false;
            }

            ResolveVolumeCellBounds(volume, out int3 minCell, out int3 maxCell, out int3 minChunk, out int3 maxChunk);
            bool hasModifiedCells = false;
            int writeCount = 0;

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

                                VoxelModifiedCell modifiedCell = new VoxelModifiedCell
                                {
                                    Density = BitsToHalf(compactedState.GetSdfValueBits(flatIndex)),
                                    MaterialId = compactedState.GetMaterialId(flatIndex),
                                    Flags = compactedState.GetCellFlags(flatIndex)
                                };
                                if (!TryWriteModifiedCellEntry(
                                        modifiedCells,
                                        modifiedCellBucketHeads,
                                        modifiedCellNext,
                                        bucketCount,
                                        ref writeCount,
                                        cell,
                                        in modifiedCell))
                                {
                                    modifiedCellCount[0] = -1;
                                    return false;
                                }

                                hasModifiedCells = true;
                            }
                        }

                        if (!_chunkStates.TryGetValue(address, out ChunkDeltaState state))
                            continue;

                        if (!TryResolveChunkStateStorage(
                                in state,
                                out NativeArray<uint> dirtyMaskWords,
                                out NativeArray<ushort> sdfValueBits,
                                out NativeArray<byte> materialIds,
                                out NativeArray<byte> cellFlags))
                        {
                            continue;
                        }

                        for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)
                        {
                            uint dirtyWord = dirtyMaskWords[wordIndex];
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

                                VoxelModifiedCell modifiedCell = new VoxelModifiedCell
                                {
                                    Density = BitsToHalf(sdfValueBits[flatIndex]),
                                    MaterialId = materialIds[flatIndex],
                                    Flags = cellFlags[flatIndex]
                                };
                                if (!TryWriteModifiedCellEntry(
                                        modifiedCells,
                                        modifiedCellBucketHeads,
                                        modifiedCellNext,
                                        bucketCount,
                                        ref writeCount,
                                        cell,
                                        in modifiedCell))
                                {
                                    modifiedCellCount[0] = -1;
                                    return false;
                                }

                                hasModifiedCells = true;
                            }
                        }
                    }
                }
            }

            modifiedCellCount[0] = hasModifiedCells ? writeCount : 0;
            return hasModifiedCells;
        }

        private static bool TryWriteModifiedCellEntry(
            NativeArray<global::VoxelModifiedCellEntry> modifiedCells,
            NativeArray<int> modifiedCellBucketHeads,
            NativeArray<int> modifiedCellNext,
            int modifiedCellBucketCount,
            ref int writeCount,
            int3 absoluteCell,
            in VoxelModifiedCell cell)
        {
            int safeCount = math.clamp(writeCount, 0, modifiedCells.Length);
            if (!modifiedCellBucketHeads.IsCreated ||
                !modifiedCellNext.IsCreated ||
                modifiedCellBucketCount <= 0 ||
                (modifiedCellBucketCount & (modifiedCellBucketCount - 1)) != 0 ||
                modifiedCellBucketCount > modifiedCellBucketHeads.Length ||
                modifiedCellNext.Length < modifiedCells.Length)
            {
                return false;
            }

            int bucketIndex = ResolveModifiedCellBucket(absoluteCell, modifiedCellBucketCount);
            int cursor = modifiedCellBucketHeads[bucketIndex];
            int guard = 0;
            while ((uint)cursor < (uint)safeCount && guard < safeCount)
            {
                global::VoxelModifiedCellEntry entry = modifiedCells[cursor];
                if (math.all(entry.AbsoluteCell == absoluteCell))
                {
                    entry.Cell = cell;
                    modifiedCells[cursor] = entry;
                    writeCount = safeCount;
                    return true;
                }

                cursor = modifiedCellNext[cursor];
                guard++;
            }

            if (safeCount >= modifiedCells.Length || safeCount >= modifiedCellNext.Length)
                return false;

            modifiedCells[safeCount] = new global::VoxelModifiedCellEntry
            {
                AbsoluteCell = absoluteCell,
                Cell = cell
            };
            modifiedCellNext[safeCount] = modifiedCellBucketHeads[bucketIndex];
            modifiedCellBucketHeads[bucketIndex] = safeCount;
            writeCount = safeCount + 1;
            return true;
        }

        private static int ResolveModifiedCellBucket(int3 cell, int bucketCount)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)cell.x) * 16777619u;
            hash = (hash ^ (uint)cell.y) * 16777619u;
            hash = (hash ^ (uint)cell.z) * 16777619u;
            return (int)(hash & (uint)(bucketCount - 1));
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
            data.voxelDeltaPersistence.carvingOperationCount = 0;
            data.voxelDeltaPersistence.carvingOperations ??= Array.Empty<VoxelCarvingOperationDTO>();

            for (int slot = 0; slot < _compactedChunkStates.SlotCapacity; slot++)
            {
                if (!_compactedChunkStates.TryGetSlot(slot, out ChunkAddress address, out CompactedChunkState compactedState))
                    continue;

                _chunkStates.TryGetValue(address, out ChunkDeltaState overlayState);
                WriteCompactedSaveChunk(data, address, in compactedState, in overlayState, HasChunkStateStorage(in overlayState));
            }

            for (int slot = 0; slot < _chunkStates.SlotCapacity; slot++)
            {
                if (!_chunkStates.TryGetSlot(slot, out ChunkAddress address, out ChunkDeltaState state))
                    continue;

                if (_compactedChunkStates.ContainsKey(address))
                    continue;

                WriteDirtySaveChunk(data, address, in state);
            }

            for (int i = data.voxelDeltaPersistence.chunkCount; i < data.voxelDeltaPersistence.chunks.Length; i++)
            {
                VoxelDeltaChunkDTO staleChunk = data.voxelDeltaPersistence.chunks[i];
                staleChunk.EnsureCapacity(0);
                data.voxelDeltaPersistence.chunks[i] = staleChunk;
            }
        }

        private void WriteDirtySaveChunk(SaveData data, ChunkAddress address, in ChunkDeltaState state)
        {
            int cellCount = CountDirtyCells(in state);
            if (cellCount <= 0)
                return;

            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                return;
            }

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
                chunkDto.dirtyMaskWords[i] = dirtyMaskWords[i];

            for (int i = 0; i < ChunkCellCount; i++)
            {
                chunkDto.sdfValueBits[i] = sdfValueBits[i];
                chunkDto.materialIds[i] = materialIds[i];
                chunkDto.cellFlags[i] = cellFlags[i];
            }

            chunkDto.cells = Array.Empty<VoxelDeltaCellDTO>();
            data.voxelDeltaPersistence.chunks[chunkIndex] = chunkDto;
            data.voxelDeltaPersistence.chunkCount = chunkIndex + 1;
            data.voxelDeltaPersistence.totalCellCount += cellCount;
        }

        private void WriteCompactedSaveChunk(
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

        private void ResolveCompactedMergedCell(
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
                if (TryResolveChunkStateStorage(
                        in overlayState,
                        out NativeArray<uint> overlayDirtyMaskWords,
                        out NativeArray<ushort> overlaySdfValueBits,
                        out NativeArray<byte> overlayMaterialIds,
                        out NativeArray<byte> overlayCellFlags) &&
                    IsDirty(overlayDirtyMaskWords, localIndex))
                {
                    float overlayDensity = (float)BitsToHalf(overlaySdfValueBits[flatIndex]);
                    byte overlayFlags = overlayCellFlags[flatIndex];
                    if ((overlayFlags & DeltaModeReplace) != 0)
                        density = overlayDensity;
                    else if ((overlayFlags & DeltaModeAdditive) != 0)
                        density = math.max(density, overlayDensity);
                    else
                        density = math.min(density, overlayDensity);

                    materialId = overlayMaterialIds[flatIndex];
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
            if (TryLoadFromSaveData(data, out string error) || string.IsNullOrEmpty(error))
                return;

            Hecton8.Core.H8Debug.LogError("[VoxelDeltaProcessor] " + error, this);
        }

        public bool TryLoadFromSaveData(SaveData data, out string error)
        {
            error = string.Empty;
            DisposeChunkStates();
            DisposeCompactedChunkStates();
            _chunkWriteVersions.Clear();
            _pendingRebuildVolumes.Clear();

            if (!TryValidateSaveDataForLoad(data, out string validationError))
            {
                return FailLoadedVoxelDeltaState(
                    validationError,
                    out error);
            }

            if (data == null)
                return true;

            VoxelDeltaPersistenceDTO voxelDeltaPersistence = data.voxelDeltaPersistence;
            if (voxelDeltaPersistence.chunkCount <= 0)
            {
                if (voxelDeltaPersistence.totalCellCount > 0)
                {
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta binary payload has cells without chunks.",
                        out error);
                }

                return true;
            }

            if (voxelDeltaPersistence.chunks == null ||
                voxelDeltaPersistence.chunkCount > voxelDeltaPersistence.chunks.Length)
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta chunk count exceeds available binary payload chunks.",
                    out error);
            }

            int chunkCount = voxelDeltaPersistence.chunkCount;
            int loadedCellCount = 0;
            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                VoxelDeltaChunkDTO chunk = voxelDeltaPersistence.chunks[chunkIndex];
                bool hasUniformStorage = (chunk.storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) != 0;
                bool hasDenseStorage = HasDenseStorage(in chunk);
                int denseCellCount = hasDenseStorage ? CountDirtyCells(chunk.dirtyMaskWords) : 0;
                int legacyCellCount = chunk.cells != null
                    ? math.clamp(chunk.cellCount, 0, math.min(chunk.cells.Length, ChunkCellCount))
                    : 0;

                int3 chunkCoord = new int3((int)chunk.chunkX, (int)chunk.chunkY, (int)chunk.chunkZ);
                ChunkAddress address = new ChunkAddress(chunkCoord, chunk.voxelSize);

                if (hasUniformStorage)
                {
                    if (!TryStoreCompactedChunkState(address, new CompactedChunkState(
                            chunkCoord,
                            chunk.voxelSize,
                            chunk.uniformSdfValueBits,
                            DefaultMaterialId,
                            DeltaModeReplace)))
                    {
                        return FailLoadedVoxelDeltaState(
                            "Voxel delta compacted chunk store failed while loading binary payload.",
                            out error);
                    }

                    loadedCellCount = AddNativeSnapshotDirtyCellCountClamped(loadedCellCount, VoxelDeltaChunkDTO.CellCount);
                    continue;
                }

                if (denseCellCount <= 0 && legacyCellCount <= 0)
                    continue;

                if (!TryGetOrCreateChunkState(chunkCoord, chunk.voxelSize, out ChunkDeltaState state))
                {
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta dirty chunk pool exhausted while loading binary payload.",
                        out error);
                }

                if (!TryResolveChunkStateStorage(
                        in state,
                        out NativeArray<uint> dirtyMaskWords,
                        out NativeArray<ushort> sdfValueBits,
                        out NativeArray<byte> materialIds,
                        out NativeArray<byte> cellFlags))
                {
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta chunk storage is unavailable while loading binary payload.",
                        out error);
                }

                if (hasDenseStorage && denseCellCount > 0)
                {
                    for (int i = 0; i < ChunkDirtyMaskWordCount; i++)
                        dirtyMaskWords[i] = chunk.dirtyMaskWords[i];

                    for (int i = 0; i < ChunkCellCount; i++)
                    {
                        sdfValueBits[i] = chunk.sdfValueBits[i];
                        materialIds[i] = chunk.materialIds[i];
                        byte sourceFlags = chunk.cellFlags != null && chunk.cellFlags.Length == ChunkCellCount
                            ? chunk.cellFlags[i]
                            : (byte)0;
                        cellFlags[i] = SanitizeVoxelDeltaCellFlags(sourceFlags);
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

                        if (!SetCell(
                                ref state,
                                localIndex,
                                ClampToHalf(cell.sdfValue),
                                cell.materialId,
                                SanitizeVoxelDeltaCellFlags(cell.flags)))
                        {
                            return FailLoadedVoxelDeltaState(
                                "Voxel delta legacy cell store failed while loading binary payload.",
                                out error);
                        }
                    }
                }

                loadedCellCount = AddNativeSnapshotDirtyCellCountClamped(loadedCellCount, state.DirtyCellCount);
                if (!TryStoreChunkState(address, in state))
                {
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta dirty chunk store failed while loading binary payload.",
                        out error);
                }
            }

            if (loadedCellCount != voxelDeltaPersistence.totalCellCount)
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta binary payload total cell count mismatch.",
                    out error);
            }

            for (int i = 0; i < _registeredVolumes.Count; i++)
            {
                HectonVoxelVolume volume = _registeredVolumes[i];
                if (volume != null && HasOverlappingDelta(volume))
                    volume.RequestDeltaRebuild();
            }

            return true;
        }

        internal static bool TryValidateSaveDataForLoad(SaveData data, out string error)
        {
            error = string.Empty;
            if (data == null)
                return true;

            VoxelDeltaPersistenceDTO voxelDeltaPersistence = data.voxelDeltaPersistence;
            if (voxelDeltaPersistence.chunkCount <= 0)
            {
                if (voxelDeltaPersistence.totalCellCount > 0)
                {
                    error = "Voxel delta binary payload has cells without chunks.";
                    return false;
                }

                return true;
            }

            if (voxelDeltaPersistence.chunks == null ||
                voxelDeltaPersistence.chunkCount > voxelDeltaPersistence.chunks.Length)
            {
                error = "Voxel delta chunk count exceeds available binary payload chunks.";
                return false;
            }

            int loadedCellCount = 0;
            uint[] legacyDirtyMaskScratch = null;
            for (int chunkIndex = 0; chunkIndex < voxelDeltaPersistence.chunkCount; chunkIndex++)
            {
                VoxelDeltaChunkDTO chunk = voxelDeltaPersistence.chunks[chunkIndex];
                if (!IsSupportedVoxelDeltaChunkCoordinate(chunk.chunkX) ||
                    !IsSupportedVoxelDeltaChunkCoordinate(chunk.chunkY) ||
                    !IsSupportedVoxelDeltaChunkCoordinate(chunk.chunkZ))
                {
                    error = "Voxel delta binary payload chunk coordinate is outside the supported range.";
                    return false;
                }

                if (!math.isfinite(chunk.voxelSize) || chunk.voxelSize <= 0f)
                {
                    error = "Voxel delta binary payload chunk has invalid voxel size.";
                    return false;
                }

                if ((chunk.storageFlags & ~VoxelDeltaChunkDTO.SupportedStorageFlags) != 0)
                {
                    error = "Voxel delta binary payload chunk has unsupported storage flags.";
                    return false;
                }

                if ((chunk.storageFlags & VoxelDeltaChunkDTO.StorageUniformSdfRle) != 0)
                {
                    loadedCellCount = AddNativeSnapshotDirtyCellCountClamped(
                        loadedCellCount,
                        VoxelDeltaChunkDTO.CellCount);
                    continue;
                }

                if (HasDenseStorage(in chunk))
                {
                    loadedCellCount = AddNativeSnapshotDirtyCellCountClamped(
                        loadedCellCount,
                        CountDirtyCells(chunk.dirtyMaskWords));
                    continue;
                }

                if (chunk.cells == null)
                    continue;

                int legacyCellCount = math.clamp(
                    chunk.cellCount,
                    0,
                    math.min(chunk.cells.Length, ChunkCellCount));
                if (legacyCellCount <= 0)
                    continue;

                legacyDirtyMaskScratch ??= new uint[ChunkDirtyMaskWordCount];
                Array.Clear(legacyDirtyMaskScratch, 0, legacyDirtyMaskScratch.Length);
                int3 chunkCoord = new int3((int)chunk.chunkX, (int)chunk.chunkY, (int)chunk.chunkZ);
                int appliedLegacyCellCount = 0;
                for (int cellIndex = 0; cellIndex < legacyCellCount; cellIndex++)
                {
                    VoxelDeltaCellDTO cell = chunk.cells[cellIndex];
                    int3 absoluteCell = MortonDecodeSigned(cell.universeKey);
                    if (!TryComputeLocalCellIndex(absoluteCell, chunkCoord, out uint localIndex))
                        continue;

                    int wordIndex = (int)(localIndex >> 5);
                    uint bitMask = 1u << ((int)localIndex & 31);
                    if ((legacyDirtyMaskScratch[wordIndex] & bitMask) != 0u)
                        continue;

                    legacyDirtyMaskScratch[wordIndex] |= bitMask;
                    appliedLegacyCellCount++;
                }

                loadedCellCount = AddNativeSnapshotDirtyCellCountClamped(
                    loadedCellCount,
                    appliedLegacyCellCount);
            }

            if (loadedCellCount != voxelDeltaPersistence.totalCellCount)
            {
                error = "Voxel delta binary payload total cell count mismatch.";
                return false;
            }

            return true;
        }

        private bool FailLoadedVoxelDeltaState(string message, out string error)
        {
            error = message;
            WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
            ClearLoadedVoxelDeltaStateAfterFailedLoad();
            return false;
        }

        private void ClearLoadedVoxelDeltaStateAfterFailedLoad()
        {
            DisposeChunkStates();
            DisposeCompactedChunkStates();
            _chunkWriteVersions.Clear();
            _pendingRebuildVolumes.Clear();
        }

        public bool TryMeasureNativeSnapshotByteCount(out int byteCount)
        {
            if (!TryMeasureNativeSnapshot(out NativeSnapshotWriteStats stats))
            {
                byteCount = 0;
                return false;
            }

            byteCount = stats.TotalBytes;
            return byteCount > 0;
        }

        public unsafe bool TryCopyNativeSnapshotToBorrowedScratch(out NativeArray<byte> snapshot, out int bytesWritten)
        {
            snapshot = default;
            bytesWritten = 0;

            if (!TryMeasureNativeSnapshot(out NativeSnapshotWriteStats stats) ||
                stats.TotalBytes <= 0 ||
                stats.ChunkCount <= 0)
            {
                return false;
            }

            bytesWritten = stats.TotalBytes;
            if (_nativeSnapshotScratchLeaseCount > 0)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                return false;
            }

            EnsureNativeSnapshotScratchBuffer();
            if (!TryResolveNativeSnapshotScratch(out NativeArray<byte> nativeSnapshotScratch) ||
                stats.TotalBytes > nativeSnapshotScratch.Length)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                return false;
            }

            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nativeSnapshotScratch);
            if (!TryCopyNativeSnapshot(destinationPtr, stats.TotalBytes, out int copiedBytes) ||
                copiedBytes != stats.TotalBytes)
            {
                bytesWritten = stats.TotalBytes;
                return false;
            }

            bytesWritten = copiedBytes;
            snapshot = nativeSnapshotScratch.GetSubArray(0, bytesWritten);
            _nativeSnapshotScratchLeaseCount++;
            _nativeSnapshotScratchDisposeDeferred = false;
            return true;
        }

        public void ReleaseBorrowedNativeSnapshotScratch()
        {
            if (_nativeSnapshotScratchLeaseCount <= 0)
            {
                _nativeSnapshotScratchLeaseCount = 0;
                return;
            }

            _nativeSnapshotScratchLeaseCount--;
            if (_nativeSnapshotScratchLeaseCount == 0 && _nativeSnapshotScratchDisposeDeferred)
                DisposeNativeSnapshotScratchBuffer();
        }

        public unsafe bool TryCopyNativeSnapshot(void* destinationPtr, int destinationByteCapacity, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destinationPtr == null || destinationByteCapacity <= 0)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                return false;
            }

            if (!TryMeasureNativeSnapshot(out NativeSnapshotWriteStats stats) ||
                stats.TotalBytes <= 0 ||
                stats.ChunkCount <= 0 ||
                destinationByteCapacity < stats.TotalBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                return false;
            }

            byte* snapshotPtr = (byte*)destinationPtr;
            int cursor = 0;

            NativeSnapshotHeader header = new NativeSnapshotHeader
            {
                Version = NativeSnapshotDeltaRleAlignedMagic,
                ChunkCount = stats.ChunkCount,
                TotalDirtyCellCount = stats.TotalDirtyCellCount,
                Reserved0 = 0
            };

            UnsafeUtility.CopyStructureToPtr(ref header, snapshotPtr);
            cursor += UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            for (int slot = 0; slot < _compactedChunkStates.SlotCapacity; slot++)
            {
                if (!_compactedChunkStates.TryGetSlot(slot, out ChunkAddress address, out CompactedChunkState compactedState))
                    continue;

                _chunkStates.TryGetValue(address, out ChunkDeltaState overlayState);
                bool hasOverlay = HasChunkStateStorage(in overlayState);
                if (IsUniformSdfRleSnapshotEligible(compactedState, hasOverlay))
                {
                    WriteUniformSdfRleNativeSnapshotChunk(snapshotPtr, stats.TotalBytes, ref cursor, address, in compactedState);
                }
                else
                {
                    int sparsePayloadBytes = CountCompactedSparseRuns(in compactedState, in overlayState, hasOverlay) * UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
                    if (ShouldUseDenseDeltaSnapshot(sparsePayloadBytes))
                    {
                        WriteCompactedDenseDeltaNativeSnapshotChunk(
                            snapshotPtr,
                            stats.TotalBytes,
                            ref cursor,
                            address,
                            in compactedState,
                            in overlayState,
                            hasOverlay);
                    }
                    else
                    {
                        WriteCompactedSparseRleNativeSnapshotChunk(
                            snapshotPtr,
                            stats.TotalBytes,
                            ref cursor,
                            address,
                            in compactedState,
                            in overlayState,
                            hasOverlay);
                    }
                }
            }

            for (int slot = 0; slot < _chunkStates.SlotCapacity; slot++)
            {
                if (!_chunkStates.TryGetSlot(slot, out ChunkAddress address, out ChunkDeltaState state))
                    continue;

                if (_compactedChunkStates.ContainsKey(address))
                    continue;

                int dirtyCellCount = CountDirtyCells(in state);
                if (dirtyCellCount <= 0)
                    continue;

                int sparsePayloadBytes = CountSparseDirtyRuns(in state) * UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
                if (ShouldUseDenseDeltaSnapshot(sparsePayloadBytes))
                    WriteDirtyDenseDeltaNativeSnapshotChunk(snapshotPtr, stats.TotalBytes, ref cursor, address, in state, dirtyCellCount);
                else
                    WriteDirtySparseRleNativeSnapshotChunk(snapshotPtr, stats.TotalBytes, ref cursor, address, in state, dirtyCellCount);
            }

            if (cursor != stats.TotalBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                return false;
            }

            bytesWritten = cursor;
            return true;
        }

        private bool TryMeasureNativeSnapshot(out NativeSnapshotWriteStats stats)
        {
            stats = default;
            if (_chunkStates.Count <= 0 && _compactedChunkStates.Count <= 0)
                return false;

            int chunkCount = 0;
            int totalDirtyCellCount = 0;
            int deltaChunkHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            int runBytes = UnsafeUtility.SizeOf<SaveVoxelDeltaRun8>();
            int totalBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();

            for (int slot = 0; slot < _compactedChunkStates.SlotCapacity; slot++)
            {
                if (!_compactedChunkStates.TryGetSlot(slot, out ChunkAddress address, out CompactedChunkState compactedState))
                    continue;

                _chunkStates.TryGetValue(address, out ChunkDeltaState overlayState);
                bool hasOverlay = HasChunkStateStorage(in overlayState);
                chunkCount++;
                totalDirtyCellCount += ChunkCellCount;
                if (IsUniformSdfRleSnapshotEligible(compactedState, hasOverlay))
                {
                    totalBytes += deltaChunkHeaderBytes + AlignSnapshotPayloadBytes4(NativeSnapshotUniformSdfRlePayloadBytes);
                }
                else
                {
                    int sparsePayloadBytes = CountCompactedSparseRuns(in compactedState, in overlayState, hasOverlay) * runBytes;
                    int payloadBytes = ShouldUseDenseDeltaSnapshot(sparsePayloadBytes)
                        ? GetNativeSnapshotDensePayloadBytes()
                        : sparsePayloadBytes;
                    totalBytes += deltaChunkHeaderBytes + AlignSnapshotPayloadBytes4(payloadBytes);
                }
            }

            for (int slot = 0; slot < _chunkStates.SlotCapacity; slot++)
            {
                if (!_chunkStates.TryGetSlot(slot, out ChunkAddress address, out ChunkDeltaState state))
                    continue;

                if (_compactedChunkStates.ContainsKey(address))
                    continue;

                int cellCount = CountDirtyCells(in state);
                if (cellCount <= 0)
                    continue;

                int runCount = CountSparseDirtyRuns(in state);
                if (runCount <= 0)
                    continue;

                chunkCount++;
                totalDirtyCellCount += cellCount;
                int sparsePayloadBytes = runCount * runBytes;
                int payloadBytes = ShouldUseDenseDeltaSnapshot(sparsePayloadBytes)
                    ? GetNativeSnapshotDensePayloadBytes()
                    : sparsePayloadBytes;
                totalBytes += deltaChunkHeaderBytes + AlignSnapshotPayloadBytes4(payloadBytes);
            }

            if (chunkCount <= 0)
                return false;

            stats.TotalBytes = totalBytes;
            stats.ChunkCount = chunkCount;
            stats.TotalDirtyCellCount = totalDirtyCellCount;
            stats.Reserved0 = 0;
            return true;
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

        private static int GetNativeSnapshotDensePayloadBytes()
        {
            return (ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<ushort>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>())
                + (ChunkCellCount * UnsafeUtility.SizeOf<byte>());
        }

        private static bool ShouldUseDenseDeltaSnapshot(int sparsePayloadBytes)
        {
            int runBytes = SaveDeltaCompressionLayout.SaveVoxelDeltaRun8StrideBytes;
            int sparseRunCount = runBytes <= 0 ? int.MaxValue : (sparsePayloadBytes + runBytes - 1) / runBytes;
            return sparsePayloadBytes > GetNativeSnapshotDensePayloadBytes() ||
                   sparseRunCount > MaxSparseDeltaRunsPerPagerPayload;
        }

        private static int ResolveNativeSnapshotScratchCapacityBytes()
        {
            int snapshotHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
            int chunkHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            int denseChunkBytes = chunkHeaderBytes + AlignSnapshotPayloadBytes4(GetNativeSnapshotDensePayloadBytes());
            int uniformChunkBytes = chunkHeaderBytes + AlignSnapshotPayloadBytes4(NativeSnapshotUniformSdfRlePayloadBytes);
            int dirtyPoolWorstCaseBytes = DirtyChunkStatePoolCapacity * denseChunkBytes;
            int compactedUniformWorstCaseBytes = InitialChunkRegistryCapacity * uniformChunkBytes;
            int capacity = snapshotHeaderBytes + dirtyPoolWorstCaseBytes + compactedUniformWorstCaseBytes;
            return math.min(capacity, SaveBinaryStorage.RawPayloadCapacityBytes);
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

        private int CountSparseDirtyRuns(in ChunkDeltaState state)
        {
            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                return 0;
            }

            int runCount = 0;
            int index = 0;
            while (index < ChunkCellCount)
            {
                if (!IsDirty(dirtyMaskWords, (uint)index))
                {
                    index++;
                    continue;
                }

                runCount++;
                sbyte sdfValue = QuantizeSdfByte(sdfValueBits[index]);
                byte materialId = materialIds[index];
                byte flags = cellFlags[index];
                index++;
                while (index < ChunkCellCount &&
                       IsDirty(dirtyMaskWords, (uint)index) &&
                       QuantizeSdfByte(sdfValueBits[index]) == sdfValue &&
                       materialIds[index] == materialId &&
                       cellFlags[index] == flags)
                {
                    index++;
                }
            }

            return runCount;
        }

        private int CountCompactedSparseRuns(
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

        private unsafe void WriteDirtySparseRleNativeSnapshotChunk(
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

            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int cellIndex = 0;
            while (cellIndex < ChunkCellCount)
            {
                if (!IsDirty(dirtyMaskWords, (uint)cellIndex))
                {
                    cellIndex++;
                    continue;
                }

                int startIndex = cellIndex;
                ushort sdfBits = sdfValueBits[cellIndex];
                sbyte sdfValue = QuantizeSdfByte(sdfBits);
                byte materialId = materialIds[cellIndex];
                byte flags = cellFlags[cellIndex];
                cellIndex++;
                while (cellIndex < ChunkCellCount &&
                       IsDirty(dirtyMaskWords, (uint)cellIndex) &&
                       QuantizeSdfByte(sdfValueBits[cellIndex]) == sdfValue &&
                       materialIds[cellIndex] == materialId &&
                       cellFlags[cellIndex] == flags)
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

        private unsafe void WriteDirtyDenseDeltaNativeSnapshotChunk(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            ChunkAddress address,
            in ChunkDeltaState state,
            int dirtyCellCount)
        {
            int headerBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            int payloadBytes = GetNativeSnapshotDensePayloadBytes();
            if (dirtyCellCount <= 0 || cursor > snapshotLength - headerBytes - payloadBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int headerCursor = cursor;
            cursor += headerBytes;
            int payloadCursor = cursor;

            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int dirtyMaskBytes = ChunkDirtyMaskWordCount * UnsafeUtility.SizeOf<uint>();
            void* dirtyMaskPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dirtyMaskWords);
            UnsafeUtility.MemCpy(snapshotPtr + cursor, dirtyMaskPtr, dirtyMaskBytes);
            cursor += dirtyMaskBytes;

            int sdfBytes = ChunkCellCount * UnsafeUtility.SizeOf<ushort>();
            void* sdfPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sdfValueBits);
            UnsafeUtility.MemCpy(snapshotPtr + cursor, sdfPtr, sdfBytes);
            cursor += sdfBytes;

            int materialBytes = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            void* materialPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(materialIds);
            UnsafeUtility.MemCpy(snapshotPtr + cursor, materialPtr, materialBytes);
            cursor += materialBytes;

            int flagsBytes = ChunkCellCount * UnsafeUtility.SizeOf<byte>();
            void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(cellFlags);
            UnsafeUtility.MemCpy(snapshotPtr + cursor, flagsPtr, flagsBytes);
            cursor += flagsBytes;

            ulong payloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes);
            NativeSnapshotChunkHeaderDeltaRle chunkHeader = new NativeSnapshotChunkHeaderDeltaRle
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = dirtyCellCount,
                StorageFlags = NativeSnapshotStorageDense,
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

        private unsafe void WriteCompactedDenseDeltaNativeSnapshotChunk(
            byte* snapshotPtr,
            int snapshotLength,
            ref int cursor,
            ChunkAddress address,
            in CompactedChunkState compactedState,
            in ChunkDeltaState overlayState,
            bool hasOverlay)
        {
            int headerBytes = UnsafeUtility.SizeOf<NativeSnapshotChunkHeaderDeltaRle>();
            int payloadBytes = GetNativeSnapshotDensePayloadBytes();
            if (cursor > snapshotLength - headerBytes - payloadBytes)
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));
                cursor = snapshotLength;
                return;
            }

            int headerCursor = cursor;
            cursor += headerBytes;
            int payloadCursor = cursor;

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

            for (int i = 0; i < ChunkCellCount; i++)
            {
                ResolveCompactedMergedCell(in compactedState, in overlayState, hasOverlay, i, out ushort sdfBits, out byte materialId);
                UnsafeUtility.WriteArrayElement(snapshotPtr + sdfCursor, i, sdfBits);
                UnsafeUtility.WriteArrayElement(snapshotPtr + materialCursor, i, materialId);
                UnsafeUtility.WriteArrayElement(snapshotPtr + flagsCursor, i, DeltaModeReplace);
            }

            cursor += sdfBytes + materialBytes + flagsBytes;
            ulong payloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + payloadCursor, payloadBytes);
            NativeSnapshotChunkHeaderDeltaRle chunkHeader = new NativeSnapshotChunkHeaderDeltaRle
            {
                ChunkX = address.ChunkCoord.x,
                ChunkY = address.ChunkCoord.y,
                ChunkZ = address.ChunkCoord.z,
                VoxelSize = address.VoxelSize,
                DirtyCellCount = ChunkCellCount,
                StorageFlags = NativeSnapshotStorageDense,
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

        private unsafe void WriteCompactedSparseRleNativeSnapshotChunk(
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

        private unsafe void WriteCompactedNativeSnapshotChunk(
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


        private unsafe bool TryLoadNativeSnapshotChunk(
            NativeArray<byte> snapshot,
            byte* snapshotPtr,
            bool snapshotHasDeltaRle,
            bool snapshotHasAlignedHeaders,
            bool snapshotHasRleChunks,
            bool snapshotHasFlags,
            int chunkHeaderBytes,
            int dirtyMaskByteLength,
            int sdfByteLength,
            int materialByteLength,
            int flagsByteLength,
            ref int cursor,
            ref int loadedDirtyCellCount,
            out string error)
        {
            error = string.Empty;
            if (cursor > snapshot.Length - chunkHeaderBytes)
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta chunk header exceeds the snapshot bounds.",
                    out error);
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

            if (!math.isfinite(chunkHeader.VoxelSize) ||
                chunkHeader.VoxelSize <= 0f ||
                chunkHeader.DirtyCellCount < 0)
            {
                if (snapshotHasDeltaRle)
                    ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);

                return FailLoadedVoxelDeltaState(
                    "Voxel delta chunk header contains invalid values.",
                    out error);
            }

            if (snapshotHasRleChunks && !IsSupportedNativeSnapshotStorageFlags(storageFlags))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta snapshot storage flags are outside the supported range.",
                    out error);
            }

            int chunkPayloadBytes = dirtyMaskByteLength + sdfByteLength + materialByteLength + (snapshotHasFlags ? flagsByteLength : 0);
            int3 chunkCoord = new int3(chunkHeader.ChunkX, chunkHeader.ChunkY, chunkHeader.ChunkZ);
            ChunkAddress address = new ChunkAddress(chunkCoord, chunkHeader.VoxelSize);

            if (snapshotHasDeltaRle)
            {
                if (declaredPayloadBytes < 0 || cursor > snapshot.Length - declaredPayloadBytes)
                {
                    ReportVoxelDeltaChunkCorruption(SaveCorruptionBoundsAction, chunkHeader.DirtyCellCount);
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta delta-RLE payload exceeds the snapshot bounds.",
                        out error);
                }

                ulong computedPayloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + cursor, declaredPayloadBytes);
                if (computedPayloadHash64 != declaredPayloadHash64)
                {
                    ReportVoxelDeltaChunkCorruption(SaveCorruptionHashMismatchAction, chunkHeader.DirtyCellCount);
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta delta-RLE payload hash mismatch.",
                        out error);
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
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);

                    return FailLoadedVoxelDeltaState(
                        "Voxel delta RLE payload is invalid.",
                        out error);
                }

                ushort sdfBits = hasCurrentUniformPayload
                    ? DequantizeSdfByte((sbyte)(*(snapshotPtr + cursor)))
                    : UnsafeUtility.ReadArrayElement<ushort>(snapshotPtr + cursor, 0);
                cursor += declaredPayloadBytes;
                if (snapshotHasAlignedHeaders)
                    cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);

                if (!TryStoreCompactedChunkState(address, new CompactedChunkState(
                        chunkCoord,
                        chunkHeader.VoxelSize,
                        sdfBits,
                        DefaultMaterialId,
                        DeltaModeReplace)))
                {
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta compacted chunk store failed while loading uniform payload.",
                        out error);
                }
                loadedDirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                    loadedDirtyCellCount,
                    chunkHeader.DirtyCellCount);
                return true;
            }

            if ((storageFlags & NativeSnapshotStorageSparseDeltaRle) != 0)
            {
                if (declaredPayloadBytes < 0 || cursor > snapshot.Length - declaredPayloadBytes)
                {
                    if (snapshotHasDeltaRle)
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionBoundsAction, chunkHeader.DirtyCellCount);

                    return FailLoadedVoxelDeltaState(
                        "Voxel delta sparse RLE payload exceeds the snapshot bounds.",
                        out error);
                }

                if (!TryLoadSparseRlePayload(
                        snapshotPtr + cursor,
                        declaredPayloadBytes,
                        chunkCoord,
                        chunkHeader.VoxelSize,
                        chunkHeader.DirtyCellCount,
                        address))
                {
                    if (snapshotHasDeltaRle)
                        ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);

                    return FailLoadedVoxelDeltaState(
                        "Voxel delta sparse RLE payload is invalid.",
                        out error);
                }

                cursor += declaredPayloadBytes;
                if (snapshotHasAlignedHeaders)
                    cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
                loadedDirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                    loadedDirtyCellCount,
                    chunkHeader.DirtyCellCount);
                return true;
            }

            if (snapshotHasRleChunks && declaredPayloadBytes != chunkPayloadBytes)
            {
                if (snapshotHasDeltaRle)
                    ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);

                return FailLoadedVoxelDeltaState(
                    "Voxel delta dense payload length mismatch.",
                    out error);
            }

            if (cursor > snapshot.Length - chunkPayloadBytes)
            {
                if (snapshotHasDeltaRle)
                    ReportVoxelDeltaChunkCorruption(SaveCorruptionBoundsAction, chunkHeader.DirtyCellCount);

                return FailLoadedVoxelDeltaState(
                    "Voxel delta chunk payload exceeds the snapshot bounds.",
                    out error);
            }

            int denseDirtyCellCount = CountNativeSnapshotDirtyMaskBits(snapshotPtr + cursor);
            if (denseDirtyCellCount != chunkHeader.DirtyCellCount)
            {
                if (snapshotHasDeltaRle)
                    ReportVoxelDeltaChunkCorruption(SaveCorruptionMalformedRleAction, chunkHeader.DirtyCellCount);

                return FailLoadedVoxelDeltaState(
                    "Voxel delta dense dirty-mask count does not match the chunk header.",
                    out error);
            }

            if (!TryGetOrCreateChunkState(chunkCoord, chunkHeader.VoxelSize, out ChunkDeltaState state))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta dirty chunk pool exhausted while loading dense payload.",
                    out error);
            }

            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta chunk storage is unavailable.",
                    out error);
            }

            void* dirtyMaskPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dirtyMaskWords);
            if (!UnsafeMemoryCopyGuard.SafeCopy(dirtyMaskPtr, dirtyMaskWords.Length * UnsafeUtility.SizeOf<uint>(), snapshotPtr + cursor, dirtyMaskByteLength))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta dirty-mask copy exceeded destination bounds.",
                    out error);
            }
            cursor += dirtyMaskByteLength;

            void* sdfPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sdfValueBits);
            if (!UnsafeMemoryCopyGuard.SafeCopy(sdfPtr, sdfValueBits.Length * UnsafeUtility.SizeOf<ushort>(), snapshotPtr + cursor, sdfByteLength))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta SDF copy exceeded destination bounds.",
                    out error);
            }
            cursor += sdfByteLength;

            void* materialPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(materialIds);
            if (!UnsafeMemoryCopyGuard.SafeCopy(materialPtr, materialIds.Length * UnsafeUtility.SizeOf<byte>(), snapshotPtr + cursor, materialByteLength))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta material copy exceeded destination bounds.",
                    out error);
            }
            cursor += materialByteLength;

            if (snapshotHasFlags)
            {
                void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(cellFlags);
                if (!UnsafeMemoryCopyGuard.SafeCopy(flagsPtr, cellFlags.Length * UnsafeUtility.SizeOf<byte>(), snapshotPtr + cursor, flagsByteLength))
                {
                    return FailLoadedVoxelDeltaState(
                        "Voxel delta flag copy exceeded destination bounds.",
                        out error);
                }
                cursor += flagsByteLength;
            }
            else
            {
                void* flagsPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(cellFlags);
                UnsafeUtility.MemClear(flagsPtr, flagsByteLength);
            }

            state.DirtyCellCount = chunkHeader.DirtyCellCount;
            if (!TryStoreChunkState(address, in state))
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta dirty chunk store failed while loading dense payload.",
                    out error);
            }
            loadedDirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                loadedDirtyCellCount,
                chunkHeader.DirtyCellCount);
            if (snapshotHasAlignedHeaders)
                cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
            return true;
        }
        public unsafe bool TryLoadNativeSnapshot(NativeArray<byte> snapshot, out string error)
        {
            error = string.Empty;

            if (!TryValidateNativeSnapshotForLoad(snapshot, out error))
                return false;

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
                return FailLoadedVoxelDeltaState(
                    "Voxel delta snapshot is truncated.",
                    out error);
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
                        return FailLoadedVoxelDeltaState(
                            "Voxel delta aligned snapshot header is truncated.",
                            out error);
                    }

                    header = UnsafeUtility.ReadArrayElement<NativeSnapshotHeader>(snapshotPtr, 0);
                    minimumHeaderBytes = UnsafeUtility.SizeOf<NativeSnapshotHeader>();
                }
                else
                {
                    if (snapshot.Length < NativeSnapshotVersionedHeaderBytes)
                    {
                        return FailLoadedVoxelDeltaState(
                            "Voxel delta versioned snapshot header is truncated.",
                            out error);
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
                return FailLoadedVoxelDeltaState(
                    "Voxel delta snapshot header is invalid.",
                    out error);
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

            for (int chunkIndex = 0; chunkIndex < header.ChunkCount; chunkIndex++)
            {
                if (!TryLoadNativeSnapshotChunk(
                        snapshot,
                        snapshotPtr,
                        snapshotHasDeltaRle,
                        snapshotHasAlignedHeaders,
                        snapshotHasRleChunks,
                        snapshotHasFlags,
                        chunkHeaderBytes,
                        dirtyMaskByteLength,
                        sdfByteLength,
                        materialByteLength,
                        flagsByteLength,
                        ref cursor,
                        ref loadedDirtyCellCount,
                        out error))
                {
                    return false;
                }
            }

            if (cursor != snapshot.Length)
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta snapshot contains unread trailing bytes.",
                    out error);
            }

            if (loadedDirtyCellCount != header.TotalDirtyCellCount)
            {
                return FailLoadedVoxelDeltaState(
                    "Voxel delta snapshot dirty-cell count does not match the header.",
                    out error);
            }

            RequestRebuildsForLoadedState();
            return true;
        }

        internal static bool TryValidateNativeSnapshotForLoad(NativeArray<byte> snapshot, out string error)
        {
            unsafe
            {
                return TryValidateNativeSnapshotForLoadUnsafe(snapshot, out error);
            }
        }

        private static unsafe bool TryValidateNativeSnapshotForLoadUnsafe(NativeArray<byte> snapshot, out string error)
        {
            error = string.Empty;
            if (!snapshot.IsCreated || snapshot.Length <= 0)
                return true;

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

                if (!math.isfinite(chunkHeader.VoxelSize) ||
                    chunkHeader.VoxelSize <= 0f ||
                    chunkHeader.DirtyCellCount < 0)
                {
                    error = "Voxel delta chunk header contains invalid values.";
                    return false;
                }

                if (snapshotHasRleChunks && !IsSupportedNativeSnapshotStorageFlags(storageFlags))
                {
                    error = "Voxel delta snapshot storage flags are outside the supported range.";
                    return false;
                }

                int chunkPayloadBytes = dirtyMaskByteLength + sdfByteLength + materialByteLength + (snapshotHasFlags ? flagsByteLength : 0);
                if (snapshotHasDeltaRle)
                {
                    if (declaredPayloadBytes < 0 || cursor > snapshot.Length - declaredPayloadBytes)
                    {
                        error = "Voxel delta delta-RLE payload exceeds the snapshot bounds.";
                        return false;
                    }

                    ulong computedPayloadHash64 = SaveBinaryStorage.Hash64(snapshotPtr + cursor, declaredPayloadBytes);
                    if (computedPayloadHash64 != declaredPayloadHash64)
                    {
                        error = "Voxel delta delta-RLE payload hash mismatch.";
                        return false;
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
                        error = "Voxel delta RLE payload is invalid.";
                        return false;
                    }

                    cursor += declaredPayloadBytes;
                    if (snapshotHasAlignedHeaders)
                        cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
                    loadedDirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                        loadedDirtyCellCount,
                        chunkHeader.DirtyCellCount);
                    continue;
                }

                if ((storageFlags & NativeSnapshotStorageSparseDeltaRle) != 0)
                {
                    if (declaredPayloadBytes < 0 || cursor > snapshot.Length - declaredPayloadBytes)
                    {
                        error = "Voxel delta sparse RLE payload exceeds the snapshot bounds.";
                        return false;
                    }

                    if (!TryValidateSparseRlePayload(
                            snapshotPtr + cursor,
                            declaredPayloadBytes,
                            chunkHeader.DirtyCellCount))
                    {
                        error = "Voxel delta sparse RLE payload is invalid.";
                        return false;
                    }

                    cursor += declaredPayloadBytes;
                    if (snapshotHasAlignedHeaders)
                        cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
                    loadedDirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                        loadedDirtyCellCount,
                        chunkHeader.DirtyCellCount);
                    continue;
                }

                if (snapshotHasRleChunks && declaredPayloadBytes != chunkPayloadBytes)
                {
                    error = "Voxel delta dense payload length mismatch.";
                    return false;
                }

                if (cursor > snapshot.Length - chunkPayloadBytes)
                {
                    error = "Voxel delta chunk payload exceeds the snapshot bounds.";
                    return false;
                }

                int denseDirtyCellCount = CountNativeSnapshotDirtyMaskBits(snapshotPtr + cursor);
                if (denseDirtyCellCount != chunkHeader.DirtyCellCount)
                {
                    error = "Voxel delta dense dirty-mask count does not match the chunk header.";
                    return false;
                }

                cursor += chunkPayloadBytes;
                if (snapshotHasAlignedHeaders)
                    cursor = AlignSnapshotCursor4Clamped(cursor, snapshot.Length);
                loadedDirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                    loadedDirtyCellCount,
                    chunkHeader.DirtyCellCount);
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

            return true;
        }

        private static unsafe int CountNativeSnapshotDirtyMaskBits(byte* dirtyMaskPtr)
        {
            int dirtyCellCount = 0;
            for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)
            {
                uint word = UnsafeUtility.ReadArrayElement<uint>(dirtyMaskPtr, wordIndex);
                dirtyCellCount = AddNativeSnapshotDirtyCellCountClamped(
                    dirtyCellCount,
                    math.countbits(word));
            }

            return dirtyCellCount;
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

            ChunkDeltaState state;
            if (_chunkStates.TryGetValue(address, out ChunkDeltaState existingState))
            {
                state = existingState;
                ClearChunkStateStorage(in state);
            }
            else if (!TryLeaseChunkState(chunkCoord, voxelSize, out state))
            {
                return false;
            }

            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                return false;
            }

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
                SetDirtyRunBits(dirtyMaskWords, startIndex, runLength);
                for (int flatIndex = startIndex; flatIndex < endIndex; flatIndex++)
                {
                    sdfValueBits[flatIndex] = sdfBits;
                    materialIds[flatIndex] = run.MaterialId;
                    cellFlags[flatIndex] = run.Flags;
                }

                loadedDirtyCellCount += runLength;
            }

            state.DirtyCellCount = loadedDirtyCellCount;
            return TryStoreChunkState(address, in state);
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
            NativeArray<uint> dirtyMaskWords,
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
                dirtyMaskWords[firstWord] |= mask;
                return;
            }

            dirtyMaskWords[firstWord] |= uint.MaxValue << startBit;
            for (int wordIndex = firstWord + 1; wordIndex < lastWord; wordIndex++)
                dirtyMaskWords[wordIndex] = uint.MaxValue;

            dirtyMaskWords[lastWord] |= uint.MaxValue >> (31 - endBit);
        }

        private static void ReportVoxelDeltaChunkCorruption(uint actionMask, int dirtyCellCount)
        {
            uint context = _SaveCorruptionContextHash ^ actionMask;
            GlobalTelemetryBus.PublishSystemDegradation(_SaveCorruptionHash, context, math.max(0, dirtyCellCount));
        }

        private void TryRegisterSaveService()
        {
            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (_saveRegistered)
                return;

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveService = saveService;
            _saveRegistered = true;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryUnregisterSaveService()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private void ReplaceSaveService(ISaveService nextService)
        {
            TryUnregisterSaveService();
            _saveService = nextService;
            TryRegisterSaveService();
        }

        private void FlushPendingRebuilds()
        {
            for (int i = _pendingRebuildVolumes.Count - 1; i >= 0; i--)
            {
                HectonVoxelVolume volume = _pendingRebuildVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasRuntimeData)
                {
                    _pendingRebuildVolumes.RemoveAtSwapBack(i);
                    continue;
                }

                volume.RequestDeltaRebuild();
                _pendingRebuildVolumes.RemoveAtSwapBack(i);
            }
        }

        private void TrySchedulePendingCarve()
        {
            if (IsScheduledCarveBusy || !ValidatePendingCarveQueueState() || _pendingCarveCount <= 0)
                return;

            PendingCarveRequest request = PopPendingCarve();
            HectonVoxelVolume volume = request.Volume;
            if (volume == null || !volume.HasRuntimeData)
                return;

            ulong volumeId = EntityId.ToULong(volume.GetEntityId());
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
                    ClampCarveExtentMeters(request.AbsoluteHalfExtents.x, voxelSize),
                    ClampCarveExtentMeters(request.AbsoluteHalfExtents.y, voxelSize),
                    ClampCarveExtentMeters(request.AbsoluteHalfExtents.z, voxelSize))
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
            if (!TryResolveScheduledCarveTotalCandidateCount(span, out int totalCandidateCount))
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxQueueOverflowFlag);
                return;
            }

            int sliceStartIndex = ResolveScheduledCarveSliceStartIndex(in request, totalCandidateCount);
            if (sliceStartIndex < 0)
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxInvalidPendingCarveFlag);
                return;
            }

            int remainingCandidateCount = totalCandidateCount - sliceStartIndex;
            int candidateBudget = ResolveScheduledCarveJobCandidateBudget(ResolveGlobalQualityWeight01());
            int candidateCount = math.min(remainingCandidateCount, candidateBudget);
            if (candidateCount <= 0 || candidateCount > ScheduledCarveWriteCapacity)
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxQueueOverflowFlag);
                return;
            }

            int nextSliceStartIndex = sliceStartIndex + candidateCount;
            bool hasContinuation = nextSliceStartIndex < totalCandidateCount;
            if (hasContinuation &&
                (!ValidatePendingCarveQueueState() || _pendingCarveCount >= _pendingCarves.Length))
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxQueueOverflowFlag);
                return;
            }

            if (!TryResolveScheduledCarveWriteBuffer(candidateCount, out NativeArray<CarveCellWrite> scheduledWrites))
            {
                WriteBlackBoxSample(volumeId, VoxelBlackBoxInvalidPendingCarveFlag);
                return;
            }

            bool keepScheduledCarveWriteLock = false;
            try
            {
                if (!scheduledWrites.IsCreated || scheduledWrites.Length < candidateCount)
                {
                    DeferScheduledCarveBlackBoxSample(volumeId, VoxelBlackBoxInvalidPendingCarveFlag);
                    return;
                }

                _scheduledCarveRequest = request;
                ResetScheduledCarveCommitProgress();

                CarveSdfJob carveJob = default;
                carveJob.MinCell = minCell;
                carveJob.Span = span;
                carveJob.CandidateOffset = sliceStartIndex;
                carveJob.VoxelSize = voxelSize;
                carveJob.Radius = radius;
                carveJob.BlendRadius = blendRadius;
                carveJob.BlendStrength = ResolveBlendStrength(in request, voxelSize);
                carveJob.Center = segmentStart;
                carveJob.SegmentEnd = segmentEnd;
                carveJob.HalfExtents = halfExtents;
                carveJob.MaterialId = request.MaterialId;
                carveJob.DeltaFlags = request.DeltaFlags;
                carveJob.Shape = shape;
                carveJob.Writes = scheduledWrites;

                bool scheduled = false;
                try
                {
                    using (_carveScheduleProfilerMarker.Auto())
                    {
                        _scheduledCarveWriteCount = candidateCount;
                        _scheduledCarveHandle = carveJob.Schedule(candidateCount, 64);
                        _scheduledCarveRunning = true;
                        scheduled = true;
                        keepScheduledCarveWriteLock = true;
                        if (hasContinuation)
                        {
                            if (TryEnqueuePendingCarveContinuation(in request, nextSliceStartIndex))
                                DeferScheduledCarveBlackBoxSample(volumeId, VoxelBlackBoxScheduledCarveSlicedFlag);
                            else
                                DeferScheduledCarveBlackBoxSample(volumeId, VoxelBlackBoxQueueOverflowFlag);
                        }

                        if (!ShouldSuppressCarvePresentation(in request))
                            PublishDebrisSpawnSignal(in request, radius);
                    }
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
                        FlushDeferredScheduledCarveBlackBoxSample();
                    }
                }
            }
            finally
            {
                if (!keepScheduledCarveWriteLock && _scheduledCarveWritesLocked)
                {
                    UnlockScheduledCarveWrites();
                    FlushDeferredScheduledCarveBlackBoxSample();
                }
            }
        }

        private static bool TryResolveScheduledCarveTotalCandidateCount(int3 span, out int candidateCount)
        {
            candidateCount = 0;
            if (span.x <= 0 || span.y <= 0 || span.z <= 0)
                return false;

            long xy = (long)span.x * span.y;
            long total = xy * span.z;
            if (total <= 0L || total > int.MaxValue)
                return false;

            candidateCount = (int)total;
            return true;
        }

        private static int ResolveScheduledCarveSliceStartIndex(in PendingCarveRequest request, int totalCandidateCount)
        {
            if ((request.RuntimeFlags & PendingCarveRuntimeFlagSliced) == 0)
                return 0;

            int sliceStartIndex = request.SliceStartIndex;
            return (uint)sliceStartIndex < (uint)totalCandidateCount ? sliceStartIndex : -1;
        }

        private int ResolveScheduledCarveJobCandidateBudget(float qualityWeight01)
        {
            int budget = (int)math.ceil(math.lerp(
                MinScheduledCarveJobCandidatesPerSlice,
                MaxScheduledCarveJobCandidatesPerSlice,
                ResolveScheduledCarvePressure(qualityWeight01)));
            return math.clamp(budget, 1, ScheduledCarveWriteCapacity);
        }

        private bool TryEnqueuePendingCarveContinuation(in PendingCarveRequest request, int nextSliceStartIndex)
        {
            if (nextSliceStartIndex <= 0 || !ValidatePendingCarveQueueState() || _pendingCarveCount >= _pendingCarves.Length)
                return false;

            PendingCarveRequest continuation = request;
            continuation.SliceStartIndex = nextSliceStartIndex;
            continuation.RuntimeFlags = (byte)(request.RuntimeFlags |
                                               PendingCarveRuntimeFlagSliced |
                                               PendingCarveRuntimeFlagSuppressPresentation);
            EnqueuePendingCarveUnchecked(in continuation);
            return true;
        }

        private void TryCommitScheduledCarve()
        {
            if (!_scheduledCarveRunning && !_scheduledCarveCommitPending)
                return;

            using (_carveCommitProfilerMarker.Auto())
            {
                long commitStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
                bool scheduledWritesLocked = false;
                ulong deferredCommitFaultVolumeId = 0UL;
                uint deferredCommitFaultFlags = 0u;
                try
                {
                    if (_scheduledCarveRunning)
                    {
                        if (!TryFinalizeScheduledCarveJobForCommit())
                            return;

                        _scheduledCarveRunning = false;
                        _scheduledCarveCommitPending = true;
                        ResetScheduledCarveCommitProgress();
                        UnlockScheduledCarveWrites();
                    }

                    HectonVoxelVolume volume = _scheduledCarveRequest.Volume;
                    if (volume == null || !volume.HasRuntimeData)
                    {
                        ResetScheduledCarveState();
                        return;
                    }

                    if (!TryAcquireScheduledCarveWritesForCommit(out NativeArray<CarveCellWrite> scheduledWrites))
                    {
                        deferredCommitFaultVolumeId = ResolveScheduledCarveVolumeId();
                        deferredCommitFaultFlags |= VoxelBlackBoxInvalidPendingCarveFlag;
                        ResetScheduledCarveState();
                        return;
                    }

                    scheduledWritesLocked = true;

                    float voxelSize = math.max(volume.VoxelSize, MinRuntimeVoxelSize);
                    int writeCount = math.min(_scheduledCarveWriteCount, scheduledWrites.Length);
                    int remainingWrites = writeCount - _scheduledCarveCommitIndex;
                    int commitBudget = ConsumeScheduledCarveCommitWriteBudgetThisFrame(remainingWrites);
                    int scanBudget = ResolveScheduledCarveCommitScanBudgetThisFrame(remainingWrites, commitBudget);
                    int scannedWrites = 0;
                    int committedWrites = 0;
                    int i = _scheduledCarveCommitIndex;
                    for (; i < writeCount && scannedWrites < scanBudget && committedWrites < commitBudget; i++)
                    {
                        scannedWrites++;
                        CarveCellWrite write = scheduledWrites[i];
                        if (write.IsActive == 0)
                            continue;

                        committedWrites++;
                        int3 chunkCoord = FloorDiv(write.AbsoluteCell, ChunkResolution);
                        ChunkAddress address = new ChunkAddress(chunkCoord, voxelSize);
                        if (!TryGetOrCreateChunkState(chunkCoord, voxelSize, out ChunkDeltaState state))
                        {
                            deferredCommitFaultVolumeId = ResolveScheduledCarveVolumeId();
                            deferredCommitFaultFlags |= VoxelBlackBoxQueueOverflowFlag;
                            continue;
                        }
                        if (!TryComputeLocalCellIndex(write.AbsoluteCell, state.ChunkCoord, out uint localIndex))
                            continue;

                        if (!TryResolveChunkStateStorage(
                                in state,
                                out NativeArray<uint> dirtyMaskWords,
                                out NativeArray<ushort> sdfValueBits,
                                out NativeArray<byte> materialIds,
                                out NativeArray<byte> cellFlags))
                        {
                            deferredCommitFaultVolumeId = ResolveScheduledCarveVolumeId();
                            deferredCommitFaultFlags |= VoxelBlackBoxInvalidPendingCarveFlag;
                            continue;
                        }

                        byte previousMaterialId = DefaultMaterialId;
                        if (localIndex < (uint)materialIds.Length)
                            previousMaterialId = materialIds[(int)localIndex];

                        half resolvedValue = BitsToHalf(write.SdfValueBits);
                        if ((write.DeltaFlags & DeltaModeAdditive) != 0)
                        {
                            float currentDensity;
                            if (!TryResolveCurrentCellDensity(volume, in state, localIndex, write.AbsoluteCell, voxelSize, out currentDensity))
                                currentDensity = 0f;

                            resolvedValue = ClampToHalf(SmoothMaxQuadratic(currentDensity, (float)resolvedValue, math.max(voxelSize, write.BlendStrength)));
                        }

                        SetCell(dirtyMaskWords, sdfValueBits, materialIds, cellFlags, ref state, localIndex, resolvedValue, write.MaterialId, write.DeltaFlags);
                        TryStoreChunkState(address, in state);
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

                    _scheduledCarveCommitIndex = i;
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
                    if (!ShouldSuppressCarvePresentation(in _scheduledCarveRequest))
                    {
                        EmitCaveInDustDecal(in _scheduledCarveRequest, resolvedCarveRadius);
                        if ((_scheduledCarveRequest.SourceFlags & CarveSourceLaser) != 0 &&
                            (_scheduledCarveRequest.DeltaFlags & DeltaModeAdditive) == 0 &&
                            _scheduledCarveRequest.Shape != DeltaShapeBox)
                        {
                            PushRecentCutHeat(in _scheduledCarveRequest, resolvedCarveRadius);
                        }
                    }

                    PublishMaterialYieldIfNeeded();
                    PublishCarveMassTelemetryIfNeeded();
                    ResetScheduledCarveState();
                }
                finally
                {
                    if (scheduledWritesLocked)
                        UnlockScheduledCarveWrites();

                    FlushDeferredScheduledCarveBlackBoxSample();

                    if (deferredCommitFaultFlags != 0u)
                        WriteBlackBoxSample(deferredCommitFaultVolumeId, deferredCommitFaultFlags);

                    PublishCarveCommitWarningIfNeeded(commitStartTimestamp);
                }
            }
        }

        private bool TryFinalizeScheduledCarveJobForCommit()
        {
            if (DispatcherJobSwap.TryComplete(ref _scheduledCarveHandle, false))
                return true;

            DeferScheduledCarveBlackBoxSample(ResolveScheduledCarveVolumeId(), VoxelBlackBoxScheduledCarveJobOverrunFlag);
            // Keep the vault pin as the relocation fence instead of stalling the frame.
            // DataVault defrag/growth observes ActiveBurstLockMask and defers while the carve worker owns this buffer.
            return false;
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
        }

        private int ConsumeScheduledCarveCommitWriteBudgetThisFrame(int remainingWrites)
        {
            if (remainingWrites <= 0)
                return 0;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_scheduledCarveCommitFrame != frame)
            {
                _scheduledCarveCommitFrame = frame;
                float perFrame = ResolveScheduledCarveCommitWritesPerFrame(ResolveGlobalQualityWeight01());
                float frameCap = math.ceil(perFrame);
                _scheduledCarveCommitWriteTokens = math.min(frameCap, _scheduledCarveCommitWriteTokens + perFrame);
            }

            int budget = (int)math.floor(_scheduledCarveCommitWriteTokens);
            budget = math.clamp(budget, 1, MaxScheduledCarveCommitWritesPerFrame);
            _scheduledCarveCommitWriteTokens = math.max(0f, _scheduledCarveCommitWriteTokens - budget);
            return math.min(budget, remainingWrites);
        }

        private int ResolveScheduledCarveCommitScanBudgetThisFrame(int remainingWrites, int writeBudget)
        {
            if (remainingWrites <= 0)
                return 0;

            int budget = (int)math.ceil(ResolveScheduledCarveCommitScansPerFrame(ResolveGlobalQualityWeight01()));
            budget = math.max(budget, writeBudget);
            budget = math.clamp(budget, 1, MaxScheduledCarveCommitScansPerFrame);
            return math.min(budget, remainingWrites);
        }

        private float ResolveScheduledCarveCommitWritesPerFrame(float qualityWeight01)
        {
            return math.lerp(
                MinScheduledCarveCommitWritesPerFrame,
                MaxScheduledCarveCommitWritesPerFrame,
                ResolveScheduledCarvePressure(qualityWeight01));
        }

        private float ResolveScheduledCarveCommitScansPerFrame(float qualityWeight01)
        {
            return math.lerp(
                MinScheduledCarveCommitScansPerFrame,
                MaxScheduledCarveCommitScansPerFrame,
                ResolveScheduledCarvePressure(qualityWeight01));
        }

        private float ResolveScheduledCarvePressure(float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float smooth = quality * quality * (3f - 2f * quality);
            float pendingCapacity = InitialCarveEventQueueCapacity + InitialPendingCarveCapacity;
            float backlog = math.saturate((_queuedCarveEventCount + _pendingCarveCount) / math.max(1f, pendingCapacity));
            return math.saturate(smooth + (backlog * ScheduledCarveBacklogPressureBoost * (1f - smooth)));
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

            _pendingRebuildVolumes.TryAdd(volume);
        }

        private bool TryGetOrCreateChunkState(int3 chunkCoord, float voxelSize, out ChunkDeltaState state)
        {
            ChunkAddress address = new ChunkAddress(chunkCoord, voxelSize);
            if (_chunkStates.TryGetValue(address, out state))
                return true;

            if (!TryLeaseChunkState(chunkCoord, voxelSize, out state))
                return false;

            if (_chunkStates.TryAdd(address, state))
                return true;

            ReleaseChunkState(state);
            state = default;
            ReportBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
            return false;
        }

        private bool TryStoreChunkState(ChunkAddress address, in ChunkDeltaState state)
        {
            if (_chunkStates.TrySet(address, state, out _, out _))
                return true;

            ReportBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
            return false;
        }

        private bool TryStoreCompactedChunkState(ChunkAddress address, CompactedChunkState state)
        {
            if (!_compactedChunkStates.TrySet(address, state, out CompactedChunkState previous, out bool hadPrevious))
            {
                state.Dispose();
                ReportBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
                return false;
            }

            if (hadPrevious)
                previous.Dispose();

            return true;
        }

        private void ResetScheduledCarveCommitProgress()
        {
            _scheduledCarveCommitIndex = 0;
            _scheduledCarveCommitFrame = -1;
            _scheduledCarveCommitWriteTokens = 0f;
            _scheduledCarveTouchedMinCell = new int3(int.MaxValue);
            _scheduledCarveTouchedMaxCell = new int3(int.MinValue);
            _scheduledCarveTouchedAnyCell = false;
            _scheduledCarveDestroyedTitaniumCells = 0;
            _scheduledCarveMassUnits = 0;
        }

        private void ResetScheduledCarveState()
        {
            UnlockScheduledCarveWrites();
            FlushDeferredScheduledCarveBlackBoxSample();
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
            if (!_chunkWriteVersions.TrySet(address, version + 1, out _, out _))
                ReportBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
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
            if (!ValidatePendingCompactionQueueState())
                return;

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
            if (!ValidatePendingCompactionQueueState())
                return false;

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
            if (_scheduledCompactionRunning || !ValidatePendingCompactionQueueState() || _pendingCompactionCount <= 0)
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

            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> stateDirtyMaskWords,
                    out NativeArray<ushort> stateSdfValueBits,
                    out NativeArray<byte> stateMaterialIds,
                    out NativeArray<byte> stateCellFlags))
            {
                return;
            }

            if (!volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float sdfRange,
                    out int publishedSonarVersion,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease sourceSdfReadLease))
            {
                volume.RequestDeltaRebuild();
                request.RequiredSonarVersion = volume.PublishedSonarVersion + 1;
                request.DirtyCount = currentDirtyCount;
                RequeueCompaction(in request);
                return;
            }

            if (publishedSonarVersion < request.RequiredSonarVersion)
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in sourceSdfReadLease);
                RequeueCompaction(in request);
                return;
            }

            int encodedSdfSampleCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;
            if (encodedSdfSampleCount <= 0 || encodedSdf.Length < encodedSdfSampleCount)
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in sourceSdfReadLease);
                RequeueCompaction(in request);
                return;
            }

            if (!TryLeaseCompactionScratchBuffers(
                    encodedSdfSampleCount,
                    out NativeArray<byte> sourceSdf,
                    out NativeArray<uint> dirtyMaskCopy,
                    out NativeArray<ushort> deltaSdfCopy,
                    out NativeArray<byte> materialCopy,
                    out NativeArray<byte> flagsCopy,
                    out NativeArray<ushort> outputSdf,
                    out NativeArray<byte> outputMaterials,
                    out NativeArray<byte> outputFlags,
                    out NativeArray<byte> rleUniformFlag))
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in sourceSdfReadLease);
                return;
            }

            int snapshotWriteVersion = ResolveChunkWriteVersion(request.Address);
            bool sourceLeaseHeld = true;
            bool scheduled = false;
            try
            {
                rleUniformFlag[0] = 0;
                for (int i = 0; i < encodedSdfSampleCount; i++)
                    sourceSdf[i] = encodedSdf[i];

                volume.ReleasePublishedSonarSdfPayloadReadLease(in sourceSdfReadLease);
                sourceSdfReadLease = default;
                sourceLeaseHeld = false;

                _scheduledCompactionRequest = new ScheduledCompactionRequest
                {
                    Volume = volume,
                    Address = request.Address,
                    RequiredSonarVersion = request.RequiredSonarVersion,
                    SourceSonarVersion = publishedSonarVersion,
                    WriteVersion = snapshotWriteVersion
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
                JobHandle chunkStateCopyHandle = new VoxelDeltaCopyChunkStateJob
                {
                    SourceDirtyMaskWords = stateDirtyMaskWords,
                    SourceSdfValueBits = stateSdfValueBits,
                    SourceMaterialIds = stateMaterialIds,
                    SourceCellFlags = stateCellFlags,
                    DestinationDirtyMaskWords = dirtyMaskCopy,
                    DestinationSdfValueBits = deltaSdfCopy,
                    DestinationMaterialIds = materialCopy,
                    DestinationCellFlags = flagsCopy
                }.Schedule(ChunkCellCount, 64);
                JobHandle compactionHandle = job.Schedule(ChunkCellCount, 64, chunkStateCopyHandle);
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
                if (sourceLeaseHeld)
                    volume.ReleasePublishedSonarSdfPayloadReadLease(in sourceSdfReadLease);

                if (!scheduled)
                {
                    ReleaseCompactionScratchBuffers();
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

            if (!ValidatePendingCompactionQueueState())
            {
                _compactionFrostTickCounter = 0;
                return;
            }

            if (_pendingCompactionCount <= 0)
            {
                _compactionFrostTickCounter = 0;
                return;
            }

            if (IsCompactionPressureHigh())
            {
                _compactionFrostTickCounter = 0;
                TrySchedulePendingCompaction();
                return;
            }

            _compactionFrostTickCounter++;
            if (_compactionFrostTickCounter < CompactionFrostTickIntervalFrames)
                return;

            _compactionFrostTickCounter = 0;
            TrySchedulePendingCompaction();
        }

        private bool IsCompactionPressureHigh()
        {
            if (!ValidatePendingCompactionQueueState())
                return false;

            if (_pendingCompactionCount >= CompactionPressurePendingThreshold)
                return true;

            if (!_chunkStatePoolCreated)
                return false;

            return _chunkStateFreeCount <= CompactionPressureFreeSlotThreshold;
        }

        private void TryCommitScheduledCompaction()
        {
            if (!_scheduledCompactionRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledCompactionHandle, false))
                return;

            _scheduledCompactionRunning = false;
            ScheduledCompactionRequest request = _scheduledCompactionRequest;
            bool sourceStillCurrent = request.Volume != null &&
                                      request.Volume.PublishedSonarVersion == request.SourceSonarVersion;
            if (!sourceStillCurrent ||
                !TryResolveCompactionScratchBuffers(
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out NativeArray<ushort> outputSdf,
                    out NativeArray<byte> outputMaterials,
                    out NativeArray<byte> outputFlags,
                    out NativeArray<byte> rleUniformFlag))
            {
                ReleaseCompactionScratchBuffers();
                _scheduledCompactionRequest = default;
                _scheduledCompactionHandle = default;
                return;
            }

            bool uniformCompaction = rleUniformFlag.IsCreated &&
                                     rleUniformFlag.Length > 0 &&
                                     rleUniformFlag[0] != 0 &&
                                     outputSdf.IsCreated &&
                                     outputSdf.Length > 0 &&
                                     outputMaterials.IsCreated &&
                                     outputMaterials.Length > 0 &&
                                     outputFlags.IsCreated &&
                                     outputFlags.Length > 0;

            if (uniformCompaction)
            {
                TryStoreCompactedChunkState(request.Address, new CompactedChunkState(
                    request.Address.ChunkCoord,
                    request.Address.VoxelSize,
                    outputSdf[0],
                    outputMaterials[0],
                    outputFlags[0]));
            }

            if (uniformCompaction &&
                ResolveChunkWriteVersion(request.Address) == request.WriteVersion &&
                _chunkStates.TryRemove(request.Address, out ChunkDeltaState dirtyState))
            {
                ReleaseChunkState(dirtyState);
                _chunkWriteVersions.Remove(request.Address);
            }

            ReleaseCompactionScratchBuffers();
            _scheduledCompactionRequest = default;
            _scheduledCompactionHandle = default;
        }

        private static float ResolveBlendStrength(in PendingCarveRequest request, float voxelSize)
        {
            return request.ExplicitBlendStrength > 0f
                ? ClampCarveBlendStrengthMeters(request.ExplicitBlendStrength, voxelSize)
                : ClampCarveBlendStrengthMeters(request.ExplicitRadiusMeters * 0.35f, voxelSize);
        }

        private bool TryResolveCurrentCellDensity(
            HectonVoxelVolume volume,
            in ChunkDeltaState state,
            uint localIndex,
            int3 absoluteCell,
            float voxelSize,
            out float density)
        {
            if (TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out _,
                    out _) &&
                IsDirty(dirtyMaskWords, localIndex))
            {
                density = (float)BitsToHalf(sdfValueBits[(int)localIndex]);
                return true;
            }

            if (volume != null)
            {
                double3 absoluteCellCenter = (new double3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5d) * voxelSize;
                Vector3 runtimeCellCenter = ToRuntimeVector3(absoluteCellCenter);
                if (volume.TrySampleDensity(runtimeCellCenter, out density))
                    return true;
            }

            density = 0f;
            return false;
        }

        private int CountDirtyCells(in ChunkDeltaState state)
        {
            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out _,
                    out _,
                    out _))
            {
                return 0;
            }

            if (state.DirtyCellCount > 0)
                return state.DirtyCellCount;

            int dirtyCount = 0;
            for (int i = 0; i < dirtyMaskWords.Length; i++)
                dirtyCount += math.countbits(dirtyMaskWords[i]);

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

        private static bool IsSupportedVoxelDeltaChunkCoordinate(long value)
        {
            return value >= int.MinValue && value <= int.MaxValue;
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

        private static bool IsDirty(NativeArray<uint> dirtyMaskWords, uint localIndex)
        {
            int wordIndex = (int)(localIndex >> 5);
            uint bitMask = 1u << ((int)localIndex & 31);
            return (dirtyMaskWords[wordIndex] & bitMask) != 0u;
        }

        private static void SetDirtyBit(NativeArray<uint> dirtyMaskWords, uint localIndex)
        {
            int wordIndex = (int)(localIndex >> 5);
            uint bitMask = 1u << ((int)localIndex & 31);
            dirtyMaskWords[wordIndex] |= bitMask;
        }

        private bool SetCell(ref ChunkDeltaState state, uint localIndex, half value, byte materialId, byte cellFlags)
        {
            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlagValues))
            {
                return false;
            }

            SetCell(dirtyMaskWords, sdfValueBits, materialIds, cellFlagValues, ref state, localIndex, value, materialId, cellFlags);
            return true;
        }

        private static byte SanitizeVoxelDeltaCellFlags(byte cellFlags)
        {
            return (byte)(cellFlags & VoxelDeltaChunkDTO.SupportedCellFlags);
        }

        private static void SetCell(
            NativeArray<uint> dirtyMaskWords,
            NativeArray<ushort> sdfValueBits,
            NativeArray<byte> materialIds,
            NativeArray<byte> cellFlagValues,
            ref ChunkDeltaState state,
            uint localIndex,
            half value,
            byte materialId,
            byte cellFlags)
        {
            int flatIndex = (int)localIndex;
            bool isDirty = IsDirty(dirtyMaskWords, localIndex);
            if (!isDirty)
            {
                SetDirtyBit(dirtyMaskWords, localIndex);
                state.DirtyCellCount++;
                sdfValueBits[flatIndex] = HalfToBits(value);
                cellFlagValues[flatIndex] = cellFlags;
            }
            else
            {
                byte existingFlags = cellFlagValues[flatIndex];
                bool replace = (cellFlags & DeltaModeReplace) != 0;
                bool existingReplace = (existingFlags & DeltaModeReplace) != 0;
                float existingValue = (float)BitsToHalf(sdfValueBits[flatIndex]);
                float nextValue = (float)value;

                if (replace || existingReplace)
                {
                    sdfValueBits[flatIndex] = HalfToBits(value);
                    cellFlagValues[flatIndex] = cellFlags;
                }
                else
                {
                    float mergedValue = MergeSdfDeltaDensity(existingValue, existingFlags, nextValue, cellFlags);
                    sdfValueBits[flatIndex] = HalfToBits(ClampToHalf(mergedValue));
                    if (((existingFlags ^ cellFlags) & DeltaModeAdditive) != 0)
                        cellFlagValues[flatIndex] = cellFlags;
                }
            }

            materialIds[flatIndex] = materialId;
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
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001VoxelDeltaProcessorSignalPushDropCount);
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
                SourceKind = ItemAcquiredSignalSourceKinds.VoxelCarve,
                Flags = _scheduledCarveRequest.SourceFlags,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            };
            SignalBus<ItemAcquiredSignal>.TryPushTracked(in signal, ref s_x001VoxelDeltaProcessorSignalPushDropCount);
        }

        private void PublishCarveMassTelemetryIfNeeded()
        {
            if (_scheduledCarveMassUnits <= 0)
                return;

            ReportBlackBoxSample((ulong)(uint)math.max(0, _totalVoxelsCarved), VoxelBlackBoxCarvedMassTelemetryFlag);
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
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Operation = ResolveVoxelChunkModifiedOperation(_scheduledCarveRequest.DeltaFlags),
                Shape = _scheduledCarveRequest.Shape,
                Flags = _scheduledCarveRequest.SourceFlags,
                StateHash = stateHash
            };

            VoxelChunkModifiedEvents.TryPublish(in modifiedEvent);
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
            float minRadius = math.max(volume.VoxelSize * 1.25f, MinCarveRadiusMeters);
            if (request.ExplicitRadiusMeters > 0f)
                return math.clamp(request.ExplicitRadiusMeters, minRadius, math.max(minRadius, MaxCarveRadiusMeters));

            float baseRadius = math.max(volume.VoxelSize * 2f, MinCarveRadiusMeters);
            return math.clamp(baseRadius + request.AccumulatedDamage * 0.08f, baseRadius, math.max(baseRadius, MaxCarveRadiusMeters));
        }

        private static float ClampCarveRadiusMeters(float radius, float voxelSize)
        {
            float minRadius = math.max(voxelSize * 1.25f, MinCarveRadiusMeters);
            return math.clamp(radius, minRadius, math.max(minRadius, MaxCarveRadiusMeters));
        }

        private static float ClampCarveExtentMeters(float extent, float voxelSize)
        {
            float minExtent = math.max(voxelSize, MinRuntimeVoxelSize);
            return math.clamp(math.abs(extent), minExtent, math.max(minExtent, MaxCarveRadiusMeters));
        }

        private static float ClampCarveBlendStrengthMeters(float strength, float voxelSize)
        {
            float minStrength = math.max(voxelSize, MinRuntimeVoxelSize);
            return math.clamp(strength, minStrength, math.max(minStrength, MaxCarveRadiusMeters));
        }

        private void EmitCaveInDustDecal(in PendingCarveRequest request, float radius)
        {
            if ((request.DeltaFlags & DeltaModeAdditive) != 0 || radius <= 0f)
                return;

            IFluidDecalPresentationSink fluidDecals = _fluidDecals;
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
            if (requiredCount <= 0 || requiredCount > ScheduledCarveWriteCapacity)
            {
                WriteBlackBoxSample(0UL, VoxelBlackBoxQueueOverflowFlag);
                return false;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (vault.IsCompactionFenceActive)
                return false;

            if (!IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites) ||
                _scheduledCarveWritesCapacity < ScheduledCarveWriteCapacity ||
                vault.IsCompactionFenceActive ||
                !TryAcquireScheduledCarveWritesGuard(vault))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (!TryResolveVaultBuffer(vault, in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites, requiredCount, out writes))
                    return false;

                _scheduledCarveWritesCapacity = writes.Length;
                keepLock = true;
                return true;
            }
            finally
            {
                if (!keepLock)
                {
                    UnlockScheduledCarveWrites();
                    writes = default;
                }
            }
        }

        private bool EnsureScheduledCarveWriteBuffer()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _scheduledCarveWritesCapacity = 0;
                return false;
            }

            if (IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites) &&
                _scheduledCarveWritesCapacity >= ScheduledCarveWriteCapacity &&
                TryResolveVaultBuffer(vault, in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites, ScheduledCarveWriteCapacity, out _))
            {
                return true;
            }

            if (vault.IsCompactionFenceActive)
                return false;

            _scheduledCarveWritesHandle = vault.EnsureGenerationHandle<CarveCellWrite>(
                BufferID.ShinobuDeltaCrusherCarveWrites,
                ScheduledCarveWriteCapacity,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            _scheduledCarveWritesCapacity = IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites)
                ? ScheduledCarveWriteCapacity
                : 0;
            return _scheduledCarveWritesCapacity >= ScheduledCarveWriteCapacity &&
                   TryResolveVaultBuffer(vault, in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites, ScheduledCarveWriteCapacity, out _);
        }

        private void DisposeScheduledCarveBuffersForShutdownOnly()
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

        private void ReleaseScheduledCarveWriteHandle(IDataVault vault)
        {
            if (_scheduledCarveWritesLocked)
                return;

            if (vault != null && IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites))
                vault.ReleaseBuffer(in _scheduledCarveWritesHandle);

            _scheduledCarveWritesHandle = default;
            _scheduledCarveWritesCapacity = 0;
        }

        private void UnlockScheduledCarveWrites()
        {
            if (!_scheduledCarveWritesLocked)
                return;

            _scheduledCarveWritesLocked = false;
            IDataVault vault = _scheduledCarveWritesPinVault;
            _scheduledCarveWritesPinVault = null;
            vault?.TryUnlockBuffer(BufferID.ShinobuDeltaCrusherCarveWrites, SystemID.TerrainSeams);
        }

        private bool TryAcquireScheduledCarveWritesForCommit(out NativeArray<CarveCellWrite> writes)
        {
            writes = default;
            if (_scheduledCarveWriteCount <= 0)
                return false;

            IDataVault vault = ResolveDataVault();
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in _scheduledCarveWritesHandle, BufferID.ShinobuDeltaCrusherCarveWrites) ||
                !TryAcquireScheduledCarveWritesGuard(vault))
                return false;

            bool keepLock = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (!TryResolveVaultBuffer(
                        vault,
                        in _scheduledCarveWritesHandle,
                        BufferID.ShinobuDeltaCrusherCarveWrites,
                        math.max(1, _scheduledCarveWriteCount),
                        out writes) &&
                    _scheduledCarveWriteCount > 0)
                {
                    return false;
                }

                _scheduledCarveWritesCapacity = math.max(_scheduledCarveWritesCapacity, writes.Length);
                keepLock = true;
                return true;
            }
            finally
            {
                if (!keepLock)
                {
                    UnlockScheduledCarveWrites();
                    writes = default;
                }
            }
        }

        private bool TryAcquireScheduledCarveWritesGuard(IDataVault vault)
        {
            if (_scheduledCarveWritesLocked || vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryLockBuffer(BufferID.ShinobuDeltaCrusherCarveWrites, SystemID.TerrainSeams))
                return false;

            bool keepPin = false;
            try
            {
                _scheduledCarveWritesPinVault = vault;
                _scheduledCarveWritesLocked = true;
                keepPin = true;
                return true;
            }
            finally
            {
                if (!keepPin)
                    vault.TryUnlockBuffer(BufferID.ShinobuDeltaCrusherCarveWrites, SystemID.TerrainSeams);
            }
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

            Vector3 runtimeHitPoint = ToRuntimeVector3(request.AbsoluteHitPoint);
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
            s_recentCutHeatStrengthTime[slot] = new Vector4(
                LaserCutHeatStrength,
                ResolveLaserCutHeatShaderClockSeconds(),
                LaserCutHeatLifetimeSeconds,
                0f);
            Shader.SetGlobalVector(_laserHitAupId, s_recentCutHeatPositionRadius[slot]);
            Shader.SetGlobalVector(_laserHitHeatId, s_recentCutHeatStrengthTime[slot]);
            Shader.SetGlobalVectorArray(_recentCutHeatPositionRadiusId, s_recentCutHeatPositionRadius);
            Shader.SetGlobalVectorArray(_recentCutHeatStrengthTimeId, s_recentCutHeatStrengthTime);
            Shader.SetGlobalInt(_recentCutHeatCountId, s_recentCutHeatCount);
        }

        private static float ResolveLaserCutHeatShaderClockSeconds()
        {
            return Time.timeSinceLevelLoad;
        }

        private void DisposeScheduledCompactionBuffersForShutdownOnly()
        {
            // [BLOCKING_SYNC_POINT] OnDisable teardown only: compaction scratch is persistent and must
            // not be released while a scene-shutdown compaction writer can still touch it.
            if (_scheduledCompactionRunning)
                DispatcherJobFence.TryComplete(ref _scheduledCompactionHandle, forceComplete: true);

            ReleaseCompactionScratchBuffers();
            _scheduledCompactionRequest = default;
            _scheduledCompactionHandle = default;
            _scheduledCompactionRunning = false;
        }

        private void EnsureCompactionScratchBuffers()
        {
            if (_compactionScratchCreated)
                return;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                _compactionScratchCreated = false;
                _compactionScratchLeased = false;
                return;
            }

            _compactionSourceSdfScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.SaveVoxelDeltaCompactionSourceSdfScratch, CompactionSourceSdfCapacity, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionDirtyMaskScratchHandle = vault.EnsureGenerationHandle<uint>(BufferID.SaveVoxelDeltaCompactionDirtyMaskScratch, ChunkDirtyMaskWordCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionDeltaSdfScratchHandle = vault.EnsureGenerationHandle<ushort>(BufferID.SaveVoxelDeltaCompactionDeltaSdfScratch, ChunkCellCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionMaterialScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.SaveVoxelDeltaCompactionMaterialScratch, ChunkCellCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionFlagsScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.SaveVoxelDeltaCompactionFlagsScratch, ChunkCellCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionOutputSdfScratchHandle = vault.EnsureGenerationHandle<ushort>(BufferID.SaveVoxelDeltaCompactionOutputSdfScratch, ChunkCellCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionOutputMaterialsScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.SaveVoxelDeltaCompactionOutputMaterialsScratch, ChunkCellCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionOutputFlagsScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.SaveVoxelDeltaCompactionOutputFlagsScratch, ChunkCellCount, SystemID.TerrainSeams, NativeArrayOptions.UninitializedMemory);
            _compactionUniformFlagScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.SaveVoxelDeltaCompactionUniformFlagScratch, 1, SystemID.TerrainSeams, NativeArrayOptions.ClearMemory);
            _compactionScratchCreated = TryResolveCompactionScratchBuffers(
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
            _compactionScratchLeased = false;
        }

        private bool TryLeaseCompactionScratchBuffers(
            int sourceSdfLength,
            out NativeArray<byte> sourceSdf,
            out NativeArray<uint> dirtyMaskCopy,
            out NativeArray<ushort> deltaSdfCopy,
            out NativeArray<byte> materialCopy,
            out NativeArray<byte> flagsCopy,
            out NativeArray<ushort> outputSdf,
            out NativeArray<byte> outputMaterials,
            out NativeArray<byte> outputFlags,
            out NativeArray<byte> rleUniformFlag)
        {
            EnsureCompactionScratchBuffers();
            if (_compactionScratchLeased ||
                sourceSdfLength <= 0 ||
                !TryPinCompactionScratchBuffers())
            {
                sourceSdf = default;
                dirtyMaskCopy = default;
                deltaSdfCopy = default;
                materialCopy = default;
                flagsCopy = default;
                outputSdf = default;
                outputMaterials = default;
                outputFlags = default;
                rleUniformFlag = default;
                return false;
            }

            if (!TryResolveCompactionScratchBuffers(
                    out NativeArray<byte> sourceSdfScratch,
                    out NativeArray<uint> dirtyMaskScratch,
                    out NativeArray<ushort> deltaSdfScratch,
                    out NativeArray<byte> materialScratch,
                    out NativeArray<byte> flagsScratch,
                    out NativeArray<ushort> outputSdfScratch,
                    out NativeArray<byte> outputMaterialsScratch,
                    out NativeArray<byte> outputFlagsScratch,
                    out NativeArray<byte> uniformFlagScratch))
            {
                UnlockCompactionScratchBuffers();
                sourceSdf = default;
                dirtyMaskCopy = default;
                deltaSdfCopy = default;
                materialCopy = default;
                flagsCopy = default;
                outputSdf = default;
                outputMaterials = default;
                outputFlags = default;
                rleUniformFlag = default;
                return false;
            }

            if (sourceSdfLength > sourceSdfScratch.Length)
            {
                UnlockCompactionScratchBuffers();
                sourceSdf = default;
                dirtyMaskCopy = default;
                deltaSdfCopy = default;
                materialCopy = default;
                flagsCopy = default;
                outputSdf = default;
                outputMaterials = default;
                outputFlags = default;
                rleUniformFlag = default;
                return false;
            }

            _compactionScratchLeased = true;
            sourceSdf = sourceSdfScratch;
            dirtyMaskCopy = dirtyMaskScratch;
            deltaSdfCopy = deltaSdfScratch;
            materialCopy = materialScratch;
            flagsCopy = flagsScratch;
            outputSdf = outputSdfScratch;
            outputMaterials = outputMaterialsScratch;
            outputFlags = outputFlagsScratch;
            rleUniformFlag = uniformFlagScratch;
            return true;
        }

        private bool TryPinCompactionScratchBuffers()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive || _compactionScratchPinMask != 0u)
                return false;

            _compactionScratchPinVault = vault;
            bool keepPins = false;
            try
            {
                if (!TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionSourceSdfScratch, CompactionScratchPinSourceSdf) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionDirtyMaskScratch, CompactionScratchPinDirtyMask) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionDeltaSdfScratch, CompactionScratchPinDeltaSdf) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionMaterialScratch, CompactionScratchPinMaterial) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionFlagsScratch, CompactionScratchPinFlags) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionOutputSdfScratch, CompactionScratchPinOutputSdf) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionOutputMaterialsScratch, CompactionScratchPinOutputMaterials) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionOutputFlagsScratch, CompactionScratchPinOutputFlags) ||
                    !TryLockCompactionScratchBuffer(vault, BufferID.SaveVoxelDeltaCompactionUniformFlagScratch, CompactionScratchPinUniformFlag) ||
                    vault.IsCompactionFenceActive ||
                    !TryResolveCompactionScratchBuffers(
                        vault,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    return false;
                }

                keepPins = true;
                return true;
            }
            finally
            {
                if (!keepPins)
                    UnlockCompactionScratchBuffers();
            }
        }

        private void UnlockCompactionScratchBuffers()
        {
            IDataVault vault = _compactionScratchPinVault;
            uint pinMask = _compactionScratchPinMask;
            _compactionScratchPinMask = 0u;
            _compactionScratchPinVault = null;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinUniformFlag, BufferID.SaveVoxelDeltaCompactionUniformFlagScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinOutputFlags, BufferID.SaveVoxelDeltaCompactionOutputFlagsScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinOutputMaterials, BufferID.SaveVoxelDeltaCompactionOutputMaterialsScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinOutputSdf, BufferID.SaveVoxelDeltaCompactionOutputSdfScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinFlags, BufferID.SaveVoxelDeltaCompactionFlagsScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinMaterial, BufferID.SaveVoxelDeltaCompactionMaterialScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinDeltaSdf, BufferID.SaveVoxelDeltaCompactionDeltaSdfScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinDirtyMask, BufferID.SaveVoxelDeltaCompactionDirtyMaskScratch);
            TryUnlockCompactionScratchBuffer(vault, pinMask, CompactionScratchPinSourceSdf, BufferID.SaveVoxelDeltaCompactionSourceSdfScratch);
        }

        private bool TryLockCompactionScratchBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_compactionScratchPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.TerrainSeams))
                return false;

            _compactionScratchPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockCompactionScratchBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.TerrainSeams);
        }

        private bool TryResolveCompactionScratchBuffers(
            out NativeArray<byte> sourceSdf,
            out NativeArray<uint> dirtyMaskCopy,
            out NativeArray<ushort> deltaSdfCopy,
            out NativeArray<byte> materialCopy,
            out NativeArray<byte> flagsCopy,
            out NativeArray<ushort> outputSdf,
            out NativeArray<byte> outputMaterials,
            out NativeArray<byte> outputFlags,
            out NativeArray<byte> rleUniformFlag)
        {
            sourceSdf = default;
            dirtyMaskCopy = default;
            deltaSdfCopy = default;
            materialCopy = default;
            flagsCopy = default;
            outputSdf = default;
            outputMaterials = default;
            outputFlags = default;
            rleUniformFlag = default;

            IDataVault vault = ResolveDataVault();
            return TryResolveCompactionScratchBuffers(
                vault,
                out sourceSdf,
                out dirtyMaskCopy,
                out deltaSdfCopy,
                out materialCopy,
                out flagsCopy,
                out outputSdf,
                out outputMaterials,
                out outputFlags,
                out rleUniformFlag);
        }

        private bool TryResolveCompactionScratchBuffers(
            IDataVault vault,
            out NativeArray<byte> sourceSdf,
            out NativeArray<uint> dirtyMaskCopy,
            out NativeArray<ushort> deltaSdfCopy,
            out NativeArray<byte> materialCopy,
            out NativeArray<byte> flagsCopy,
            out NativeArray<ushort> outputSdf,
            out NativeArray<byte> outputMaterials,
            out NativeArray<byte> outputFlags,
            out NativeArray<byte> rleUniformFlag)
        {
            sourceSdf = default;
            dirtyMaskCopy = default;
            deltaSdfCopy = default;
            materialCopy = default;
            flagsCopy = default;
            outputSdf = default;
            outputMaterials = default;
            outputFlags = default;
            rleUniformFlag = default;

            return TryResolveVaultBuffer(vault, in _compactionSourceSdfScratchHandle, BufferID.SaveVoxelDeltaCompactionSourceSdfScratch, CompactionSourceSdfCapacity, out sourceSdf) &&
                   TryResolveVaultBuffer(vault, in _compactionDirtyMaskScratchHandle, BufferID.SaveVoxelDeltaCompactionDirtyMaskScratch, ChunkDirtyMaskWordCount, out dirtyMaskCopy) &&
                   TryResolveVaultBuffer(vault, in _compactionDeltaSdfScratchHandle, BufferID.SaveVoxelDeltaCompactionDeltaSdfScratch, ChunkCellCount, out deltaSdfCopy) &&
                   TryResolveVaultBuffer(vault, in _compactionMaterialScratchHandle, BufferID.SaveVoxelDeltaCompactionMaterialScratch, ChunkCellCount, out materialCopy) &&
                   TryResolveVaultBuffer(vault, in _compactionFlagsScratchHandle, BufferID.SaveVoxelDeltaCompactionFlagsScratch, ChunkCellCount, out flagsCopy) &&
                   TryResolveVaultBuffer(vault, in _compactionOutputSdfScratchHandle, BufferID.SaveVoxelDeltaCompactionOutputSdfScratch, ChunkCellCount, out outputSdf) &&
                   TryResolveVaultBuffer(vault, in _compactionOutputMaterialsScratchHandle, BufferID.SaveVoxelDeltaCompactionOutputMaterialsScratch, ChunkCellCount, out outputMaterials) &&
                   TryResolveVaultBuffer(vault, in _compactionOutputFlagsScratchHandle, BufferID.SaveVoxelDeltaCompactionOutputFlagsScratch, ChunkCellCount, out outputFlags) &&
                   TryResolveVaultBuffer(vault, in _compactionUniformFlagScratchHandle, BufferID.SaveVoxelDeltaCompactionUniformFlagScratch, 1, out rleUniformFlag);
        }

        private void ReleaseCompactionScratchBuffers()
        {
            if (_compactionScratchLeased)
                UnlockCompactionScratchBuffers();

            _compactionScratchLeased = false;
        }

        private void DisposeCompactionScratchBuffers()
        {
            DisposeCompactionScratchBuffers(ResolveDataVault());
        }

        private void DisposeCompactionScratchBuffers(IDataVault vault)
        {
            if (_compactionScratchPinMask != 0u)
                UnlockCompactionScratchBuffers();

            ReleaseVaultHandle(vault, ref _compactionSourceSdfScratchHandle, BufferID.SaveVoxelDeltaCompactionSourceSdfScratch);
            ReleaseVaultHandle(vault, ref _compactionDirtyMaskScratchHandle, BufferID.SaveVoxelDeltaCompactionDirtyMaskScratch);
            ReleaseVaultHandle(vault, ref _compactionDeltaSdfScratchHandle, BufferID.SaveVoxelDeltaCompactionDeltaSdfScratch);
            ReleaseVaultHandle(vault, ref _compactionMaterialScratchHandle, BufferID.SaveVoxelDeltaCompactionMaterialScratch);
            ReleaseVaultHandle(vault, ref _compactionFlagsScratchHandle, BufferID.SaveVoxelDeltaCompactionFlagsScratch);
            ReleaseVaultHandle(vault, ref _compactionOutputSdfScratchHandle, BufferID.SaveVoxelDeltaCompactionOutputSdfScratch);
            ReleaseVaultHandle(vault, ref _compactionOutputMaterialsScratchHandle, BufferID.SaveVoxelDeltaCompactionOutputMaterialsScratch);
            ReleaseVaultHandle(vault, ref _compactionOutputFlagsScratchHandle, BufferID.SaveVoxelDeltaCompactionOutputFlagsScratch);
            ReleaseVaultHandle(vault, ref _compactionUniformFlagScratchHandle, BufferID.SaveVoxelDeltaCompactionUniformFlagScratch);
            _compactionScratchCreated = false;
            _compactionScratchLeased = false;
        }

        private void EnsureNativeSnapshotScratchBuffer()
        {
            int requiredCapacity = ResolveNativeSnapshotScratchCapacityBytes();
            if (TryResolveNativeSnapshotScratch(out NativeArray<byte> scratch) &&
                scratch.Length >= requiredCapacity &&
                _nativeSnapshotScratchCapacityBytes >= requiredCapacity)
            {
                return;
            }

            if (_nativeSnapshotScratchLeaseCount > 0)
            {
                _nativeSnapshotScratchDisposeDeferred = true;
                if (_nativeSnapshotScratchDeferredVault == null)
                    _nativeSnapshotScratchDeferredVault = ResolveDataVault();
                return;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                _nativeSnapshotScratchCapacityBytes = 0;
                return;
            }

            DisposeNativeSnapshotScratchBuffer(vault);
            _nativeSnapshotScratchHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.SaveVoxelDeltaNativeSnapshotScratch,
                requiredCapacity,
                SystemID.TerrainSeams,
                NativeArrayOptions.UninitializedMemory);
            _nativeSnapshotScratchCapacityBytes = TryResolveNativeSnapshotScratch(out scratch) && scratch.Length >= requiredCapacity
                ? scratch.Length
                : 0;
        }

        private void DisposeNativeSnapshotScratchBuffer()
        {
            DisposeNativeSnapshotScratchBuffer(_nativeSnapshotScratchDeferredVault ?? ResolveDataVault());
        }

        private void DisposeNativeSnapshotScratchBuffer(IDataVault vault)
        {
            if (_nativeSnapshotScratchLeaseCount > 0)
            {
                _nativeSnapshotScratchDisposeDeferred = true;
                _nativeSnapshotScratchDeferredVault = vault;
                return;
            }

            ReleaseVaultHandle(vault, ref _nativeSnapshotScratchHandle, BufferID.SaveVoxelDeltaNativeSnapshotScratch);
            _nativeSnapshotScratchCapacityBytes = 0;
            _nativeSnapshotScratchDisposeDeferred = false;
            _nativeSnapshotScratchDeferredVault = null;
        }

        private bool TryResolveNativeSnapshotScratch(out NativeArray<byte> scratch)
        {
            int requiredLength = math.max(1, _nativeSnapshotScratchCapacityBytes);
            return TryResolveVaultBuffer(
                ResolveDataVault(),
                in _nativeSnapshotScratchHandle,
                BufferID.SaveVoxelDeltaNativeSnapshotScratch,
                requiredLength,
                out scratch);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            if (vault != null && IsExactVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void DisposeChunkStates()
        {
            for (int slot = 0; slot < _chunkStates.SlotCapacity; slot++)
            {
                if (_chunkStates.TryGetSlot(slot, out _, out ChunkDeltaState state))
                    ReleaseChunkState(state);
            }

            _chunkStates.Clear();
            _chunkWriteVersions.Clear();
        }

        private void EnsureChunkStatePool()
        {
            if (_chunkStatePoolCreated)
                return;

            bool vaultBacked = TryEnsureVaultChunkStatePool();

            _chunkStatePoolBank0.Clear();
            _chunkStatePoolBank1.Clear();
            _chunkStatePoolBank2.Clear();
            _chunkStateFreeStack.Clear();
            if (!vaultBacked)
            {
                _chunkStateFreeCount = 0;
                _chunkStatePoolCreated = true;
                _chunkStatePoolVaultBacked = false;
                _chunkStatePoolExhaustedWarningArmed = false;
                return;
            }

            for (int i = 0; i < DirtyChunkStatePoolCapacity; i++)
            {
                ChunkDeltaState slotState = new ChunkDeltaState(
                    default,
                    MinRuntimeVoxelSize,
                    i);
                if (!TryAddChunkStatePoolSlot(in slotState))
                {
                    WriteChunkStatePoolCorruptionSample(i, _chunkStateFreeCount, DirtyChunkStatePoolCapacity);
                    _chunkStatePoolBank0.Clear();
                    _chunkStatePoolBank1.Clear();
                    _chunkStatePoolBank2.Clear();
                    _chunkStateFreeStack.Clear();
                    _chunkStateFreeCount = 0;
                    _chunkStatePoolCreated = true;
                    _chunkStatePoolVaultBacked = false;
                    _chunkStatePoolExhaustedWarningArmed = true;
                    return;
                }

                _chunkStateFreeStack.Add(DirtyChunkStatePoolCapacity - 1 - i);
            }

            _chunkStateFreeCount = DirtyChunkStatePoolCapacity;
            _chunkStatePoolCreated = true;
            _chunkStatePoolVaultBacked = vaultBacked;
            _chunkStatePoolExhaustedWarningArmed = false;
        }

        private bool TryAddChunkStatePoolSlot(in ChunkDeltaState state)
        {
            if (_chunkStatePoolBank0.Length < _chunkStatePoolBank0.Capacity)
            {
                _chunkStatePoolBank0.Add(state);
                return true;
            }

            if (_chunkStatePoolBank1.Length < _chunkStatePoolBank1.Capacity)
            {
                _chunkStatePoolBank1.Add(state);
                return true;
            }

            if (_chunkStatePoolBank2.Length < _chunkStatePoolBank2.Capacity)
            {
                _chunkStatePoolBank2.Add(state);
                return true;
            }

            return false;
        }

        private bool TryGetChunkStatePoolSlot(int slot, out ChunkDeltaState state)
        {
            state = default;
            if ((uint)slot >= DirtyChunkStatePoolCapacity)
                return false;

            int bank0Capacity = _chunkStatePoolBank0.Capacity;
            if (slot < bank0Capacity)
            {
                if (slot >= _chunkStatePoolBank0.Length)
                    return false;

                state = _chunkStatePoolBank0[slot];
                return true;
            }

            slot -= bank0Capacity;
            int bank1Capacity = _chunkStatePoolBank1.Capacity;
            if (slot < bank1Capacity)
            {
                if (slot >= _chunkStatePoolBank1.Length)
                    return false;

                state = _chunkStatePoolBank1[slot];
                return true;
            }

            slot -= bank1Capacity;
            if (slot >= _chunkStatePoolBank2.Length)
                return false;

            state = _chunkStatePoolBank2[slot];
            return true;
        }

        private bool TrySetChunkStatePoolSlot(int slot, in ChunkDeltaState state)
        {
            if ((uint)slot >= DirtyChunkStatePoolCapacity)
                return false;

            int bank0Capacity = _chunkStatePoolBank0.Capacity;
            if (slot < bank0Capacity)
            {
                if (slot >= _chunkStatePoolBank0.Length)
                    return false;

                _chunkStatePoolBank0[slot] = state;
                return true;
            }

            slot -= bank0Capacity;
            int bank1Capacity = _chunkStatePoolBank1.Capacity;
            if (slot < bank1Capacity)
            {
                if (slot >= _chunkStatePoolBank1.Length)
                    return false;

                _chunkStatePoolBank1[slot] = state;
                return true;
            }

            slot -= bank1Capacity;
            if (slot >= _chunkStatePoolBank2.Length)
                return false;

            _chunkStatePoolBank2[slot] = state;
            return true;
        }

        private void WriteChunkStatePoolCorruptionSample(int slot, int freeCount, int capacity)
        {
            int safeSlot = math.clamp(slot, 0, 0xFFFF);
            int safeFreeCount = math.clamp(freeCount, 0, 0xFFFFFF);
            int safeCapacity = math.clamp(capacity, 0, 0xFF);
            ulong encodedState = ((ulong)(uint)safeSlot << 40) |
                                 ((ulong)(uint)safeFreeCount << 8) |
                                 (uint)safeCapacity;
            ReportBlackBoxSample(encodedState, VoxelBlackBoxChunkStatePoolCorruptionFlag);
        }

        private bool TryEnsureVaultChunkStatePool()
        {
            int dirtyMaskLength = DirtyChunkStatePoolCapacity * ChunkDirtyMaskWordCount;
            int cellLength = DirtyChunkStatePoolCapacity * ChunkCellCount;
            return TryEnsureVaultChunkStatePoolStorage(
                dirtyMaskLength,
                cellLength,
                out _,
                out _,
                out _,
                out _);
        }

        private bool TryResolveChunkStateStorage(
            in ChunkDeltaState state,
            out NativeArray<uint> dirtyMaskWords,
            out NativeArray<ushort> sdfValueBits,
            out NativeArray<byte> materialIds,
            out NativeArray<byte> cellFlags)
        {
            dirtyMaskWords = default;
            sdfValueBits = default;
            materialIds = default;
            cellFlags = default;

            if (!_chunkStatePoolVaultBacked || state.PoolSlot < 0 || state.PoolSlot >= DirtyChunkStatePoolCapacity)
                return false;

            int dirtyMaskLength = DirtyChunkStatePoolCapacity * ChunkDirtyMaskWordCount;
            int cellLength = DirtyChunkStatePoolCapacity * ChunkCellCount;
            if (!TryResolveVaultChunkStatePool(
                    dirtyMaskLength,
                    cellLength,
                    out NativeArray<uint> dirtyMaskPool,
                    out NativeArray<ushort> sdfBitsPool,
                    out NativeArray<byte> materialPool,
                    out NativeArray<byte> flagsPool))
            {
                return false;
            }

            dirtyMaskWords = dirtyMaskPool.GetSubArray(state.PoolSlot * ChunkDirtyMaskWordCount, ChunkDirtyMaskWordCount);
            sdfValueBits = sdfBitsPool.GetSubArray(state.PoolSlot * ChunkCellCount, ChunkCellCount);
            materialIds = materialPool.GetSubArray(state.PoolSlot * ChunkCellCount, ChunkCellCount);
            cellFlags = flagsPool.GetSubArray(state.PoolSlot * ChunkCellCount, ChunkCellCount);
            return true;
        }

        private bool HasChunkStateStorage(in ChunkDeltaState state)
        {
            return _chunkStatePoolVaultBacked &&
                   state.VaultBacked != 0 &&
                   state.PoolSlot >= 0 &&
                   state.PoolSlot < DirtyChunkStatePoolCapacity;
        }

        private bool TryEnsureVaultChunkStatePoolStorage(
            int dirtyMaskLength,
            int cellLength,
            out NativeArray<uint> dirtyMaskPool,
            out NativeArray<ushort> sdfBitsPool,
            out NativeArray<byte> materialPool,
            out NativeArray<byte> flagsPool)
        {
            dirtyMaskPool = default;
            sdfBitsPool = default;
            materialPool = default;
            flagsPool = default;

            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            _chunkStateDirtyMaskPoolHandle = vault.EnsureGenerationHandle<uint>(
                BufferID.ShinobuDeltaCrusherDirtyMaskPool,
                dirtyMaskLength,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            _chunkStateSdfBitsPoolHandle = vault.EnsureGenerationHandle<ushort>(
                BufferID.ShinobuDeltaCrusherSdfBitsPool,
                cellLength,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            _chunkStateMaterialPoolHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuDeltaCrusherMaterialPool,
                cellLength,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);
            _chunkStateCellFlagsPoolHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuDeltaCrusherCellFlagsPool,
                cellLength,
                SystemID.TerrainSeams,
                NativeArrayOptions.ClearMemory);

            return TryResolveVaultChunkStatePool(
                dirtyMaskLength,
                cellLength,
                out dirtyMaskPool,
                out sdfBitsPool,
                out materialPool,
                out flagsPool);
        }

        private bool TryResolveVaultChunkStatePool(
            int dirtyMaskLength,
            int cellLength,
            out NativeArray<uint> dirtyMaskPool,
            out NativeArray<ushort> sdfBitsPool,
            out NativeArray<byte> materialPool,
            out NativeArray<byte> flagsPool)
        {
            dirtyMaskPool = default;
            sdfBitsPool = default;
            materialPool = default;
            flagsPool = default;

            IDataVault vault = ResolveDataVault();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return TryResolveVaultBuffer(vault, in _chunkStateDirtyMaskPoolHandle, BufferID.ShinobuDeltaCrusherDirtyMaskPool, dirtyMaskLength, out dirtyMaskPool) &&
                   TryResolveVaultBuffer(vault, in _chunkStateSdfBitsPoolHandle, BufferID.ShinobuDeltaCrusherSdfBitsPool, cellLength, out sdfBitsPool) &&
                   TryResolveVaultBuffer(vault, in _chunkStateMaterialPoolHandle, BufferID.ShinobuDeltaCrusherMaterialPool, cellLength, out materialPool) &&
                   TryResolveVaultBuffer(vault, in _chunkStateCellFlagsPoolHandle, BufferID.ShinobuDeltaCrusherCellFlagsPool, cellLength, out flagsPool);
        }

        private bool TryLeaseChunkState(int3 chunkCoord, float voxelSize, out ChunkDeltaState state)
        {
            state = default;
            int dirtyMaskLength = DirtyChunkStatePoolCapacity * ChunkDirtyMaskWordCount;
            int cellLength = DirtyChunkStatePoolCapacity * ChunkCellCount;
            if (!_chunkStatePoolCreated ||
                !_chunkStatePoolVaultBacked ||
                !TryResolveVaultChunkStatePool(dirtyMaskLength, cellLength, out _, out _, out _, out _))
            {
                if (!_chunkStatePoolExhaustedWarningArmed)
                {
                    _chunkStatePoolExhaustedWarningArmed = true;
                    WriteChunkStatePoolCorruptionSample(-1, _chunkStateFreeCount, DirtyChunkStatePoolCapacity);
                }

                return false;
            }

            if (_chunkStateFreeCount <= 0)
            {
                if (!_chunkStatePoolExhaustedWarningArmed)
                {
                    _chunkStatePoolExhaustedWarningArmed = true;
                    ReportBlackBoxSample((ulong)(uint)DirtyChunkStatePoolCapacity, VoxelBlackBoxChunkStatePoolExhaustedFlag);
                }

                return false;
            }

            int slot = _chunkStateFreeStack[--_chunkStateFreeCount];
            if (!TryGetChunkStatePoolSlot(slot, out state))
            {
                WriteChunkStatePoolCorruptionSample(slot, _chunkStateFreeCount, DirtyChunkStatePoolCapacity);
                _chunkStateFreeStack.Clear();
                _chunkStateFreeCount = 0;
                _chunkStatePoolExhaustedWarningArmed = true;
                state = default;
                return false;
            }

            state.ResetForLease(chunkCoord, voxelSize);
            ClearChunkStateStorage(in state);
            if (!TrySetChunkStatePoolSlot(slot, in state))
            {
                WriteChunkStatePoolCorruptionSample(slot, _chunkStateFreeCount, DirtyChunkStatePoolCapacity);
                _chunkStateFreeStack.Clear();
                _chunkStateFreeCount = 0;
                _chunkStatePoolExhaustedWarningArmed = true;
                state = default;
                return false;
            }

            return true;
        }

        private void ReleaseChunkState(ChunkDeltaState state)
        {
            if (!state.IsPooled)
            {
                state.Dispose();
                return;
            }

            if (!_chunkStatePoolCreated || state.PoolSlot < 0 || state.PoolSlot >= DirtyChunkStatePoolCapacity)
                return;

            state.ResetForLease(default, MinRuntimeVoxelSize);
            state.DirtyCellCount = 0;
            if (!TrySetChunkStatePoolSlot(state.PoolSlot, in state))
            {
                WriteChunkStatePoolCorruptionSample(state.PoolSlot, _chunkStateFreeCount, DirtyChunkStatePoolCapacity);
                _chunkStateFreeCount = 0;
                _chunkStateFreeStack.Clear();
                _chunkStatePoolExhaustedWarningArmed = true;
                return;
            }

            if (_chunkStateFreeCount < DirtyChunkStatePoolCapacity)
            {
                if (_chunkStateFreeCount < _chunkStateFreeStack.Length)
                    _chunkStateFreeStack[_chunkStateFreeCount] = state.PoolSlot;
                else
                    _chunkStateFreeStack.Add(state.PoolSlot);

                _chunkStateFreeCount++;
            }

            if (_chunkStateFreeCount > DirtyChunkStatePoolCapacity / 4)
                _chunkStatePoolExhaustedWarningArmed = false;
        }

        private void DisposeChunkStatePool()
        {
            DisposeChunkStatePool(ResolveDataVault());
        }

        private void DisposeChunkStatePool(IDataVault vault)
        {
            if (!_chunkStatePoolCreated)
                return;

            for (int i = 0; i < DirtyChunkStatePoolCapacity; i++)
            {
                if (!TryGetChunkStatePoolSlot(i, out ChunkDeltaState state))
                    continue;

                state.Dispose();
                TrySetChunkStatePoolSlot(i, in state);
            }

            _chunkStatePoolBank0.Clear();
            _chunkStatePoolBank1.Clear();
            _chunkStatePoolBank2.Clear();
            _chunkStateFreeStack.Clear();
            _chunkStateFreeCount = 0;
            _chunkStatePoolCreated = false;
            ReleaseChunkStatePoolVaultHandles(vault);
            _chunkStatePoolVaultBacked = false;
            _chunkStatePoolExhaustedWarningArmed = false;
        }

        private void ReleaseChunkStatePoolVaultHandles()
        {
            ReleaseChunkStatePoolVaultHandles(ResolveDataVault());
        }

        private void ReleaseChunkStatePoolVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                if (IsExactVaultHandle(in _chunkStateDirtyMaskPoolHandle, BufferID.ShinobuDeltaCrusherDirtyMaskPool))
                    vault.ReleaseBuffer(in _chunkStateDirtyMaskPoolHandle);
                if (IsExactVaultHandle(in _chunkStateSdfBitsPoolHandle, BufferID.ShinobuDeltaCrusherSdfBitsPool))
                    vault.ReleaseBuffer(in _chunkStateSdfBitsPoolHandle);
                if (IsExactVaultHandle(in _chunkStateMaterialPoolHandle, BufferID.ShinobuDeltaCrusherMaterialPool))
                    vault.ReleaseBuffer(in _chunkStateMaterialPoolHandle);
                if (IsExactVaultHandle(in _chunkStateCellFlagsPoolHandle, BufferID.ShinobuDeltaCrusherCellFlagsPool))
                    vault.ReleaseBuffer(in _chunkStateCellFlagsPoolHandle);
            }

            _chunkStateDirtyMaskPoolHandle = default;
            _chunkStateSdfBitsPoolHandle = default;
            _chunkStateMaterialPoolHandle = default;
            _chunkStateCellFlagsPoolHandle = default;
        }

        private unsafe void ClearChunkStateStorage(in ChunkDeltaState state)
        {
            if (!TryResolveChunkStateStorage(
                    in state,
                    out NativeArray<uint> dirtyMaskWords,
                    out NativeArray<ushort> sdfValueBits,
                    out NativeArray<byte> materialIds,
                    out NativeArray<byte> cellFlags))
            {
                return;
            }

            if (dirtyMaskWords.IsCreated)
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(dirtyMaskWords);
                UnsafeUtility.MemClear(ptr, dirtyMaskWords.Length * UnsafeUtility.SizeOf<uint>());
            }

            if (sdfValueBits.IsCreated)
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sdfValueBits);
                UnsafeUtility.MemClear(ptr, sdfValueBits.Length * UnsafeUtility.SizeOf<ushort>());
            }

            if (materialIds.IsCreated)
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(materialIds);
                UnsafeUtility.MemClear(ptr, materialIds.Length * UnsafeUtility.SizeOf<byte>());
            }

            if (cellFlags.IsCreated)
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(cellFlags);
                UnsafeUtility.MemClear(ptr, cellFlags.Length * UnsafeUtility.SizeOf<byte>());
            }
        }

        private void DisposeCompactedChunkStates()
        {
            for (int slot = 0; slot < _compactedChunkStates.SlotCapacity; slot++)
            {
                if (_compactedChunkStates.TryGetSlot(slot, out _, out CompactedChunkState state))
                    state.Dispose();
            }

            _compactedChunkStates.Clear();
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
            if (sentinelId <= 0)
                throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static unsafe void DisposeTrackedNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private ulong ResolveScheduledCarveVolumeId()
        {
            HectonVoxelVolume volume = _scheduledCarveRequest.Volume;
            return volume != null ? EntityId.ToULong(volume.GetEntityId()) : 0ul;
        }

        private void DeferScheduledCarveBlackBoxSample(ulong focusVolumeId, uint flags)
        {
            if (flags == 0u)
                return;

            if (_deferredScheduledCarveBlackBoxFlags == 0u)
                _deferredScheduledCarveBlackBoxVolumeId = focusVolumeId;

            _deferredScheduledCarveBlackBoxFlags |= flags;
        }

        private void ReportBlackBoxSample(ulong focusVolumeId, uint flags)
        {
            if (_scheduledCarveWritesLocked)
            {
                DeferScheduledCarveBlackBoxSample(focusVolumeId, flags);
                return;
            }

            WriteBlackBoxSample(focusVolumeId, flags);
        }

        private void FlushDeferredScheduledCarveBlackBoxSample()
        {
            uint flags = _deferredScheduledCarveBlackBoxFlags;
            if (flags == 0u)
                return;

            ulong focusVolumeId = _deferredScheduledCarveBlackBoxVolumeId;
            _deferredScheduledCarveBlackBoxVolumeId = 0UL;
            _deferredScheduledCarveBlackBoxFlags = 0u;
            WriteBlackBoxSample(focusVolumeId, flags);
        }

        private void WriteBlackBoxSample(ulong focusVolumeId, uint flags)
        {
            if (!TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<VoxelCarveTelemetryEntry> blackBox))
                return;

            try
            {
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

                VoxelCarveTelemetryEntry entry = default;
                entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                entry.Flags = flags;
                entry.FocusVolumeId = focusVolumeId;
                entry.LastHitAup = lastHitAup;
                entry.TouchedMinX = minCell.x;
                entry.TouchedMinY = minCell.y;
                entry.TouchedMinZ = minCell.z;
                entry.TouchedMaxX = maxCell.x;
                entry.TouchedMaxY = maxCell.y;
                entry.TouchedMaxZ = maxCell.z;
                entry.QueuedCarves = (ushort)math.min(ushort.MaxValue, _queuedCarveEventCount);
                entry.PendingCarves = (ushort)math.min(ushort.MaxValue, _pendingCarveCount);
                entry.ScheduledWrites = (ushort)math.min(ushort.MaxValue, _scheduledCarveWriteCount);
                entry.DirtyChunks = (ushort)math.min(ushort.MaxValue, _chunkStates.Count);
                entry.ScheduledState = scheduledState;
                entry.DrainBudget = (byte)math.min(byte.MaxValue, ResolveQueuedCarveDrainBudget());
                entry.StateHash16 = (ushort)(stateHash ^ (stateHash >> 16));
                blackBox[_blackBoxCursor] = entry;

                _blackBoxCursor++;
                if (_blackBoxCursor >= VoxelBlackBoxCapacity)
                    _blackBoxCursor = 0;
            }
            finally
            {
                ReleaseBlackBoxBuffer(vault);
            }
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
                   request.SliceStartIndex >= 0 &&
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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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
            [FieldOffset(24)] public uint _pad1;
            [FieldOffset(28)] public uint _pad2;
        }

        private void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (_blackBoxDumpedThisActivation)
                return;

            _blackBoxDumpedThisActivation = DumpBlackBox(reasonFlags);
        }

        private bool DumpBlackBox(uint reasonFlags)
        {
            WriteBlackBoxSample(0ul, reasonFlags);
            if (!TryAcquireBlackBoxBuffer(out IDataVault vault, out NativeArray<VoxelCarveTelemetryEntry> blackBox))
                return false;

            try
            {
                bool paging = WriteBlackBoxDumpFile(VoxelPagingBlackBoxDumpRelativePath1312, reasonFlags, blackBox);
                bool primary = WriteBlackBoxDumpFile(VoxelBlackBoxDumpRelativePath, reasonFlags, blackBox);
                return paging && primary;
            }
            finally
            {
                ReleaseBlackBoxBuffer(vault);
            }
        }

        private bool WriteBlackBoxDumpFile(string relativePath, uint reasonFlags, NativeArray<VoxelCarveTelemetryEntry> blackBox)
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
                unsafe
                {
                    VoxelBlackBoxDumpHeader header = default;
                    header.Magic = VoxelBlackBoxDumpMagic;
                    header.Capacity = (uint)VoxelBlackBoxCapacity;
                    header.Stride = (uint)UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>();
                    header.Cursor = (uint)_blackBoxCursor;
                    header.ReasonFlags = reasonFlags;
                    header._pad0 = 0u;
                    header._pad1 = 0u;
                    header._pad2 = 0u;

                    int headerBytes = UnsafeUtility.SizeOf<VoxelBlackBoxDumpHeader>();
                    int entriesBytes = VoxelBlackBoxCapacity * UnsafeUtility.SizeOf<VoxelCarveTelemetryEntry>();
                    int payloadBytes = headerBytes + entriesBytes;
                    NativeArray<byte> payload = new NativeArray<byte>(payloadBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                        UnsafeUtility.MemCpy(payloadPtr, &header, headerBytes);
                        void* entries = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
                        UnsafeUtility.MemCpy(payloadPtr + headerBytes, entries, entriesBytes);
                        return NativeFaultDumpWriter.TryWriteAll(path, payload, payloadBytes);
                    }
                    finally
                    {
                        if (payload.IsCreated)
                            payload.Dispose();
                    }
                }
            }
            catch (IOException)
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
            catch (ObjectDisposedException)
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
            catch (NotSupportedException)
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
            catch (ArgumentException)
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
            catch (InvalidOperationException)
            {
                // Fault-path export must never trigger a second gameplay failure.
            }

            return false;
        }

#if UNITY_EDITOR
        public static bool ValidateAgent1312PrivateLayouts(ref uint failureFlags)
        {
            return ValidateAgent1304PrivateLayouts(ref failureFlags);
        }

        public static bool ValidateAgent1304PrivateLayouts(ref uint failureFlags)
        {
            bool ok = true;
            ok &= AssertAgent1304ExplicitLayout<ThermalMeltEvent>(48, ref failureFlags);
            ok &= AssertAgent1304Offset<ThermalMeltEvent>(nameof(ThermalMeltEvent.AbsoluteUniversePositionDouble), 0, ref failureFlags);
            ok &= AssertAgent1304Offset<ThermalMeltEvent>(nameof(ThermalMeltEvent.AbsoluteUniversePosition), 24, ref failureFlags);
            ok &= AssertAgent1304Offset<ThermalMeltEvent>(nameof(ThermalMeltEvent.RadiusMeters), 36, ref failureFlags);
            ok &= AssertAgent1304Offset<ThermalMeltEvent>(nameof(ThermalMeltEvent.Heat01), 40, ref failureFlags);
            ok &= AssertAgent1304Offset<ThermalMeltEvent>("_pad0", 44, ref failureFlags);

            ok &= AssertAgent1304ExplicitLayout<VoxelBlackBoxDumpHeader>(32, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader.Magic), 0, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader.Capacity), 4, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader.Stride), 8, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader.Cursor), 12, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader.ReasonFlags), 16, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader._pad0), 20, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader._pad1), 24, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelBlackBoxDumpHeader>(nameof(VoxelBlackBoxDumpHeader._pad2), 28, ref failureFlags);

            ok &= AssertAgent1304ExplicitLayout<VoxelCarveTelemetryEntry>(80, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.LastHitAup), 0, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.FocusVolumeId), 24, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.Frame), 32, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.Flags), 36, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.TouchedMinX), 40, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.TouchedMinY), 44, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.TouchedMinZ), 48, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.TouchedMaxX), 52, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.TouchedMaxY), 56, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.TouchedMaxZ), 60, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>("_pad0", 64, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.QueuedCarves), 68, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.PendingCarves), 70, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.ScheduledWrites), 72, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.DirtyChunks), 74, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.StateHash16), 76, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.ScheduledState), 78, ref failureFlags);
            ok &= AssertAgent1304Offset<VoxelCarveTelemetryEntry>(nameof(VoxelCarveTelemetryEntry.DrainBudget), 79, ref failureFlags);

            ok &= AssertAgent1304ExplicitLayout<CarveCellWrite>(32, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.AbsoluteCellX), 0, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.AbsoluteCellY), 4, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.AbsoluteCellZ), 8, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.BlendStrength), 12, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.SdfValueBits), 16, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.MaterialId), 18, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.DeltaFlags), 19, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>(nameof(CarveCellWrite.IsActive), 20, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>("_pad0", 21, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>("_pad1", 22, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>("_pad2", 24, ref failureFlags);
            ok &= AssertAgent1304Offset<CarveCellWrite>("_pad3", 28, ref failureFlags);

            ok &= AssertAgent1304ExplicitLayout<CompactedChunkState>(24, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>(nameof(CompactedChunkState.ChunkCoord), 0, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>(nameof(CompactedChunkState.VoxelSize), 12, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>(nameof(CompactedChunkState.RleSdfValueBits), 16, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>(nameof(CompactedChunkState.IsRleCompressed), 18, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>(nameof(CompactedChunkState.RleMaterialId), 19, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>(nameof(CompactedChunkState.RleCellFlags), 20, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>("_pad0", 21, ref failureFlags);
            ok &= AssertAgent1304Offset<CompactedChunkState>("_pad1", 22, ref failureFlags);

            ok &= AssertAgent1304ExplicitLayout<ChunkDeltaState>(32, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>(nameof(ChunkDeltaState.ChunkCoord), 0, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>(nameof(ChunkDeltaState.VoxelSize), 12, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>(nameof(ChunkDeltaState.DirtyCellCount), 16, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>(nameof(ChunkDeltaState.PoolSlot), 20, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>(nameof(ChunkDeltaState.VaultBacked), 24, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>("_pad0", 25, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>("_pad1", 26, ref failureFlags);
            ok &= AssertAgent1304Offset<ChunkDeltaState>("_pad2", 28, ref failureFlags);

            ok &= AssertAgent1304ExplicitLayout<NativeSnapshotWriteStats>(16, ref failureFlags);
            ok &= AssertAgent1304ExplicitLayout<NativeSnapshotHeader>(16, ref failureFlags);
            ok &= AssertAgent1304ExplicitLayout<LegacyNativeSnapshotHeader>(8, ref failureFlags);
            ok &= AssertAgent1304ExplicitLayout<NativeSnapshotChunkHeader>(24, ref failureFlags);
            ok &= AssertAgent1304ExplicitLayout<NativeSnapshotChunkHeaderRle>(32, ref failureFlags);
            ok &= AssertAgent1304ExplicitLayout<NativeSnapshotChunkHeaderDeltaRle>(40, ref failureFlags);
            ok &= AssertAgent1304ExplicitLayout<ChunkAddress>(8, ref failureFlags);
            return ok;
        }

        private static bool AssertAgent1304ExplicitLayout<T>(int expectedSize, ref uint failureFlags)
            where T : struct
        {
            StructLayoutAttribute layout = typeof(T).StructLayoutAttribute;
            int observedSize = UnsafeUtility.SizeOf<T>();
            bool ok = layout != null &&
                      layout.Value == LayoutKind.Explicit &&
                      observedSize == expectedSize &&
                      (observedSize & 7) == 0;
            if (!ok)
                failureFlags |= 1u;

            return ok;
        }

        private static bool AssertAgent1304Offset<T>(string fieldName, int expectedOffset, ref uint failureFlags)
            where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            int observedOffset = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            bool ok = observedOffset == expectedOffset;
            if (!ok)
                failureFlags |= 1u;

            return ok;
        }
#endif
        #endif

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
            [FieldOffset(64)] private uint _pad0;
            [FieldOffset(68)] public ushort QueuedCarves;
            [FieldOffset(70)] public ushort PendingCarves;
            [FieldOffset(72)] public ushort ScheduledWrites;
            [FieldOffset(74)] public ushort DirtyChunks;
            [FieldOffset(76)] public ushort StateHash16;
            [FieldOffset(78)] public byte ScheduledState;
            [FieldOffset(79)] public byte DrainBudget;
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
            public int SourceSonarVersion;
            public int WriteVersion;
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
            public int SliceStartIndex;
            public float AccumulatedDamage;
            public float ExplicitRadiusMeters;
            public float ExplicitBlendStrength;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte SourceFlags;
            public byte Shape;
            public byte RuntimeFlags;
            public Vector3 AbsoluteImpulseDirection;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CarveSdfJob : IJobParallelFor
        {
            public double3 Center;
            public double3 SegmentEnd;
            public int3 MinCell;
            public int3 Span;
            public int CandidateOffset;
            public float VoxelSize;
            public float Radius;
            public float BlendRadius;
            public float BlendStrength;
            public float3 HalfExtents;
            public byte MaterialId;
            public byte DeltaFlags;
            public byte Shape;
            [NativeDisableParallelForRestriction] public NativeArray<CarveCellWrite> Writes;

            public void Execute(int index)
            {
                if (!Writes.IsCreated || (uint)index >= (uint)Writes.Length)
                    return;

                int candidateIndex = CandidateOffset + index;
                int spanXY = Span.x * Span.y;
                int localZ = candidateIndex / spanXY;
                int remainder = candidateIndex - (localZ * spanXY);
                int localY = remainder / Span.x;
                int localX = remainder - (localY * Span.x);
                int3 absoluteCell = MinCell + new int3(localX, localY, localZ);
                double3 cellCenter = (new double3(absoluteCell.x, absoluteCell.y, absoluteCell.z) + 0.5d) * VoxelSize;
                double signedDistance = Shape == DeltaShapeBox
                    ? BoxSdf(cellCenter - Center, HalfExtents)
                    : Shape == DeltaShapeCapsule
                        ? CapsuleSdf(cellCenter, Center, SegmentEnd, Radius)
                        : SphereSdfApprox(cellCenter - Center, Radius);

                float densityValue = (float)((DeltaFlags & DeltaModeAdditive) != 0
                    ? math.clamp(-signedDistance, -8d, 8d)
                    : math.clamp(signedDistance, -8d, 8d));
                byte isActive = (byte)math.select(1, 0, signedDistance >= BlendRadius);

                Writes[index] = new CarveCellWrite
                {
                    AbsoluteCellX = absoluteCell.x,
                    AbsoluteCellY = absoluteCell.y,
                    AbsoluteCellZ = absoluteCell.z,
                    SdfValueBits = (ushort)math.f32tof16(densityValue),
                    MaterialId = MaterialId,
                    DeltaFlags = DeltaFlags,
                    BlendStrength = BlendStrength,
                    IsActive = isActive
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

            /// <summary>
            /// R99: exact Euclidean length. This used to return
            /// <c>cmax(|v|) + (|x|+|y|+|z|) * 0.33</c>, which is NOT a distance — it is a Chebyshev/Manhattan
            /// blend whose unit isosurface is an octahedron-cornered box, not a sphere. Directional error
            /// ranged from +14.8% along an axis to -0.6% along the diagonal, so a "spherical" 4 m tool carve
            /// came out roughly 0.6 m out of round, and the SDF gradient (used for the carve-boundary
            /// normals) pointed wrong everywhere except on the axes. Every consumer of these primitives —
            /// box, capsule, sphere — was affected. math.length is exact and Burst-vectorized; the cost is
            /// one sqrt inside a bounded per-carve loop.
            /// </summary>
            private static double AxisWeightedLengthApprox(double3 value)
            {
                return math.length(value);
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

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct CompactedChunkState : IDisposable
        {
            [FieldOffset(0)] public readonly int3 ChunkCoord;
            [FieldOffset(12)] public readonly float VoxelSize;
            [FieldOffset(16)] public ushort RleSdfValueBits;
            [FieldOffset(18)] public byte IsRleCompressed;
            [FieldOffset(19)] public byte RleMaterialId;
            [FieldOffset(20)] public byte RleCellFlags;
            [FieldOffset(21)] private byte _pad0;
            [FieldOffset(22)] private ushort _pad1;

            public CompactedChunkState(
                int3 chunkCoord,
                float voxelSize,
                ushort rleSdfValueBits,
                byte rleMaterialId,
                byte rleCellFlags)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                RleSdfValueBits = rleSdfValueBits;
                IsRleCompressed = 1;
                RleMaterialId = rleMaterialId;
                RleCellFlags = rleCellFlags;
                _pad0 = 0;
                _pad1 = 0;
            }

            public ushort GetSdfValueBits(int flatIndex)
            {
                return RleSdfValueBits;
            }

            public byte GetMaterialId(int flatIndex)
            {
                return RleMaterialId;
            }

            public byte GetCellFlags(int flatIndex)
            {
                return RleCellFlags;
            }

            public void Dispose()
            {
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct VoxelDeltaCopyChunkStateJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<uint> SourceDirtyMaskWords;
            [ReadOnly] public NativeArray<ushort> SourceSdfValueBits;
            [ReadOnly] public NativeArray<byte> SourceMaterialIds;
            [ReadOnly] public NativeArray<byte> SourceCellFlags;
            [NativeDisableParallelForRestriction] public NativeArray<uint> DestinationDirtyMaskWords;
            [WriteOnly] public NativeArray<ushort> DestinationSdfValueBits;
            [WriteOnly] public NativeArray<byte> DestinationMaterialIds;
            [WriteOnly] public NativeArray<byte> DestinationCellFlags;

            public void Execute(int index)
            {
                if ((uint)index >= ChunkCellCount)
                    return;

                DestinationSdfValueBits[index] = SourceSdfValueBits[index];
                DestinationMaterialIds[index] = SourceMaterialIds[index];
                DestinationCellFlags[index] = SourceCellFlags[index];

                if (index < ChunkDirtyMaskWordCount)
                    DestinationDirtyMaskWords[index] = SourceDirtyMaskWords[index];
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

        private static bool IsSupportedNativeSnapshotStorageFlags(byte storageFlags)
        {
            return storageFlags == NativeSnapshotStorageDense ||
                   storageFlags == NativeSnapshotStorageUniformSdfRle ||
                   storageFlags == NativeSnapshotStorageSparseDeltaRle;
        }

        private static int AddNativeSnapshotDirtyCellCountClamped(int current, int add)
        {
            if (add <= 0)
                return math.max(0, current);

            return current > int.MaxValue - add
                ? int.MaxValue
                : current + add;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct NativeSnapshotWriteStats
        {
            [FieldOffset(0)] public int TotalBytes;
            [FieldOffset(4)] public int ChunkCount;
            [FieldOffset(8)] public int TotalDirtyCellCount;
            [FieldOffset(12)] public int Reserved0;
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

        private struct FixedChunkRegistry<T> where T : unmanaged
        {
            private FixedList4096Bytes<ChunkAddress> _keys;
            private FixedList4096Bytes<T> _values0;
            private FixedList4096Bytes<T> _values1;
            private FixedList4096Bytes<T> _values2;
            private FixedList4096Bytes<T> _values3;
            private FixedList4096Bytes<byte> _occupied;
            private int _count;
            private byte _initialized;

            public int Count => _count;

            public int SlotCapacity => InitialChunkRegistryCapacity;

            public bool ContainsKey(ChunkAddress key)
            {
                if (_initialized == 0)
                    return false;

                return FindSlot(key) >= 0;
            }

            public bool TryGetValue(ChunkAddress key, out T value)
            {
                if (_initialized == 0)
                {
                    value = default;
                    return false;
                }

                int slot = FindSlot(key);
                if (slot >= 0)
                    return TryGetValueSlot(slot, out value);

                value = default;
                return false;
            }

            public bool TrySet(ChunkAddress key, T value, out T previous, out bool hadPrevious)
            {
                if (!EnsureInitialized())
                {
                    previous = default;
                    hadPrevious = false;
                    return false;
                }

                int slot = FindSlot(key);
                if (slot >= 0)
                {
                    if (!TryGetValueSlot(slot, out previous) || !TrySetValueSlot(slot, value))
                    {
                        previous = default;
                        hadPrevious = false;
                        return false;
                    }

                    hadPrevious = true;
                    return true;
                }

                slot = FindFreeSlot();
                if (slot < 0)
                {
                    previous = default;
                    hadPrevious = false;
                    return false;
                }

                _keys[slot] = key;
                if (!TrySetValueSlot(slot, value))
                {
                    previous = default;
                    hadPrevious = false;
                    return false;
                }

                _occupied[slot] = 1;
                _count++;
                previous = default;
                hadPrevious = false;
                return true;
            }

            public bool TryAdd(ChunkAddress key, T value)
            {
                if (!EnsureInitialized())
                    return false;

                if (ContainsKey(key))
                    return false;

                int slot = FindFreeSlot();
                if (slot < 0)
                    return false;

                _keys[slot] = key;
                if (!TrySetValueSlot(slot, value))
                    return false;

                _occupied[slot] = 1;
                _count++;
                return true;
            }

            public bool TryRemove(ChunkAddress key, out T value)
            {
                if (!EnsureInitialized())
                {
                    value = default;
                    return false;
                }

                int slot = FindSlot(key);
                if (slot < 0)
                {
                    value = default;
                    return false;
                }

                if (!TryGetValueSlot(slot, out value))
                {
                    value = default;
                    return false;
                }

                TrySetValueSlot(slot, default);
                _keys[slot] = default;
                _occupied[slot] = 0;
                _count--;
                return true;
            }

            public bool Remove(ChunkAddress key)
            {
                return TryRemove(key, out _);
            }

            public void Clear()
            {
                if (!EnsureInitialized())
                    return;

                for (int i = 0; i < _occupied.Length; i++)
                {
                    _occupied[i] = 0;
                    _keys[i] = default;
                    TrySetValueSlot(i, default);
                }

                _count = 0;
            }

            public bool TryGetSlot(int slot, out ChunkAddress key, out T value)
            {
                if (_initialized == 0)
                {
                    key = default;
                    value = default;
                    return false;
                }

                if ((uint)slot >= InitialChunkRegistryCapacity || _occupied[slot] == 0)
                {
                    key = default;
                    value = default;
                    return false;
                }

                key = _keys[slot];
                return TryGetValueSlot(slot, out value);
            }

            private int FindSlot(ChunkAddress key)
            {
                for (int i = 0; i < InitialChunkRegistryCapacity; i++)
                {
                    if (_occupied[i] != 0 && _keys[i].Equals(key))
                        return i;
                }

                return -1;
            }

            private int FindFreeSlot()
            {
                for (int i = 0; i < InitialChunkRegistryCapacity; i++)
                {
                    if (_occupied[i] == 0)
                        return i;
                }

                return -1;
            }

            private bool EnsureInitialized()
            {
                if (_initialized != 0)
                    return true;

                _keys.Clear();
                _occupied.Clear();
                _values0.Clear();
                _values1.Clear();
                _values2.Clear();
                _values3.Clear();
                _count = 0;

                for (int i = 0; i < InitialChunkRegistryCapacity; i++)
                {
                    if (_keys.Length >= _keys.Capacity ||
                        _occupied.Length >= _occupied.Capacity ||
                        !TryAddValueSlot(default))
                    {
                        _keys.Clear();
                        _occupied.Clear();
                        _values0.Clear();
                        _values1.Clear();
                        _values2.Clear();
                        _values3.Clear();
                        return false;
                    }

                    _keys.Add(default);
                    _occupied.Add(0);
                }

                _initialized = 1;
                return true;
            }

            private bool TryAddValueSlot(T value)
            {
                if (_values0.Length < _values0.Capacity)
                {
                    _values0.Add(value);
                    return true;
                }

                if (_values1.Length < _values1.Capacity)
                {
                    _values1.Add(value);
                    return true;
                }

                if (_values2.Length < _values2.Capacity)
                {
                    _values2.Add(value);
                    return true;
                }

                if (_values3.Length < _values3.Capacity)
                {
                    _values3.Add(value);
                    return true;
                }

                return false;
            }

            private bool TryGetValueSlot(int slot, out T value)
            {
                if ((uint)slot >= InitialChunkRegistryCapacity)
                {
                    value = default;
                    return false;
                }

                int bank0Capacity = _values0.Capacity;
                if (slot < bank0Capacity)
                {
                    if (slot >= _values0.Length)
                    {
                        value = default;
                        return false;
                    }

                    value = _values0[slot];
                    return true;
                }

                slot -= bank0Capacity;
                int bank1Capacity = _values1.Capacity;
                if (slot < bank1Capacity)
                {
                    if (slot >= _values1.Length)
                    {
                        value = default;
                        return false;
                    }

                    value = _values1[slot];
                    return true;
                }

                slot -= bank1Capacity;
                int bank2Capacity = _values2.Capacity;
                if (slot < bank2Capacity)
                {
                    if (slot >= _values2.Length)
                    {
                        value = default;
                        return false;
                    }

                    value = _values2[slot];
                    return true;
                }

                slot -= bank2Capacity;
                if (slot >= _values3.Length)
                {
                    value = default;
                    return false;
                }

                value = _values3[slot];
                return true;
            }

            private bool TrySetValueSlot(int slot, T value)
            {
                if ((uint)slot >= InitialChunkRegistryCapacity)
                    return false;

                int bank0Capacity = _values0.Capacity;
                if (slot < bank0Capacity)
                {
                    if (slot >= _values0.Length)
                        return false;

                    _values0[slot] = value;
                    return true;
                }

                slot -= bank0Capacity;
                int bank1Capacity = _values1.Capacity;
                if (slot < bank1Capacity)
                {
                    if (slot >= _values1.Length)
                        return false;

                    _values1[slot] = value;
                    return true;
                }

                slot -= bank1Capacity;
                int bank2Capacity = _values2.Capacity;
                if (slot < bank2Capacity)
                {
                    if (slot >= _values2.Length)
                        return false;

                    _values2[slot] = value;
                    return true;
                }

                slot -= bank2Capacity;
                if (slot >= _values3.Length)
                    return false;

                _values3[slot] = value;
                return true;
            }
        }

        private enum VolumeRegistryLane : byte
        {
            Registered = 0,
            PendingRebuild = 1
        }

        private struct FixedVolumeRegistry
        {
            private HectonVoxelVolume _head;
            private int _count;
            private readonly int _capacity;
            private readonly VolumeRegistryLane _lane;

            public FixedVolumeRegistry(int capacity, VolumeRegistryLane lane)
            {
                _head = null;
                _count = 0;
                _capacity = math.max(1, capacity);
                _lane = lane;
            }

            public int Count => _count;

            public int Capacity => _capacity;

            public HectonVoxelVolume this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_count)
                        return null;

                    HectonVoxelVolume node = _head;
                    for (int i = 0; node != null && i < index; i++)
                        node = GetNext(node);

                    return node;
                }
            }

            public void Clear()
            {
                while (_head != null)
                    RemoveNode(_head);
            }

            public bool TryAdd(HectonVoxelVolume volume)
            {
                if (volume == null || Contains(volume) || _count >= _capacity)
                    return false;

                SetPrevious(volume, null);
                SetNext(volume, _head);
                if (_head != null)
                    SetPrevious(_head, volume);

                _head = volume;
                SetRegistered(volume, true);
                _count++;
                return true;
            }

            public bool Contains(HectonVoxelVolume volume)
            {
                return volume != null && IsRegistered(volume);
            }

            public bool Remove(HectonVoxelVolume volume)
            {
                if (!Contains(volume))
                    return false;

                RemoveNode(volume);
                return true;
            }

            public void RemoveAtSwapBack(int index)
            {
                if ((uint)index >= (uint)_count)
                    return;

                HectonVoxelVolume node = this[index];
                if (node != null)
                    RemoveNode(node);
            }

            private void RemoveNode(HectonVoxelVolume node)
            {
                HectonVoxelVolume previous = GetPrevious(node);
                HectonVoxelVolume next = GetNext(node);

                if (previous != null)
                    SetNext(previous, next);
                else
                    _head = next;

                if (next != null)
                    SetPrevious(next, previous);

                SetNext(node, null);
                SetPrevious(node, null);
                SetRegistered(node, false);
                _count = math.max(0, _count - 1);
            }

            private HectonVoxelVolume GetNext(HectonVoxelVolume volume)
            {
                return _lane == VolumeRegistryLane.Registered
                    ? volume._deltaRegisteredNext
                    : volume._deltaPendingRebuildNext;
            }

            private HectonVoxelVolume GetPrevious(HectonVoxelVolume volume)
            {
                return _lane == VolumeRegistryLane.Registered
                    ? volume._deltaRegisteredPrev
                    : volume._deltaPendingRebuildPrev;
            }

            private void SetNext(HectonVoxelVolume volume, HectonVoxelVolume next)
            {
                if (_lane == VolumeRegistryLane.Registered)
                    volume._deltaRegisteredNext = next;
                else
                    volume._deltaPendingRebuildNext = next;
            }

            private void SetPrevious(HectonVoxelVolume volume, HectonVoxelVolume previous)
            {
                if (_lane == VolumeRegistryLane.Registered)
                    volume._deltaRegisteredPrev = previous;
                else
                    volume._deltaPendingRebuildPrev = previous;
            }

            private bool IsRegistered(HectonVoxelVolume volume)
            {
                return _lane == VolumeRegistryLane.Registered
                    ? volume._deltaRegistered
                    : volume._deltaPendingRebuildRegistered;
            }

            private void SetRegistered(HectonVoxelVolume volume, bool registered)
            {
                if (_lane == VolumeRegistryLane.Registered)
                    volume._deltaRegistered = registered;
                else
                    volume._deltaPendingRebuildRegistered = registered;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ChunkDeltaState : IDisposable
        {
            [FieldOffset(0)] public int3 ChunkCoord;
            [FieldOffset(12)] public float VoxelSize;
            [FieldOffset(16)] public int DirtyCellCount;
            [FieldOffset(20)] public int PoolSlot;
            [FieldOffset(24)] public byte VaultBacked;
            [FieldOffset(25)] private byte _pad0;
            [FieldOffset(26)] private ushort _pad1;
            [FieldOffset(28)] private uint _pad2;

            public bool IsPooled => VaultBacked != 0 && PoolSlot >= 0;

            public ChunkDeltaState(int3 chunkCoord, float voxelSize, int poolSlot)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                DirtyCellCount = 0;
                PoolSlot = poolSlot;
                VaultBacked = 1;
                _pad0 = 0;
                _pad1 = 0;
                _pad2 = 0;
            }

            public void ResetForLease(int3 chunkCoord, float voxelSize)
            {
                ChunkCoord = chunkCoord;
                VoxelSize = voxelSize;
                DirtyCellCount = 0;
            }

            public void Dispose()
            {
                ChunkCoord = default;
                VoxelSize = MinRuntimeVoxelSize;
                DirtyCellCount = 0;
                PoolSlot = -1;
                VaultBacked = 0;
                _pad0 = 0;
                _pad1 = 0;
                _pad2 = 0;
            }
        }

    }
}
