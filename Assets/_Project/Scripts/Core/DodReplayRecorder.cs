#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Hecton8.Core
{
    /// <summary>
    /// Fixed-size replay snapshot header. Keep exactly 128 bytes for forward-compatible parsers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DodReplaySnapshotHeader
    {
        /// <summary>Replay file magic.</summary>
        [FieldOffset(0)] public ulong Magic;
        /// <summary>Binary format version.</summary>
        [FieldOffset(8)] public uint Version;
        /// <summary>Header byte size.</summary>
        [FieldOffset(12)] public ushort HeaderSizeBytes;
        /// <summary>Segment header byte size.</summary>
        [FieldOffset(14)] public ushort SegmentHeaderSizeBytes;
        /// <summary>Unity frame index captured by the recorder.</summary>
        [FieldOffset(16)] public uint FrameIndex;
        /// <summary>Monotonic replay snapshot sequence.</summary>
        [FieldOffset(20)] public uint SnapshotSequence;
        /// <summary>Number of segment headers in this snapshot.</summary>
        [FieldOffset(24)] public uint SegmentCount;
        /// <summary>Snapshot flags.</summary>
        [FieldOffset(28)] public uint Flags;
        /// <summary>High precision timestamp from Unity realtime.</summary>
        [FieldOffset(32)] public double PrecisionTimestamp;
        /// <summary>Total snapshot payload bytes staged after this header.</summary>
        [FieldOffset(40)] public long PayloadBytes;
        /// <summary>Total source bytes seen before delta filtering/truncation.</summary>
        [FieldOffset(48)] public long TotalSourceBytes;
        /// <summary>Total bytes dropped because the staging page was full.</summary>
        [FieldOffset(56)] public long DroppedBytes;
        /// <summary>Circular replay-file write offset used for this snapshot.</summary>
        [FieldOffset(64)] public long WriteOffset;
        /// <summary>Faulting system/entity subject hash, if this is a fault dump.</summary>
        [FieldOffset(72)] public uint SubjectHash;
        /// <summary>Numeric error code, if this is a fault dump.</summary>
        [FieldOffset(76)] public uint ErrorCode;
        /// <summary>Frame-indexed replay seed used for this snapshot.</summary>
        [FieldOffset(80)] public uint ReplaySeed;
        /// <summary>Native source count scanned from the sentinel.</summary>
        [FieldOffset(84)] public uint SourceCount;
        [FieldOffset(88)] public ulong Reserved0;
        [FieldOffset(96)] public ulong Reserved1;
        [FieldOffset(104)] public ulong Reserved2;
        [FieldOffset(112)] public ulong Reserved3;
        [FieldOffset(120)] public ulong Reserved4;
    }

    /// <summary>
    /// Fixed-size segment header for one captured native buffer or replay sidecar.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DodReplaySegmentHeader
    {
        /// <summary>Owning system hash.</summary>
        [FieldOffset(0)] public uint OwnerHash;
        /// <summary>Native allocation label hash.</summary>
        [FieldOffset(4)] public uint LabelHash;
        /// <summary>Source allocation byte count.</summary>
        [FieldOffset(8)] public long SourceBytes;
        /// <summary>Bytes copied after this segment header.</summary>
        [FieldOffset(16)] public int PayloadBytes;
        /// <summary>Allocation registration frame.</summary>
        [FieldOffset(20)] public int AllocationFrame;
        /// <summary>Previous FNV64 source hash.</summary>
        [FieldOffset(24)] public ulong PreviousHash;
        /// <summary>Current FNV64 source hash.</summary>
        [FieldOffset(32)] public ulong CurrentHash;
        /// <summary>Segment flags.</summary>
        [FieldOffset(40)] public uint Flags;
        /// <summary>Segment index inside the snapshot.</summary>
        [FieldOffset(44)] public uint SegmentIndex;
        /// <summary>Offset from the snapshot header to the segment payload.</summary>
        [FieldOffset(48)] public long PayloadOffset;
        [FieldOffset(56)] public long Reserved;
    }

    /// <summary>
    /// Binary hardware-input journal event for deterministic replay.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DodReplayInputEvent
    {
        /// <summary>Double precision input timestamp.</summary>
        [FieldOffset(0)] public double PrecisionTimestamp;
        /// <summary>Frame index at recording time.</summary>
        [FieldOffset(8)] public uint FrameIndex;
        /// <summary>Monotonic input sequence number.</summary>
        [FieldOffset(12)] public uint Sequence;
        /// <summary>Input device hash.</summary>
        [FieldOffset(16)] public uint DeviceHash;
        /// <summary>Input control or event-type hash.</summary>
        [FieldOffset(20)] public uint ControlHash;
        /// <summary>Input phase or event hash.</summary>
        [FieldOffset(24)] public uint PhaseHash;
        /// <summary>Primary scalar value.</summary>
        [FieldOffset(28)] public float Value0;
        /// <summary>Secondary scalar value.</summary>
        [FieldOffset(32)] public float Value1;
        /// <summary>Tertiary scalar value.</summary>
        [FieldOffset(36)] public float Value2;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>
    /// Fixed job-completion sample stored in replay sidecars.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DodReplayJobProfileRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Profiled subject hash.</summary>
        [FieldOffset(4)] public uint SubjectHash;
        /// <summary>Completion duration in microseconds.</summary>
        [FieldOffset(8)] public uint CompletionMicroseconds;
        /// <summary>Worker or lane index.</summary>
        [FieldOffset(12)] public ushort WorkerIndex;
        /// <summary>Flags.</summary>
        [FieldOffset(14)] public ushort Flags;
        /// <summary>Error code when completion represents a stall.</summary>
        [FieldOffset(16)] public uint ErrorCode;
        [FieldOffset(20)] private uint _pad0;
        /// <summary>Reserved.</summary>
        [FieldOffset(24)] public ulong Reserved;
    }

    /// <summary>
    /// Burst panic capture header. Full job-data bytes are stored in the panic payload sidecar.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DodReplayBurstPanicRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(8)]
        public uint FrameIndex;
        /// <summary>Faulting job or system hash.</summary>
        [FieldOffset(12)]
        public uint SubjectHash;
        /// <summary>Numeric panic code.</summary>
        [FieldOffset(16)]
        public uint ErrorCode;
        /// <summary>Byte offset into the panic payload ring.</summary>
        [FieldOffset(20)]
        public uint PayloadOffsetBytes;
        /// <summary>Copied job-data bytes.</summary>
        [FieldOffset(28)]
        public ushort PayloadBytes;
        /// <summary>Total job-data size.</summary>
        [FieldOffset(30)]
        public ushort SourceBytes;
        /// <summary>FNV64 hash of full job data.</summary>
        [FieldOffset(0)]
        public ulong JobDataHash;
        /// <summary>Flags.</summary>
        [FieldOffset(24)]
        public uint Flags;
    }

    /// <summary>
    /// AUP drift detector result over a 1000-frame sample window.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct DodReplayAupDriftRecord
    {
        /// <summary>Frame index where drift was evaluated.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Subject hash.</summary>
        [FieldOffset(4)] public uint SubjectHash;
        /// <summary>Frame window length.</summary>
        [FieldOffset(8)] public uint FrameSpan;
        /// <summary>Flags.</summary>
        [FieldOffset(12)] public uint Flags;
        /// <summary>Grid delta X.</summary>
        [FieldOffset(16)] public long GridDeltaX;
        /// <summary>Grid delta Y.</summary>
        [FieldOffset(24)] public long GridDeltaY;
        /// <summary>Grid delta Z.</summary>
        [FieldOffset(32)] public long GridDeltaZ;
        /// <summary>Maximum absolute local drift.</summary>
        [FieldOffset(40)] public float MaxLocalDrift;
        /// <summary>Reserved.</summary>
        [FieldOffset(44)] public uint Reserved;
    }

    /// <summary>
    /// Entity ghost breadcrumb for editor replay overlays.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DodReplayEntityGhostRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Entity hash or stable id.</summary>
        [FieldOffset(4)] public uint EntityHash;
        /// <summary>Runtime position.</summary>
        [FieldOffset(8)] public float3 Position;
        /// <summary>Flags.</summary>
        [FieldOffset(20)] public uint Flags;
        /// <summary>Sequence.</summary>
        [FieldOffset(24)] public uint Sequence;
        [FieldOffset(28)] private uint _pad0;
    }

    /// <summary>
    /// Debug vector for logistics Jacobi flow visualization.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct DodReplayLogisticFlowRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Edge hash.</summary>
        [FieldOffset(4)] public uint EdgeHash;
        /// <summary>Source point.</summary>
        [FieldOffset(8)] public float3 From;
        /// <summary>Destination point.</summary>
        [FieldOffset(20)] public float3 To;
        /// <summary>Scalar potential.</summary>
        [FieldOffset(32)] public float Potential;
        /// <summary>Flags.</summary>
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] private ulong _pad0;
    }

    /// <summary>
    /// Atmosphere pressure/gas grid cell sample.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct DodReplayAtmosphereCellRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Cell hash.</summary>
        [FieldOffset(4)] public uint CellHash;
        /// <summary>Grid X.</summary>
        [FieldOffset(8)] public int X;
        /// <summary>Grid Y.</summary>
        [FieldOffset(12)] public int Y;
        /// <summary>Oxygen concentration.</summary>
        [FieldOffset(16)] public float Oxygen01;
        /// <summary>Carbon dioxide concentration.</summary>
        [FieldOffset(20)] public float CarbonDioxide01;
        /// <summary>Pressure in kPa.</summary>
        [FieldOffset(24)] public float PressureKpa;
        /// <summary>Flags.</summary>
        [FieldOffset(28)] public uint Flags;
        /// <summary>Reserved.</summary>
        [FieldOffset(32)] public ulong Reserved;
    }

    /// <summary>
    /// Graphics buffer allocation sample captured near faults.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DodReplayVramAllocationRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Owner hash.</summary>
        [FieldOffset(4)] public uint OwnerHash;
        /// <summary>Label hash.</summary>
        [FieldOffset(8)] public uint LabelHash;
        [FieldOffset(12)] private uint _pad0;
        /// <summary>Allocation byte count.</summary>
        [FieldOffset(16)] public long Bytes;
        /// <summary>Graphics buffer stride.</summary>
        [FieldOffset(24)] public uint Stride;
        /// <summary>Flags.</summary>
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>
    /// Deterministic physics smoke-test result.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct DodReplayPhysicsSmokeRecord
    {
        /// <summary>Frame index.</summary>
        [FieldOffset(0)] public uint FrameIndex;
        /// <summary>Test hash.</summary>
        [FieldOffset(4)] public uint TestHash;
        /// <summary>First run FNV64 state hash.</summary>
        [FieldOffset(8)] public ulong RunAHash;
        /// <summary>Second run FNV64 state hash.</summary>
        [FieldOffset(16)] public ulong RunBHash;
        /// <summary>Grid delta X.</summary>
        [FieldOffset(24)] public long GridDeltaX;
        /// <summary>Grid delta Y.</summary>
        [FieldOffset(32)] public long GridDeltaY;
        /// <summary>Grid delta Z.</summary>
        [FieldOffset(40)] public long GridDeltaZ;
        /// <summary>Flags. Bit0 means mismatch.</summary>
        [FieldOffset(48)] public uint Flags;
        /// <summary>Local maximum drift.</summary>
        [FieldOffset(52)] public float MaxLocalDrift;
    }

    /// <summary>
    /// Development-build deterministic replay recorder for DOD native buffers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed unsafe class DodReplayRecorder : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int SnapshotIntervalFrames = 10;
        private const int MaxSnapshotSources = 1024;
        private const int SnapshotScratchBytes = 2 * 1024 * 1024;
        private const int ReplayFileWriteScratchBytes = 64 * 1024;
        private const int InputJournalCapacity = 512;
        private const int SidecarCapacity = 256;
        private const int GhostCapacity = 128;
        private const int PanicPayloadCapacity = 64;
        private const int PanicPayloadStrideBytes = 256;
        private const int AupTrackedSubjectCapacity = 256;
        private const int AupDriftWindowFrames = 1000;
        private const int HeaderSizeBytes = 128;
        private const int SegmentHeaderSizeBytes = 64;
        private const int ReplayVersion = 3;
        private const int WriterJoinMilliseconds = 250;
        private const SystemID NativeMemoryOwner = SystemID.CoreDiagnostics;
        private const long ReplayFileCapacityBytes = 499L * 1024L * 1024L;
        private const ulong ReplayMagic = 0x48385245504C4159ul;
        private const ulong FnvOffset = 14695981039346656037ul;
        private const ulong FnvPrime = 1099511628211ul;
        private const float AupDriftThreshold = 0.0001f;
        private const uint SnapshotFlagForced = 1u << 0;
        private const uint SnapshotFlagTruncated = 1u << 1;
        private const uint SegmentFlagChanged = 1u << 0;
        private const uint SegmentFlagUnchanged = 1u << 1;
        private const uint SegmentFlagTruncated = 1u << 2;
        private const uint SegmentFlagInputJournal = 1u << 3;
        private const uint SegmentFlagReplaySidecar = 1u << 4;
        private const uint SegmentFlagDeltaSuppressed = 1u << 5;
        private const uint PanicPayloadTruncatedFlag = 1u << 0;
        private const uint AupDriftExceededFlag = 1u << 0;
        private const uint PhysicsSmokeMismatchFlag = 1u << 0;
        private const uint ErrorCodeRemoteSnapshot = 0x52534E50u;
        private const uint ErrorCodeBurstPanic = 0x42505221u;
        private const uint ErrorCodeAupDrift = 0x41555044u;
        private const uint ErrorCodeAupNonFinite = 0x4155504Eu;
        private const uint ErrorCodePhysicsSmokeMismatch = 0x50535921u;

        private static readonly uint _recorderOwnerHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(DodReplayRecorder));
        private static readonly uint _inputJournalOwnerHash = NativeMemorySentinel.ComputeSnapshotHash("ReplayInputJournal");
        private static readonly uint _inputJournalLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_inputJournal));
        private static readonly uint _jobProfileLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_jobProfiles));
        private static readonly uint _panicRecordLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_panicRecords));
        private static readonly uint _panicPayloadLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_panicPayloadBytes));
        private static readonly uint _aupDriftLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_aupDriftRecords));
        private static readonly uint _ghostLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_ghostRecords));
        private static readonly uint _logisticFlowLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_logisticFlowRecords));
        private static readonly uint _atmosphereLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_atmosphereRecords));
        private static readonly uint _vramLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_vramRecords));
        private static readonly uint _physicsSmokeLabelHash = NativeMemorySentinel.ComputeSnapshotHash(nameof(_physicsSmokeRecords));
        private static readonly Action<InputEventPtr, InputDevice> _inputEventDelegate = HandleInputEvent;

        private static DodReplayRecorder _activeRecorder;

#if UNITY_EDITOR
        [SerializeField] private bool _drawWireReplayOverlay = true;
        [SerializeField] private uint _wireReplayEntityFilter;
        [SerializeField, Range(1, 100)] private int _wireReplayFrameCount = 100;
        [SerializeField, Min(0.01f)] private float _wireReplayCubeSize = 0.25f;
#endif

        // COLD ALLOC: object[1] - writer monitor gate - owner: DodReplayRecorder
        private readonly object _writerGate = new object();
        private DodReplayNativeBufferSet _nativeBuffers = new DodReplayNativeBufferSet();
        private ref NativeArray<NativeAllocationSnapshotSource> _sources => ref _nativeBuffers.Sources;
        private ref NativeArray<byte> _snapshotScratch => ref _nativeBuffers.SnapshotScratch;
        private ref NativeArray<ReplaySourceHash> _sourceHashes => ref _nativeBuffers.SourceHashes;
        private ref NativeArray<DodReplayInputEvent> _inputJournal => ref _nativeBuffers.InputJournal;
        private ref NativeArray<DodReplayJobProfileRecord> _jobProfiles => ref _nativeBuffers.JobProfiles;
        private ref NativeArray<DodReplayBurstPanicRecord> _panicRecords => ref _nativeBuffers.PanicRecords;
        private ref NativeArray<byte> _panicPayloadBytes => ref _nativeBuffers.PanicPayloadBytes;
        private ref NativeArray<DodReplayAupDriftRecord> _aupDriftRecords => ref _nativeBuffers.AupDriftRecords;
        private ref NativeArray<AupDriftState> _aupDriftStates => ref _nativeBuffers.AupDriftStates;
        private ref NativeArray<DodReplayEntityGhostRecord> _ghostRecords => ref _nativeBuffers.GhostRecords;
        private ref NativeArray<DodReplayLogisticFlowRecord> _logisticFlowRecords => ref _nativeBuffers.LogisticFlowRecords;
        private ref NativeArray<DodReplayAtmosphereCellRecord> _atmosphereRecords => ref _nativeBuffers.AtmosphereRecords;
        private ref NativeArray<DodReplayVramAllocationRecord> _vramRecords => ref _nativeBuffers.VramRecords;
        private ref NativeArray<DodReplayPhysicsSmokeRecord> _physicsSmokeRecords => ref _nativeBuffers.PhysicsSmokeRecords;
        private FileStream _replayStream;
        // COLD ALLOC: byte[65536] - portable replay file write staging - owner: DodReplayRecorder
        private readonly byte[] _replayFileWriteScratch = new byte[ReplayFileWriteScratchBytes];
        private Thread _writerThread;
        private AutoResetEvent _writerSignal;
        private string _replayPath;
        private long _writeOffset;
        private uint _snapshotSequence;
        private uint _pendingSubjectHash;
        private uint _pendingErrorCode;
        private int _sourceHashCount;
        private int _registeredLateFrame;
        private int _registeredHotSwap;
        private int _initialized;
        private int _writerShouldStop;
        private int _writeInProgress;
        private int _pendingWriteBytes;
        private int _forceDump;
        private int _inputSequence;
        private int _jobProfileSequence;
        private int _panicSequence;
        private int _aupDriftSequence;
        private int _ghostSequence;
        private int _logisticSequence;
        private int _atmosphereSequence;
        private int _vramSequence;
        private int _physicsSmokeSequence;
        private int _inputJournalDirty;
        private int _jobProfileDirty;
        private int _panicDirty;
        private int _aupDriftDirty;
        private int _ghostDirty;
        private int _logisticDirty;
        private int _atmosphereDirty;
        private int _vramDirty;
        private int _physicsSmokeDirty;
        private int _inputHooked;

        /// <summary>
        /// Ensures a scene-local recorder exists for the current development session.
        /// </summary>
        public static DodReplayRecorder EnsureRuntimeInstance()
        {
            if (_activeRecorder != null)
                return _activeRecorder;

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // DOD replay owns burst panic / full-state dumps; without create, debug
            // capture paths no-op when bootstrap or panic sites skip explicit wiring.            GameObject recorderObject = new GameObject("DOD Replay Recorder"); // COLD ALLOC: GameObject[1] - debug replay owner - owner: DodReplayRecorder
            return recorderObject.AddComponent<DodReplayRecorder>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRecorder = null;
        }

        /// <summary>
        /// Requests an immediate full-state dump for a numeric fault.
        /// </summary>
        public static void RequestFullStateDump(uint subjectHash, uint errorCode)
        {
            DodReplayRecorder recorder = _activeRecorder ?? EnsureRuntimeInstance();
            if (recorder == null)
                return;

            recorder.QueueFullStateDump(subjectHash, errorCode, true);
        }

        /// <summary>
        /// Console/remote command entry point for architect-triggered snapshots.
        /// </summary>
        public static void TriggerRemoteSnapshot(uint subjectHash)
        {
            RequestFullStateDump(subjectHash, ErrorCodeRemoteSnapshot);
        }

        /// <summary>
        /// Records one hardware input event for deterministic replay.
        /// </summary>
        public static void RecordInputEvent(
            uint deviceHash,
            uint controlHash,
            uint phaseHash,
            float value0,
            float value1,
            float value2,
            double precisionTimestamp)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordInputEventInternal(
                deviceHash,
                controlHash,
                phaseHash,
                value0,
                value1,
                value2,
                precisionTimestamp);
        }

        /// <summary>
        /// Records a job completion sample into the replay sidecar.
        /// </summary>
        public static void RecordJobCompletion(uint subjectHash, uint completionMicroseconds, ushort workerIndex, uint errorCode)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordJobCompletionInternal(subjectHash, completionMicroseconds, workerIndex, errorCode);
        }

        /// <summary>
        /// Captures a Burst panic and copies the caller-owned job data into the replay panic payload ring.
        /// </summary>
        public static void CaptureBurstPanic<T>(uint subjectHash, uint errorCode, ref T jobData)
            where T : unmanaged
        {
            DodReplayRecorder recorder = _activeRecorder ?? EnsureRuntimeInstance();
            if (recorder == null)
                return;

            recorder.CaptureBurstPanicInternal(subjectHash, errorCode, ref jobData);
            recorder.QueueFullStateDump(subjectHash, errorCode != 0u ? errorCode : ErrorCodeBurstPanic, true);
        }

        /// <summary>
        /// Records one AUP sample and emits a drift record when the tracked 1000-frame window closes.
        /// </summary>
        public static void RecordAupSample(
            uint subjectHash,
            long gridX,
            long gridY,
            long gridZ,
            float localX,
            float localY,
            float localZ)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordAupSampleInternal(subjectHash, gridX, gridY, gridZ, localX, localY, localZ);
        }

        /// <summary>
        /// Records an entity breadcrumb for the last-100-frame ghost overlay.
        /// </summary>
        public static void RecordEntityGhost(uint entityHash, float3 position, uint flags)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordEntityGhostInternal(entityHash, position, flags);
        }

        /// <summary>
        /// Records one logistics flow vector for editor visualization.
        /// </summary>
        public static void RecordLogisticFlow(uint edgeHash, float3 from, float3 to, float potential, uint flags)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordLogisticFlowInternal(edgeHash, from, to, potential, flags);
        }

        /// <summary>
        /// Records one atmosphere pressure-map cell for editor visualization.
        /// </summary>
        public static void RecordAtmosphereCell(
            uint cellHash,
            int x,
            int y,
            float oxygen01,
            float carbonDioxide01,
            float pressureKpa,
            uint flags)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordAtmosphereCellInternal(cellHash, x, y, oxygen01, carbonDioxide01, pressureKpa, flags);
        }

        /// <summary>
        /// Records a graphics buffer allocation sample for fault-time VRAM accounting.
        /// </summary>
        public static void RecordGraphicsBufferAllocation(uint ownerHash, uint labelHash, long bytes, uint stride, uint flags)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordGraphicsBufferAllocationInternal(ownerHash, labelHash, bytes, stride, flags);
        }

        /// <summary>
        /// Records a deterministic physics replay smoke-test result.
        /// </summary>
        public static void RecordPhysicsSmokeResult(
            uint testHash,
            ulong runAHash,
            ulong runBHash,
            long gridDeltaX,
            long gridDeltaY,
            float maxLocalDrift)
        {
            RecordPhysicsSmokeResult(testHash, runAHash, runBHash, gridDeltaX, gridDeltaY, 0L, maxLocalDrift);
        }

        /// <summary>
        /// Records a deterministic physics replay smoke-test result with full AUP grid deltas.
        /// </summary>
        public static void RecordPhysicsSmokeResult(
            uint testHash,
            ulong runAHash,
            ulong runBHash,
            long gridDeltaX,
            long gridDeltaY,
            long gridDeltaZ,
            float maxLocalDrift)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null)
                return;

            recorder.RecordPhysicsSmokeResultInternal(testHash, runAHash, runBHash, gridDeltaX, gridDeltaY, gridDeltaZ, maxLocalDrift);
        }

        /// <summary>
        /// Compares two final AUP states bit-for-bit and records the smoke-test result.
        /// </summary>
        public static bool RecordPhysicsSmokeAupResult(
            uint testHash,
            long runAGridX,
            long runAGridY,
            long runAGridZ,
            float3 runALocal,
            long runBGridX,
            long runBGridY,
            long runBGridZ,
            float3 runBLocal)
        {
            ulong runAHash = HashPhysicsAup(runAGridX, runAGridY, runAGridZ, runALocal);
            ulong runBHash = HashPhysicsAup(runBGridX, runBGridY, runBGridZ, runBLocal);
            float maxLocalDrift = math.max(
                math.max(math.abs(runALocal.x - runBLocal.x), math.abs(runALocal.y - runBLocal.y)),
                math.abs(runALocal.z - runBLocal.z));
            long gridDeltaX = runBGridX - runAGridX;
            long gridDeltaY = runBGridY - runAGridY;
            long gridDeltaZ = runBGridZ - runAGridZ;
            RecordPhysicsSmokeResult(testHash, runAHash, runBHash, gridDeltaX, gridDeltaY, gridDeltaZ, maxLocalDrift);
            return runAHash == runBHash && gridDeltaX == 0L && gridDeltaY == 0L && gridDeltaZ == 0L;
        }

        /// <summary>
        /// Attempts to copy the newest ghost path records into a caller-owned buffer for wireframe replay overlays.
        /// </summary>
        public static int CopyGhostPath(NativeArray<DodReplayEntityGhostRecord> destination)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null || !destination.IsCreated || !recorder._ghostRecords.IsCreated)
                return 0;

            int count = math.min(destination.Length, math.min(GhostCapacity, Volatile.Read(ref recorder._ghostSequence)));
            int start = Volatile.Read(ref recorder._ghostSequence) - count;
            for (int i = 0; i < count; i++)
                destination[i] = recorder._ghostRecords[(start + i) & (GhostCapacity - 1)];

            return count;
        }

        /// <summary>
        /// Copies latest logistics flow vectors into a caller-owned buffer for editor overlays.
        /// </summary>
        public static int CopyLogisticFlows(NativeArray<DodReplayLogisticFlowRecord> destination)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null || !destination.IsCreated || !recorder._logisticFlowRecords.IsCreated)
                return 0;

            int count = math.min(destination.Length, math.min(SidecarCapacity, Volatile.Read(ref recorder._logisticSequence)));
            int start = Volatile.Read(ref recorder._logisticSequence) - count;
            for (int i = 0; i < count; i++)
                destination[i] = recorder._logisticFlowRecords[(start + i) & (SidecarCapacity - 1)];

            return count;
        }

        /// <summary>
        /// Copies latest atmosphere grid samples into a caller-owned buffer for the pressure-map editor.
        /// </summary>
        public static int CopyAtmosphereCells(NativeArray<DodReplayAtmosphereCellRecord> destination)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null || !destination.IsCreated || !recorder._atmosphereRecords.IsCreated)
                return 0;

            int count = math.min(destination.Length, math.min(SidecarCapacity, Volatile.Read(ref recorder._atmosphereSequence)));
            int start = Volatile.Read(ref recorder._atmosphereSequence) - count;
            for (int i = 0; i < count; i++)
                destination[i] = recorder._atmosphereRecords[(start + i) & (SidecarCapacity - 1)];

            return count;
        }

        /// <summary>
        /// Copies latest graphics-buffer allocation samples into a caller-owned buffer.
        /// </summary>
        public static int CopyGraphicsBufferAllocations(NativeArray<DodReplayVramAllocationRecord> destination)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null || !destination.IsCreated || !recorder._vramRecords.IsCreated)
                return 0;

            int count = math.min(destination.Length, math.min(SidecarCapacity, Volatile.Read(ref recorder._vramSequence)));
            int start = Volatile.Read(ref recorder._vramSequence) - count;
            for (int i = 0; i < count; i++)
                destination[i] = recorder._vramRecords[(start + i) & (SidecarCapacity - 1)];

            return count;
        }

        /// <summary>
        /// Copies latest job-completion profiler samples into a caller-owned buffer.
        /// </summary>
        public static int CopyJobProfiles(NativeArray<DodReplayJobProfileRecord> destination)
        {
            DodReplayRecorder recorder = _activeRecorder;
            if (recorder == null || !destination.IsCreated || !recorder._jobProfiles.IsCreated)
                return 0;

            int count = math.min(destination.Length, math.min(SidecarCapacity, Volatile.Read(ref recorder._jobProfileSequence)));
            int start = Volatile.Read(ref recorder._jobProfileSequence) - count;
            for (int i = 0; i < count; i++)
                destination[i] = recorder._jobProfiles[(start + i) & (SidecarCapacity - 1)];

            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStartDevelopmentRecorder()
        {
            if (Application.isPlaying)
                EnsureRuntimeInstance();
        }

        private static void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.valid)
                return;

            uint deviceHash = unchecked((uint)(device != null ? device.deviceId : eventPtr.deviceId));
            uint controlHash = unchecked((uint)(int)eventPtr.type);
            uint phaseHash = unchecked((uint)eventPtr.id);
            RecordInputEvent(
                deviceHash,
                controlHash,
                phaseHash,
                eventPtr.deviceId,
                eventPtr.sizeInBytes,
                eventPtr.handled ? 1f : 0f,
                PlatformPrecisionClock.NowSeconds);
        }

        private void OnEnable()
        {
            if (!EnsureSingletonOwnership())
                return;

            Initialize();
            if (_initialized == 0)
                return;

            RegisterInputHook();
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTickable();
        }

        private void Start()
        {
            if (_initialized == 0)
                return;

            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            ShutdownForLifecycle();
        }

        private void OnDestroy()
        {
            ShutdownForLifecycle();
        }

        private bool EnsureSingletonOwnership()
        {
            DodReplayRecorder activeRecorder = _activeRecorder;
            if (activeRecorder != null && !ReferenceEquals(activeRecorder, this))
            {
                Destroy(gameObject);
                return false;
            }

            _activeRecorder = this;
            return true;
        }

        private void ShutdownForLifecycle()
        {
            UnregisterInputHook();
            TryUnregisterHotSwapListener();
            if (_registeredLateFrame != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = 0;
            }

            if (!StopWriterThread())
            {
                if (ReferenceEquals(_activeRecorder, this))
                    _activeRecorder = null;
                _initialized = 0;
                return;
            }

            DisposeReplayFile();
            DisposeNativeBuffers();

            if (ReferenceEquals(_activeRecorder, this))
                _activeRecorder = null;

            _initialized = 0;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_initialized == 0)
                return;

            int frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            bool forced = Volatile.Read(ref _forceDump) != 0;
            if (!forced && frameIndex % SnapshotIntervalFrames != 0)
                return;

            if (CaptureSnapshot(frameIndex, forced) && forced)
                Volatile.Write(ref _forceDump, 0);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawWireReplayOverlay)
                return;

            Color previousColor = Gizmos.color;
            DrawGhostWireOverlay();
            DrawLogisticFlowOverlay();
            Gizmos.color = previousColor;
        }

        private void DrawGhostWireOverlay()
        {
            if (!_ghostRecords.IsCreated)
                return;

            int sequence = Volatile.Read(ref _ghostSequence);
            int count = math.min(math.min(_wireReplayFrameCount, GhostCapacity), sequence);
            int start = sequence - count;
            Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.85f);
            float cubeSize = math.max(0.01f, _wireReplayCubeSize);
            Vector3 cube = new Vector3(cubeSize, cubeSize, cubeSize);
            Vector3 previousPosition = default;
            uint previousEntityHash = 0u;

            for (int i = 0; i < count; i++)
            {
                DodReplayEntityGhostRecord record = _ghostRecords[(start + i) & (GhostCapacity - 1)];
                if (record.EntityHash == 0u ||
                    (_wireReplayEntityFilter != 0u && record.EntityHash != _wireReplayEntityFilter))
                {
                    continue;
                }

                Vector3 position = new Vector3(record.Position.x, record.Position.y, record.Position.z);
                Gizmos.DrawWireCube(position, cube);
                if (previousEntityHash == record.EntityHash)
                    Gizmos.DrawLine(previousPosition, position);

                previousPosition = position;
                previousEntityHash = record.EntityHash;
            }
        }

        private void DrawLogisticFlowOverlay()
        {
            if (!_logisticFlowRecords.IsCreated)
                return;

            int sequence = Volatile.Read(ref _logisticSequence);
            int count = math.min(SidecarCapacity, sequence);
            int start = sequence - count;
            for (int i = 0; i < count; i++)
            {
                DodReplayLogisticFlowRecord record = _logisticFlowRecords[(start + i) & (SidecarCapacity - 1)];
                if (record.EdgeHash == 0u ||
                    !math.all(math.isfinite(record.From)) ||
                    !math.all(math.isfinite(record.To)))
                {
                    continue;
                }

                float pressure = math.saturate(math.abs(record.Potential));
                Gizmos.color = Color.Lerp(new Color(0.15f, 0.4f, 1f, 0.65f), new Color(1f, 0.35f, 0.1f, 0.9f), pressure);
                Vector3 from = new Vector3(record.From.x, record.From.y, record.From.z);
                Vector3 to = new Vector3(record.To.x, record.To.y, record.To.z);
                Gizmos.DrawLine(from, to);
                DrawArrowHead(from, to);
            }
        }

        private static void DrawArrowHead(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.0001f)
                return;

            Vector3 direction = delta / length;
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude <= 0.0001f)
                side = Vector3.Cross(direction, Vector3.right);

            side.Normalize();
            float headLength = math.min(0.35f, length * 0.25f);
            float headWidth = headLength * 0.5f;
            Vector3 basePoint = to - direction * headLength;
            Gizmos.DrawLine(to, basePoint + side * headWidth);
            Gizmos.DrawLine(to, basePoint - side * headWidth);
        }
#endif

        private void Initialize()
        {
            if (_initialized != 0)
                return;

            int snapshotHeaderSize = UnsafeUtility.SizeOf<DodReplaySnapshotHeader>();
            int segmentHeaderSize = UnsafeUtility.SizeOf<DodReplaySegmentHeader>();
            if (snapshotHeaderSize != HeaderSizeBytes || segmentHeaderSize != SegmentHeaderSizeBytes)
            {
                enabled = false;
                return;
            }

            try
            {
                _nativeBuffers.Sources = AllocateNativeArray<NativeAllocationSnapshotSource>(MaxSnapshotSources, nameof(_nativeBuffers.Sources), NativeArrayOptions.UninitializedMemory);
                _nativeBuffers.SnapshotScratch = AllocateNativeArray<byte>(SnapshotScratchBytes, nameof(_nativeBuffers.SnapshotScratch), NativeArrayOptions.UninitializedMemory);
                _nativeBuffers.SourceHashes = AllocateNativeArray<ReplaySourceHash>(MaxSnapshotSources, nameof(_nativeBuffers.SourceHashes), NativeArrayOptions.ClearMemory);
                _nativeBuffers.InputJournal = AllocateNativeArray<DodReplayInputEvent>(InputJournalCapacity, nameof(_nativeBuffers.InputJournal), NativeArrayOptions.ClearMemory);
                _nativeBuffers.JobProfiles = AllocateNativeArray<DodReplayJobProfileRecord>(SidecarCapacity, nameof(_nativeBuffers.JobProfiles), NativeArrayOptions.ClearMemory);
                _nativeBuffers.PanicRecords = AllocateNativeArray<DodReplayBurstPanicRecord>(SidecarCapacity, nameof(_nativeBuffers.PanicRecords), NativeArrayOptions.ClearMemory);
                _nativeBuffers.PanicPayloadBytes = AllocateNativeArray<byte>(PanicPayloadCapacity * PanicPayloadStrideBytes, nameof(_nativeBuffers.PanicPayloadBytes), NativeArrayOptions.ClearMemory);
                _nativeBuffers.AupDriftRecords = AllocateNativeArray<DodReplayAupDriftRecord>(SidecarCapacity, nameof(_nativeBuffers.AupDriftRecords), NativeArrayOptions.ClearMemory);
                _nativeBuffers.AupDriftStates = AllocateNativeArray<AupDriftState>(AupTrackedSubjectCapacity, nameof(_nativeBuffers.AupDriftStates), NativeArrayOptions.ClearMemory);
                _nativeBuffers.GhostRecords = AllocateNativeArray<DodReplayEntityGhostRecord>(GhostCapacity, nameof(_nativeBuffers.GhostRecords), NativeArrayOptions.ClearMemory);
                _nativeBuffers.LogisticFlowRecords = AllocateNativeArray<DodReplayLogisticFlowRecord>(SidecarCapacity, nameof(_nativeBuffers.LogisticFlowRecords), NativeArrayOptions.ClearMemory);
                _nativeBuffers.AtmosphereRecords = AllocateNativeArray<DodReplayAtmosphereCellRecord>(SidecarCapacity, nameof(_nativeBuffers.AtmosphereRecords), NativeArrayOptions.ClearMemory);
                _nativeBuffers.VramRecords = AllocateNativeArray<DodReplayVramAllocationRecord>(SidecarCapacity, nameof(_nativeBuffers.VramRecords), NativeArrayOptions.ClearMemory);
                _nativeBuffers.PhysicsSmokeRecords = AllocateNativeArray<DodReplayPhysicsSmokeRecord>(SidecarCapacity, nameof(_nativeBuffers.PhysicsSmokeRecords), NativeArrayOptions.ClearMemory);

                _writerSignal = new AutoResetEvent(false); // COLD ALLOC: AutoResetEvent[1] - SPSC writer wake signal - owner: DodReplayRecorder
                InitializeReplayFile();
                if (!StartWriterThread())
                {
                    ShutdownAllocatedReplayStateAfterInitializeFailure();
                    enabled = false;
                    return;
                }

                _initialized = 1;
            }
            catch (Exception)
            {
                ShutdownAllocatedReplayStateAfterInitializeFailure();
                enabled = false;
            }
        }

        private static NativeArray<T> AllocateNativeArray<T>(int length, string label, NativeArrayOptions options)
            where T : struct
        {
            NativeArray<T> array = H8Memory.Allocate<T>(length, NativeMemoryOwner, Allocator.Persistent, options);
            if (!array.IsCreated)
                throw new InvalidOperationException($"H8Memory rejected DodReplayRecorder allocation for {label}.");

            return array;
        }

        private void RegisterInputHook()
        {
            if (_inputHooked != 0)
                return;

            InputSystem.onEvent += _inputEventDelegate;
            _inputHooked = 1;
        }

        private void UnregisterInputHook()
        {
            if (_inputHooked == 0)
                return;

            InputSystem.onEvent -= _inputEventDelegate;
            _inputHooked = 0;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            if (_registeredLateFrame != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = 0;
            }

            if (currentService == null || !isActiveAndEnabled || _initialized == 0)
                return;

            TryRegisterLateFrameTickable();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap != 0 || !Application.isPlaying || _initialized == 0)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this) ? 1 : 0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwap == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = 0;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame != 0)
                return;

            if (_initialized == 0)
                return;

            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core) ? 1 : 0;
        }

        private void QueueFullStateDump(uint subjectHash, uint errorCode, bool attemptImmediate)
        {
            _pendingSubjectHash = subjectHash;
            _pendingErrorCode = errorCode;
            Volatile.Write(ref _forceDump, 1);

            if (attemptImmediate && _initialized != 0)
            {
                if (CaptureSnapshot(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, true))
                    Volatile.Write(ref _forceDump, 0);
            }
        }

        private void RecordInputEventInternal(
            uint deviceHash,
            uint controlHash,
            uint phaseHash,
            float value0,
            float value1,
            float value2,
            double precisionTimestamp)
        {
            if (!_inputJournal.IsCreated)
                return;

            int sequence = Interlocked.Increment(ref _inputSequence) - 1;
            int index = sequence & (InputJournalCapacity - 1);
            _inputJournal[index] = new DodReplayInputEvent
            {
                PrecisionTimestamp = precisionTimestamp,
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = unchecked((uint)sequence),
                DeviceHash = deviceHash,
                ControlHash = controlHash,
                PhaseHash = phaseHash,
                Value0 = value0,
                Value1 = value1,
                Value2 = value2
            };
            Volatile.Write(ref _inputJournalDirty, 1);
        }

        private void RecordJobCompletionInternal(uint subjectHash, uint completionMicroseconds, ushort workerIndex, uint errorCode)
        {
            if (!_jobProfiles.IsCreated)
                return;

            int sequence = Interlocked.Increment(ref _jobProfileSequence) - 1;
            _jobProfiles[sequence & (SidecarCapacity - 1)] = new DodReplayJobProfileRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SubjectHash = subjectHash,
                CompletionMicroseconds = completionMicroseconds,
                WorkerIndex = workerIndex,
                Flags = errorCode != 0u ? (ushort)1 : (ushort)0,
                ErrorCode = errorCode
            };
            Volatile.Write(ref _jobProfileDirty, 1);
        }

        private void CaptureBurstPanicInternal<T>(uint subjectHash, uint errorCode, ref T jobData)
            where T : unmanaged
        {
            if (!_panicRecords.IsCreated || !_panicPayloadBytes.IsCreated)
                return;

            int sequence = Interlocked.Increment(ref _panicSequence) - 1;
            int payloadSlot = sequence & (PanicPayloadCapacity - 1);
            int payloadOffset = payloadSlot * PanicPayloadStrideBytes;
            int sourceBytes = UnsafeUtility.SizeOf<T>();
            int payloadBytes = math.min(sourceBytes, PanicPayloadStrideBytes);
            void* sourcePtr = UnsafeUtility.AddressOf(ref jobData);
            void* destinationPtr = (byte*)_panicPayloadBytes.GetUnsafePtr() + payloadOffset;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, PanicPayloadStrideBytes, sourcePtr, payloadBytes))
            {
                UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(DodReplayRecorder));
                return;
            }

            _panicRecords[sequence & (SidecarCapacity - 1)] = new DodReplayBurstPanicRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SubjectHash = subjectHash,
                ErrorCode = errorCode != 0u ? errorCode : ErrorCodeBurstPanic,
                PayloadOffsetBytes = unchecked((uint)payloadOffset),
                PayloadBytes = unchecked((ushort)payloadBytes),
                SourceBytes = unchecked((ushort)math.min(sourceBytes, ushort.MaxValue)),
                JobDataHash = ComputeFnv64((byte*)sourcePtr, sourceBytes),
                Flags = payloadBytes < sourceBytes ? PanicPayloadTruncatedFlag : 0u
            };
            Volatile.Write(ref _panicDirty, 1);
        }

        private void RecordAupSampleInternal(
            uint subjectHash,
            long gridX,
            long gridY,
            long gridZ,
            float localX,
            float localY,
            float localZ)
        {
            if (subjectHash == 0u || !_aupDriftStates.IsCreated || !_aupDriftRecords.IsCreated)
                return;

            if (!math.isfinite(localX) || !math.isfinite(localY) || !math.isfinite(localZ))
            {
                QueueFullStateDump(subjectHash, ErrorCodeAupNonFinite, true);
                return;
            }

            int frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            int slot = FindOrCreateAupStateSlot(subjectHash);
            if (slot < 0)
                return;

            AupDriftState state = _aupDriftStates[slot];
            if (state.Valid == 0u)
            {
                _aupDriftStates[slot] = BuildAupDriftState(subjectHash, frameIndex, gridX, gridY, gridZ, localX, localY, localZ);
                return;
            }

            int frameSpan = frameIndex - state.StartFrame;
            if (frameSpan < AupDriftWindowFrames)
                return;

            float maxLocalDrift = math.max(
                math.max(math.abs(localX - state.LocalX), math.abs(localY - state.LocalY)),
                math.abs(localZ - state.LocalZ));
            bool drifted = maxLocalDrift > AupDriftThreshold ||
                           gridX != state.GridX ||
                           gridY != state.GridY ||
                           gridZ != state.GridZ;

            int sequence = Interlocked.Increment(ref _aupDriftSequence) - 1;
            _aupDriftRecords[sequence & (SidecarCapacity - 1)] = new DodReplayAupDriftRecord
            {
                FrameIndex = unchecked((uint)frameIndex),
                SubjectHash = subjectHash,
                FrameSpan = unchecked((uint)frameSpan),
                Flags = drifted ? AupDriftExceededFlag : 0u,
                GridDeltaX = gridX - state.GridX,
                GridDeltaY = gridY - state.GridY,
                GridDeltaZ = gridZ - state.GridZ,
                MaxLocalDrift = maxLocalDrift
            };
            _aupDriftStates[slot] = BuildAupDriftState(subjectHash, frameIndex, gridX, gridY, gridZ, localX, localY, localZ);
            Volatile.Write(ref _aupDriftDirty, 1);

            if (drifted)
                QueueFullStateDump(subjectHash, ErrorCodeAupDrift, true);
        }

        private int FindOrCreateAupStateSlot(uint subjectHash)
        {
            int emptySlot = -1;
            for (int i = 0; i < _aupDriftStates.Length; i++)
            {
                AupDriftState state = _aupDriftStates[i];
                if (state.Valid == 0u)
                {
                    if (emptySlot < 0)
                        emptySlot = i;
                    continue;
                }

                if (state.SubjectHash == subjectHash)
                    return i;
            }

            return emptySlot;
        }

        private static AupDriftState BuildAupDriftState(
            uint subjectHash,
            int frameIndex,
            long gridX,
            long gridY,
            long gridZ,
            float localX,
            float localY,
            float localZ)
        {
            return new AupDriftState
            {
                SubjectHash = subjectHash,
                StartFrame = frameIndex,
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = localX,
                LocalY = localY,
                LocalZ = localZ,
                Valid = 1u
            };
        }

        private void RecordEntityGhostInternal(uint entityHash, float3 position, uint flags)
        {
            if (!_ghostRecords.IsCreated || entityHash == 0u || !math.all(math.isfinite(position)))
                return;

            int sequence = Interlocked.Increment(ref _ghostSequence) - 1;
            _ghostRecords[sequence & (GhostCapacity - 1)] = new DodReplayEntityGhostRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                EntityHash = entityHash,
                Position = position,
                Flags = flags,
                Sequence = unchecked((uint)sequence)
            };
            Volatile.Write(ref _ghostDirty, 1);
        }

        private void RecordLogisticFlowInternal(uint edgeHash, float3 from, float3 to, float potential, uint flags)
        {
            if (!_logisticFlowRecords.IsCreated ||
                edgeHash == 0u ||
                !math.all(math.isfinite(from)) ||
                !math.all(math.isfinite(to)) ||
                !math.isfinite(potential))
            {
                return;
            }

            int sequence = Interlocked.Increment(ref _logisticSequence) - 1;
            _logisticFlowRecords[sequence & (SidecarCapacity - 1)] = new DodReplayLogisticFlowRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                EdgeHash = edgeHash,
                From = from,
                To = to,
                Potential = potential,
                Flags = flags
            };
            Volatile.Write(ref _logisticDirty, 1);
        }

        private void RecordAtmosphereCellInternal(
            uint cellHash,
            int x,
            int y,
            float oxygen01,
            float carbonDioxide01,
            float pressureKpa,
            uint flags)
        {
            if (!_atmosphereRecords.IsCreated ||
                cellHash == 0u ||
                !math.isfinite(oxygen01) ||
                !math.isfinite(carbonDioxide01) ||
                !math.isfinite(pressureKpa))
            {
                return;
            }

            int sequence = Interlocked.Increment(ref _atmosphereSequence) - 1;
            _atmosphereRecords[sequence & (SidecarCapacity - 1)] = new DodReplayAtmosphereCellRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                CellHash = cellHash,
                X = x,
                Y = y,
                Oxygen01 = math.saturate(oxygen01),
                CarbonDioxide01 = math.saturate(carbonDioxide01),
                PressureKpa = math.max(0f, pressureKpa),
                Flags = flags
            };
            Volatile.Write(ref _atmosphereDirty, 1);
        }

        private void RecordGraphicsBufferAllocationInternal(uint ownerHash, uint labelHash, long bytes, uint stride, uint flags)
        {
            if (!_vramRecords.IsCreated || ownerHash == 0u || bytes <= 0L)
                return;

            int sequence = Interlocked.Increment(ref _vramSequence) - 1;
            _vramRecords[sequence & (SidecarCapacity - 1)] = new DodReplayVramAllocationRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                OwnerHash = ownerHash,
                LabelHash = labelHash,
                Bytes = bytes,
                Stride = stride,
                Flags = flags
            };
            Volatile.Write(ref _vramDirty, 1);
        }

        private void RecordPhysicsSmokeResultInternal(
            uint testHash,
            ulong runAHash,
            ulong runBHash,
            long gridDeltaX,
            long gridDeltaY,
            long gridDeltaZ,
            float maxLocalDrift)
        {
            if (!_physicsSmokeRecords.IsCreated || testHash == 0u || !math.isfinite(maxLocalDrift))
                return;

            int sequence = Interlocked.Increment(ref _physicsSmokeSequence) - 1;
            uint flags = runAHash != runBHash ||
                         gridDeltaX != 0L ||
                         gridDeltaY != 0L ||
                         gridDeltaZ != 0L ||
                         maxLocalDrift > 0f
                ? PhysicsSmokeMismatchFlag
                : 0u;
            _physicsSmokeRecords[sequence & (SidecarCapacity - 1)] = new DodReplayPhysicsSmokeRecord
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                TestHash = testHash,
                RunAHash = runAHash,
                RunBHash = runBHash,
                GridDeltaX = gridDeltaX,
                GridDeltaY = gridDeltaY,
                GridDeltaZ = gridDeltaZ,
                Flags = flags,
                MaxLocalDrift = math.max(0f, maxLocalDrift)
            };
            Volatile.Write(ref _physicsSmokeDirty, 1);

            if ((flags & PhysicsSmokeMismatchFlag) != 0u)
                QueueFullStateDump(testHash, ErrorCodePhysicsSmokeMismatch, true);
        }

        private bool CaptureSnapshot(int frameIndex, bool forced)
        {
            if (Volatile.Read(ref _writeInProgress) != 0 || _snapshotScratch.IsCreated == false)
                return false;

            if (Interlocked.CompareExchange(ref _writeInProgress, 1, 0) != 0)
                return false;

            int stagedBytes = 0;
            uint subjectHash = forced ? _pendingSubjectHash : 0u;
            uint errorCode = forced ? _pendingErrorCode : 0u;
            bool captured = false;
            try
            {
                byte* scratchBase = (byte*)_snapshotScratch.GetUnsafePtr();
                int cursor = HeaderSizeBytes;
                int segmentCount = 0;
                long totalSourceBytes = 0L;
                long droppedBytes = 0L;
                uint flags = forced ? SnapshotFlagForced : 0u;
                int sourceCount = NativeMemorySentinel.CopySnapshotSources(_sources, _recorderOwnerHash);
                sourceCount = H8Memory.CopySnapshotSources(_sources, sourceCount, _recorderOwnerHash);

                for (int i = 0; i < sourceCount; i++)
                {
                    NativeAllocationSnapshotSource source = _sources[i];
                    if (source.SourcePointerValue == 0ul ||
                        source.Bytes <= 0L ||
                        IsRecorderNativeBufferSource(source.SourcePointerValue))
                    {
                        continue;
                    }

                    totalSourceBytes += source.Bytes;
                    if (!TryAppendNativeSegment(
                            scratchBase,
                            ref cursor,
                            ref segmentCount,
                            source,
                            forced,
                            ref droppedBytes,
                            ref flags))
                    {
                        break;
                    }
                }

                AppendSidecarSegments(
                    scratchBase,
                    ref cursor,
                    ref segmentCount,
                    forced,
                    ref totalSourceBytes,
                    ref droppedBytes,
                    ref flags);

                uint replaySeed = DeterministicReplaySeed.ComposeSeed(
                    0u,
                    unchecked((uint)frameIndex),
                    subjectHash,
                    unchecked((uint)_snapshotSequence));

                DodReplaySnapshotHeader header = new DodReplaySnapshotHeader
                {
                    Magic = ReplayMagic,
                    Version = ReplayVersion,
                    HeaderSizeBytes = HeaderSizeBytes,
                    SegmentHeaderSizeBytes = SegmentHeaderSizeBytes,
                    FrameIndex = unchecked((uint)frameIndex),
                    SnapshotSequence = _snapshotSequence++,
                    SegmentCount = unchecked((uint)segmentCount),
                    Flags = flags,
                    PrecisionTimestamp = PlatformPrecisionClock.NowSeconds,
                    PayloadBytes = cursor - HeaderSizeBytes,
                    TotalSourceBytes = totalSourceBytes,
                    DroppedBytes = droppedBytes,
                    WriteOffset = Volatile.Read(ref _writeOffset),
                    SubjectHash = subjectHash,
                    ErrorCode = errorCode,
                    ReplaySeed = replaySeed,
                    SourceCount = unchecked((uint)sourceCount)
                };
                UnsafeUtility.CopyStructureToPtr(ref header, scratchBase);
                stagedBytes = cursor;
                captured = true;
            }
            finally
            {
                if (stagedBytes > 0)
                {
                    Volatile.Write(ref _pendingWriteBytes, stagedBytes);
                    if (!SignalWriterNoThrow())
                    {
                        Volatile.Write(ref _pendingWriteBytes, 0);
                        Volatile.Write(ref _writeInProgress, 0);
                        captured = false;
                    }
                }
                else
                {
                    Volatile.Write(ref _writeInProgress, 0);
                }
            }

            return captured;
        }

        private bool TryAppendNativeSegment(
            byte* scratchBase,
            ref int cursor,
            ref int segmentCount,
            NativeAllocationSnapshotSource source,
            bool forced,
            ref long droppedBytes,
            ref uint snapshotFlags)
        {
            byte* sourcePtr = (byte*)source.SourcePointerValue;
            ulong currentHash = ComputeFnv64(sourcePtr, source.Bytes);
            int hashIndex = FindOrCreateHashIndex(source.OwnerHash, source.LabelHash, source.Bytes);
            ulong previousHash = hashIndex >= 0 ? _sourceHashes[hashIndex].Hash : 0ul;
            bool changed = forced || currentHash != previousHash;
            uint changedBit = math.select(0u, 1u, changed);
            int sourceBytesAsInt = source.Bytes > int.MaxValue ? int.MaxValue : (int)source.Bytes;
            int payloadBytes = math.select(0, sourceBytesAsInt, changedBit != 0u);
            uint segmentFlags = math.select(SegmentFlagUnchanged | SegmentFlagDeltaSuppressed, SegmentFlagChanged, changedBit != 0u);

            if (!AppendRawSegment(
                    scratchBase,
                    ref cursor,
                    ref segmentCount,
                    source.OwnerHash,
                    source.LabelHash,
                    source.Bytes,
                    payloadBytes,
                    source.AllocationFrame,
                    previousHash,
                    currentHash,
                    segmentFlags,
                    sourcePtr,
                    ref droppedBytes,
                    ref snapshotFlags))
            {
                return false;
            }

            if (hashIndex >= 0)
            {
                ReplaySourceHash sourceHash = _sourceHashes[hashIndex];
                sourceHash.Hash = currentHash;
                _sourceHashes[hashIndex] = sourceHash;
            }

            return true;
        }

        private bool IsRecorderNativeBufferSource(ulong pointerValue)
        {
            return pointerValue == GetNativeArrayPointerValue(_sources) ||
                   pointerValue == GetNativeArrayPointerValue(_snapshotScratch) ||
                   pointerValue == GetNativeArrayPointerValue(_sourceHashes) ||
                   pointerValue == GetNativeArrayPointerValue(_inputJournal) ||
                   pointerValue == GetNativeArrayPointerValue(_jobProfiles) ||
                   pointerValue == GetNativeArrayPointerValue(_panicRecords) ||
                   pointerValue == GetNativeArrayPointerValue(_panicPayloadBytes) ||
                   pointerValue == GetNativeArrayPointerValue(_aupDriftRecords) ||
                   pointerValue == GetNativeArrayPointerValue(_aupDriftStates) ||
                   pointerValue == GetNativeArrayPointerValue(_ghostRecords) ||
                   pointerValue == GetNativeArrayPointerValue(_logisticFlowRecords) ||
                   pointerValue == GetNativeArrayPointerValue(_atmosphereRecords) ||
                   pointerValue == GetNativeArrayPointerValue(_vramRecords) ||
                   pointerValue == GetNativeArrayPointerValue(_physicsSmokeRecords);
        }

        private static ulong GetNativeArrayPointerValue<T>(NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return 0ul;

            return unchecked((ulong)((IntPtr)array.GetUnsafeReadOnlyPtr()).ToInt64());
        }

        private void AppendSidecarSegments(
            byte* scratchBase,
            ref int cursor,
            ref int segmentCount,
            bool forced,
            ref long totalSourceBytes,
            ref long droppedBytes,
            ref uint snapshotFlags)
        {
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _inputJournalOwnerHash, _inputJournalLabelHash, _inputJournal, forced, ref _inputJournalDirty, SegmentFlagInputJournal, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _jobProfileLabelHash, _jobProfiles, forced, ref _jobProfileDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _panicRecordLabelHash, _panicRecords, forced, ref _panicDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _panicPayloadLabelHash, _panicPayloadBytes, forced, ref _panicDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _aupDriftLabelHash, _aupDriftRecords, forced, ref _aupDriftDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _ghostLabelHash, _ghostRecords, forced, ref _ghostDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _logisticFlowLabelHash, _logisticFlowRecords, forced, ref _logisticDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _atmosphereLabelHash, _atmosphereRecords, forced, ref _atmosphereDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _vramLabelHash, _vramRecords, forced, ref _vramDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
            AppendNativeArraySegment(scratchBase, ref cursor, ref segmentCount, _recorderOwnerHash, _physicsSmokeLabelHash, _physicsSmokeRecords, forced, ref _physicsSmokeDirty, SegmentFlagReplaySidecar, ref totalSourceBytes, ref droppedBytes, ref snapshotFlags);
        }

        private void AppendNativeArraySegment<T>(
            byte* scratchBase,
            ref int cursor,
            ref int segmentCount,
            uint ownerHash,
            uint labelHash,
            NativeArray<T> source,
            bool forced,
            ref int dirtyFlag,
            uint extraFlags,
            ref long totalSourceBytes,
            ref long droppedBytes,
            ref uint snapshotFlags)
            where T : struct
        {
            if (!source.IsCreated)
                return;

            bool dirty = Volatile.Read(ref dirtyFlag) != 0;
            if (!forced && !dirty)
                return;

            long sourceBytes = (long)UnsafeUtility.SizeOf<T>() * source.Length;
            totalSourceBytes += sourceBytes;
            void* sourcePtr = source.GetUnsafeReadOnlyPtr();
            ulong currentHash = ComputeFnv64((byte*)sourcePtr, sourceBytes);
            int payloadBytes = sourceBytes > int.MaxValue ? int.MaxValue : (int)sourceBytes;
            if (AppendRawSegment(
                    scratchBase,
                    ref cursor,
                    ref segmentCount,
                    ownerHash,
                    labelHash,
                    sourceBytes,
                    payloadBytes,
                    0,
                    0ul,
                    currentHash,
                    SegmentFlagChanged | extraFlags,
                    sourcePtr,
                    ref droppedBytes,
                    ref snapshotFlags))
            {
                Volatile.Write(ref dirtyFlag, 0);
            }
        }

        private bool AppendRawSegment(
            byte* scratchBase,
            ref int cursor,
            ref int segmentCount,
            uint ownerHash,
            uint labelHash,
            long sourceBytes,
            int requestedPayloadBytes,
            int allocationFrame,
            ulong previousHash,
            ulong currentHash,
            uint segmentFlags,
            void* sourcePtr,
            ref long droppedBytes,
            ref uint snapshotFlags)
        {
            int remaining = _snapshotScratch.Length - cursor;
            if (remaining < SegmentHeaderSizeBytes)
            {
                droppedBytes += sourceBytes;
                snapshotFlags |= SnapshotFlagTruncated;
                return false;
            }

            int availablePayloadBytes = _snapshotScratch.Length - cursor - SegmentHeaderSizeBytes;
            int payloadBytes = requestedPayloadBytes;
            if (payloadBytes > availablePayloadBytes)
            {
                payloadBytes = math.max(0, availablePayloadBytes);
                segmentFlags |= SegmentFlagTruncated;
                snapshotFlags |= SnapshotFlagTruncated;
                droppedBytes += sourceBytes - payloadBytes;
            }

            DodReplaySegmentHeader segment = new DodReplaySegmentHeader
            {
                OwnerHash = ownerHash,
                LabelHash = labelHash,
                SourceBytes = sourceBytes,
                PayloadBytes = payloadBytes,
                AllocationFrame = allocationFrame,
                PreviousHash = previousHash,
                CurrentHash = currentHash,
                Flags = segmentFlags,
                SegmentIndex = unchecked((uint)segmentCount),
                PayloadOffset = cursor + SegmentHeaderSizeBytes - HeaderSizeBytes
            };

            UnsafeUtility.CopyStructureToPtr(ref segment, scratchBase + cursor);
            cursor += SegmentHeaderSizeBytes;

            if (payloadBytes > 0)
            {
                if (!UnsafeMemoryCopyGuard.TryMemCpy(
                        scratchBase + cursor,
                        _snapshotScratch.Length - cursor,
                        sourcePtr,
                        payloadBytes))
                {
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(DodReplayRecorder));
                    return false;
                }

                cursor += payloadBytes;
            }

            segmentCount++;
            return true;
        }

        private int FindOrCreateHashIndex(uint ownerHash, uint labelHash, long bytes)
        {
            int count = _sourceHashCount;
            for (int i = 0; i < count; i++)
            {
                ReplaySourceHash sourceHash = _sourceHashes[i];
                if (sourceHash.OwnerHash == ownerHash &&
                    sourceHash.LabelHash == labelHash &&
                    sourceHash.Bytes == bytes)
                {
                    return i;
                }
            }

            if (count >= _sourceHashes.Length)
                return -1;

            _sourceHashes[count] = new ReplaySourceHash
            {
                OwnerHash = ownerHash,
                LabelHash = labelHash,
                Bytes = bytes,
                Hash = 0ul
            };
            _sourceHashCount = count + 1;
            return count;
        }

        private static ulong ComputeFnv64(byte* data, long byteCount)
        {
            if (data == null || byteCount <= 0L)
                return 0ul;

            ulong hash = FnvOffset;
            for (long i = 0; i < byteCount; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        private static ulong HashPhysicsAup(long gridX, long gridY, long gridZ, float3 local)
        {
            ulong hash = FnvOffset;
            hash = HashUInt64(hash, unchecked((ulong)gridX));
            hash = HashUInt64(hash, unchecked((ulong)gridY));
            hash = HashUInt64(hash, unchecked((ulong)gridZ));
            uint3 localBits = math.asuint(local);
            hash = HashUInt32(hash, localBits.x);
            hash = HashUInt32(hash, localBits.y);
            hash = HashUInt32(hash, localBits.z);
            return hash;
        }

        private static ulong HashUInt32(ulong hash, uint value)
        {
            hash ^= value & 0xFFu;
            hash *= FnvPrime;
            hash ^= (value >> 8) & 0xFFu;
            hash *= FnvPrime;
            hash ^= (value >> 16) & 0xFFu;
            hash *= FnvPrime;
            hash ^= (value >> 24) & 0xFFu;
            hash *= FnvPrime;
            return hash;
        }

        private static ulong HashUInt64(ulong hash, ulong value)
        {
            hash = HashUInt32(hash, unchecked((uint)value));
            hash = HashUInt32(hash, unchecked((uint)(value >> 32)));
            return hash;
        }

        private void InitializeReplayFile()
        {
            _replayPath = HectonPersistentPathPolicy.CombineFile("replay.bin");
            HectonPersistentPathPolicy.EnsureParentDirectory(_replayPath);

            _replayStream = new FileStream(_replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough | FileOptions.RandomAccess);
            if (_replayStream.Length != ReplayFileCapacityBytes)
                _replayStream.SetLength(ReplayFileCapacityBytes);

            if (_replayStream.Length != ReplayFileCapacityBytes)
                throw new InvalidOperationException("Dod replay file capacity initialization failed.");

            _replayStream.Flush(true);
        }

        private bool StartWriterThread()
        {
            try
            {
                Volatile.Write(ref _writerShouldStop, 0);
                Thread writerThread = new Thread(WriterLoop)
                {
                    IsBackground = true,
                    Name = "H8.DODReplayWriter",
                    Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.BackgroundIo)
                };
                _writerThread = writerThread;
                writerThread.Start();
                return true;
            }
            catch (Exception)
            {
                Volatile.Write(ref _writerShouldStop, 1);
                Volatile.Write(ref _writeInProgress, 0);
                _writerThread = null;
                return false;
            }
        }

        private void WriterLoop()
        {
            try
            {
                while (Volatile.Read(ref _writerShouldStop) == 0)
                {
                    AutoResetEvent signal = _writerSignal;
                    if (signal == null)
                        return;

                    signal.WaitOne();
                    if (Volatile.Read(ref _writerShouldStop) != 0)
                        return;

                    int byteCount = Volatile.Read(ref _pendingWriteBytes);
                    if (byteCount <= 0)
                    {
                        Volatile.Write(ref _writeInProgress, 0);
                        continue;
                    }

                    WritePendingScratch(byteCount);
                }
            }
            catch (Exception)
            {
                Volatile.Write(ref _writerShouldStop, 1);
            }
            finally
            {
                Volatile.Write(ref _writeInProgress, 0);
            }
        }

        private void WritePendingScratch(int byteCount)
        {
            try
            {
                lock (_writerGate)
                {
                    if (_replayStream == null || !_snapshotScratch.IsCreated)
                        return;

                    long writeOffset = Volatile.Read(ref _writeOffset);
                    if (writeOffset + byteCount > ReplayFileCapacityBytes)
                        writeOffset = 0L;

                    void* sourcePtr = _snapshotScratch.GetUnsafeReadOnlyPtr();
                    _replayStream.Position = writeOffset;
                    if (!TryWriteReplayBytes(sourcePtr, byteCount))
                    {
                        UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(DodReplayRecorder));
                        return;
                    }

                    _replayStream.Flush(true);
                    Volatile.Write(ref _writeOffset, writeOffset + byteCount);
                    Volatile.Write(ref _pendingWriteBytes, 0);
                }
            }
            finally
            {
                Volatile.Write(ref _writeInProgress, 0);
            }
        }

        private bool StopWriterThread()
        {
            Volatile.Write(ref _writerShouldStop, 1);
            SignalWriterNoThrow();

            if (!TryJoinWriterNoThrow(_writerThread))
                return false;

            _writerThread = null;
            DisposeWriterSignalNoThrow();
            return true;
        }

        private bool SignalWriterNoThrow()
        {
            AutoResetEvent signal = _writerSignal;
            if (signal == null)
                return false;

            try
            {
                signal.Set();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryJoinWriterNoThrow(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
                return true;
            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(WriterJoinMilliseconds);
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void DisposeWriterSignalNoThrow()
        {
            if (_writerSignal == null)
                return;

            try
            {
                _writerSignal.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                _writerSignal = null;
            }
        }

        private bool TryWriteReplayBytes(void* sourcePtr, int byteCount)
        {
            if (_replayStream == null || sourcePtr == null || byteCount <= 0)
                return false;

            byte* source = (byte*)sourcePtr;
            int written = 0;
            while (written < byteCount)
            {
                int chunkBytes = byteCount - written;
                if (chunkBytes > _replayFileWriteScratch.Length)
                    chunkBytes = _replayFileWriteScratch.Length;

                Marshal.Copy((IntPtr)(source + written), _replayFileWriteScratch, 0, chunkBytes);
                _replayStream.Write(_replayFileWriteScratch, 0, chunkBytes);
                written += chunkBytes;
            }

            return true;
        }

        private void DisposeReplayFile()
        {
            try
            {
                _replayStream?.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                _replayStream = null;
            }
        }

        private void ShutdownAllocatedReplayStateAfterInitializeFailure()
        {
            if (!StopWriterThread())
                return;

            DisposeReplayFile();
            DisposeNativeBuffers();
            if (ReferenceEquals(_activeRecorder, this))
                _activeRecorder = null;
            Volatile.Write(ref _initialized, 0);
        }

        private void DisposeNativeBuffers()
        {
            DisposeNativeArray(ref _nativeBuffers.Sources);
            DisposeNativeArray(ref _nativeBuffers.SnapshotScratch);
            DisposeNativeArray(ref _nativeBuffers.SourceHashes);
            DisposeNativeArray(ref _nativeBuffers.InputJournal);
            DisposeNativeArray(ref _nativeBuffers.JobProfiles);
            DisposeNativeArray(ref _nativeBuffers.PanicRecords);
            DisposeNativeArray(ref _nativeBuffers.PanicPayloadBytes);
            DisposeNativeArray(ref _nativeBuffers.AupDriftRecords);
            DisposeNativeArray(ref _nativeBuffers.AupDriftStates);
            DisposeNativeArray(ref _nativeBuffers.GhostRecords);
            DisposeNativeArray(ref _nativeBuffers.LogisticFlowRecords);
            DisposeNativeArray(ref _nativeBuffers.AtmosphereRecords);
            DisposeNativeArray(ref _nativeBuffers.VramRecords);
            DisposeNativeArray(ref _nativeBuffers.PhysicsSmokeRecords);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            H8Memory.Release(ref array, NativeMemoryOwner);
        }

        private sealed class DodReplayNativeBufferSet
        {
            public NativeArray<NativeAllocationSnapshotSource> Sources;
            public NativeArray<byte> SnapshotScratch;
            public NativeArray<ReplaySourceHash> SourceHashes;
            public NativeArray<DodReplayInputEvent> InputJournal;
            public NativeArray<DodReplayJobProfileRecord> JobProfiles;
            public NativeArray<DodReplayBurstPanicRecord> PanicRecords;
            public NativeArray<byte> PanicPayloadBytes;
            public NativeArray<DodReplayAupDriftRecord> AupDriftRecords;
            public NativeArray<AupDriftState> AupDriftStates;
            public NativeArray<DodReplayEntityGhostRecord> GhostRecords;
            public NativeArray<DodReplayLogisticFlowRecord> LogisticFlowRecords;
            public NativeArray<DodReplayAtmosphereCellRecord> AtmosphereRecords;
            public NativeArray<DodReplayVramAllocationRecord> VramRecords;
            public NativeArray<DodReplayPhysicsSmokeRecord> PhysicsSmokeRecords;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct ReplaySourceHash
        {
            [FieldOffset(0)] public uint OwnerHash;
            [FieldOffset(4)] public uint LabelHash;
            [FieldOffset(8)] public long Bytes;
            [FieldOffset(16)] public ulong Hash;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct AupDriftState
        {
            [FieldOffset(0)] public uint SubjectHash;
            [FieldOffset(4)] public int StartFrame;
            [FieldOffset(8)] public long GridX;
            [FieldOffset(16)] public long GridY;
            [FieldOffset(24)] public long GridZ;
            [FieldOffset(32)] public float LocalX;
            [FieldOffset(36)] public float LocalY;
            [FieldOffset(40)] public float LocalZ;
            [FieldOffset(44)] public uint Valid;
        }
    }
}
#endif
