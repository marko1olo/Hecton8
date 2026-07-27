using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4130)]
    public unsafe sealed class TerrainChunkPagerRuntime : MonoBehaviour, IFrostTickable, IDisposable, IGlobalRegistryHotSwapListener
    {
        private const string WorkerName = "H8_Terrain_Pager";
        private const string DefaultChunkRootRelativePath = "Hecton8/TerrainChunks";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1305_TerrainChunkPager.bin";
        private const string StreamingProfileCsvRelativePath = "Docs/Tasks/streaming_hardware_profiles.csv";
        private const ulong HectonDumpMagic = 0x00384E4F54434548UL;
        private const uint DumpVersion = 1305u;
        private const int DumpHeaderBytes = 32;
        private const uint DumpLayoutHash = 0x44504354u; // TCPD
        private const string DumpPayloadLabel = "terrainChunkPagerTelemetryDumpPayload";
        private const int WorkerShutdownWaitMilliseconds = 2000;

        // R97: VisualSync ticks a transiently-failed sector stays parked before its slot is
        // released for a natural residency re-request (~4 s at 60 FPS; byte-sized field).
        private const byte TransientChunkRetryBackoffTicks = 240;

        private const BufferID MetadataBufferId = BufferID.TerrainChunkPagerRuntime_MetadataBufferId;
        private const BufferID SectorCoordsBufferId = BufferID.TerrainChunkPagerRuntime_SectorCoordsBufferId;
        private const BufferID StagingBytesBufferId = BufferID.TerrainChunkPagerRuntime_StagingBytesBufferId;
        private const BufferID ActiveBytesBufferId = BufferID.TerrainChunkPagerRuntime_ActiveBytesBufferId;
        private const BufferID CompressedScratchBufferId = BufferID.TerrainChunkPagerRuntime_CompressedScratchBufferId;
        private const BufferID WorkerRequestBufferId = BufferID.TerrainChunkPagerRuntime_WorkerRequestBufferId;
        private const BufferID WorkerResultBufferId = BufferID.TerrainChunkPagerRuntime_WorkerResultBufferId;
        private const BufferID JobLoadRequestsBufferId = BufferID.TerrainChunkPagerRuntime_JobLoadRequestsBufferId;
        private const BufferID JobLoadCountBufferId = BufferID.TerrainChunkPagerRuntime_JobLoadCountBufferId;
        private const BufferID JobStaleSlotsBufferId = BufferID.TerrainChunkPagerRuntime_JobStaleSlotsBufferId;
        private const BufferID JobStaleCountBufferId = BufferID.TerrainChunkPagerRuntime_JobStaleCountBufferId;
        private const BufferID TelemetryRingBufferId = BufferID.TerrainChunkPagerRuntime_TelemetryRingBufferId;
        private const BufferID TuningBufferId = BufferID.TerrainChunkPagerRuntime_TuningBufferId;
        private const BufferID CountersBufferId = BufferID.TerrainChunkPagerRuntime_CountersBufferId;
        private const BufferID FreedSlotsBufferId = BufferID.TerrainChunkPagerRuntime_FreedSlotsBufferId;
        private const BufferID FreedCountBufferId = BufferID.TerrainChunkPagerRuntime_FreedCountBufferId;
        private const BufferID HardwareProfilesBufferId = BufferID.TerrainChunkPagerRuntime_HardwareProfilesBufferId;
        private const BufferID CsvScratchBufferId = BufferID.TerrainChunkPagerRuntime_CsvScratchBufferId;
        private const BufferID TelemetryDumpSnapshotBufferId = BufferID.TerrainChunkPagerRuntime_TelemetryDumpSnapshotBufferId;

        private const ulong MetadataBit = 1UL << 0;
        private const ulong SectorCoordsBit = 1UL << 1;
        private const ulong StagingBytesBit = 1UL << 2;
        private const ulong ActiveBytesBit = 1UL << 3;
        private const ulong CompressedScratchBit = 1UL << 4;
        private const ulong WorkerRequestBit = 1UL << 5;
        private const ulong WorkerResultBit = 1UL << 6;
        private const ulong JobLoadRequestsBit = 1UL << 7;
        private const ulong JobLoadCountBit = 1UL << 8;
        private const ulong JobStaleSlotsBit = 1UL << 9;
        private const ulong JobStaleCountBit = 1UL << 10;
        private const ulong TelemetryRingBit = 1UL << 11;
        private const ulong TuningBit = 1UL << 12;
        private const ulong CountersBit = 1UL << 13;
        private const ulong FreedSlotsBit = 1UL << 14;
        private const ulong FreedCountBit = 1UL << 15;
        private const ulong HardwareProfilesBit = 1UL << 16;
        private const ulong CsvScratchBit = 1UL << 17;
        private const ulong TelemetryDumpSnapshotBit = 1UL << 18;
        private const ulong RequiredVaultMask = (1UL << 19) - 1UL;

        private static readonly ProfilerMarker _preSimulationMarker = new ProfilerMarker("H8.World.TerrainChunkPager.PreSimulation");
        private static readonly ProfilerMarker _postSimulationMarker = new ProfilerMarker("H8.World.TerrainChunkPager.PostSimulation");
        private static readonly ProfilerMarker _visualSyncMarker = new ProfilerMarker("H8.World.TerrainChunkPager.VisualSync");
        private static TerrainChunkPagerRuntime s_active;

        [Header("Terrain Pager")]
        [SerializeField, Min(1)] private int maxChunkSlots = TerrainChunkPagerConstants.DefaultMaxChunkSlots;
        [SerializeField, Min(2)] private int queueCapacity = TerrainChunkPagerConstants.DefaultQueueCapacity;
        [SerializeField, Min(4096)] private int chunkByteCapacity = TerrainChunkPagerConstants.DefaultChunkBytes;
        [SerializeField] private string chunkRootRelativePath = DefaultChunkRootRelativePath;
        [SerializeField] private bool forceMockDiskIo = false;
        [SerializeField] private bool loadCsvProfileOnEnable = true;
        [SerializeField] private bool useMockCameraAupWhenNoPlayer = true;
        [SerializeField] private Vector3 mockCameraAupMeters;

#if UNITY_EDITOR
        [Header("Editor Diagnostics")]
        [SerializeField] private bool drawDebugGizmos = true;
        [SerializeField, Min(1f)] private float debugGizmoHeightMeters = 8f;
#endif

        private readonly PreSimulationPhaseSystem _preSimulationPhase;
        private readonly PostSimulationPhaseSystem _postSimulationPhase;
        private readonly VisualSyncPhaseSystem _visualSyncPhase;

        private IDataVault _vault;
        private IDataVault _pendingLifecycleRebindVault;
        private VaultGenerationHandle<ChunkMetadataDTO> _metadataHandle;
        private VaultGenerationHandle<TerrainChunkSectorCoordDTO> _sectorCoordsHandle;
        private VaultGenerationHandle<byte> _stagingBytesHandle;
        private VaultGenerationHandle<byte> _activeBytesHandle;
        private VaultGenerationHandle<byte> _compressedScratchBytesHandle;
        private VaultGenerationHandle<TerrainChunkWorkerRequestDTO> _workerRequestsHandle;
        private VaultGenerationHandle<TerrainChunkWorkerResultDTO> _workerResultsHandle;
        private VaultGenerationHandle<TerrainChunkWorkerRequestDTO> _jobLoadRequestsHandle;
        private VaultGenerationHandle<int> _jobLoadCountHandle;
        private VaultGenerationHandle<int> _jobStaleSlotsHandle;
        private VaultGenerationHandle<int> _jobStaleCountHandle;
        private VaultGenerationHandle<PagerTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<TerrainChunkPagerTuningDTO> _tuningHandle;
        private VaultGenerationHandle<TerrainChunkPagerCountersDTO> _countersHandle;
        private VaultGenerationHandle<int> _freedSlotsHandle;
        private VaultGenerationHandle<int> _freedCountHandle;
        private VaultGenerationHandle<StreamingHardwareProfileDTO> _hardwareProfilesHandle;
        private VaultGenerationHandle<byte> _csvScratchBytesHandle;
        private VaultGenerationHandle<byte> _telemetryDumpSnapshotBytesHandle;

        private int _metadataLength;
        private int _sectorCoordsLength;
        private int _stagingByteLength;
        private int _activeByteLength;
        private int _compressedScratchByteLength;
        private int _workerRequestLength;
        private int _workerResultLength;
        private int _jobLoadRequestLength;
        private int _jobLoadCountLength;
        private int _jobStaleSlotLength;
        private int _jobStaleCountLength;
        private int _telemetryLength;
        private int _tuningLength;
        private int _countersLength;
        private int _freedSlotLength;
        private int _freedCountLength;
        private int _hardwareProfileLength;
        private int _csvScratchByteLength;
        private int _telemetryDumpSnapshotByteLength;

        private AutoResetEvent _workerWake;
        private Thread _workerThread;
        private char[] _pathBuffer;
        private byte[] _utf8PathBuffer;
        private string _chunkRootFullPath;
        private int _workerRunning;
        private int _forceMockDiskIo;
        private int _requestHead;
        private int _requestTail;
        private int _resultHead;
        private int _resultTail;
        private int _queueMask;
        private int _allocatedChunkByteCapacity;
        private int _allocatedCompressedChunkByteCapacity;
        private int _chunkSlabByteLength;
        private int _compressedSlabByteLength;
        private int _dumpSnapshotByteLength;
        private int _registeredPreSimulation;
        private int _registeredPostSimulation;
        private int _registeredVisualSync;
        private int _registeredFrost;
        private int _registeredHotSwap;
        private int _initialized;
        private int _disposed;
        private int _telemetryCursor;
        private int _csvProfileCount;
        private int _validatedVaultBuffers;
        private int _lastDumpFrame;
        private long _dumpRequestPacked;
        private uint _lastDumpFaultFlags;
        private uint _faultFlags;
        private uint _workerSequence;
        private uint _lastEvalMicros;
        private uint _frameId;
        private uint _layoutValid;
        private ulong _vaultBackedMask;
        private IPlayerRuntimeContext _cachedRuntimeContext;
        private double3 _lastCameraAup;
        private long _lastCameraSectorX;
        private long _lastCameraSectorZ;
        private JobHandle _pendingResidencyHandle;
        private JobHandle _pendingEvictionHandle;
        private long _pendingResidencyStartTimestamp;
        private int _pendingResidency;
        private int _pendingEviction;
        private uint _evictedChunksTotal;
        private int _deferredShutdown;
        private int _cameraAupSequence;
        private long _cameraAupBitsX;
        private long _cameraAupBitsY;
        private long _cameraAupBitsZ;
        private int _workerThreadActive;
        private long _workerHeartbeatTimestamp;

        public TerrainChunkPagerRuntime()
        {
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        public static bool TryReadTuning(out TerrainChunkPagerTuningDTO tuning)
        {
            TerrainChunkPagerRuntime active = s_active;
            if (active == null ||
                active._initialized == 0 ||
                !active.TryReadOnlyArray(in active._tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningBuffer))
            {
                tuning = default;
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TryWriteTuning(in TerrainChunkPagerTuningDTO tuning)
        {
            TerrainChunkPagerRuntime active = s_active;
            if (active == null ||
                active._initialized == 0 ||
                !active.TryAcquireWriteArray(in active._tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO> tuningBuffer, out IDataVault tuningVault))
            {
                return false;
            }

            try
            {
                TerrainChunkPagerTuningDTO sanitized = TerrainChunkPagerMath.Sanitize(tuning);
                sanitized.ChunkByteCapacity = active._allocatedChunkByteCapacity;
                sanitized.MaxQueuedLoads = math.clamp(sanitized.MaxQueuedLoads, 1, math.max(1, active.queueCapacity - 1));
                sanitized.CommitByteBudgetPerFrame = math.min(
                    sanitized.CommitByteBudgetPerFrame,
                    ResolveCommitByteBudget(active._allocatedChunkByteCapacity, sanitized.MaxCommitsPerVisualSync));
                tuningBuffer[0] = sanitized;
                return true;
            }
            finally
            {
                ReleaseWriteArray(tuningVault, in active._tuningHandle);
            }
        }

        public static bool TryPublishCameraAup(double3 cameraAup)
        {
            TerrainChunkPagerRuntime active = s_active;
            if (active == null || active._initialized == 0 || !math.all(math.isfinite(cameraAup)))
                return false;

            int sequence = Volatile.Read(ref active._cameraAupSequence);
            Volatile.Write(ref active._cameraAupSequence, sequence | 1);
            Volatile.Write(ref active._cameraAupBitsX, math.aslong(cameraAup.x));
            Volatile.Write(ref active._cameraAupBitsY, math.aslong(cameraAup.y));
            Volatile.Write(ref active._cameraAupBitsZ, math.aslong(cameraAup.z));
            Volatile.Write(ref active._cameraAupSequence, (sequence + 2) & ~1);
            return true;
        }

        public void BindRuntimeContext(PlayerRuntimeContext runtimeContext)
        {
            _ = runtimeContext;
            BindRuntimeContext(PlayerRuntimeContextService.ActiveRuntimeContext);
        }

        public void BindRuntimeContext(IPlayerRuntimeContext runtimeContext)
        {
            _cachedRuntimeContext = IsPlayerRuntimeContextBound(runtimeContext) ? runtimeContext : null;
        }

        public static bool TryReadCounters(out TerrainChunkPagerCountersDTO counters)
        {
            TerrainChunkPagerRuntime active = s_active;
            if (active == null ||
                active._initialized == 0 ||
                !active.TryReadOnlyArray(in active._countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO>.ReadOnly countersBuffer))
            {
                counters = default;
                return false;
            }

            counters = countersBuffer[0];
            return true;
        }

        public static bool TryGetDebugCell(int index, out ChunkMetadataDTO metadata, out TerrainChunkSectorCoordDTO sectorCoord, out int count)
        {
            TerrainChunkPagerRuntime active = s_active;
            if (active == null ||
                active._initialized == 0 ||
                !active.TryReadOnlyArray(in active._metadataHandle, 1, out NativeArray<ChunkMetadataDTO>.ReadOnly metadataBuffer) ||
                !active.TryReadOnlyArray(in active._sectorCoordsHandle, 1, out NativeArray<TerrainChunkSectorCoordDTO>.ReadOnly sectorCoordBuffer))
            {
                metadata = default;
                sectorCoord = default;
                count = 0;
                return false;
            }

            int metadataCount = active._metadataLength > 0 ? math.min(active._metadataLength, metadataBuffer.Length) : metadataBuffer.Length;
            int sectorCoordCount = active._sectorCoordsLength > 0 ? math.min(active._sectorCoordsLength, sectorCoordBuffer.Length) : sectorCoordBuffer.Length;
            count = math.min(metadataCount, sectorCoordCount);
            if ((uint)index >= (uint)count)
            {
                metadata = default;
                sectorCoord = default;
                return false;
            }

            metadata = metadataBuffer[index];
            sectorCoord = sectorCoordBuffer[index];
            return true;
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        public void Dispose()
        {
            Shutdown();
        }

        public void FrostTick()
        {
            if (_initialized == 0)
                return;

            if (!TryFinalizeEviction() || !TryFinalizeResidencyEvaluation())
                return;

            if (!TryResolveArray(in _metadataHandle, _metadataLength, out NativeArray<ChunkMetadataDTO> metadata) ||
                !TryResolveArray(in _sectorCoordsHandle, _sectorCoordsLength, out NativeArray<TerrainChunkSectorCoordDTO> sectorCoords) ||
                !TryResolveArray(in _freedSlotsHandle, _freedSlotLength, out NativeArray<int> freedSlots) ||
                !TryResolveArray(in _freedCountHandle, _freedCountLength, out NativeArray<int> freedCount))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            TerrainChunkPagerTuningDTO tuning = TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningView)
                ? TerrainChunkPagerMath.Sanitize(tuningView[0])
                : TerrainChunkPagerTuningDTO.CreateDefault();

            EvictStaleChunksJob job = default;
            job.Metadata = metadata;
            job.SectorCoords = sectorCoords;
            job.MetadataCapacity = math.min(_metadataLength, math.min(metadata.Length, sectorCoords.Length));
            job.FreedSlots = freedSlots;
            job.FreedSlotCount = freedCount;
            job.CameraSectorX = _lastCameraSectorX;
            job.CameraSectorZ = _lastCameraSectorZ;
            job.SectorSizeMeters = tuning.SectorSizeMeters;
            job.CullRadiusSectors = TerrainChunkPagerMath.ResolveCullRadiusSectors(
                tuning.EffectiveRingRadius,
                tuning.EvictionHysteresisSectors);
            _pendingEvictionHandle = job.Schedule();
            _pendingEviction = 1;
            H8Memory.RegisterActiveJob(SystemID.WorldStreaming, _pendingEvictionHandle);
        }

        private void Initialize()
        {
            if (_initialized != 0)
                return;

            // R100 FIX: never re-initialise over a teardown that could not finish. A latched deferred
            // shutdown means Shutdown failed to fence its jobs or join its worker and DELIBERATELY kept
            // those handles live. Proceeding past this point would:
            //  - run ResetRuntimeStateCounters, overwriting a still-running JobHandle with default. That
            //    orphans a job writing the vault metadata slab, which AllocateNativeState then re-Ensures
            //    with NativeArrayOptions.ClearMemory and ReleaseNativeState can later free underneath it.
            //    Once the handle is gone nothing on this side can ever fence that job again.
            //  - revive the previous worker via StartWorker's Volatile.Write(_workerRunning, 1) while the
            //    old thread is still inside ProcessWorkerRequest, and overwrite _workerWake/_workerThread
            //    (leaking the old AutoResetEvent). The request ring is single-consumer and its dequeue is
            //    a plain read plus Interlocked.Exchange, not a CAS, so two consumers read the same tail
            //    and the same chunk is loaded twice.
            // Bail out instead and let the VisualSyncTick recovery lane reclaim first; it calls
            // TryReleaseDeferredShutdownState every tick and re-enters Initialize once the state is clean.
            // Do NOT call TryReleaseDeferredShutdownState here: it tail-calls Initialize itself.
            if (Volatile.Read(ref _deferredShutdown) != 0 || Volatile.Read(ref _workerThreadActive) != 0)
                return;

            _disposed = 0;
            Volatile.Write(ref _deferredShutdown, 0);
            _faultFlags = 0u;
            _lastDumpFaultFlags = 0u;
            _layoutValid = ChunkMetadataLayoutGuard.ValidateLayout() ? 1u : 0u;
            ResetRuntimeStateCounters();
            _vault = GlobalRegistry.DataVault;
            queueCapacity = NextPowerOfTwo(math.max(2, queueCapacity));
            _queueMask = queueCapacity - 1;
            maxChunkSlots = math.max(1, maxChunkSlots);
            chunkByteCapacity = math.max(4096, chunkByteCapacity);
            if (!TryResolveLz4BoundedCapacity(chunkByteCapacity, out int compressedChunkCapacity) ||
                !TryResolveChunkSlabByteLength(maxChunkSlots, chunkByteCapacity, out int chunkSlabByteLength) ||
                !TryResolveChunkSlabByteLength(maxChunkSlots, compressedChunkCapacity, out int compressedSlabByteLength) ||
                !TryResolveTelemetrySnapshotByteLength(out int dumpSnapshotByteLength))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultCapacityOverflow;
                _initialized = 0;
                return;
            }

            _allocatedChunkByteCapacity = chunkByteCapacity;
            _allocatedCompressedChunkByteCapacity = compressedChunkCapacity;
            _chunkSlabByteLength = chunkSlabByteLength;
            _compressedSlabByteLength = compressedSlabByteLength;
            _dumpSnapshotByteLength = dumpSnapshotByteLength;
            bool resolvedForceMockDiskIo = ResolveForceMockDiskIo();
            _forceMockDiskIo = resolvedForceMockDiskIo ? 1 : 0;
            _pathBuffer = new char[512];
            _utf8PathBuffer = new byte[4096];
            _chunkRootFullPath = ResolveChunkRootPath();
            BindRuntimeContext(PlayerRuntimeContextService.ActiveRuntimeContext);

            AllocateNativeState();
            if (_layoutValid == 0u)
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultLayout;
            if (!AreRequiredVaultBuffersReady())
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                ReleaseNativeState();
                return;
            }

            ResetRuntimeStateCounters();

            LoadColdStreamingProfile();
            if (!StartWorker())
            {
                ReleaseNativeState();
                return;
            }

            RegisterDispatcher();
            _initialized = 1;
            s_active = this;
        }

        private void Shutdown()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            bool jobsFinalized = TryFinalizePendingPagerJobsForTeardown();
            bool workerStopped = StopWorker();
            if (!jobsFinalized || !workerStopped)
            {
                UnregisterDispatcher(keepVisualSyncForDeferredShutdown: true);
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
                Volatile.Write(ref _deferredShutdown, 1);
                if (ReferenceEquals(s_active, this))
                    s_active = null;
                _initialized = 0;
                return;
            }

            UnregisterDispatcher(keepVisualSyncForDeferredShutdown: false);
            ReleaseNativeState();
            if (ReferenceEquals(s_active, this))
                s_active = null;
            _initialized = 0;
            _vault = null;
            _pendingLifecycleRebindVault = null;
            _chunkRootFullPath = null;
            _pathBuffer = null;
            _utf8PathBuffer = null;
            _cachedRuntimeContext = null;
            _allocatedChunkByteCapacity = 0;
            _allocatedCompressedChunkByteCapacity = 0;
            _chunkSlabByteLength = 0;
            _compressedSlabByteLength = 0;
            _dumpSnapshotByteLength = 0;
            Volatile.Write(ref _cameraAupSequence, 0);
            Volatile.Write(ref _cameraAupBitsX, 0L);
            Volatile.Write(ref _cameraAupBitsY, 0L);
            Volatile.Write(ref _cameraAupBitsZ, 0L);
        }

        private void TryReleaseDeferredShutdownState()
        {
            if (!TryFinalizePendingPagerJobsForTeardown())
                return;

            if (Volatile.Read(ref _workerThreadActive) != 0)
                return;

            if (!StopWorker())
                return;

            UnregisterDispatcher(keepVisualSyncForDeferredShutdown: false);
            ReleaseNativeState();
            IDataVault pendingRebindVault = _pendingLifecycleRebindVault;
            _vault = null;
            _pendingLifecycleRebindVault = null;
            _chunkRootFullPath = null;
            _pathBuffer = null;
            _utf8PathBuffer = null;
            _cachedRuntimeContext = null;
            _allocatedChunkByteCapacity = 0;
            _allocatedCompressedChunkByteCapacity = 0;
            _chunkSlabByteLength = 0;
            _compressedSlabByteLength = 0;
            _dumpSnapshotByteLength = 0;
            Volatile.Write(ref _deferredShutdown, 0);

            // R100 FIX: re-initialise after ANY successful deferred release, not only a vault rebind.
            // This used to be gated on pendingRebindVault != null, which was sufficient only because
            // Initialize would previously barge straight through a latched deferred shutdown. Now that
            // Initialize correctly refuses while the latch is set, the plain disable/enable path (where
            // no rebind vault is pending) would re-enable with _initialized == 0 and never recover.
            if (isActiveAndEnabled)
            {
                if (pendingRebindVault != null)
                    _vault = pendingRebindVault;

                Initialize();
            }
        }

        private void RegisterDispatcher()
        {
            if (_registeredPreSimulation == 0 && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = 1;
            if (_registeredPostSimulation == 0 && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = 1;
            if (_registeredVisualSync == 0 && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = 1;
            if (_registeredFrost == 0 && GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment))
                _registeredFrost = 1;
            if (_registeredHotSwap == 0 && GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = 1;
        }

        private void UnregisterDispatcher(bool keepVisualSyncForDeferredShutdown)
        {
            if (_registeredPreSimulation != 0)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = 0;
            }

            if (_registeredPostSimulation != 0)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = 0;
            }

            if (_registeredVisualSync != 0 && !keepVisualSyncForDeferredShutdown)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = 0;
            }

            if (_registeredFrost != 0)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                _registeredFrost = 0;
            }

            if (!keepVisualSyncForDeferredShutdown && _registeredHotSwap != 0)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = 0;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService as IDataVault;
                if (_initialized == 0)
                {
                    if (Volatile.Read(ref _deferredShutdown) != 0)
                    {
                        _pendingLifecycleRebindVault = nextVault;
                        return;
                    }

                    _vault = nextVault;
                    if (nextVault != null &&
                        isActiveAndEnabled &&
                        Volatile.Read(ref _deferredShutdown) == 0)
                    {
                        Initialize();
                    }
                }
                else
                {
                    RebindDataVaultForLifecycle(nextVault);
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                BindRuntimeContext(currentService as IPlayerRuntimeContext);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            // R97 FIX: unconditional `false` severed the VisualSync lane that drives
            // TryReleaseDeferredShutdownState when the dispatcher slot went null during a pending
            // deferred shutdown — the worker thread and the vault slabs then stayed unreclaimed for
            // the rest of the session. Keep the recovery lane alive while a deferred shutdown is
            // pending.
            UnregisterDispatcher(keepVisualSyncForDeferredShutdown: Volatile.Read(ref _deferredShutdown) != 0);
            if (currentService == null ||
                (_initialized == 0 && Volatile.Read(ref _deferredShutdown) == 0))
            {
                return;
            }

            RegisterDispatcher();
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            UnregisterDispatcher(keepVisualSyncForDeferredShutdown: true);
            CompletePendingPagerJobsForLifecycle();
            if (!StopWorker())
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo | TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                _pendingLifecycleRebindVault = nextVault;
                Volatile.Write(ref _deferredShutdown, 1);
                if (ReferenceEquals(s_active, this))
                    s_active = null;
                _initialized = 0;
                return;
            }

            ReleaseNativeState();
            _vault = nextVault;
            _pendingLifecycleRebindVault = null;
            ResetRuntimeStateCounters();
            Volatile.Write(ref _deferredShutdown, 0);

            if (nextVault == null)
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                if (ReferenceEquals(s_active, this))
                    s_active = null;
                _initialized = 0;
                return;
            }

            AllocateNativeState();
            if (!AreRequiredVaultBuffersReady())
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                ReleaseNativeState();
                if (ReferenceEquals(s_active, this))
                    s_active = null;
                _initialized = 0;
                return;
            }

            LoadColdStreamingProfile();
            if (!StartWorker())
            {
                UnregisterDispatcher(keepVisualSyncForDeferredShutdown: false);
                ReleaseNativeState();
                if (ReferenceEquals(s_active, this))
                    s_active = null;
                _initialized = 0;
                return;
            }

            RegisterDispatcher();
            _initialized = 1;
            s_active = this;
        }

        private void AllocateNativeState()
        {
            _vaultBackedMask = 0UL;
            _validatedVaultBuffers = 0;
            ResetVaultAliases();
            AcquireArray(MetadataBufferId, maxChunkSlots, NativeArrayOptions.ClearMemory, MetadataBit, ref _metadataHandle);
            AcquireArray(SectorCoordsBufferId, maxChunkSlots, NativeArrayOptions.ClearMemory, SectorCoordsBit, ref _sectorCoordsHandle);
            AcquireArray(StagingBytesBufferId, _chunkSlabByteLength, NativeArrayOptions.UninitializedMemory, StagingBytesBit, ref _stagingBytesHandle);
            AcquireArray(ActiveBytesBufferId, _chunkSlabByteLength, NativeArrayOptions.UninitializedMemory, ActiveBytesBit, ref _activeBytesHandle);
            AcquireArray(CompressedScratchBufferId, _compressedSlabByteLength, NativeArrayOptions.UninitializedMemory, CompressedScratchBit, ref _compressedScratchBytesHandle);
            AcquireArray(WorkerRequestBufferId, queueCapacity, NativeArrayOptions.UninitializedMemory, WorkerRequestBit, ref _workerRequestsHandle);
            AcquireArray(WorkerResultBufferId, queueCapacity, NativeArrayOptions.UninitializedMemory, WorkerResultBit, ref _workerResultsHandle);
            AcquireArray(JobLoadRequestsBufferId, math.min(maxChunkSlots, 121), NativeArrayOptions.UninitializedMemory, JobLoadRequestsBit, ref _jobLoadRequestsHandle);
            AcquireArray(JobLoadCountBufferId, 1, NativeArrayOptions.ClearMemory, JobLoadCountBit, ref _jobLoadCountHandle);
            AcquireArray(JobStaleSlotsBufferId, maxChunkSlots, NativeArrayOptions.UninitializedMemory, JobStaleSlotsBit, ref _jobStaleSlotsHandle);
            AcquireArray(JobStaleCountBufferId, 1, NativeArrayOptions.ClearMemory, JobStaleCountBit, ref _jobStaleCountHandle);
            AcquireArray(TelemetryRingBufferId, TerrainChunkPagerConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory, TelemetryRingBit, ref _telemetryRingHandle);
            AcquireArray(TuningBufferId, 1, NativeArrayOptions.ClearMemory, TuningBit, ref _tuningHandle);
            AcquireArray(CountersBufferId, 1, NativeArrayOptions.ClearMemory, CountersBit, ref _countersHandle);
            AcquireArray(FreedSlotsBufferId, maxChunkSlots, NativeArrayOptions.UninitializedMemory, FreedSlotsBit, ref _freedSlotsHandle);
            AcquireArray(FreedCountBufferId, 1, NativeArrayOptions.ClearMemory, FreedCountBit, ref _freedCountHandle);
            AcquireArray(HardwareProfilesBufferId, 8, NativeArrayOptions.ClearMemory, HardwareProfilesBit, ref _hardwareProfilesHandle);
            AcquireArray(CsvScratchBufferId, 16 * 1024, NativeArrayOptions.UninitializedMemory, CsvScratchBit, ref _csvScratchBytesHandle);
            AcquireArray(TelemetryDumpSnapshotBufferId, _dumpSnapshotByteLength, NativeArrayOptions.UninitializedMemory, TelemetryDumpSnapshotBit, ref _telemetryDumpSnapshotBytesHandle);

            if (!CacheUnsafePointers())
                return;

            _validatedVaultBuffers = 1;
            if (TryAcquireWriteArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO> tuningBuffer, out IDataVault tuningVault))
            {
                try
                {
                    TerrainChunkPagerTuningDTO tuning = TerrainChunkPagerTuningDTO.CreateDefault();
                    tuning.ChunkByteCapacity = _allocatedChunkByteCapacity;
                    tuning.Flags = Volatile.Read(ref _forceMockDiskIo) != 0 ? TerrainChunkPagerConstants.RequestFlagForceMock : 0u;
                    tuningBuffer[0] = TerrainChunkPagerMath.Sanitize(tuning);
                }
                finally
                {
                    ReleaseWriteArray(tuningVault, in _tuningHandle);
                }
            }

        }

        private bool ResolveForceMockDiskIo()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return forceMockDiskIo;
#else
            return false;
#endif
        }

        private void AcquireArray<T>(
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            ulong bit,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _vault;
            if (vault != null)
            {
                handle = vault.EnsureGenerationHandle<T>(bufferId, math.max(1, length), SystemID.WorldStreaming, options);
                if (HasVaultHandle(in handle) &&
                    vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                    buffer.IsCreated &&
                    buffer.Length >= math.max(1, length))
                {
                    _vaultBackedMask |= bit;
                    return;
                }
            }

            // R100 FIX (orphaned vault buffer): EnsureGenerationHandle allocates and sets RefCount=1,
            // so Ensure can succeed while resolve/length validation still fails - the vault refuses to
            // resolve while its compaction fence is raised. Discarding the handle here used to strand
            // that buffer permanently, because ReleaseArray only releases when the mask bit is set and
            // this path clears it. Release before discarding so the refcount cannot leak.
            if (vault != null && HasVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            _vaultBackedMask &= ~bit;
            handle = default;
            _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
        }

        private bool AreRequiredVaultBuffersReady()
        {
            return (_vaultBackedMask & RequiredVaultMask) == RequiredVaultMask &&
                   _validatedVaultBuffers != 0 &&
                   _metadataLength > 0 &&
                   _sectorCoordsLength > 0 &&
                   _activeByteLength > 0 &&
                   _csvScratchByteLength > 0 &&
                   _workerRequestLength > 0 &&
                   _workerResultLength > 0 &&
                   _jobLoadRequestLength > 0 &&
                   _jobLoadCountLength > 0 &&
                   _jobStaleSlotLength > 0 &&
                   _jobStaleCountLength > 0 &&
                   _telemetryLength > 0 &&
                   _tuningLength > 0 &&
                   _countersLength > 0 &&
                   _freedSlotLength > 0 &&
                   _freedCountLength > 0 &&
                   _hardwareProfileLength > 0;
        }

        private bool CacheUnsafePointers()
        {
            ResetVaultAliases();
            if (!TryResolveArray(in _metadataHandle, maxChunkSlots, out NativeArray<ChunkMetadataDTO> metadata) ||
                !TryResolveArray(in _sectorCoordsHandle, maxChunkSlots, out NativeArray<TerrainChunkSectorCoordDTO> sectorCoords) ||
                !TryResolveArray(in _stagingBytesHandle, _chunkSlabByteLength, out NativeArray<byte> stagingBytes) ||
                !TryResolveArray(in _activeBytesHandle, _chunkSlabByteLength, out NativeArray<byte> activeBytes) ||
                !TryResolveArray(in _compressedScratchBytesHandle, _compressedSlabByteLength, out NativeArray<byte> compressedScratchBytes) ||
                !TryResolveArray(in _workerRequestsHandle, queueCapacity, out NativeArray<TerrainChunkWorkerRequestDTO> workerRequests) ||
                !TryResolveArray(in _workerResultsHandle, queueCapacity, out NativeArray<TerrainChunkWorkerResultDTO> workerResults) ||
                !TryResolveArray(in _jobLoadRequestsHandle, math.min(maxChunkSlots, 121), out NativeArray<TerrainChunkWorkerRequestDTO> jobLoadRequests) ||
                !TryResolveArray(in _jobLoadCountHandle, 1, out NativeArray<int> jobLoadCount) ||
                !TryResolveArray(in _jobStaleSlotsHandle, maxChunkSlots, out NativeArray<int> jobStaleSlots) ||
                !TryResolveArray(in _jobStaleCountHandle, 1, out NativeArray<int> jobStaleCount) ||
                !TryResolveArray(in _telemetryRingHandle, TerrainChunkPagerConstants.TelemetryCapacity, out NativeArray<PagerTelemetryEntry> telemetryRing) ||
                !TryResolveArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO> tuning) ||
                !TryResolveArray(in _countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO> counters) ||
                !TryResolveArray(in _freedSlotsHandle, maxChunkSlots, out NativeArray<int> freedSlots) ||
                !TryResolveArray(in _freedCountHandle, 1, out NativeArray<int> freedCount) ||
                !TryResolveArray(in _hardwareProfilesHandle, 8, out NativeArray<StreamingHardwareProfileDTO> hardwareProfiles) ||
                !TryResolveArray(in _csvScratchBytesHandle, 16 * 1024, out NativeArray<byte> csvScratchBytes) ||
                !TryResolveArray(in _telemetryDumpSnapshotBytesHandle, _dumpSnapshotByteLength, out NativeArray<byte> telemetryDumpSnapshotBytes))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                ResetVaultAliases();
                return false;
            }

            _metadataLength = metadata.Length;
            _sectorCoordsLength = sectorCoords.Length;
            _stagingByteLength = stagingBytes.Length;
            _activeByteLength = activeBytes.Length;
            _compressedScratchByteLength = compressedScratchBytes.Length;
            _workerRequestLength = workerRequests.Length;
            _workerResultLength = workerResults.Length;
            _jobLoadRequestLength = jobLoadRequests.Length;
            _jobLoadCountLength = jobLoadCount.Length;
            _jobStaleSlotLength = jobStaleSlots.Length;
            _jobStaleCountLength = jobStaleCount.Length;
            _telemetryLength = telemetryRing.Length;
            _tuningLength = tuning.Length;
            _countersLength = counters.Length;
            _freedSlotLength = freedSlots.Length;
            _freedCountLength = freedCount.Length;
            _hardwareProfileLength = hardwareProfiles.Length;
            _csvScratchByteLength = csvScratchBytes.Length;
            _telemetryDumpSnapshotByteLength = telemetryDumpSnapshotBytes.Length;
            return true;
        }

        private void ReleaseNativeState()
        {
            if (!TryFinalizePendingPagerJobsForTeardown())
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
                return;
            }

            ReleaseArray(ref _metadataHandle, MetadataBit);
            ReleaseArray(ref _sectorCoordsHandle, SectorCoordsBit);
            ReleaseArray(ref _stagingBytesHandle, StagingBytesBit);
            ReleaseArray(ref _activeBytesHandle, ActiveBytesBit);
            ReleaseArray(ref _compressedScratchBytesHandle, CompressedScratchBit);
            ReleaseArray(ref _workerRequestsHandle, WorkerRequestBit);
            ReleaseArray(ref _workerResultsHandle, WorkerResultBit);
            ReleaseArray(ref _jobLoadRequestsHandle, JobLoadRequestsBit);
            ReleaseArray(ref _jobLoadCountHandle, JobLoadCountBit);
            ReleaseArray(ref _jobStaleSlotsHandle, JobStaleSlotsBit);
            ReleaseArray(ref _jobStaleCountHandle, JobStaleCountBit);
            ReleaseArray(ref _telemetryRingHandle, TelemetryRingBit);
            ReleaseArray(ref _tuningHandle, TuningBit);
            ReleaseArray(ref _countersHandle, CountersBit);
            ReleaseArray(ref _freedSlotsHandle, FreedSlotsBit);
            ReleaseArray(ref _freedCountHandle, FreedCountBit);
            ReleaseArray(ref _hardwareProfilesHandle, HardwareProfilesBit);
            ReleaseArray(ref _csvScratchBytesHandle, CsvScratchBit);
            ReleaseArray(ref _telemetryDumpSnapshotBytesHandle, TelemetryDumpSnapshotBit);
            ResetVaultAliases();
            _validatedVaultBuffers = 0;
            _vaultBackedMask = 0UL;
        }

        private void ReleaseArray<T>(ref VaultGenerationHandle<T> handle, ulong bit) where T : struct
        {
            // R100 FIX: keyed on the handle, not on _vaultBackedMask. The mask is a readiness signal
            // and can legitimately be clear while a real buffer exists (see AcquireArray's partial
            // failure path); gating release on the mask therefore stranded buffers. Releasing on
            // HasVaultHandle alone makes a mask/handle divergence unable to leak.
            IDataVault vault = _vault;
            if (vault != null && HasVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
            _vaultBackedMask &= ~bit;
        }

        private static bool HasVaultHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryResolveArray<T>(in VaultGenerationHandle<T> handle, int minLength, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return vault != null &&
                   HasVaultHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, minLength);
        }

        private bool TryReadOnlyArray<T>(in VaultGenerationHandle<T> handle, int minLength, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return vault != null &&
                   HasVaultHandle(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, minLength);
        }

        private bool TryAcquireWriteArray<T>(in VaultGenerationHandle<T> handle, int minLength, out NativeArray<T> buffer, out IDataVault writeVault) where T : struct
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _vault;
            if (vault == null || !HasVaultHandle(in handle))
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.WorldStreaming, out buffer))
                return false;

            bool keepLock = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= math.max(1, minLength))
                {
                    writeVault = vault;
                    keepLock = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                    vault.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
            }
        }

        private static void ReleaseWriteArray<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            vault?.ReleaseWriteLock(in handle, SystemID.WorldStreaming);
        }

        private void ClearFirstValue<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            if (!TryAcquireWriteArray(in handle, 1, out NativeArray<T> buffer, out IDataVault writeVault))
                return;

            try
            {
                buffer[0] = default;
            }
            finally
            {
                ReleaseWriteArray(writeVault, in handle);
            }
        }

        private void ResetVaultAliases()
        {
            _metadataLength = 0;
            _sectorCoordsLength = 0;
            _stagingByteLength = 0;
            _activeByteLength = 0;
            _compressedScratchByteLength = 0;
            _workerRequestLength = 0;
            _workerResultLength = 0;
            _jobLoadRequestLength = 0;
            _jobLoadCountLength = 0;
            _jobStaleSlotLength = 0;
            _jobStaleCountLength = 0;
            _telemetryLength = 0;
            _tuningLength = 0;
            _countersLength = 0;
            _freedSlotLength = 0;
            _freedCountLength = 0;
            _hardwareProfileLength = 0;
            _csvScratchByteLength = 0;
            _telemetryDumpSnapshotByteLength = 0;
        }

        private void ResetRuntimeStateCounters()
        {
            Volatile.Write(ref _requestHead, 0);
            Volatile.Write(ref _requestTail, 0);
            Volatile.Write(ref _resultHead, 0);
            Volatile.Write(ref _resultTail, 0);
            _telemetryCursor = 0;
            _csvProfileCount = 0;
            _lastDumpFrame = 0;
            Interlocked.Exchange(ref _dumpRequestPacked, 0L);
            _workerSequence = 0u;
            _lastEvalMicros = 0u;
            _frameId = 0u;
            _pendingResidency = 0;
            _pendingEviction = 0;
            _evictedChunksTotal = 0u;
            _pendingResidencyStartTimestamp = 0L;
            _pendingResidencyHandle = default;
            _pendingEvictionHandle = default;
            Volatile.Write(ref _workerHeartbeatTimestamp, 0L);
            ClearFirstValue(in _jobLoadCountHandle);
            ClearFirstValue(in _jobStaleCountHandle);
            ClearFirstValue(in _freedCountHandle);
            ClearFirstValue(in _countersHandle);
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            using (_preSimulationMarker.Auto())
            {
                if (_initialized == 0)
                    return;

                _frameId = timing.FrameId;
                if (!TryFinalizeEviction() || !TryFinalizeResidencyEvaluation())
                    return;

                if (!TryReadCameraAupSnapshot(out double3 cameraAup))
                {
                    _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultNonFiniteAup;
                    return;
                }

                _lastCameraAup = cameraAup;
                TerrainChunkPagerTuningDTO tuning = ResolveFrameTuning();
                _lastCameraSectorX = TerrainChunkPagerMath.ResolveSectorCoord(cameraAup.x, tuning.SectorSizeMeters);
                _lastCameraSectorZ = TerrainChunkPagerMath.ResolveSectorCoord(cameraAup.z, tuning.SectorSizeMeters);
                if (!TryResolveArray(in _metadataHandle, _metadataLength, out NativeArray<ChunkMetadataDTO> metadata) ||
                    !TryResolveArray(in _sectorCoordsHandle, _sectorCoordsLength, out NativeArray<TerrainChunkSectorCoordDTO> sectorCoords) ||
                    !TryResolveArray(in _jobLoadRequestsHandle, _jobLoadRequestLength, out NativeArray<TerrainChunkWorkerRequestDTO> jobLoadRequests) ||
                    !TryResolveArray(in _jobLoadCountHandle, _jobLoadCountLength, out NativeArray<int> jobLoadCount) ||
                    !TryResolveArray(in _jobStaleSlotsHandle, _jobStaleSlotLength, out NativeArray<int> jobStaleSlots) ||
                    !TryResolveArray(in _jobStaleCountHandle, _jobStaleCountLength, out NativeArray<int> jobStaleCount))
                {
                    _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                    return;
                }

                jobLoadCount[0] = 0;
                jobStaleCount[0] = 0;

                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                EvaluateChunkResidencyJob job = default;
                job.Metadata = metadata;
                job.SectorCoords = sectorCoords;
                job.MetadataCapacity = math.min(_metadataLength, math.min(metadata.Length, sectorCoords.Length));
                job.LoadRequests = jobLoadRequests;
                job.LoadRequestCount = jobLoadCount;
                job.StaleSlots = jobStaleSlots;
                job.StaleSlotCount = jobStaleCount;
                job.CameraAup = cameraAup;
                job.Tuning = tuning;
                job.Frame = timing.FrameId;
                job.SequenceBase = _workerSequence + 1u;
                _pendingResidencyStartTimestamp = start;
                _pendingResidencyHandle = job.Schedule();
                _pendingResidency = 1;
                H8Memory.RegisterActiveJob(SystemID.WorldStreaming, _pendingResidencyHandle);
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            using (_postSimulationMarker.Auto())
            {
                if (_initialized == 0)
                    return;

                _frameId = timing.FrameId;
                if (!TryFinalizeEviction() || !TryFinalizeResidencyEvaluation())
                    return;

                DrainWorkerResults();
                DispatchEvaluationLoadRequests();
                WriteTelemetry();
            }
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            using (_visualSyncMarker.Auto())
            {
                if (Volatile.Read(ref _deferredShutdown) != 0)
                {
                    TryReleaseDeferredShutdownState();
                    return;
                }

                if (_initialized == 0)
                    return;

                _frameId = timing.FrameId;
                if (!TryFinalizeEviction() || !TryFinalizeResidencyEvaluation())
                    return;

                if (!TryResolveArray(in _metadataHandle, _metadataLength, out NativeArray<ChunkMetadataDTO> metadata) ||
                    !TryResolveArray(in _stagingBytesHandle, _stagingByteLength, out NativeArray<byte> stagingBytes) ||
                    !TryResolveArray(in _activeBytesHandle, _activeByteLength, out NativeArray<byte> activeBytes))
                {
                    _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                    return;
                }

                TerrainChunkPagerTuningDTO tuning = TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningView)
                    ? TerrainChunkPagerMath.Sanitize(tuningView[0])
                    : TerrainChunkPagerTuningDTO.CreateDefault();
                int commitBudget = math.max(1, tuning.MaxCommitsPerVisualSync);
                int byteBudget = math.max(4096, (int)tuning.CommitByteBudgetPerFrame);
                int committed = 0;
                int metadataCount = math.min(_metadataLength, metadata.Length);
                byte* stagingPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(stagingBytes);
                byte* activePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(activeBytes);
                int byteLength = math.min(_stagingByteLength, activeBytes.Length);
                for (int slot = 0; slot < metadataCount && committed < commitBudget && byteBudget > 0; slot++)
                {
                    ChunkMetadataDTO meta = metadata[slot];
                    if ((meta.StateFlags & TerrainChunkStateFlags.ReadyToCommit) == 0u)
                        continue;

                    int bytes = math.clamp((int)meta.FileOffset, 0, _allocatedChunkByteCapacity);
                    // R97 FIX (forward-progress guarantee): the first commit of the frame is always
                    // admitted even when it exceeds the remaining byte budget — paired with the
                    // Sanitize clamp (budget >= ChunkByteCapacity) this makes the commit-livelock
                    // class impossible regardless of runtime tuning writes.
                    if (bytes <= 0 || (committed > 0 && bytes > byteBudget))
                        continue;

                    int offset = slot * _allocatedChunkByteCapacity;
                    if (offset < 0 || offset > byteLength - bytes)
                        continue;

                    UnsafeUtility.MemCpy(activePtr + offset, stagingPtr + offset, bytes);
                    meta.StateFlags = (meta.StateFlags | TerrainChunkStateFlags.Active | TerrainChunkStateFlags.NetcodeExcluded) &
                                      ~(TerrainChunkStateFlags.Loading | TerrainChunkStateFlags.ReadyToCommit | TerrainChunkStateFlags.Stale);
                    meta.BufferIdRef = unchecked((uint)slot);
                    metadata[slot] = meta;
                    committed++;
                    byteBudget -= bytes;
                }

                // R97: transient-failure retry/backoff lane. Slots parked as MissingFile with a
                // retry marker (_pad0 == 1, set by DrainWorkerResults for IO-class failures) count
                // down here and are then released (metadata/coords -> default, SectorHash 0), so
                // the residency job re-requests the sector naturally. Worst case a persistently
                // failing file retries once per backoff window (~bounded IO), instead of poisoning
                // the sector for the whole session. Genuine missing files (_pad0 == 0) stay parked.
                for (int slot = 0; slot < metadataCount; slot++)
                {
                    ChunkMetadataDTO meta = metadata[slot];
                    if ((meta.StateFlags & TerrainChunkStateFlags.MissingFile) == 0u || meta._pad0 == 0)
                        continue;

                    if (meta._pad1 > 0)
                    {
                        meta._pad1--;
                        metadata[slot] = meta;
                        continue;
                    }

                    // SectorHash == 0 is the free-slot condition (FindFreeSlot); stale sector coords
                    // are harmless — dispatch rewrites them on the next allocation of this slot.
                    metadata[slot] = default;
                }
            }
        }

        private bool TryFinalizeResidencyEvaluation()
        {
            if (_pendingResidency == 0)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingResidencyHandle))
                return false;

            _pendingResidency = 0;
            _lastEvalMicros = ElapsedMicroseconds(_pendingResidencyStartTimestamp);
            _pendingResidencyStartTimestamp = 0L;
            return true;
        }

        private bool TryFinalizeEviction()
        {
            if (_pendingEviction == 0)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingEvictionHandle))
                return false;

            _pendingEviction = 0;

            // R100: fold the job's freed-slot count into the release ledger. This is the only reader of
            // FreedSlotCount in the project - the eviction path previously produced no observable output,
            // which streaming.md rejects and which made eviction thrash impossible to see in the tuner.
            // Safe to read here and only here: the fence above proves the job is complete.
            if (TryReadOnlyArray(in _freedCountHandle, _freedCountLength, out NativeArray<int>.ReadOnly freedCountView) &&
                freedCountView.Length > 0)
            {
                int freedThisPass = freedCountView[0];
                if (freedThisPass > 0)
                    _evictedChunksTotal += (uint)freedThisPass;
            }

            return true;
        }

        private bool TryFinalizePendingPagerJobsForTeardown()
        {
            bool finalized = true;
            if (_pendingResidency != 0)
            {
                if (DispatcherJobFence.TryFinalizeCompleted(ref _pendingResidencyHandle))
                {
                    _pendingResidency = 0;
                    _lastEvalMicros = ElapsedMicroseconds(_pendingResidencyStartTimestamp);
                    _pendingResidencyStartTimestamp = 0L;
                }
                else
                {
                    finalized = false;
                }
            }

            if (_pendingEviction != 0)
            {
                if (DispatcherJobFence.TryFinalizeCompleted(ref _pendingEvictionHandle))
                    _pendingEviction = 0;
                else
                    finalized = false;
            }

            return finalized;
        }

        private void CompletePendingPagerJobsForLifecycle()
        {
            if (_pendingResidency != 0)
            {
                DispatcherJobFence.TryComplete(ref _pendingResidencyHandle, forceComplete: true);
                _pendingResidency = 0;
                if (_pendingResidencyStartTimestamp > 0L)
                    _lastEvalMicros = ElapsedMicroseconds(_pendingResidencyStartTimestamp);
                _pendingResidencyStartTimestamp = 0L;
            }

            if (_pendingEviction != 0)
            {
                DispatcherJobFence.TryComplete(ref _pendingEvictionHandle, forceComplete: true);
                _pendingEviction = 0;
            }
        }

        private TerrainChunkPagerTuningDTO ResolveFrameTuning()
        {
            TerrainChunkPagerTuningDTO tuning = TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningView)
                ? tuningView[0]
                : TerrainChunkPagerTuningDTO.CreateDefault();
            tuning.GlobalQualityWeight = math.saturate(TerrainChunkPagerMath.FiniteOr(HomeostasisBrain.GlobalQualityWeight, tuning.GlobalQualityWeight));
            if (_allocatedChunkByteCapacity > 0)
                tuning.ChunkByteCapacity = _allocatedChunkByteCapacity;
            tuning.Flags = Volatile.Read(ref _forceMockDiskIo) != 0
                ? (tuning.Flags | TerrainChunkPagerConstants.RequestFlagForceMock)
                : (tuning.Flags & ~TerrainChunkPagerConstants.RequestFlagForceMock);
            tuning = TerrainChunkPagerMath.Sanitize(tuning);
            tuning.MaxQueuedLoads = math.clamp(tuning.MaxQueuedLoads, 1, math.max(1, queueCapacity - 1));
            tuning.CommitByteBudgetPerFrame = math.min(
                tuning.CommitByteBudgetPerFrame,
                ResolveCommitByteBudget(_allocatedChunkByteCapacity, tuning.MaxCommitsPerVisualSync));
            if (TryAcquireWriteArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO> tuningWrite, out IDataVault tuningVault))
            {
                try
                {
                    tuningWrite[0] = tuning;
                }
                finally
                {
                    ReleaseWriteArray(tuningVault, in _tuningHandle);
                }
            }

            return tuning;
        }

        private void DispatchEvaluationLoadRequests()
        {
            if (!TryResolveArray(in _metadataHandle, _metadataLength, out NativeArray<ChunkMetadataDTO> metadata) ||
                !TryResolveArray(in _sectorCoordsHandle, _sectorCoordsLength, out NativeArray<TerrainChunkSectorCoordDTO> sectorCoords) ||
                !TryResolveArray(in _jobLoadRequestsHandle, _jobLoadRequestLength, out NativeArray<TerrainChunkWorkerRequestDTO> jobLoadRequests) ||
                !TryResolveArray(in _jobLoadCountHandle, _jobLoadCountLength, out NativeArray<int> jobLoadCount) ||
                !TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningRead))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            int metadataCapacity = math.min(_metadataLength, math.min(metadata.Length, sectorCoords.Length));
            int count = math.min(jobLoadCount[0], jobLoadRequests.Length);
            TerrainChunkPagerTuningDTO tuning = tuningRead[0];
            int dispatched = 0;
            for (int i = 0; i < count && dispatched < tuning.MaxQueuedLoads; i++)
            {
                TerrainChunkWorkerRequestDTO request = jobLoadRequests[i];
                if (FindSlotByHash(metadata, metadataCapacity, request.SectorHash) >= 0)
                    continue;

                int slot = FindFreeSlot(metadata, metadataCapacity);
                if (slot < 0)
                    break;

                request.SlotIndex = slot;
                request.Sequence = ++_workerSequence;
                ChunkMetadataDTO meta = default;
                meta.SectorHash = request.SectorHash;
                meta.BufferIdRef = unchecked((uint)slot);
                meta.FileOffset = request.Sequence;
                meta.StateFlags = TerrainChunkStateFlags.Loading | TerrainChunkStateFlags.NetcodeExcluded;
                meta.DistanceSq = request.DistanceSq;
                metadata[slot] = meta;
                TerrainChunkSectorCoordDTO coord = default;
                coord.X = request.SectorX;
                coord.Z = request.SectorZ;
                sectorCoords[slot] = coord;
                if (!TryEnqueueWorkerRequest(in request))
                {
                    metadata[slot] = default;
                    sectorCoords[slot] = default;
                    _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultQueueOverflow;
                    IncrementQueueOverflow();
                    break;
                }

                dispatched++;
            }
        }

        private void DrainWorkerResults()
        {
            if (Volatile.Read(ref _resultTail) == Volatile.Read(ref _resultHead))
                return;

            if (!TryResolveArray(in _metadataHandle, _metadataLength, out NativeArray<ChunkMetadataDTO> metadata) ||
                !TryResolveArray(in _sectorCoordsHandle, _sectorCoordsLength, out NativeArray<TerrainChunkSectorCoordDTO> sectorCoords))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            int metadataCapacity = math.min(_metadataLength, math.min(metadata.Length, sectorCoords.Length));
            TerrainChunkPagerTuningDTO tuning = TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningRead)
                ? tuningRead[0]
                : TerrainChunkPagerTuningDTO.CreateDefault();
            bool tuningDirty = false;
            TerrainChunkWorkerResultDTO result;
            while (TryDequeueWorkerResult(out result))
            {
                if ((uint)result.SlotIndex >= (uint)metadataCapacity)
                    continue;

                ChunkMetadataDTO meta = metadata[result.SlotIndex];
                if (meta.SectorHash != result.SectorHash ||
                    meta.FileOffset != result.Sequence ||
                    (meta.StateFlags & TerrainChunkStateFlags.Loading) == 0u)
                {
                    _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
                    continue;
                }

                float safeLatency = math.max(0f, result.LatencyMs);
                tuning.LatencyEwmaMs = tuning.LatencyEwmaMs <= 0f
                    ? safeLatency
                    : math.lerp(tuning.LatencyEwmaMs, safeLatency, 0.08f);
                tuning.EffectiveRingRadius = TerrainChunkPagerMath.ResolveContinuousRingRadius(tuning.GlobalQualityWeight, tuning.LatencyEwmaMs, in tuning);
                tuning = TerrainChunkPagerMath.Sanitize(tuning);
                tuningDirty = true;

                if ((result.Flags & TerrainChunkPagerConstants.ResultFlagSuccess) != 0u && result.BytesWritten > 0)
                {
                    meta.SectorHash = result.SectorHash;
                    meta.BufferIdRef = unchecked((uint)result.SlotIndex);
                    meta.FileOffset = unchecked((uint)math.min(result.BytesWritten, _allocatedChunkByteCapacity));
                    meta.StateFlags = (meta.StateFlags | TerrainChunkStateFlags.ReadyToCommit | TerrainChunkStateFlags.NetcodeExcluded) &
                                      ~TerrainChunkStateFlags.Stale;
                    if ((result.Flags & TerrainChunkPagerConstants.ResultFlagMock) != 0u)
                        meta.StateFlags |= TerrainChunkStateFlags.MockPayload;
                    TerrainChunkSectorCoordDTO coord = default;
                    coord.X = result.SectorX;
                    coord.Z = result.SectorZ;
                    sectorCoords[result.SlotIndex] = coord;
                }
                else
                {
                    meta.SectorHash = result.SectorHash;
                    meta.BufferIdRef = unchecked((uint)result.SlotIndex);
                    meta.StateFlags = TerrainChunkStateFlags.MissingFile | TerrainChunkStateFlags.Stale | TerrainChunkStateFlags.NetcodeExcluded;
                    meta.FileOffset = 0u;
                    // R97 FIX (permanent sector poisoning): transient IO-class failures (locked file,
                    // torn read, decode error) previously negative-cached the sector forever while the
                    // player stayed in range. Mark them retryable with a backoff countdown; the
                    // VisualSyncTick backoff lane frees the slot when it expires so residency
                    // re-requests naturally. A genuinely absent file stays a permanent negative cache
                    // (existing semantics).
                    const uint transientMask = TerrainChunkPagerConstants.ResultFlagIoError |
                                               TerrainChunkPagerConstants.ResultFlagLz4Error |
                                               TerrainChunkPagerConstants.ResultFlagPartialRead |
                                               TerrainChunkPagerConstants.ResultFlagChecksumMismatch |
                                               TerrainChunkPagerConstants.ResultFlagInvalidHeader;
                    bool transientFailure = (result.Flags & transientMask) != 0u &&
                                            (result.Flags & TerrainChunkPagerConstants.ResultFlagMissingFile) == 0u;
                    if (transientFailure)
                    {
                        meta._pad0 = 1;
                        meta._pad1 = TransientChunkRetryBackoffTicks;
                    }
                    else
                    {
                        meta._pad0 = 0;
                        meta._pad1 = 0;
                    }

                    if ((result.Flags & TerrainChunkPagerConstants.ResultFlagMissingFile) != 0u)
                        IncrementMissingFile();
                    if ((result.Flags & TerrainChunkPagerConstants.ResultFlagIoError) != 0u)
                        IncrementIoError();
                    if ((result.Flags & TerrainChunkPagerConstants.ResultFlagLz4Error) != 0u)
                        IncrementLz4Error();
                    if ((result.Flags & TerrainChunkPagerConstants.ResultFlagInvalidHeader) != 0u)
                        _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultInvalidHeader;
                    if ((result.Flags & TerrainChunkPagerConstants.ResultFlagChecksumMismatch) != 0u)
                        _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultChecksum;
                }

                metadata[result.SlotIndex] = meta;
            }

            if (tuningDirty && TryAcquireWriteArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO> tuningWrite, out IDataVault tuningVault))
            {
                try
                {
                    tuningWrite[0] = tuning;
                }
                finally
                {
                    ReleaseWriteArray(tuningVault, in _tuningHandle);
                }
            }
        }

        private bool TryEnqueueWorkerRequest(in TerrainChunkWorkerRequestDTO request)
        {
            if (!TryResolveArray(in _workerRequestsHandle, _workerRequestLength, out NativeArray<TerrainChunkWorkerRequestDTO> workerRequests))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return false;
            }

            int head = Volatile.Read(ref _requestHead);
            int next = (head + 1) & _queueMask;
            if (next == Volatile.Read(ref _requestTail) || (uint)head >= (uint)workerRequests.Length)
                return false;

            workerRequests[head] = request;
            Interlocked.Exchange(ref _requestHead, next);
            _workerWake.Set();
            return true;
        }

        private bool TryDequeueWorkerRequest(out TerrainChunkWorkerRequestDTO request)
        {
            if (!TryResolveArray(in _workerRequestsHandle, _workerRequestLength, out NativeArray<TerrainChunkWorkerRequestDTO> workerRequests))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                request = default;
                return false;
            }

            int tail = Volatile.Read(ref _requestTail);
            if (tail == Volatile.Read(ref _requestHead))
            {
                request = default;
                return false;
            }

            if ((uint)tail >= (uint)workerRequests.Length)
            {
                request = default;
                return false;
            }

            request = workerRequests[tail];
            Interlocked.Exchange(ref _requestTail, (tail + 1) & _queueMask);
            return true;
        }

        private bool TryEnqueueWorkerResult(in TerrainChunkWorkerResultDTO result)
        {
            if (!TryResolveArray(in _workerResultsHandle, _workerResultLength, out NativeArray<TerrainChunkWorkerResultDTO> workerResults))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return false;
            }

            int head = Volatile.Read(ref _resultHead);
            int next = (head + 1) & _queueMask;
            if (next == Volatile.Read(ref _resultTail) || (uint)head >= (uint)workerResults.Length)
                return false;

            workerResults[head] = result;
            Interlocked.Exchange(ref _resultHead, next);
            return true;
        }

        private bool TryDequeueWorkerResult(out TerrainChunkWorkerResultDTO result)
        {
            if (!TryResolveArray(in _workerResultsHandle, _workerResultLength, out NativeArray<TerrainChunkWorkerResultDTO> workerResults))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                result = default;
                return false;
            }

            int tail = Volatile.Read(ref _resultTail);
            if (tail == Volatile.Read(ref _resultHead))
            {
                result = default;
                return false;
            }

            if ((uint)tail >= (uint)workerResults.Length)
            {
                result = default;
                return false;
            }

            result = workerResults[tail];
            Interlocked.Exchange(ref _resultTail, (tail + 1) & _queueMask);
            return true;
        }

        private bool StartWorker()
        {
            // R100 FIX: refuse to start a second worker over a live one. StopWorker intentionally leaves
            // _workerWake and _workerThread intact when its join times out, so starting again would leak
            // that AutoResetEvent, orphan a thread still holding this instance, and - because
            // Volatile.Write(_workerRunning, 1) below revives the old thread's loop condition - put two
            // consumers on a single-consumer request ring whose dequeue is not a CAS. Both would read the
            // same tail and load the same chunk twice. Initialize now guards this too; this is the
            // invariant enforced at the owner so no future caller can bypass it.
            if (Volatile.Read(ref _workerThreadActive) != 0)
                return false;

            AutoResetEvent wake = null;
            Thread thread = null;
            try
            {
                wake = new AutoResetEvent(false);
                thread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = WorkerName,
                    Priority = System.Threading.ThreadPriority.BelowNormal
                };

                Volatile.Write(ref _workerRunning, 1);
                Volatile.Write(ref _workerThreadActive, 1);
                Volatile.Write(ref _workerHeartbeatTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
                _workerWake = wake;
                _workerThread = thread;
                thread.Start();
                return true;
            }
            catch (Exception)
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
                Volatile.Write(ref _workerRunning, 0);
                Volatile.Write(ref _workerThreadActive, 0);
                Volatile.Write(ref _workerHeartbeatTimestamp, 0L);
                if (thread == null || ReferenceEquals(_workerThread, thread))
                    _workerThread = null;
                if (wake != null && ReferenceEquals(_workerWake, wake))
                    _workerWake = null;
                DisposeWorkerWakeNoThrow(wake);
                return false;
            }
        }

        private bool StopWorker()
        {
            Volatile.Write(ref _workerRunning, 0);
            AutoResetEvent wake = _workerWake;
            SignalWorkerWakeNoThrow(wake);

            Thread thread = _workerThread;
            if (thread != null && thread.IsAlive)
            {
                if (!TryJoinWorkerNoThrow(thread))
                    return false;
            }

            _workerThread = null;
            if (_workerWake != null)
            {
                DisposeWorkerWakeNoThrow(_workerWake);
                _workerWake = null;
            }

            Volatile.Write(ref _workerThreadActive, 0);
            Volatile.Write(ref _workerHeartbeatTimestamp, 0L);
            return true;
        }

        private static bool TryJoinWorkerNoThrow(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
                return true;
            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(WorkerShutdownWaitMilliseconds);
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SignalWorkerWakeNoThrow(AutoResetEvent wake)
        {
            if (wake == null)
                return;

            try
            {
                wake.Set();
            }
            catch (Exception)
            {
            }
        }

        private static void DisposeWorkerWakeNoThrow(AutoResetEvent wake)
        {
            if (wake == null)
                return;

            try
            {
                wake.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private void WorkerLoop()
        {
            try
            {
                while (Volatile.Read(ref _workerRunning) != 0)
                {
                    Volatile.Write(ref _workerHeartbeatTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
                    _workerWake.WaitOne();
                    Volatile.Write(ref _workerHeartbeatTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
                    TerrainChunkWorkerRequestDTO request;
                    while (Volatile.Read(ref _workerRunning) != 0 && TryDequeueWorkerRequest(out request))
                    {
                        try
                        {
                            Volatile.Write(ref _workerHeartbeatTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
                            ProcessWorkerRequest(in request);
                            Volatile.Write(ref _workerHeartbeatTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
                        }
                        catch (Exception)
                        {
                            TerrainChunkWorkerResultDTO result = default;
                            result.SectorHash = request.SectorHash;
                            result.SectorX = request.SectorX;
                            result.SectorZ = request.SectorZ;
                            result.SlotIndex = request.SlotIndex;
                            result.Sequence = request.Sequence;
                            result.RequestFrame = request.RequestFrame;
                            result.Flags = TerrainChunkPagerConstants.ResultFlagIoError;
                            PublishWorkerResult(in result);
                        }
                    }

                    TryDrainTelemetryDumpRequestOnWorker();
                }
            }
            finally
            {
                Volatile.Write(ref _workerThreadActive, 0);
                Volatile.Write(ref _workerHeartbeatTimestamp, 0L);
            }
        }

        private void ProcessWorkerRequest(in TerrainChunkWorkerRequestDTO request)
        {
            TerrainChunkWorkerResultDTO result = default;
            result.SectorHash = request.SectorHash;
            result.SectorX = request.SectorX;
            result.SectorZ = request.SectorZ;
            result.SlotIndex = request.SlotIndex;
            result.Sequence = request.Sequence;
            result.RequestFrame = request.RequestFrame;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            int slot = request.SlotIndex;
            int capacity = math.min(request.ChunkByteCapacity, _allocatedChunkByteCapacity);
            int compressedCapacity = _allocatedCompressedChunkByteCapacity;
            if (!TryResolveArray(in _stagingBytesHandle, _chunkSlabByteLength, out NativeArray<byte> stagingBytes) ||
                !TryResolveArray(in _compressedScratchBytesHandle, _compressedSlabByteLength, out NativeArray<byte> compressedScratchBytes))
            {
                result.Flags = TerrainChunkPagerConstants.ResultFlagIoError;
                result.LatencyMs = ElapsedMilliseconds(start);
                PublishWorkerResult(in result);
                return;
            }

            long stagingOffset = (long)slot * _allocatedChunkByteCapacity;
            long compressedOffset = (long)slot * _allocatedCompressedChunkByteCapacity;
            if ((uint)slot >= (uint)maxChunkSlots ||
                capacity <= 0 ||
                compressedCapacity <= 0 ||
                stagingOffset < 0L ||
                compressedOffset < 0L ||
                stagingOffset > stagingBytes.Length - capacity ||
                compressedOffset > compressedScratchBytes.Length - compressedCapacity)
            {
                result.Flags = TerrainChunkPagerConstants.ResultFlagIoError;
                result.LatencyMs = ElapsedMilliseconds(start);
                PublishWorkerResult(in result);
                return;
            }

            byte* staging = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(stagingBytes) + (int)stagingOffset;
            byte* compressed = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(compressedScratchBytes) + (int)compressedOffset;
            bool forceMock = Volatile.Read(ref _forceMockDiskIo) != 0 ||
                             (request.Flags & TerrainChunkPagerConstants.RequestFlagForceMock) != 0u;
            if (forceMock)
            {
                SimulateMockDiskDelay(in request);
                GenerateMockDiskLoadJob.Fill(staging, capacity, request.SectorHash, request.Sequence);
                result.BytesWritten = capacity;
                result.Flags = TerrainChunkPagerConstants.ResultFlagSuccess | TerrainChunkPagerConstants.ResultFlagMock;
                result.LatencyMs = ElapsedMilliseconds(start);
                PublishWorkerResult(in result);
                return;
            }

            if (!TryLoadChunkFile(in request, staging, compressed, capacity, compressedCapacity, ref result))
            {
                result.LatencyMs = ElapsedMilliseconds(start);
                PublishWorkerResult(in result);
                return;
            }

            result.Flags |= TerrainChunkPagerConstants.ResultFlagSuccess;
            result.LatencyMs = ElapsedMilliseconds(start);
            PublishWorkerResult(in result);
        }

        private void PublishWorkerResult(in TerrainChunkWorkerResultDTO result)
        {
            SpinWait spin = new SpinWait();
            while (Volatile.Read(ref _workerRunning) != 0)
            {
                if (TryEnqueueWorkerResult(in result))
                    return;

                spin.SpinOnce();
            }
        }

        private bool TryLoadChunkFile(
            in TerrainChunkWorkerRequestDTO request,
            byte* staging,
            byte* compressed,
            int capacity,
            int compressedCapacity,
            ref TerrainChunkWorkerResultDTO result)
        {
            if (!TryOpenSectorReadStream(request.SectorHash, out FileStream stream, out bool missingFile))
            {
                result.Flags = missingFile
                    ? TerrainChunkPagerConstants.ResultFlagMissingFile
                    : TerrainChunkPagerConstants.ResultFlagIoError;
                return false;
            }

            try
            {
                using (stream) // BACKGROUND_WORKER_IO_1305_STREAMING: native path open, never executed on Unity main thread.
                {
                    TerrainChunkFileHeaderDTO header = default;
                    int headerBytes = CopyExactFromStream(stream, (byte*)&header, UnsafeUtility.SizeOf<TerrainChunkFileHeaderDTO>());
                    if (headerBytes != UnsafeUtility.SizeOf<TerrainChunkFileHeaderDTO>() ||
                        !TryNormalizeChunkHeader(ref header) ||
                        !TryValidateChunkHeader(in header, stream.Length, capacity, compressedCapacity, out int storedBytes, out int uncompressedBytes, out long payloadOffset))
                    {
                        result.Flags = TerrainChunkPagerConstants.ResultFlagInvalidHeader;
                        return false;
                    }

                    stream.Position = payloadOffset;

                    if (header.Compression == TerrainChunkPagerConstants.FileCompressionRaw)
                    {
                        int rawBytes = CopyExactFromStream(stream, staging, uncompressedBytes);
                        result.BytesWritten = rawBytes;
                        if (rawBytes != uncompressedBytes)
                            result.Flags |= TerrainChunkPagerConstants.ResultFlagPartialRead;
                        if (rawBytes <= 0)
                            return false;
                        if (!ValidateChunkPayloadCrc(staging, rawBytes, header.Crc32))
                        {
                            result.Flags |= TerrainChunkPagerConstants.ResultFlagChecksumMismatch;
                            return false;
                        }

                        return true;
                    }

                    if (header.Compression == TerrainChunkPagerConstants.FileCompressionLz4)
                    {
                        int readBytes = CopyExactFromStream(stream, compressed, storedBytes);
                        if (readBytes != storedBytes)
                        {
                            result.Flags = TerrainChunkPagerConstants.ResultFlagPartialRead;
                            return false;
                        }

                        if (!TerrainChunkLz4Codec.TryDecompress(compressed, storedBytes, staging, capacity, out int written) ||
                            written <= 0 ||
                            written != uncompressedBytes ||
                            written > capacity)
                        {
                            result.Flags = TerrainChunkPagerConstants.ResultFlagLz4Error;
                            return false;
                        }

                        if (!ValidateChunkPayloadCrc(staging, written, header.Crc32))
                        {
                            result.Flags = TerrainChunkPagerConstants.ResultFlagChecksumMismatch;
                            return false;
                        }

                        result.BytesWritten = written;
                        return true;
                    }

                    result.Flags = TerrainChunkPagerConstants.ResultFlagIoError;
                    return false;
                }
            }
            catch (IOException)
            {
                result.Flags = TerrainChunkPagerConstants.ResultFlagIoError;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                result.Flags = TerrainChunkPagerConstants.ResultFlagIoError;
                return false;
            }
        }

        private static bool TryNormalizeChunkHeader(ref TerrainChunkFileHeaderDTO header)
        {
            if (header.Magic == TerrainChunkPagerConstants.FileMagic)
                return true;

            if (ReverseUInt32(header.Magic) != TerrainChunkPagerConstants.FileMagic)
                return false;

            header.Magic = TerrainChunkPagerConstants.FileMagic;
            header.Version = ReverseUInt32(header.Version);
            header.StoredBytes = ReverseUInt32(header.StoredBytes);
            header.UncompressedBytes = ReverseUInt32(header.UncompressedBytes);
            header.Compression = ReverseUInt32(header.Compression);
            header.PayloadOffset = ReverseUInt32(header.PayloadOffset);
            header.Crc32 = ReverseUInt32(header.Crc32);
            header.Flags = ReverseUInt32(header.Flags);
            return true;
        }

        private static bool TryValidateChunkHeader(
            in TerrainChunkFileHeaderDTO header,
            long fileLength,
            int capacity,
            int compressedCapacity,
            out int storedBytes,
            out int uncompressedBytes,
            out long payloadOffset)
        {
            storedBytes = 0;
            uncompressedBytes = 0;
            payloadOffset = 0L;
            uint headerBytes = (uint)UnsafeUtility.SizeOf<TerrainChunkFileHeaderDTO>();
            if (capacity <= 0 ||
                compressedCapacity <= 0 ||
                fileLength <= headerBytes ||
                header.Version != TerrainChunkPagerConstants.FileVersion ||
                (header.Flags & ~TerrainChunkPagerConstants.FileFlagsMask) != 0u ||
                (header.Compression != TerrainChunkPagerConstants.FileCompressionRaw &&
                 header.Compression != TerrainChunkPagerConstants.FileCompressionLz4) ||
                header.StoredBytes == 0u ||
                header.UncompressedBytes == 0u ||
                header.StoredBytes > (uint)compressedCapacity ||
                header.UncompressedBytes > (uint)capacity ||
                header.PayloadOffset < headerBytes)
            {
                return false;
            }

            if (header.Compression == TerrainChunkPagerConstants.FileCompressionRaw &&
                header.StoredBytes != header.UncompressedBytes)
            {
                return false;
            }

            payloadOffset = header.PayloadOffset;
            long remaining = fileLength - payloadOffset;
            if (payloadOffset > fileLength || remaining < header.StoredBytes)
                return false;

            storedBytes = (int)header.StoredBytes;
            uncompressedBytes = (int)header.UncompressedBytes;
            return true;
        }

        private static bool ValidateChunkPayloadCrc(byte* payload, int byteCount, uint expectedCrc)
        {
            return payload != null &&
                   byteCount > 0 &&
                   H8Crc32.Compute(new ReadOnlySpan<byte>(payload, byteCount)) == expectedCrc;
        }

        private static uint ReverseUInt32(uint value)
        {
            return (value >> 24) |
                   ((value >> 8) & 0x0000FF00u) |
                   ((value << 8) & 0x00FF0000u) |
                   (value << 24);
        }

        private static int CopyExactFromStream(FileStream stream, byte* destination, int byteCount)
        {
            if (stream == null || destination == null || byteCount <= 0)
                return 0;

            int total = 0;
            Span<byte> span = new Span<byte>(destination, byteCount);
            while (total < byteCount)
            {
                int read = stream.Read(span.Slice(total)); // BACKGROUND_WORKER_IO_1305_STREAMING / COLD_BOOT_CONFIG_READ_1305_STREAMING
                if (read <= 0)
                    break;
                total += read;
            }

            return total;
        }

        private void SimulateMockDiskDelay(in TerrainChunkWorkerRequestDTO request)
        {
            int minMs = math.max(0, request.WorkerMockDelayMinMs);
            int maxMs = math.max(minMs, request.WorkerMockDelayMaxMs);
            if (maxMs <= 0)
                return;

            uint mixed = unchecked((uint)(request.SectorHash ^ (request.SectorHash >> 32)) ^ request.Sequence);
            int range = math.max(1, maxMs - minMs + 1);
            int delay = minMs + (int)(mixed % (uint)range);
            Thread.Sleep(delay);
        }

        private bool TryOpenSectorReadStream(ulong sectorHash, out FileStream stream, out bool missingFile)
        {
            stream = null;
            missingFile = false;
            if (!TryBuildSectorPathChars(sectorHash, out int charCount))
                return false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fixed (char* path = _pathBuffer)
            {
                SafeFileHandle handle = NativeChunkFileOpen.CreateFileW(
                    path,
                    NativeChunkFileOpen.GenericRead,
                    NativeChunkFileOpen.FileShareRead,
                    IntPtr.Zero,
                    NativeChunkFileOpen.OpenExisting,
                    NativeChunkFileOpen.FileAttributeNormal | NativeChunkFileOpen.FileFlagSequentialScan | NativeChunkFileOpen.FileFlagOverlapped,
                    IntPtr.Zero);
                if (handle == null || handle.IsInvalid)
                {
                    if (handle != null)
                        handle.Dispose();
                    int error = Marshal.GetLastWin32Error();
                    missingFile = error == NativeChunkFileOpen.ErrorFileNotFound || error == NativeChunkFileOpen.ErrorPathNotFound;
                    return false;
                }

                stream = new FileStream(handle, FileAccess.Read, 64 * 1024, true); // BACKGROUND_WORKER_IO_1305_STREAMING: SafeFileHandle stream only.
                return true;
            }
#else
            if (!TryEncodePathUtf8(_pathBuffer, charCount, _utf8PathBuffer, out int byteCount))
                return false;

            fixed (byte* path = _utf8PathBuffer)
            {
                int fd = NativeChunkFileOpen.OpenUnix(path, NativeChunkFileOpen.O_RDONLY);
                if (fd < 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    missingFile = error == NativeChunkFileOpen.ENOENT || error == NativeChunkFileOpen.ENOTDIR;
                    return false;
                }

                SafeFileHandle handle = new SafeFileHandle(new IntPtr(fd), true);
                stream = new FileStream(handle, FileAccess.Read, 64 * 1024, true); // BACKGROUND_WORKER_IO_1305_STREAMING: SafeFileHandle stream only.
                return true;
            }
#endif
        }

        private bool TryBuildSectorPathChars(ulong sectorHash, out int charCount)
        {
            charCount = 0;
            char[] buffer = _pathBuffer;
            if (buffer == null || string.IsNullOrEmpty(_chunkRootFullPath))
                return false;

            int cursor = 0;
            if (!Append(buffer, ref cursor, _chunkRootFullPath))
                return false;
            if (cursor > 0)
            {
                char last = buffer[cursor - 1];
                if (last != '\\' && last != '/')
                {
                    if (cursor >= buffer.Length)
                        return false;
                    buffer[cursor++] = Path.DirectorySeparatorChar;
                }
            }

            if (!Append(buffer, ref cursor, "sector_") ||
                !AppendHex64(buffer, ref cursor, sectorHash) ||
                !Append(buffer, ref cursor, ".h8bin"))
            {
                return false;
            }

            if (cursor >= buffer.Length)
                return false;

            buffer[cursor] = '\0';
            charCount = cursor;
            return true;
        }

        private static bool TryEncodePathUtf8(char[] source, int charCount, byte[] destination, out int byteCount)
        {
            byteCount = 0;
            if (source == null || destination == null || charCount < 0)
                return false;

            for (int i = 0; i < charCount; i++)
            {
                int code = source[i];
                if (code <= 0x7F)
                {
                    if (byteCount + 1 >= destination.Length)
                        return false;
                    destination[byteCount++] = (byte)code;
                }
                else if (code <= 0x7FF)
                {
                    if (byteCount + 2 >= destination.Length)
                        return false;
                    destination[byteCount++] = (byte)(0xC0 | (code >> 6));
                    destination[byteCount++] = (byte)(0x80 | (code & 0x3F));
                }
                else
                {
                    if (char.IsSurrogate(source[i]))
                        return false;
                    if (byteCount + 3 >= destination.Length)
                        return false;
                    destination[byteCount++] = (byte)(0xE0 | (code >> 12));
                    destination[byteCount++] = (byte)(0x80 | ((code >> 6) & 0x3F));
                    destination[byteCount++] = (byte)(0x80 | (code & 0x3F));
                }
            }

            destination[byteCount] = 0;
            return true;
        }

        private static bool Append(char[] buffer, ref int cursor, string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            int max = buffer.Length;
            if (text.Length > max - cursor)
                return false;

            for (int i = 0; i < text.Length; i++)
                buffer[cursor++] = text[i];
            return true;
        }

        private static bool AppendHex64(char[] buffer, ref int cursor, ulong value)
        {
            if (buffer.Length - cursor < 16)
                return false;

            for (int shift = 60; shift >= 0; shift -= 4)
            {
                int nibble = (int)((value >> shift) & 0xFUL);
                buffer[cursor++] = (char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10));
            }

            return true;
        }

        private static int FindFreeSlot(NativeArray<ChunkMetadataDTO> metadata, int count)
        {
            int safeCount = math.min(count, metadata.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChunkMetadataDTO meta = metadata[i];
                if (meta.SectorHash == 0UL)
                    return i;
            }

            return -1;
        }

        private static int FindSlotByHash(NativeArray<ChunkMetadataDTO> metadata, int count, ulong sectorHash)
        {
            int safeCount = math.min(count, metadata.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChunkMetadataDTO meta = metadata[i];
                if (meta.SectorHash == sectorHash &&
                    (meta.StateFlags & (TerrainChunkStateFlags.Active | TerrainChunkStateFlags.Loading | TerrainChunkStateFlags.ReadyToCommit | TerrainChunkStateFlags.MissingFile)) != 0u)
                {
                    return i;
                }
            }

            return -1;
        }

        private void WriteTelemetry()
        {
            if (!TryResolveArray(in _metadataHandle, _metadataLength, out NativeArray<ChunkMetadataDTO> metadata) ||
                !TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningRead))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            int metadataCount = math.min(_metadataLength, metadata.Length);
            int active = 0;
            int loading = 0;
            int stale = 0;
            for (int i = 0; i < metadataCount; i++)
            {
                uint flags = metadata[i].StateFlags;
                if ((flags & TerrainChunkStateFlags.Active) != 0u) active++;
                if ((flags & TerrainChunkStateFlags.Loading) != 0u) loading++;
                if ((flags & TerrainChunkStateFlags.Stale) != 0u) stale++;
            }

            int pendingLoads = CountRing(ref _requestHead, ref _requestTail);
            int pendingResults = CountRing(ref _resultHead, ref _resultTail);
            TerrainChunkPagerTuningDTO tuning = tuningRead[0];
            if (Volatile.Read(ref _workerRunning) != 0 &&
                (Volatile.Read(ref _workerThreadActive) == 0 ||
                 IsWorkerHeartbeatStale(Volatile.Read(ref _workerHeartbeatTimestamp), tuning.CriticalLatencyMs, pendingLoads, loading)))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
            }

            uint missingFileCount;
            if (!TryAcquireWriteArray(in _countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO> countersWrite, out IDataVault countersVault))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            try
            {
                TerrainChunkPagerCountersDTO counters = countersWrite[0];
                counters.Frame = _frameId;
                counters.ActiveChunks = active;
                counters.LoadingChunks = loading;
                counters.StaleChunks = stale;
                counters.EvictedChunks = _evictedChunksTotal;
                counters.PendingRequests = pendingLoads;
                counters.PendingResults = pendingResults;
                counters.LatencyEwmaMs = tuning.LatencyEwmaMs;
                counters.EffectiveRingRadius = tuning.EffectiveRingRadius;
                counters.LastFaultFlags = _faultFlags;
                counters.WorkerSequence = _workerSequence;
                counters.LayoutValid = _layoutValid;
                missingFileCount = counters.MissingFileCount;
                countersWrite[0] = counters;
            }
            finally
            {
                ReleaseWriteArray(countersVault, in _countersHandle);
            }

            if (!TryAcquireWriteArray(in _telemetryRingHandle, _telemetryLength, out NativeArray<PagerTelemetryEntry> telemetryWrite, out IDataVault telemetryVault))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            try
            {
                int cursor = _telemetryCursor;
                PagerTelemetryEntry entry = default;
                entry.CameraAup = _lastCameraAup;
                entry.Frame = _frameId;
                entry.StateHash = TerrainChunkPagerMath.HashMetadata(metadata, metadataCount);
                entry.ActiveChunks = (ushort)math.min(ushort.MaxValue, active);
                entry.LoadingChunks = (ushort)math.min(ushort.MaxValue, loading);
                entry.StaleChunks = (ushort)math.min(ushort.MaxValue, stale);
                entry.PendingLoads = (ushort)math.min(ushort.MaxValue, pendingLoads);
                entry.LatencyEwmaMs = tuning.LatencyEwmaMs;
                entry.ResidencyEvalMicros = _lastEvalMicros;
                entry.EffectiveRingRadius = tuning.EffectiveRingRadius;
                entry.Flags = _faultFlags;
                entry.MissingFileCount = missingFileCount;
                entry.WorkerSequence = _workerSequence;
                telemetryWrite[cursor] = entry;
                _telemetryCursor = (cursor + 1) % _telemetryLength;

                if (_faultFlags != 0u)
                    RequestTelemetryDumpOnce();
            }
            finally
            {
                ReleaseWriteArray(telemetryVault, in _telemetryRingHandle);
            }
        }

        private int CountRing(ref int head, ref int tail)
        {
            int safeHead = Volatile.Read(ref head);
            int safeTail = Volatile.Read(ref tail);
            return (safeHead - safeTail) & _queueMask;
        }

        private static bool IsWorkerHeartbeatStale(long heartbeatTimestamp, float criticalLatencyMs, int pendingLoads, int loadingChunks)
        {
            if (heartbeatTimestamp <= 0L || (pendingLoads | loadingChunks) == 0)
                return false;

            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - heartbeatTimestamp;
            double elapsedMs = (double)delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            float limitMs = math.max(5000f, math.max(1f, criticalLatencyMs) * 8f);
            return elapsedMs > limitMs;
        }

        private void IncrementMissingFile()
        {
            if (!TryAcquireWriteArray(in _countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO> countersBuffer, out IDataVault countersVault))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultMissingFile;
                return;
            }

            try
            {
                TerrainChunkPagerCountersDTO counters = countersBuffer[0];
                counters.MissingFileCount++;
                counters.LastFaultFlags |= TerrainChunkPagerConstants.TelemetryFaultMissingFile;
                countersBuffer[0] = counters;
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultMissingFile;
            }
            finally
            {
                ReleaseWriteArray(countersVault, in _countersHandle);
            }
        }

        private void IncrementIoError()
        {
            if (!TryAcquireWriteArray(in _countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO> countersBuffer, out IDataVault countersVault))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
                return;
            }

            try
            {
                TerrainChunkPagerCountersDTO counters = countersBuffer[0];
                counters.IoErrorCount++;
                counters.LastFaultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
                countersBuffer[0] = counters;
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
            }
            finally
            {
                ReleaseWriteArray(countersVault, in _countersHandle);
            }
        }

        private void IncrementLz4Error()
        {
            if (!TryAcquireWriteArray(in _countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO> countersBuffer, out IDataVault countersVault))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultLz4;
                return;
            }

            try
            {
                TerrainChunkPagerCountersDTO counters = countersBuffer[0];
                counters.Lz4ErrorCount++;
                counters.LastFaultFlags |= TerrainChunkPagerConstants.TelemetryFaultLz4;
                countersBuffer[0] = counters;
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultLz4;
            }
            finally
            {
                ReleaseWriteArray(countersVault, in _countersHandle);
            }
        }

        private void IncrementQueueOverflow()
        {
            if (!TryAcquireWriteArray(in _countersHandle, 1, out NativeArray<TerrainChunkPagerCountersDTO> countersBuffer, out IDataVault countersVault))
                return;

            try
            {
                TerrainChunkPagerCountersDTO counters = countersBuffer[0];
                counters.QueueOverflowCount++;
                counters.LastFaultFlags |= TerrainChunkPagerConstants.TelemetryFaultQueueOverflow;
                countersBuffer[0] = counters;
            }
            finally
            {
                ReleaseWriteArray(countersVault, in _countersHandle);
            }
        }

        private bool TryReadCameraAupSnapshot(out double3 cameraAup)
        {
            int sequence0 = Volatile.Read(ref _cameraAupSequence);
            if (sequence0 != 0 && (sequence0 & 1) == 0)
            {
                long x = Volatile.Read(ref _cameraAupBitsX);
                long y = Volatile.Read(ref _cameraAupBitsY);
                long z = Volatile.Read(ref _cameraAupBitsZ);
                int sequence1 = Volatile.Read(ref _cameraAupSequence);
                if (sequence0 == sequence1 && (sequence1 & 1) == 0)
                {
                    cameraAup = new double3(LongBitsToDouble(x), LongBitsToDouble(y), LongBitsToDouble(z));
                    return math.all(math.isfinite(cameraAup));
                }
            }

            IPlayerRuntimeContext runtimeContext = ResolveCachedPlayerRuntimeContext();
            if (runtimeContext != null)
            {
                if (TryReadCameraAupFromRuntimeContext(runtimeContext, out cameraAup))
                    return true;
            }

            if (useMockCameraAupWhenNoPlayer)
            {
                cameraAup = new double3(mockCameraAupMeters.x, mockCameraAupMeters.y, mockCameraAupMeters.z);
                return math.all(math.isfinite(cameraAup));
            }

            cameraAup = default;
            return false;
        }

        private IPlayerRuntimeContext ResolveCachedPlayerRuntimeContext()
        {
            IPlayerRuntimeContext runtimeContext = _cachedRuntimeContext;
            if (IsPlayerRuntimeContextBound(runtimeContext))
                return runtimeContext;

            runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (IsPlayerRuntimeContextBound(runtimeContext))
            {
                _cachedRuntimeContext = runtimeContext;
                return runtimeContext;
            }

            _cachedRuntimeContext = null;
            return null;
        }

        private static bool TryReadCameraAupFromRuntimeContext(
            IPlayerRuntimeContext runtimeContext,
            out double3 cameraAup)
        {
            cameraAup = default;
            if (!IsPlayerRuntimeContextBound(runtimeContext))
                return false;

            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite())
            {
                cameraAup = snapshot.Aup.ToAbsoluteDouble3();
                return math.all(math.isfinite(cameraAup));
            }

            if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                cameraAup = movementState.PredictedAup.ToAbsoluteDouble3();
                return math.all(math.isfinite(cameraAup));
            }

            return false;
        }

        private static bool IsPlayerRuntimeContextBound(IPlayerRuntimeContext runtimeContext)
        {
            return runtimeContext != null &&
                   runtimeContext.IsInitialized &&
                   runtimeContext.PlayerTransform != null;
        }

        private void LoadColdStreamingProfile()
        {
#if !UNITY_EDITOR
            return;
#else
            if (!loadCsvProfileOnEnable ||
                _csvScratchByteLength <= 0 ||
                !TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningRead))
            {
                return;
            }

            if (!TryResolveArray(in _hardwareProfilesHandle, _hardwareProfileLength, out NativeArray<StreamingHardwareProfileDTO> hardwareProfiles) ||
                !TryResolveArray(in _csvScratchBytesHandle, _csvScratchByteLength, out NativeArray<byte> csvScratch))
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultVaultUnavailable;
                return;
            }

            string path = Path.Combine(ResolveProjectRoot(), StreamingProfileCsvRelativePath);
            try
            {
                using (FileStream stream = new FileStream( // COLD_BOOT_CONFIG_READ_1305_STREAMING: one-shot CSV ingest into native scratch.
                           path,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite,
                           4096,
                           FileOptions.SequentialScan))
                {
                    int byteCount = (int)math.min(stream.Length, _csvScratchByteLength);
                    if (byteCount <= 0)
                        return;

                    byte* csvScratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
                    int read = CopyExactFromStream(stream, csvScratchPtr, byteCount);
                    if (read <= 0)
                        return;

                    TerrainChunkPagerTuningDTO tuning = tuningRead[0];
                    ReadOnlySpan<byte> csv = new ReadOnlySpan<byte>(csvScratchPtr, read);
                    if (TerrainChunkStreamingProfileCsvParser.TryParse(csv, ref tuning, hardwareProfiles, out _csvProfileCount))
                    {
                        tuning.ChunkByteCapacity = _allocatedChunkByteCapacity;
                        tuning.MaxQueuedLoads = math.clamp(tuning.MaxQueuedLoads, 1, math.max(1, queueCapacity - 1));
                        tuning.CommitByteBudgetPerFrame = math.min(
                            tuning.CommitByteBudgetPerFrame,
                            ResolveCommitByteBudget(_allocatedChunkByteCapacity, tuning.MaxCommitsPerVisualSync));
                        if (TryAcquireWriteArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO> tuningWrite, out IDataVault tuningVault))
                        {
                            try
                            {
                                tuningWrite[0] = TerrainChunkPagerMath.Sanitize(tuning);
                            }
                            finally
                            {
                                ReleaseWriteArray(tuningVault, in _tuningHandle);
                            }
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
            }
            catch (UnauthorizedAccessException)
            {
                _faultFlags |= TerrainChunkPagerConstants.TelemetryFaultIo;
            }
#endif
        }

        private void RequestTelemetryDumpOnce()
        {
            uint faults = _faultFlags;
            uint newFaults = faults & ~_lastDumpFaultFlags;
            if (newFaults == 0u)
                return;

            _lastDumpFaultFlags |= newFaults;
            uint frame = _frameId;
            _lastDumpFrame = unchecked((int)frame);
            if (!CopyTelemetrySnapshotForDump())
                return;

            ulong packed = ((ulong)faults << 32) | frame;
            Interlocked.Exchange(ref _dumpRequestPacked, unchecked((long)packed));
            AutoResetEvent wake = _workerWake;
            if (wake != null)
                wake.Set();
        }

        private void TryDrainTelemetryDumpRequestOnWorker()
        {
            long packedLong = Interlocked.Exchange(ref _dumpRequestPacked, 0L);
            if (packedLong == 0L)
                return;

            ulong packed = unchecked((ulong)packedLong);
            DumpTelemetryOnWorker((uint)packed, (uint)(packed >> 32));
        }

        private bool CopyTelemetrySnapshotForDump()
        {
            if (_telemetryLength <= 0 ||
                !TryResolveArray(in _telemetryRingHandle, _telemetryLength, out NativeArray<PagerTelemetryEntry> telemetryRead) ||
                !TryResolveArray(in _telemetryDumpSnapshotBytesHandle, _dumpSnapshotByteLength, out NativeArray<byte> telemetryDumpSnapshotBytes))
            {
                return false;
            }

            int bytes = _telemetryLength * UnsafeUtility.SizeOf<PagerTelemetryEntry>();
            if (bytes <= 0 || bytes > _telemetryDumpSnapshotByteLength)
                return false;

            void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRead);
            void* snapshotPtr = NativeArrayUnsafeUtility.GetUnsafePtr(telemetryDumpSnapshotBytes);
            UnsafeUtility.MemCpy(snapshotPtr, telemetryPtr, bytes);
            return true;
        }

        private void DumpTelemetryOnWorker(uint frame, uint faults)
        {
            if (_telemetryLength <= 0 ||
                !TryResolveArray(in _telemetryDumpSnapshotBytesHandle, _dumpSnapshotByteLength, out NativeArray<byte> telemetryDumpSnapshotBytes))
                return;

            int bytes = _telemetryLength * UnsafeUtility.SizeOf<PagerTelemetryEntry>();
            if (bytes <= 0 || bytes > _telemetryDumpSnapshotByteLength)
                return;

            try
            {
                int totalBytes = DumpHeaderBytes + bytes;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(TerrainChunkPagerRuntime),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory,
                    Allocator.TempJob);
                try
                {
                    unsafe
                    {
                        byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                        Span<byte> header = new Span<byte>(payloadPtr, DumpHeaderBytes);
                        WriteUInt64(header, 0, HectonDumpMagic);
                        WriteUInt32(header, 8, DumpVersion);
                        WriteUInt32(header, 12, (uint)_telemetryLength);
                        WriteUInt32(header, 16, (uint)UnsafeUtility.SizeOf<PagerTelemetryEntry>());
                        WriteUInt32(header, 20, faults);
                        WriteUInt32(header, 24, DumpLayoutHash);
                        WriteUInt32(header, 28, 0u);

                        void* snapshotPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryDumpSnapshotBytes);
                        UnsafeUtility.MemCpy(payloadPtr + DumpHeaderBytes, snapshotPtr, bytes);
                    }

                    NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(TerrainChunkPagerRuntime),
                        DumpPayloadLabel,
                        Allocator.TempJob);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private string ResolveChunkRootPath()
        {
            if (!string.IsNullOrEmpty(chunkRootRelativePath) && Path.IsPathRooted(chunkRootRelativePath))
                return chunkRootRelativePath;

            string streamingRoot = Application.streamingAssetsPath;
            return Path.Combine(streamingRoot, string.IsNullOrEmpty(chunkRootRelativePath) ? DefaultChunkRootRelativePath : chunkRootRelativePath);
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        private static int NextPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value && result < 4096)
                result <<= 1;
            return result;
        }

        private static uint ElapsedMicroseconds(long startTimestamp)
        {
            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double micros = (double)delta * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
            return (uint)math.clamp((int)micros, 0, int.MaxValue);
        }

        private static float ElapsedMilliseconds(long startTimestamp)
        {
            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double ms = (double)delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return math.isfinite(ms) ? (float)math.max(0.0, ms) : 0f;
        }

        private static bool TryResolveLz4BoundedCapacity(int uncompressedCapacity, out int compressedCapacity)
        {
            compressedCapacity = 0;
            if (uncompressedCapacity < 4096)
                return false;

            long bound = (long)uncompressedCapacity + (uncompressedCapacity / 255L) + 16L;
            if (bound <= 0L || bound > int.MaxValue)
                return false;

            compressedCapacity = (int)bound;
            return true;
        }

        private static bool TryResolveChunkSlabByteLength(int slots, int chunkBytes, out int byteLength)
        {
            byteLength = 0;
            long total = (long)slots * chunkBytes;
            if (slots <= 0 || chunkBytes < 4096 || total <= 0L || total > int.MaxValue)
                return false;

            byteLength = (int)total;
            return true;
        }

        private static bool TryResolveTelemetrySnapshotByteLength(out int byteLength)
        {
            byteLength = TerrainChunkPagerConstants.TelemetryCapacity * UnsafeUtility.SizeOf<PagerTelemetryEntry>();
            return byteLength > 0;
        }

        private static float ResolveCommitByteBudget(int chunkBytes, int commitCount)
        {
            float safeChunkBytes = math.max(4096f, (float)math.max(0, chunkBytes));
            float safeCommitCount = math.max(1f, (float)math.max(1, commitCount));
            float budget = safeChunkBytes * safeCommitCount;
            return math.min(math.max(4096f, budget), (float)int.MaxValue);
        }

        private static unsafe class NativeChunkFileOpen
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            public const uint GenericRead = 0x80000000u;
            public const uint FileShareRead = 0x00000001u;
            public const uint OpenExisting = 3u;
            public const uint FileAttributeNormal = 0x00000080u;
            public const uint FileFlagOverlapped = 0x40000000u;
            public const uint FileFlagSequentialScan = 0x08000000u;
            public const int ErrorFileNotFound = 2;
            public const int ErrorPathNotFound = 3;

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
            public static extern SafeFileHandle CreateFileW(
                char* lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);
#else
            public const int O_RDONLY = 0;
            public const int ENOENT = 2;
            public const int ENOTDIR = 20;

            [DllImport("libc", SetLastError = true, EntryPoint = "open")]
            public static extern int OpenUnix(byte* pathname, int flags);
#endif
        }

        private static void WriteUInt32(Span<byte> span, int offset, uint value)
        {
            span[offset] = (byte)value;
            span[offset + 1] = (byte)(value >> 8);
            span[offset + 2] = (byte)(value >> 16);
            span[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(Span<byte> span, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                span[offset + i] = (byte)(value >> (i * 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double LongBitsToDouble(long bits)
        {
            return *(double*)&bits;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos ||
                _initialized == 0 ||
                !TryReadOnlyArray(in _metadataHandle, 1, out NativeArray<ChunkMetadataDTO>.ReadOnly metadata) ||
                !TryReadOnlyArray(in _sectorCoordsHandle, 1, out NativeArray<TerrainChunkSectorCoordDTO>.ReadOnly sectorCoords))
            {
                return;
            }

            TerrainChunkPagerTuningDTO tuning = TryReadOnlyArray(in _tuningHandle, 1, out NativeArray<TerrainChunkPagerTuningDTO>.ReadOnly tuningRead)
                ? tuningRead[0]
                : TerrainChunkPagerTuningDTO.CreateDefault();
            float sectorSize = math.max(1f, tuning.SectorSizeMeters);
            Vector3 root = transform.position;
            Vector3 size = new Vector3(sectorSize, debugGizmoHeightMeters, sectorSize);
            int count = math.min(_metadataLength, math.min(metadata.Length, sectorCoords.Length));
            for (int i = 0; i < count; i++)
            {
                ChunkMetadataDTO meta = metadata[i];
                if (meta.SectorHash == 0UL)
                    continue;

                uint flags = meta.StateFlags;
                if ((flags & TerrainChunkStateFlags.Loading) != 0u)
                    Gizmos.color = Color.yellow;
                else if ((flags & TerrainChunkStateFlags.Stale) != 0u)
                    Gizmos.color = Color.red;
                else if ((flags & TerrainChunkStateFlags.Active) != 0u)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.gray;

                TerrainChunkSectorCoordDTO coord = sectorCoords[i];
                float x = (float)(coord.X - _lastCameraSectorX) * sectorSize;
                float z = (float)(coord.Z - _lastCameraSectorZ) * sectorSize;
                Vector3 center = root + new Vector3(x + sectorSize * 0.5f, 0f, z + sectorSize * 0.5f);
                Gizmos.DrawWireCube(center, size);
            }
        }
#endif

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly TerrainChunkPagerRuntime _owner;
            public PreSimulationPhaseSystem(TerrainChunkPagerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x54325052u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly TerrainChunkPagerRuntime _owner;
            public PostSimulationPhaseSystem(TerrainChunkPagerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x5432504Fu; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly TerrainChunkPagerRuntime _owner;
            public VisualSyncPhaseSystem(TerrainChunkPagerRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x54325056u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }
}
