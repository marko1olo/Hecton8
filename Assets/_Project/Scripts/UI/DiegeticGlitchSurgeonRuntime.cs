using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.UI
{
    /// <summary>
    /// 16-byte global glitch state consumed by diegetic UI text, matrix, radar, and audio DTO mutators.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct GlitchStateDTO
    {
        [FieldOffset(0)] public float GlobalIntensity;
        [FieldOffset(4)] public float Seed;
        [FieldOffset(8)] public uint GlitchTableOffset;
        [FieldOffset(12)] public uint _pad0;

        /// <summary>Returns a mutable reference to a raw vault pointer without copying the DTO.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref GlitchStateDTO AsRef(void* ptr)
        {
            return ref UnsafeUtility.AsRef<GlitchStateDTO>(ptr);
        }
    }

    /// <summary>
    /// 4-byte ASCII substitution mapping entry for ARM64-friendly table validation.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct ScrambledCharacterDTO
    {
        [FieldOffset(0)] public byte OriginalChar;
        [FieldOffset(1)] public byte GlitchChar;
        [FieldOffset(2)] public ushort _pad0;
    }

    /// <summary>
    /// Pointer-backed test span used when Babel, Terminal OS, or anomaly owners are unavailable.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public unsafe struct MockTextSpan
    {
        [FieldOffset(0)] public ushort* Buffer;
        [FieldOffset(8)] public int Length;
        [FieldOffset(12)] public int ReadabilityPrefixChars;
        [FieldOffset(16)] public int ReadabilityDigitBudget;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _pad0;
    }

    /// <summary>
    /// Blind anomaly corruption signal mirror.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockCorruptionLevelSignal
    {
        [FieldOffset(0)] public float Corruption01;
        [FieldOffset(4)] public float SimulationSeconds;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>
    /// Blind depth signal mirror for deep-ocean interference.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockDepthSignal
    {
        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float BaselineIntensity;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>
    /// Blind module breach signal mirror for room-local terminal corruption.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockModuleBreachSignal
    {
        [FieldOffset(0)] public uint BreachedMask0;
        [FieldOffset(4)] public uint BreachedMask1;
        [FieldOffset(8)] public uint ActiveRoomIndex;
        [FieldOffset(12)] public uint Frame;
    }

    /// <summary>
    /// Human-tuned glitch controls stored in GlobalDataVault.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GlitchTuningDTO
    {
        [FieldOffset(0)] public float MasterIntensity;
        [FieldOffset(4)] public float TextScrambleRate;
        [FieldOffset(8)] public float MatrixShatterStrength;
        [FieldOffset(12)] public float GhostBlipCount;
        [FieldOffset(16)] public float DepthStartMeters;
        [FieldOffset(20)] public float DepthFullMeters;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public uint FrameSeed;
    }

    /// <summary>
    /// Bridge hologram quad DTO matching the 112-byte wrist HUD GPU payload shape without a sibling-domain dependency.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct GlitchQuadTransformDTO
    {
        [FieldOffset(0)] public float4x4 Matrix;
        [FieldOffset(64)] public float4 Color;
        [FieldOffset(80)] public float4 UVRect;
        [FieldOffset(96)] public uint CharacterCode;
        [FieldOffset(100)] public float GlitchIntensity;
        [FieldOffset(104)] public uint _pad0;
        [FieldOffset(108)] public uint _pad1;
    }

    /// <summary>
    /// Bridge radar blip DTO for unmanaged ghost injection without touching private renderer structs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RadarBlipDTO
    {
        [FieldOffset(0)] public float4 LocalPositionIntensity;
        [FieldOffset(16)] public float4 ColorSizeAgeFlags;
    }

    /// <summary>
    /// Local synth parameter mirror matching the 16-byte audio synthesis parameter ABI.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct GlitchSynthParametersDTO
    {
        [FieldOffset(0)] public float BaseFrequency;
        [FieldOffset(4)] public float ModulationIndex;
        [FieldOffset(8)] public float GrainSize;
        [FieldOffset(12)] public float PressureScalar;
    }

    /// <summary>
    /// 64-byte black-box record for the last 300 glitch frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DiegeticGlitchTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint ScrambledCharacters;
        [FieldOffset(16)] public float CurrentGlitchIntensity;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public float ComputeTimeMs;
        [FieldOffset(28)] public float DepthMeters;
        [FieldOffset(32)] public uint GhostBlipCount;
        [FieldOffset(36)] public uint TextSpanLength;
        [FieldOffset(40)] public uint TableHash;
        [FieldOffset(44)] public uint ModuleMask;
        [FieldOffset(48)] public float MasterIntensity;
        [FieldOffset(52)] public float MatrixStrength;
        [FieldOffset(56)] public float AudioPitchScalar;
        [FieldOffset(60)] public float Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct GlitchBlackBoxDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint EntryCount;
        [FieldOffset(12)] public uint Cursor;
        [FieldOffset(16)] public uint FaultFlags;
        [FieldOffset(20)] public uint TableHash;
        [FieldOffset(24)] public ulong TimestampTicks;
    }

    public struct ExternalAsciiScrambleLease
    {
        public JobHandle Handle;
        public byte IsCreated;
        internal DiegeticGlitchSurgeonRuntime Owner;
        internal IDataVault Vault;
    }

    /// <summary>
    /// Vault-backed diegetic glitch runtime. It mutates owned unmanaged buffers and exports bridge-ready DTOs.
    /// </summary>
    [DisallowMultipleComponent]
    public unsafe sealed class DiegeticGlitchSurgeonRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        internal const int GlitchTableCapacity = 64;
        internal const int GlitchTableBufferIdRaw = 70901;
        private const int MockTextCapacity = 128;
        private const int MockQuadCapacity = 128;
        private const int RadarBlipCapacity = 64;
        private const int SynthParameterCapacity = 8;
        private const int CsvScratchCapacity = 1024;
        private const int TelemetryFrameCount = 300;
        private const int CriticalReadabilityPrefixChars = 5; // "O2 98" stays readable until near-total corruption.
        private const int LockBitState = 0;
        private const int LockBitTable = 1;
        private const int LockBitOriginalText = 2;
        private const int LockBitWorkText = 3;
        private const int LockBitTextSpan = 4;
        private const int LockBitCorruption = 5;
        private const int LockBitDepth = 6;
        private const int LockBitBreach = 7;
        private const int LockBitTuning = 8;
        private const int LockBitQuads = 9;
        private const int LockBitRadar = 10;
        private const int LockBitSynth = 11;
        private const int LockBitTelemetry = 12;
        private const int LockBitCursor = 13;
        private const uint SpecialRadarBlipCode = 0xFFFFFF04u;
        private const uint DumpMagic = 0x48474944u; // DIGH
        private const uint DumpVersion = 1u;
        private const uint FaultNonFinite = 1u << 0;
        private const uint FaultOverBudget = 1u << 1;
        private const uint FaultVaultUnavailable = 1u << 2;
        private const uint FaultTableFallback = 1u << 3;
        private const uint FaultRngDeadlock = 1u << 4;
        private const string DefaultGlitchTableRelativePath = "Assets/_Project/Data/UI/GlitchTable.bytes";
        private const string DefaultCsvRelativePath = "glitch_profiles.csv";
        private const string DefaultDumpRelativePath = "Docs/AgentLogs/Dump_GLITCH_SURGEON.bin";

        private const BufferID StateBufferId = (BufferID)70900;
        private const BufferID GlitchTableBufferId = (BufferID)GlitchTableBufferIdRaw;
        private const BufferID OriginalTextBufferId = (BufferID)70902;
        private const BufferID WorkTextBufferId = (BufferID)70903;
        private const BufferID TextSpanBufferId = (BufferID)70904;
        private const BufferID CorruptionSignalBufferId = (BufferID)70905;
        private const BufferID DepthSignalBufferId = (BufferID)70906;
        private const BufferID BreachSignalBufferId = (BufferID)70907;
        private const BufferID TuningBufferId = (BufferID)70908;
        private const BufferID MockQuadBufferId = (BufferID)70909;
        private const BufferID RadarBlipBufferId = (BufferID)70910;
        private const BufferID SynthParameterBufferId = (BufferID)70911;
        private const BufferID TelemetryRingBufferId = (BufferID)70912;
        private const BufferID TelemetryCursorBufferId = (BufferID)70913;
        private const BufferID CsvScratchBufferId = (BufferID)70914;
        private const BufferID TerminalOsStateBridgeBufferId = (BufferID)70520;

        private static readonly int _terminalDamageGlitchId = Shader.PropertyToID("_TerminalDamageGlitch");
        private static readonly int _diegeticGlitchIntensityId = Shader.PropertyToID("_HectonDiegeticGlitchIntensity");
        private static readonly int _diegeticGlitchSeedId = Shader.PropertyToID("_HectonDiegeticGlitchSeed");
        private static readonly int _diegeticGlitchQualityWeightId = Shader.PropertyToID("_HectonDiegeticGlitchQualityWeight");
        private static GlitchStateDTO s_dummyState;
        private static GlitchTuningDTO s_dummyTuning;

        [Header("Glitch Sources")]
        [SerializeField, Tooltip("Project-relative binary substitution table path.")]
        private string glitchTableRelativePath = DefaultGlitchTableRelativePath;

        [SerializeField, Tooltip("Project-relative CSV override path for live glitch glyph authoring.")]
        private string csvRelativePath = DefaultCsvRelativePath;

        [SerializeField, Tooltip("Project-relative binary black-box dump path.")]
        private string dumpRelativePath = DefaultDumpRelativePath;

        [Header("Default Tuning")]
        [SerializeField, Range(0f, 1f), Tooltip("Designer-facing master corruption scalar written to the vault tuning DTO.")]
        private float masterIntensity = 0.55f;

        [SerializeField, Range(0f, 1f), Tooltip("Text substitution probability multiplier.")]
        private float textScrambleRate = 0.68f;

        [SerializeField, Range(0f, 1f), Tooltip("Matrix/UV shatter multiplier.")]
        private float matrixShatterStrength = 0.72f;

        [SerializeField, Range(0f, 32f), Tooltip("Maximum fake radar contacts exposed through the radar ghost DTO buffer.")]
        private float ghostBlipCount = 16f;

        [SerializeField, Range(0f, 4000f), Tooltip("Depth where passive interference begins.")]
        private float depthStartMeters = 1000f;

        [SerializeField, Range(1000f, 12000f), Tooltip("Depth range where passive interference reaches 1.0.")]
        private float depthFullMeters = 11000f;

        [SerializeField, Tooltip("Deterministic sector hash mixed with SimulationFrameCounter for rollback-safe glitch RNG.")]
        private uint deterministicSectorHash = 0x5348494Eu;

        private IDataVault _vault;
        private VaultBufferHandle<GlitchStateDTO> _stateHandle;
        private VaultBufferHandle<byte> _glitchTableHandle;
        private VaultBufferHandle<ushort> _originalTextHandle;
        private VaultBufferHandle<ushort> _workTextHandle;
        private VaultBufferHandle<MockTextSpan> _textSpanHandle;
        private VaultBufferHandle<MockCorruptionLevelSignal> _corruptionSignalHandle;
        private VaultBufferHandle<MockDepthSignal> _depthSignalHandle;
        private VaultBufferHandle<MockModuleBreachSignal> _breachSignalHandle;
        private VaultBufferHandle<GlitchTuningDTO> _tuningHandle;
        private VaultBufferHandle<GlitchQuadTransformDTO> _quadHandle;
        private VaultBufferHandle<RadarBlipDTO> _radarBlipHandle;
        private VaultBufferHandle<GlitchSynthParametersDTO> _synthHandle;
        private VaultBufferHandle<DiegeticGlitchTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<uint> _telemetryCursorHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<TerminalStateDTO> _terminalStateBridgeHandle;
        private JobHandle _activeHandle;
        private string _projectRoot;
        private string _glitchTableFullPath;
        private string _csvFullPath;
        private string _dumpFullPath;
        private DateTime _csvLastWriteUtc;
        private long _jobStartTimestamp;
        private float _lastComputeMs;
        private float _lastShaderIntensity = -1f;
        private float _lastShaderSeed01 = -1f;
        private float _lastShaderQualityWeight = -1f;
        private float _lastTerminalBridgeIntensity;
        private int _lockMask;
        private int _mockTextLength;
        private uint _frameIndex;
        private uint _lastFaultFlags;
        private uint _lastTableHash;
        private uint _lastSeedBits;
        private int _stalledSeedFrames;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _nativeReady;
        private bool _jobScheduled;
        private bool _tableFallbackGenerated;
        private bool _dumpWrittenForCurrentFault;
        private bool _pendingTuningWrite;
        private bool _pendingTableReload;
        private bool _pendingCsvReload;
        private bool _pendingExternalLeaseRelease;
        private bool _pendingDisableTeardown;
        private bool _pendingVaultSwap;
        private ExternalAsciiScrambleLease _pendingExternalLease;
        private IDataVault _pendingVaultAfterSwap;
        private float _pendingMasterIntensity;
        private float _pendingTextScrambleRate;
        private float _pendingMatrixShatterStrength;
        private float _pendingGhostBlipCount;
#if UNITY_EDITOR
        private double _nextEditorCsvPollTime;
#endif

        /// <summary>True when all vault handles are valid.</summary>
        public bool IsNativeReady => _nativeReady;

        /// <summary>True while the unmanaged corruption job chain still owns vault write buffers.</summary>
        public bool IsJobScheduled => _jobScheduled;

        private void OnEnable()
        {
            _pendingDisableTeardown = false;
            _pendingVaultSwap = false;
            _pendingVaultAfterSwap = null;
            EnsureColdPaths();
            TryRegisterHotSwapListener();
            CacheDataVaultCold();
            EnsureNativeResources();
            TryRegister();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            if (!_nativeReady)
            {
                CacheDataVaultCold();
                EnsureNativeResources();
            }
        }

        private void OnDisable()
        {
            _pendingDisableTeardown = true;
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredUpdate = false;
            }

            TryUnregisterHotSwapListener();
            if (!TryDrainActiveJobIfReady() || !ServicePendingExternalLeaseRelease())
            {
                EnsureLateFrameDrainRegistered();
                return;
            }

            FinishDisableTeardown();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_nativeReady || _jobScheduled || _pendingDisableTeardown || _pendingVaultSwap)
                return;

            _frameIndex++;

            if (!TryLockScheduledBuffers())
                return;

            if (!TryResolveFramePointers(
                    out GlitchStateDTO* state,
                    out byte* table,
                    out ushort* originalText,
                    out ushort* workText,
                    out MockTextSpan* textSpan,
                    out MockCorruptionLevelSignal* corruption,
                    out MockDepthSignal* depth,
                    out MockModuleBreachSignal* breach,
                    out GlitchTuningDTO* tuning,
                    out GlitchQuadTransformDTO* quads,
                    out RadarBlipDTO* radar,
                    out GlitchSynthParametersDTO* synth,
                    out DiegeticGlitchTelemetryEntry* telemetry,
                    out uint* cursor))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                UnlockScheduledBuffers();
                return;
            }

            PrepareFrameTuning(tuning);

            textSpan->Buffer = workText;
            textSpan->Length = _mockTextLength;
            textSpan->ReadabilityPrefixChars = CriticalReadabilityPrefixChars;
            textSpan->ReadabilityDigitBudget = 0;
            textSpan->Flags = 0u;

            _jobStartTimestamp = Stopwatch.GetTimestamp();
            JobHandle handle = new MockCorruptionSignalJob
            {
                State = state,
                Corruption = corruption,
                Depth = depth,
                Breach = breach,
                Tuning = tuning,
                Frame = _frameIndex
            }.Schedule();

            handle = new AsciiScramblerPointerJob
            {
                State = state,
                TextSpan = textSpan,
                Tuning = tuning,
                Source = originalText,
                Buffer = workText,
                GlitchTableBytes = table,
                TableLength = GlitchTableCapacity,
                Frame = _frameIndex
            }.Schedule(_mockTextLength, 32, handle);

            handle = new HolographicMatrixShatterJob
            {
                State = state,
                Tuning = tuning,
                Quads = quads,
                QuadCount = MockQuadCapacity,
                Frame = _frameIndex
            }.Schedule(MockQuadCapacity, 32, handle);

            handle = new RadarGhostInjectionJob
            {
                State = state,
                Tuning = tuning,
                RadarBlips = radar,
                RadarBlipCount = RadarBlipCapacity,
                Frame = _frameIndex
            }.Schedule(RadarBlipCapacity, 32, handle);

            handle = new SynthPitchBendJob
            {
                State = state,
                Tuning = tuning,
                SynthParameters = synth,
                SynthCount = SynthParameterCapacity,
                Frame = _frameIndex
            }.Schedule(SynthParameterCapacity, 8, handle);

            handle = new TelemetryWriteJob
            {
                State = state,
                Tuning = tuning,
                Depth = depth,
                Breach = breach,
                TextSpan = textSpan,
                RadarBlips = radar,
                Telemetry = telemetry,
                Cursor = cursor,
                TableHash = _lastTableHash,
                LastComputeTimeMs = _lastComputeMs,
                Frame = _frameIndex
            }.Schedule(handle);

            _activeHandle = handle;
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.UI, _activeHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!TryDrainActiveJobIfReady())
                return;

            if (!ServicePendingExternalLeaseRelease())
                return;

            if (_pendingVaultSwap)
                FinishPendingVaultSwap();

            if (_pendingDisableTeardown)
            {
                FinishDisableTeardown();
                return;
            }

            PushShaderGlobals();
        }

        /// <summary>Writes editor slider values into the unmanaged vault tuning DTO.</summary>
        public void ApplyTuning(float master, float textRate, float matrixStrength, float ghostCount)
        {
            masterIntensity = math.saturate(master);
            textScrambleRate = math.saturate(textRate);
            matrixShatterStrength = math.saturate(matrixStrength);
            ghostBlipCount = math.clamp(ghostCount, 0f, 32f);
            if (_jobScheduled)
            {
                _pendingMasterIntensity = masterIntensity;
                _pendingTextScrambleRate = textScrambleRate;
                _pendingMatrixShatterStrength = matrixShatterStrength;
                _pendingGhostBlipCount = ghostBlipCount;
                _pendingTuningWrite = true;
                return;
            }

            WriteTuningToVault(masterIntensity, textScrambleRate, matrixShatterStrength, ghostBlipCount);
        }

        private void WriteTuningToVault(float master, float textRate, float matrixStrength, float ghostCount)
        {
            if (!_nativeReady || _vault == null || !_tuningHandle.IsCreated)
                return;

            ref GlitchTuningDTO tuning = ref _tuningHandle.GetElementAsRef(_vault, 0);
            tuning.MasterIntensity = master;
            tuning.TextScrambleRate = textRate;
            tuning.MatrixShatterStrength = matrixStrength;
            tuning.GhostBlipCount = ghostCount;
        }

        /// <summary>Sets the deterministic sector hash mixed with the simulation frame for rollback-safe glitch RNG.</summary>
        public void ApplyDeterministicSectorHash(uint sectorHash)
        {
            deterministicSectorHash = sectorHash == 0u ? 0x5348494Eu : sectorHash;
            if (!_nativeReady || _vault == null || !_tuningHandle.IsCreated)
                return;

            ref GlitchTuningDTO tuning = ref _tuningHandle.GetElementAsRef(_vault, 0);
            tuning.FrameSeed = deterministicSectorHash;
        }

        /// <summary>Returns a mutable ref to the vault state DTO for editor/debug tools.</summary>
        public ref GlitchStateDTO GetGlitchStateRef()
        {
            if (!_nativeReady || _jobScheduled || _vault == null || !_stateHandle.IsCreated)
                return ref s_dummyState;

            return ref _stateHandle.GetElementAsRef(_vault, 0);
        }

        /// <summary>Returns a mutable ref to the vault tuning DTO for editor/debug tools.</summary>
        public ref GlitchTuningDTO GetTuningRef()
        {
            if (!_nativeReady || _jobScheduled || _vault == null || !_tuningHandle.IsCreated)
                return ref s_dummyTuning;

            return ref _tuningHandle.GetElementAsRef(_vault, 0);
        }

        /// <summary>
        /// Locks and exposes the resident GlitchTable.bytes pointer for advanced caller-owned jobs.
        /// The caller may assign <see cref="ExternalAsciiScrambleLease.Handle" /> to its chained job before release.
        /// </summary>
        public bool TryLeaseGlitchTableBytes(
            out ExternalAsciiScrambleLease lease,
            out byte* tableBytes,
            out int tableLength,
            out uint tableHash)
        {
            lease = default;
            tableBytes = null;
            tableLength = 0;
            tableHash = _lastTableHash;
            if (!_nativeReady || _vault == null || !_glitchTableHandle.IsCreated)
                return false;

            if (!_vault.TryLockBuffer(GlitchTableBufferId, SystemID.UI))
                return false;

            if (!TryResolveGlitchTableBytesLocked(out tableBytes, out tableLength, out tableHash))
            {
                _vault.TryUnlockBuffer(GlitchTableBufferId, SystemID.UI);
                return false;
            }

            lease.Owner = this;
            lease.Vault = _vault;
            lease.IsCreated = 1;
            return true;
        }

        private bool TryResolveGlitchTableBytesLocked(out byte* tableBytes, out int tableLength, out uint tableHash)
        {
            tableBytes = null;
            tableLength = 0;
            tableHash = _lastTableHash;
            if (!_nativeReady || _vault == null || !_glitchTableHandle.IsCreated)
                return false;

            tableBytes = (byte*)_glitchTableHandle.ResolvePointer(_vault);
            if (tableBytes == null)
                return false;

            tableLength = GlitchTableCapacity;
            return true;
        }

        /// <summary>Schedules a zero-GC corruption pass over caller-owned UTF-16 ASCII source/destination buffers.</summary>
        public bool TryScheduleExternalAsciiScramble(
            ushort* source,
            ushort* destination,
            int length,
            int readabilityPrefixChars,
            int readabilityDigitBudget,
            float intensity01,
            uint simulationFrame,
            JobHandle dependsOn,
            out ExternalAsciiScrambleLease lease)
        {
            lease = default;
            if (source == null || destination == null || source == destination || length <= 0)
                return false;

            if (!TryReadTuningSnapshot(out GlitchTuningDTO tuning))
                return false;

            if (!TryLeaseGlitchTableBytes(out lease, out byte* tableBytes, out int tableLength, out _))
                return false;

            uint sectorHash = tuning.FrameSeed == 0u ? (deterministicSectorHash == 0u ? 0x5348494Eu : deterministicSectorHash) : tuning.FrameSeed;
            JobHandle handle = ScheduleAsciiScrambleDirect(
                source,
                destination,
                tableBytes,
                tableLength,
                length,
                readabilityPrefixChars,
                readabilityDigitBudget,
                intensity01,
                tuning.TextScrambleRate,
                tuning.GlobalQualityWeight,
                sectorHash,
                simulationFrame,
                0u,
                dependsOn);

            H8Memory.RegisterActiveJob(SystemID.UI, handle);
            lease.Handle = handle;
            return true;
        }

        /// <summary>Schedules a zero-GC in-place corruption pass over one caller-owned UTF-16 ASCII span.</summary>
        public bool TryScheduleExternalAsciiScrambleInPlace(
            ushort* buffer,
            int length,
            int readabilityPrefixChars,
            int readabilityDigitBudget,
            float intensity01,
            uint simulationFrame,
            JobHandle dependsOn,
            out ExternalAsciiScrambleLease lease)
        {
            lease = default;
            if (buffer == null || length <= 0)
                return false;

            if (!TryReadTuningSnapshot(out GlitchTuningDTO tuning))
                return false;

            if (!TryLeaseGlitchTableBytes(out lease, out byte* tableBytes, out int tableLength, out _))
                return false;

            uint sectorHash = tuning.FrameSeed == 0u ? (deterministicSectorHash == 0u ? 0x5348494Eu : deterministicSectorHash) : tuning.FrameSeed;
            JobHandle handle = ScheduleAsciiScrambleInPlaceDirect(
                buffer,
                tableBytes,
                tableLength,
                length,
                readabilityPrefixChars,
                readabilityDigitBudget,
                intensity01,
                tuning.TextScrambleRate,
                tuning.GlobalQualityWeight,
                sectorHash,
                simulationFrame,
                0u,
                dependsOn);

            H8Memory.RegisterActiveJob(SystemID.UI, handle);
            lease.Handle = handle;
            return true;
        }

        public bool TryReleaseExternalAsciiScramble(ref ExternalAsciiScrambleLease lease)
        {
            if (lease.IsCreated == 0 || lease.Owner != this)
                return false;

            if (!lease.Handle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref lease.Handle))
                return false;

            lease.Vault?.TryUnlockBuffer(GlitchTableBufferId, SystemID.UI);

            lease = default;
            return true;
        }

        /// <summary>
        /// Legacy non-blocking release request. It never stalls on the supplied job; use <see cref="TryReleaseExternalAsciiScramble" /> after the dependency has completed.
        /// </summary>
        public void CompleteAndReleaseExternalAsciiScramble(ref ExternalAsciiScrambleLease lease)
        {
            if (lease.IsCreated == 0 || lease.Owner != this)
                return;

            if (TryReleaseExternalAsciiScramble(ref lease))
                return;

            if (_pendingExternalLeaseRelease)
                ServicePendingExternalLeaseRelease();

            if (_pendingExternalLeaseRelease)
                return;

            _pendingExternalLease = lease;
            _pendingExternalLeaseRelease = true;
            lease = default;
        }

        /// <summary>Static pointer kernel entrypoint for Babel/CharBufferPool bridges that already own the table pointer.</summary>
        public static JobHandle ScheduleAsciiScrambleDirect(
            ushort* source,
            ushort* destination,
            byte* glitchTableBytes,
            int glitchTableLength,
            int length,
            int readabilityPrefixChars,
            int readabilityDigitBudget,
            float intensity01,
            float textScrambleRate01,
            float globalQualityWeight01,
            uint sectorHash,
            uint simulationFrame,
            uint tableOffset,
            JobHandle dependsOn)
        {
            if (source == null || destination == null || source == destination || glitchTableBytes == null || length <= 0 || glitchTableLength <= 0)
                return dependsOn;

            int safeLength = math.max(0, length);
            return new AsciiScramblerDirectJob
            {
                Source = source,
                Destination = destination,
                GlitchTableBytes = glitchTableBytes,
                Length = safeLength,
                TableLength = glitchTableLength,
                ReadabilityPrefixChars = math.max(0, readabilityPrefixChars),
                ReadabilityDigitBudget = math.max(0, readabilityDigitBudget),
                Intensity01 = intensity01,
                TextScrambleRate01 = textScrambleRate01,
                GlobalQualityWeight01 = globalQualityWeight01,
                SectorHash = sectorHash == 0u ? 0x5348494Eu : sectorHash,
                SimulationFrame = simulationFrame,
                TableOffset = tableOffset
            }.Schedule(safeLength, 32, dependsOn);
        }

        /// <summary>Static in-place pointer kernel for caller-owned spans where source and destination are identical.</summary>
        public static JobHandle ScheduleAsciiScrambleInPlaceDirect(
            ushort* buffer,
            byte* glitchTableBytes,
            int glitchTableLength,
            int length,
            int readabilityPrefixChars,
            int readabilityDigitBudget,
            float intensity01,
            float textScrambleRate01,
            float globalQualityWeight01,
            uint sectorHash,
            uint simulationFrame,
            uint tableOffset,
            JobHandle dependsOn)
        {
            if (buffer == null || glitchTableBytes == null || length <= 0 || glitchTableLength <= 0)
                return dependsOn;

            return new AsciiScramblerInPlaceJob
            {
                Buffer = buffer,
                GlitchTableBytes = glitchTableBytes,
                Length = math.max(0, length),
                TableLength = glitchTableLength,
                ReadabilityPrefixChars = math.max(0, readabilityPrefixChars),
                ReadabilityDigitBudget = math.max(0, readabilityDigitBudget),
                Intensity01 = intensity01,
                TextScrambleRate01 = textScrambleRate01,
                GlobalQualityWeight01 = globalQualityWeight01,
                SectorHash = sectorHash == 0u ? 0x5348494Eu : sectorHash,
                SimulationFrame = simulationFrame,
                TableOffset = tableOffset
            }.Schedule(dependsOn);
        }

        /// <summary>Copies the current mock text buffer into a caller-owned editor preview buffer.</summary>
        public int CopyMockTextTo(char[] destination)
        {
            return destination == null ? 0 : CopyMockTextTo(destination.AsSpan());
        }

        /// <summary>Copies the current mock text buffer into a caller-owned preview span.</summary>
        public int CopyMockTextTo(Span<char> destination)
        {
            if (destination.Length == 0 || !_nativeReady || _vault == null)
                return 0;

            if (_jobScheduled)
                return -1;

            if (!_vault.TryLockBuffer(WorkTextBufferId, SystemID.UI))
                return -1;

            try
            {
                ushort* text = (ushort*)_workTextHandle.ResolvePointer(_vault);
                if (text == null)
                    return 0;

                int count = math.min(math.min(_mockTextLength, destination.Length), MockTextCapacity);
                for (int i = 0; i < count; i++)
                    destination[i] = (char)text[i];

                if (count < destination.Length)
                    destination[count] = '\0';

                return count;
            }
            finally
            {
                _vault.TryUnlockBuffer(WorkTextBufferId, SystemID.UI);
            }
        }

        /// <summary>Forces a cold reload of GlitchTable.bytes or the emergency fallback, then applies CSV glyph overrides.</summary>
        public void ReloadGlitchTableForEditor()
        {
            if (_jobScheduled)
            {
                _pendingTableReload = true;
                _pendingCsvReload = true;
                return;
            }

            if (!_nativeReady || _vault == null || !_glitchTableHandle.IsCreated)
                return;

            if (!_vault.TryLockBuffer(GlitchTableBufferId, SystemID.UI))
                return;

            try
            {
                byte* table = (byte*)_glitchTableHandle.ResolvePointer(_vault);
                if (table == null)
                    return;

                LoadGlitchTableCold(table, GlitchTableCapacity);
            }
            finally
            {
                _vault.TryUnlockBuffer(GlitchTableBufferId, SystemID.UI);
            }

            if (!TryApplyCsvOverride(out bool retryCsv) && retryCsv)
                _pendingCsvReload = true;
        }

        /// <summary>Forces a zero-GC parser pass over the configured glitch_profiles.csv file.</summary>
        public void ReloadCsvForEditor()
        {
            if (_jobScheduled)
            {
                _pendingCsvReload = true;
                return;
            }

            if (!_nativeReady || _vault == null || !_csvScratchHandle.IsCreated)
                return;

            if (!TryApplyCsvOverride(out bool retryCsv) && retryCsv)
                _pendingCsvReload = true;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only CSV watch poll. Kept outside gameplay Tick to avoid runtime file I/O in hot paths.</summary>
        public bool PollCsvOverrideForEditor(double editorTimeSeconds)
        {
            if (_jobScheduled)
                return false;

            if (_pendingTableReload)
            {
                _pendingTableReload = false;
                _pendingCsvReload = false;
                ReloadGlitchTableForEditor();
                return true;
            }

            if (_pendingCsvReload)
            {
                _pendingCsvReload = false;
                ReloadCsvForEditor();
                return true;
            }

            if (editorTimeSeconds < _nextEditorCsvPollTime)
                return false;

            _nextEditorCsvPollTime = editorTimeSeconds + 0.5d;
            if (!_nativeReady || _vault == null || string.IsNullOrEmpty(_csvFullPath))
                return false;

            DateTime lastWriteUtc = File.Exists(_csvFullPath) ? File.GetLastWriteTimeUtc(_csvFullPath) : default;
            if (lastWriteUtc == default || lastWriteUtc <= _csvLastWriteUtc)
                return false;

            _csvLastWriteUtc = lastWriteUtc;
            ReloadCsvForEditor();
            return true;
        }
#endif

        /// <summary>One-line Terminal OS UV tear hook: Value2 is the shader-side tear scalar in TerminalBlit.compute.</summary>
        public static void ApplyTerminalUvTearing(ref TerminalStateDTO terminal, float intensity01)
        {
            terminal.Value2 = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            terminal.IsDirty = 1;
        }

        private static void ApplyTerminalUvTearing(ref TerminalStateDTO terminal, float intensity01, float previousAppliedIntensity01)
        {
            float safeIntensity = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            float previous = math.saturate(math.isfinite(previousAppliedIntensity01) ? previousAppliedIntensity01 : 0f);
            float current = math.saturate(math.isfinite(terminal.Value2) ? terminal.Value2 : 0f);
            float preservedExternal = current <= previous + 0.001f ? 0f : current;
            float next = math.saturate(math.max(preservedExternal, safeIntensity));
            if (math.abs(current - next) <= 0.0005f && next <= 0.001f)
                return;

            terminal.Value2 = next;
            terminal.IsDirty = 1;
        }

        private void TryRegister()
        {
            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (!TryDrainActiveJobIfReady())
            {
                _pendingVaultSwap = true;
                _pendingVaultAfterSwap = currentService as IDataVault;
                _nativeReady = false;
                EnsureLateFrameDrainRegistered();
                return;
            }

            _nativeReady = false;
            _vault = currentService as IDataVault;
            EnsureNativeResources();
        }

        private void EnsureColdPaths()
        {
            if (!string.IsNullOrEmpty(_projectRoot))
                return;

            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _glitchTableFullPath = Path.GetFullPath(Path.Combine(_projectRoot, glitchTableRelativePath));
            _csvFullPath = Path.GetFullPath(Path.Combine(_projectRoot, csvRelativePath));
            _dumpFullPath = Path.GetFullPath(Path.Combine(_projectRoot, dumpRelativePath));
        }

        private void CacheDataVaultCold()
        {
            _vault = GlobalRegistry.DataVault;
            if (_vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                _vault = latestVault;
        }

        private void EnsureNativeResources()
        {
            if (_nativeReady)
                return;

            EnsureColdPaths();
            if (_vault == null)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            _stateHandle = _vault.GetBufferHandle<GlitchStateDTO>(StateBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _glitchTableHandle = _vault.GetBufferHandle<byte>(GlitchTableBufferId, GlitchTableCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _originalTextHandle = _vault.GetBufferHandle<ushort>(OriginalTextBufferId, MockTextCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _workTextHandle = _vault.GetBufferHandle<ushort>(WorkTextBufferId, MockTextCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _textSpanHandle = _vault.GetBufferHandle<MockTextSpan>(TextSpanBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _corruptionSignalHandle = _vault.GetBufferHandle<MockCorruptionLevelSignal>(CorruptionSignalBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _depthSignalHandle = _vault.GetBufferHandle<MockDepthSignal>(DepthSignalBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _breachSignalHandle = _vault.GetBufferHandle<MockModuleBreachSignal>(BreachSignalBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = _vault.GetBufferHandle<GlitchTuningDTO>(TuningBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _quadHandle = _vault.GetBufferHandle<GlitchQuadTransformDTO>(MockQuadBufferId, MockQuadCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _radarBlipHandle = _vault.GetBufferHandle<RadarBlipDTO>(RadarBlipBufferId, RadarBlipCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _synthHandle = _vault.GetBufferHandle<GlitchSynthParametersDTO>(SynthParameterBufferId, SynthParameterCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _vault.GetBufferHandle<DiegeticGlitchTelemetryEntry>(TelemetryRingBufferId, TelemetryFrameCount, SystemID.UI, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = _vault.GetBufferHandle<uint>(TelemetryCursorBufferId, 1, SystemID.UI, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = _vault.GetBufferHandle<byte>(CsvScratchBufferId, CsvScratchCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);

            if (!ValidateStructLayouts() || !ValidateHandles())
                return;

            InitializeVaultDefaults();
            _nativeReady = true;
        }

        private bool ValidateStructLayouts()
        {
            bool valid = UnsafeUtility.SizeOf<GlitchStateDTO>() == 16 &&
                         UnsafeUtility.SizeOf<ScrambledCharacterDTO>() == 4 &&
                         UnsafeUtility.SizeOf<MockTextSpan>() == 32 &&
                         UnsafeUtility.SizeOf<GlitchTuningDTO>() == 32 &&
                         UnsafeUtility.SizeOf<GlitchQuadTransformDTO>() == 112 &&
                         UnsafeUtility.SizeOf<RadarBlipDTO>() == 32 &&
                         UnsafeUtility.SizeOf<GlitchSynthParametersDTO>() == 16 &&
                         UnsafeUtility.SizeOf<DiegeticGlitchTelemetryEntry>() == 64 &&
                         UnsafeUtility.SizeOf<GlitchBlackBoxDumpHeader>() == 32;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!valid)
                Debug.LogError("SHINOBU_49 glitch DTO layout mismatch.");
#endif
            return valid;
        }

        private bool ValidateHandles()
        {
            return _stateHandle.IsCreated &&
                   _glitchTableHandle.IsCreated &&
                   _originalTextHandle.IsCreated &&
                   _workTextHandle.IsCreated &&
                   _textSpanHandle.IsCreated &&
                   _corruptionSignalHandle.IsCreated &&
                   _depthSignalHandle.IsCreated &&
                   _breachSignalHandle.IsCreated &&
                   _tuningHandle.IsCreated &&
                   _quadHandle.IsCreated &&
                   _radarBlipHandle.IsCreated &&
                   _synthHandle.IsCreated &&
                   _telemetryHandle.IsCreated &&
                   _telemetryCursorHandle.IsCreated &&
                   _csvScratchHandle.IsCreated;
        }

        private void InitializeVaultDefaults()
        {
            ref GlitchStateDTO state = ref _stateHandle.GetElementAsRef(_vault, 0);
            state.GlobalIntensity = 0f;
            state.Seed = math.asfloat(0x3F800000u);
            state.GlitchTableOffset = 0u;
            state._pad0 = 0u;

            ref GlitchTuningDTO tuning = ref _tuningHandle.GetElementAsRef(_vault, 0);
            tuning.MasterIntensity = math.saturate(masterIntensity);
            tuning.TextScrambleRate = math.saturate(textScrambleRate);
            tuning.MatrixShatterStrength = math.saturate(matrixShatterStrength);
            tuning.GhostBlipCount = math.clamp(ghostBlipCount, 0f, 32f);
            tuning.DepthStartMeters = math.max(0f, depthStartMeters);
            tuning.DepthFullMeters = math.max(tuning.DepthStartMeters + 1f, depthFullMeters);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning.FrameSeed = deterministicSectorHash == 0u ? 0x5348494Eu : deterministicSectorHash;

            LoadGlitchTableCold((byte*)_glitchTableHandle.ResolvePointer(_vault), GlitchTableCapacity);
            SeedMockText();
            SeedMockQuads();
            SeedSynthParameters();
        }

        private void SeedMockText()
        {
            ushort* original = (ushort*)_originalTextHandle.ResolvePointer(_vault);
            ushort* work = (ushort*)_workTextHandle.ResolvePointer(_vault);
            if (original == null || work == null)
                return;

            const string Source = "O2 98%  DEPTH 1024M  SIGNAL CLEAN";
            _mockTextLength = math.min(Source.Length, MockTextCapacity);
            for (int i = 0; i < MockTextCapacity; i++)
            {
                ushort value = i < _mockTextLength ? (ushort)Source[i] : (ushort)0;
                original[i] = value;
                work[i] = value;
            }
        }

        private void SeedMockQuads()
        {
            GlitchQuadTransformDTO* quads = (GlitchQuadTransformDTO*)_quadHandle.ResolvePointer(_vault);
            if (quads == null)
                return;

            for (int i = 0; i < MockQuadCapacity; i++)
            {
                quads[i] = new GlitchQuadTransformDTO
                {
                    Matrix = BuildMockQuadMatrixForIndex(i),
                    Color = new float4(0.18f, 0.95f, 0.62f, 0.82f),
                    UVRect = new float4(0f, 0f, 1f, 1f),
                    CharacterCode = i < 16 ? SpecialRadarBlipCode : (uint)('A' + (i % 26)),
                    GlitchIntensity = 0f,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
            }
        }

        private void SeedSynthParameters()
        {
            GlitchSynthParametersDTO* synth = (GlitchSynthParametersDTO*)_synthHandle.ResolvePointer(_vault);
            if (synth == null)
                return;

            for (int i = 0; i < SynthParameterCapacity; i++)
            {
                synth[i] = new GlitchSynthParametersDTO
                {
                    BaseFrequency = 180f + i * 35f,
                    ModulationIndex = 0.25f,
                    GrainSize = 0.045f + i * 0.0025f,
                    PressureScalar = 0f
                };
            }
        }

        private void LoadGlitchTableCold(byte* table, int length)
        {
            _tableFallbackGenerated = false;
            if (table == null || length <= 0)
                return;

            int written = 0;
            try
            {
                if (File.Exists(_glitchTableFullPath))
                {
                    using (FileStream stream = new FileStream(_glitchTableFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64))
                    {
                        while (written < length)
                        {
                            int value = stream.ReadByte();
                            if (value < 0)
                                break;

                            table[written++] = SanitizeGlyphByte((byte)value);
                        }
                    }
                }
            }
            catch (Exception)
            {
                written = 0;
            }

            if (written <= 0)
            {
                GlitchTable.GenerateEmergencyMockGlitchTable(table, length);
                _tableFallbackGenerated = true;
                _lastFaultFlags |= FaultTableFallback;
            }
            else
            {
                while (written < length)
                {
                    table[written] = table[written & 15];
                    written++;
                }
            }

            _lastTableHash = HashBytes(table, length);
        }

        private bool TryApplyCsvOverride(out bool shouldRetry)
        {
            shouldRetry = false;
            if (_vault == null)
                return false;

            if (!_vault.TryLockBuffer(CsvScratchBufferId, SystemID.UI))
            {
                shouldRetry = true;
                return false;
            }

            if (!_vault.TryLockBuffer(GlitchTableBufferId, SystemID.UI))
            {
                _vault.TryUnlockBuffer(CsvScratchBufferId, SystemID.UI);
                shouldRetry = true;
                return false;
            }

            try
            {
                byte* scratch = (byte*)_csvScratchHandle.ResolvePointer(_vault);
                byte* table = (byte*)_glitchTableHandle.ResolvePointer(_vault);
                if (scratch == null || table == null)
                    return false;

                if (string.IsNullOrEmpty(_csvFullPath) || !File.Exists(_csvFullPath))
                    return false;

                int length = 0;
                try
                {
                    using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128))
                    {
                        while (length < CsvScratchCapacity)
                        {
                            int value = stream.ReadByte();
                            if (value < 0)
                                break;

                            scratch[length++] = (byte)value;
                        }
                    }
                }
                catch (Exception)
                {
                    shouldRetry = true;
                    return false;
                }

                int written = ParseGlyphCsv(scratch, length, table, GlitchTableCapacity);
                if (written <= 0)
                    return false;

                while (written < GlitchTableCapacity)
                {
                    table[written] = table[written & 15];
                    written++;
                }

                _lastTableHash = HashBytes(table, GlitchTableCapacity);
                _tableFallbackGenerated = false;
                return true;
            }
            finally
            {
                _vault.TryUnlockBuffer(GlitchTableBufferId, SystemID.UI);
                _vault.TryUnlockBuffer(CsvScratchBufferId, SystemID.UI);
            }
        }

        private void PrepareFrameTuning(GlitchTuningDTO* tuning)
        {
            if (tuning == null)
                return;

            tuning->GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning->DepthStartMeters = math.max(0f, depthStartMeters);
            tuning->DepthFullMeters = math.max(tuning->DepthStartMeters + 1f, depthFullMeters);
            tuning->FrameSeed = deterministicSectorHash == 0u ? 0x5348494Eu : deterministicSectorHash;
        }

        private bool TryReadTuningSnapshot(out GlitchTuningDTO tuning)
        {
            tuning = default;
            if (!_nativeReady || _vault == null || !_tuningHandle.IsCreated)
                return false;

            if (!_vault.TryLockBuffer(TuningBufferId, SystemID.UI))
                return false;

            try
            {
                GlitchTuningDTO* source = (GlitchTuningDTO*)_tuningHandle.ResolvePointer(_vault);
                if (source == null)
                    return false;

                tuning = *source;
                return true;
            }
            finally
            {
                _vault.TryUnlockBuffer(TuningBufferId, SystemID.UI);
            }
        }

        private bool TryResolveFramePointers(
            out GlitchStateDTO* state,
            out byte* table,
            out ushort* originalText,
            out ushort* workText,
            out MockTextSpan* textSpan,
            out MockCorruptionLevelSignal* corruption,
            out MockDepthSignal* depth,
            out MockModuleBreachSignal* breach,
            out GlitchTuningDTO* tuning,
            out GlitchQuadTransformDTO* quads,
            out RadarBlipDTO* radar,
            out GlitchSynthParametersDTO* synth,
            out DiegeticGlitchTelemetryEntry* telemetry,
            out uint* cursor)
        {
            state = (GlitchStateDTO*)_stateHandle.ResolvePointer(_vault);
            table = (byte*)_glitchTableHandle.ResolvePointer(_vault);
            originalText = (ushort*)_originalTextHandle.ResolvePointer(_vault);
            workText = (ushort*)_workTextHandle.ResolvePointer(_vault);
            textSpan = (MockTextSpan*)_textSpanHandle.ResolvePointer(_vault);
            corruption = (MockCorruptionLevelSignal*)_corruptionSignalHandle.ResolvePointer(_vault);
            depth = (MockDepthSignal*)_depthSignalHandle.ResolvePointer(_vault);
            breach = (MockModuleBreachSignal*)_breachSignalHandle.ResolvePointer(_vault);
            tuning = (GlitchTuningDTO*)_tuningHandle.ResolvePointer(_vault);
            quads = (GlitchQuadTransformDTO*)_quadHandle.ResolvePointer(_vault);
            radar = (RadarBlipDTO*)_radarBlipHandle.ResolvePointer(_vault);
            synth = (GlitchSynthParametersDTO*)_synthHandle.ResolvePointer(_vault);
            telemetry = (DiegeticGlitchTelemetryEntry*)_telemetryHandle.ResolvePointer(_vault);
            cursor = (uint*)_telemetryCursorHandle.ResolvePointer(_vault);

            return state != null &&
                   table != null &&
                   originalText != null &&
                   workText != null &&
                   textSpan != null &&
                   corruption != null &&
                   depth != null &&
                   breach != null &&
                   tuning != null &&
                   quads != null &&
                   radar != null &&
                   synth != null &&
                   telemetry != null &&
                   cursor != null;
        }

        private bool TryLockScheduledBuffers()
        {
            _lockMask = 0;
            return TryLockOne(StateBufferId, LockBitState) &&
                   TryLockOne(GlitchTableBufferId, LockBitTable) &&
                   TryLockOne(OriginalTextBufferId, LockBitOriginalText) &&
                   TryLockOne(WorkTextBufferId, LockBitWorkText) &&
                   TryLockOne(TextSpanBufferId, LockBitTextSpan) &&
                   TryLockOne(CorruptionSignalBufferId, LockBitCorruption) &&
                   TryLockOne(DepthSignalBufferId, LockBitDepth) &&
                   TryLockOne(BreachSignalBufferId, LockBitBreach) &&
                   TryLockOne(TuningBufferId, LockBitTuning) &&
                   TryLockOne(MockQuadBufferId, LockBitQuads) &&
                   TryLockOne(RadarBlipBufferId, LockBitRadar) &&
                   TryLockOne(SynthParameterBufferId, LockBitSynth) &&
                   TryLockOne(TelemetryRingBufferId, LockBitTelemetry) &&
                   TryLockOne(TelemetryCursorBufferId, LockBitCursor);
        }

        private bool TryLockOne(BufferID bufferId, int bit)
        {
            if (_vault == null || !_vault.TryLockBuffer(bufferId, SystemID.UI))
            {
                UnlockScheduledBuffers();
                return false;
            }

            _lockMask |= 1 << bit;
            return true;
        }

        private void UnlockScheduledBuffers()
        {
            if (_vault == null || _lockMask == 0)
            {
                _lockMask = 0;
                return;
            }

            UnlockOne(StateBufferId, LockBitState);
            UnlockOne(GlitchTableBufferId, LockBitTable);
            UnlockOne(OriginalTextBufferId, LockBitOriginalText);
            UnlockOne(WorkTextBufferId, LockBitWorkText);
            UnlockOne(TextSpanBufferId, LockBitTextSpan);
            UnlockOne(CorruptionSignalBufferId, LockBitCorruption);
            UnlockOne(DepthSignalBufferId, LockBitDepth);
            UnlockOne(BreachSignalBufferId, LockBitBreach);
            UnlockOne(TuningBufferId, LockBitTuning);
            UnlockOne(MockQuadBufferId, LockBitQuads);
            UnlockOne(RadarBlipBufferId, LockBitRadar);
            UnlockOne(SynthParameterBufferId, LockBitSynth);
            UnlockOne(TelemetryRingBufferId, LockBitTelemetry);
            UnlockOne(TelemetryCursorBufferId, LockBitCursor);
            _lockMask = 0;
        }

        private void UnlockOne(BufferID bufferId, int bit)
        {
            int mask = 1 << bit;
            if ((_lockMask & mask) == 0)
                return;

            _vault.TryUnlockBuffer(bufferId, SystemID.UI);
            _lockMask &= ~mask;
        }

        private bool TryDrainActiveJobIfReady()
        {
            if (!_jobScheduled)
                return true;

            if (!_activeHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeHandle))
                return false;

            _jobScheduled = false;
            _lastComputeMs = (float)((Stopwatch.GetTimestamp() - _jobStartTimestamp) * 1000.0 / Stopwatch.Frequency);
            UnlockScheduledBuffers();
            ApplyDeferredEditorWrites();
            InspectAndDumpIfNeeded();
            return true;
        }

        private void FinishDisableTeardown()
        {
            UnlockScheduledBuffers();
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            _pendingDisableTeardown = false;
            _pendingVaultSwap = false;
            _pendingVaultAfterSwap = null;
            _nativeReady = false;
            _vault = null;
        }

        private void FinishPendingVaultSwap()
        {
            _pendingVaultSwap = false;
            _nativeReady = false;
            _vault = _pendingVaultAfterSwap;
            _pendingVaultAfterSwap = null;
            EnsureNativeResources();
        }

        private void EnsureLateFrameDrainRegistered()
        {
            if (_registeredLateFrame)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void PushShaderGlobals()
        {
            if (!_nativeReady || _vault == null || !_stateHandle.IsCreated || !_tuningHandle.IsCreated)
                return;

            ref GlitchStateDTO state = ref _stateHandle.GetElementAsRef(_vault, 0);
            float intensity = math.saturate(math.isfinite(state.GlobalIntensity) ? state.GlobalIntensity : 0f);
            float seed01 = math.asuint(state.Seed) * (1f / 4294967295f);
            if (math.abs(intensity - _lastShaderIntensity) > 0.0005f)
            {
                Shader.SetGlobalFloat(_terminalDamageGlitchId, intensity);
                Shader.SetGlobalFloat(_diegeticGlitchIntensityId, intensity);
                _lastShaderIntensity = intensity;
            }

            if (math.abs(seed01 - _lastShaderSeed01) > 0.000001f)
            {
                Shader.SetGlobalFloat(_diegeticGlitchSeedId, seed01);
                _lastShaderSeed01 = seed01;
            }

            ref GlitchTuningDTO tuning = ref _tuningHandle.GetElementAsRef(_vault, 0);
            float quality = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            if (math.abs(quality - _lastShaderQualityWeight) > 0.0005f)
            {
                Shader.SetGlobalFloat(_diegeticGlitchQualityWeightId, quality);
                _lastShaderQualityWeight = quality;
            }

            if (ShouldPushTerminalStateGlitch(intensity, quality))
            {
                float previous = _lastTerminalBridgeIntensity;
                if (TryPushTerminalStateGlitch(intensity, previous))
                    _lastTerminalBridgeIntensity = intensity;
            }
        }

        private bool ShouldPushTerminalStateGlitch(float intensity, float quality)
        {
            float active = math.max(intensity, _lastTerminalBridgeIntensity);
            if (active <= 0.001f && math.abs(intensity - _lastTerminalBridgeIntensity) <= 0.0005f)
                return false;

            uint period = (uint)math.max(1, (int)math.round(math.lerp(12f, 1f, Smooth01(quality))));
            return math.abs(intensity - _lastTerminalBridgeIntensity) > 0.02f || (_frameIndex % period) == 0u;
        }

        private bool TryPushTerminalStateGlitch(float intensity, float previousIntensity)
        {
            if (_vault == null || !_vault.TryGetBufferHandle(TerminalOsStateBridgeBufferId, out _terminalStateBridgeHandle))
                return false;

            if (!_vault.TryLockBuffer(TerminalOsStateBridgeBufferId, SystemID.UI))
                return false;

            try
            {
                TerminalStateDTO* states = (TerminalStateDTO*)_terminalStateBridgeHandle.ResolvePointer(_vault);
                if (states == null)
                    return false;

                int count = math.min(_terminalStateBridgeHandle.Length, TerminalOsConstants.TerminalCapacity);
                for (int i = 0; i < count; i++)
                    ApplyTerminalUvTearing(ref states[i], intensity, previousIntensity);
            }
            finally
            {
                _vault.TryUnlockBuffer(TerminalOsStateBridgeBufferId, SystemID.UI);
            }

            return true;
        }

        private void ApplyDeferredEditorWrites()
        {
            if (_jobScheduled)
                return;

            if (_pendingTuningWrite)
            {
                WriteTuningToVault(_pendingMasterIntensity, _pendingTextScrambleRate, _pendingMatrixShatterStrength, _pendingGhostBlipCount);
                _pendingTuningWrite = false;
            }

            // File-backed table reloads are serviced by DiegeticGlitchTunerWindow's editor update hook.
        }

        private bool ServicePendingExternalLeaseRelease()
        {
            if (!_pendingExternalLeaseRelease)
                return true;

            if (TryReleaseExternalAsciiScramble(ref _pendingExternalLease))
            {
                _pendingExternalLeaseRelease = false;
                return true;
            }

            return false;
        }

        private void InspectAndDumpIfNeeded()
        {
            if (!_nativeReady || _vault == null)
                return;

            ref GlitchStateDTO state = ref _stateHandle.GetElementAsRef(_vault, 0);
            uint faultFlags = _lastFaultFlags;
            if (!math.isfinite(state.GlobalIntensity) || !math.isfinite(state.Seed))
                faultFlags |= FaultNonFinite;

            uint seedBits = math.asuint(state.Seed);
            if (seedBits == _lastSeedBits)
            {
                _stalledSeedFrames++;
                if (_stalledSeedFrames >= 3)
                    faultFlags |= FaultRngDeadlock;
            }
            else
            {
                _lastSeedBits = seedBits;
                _stalledSeedFrames = 0;
            }

            if (_lastComputeMs > 0.1f)
                faultFlags |= FaultOverBudget;

            if (_tableFallbackGenerated)
                faultFlags |= FaultTableFallback;

            _lastFaultFlags = faultFlags;
            if (faultFlags == 0u)
            {
                _dumpWrittenForCurrentFault = false;
                return;
            }

            if (_dumpWrittenForCurrentFault)
                return;

            DumpBlackBox(faultFlags);
            _dumpWrittenForCurrentFault = true;
        }

        private void DumpBlackBox(uint faultFlags)
        {
            DiegeticGlitchTelemetryEntry* telemetry = (DiegeticGlitchTelemetryEntry*)_telemetryHandle.ResolvePointer(_vault);
            uint* cursor = (uint*)_telemetryCursorHandle.ResolvePointer(_vault);
            if (telemetry == null || cursor == null)
                return;

            try
            {
                string directory = Path.GetDirectoryName(_dumpFullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                GlitchBlackBoxDumpHeader header = new GlitchBlackBoxDumpHeader
                {
                    Magic = DumpMagic,
                    Version = DumpVersion,
                    EntryCount = TelemetryFrameCount,
                    Cursor = *cursor,
                    FaultFlags = faultFlags,
                    TableHash = _lastTableHash,
                    TimestampTicks = (ulong)DateTime.UtcNow.Ticks
                };

                using (FileStream stream = new FileStream(_dumpFullPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096))
                {
                    stream.Write(new ReadOnlySpan<byte>((byte*)&header, UnsafeUtility.SizeOf<GlitchBlackBoxDumpHeader>()));
                    stream.Write(new ReadOnlySpan<byte>((byte*)telemetry, UnsafeUtility.SizeOf<DiegeticGlitchTelemetryEntry>() * TelemetryFrameCount));
                }
            }
            catch (Exception)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
            }
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static float4x4 BuildMockQuadMatrix(float3 position, float2 size)
        {
            float4x4 matrix = float4x4.identity;
            matrix.c0 = new float4(size.x, 0f, 0f, 0f);
            matrix.c1 = new float4(0f, size.y, 0f, 0f);
            matrix.c2 = new float4(0f, 0f, 1f, 0f);
            matrix.c3 = new float4(position, 1f);
            return matrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4x4 BuildMockQuadMatrixForIndex(int index)
        {
            float x = ((index & 15) - 8) * 0.018f;
            float y = ((index >> 4) - 4) * 0.014f;
            return BuildMockQuadMatrix(new float3(x, y, 0.6f), new float2(0.012f, 0.012f));
        }

        private static int ParseGlyphCsv(byte* source, int sourceLength, byte* destination, int destinationLength)
        {
            if (source == null || sourceLength <= 0 || destination == null || destinationLength <= 0)
                return 0;

            int written = 0;
            bool capture = false;
            bool rawLine = true;
            bool comment = false;
            for (int i = 0; i < sourceLength && written < destinationLength; i++)
            {
                byte value = source[i];
                if (value == '\r')
                    continue;

                if (value == '\n')
                {
                    if (written > 0)
                        break;

                    capture = false;
                    rawLine = true;
                    comment = false;
                    continue;
                }

                if (comment)
                    continue;

                if (rawLine && value == '#')
                {
                    comment = true;
                    continue;
                }

                if (value == ',' || value == ';' || value == '=')
                {
                    capture = true;
                    rawLine = false;
                    continue;
                }

                if (rawLine && IsAsciiLetter(value))
                    continue;

                rawLine = false;
                if (capture || IsGlyphByte(value))
                    destination[written++] = SanitizeGlyphByte(value);
            }

            return written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte SanitizeGlyphByte(byte value)
        {
            return IsGlyphByte(value) ? value : (byte)'#';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsGlyphByte(byte value)
        {
            return value >= 33 && value <= 126 && value != (byte)'"';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiLetter(byte value)
        {
            return (value >= (byte)'A' && value <= (byte)'Z') || (value >= (byte)'a' && value <= (byte)'z');
        }

        private static uint HashBytes(byte* bytes, int length)
        {
            uint hash = 2166136261u;
            if (bytes == null || length <= 0)
                return hash;

            for (int i = 0; i < length; i++)
                hash = (hash ^ bytes[i]) * 16777619u;

            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NonZeroRandomSeed(uint value)
        {
            uint seed = Hash(value);
            return seed == 0u ? 1u : seed;
        }

        private static bool IsCriticalReadable(int index, ushort source, int readabilityPrefixChars, int readabilityDigitBudget, ushort* sourceBuffer)
        {
            if (index < readabilityPrefixChars)
                return true;

            if (sourceBuffer == null || source < '0' || source > '9' || readabilityDigitBudget <= 0)
                return false;

            int previousDigits = 0;
            for (int i = 0; i < index; i++)
            {
                ushort value = sourceBuffer[i];
                if (value >= '0' && value <= '9')
                    previousDigits++;
            }

            return previousDigits < readabilityDigitBudget;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct MockCorruptionSignalJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchStateDTO* State;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockCorruptionLevelSignal* Corruption;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockDepthSignal* Depth;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockModuleBreachSignal* Breach;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchTuningDTO* Tuning;
            public uint Frame;

            public void Execute()
            {
                if (State == null || Corruption == null || Depth == null || Breach == null || Tuning == null)
                    return;

                ref GlitchTuningDTO tuning = ref UnsafeUtility.AsRef<GlitchTuningDTO>(Tuning);
                float quality = Sanitize01(tuning.GlobalQualityWeight, 1f);
                float deterministicSeconds = Frame * (1f / 60f);
                float pulse = math.sin(deterministicSeconds * 0.83f) * 0.5f + 0.5f;
                float surge = math.pow(pulse, math.lerp(3.5f, 1.1f, Smooth01(quality)));
                float corruption01 = Sanitize01(surge * tuning.MasterIntensity, 0f);
                float depthMeters = 850f + (math.sin(deterministicSeconds * 0.071f + 0.4f) * 0.5f + 0.5f) * 2600f;
                float depthRange = math.max(1f, tuning.DepthFullMeters - tuning.DepthStartMeters);
                float depthBaseline = math.saturate((depthMeters - tuning.DepthStartMeters) / depthRange);
                uint breachBit = 1u << (int)((Frame / 240u) & 15u);
                float breachIntensity = ((Frame / 480u) & 1u) == 0u ? 0f : 0.28f;
                float intensity = math.saturate(math.max(corruption01, depthBaseline * 0.42f) + breachIntensity);

                Corruption->Corruption01 = corruption01;
                Corruption->SimulationSeconds = deterministicSeconds;
                Corruption->Frame = Frame;
                Corruption->_pad0 = 0u;

                Depth->DepthMeters = math.isfinite(depthMeters) ? depthMeters : 0f;
                Depth->BaselineIntensity = math.isfinite(depthBaseline) ? depthBaseline : 0f;
                Depth->Frame = Frame;
                Depth->_pad0 = 0u;

                Breach->BreachedMask0 = breachIntensity > 0f ? breachBit : 0u;
                Breach->BreachedMask1 = 0u;
                Breach->ActiveRoomIndex = (Frame / 240u) & 15u;
                Breach->Frame = Frame;

                State->GlobalIntensity = math.isfinite(intensity) ? intensity : 0f;
                State->Seed = math.asfloat(0x3F800000u | (Hash(Frame ^ tuning.FrameSeed) & 0x007FFFFFu));
                State->GlitchTableOffset = (Hash(Frame + 17u) >> 24) & 63u;
                State->_pad0 = 0u;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AsciiScramblerPointerJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchStateDTO* State;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockTextSpan* TextSpan;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchTuningDTO* Tuning;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public ushort* Source;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public ushort* Buffer;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public byte* GlitchTableBytes;
            public int TableLength;
            public uint Frame;

            public void Execute(int index)
            {
                if (State == null || TextSpan == null || Tuning == null || Source == null || Buffer == null || GlitchTableBytes == null)
                    return;

                int length = math.min(TextSpan->Length, MockTextCapacity);
                if ((uint)index >= (uint)length)
                    return;

                ushort source = Source[index];
                Buffer[index] = source;
                if (source <= 32u || source > 126u)
                    return;

                float intensity = Sanitize01(State->GlobalIntensity, 0f);
                if (intensity < 0.9f && IsCriticalReadable(index, source, TextSpan->ReadabilityPrefixChars, TextSpan->ReadabilityDigitBudget, Source))
                    return;

                uint seed = math.asuint(State->Seed);
                uint mixed = Hash(seed ^ Frame ^ ((uint)index * 0x9E3779B9u) ^ ((uint)source << 16));
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(NonZeroRandomSeed(mixed));
                float sample = rng.NextFloat();
                float quality = Sanitize01(Tuning->GlobalQualityWeight, 1f);
                float authorRate = math.saturate(Tuning->TextScrambleRate);
                float rate = math.saturate(intensity * authorRate * math.lerp(0.2f, 1f, Smooth01(quality)) * math.lerp(0.55f, 1f, Smooth01(intensity)));
                if (sample > rate)
                    return;

                uint safeTableLength = (uint)math.max(1, TableLength);
                int tableIndex = (int)((rng.NextUInt() + State->GlitchTableOffset) % safeTableLength);
                byte replacement = GlitchTableBytes[tableIndex];
                Buffer[index] = (ushort)(replacement >= 33 && replacement <= 126 ? replacement : (byte)'#');
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AsciiScramblerDirectJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public ushort* Source;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public ushort* Destination;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public byte* GlitchTableBytes;
            public int Length;
            public int TableLength;
            public int ReadabilityPrefixChars;
            public int ReadabilityDigitBudget;
            public float Intensity01;
            public float TextScrambleRate01;
            public float GlobalQualityWeight01;
            public uint SectorHash;
            public uint SimulationFrame;
            public uint TableOffset;

            public void Execute(int index)
            {
                if (Source == null || Destination == null || GlitchTableBytes == null || (uint)index >= (uint)Length)
                    return;

                ushort source = Source[index];
                Destination[index] = source;
                if (source <= 32u || source > 126u)
                    return;

                float intensity = Sanitize01(Intensity01, 0f);
                if (intensity < 0.9f && IsCriticalReadable(index, source, ReadabilityPrefixChars, ReadabilityDigitBudget, Source))
                    return;

                uint mixed = SectorHash ^ SimulationFrame ^ ((uint)index * 0x9E3779B9u) ^ ((uint)source << 16);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(NonZeroRandomSeed(mixed));
                float quality = Sanitize01(GlobalQualityWeight01, 1f);
                float authorRate = math.saturate(TextScrambleRate01);
                float rate = math.saturate(intensity * authorRate * math.lerp(0.2f, 1f, Smooth01(quality)) * math.lerp(0.55f, 1f, Smooth01(intensity)));
                if (rng.NextFloat() > rate)
                    return;

                uint safeTableLength = (uint)math.max(1, TableLength);
                int tableIndex = (int)((rng.NextUInt() + TableOffset) % safeTableLength);
                byte replacement = GlitchTableBytes[tableIndex];
                Destination[index] = (ushort)(replacement >= 33 && replacement <= 126 ? replacement : (byte)'#');
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AsciiScramblerInPlaceJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public ushort* Buffer;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public byte* GlitchTableBytes;
            public int Length;
            public int TableLength;
            public int ReadabilityPrefixChars;
            public int ReadabilityDigitBudget;
            public float Intensity01;
            public float TextScrambleRate01;
            public float GlobalQualityWeight01;
            public uint SectorHash;
            public uint SimulationFrame;
            public uint TableOffset;

            public void Execute()
            {
                if (Buffer == null || GlitchTableBytes == null || Length <= 0)
                    return;

                float intensity = Sanitize01(Intensity01, 0f);
                float quality = Sanitize01(GlobalQualityWeight01, 1f);
                float authorRate = math.saturate(TextScrambleRate01);
                float rate = math.saturate(intensity * authorRate * math.lerp(0.2f, 1f, Smooth01(quality)) * math.lerp(0.55f, 1f, Smooth01(intensity)));
                uint safeTableLength = (uint)math.max(1, TableLength);
                int previousDigits = 0;
                for (int index = 0; index < Length; index++)
                {
                    ushort source = Buffer[index];
                    bool digit = source >= '0' && source <= '9';
                    bool critical = index < ReadabilityPrefixChars || (digit && previousDigits < ReadabilityDigitBudget);
                    if (digit)
                        previousDigits++;

                    if (source <= 32u || source > 126u || (intensity < 0.9f && critical))
                        continue;

                    uint mixed = SectorHash ^ SimulationFrame ^ ((uint)index * 0x9E3779B9u) ^ ((uint)source << 16);
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(NonZeroRandomSeed(mixed));
                    if (rng.NextFloat() > rate)
                        continue;

                    int tableIndex = (int)((rng.NextUInt() + TableOffset) % safeTableLength);
                    byte replacement = GlitchTableBytes[tableIndex];
                    Buffer[index] = (ushort)(replacement >= 33 && replacement <= 126 ? replacement : (byte)'#');
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HolographicMatrixShatterJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchStateDTO* State;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchTuningDTO* Tuning;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchQuadTransformDTO* Quads;
            public int QuadCount;
            public uint Frame;

            public void Execute(int index)
            {
                if (State == null || Tuning == null || Quads == null || (uint)index >= (uint)QuadCount)
                    return;

                float intensity = Sanitize01(State->GlobalIntensity, 0f);
                float quality = Sanitize01(Tuning->GlobalQualityWeight, 1f);
                float heavyCurve = Smooth01(quality);
                ref GlitchQuadTransformDTO quad = ref UnsafeUtility.AsRef<GlitchQuadTransformDTO>(Quads + index);
                float4x4 baseMatrix = BuildMockQuadMatrixForIndex(index);
                float effective = intensity * math.saturate(Tuning->MatrixShatterStrength) * heavyCurve;
                if (effective <= 0.00001f)
                {
                    quad.Matrix = baseMatrix;
                    quad.UVRect = new float4(0f, 0f, 1f, 1f);
                    quad.GlitchIntensity = 0f;
                    return;
                }

                float updateProbability = math.lerp(0.05f, 1f, heavyCurve * heavyCurve);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(NonZeroRandomSeed(Frame ^ (uint)index * 2654435761u ^ math.asuint(State->Seed)));
                if (rng.NextFloat() > updateProbability)
                    return;

                float n0 = rng.NextFloat(-1f, 1f);
                float n1 = rng.NextFloat(-1f, 1f);
                float angle = n0 * effective * 0.09f;
                float c = 1f - angle * angle * 0.5f;
                float s = angle;
                float3 xAxis = baseMatrix.c0.xyz;
                float3 yAxis = baseMatrix.c1.xyz;
                quad.Matrix = baseMatrix;
                quad.Matrix.c0 = new float4(xAxis * c + yAxis * s, 0f);
                quad.Matrix.c1 = new float4(yAxis * c - xAxis * s, 0f);
                quad.Matrix.c3 = baseMatrix.c3 + new float4(new float3(n0, n1, n0 * n1) * effective * 0.009f, 0f);
                quad.UVRect = new float4(new float2(n1, n0) * effective * 0.018f, 1f, 1f);
                quad.GlitchIntensity = intensity;
                if (!AllFinite(quad.Matrix.c0) || !AllFinite(quad.Matrix.c1) || !AllFinite(quad.Matrix.c3) || !AllFinite(quad.UVRect))
                {
                    quad.Matrix = baseMatrix;
                    quad.UVRect = new float4(0f, 0f, 1f, 1f);
                    quad.GlitchIntensity = 0f;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RadarGhostInjectionJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchStateDTO* State;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchTuningDTO* Tuning;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public RadarBlipDTO* RadarBlips;
            public int RadarBlipCount;
            public uint Frame;

            public void Execute(int index)
            {
                if (State == null || Tuning == null || RadarBlips == null || (uint)index >= (uint)RadarBlipCount)
                    return;

                float intensity = Sanitize01(State->GlobalIntensity, 0f);
                float quality = Sanitize01(Tuning->GlobalQualityWeight, 1f);
                float surge = SmoothStep(0.45f, 0.65f, intensity);
                float ghostBudget = math.saturate(math.lerp(0.2f, 1f, Smooth01(quality))) * math.max(0f, Tuning->GhostBlipCount) * surge;
                float alpha = math.saturate(ghostBudget - index);
                Unity.Mathematics.Random rng = new Unity.Mathematics.Random(NonZeroRandomSeed(Frame ^ math.asuint(State->Seed) ^ ((uint)index * 374761393u)));
                float angle = rng.NextFloat(0f, 6.2831853f);
                float radius = 0.018f + rng.NextFloat(0f, 0.075f);
                float2 local = new float2(math.cos(angle), math.sin(angle)) * radius;
                RadarBlips[index] = new RadarBlipDTO
                {
                    LocalPositionIntensity = new float4(local.x, local.y, 0f, alpha * intensity),
                    ColorSizeAgeFlags = new float4(0.85f, 0.08f + alpha * 0.42f, 0.06f, 0x53484E)
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SynthPitchBendJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchStateDTO* State;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchTuningDTO* Tuning;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchSynthParametersDTO* SynthParameters;
            public int SynthCount;
            public uint Frame;

            public void Execute(int index)
            {
                if (State == null || Tuning == null || SynthParameters == null || (uint)index >= (uint)SynthCount)
                    return;

                float intensity = Sanitize01(State->GlobalIntensity, 0f);
                float quality = Sanitize01(Tuning->GlobalQualityWeight, 1f);
                float bend = intensity * math.lerp(0.25f, 1f, Smooth01(quality));
                float baseFrequency = 180f + index * 35f;
                float baseGrain = 0.045f + index * 0.0025f;
                float n = noise.snoise(new float2((Frame + index * 19u) * 0.017f, math.asuint(State->Seed) * 0.000001f));
                float pitchScalar = math.lerp(1f, math.clamp(0.58f + n * 0.28f, 0.38f, 1.24f), bend);
                SynthParameters[index] = new GlitchSynthParametersDTO
                {
                    BaseFrequency = math.max(20f, baseFrequency * pitchScalar),
                    ModulationIndex = math.lerp(0.25f, 0.9f, bend),
                    GrainSize = math.max(0.005f, baseGrain * math.lerp(1f, 1.0f + math.abs(n) * 1.6f, bend)),
                    PressureScalar = intensity
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct TelemetryWriteJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchStateDTO* State;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public GlitchTuningDTO* Tuning;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockDepthSignal* Depth;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockModuleBreachSignal* Breach;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public MockTextSpan* TextSpan;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public RadarBlipDTO* RadarBlips;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public DiegeticGlitchTelemetryEntry* Telemetry;
            [NativeDisableUnsafePtrRestriction] [NoAlias] public uint* Cursor;
            public uint TableHash;
            public float LastComputeTimeMs;
            public uint Frame;

            public void Execute()
            {
                if (State == null || Tuning == null || Depth == null || Breach == null || TextSpan == null || Telemetry == null || Cursor == null)
                    return;

                uint cursor = *Cursor % TelemetryFrameCount;
                float intensity = Sanitize01(State->GlobalIntensity, 0f);
                float quality = Sanitize01(Tuning->GlobalQualityWeight, 1f);
                uint ghostCount = 0u;
                int budget = (int)math.min(RadarBlipCapacity, math.max(0f, Tuning->GhostBlipCount));
                for (int i = 0; i < budget; i++)
                {
                    if (RadarBlips != null && RadarBlips[i].LocalPositionIntensity.w > 0.001f)
                        ghostCount++;
                }

                uint flags = 0u;
                if (!math.isfinite(intensity) || !math.isfinite(quality) || !math.isfinite(Depth->DepthMeters))
                    flags |= FaultNonFinite;

                if (LastComputeTimeMs > 0.1f)
                    flags |= FaultOverBudget;

                uint stateHash = Hash(Frame ^ math.asuint(State->Seed) ^ TableHash ^ Breach->BreachedMask0);
                Telemetry[cursor] = new DiegeticGlitchTelemetryEntry
                {
                    FrameIndex = Frame,
                    StateHash = stateHash,
                    Flags = flags,
                    ScrambledCharacters = EstimateScrambledCharacters(TextSpan, intensity),
                    CurrentGlitchIntensity = intensity,
                    GlobalQualityWeight = quality,
                    ComputeTimeMs = math.isfinite(LastComputeTimeMs) ? LastComputeTimeMs : 0f,
                    DepthMeters = math.isfinite(Depth->DepthMeters) ? Depth->DepthMeters : 0f,
                    GhostBlipCount = ghostCount,
                    TextSpanLength = (uint)math.max(0, TextSpan->Length),
                    TableHash = TableHash,
                    ModuleMask = Breach->BreachedMask0,
                    MasterIntensity = Tuning->MasterIntensity,
                    MatrixStrength = Tuning->MatrixShatterStrength,
                    AudioPitchScalar = intensity,
                    Reserved0 = 0f
                };
                *Cursor = (cursor + 1u) % TelemetryFrameCount;
            }

            private static uint EstimateScrambledCharacters(MockTextSpan* span, float intensity)
            {
                int count = math.max(0, span->Length - span->ReadabilityPrefixChars);
                return (uint)math.round(count * math.saturate(intensity));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float denom = math.max(0.0001f, edge1 - edge0);
            return Smooth01((value - edge0) / denom);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllFinite(float4 value)
        {
            return math.all(math.isfinite(value));
        }
    }
}
