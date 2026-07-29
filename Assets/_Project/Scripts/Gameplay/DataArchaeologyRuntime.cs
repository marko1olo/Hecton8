using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Fixed 1024-bit discovery mask for scanner archaeology state.
    /// </summary>
    public static class DataArchaeologyDiscoveryBitMask
    {
        /// <summary>Total supported discovery slots.</summary>
        public const int MaxDiscoveryCount = 1024;

        /// <summary>Number of 64-bit words required for 1024 discovery bits.</summary>
        public const int WordCount = MaxDiscoveryCount / 64;

        /// <summary>Exact byte payload used by discovery flags in the save stream.</summary>
        public const int ByteCount = WordCount * sizeof(long);

        /// <summary>
        /// Ensures the caller-owned backing array is exactly sized for 1024 discovery bits.
        /// </summary>
        public static void EnsureCapacity(ref long[] words)
        {
            if (HasExpectedCapacity(words))
                return;

            words = new long[WordCount]; // COLD ALLOC: long[16] - 128-byte archaeology discovery save mask - owner: SaveData/DataArchaeologyRuntime
        }

        /// <summary>
        /// Returns true when the backing array contains the exact archaeology bit payload.
        /// </summary>
        public static bool HasExpectedCapacity(long[] words)
        {
            return words != null && words.Length == WordCount;
        }

        /// <summary>
        /// Clears every discovery bit.
        /// </summary>
        public static void Clear(long[] words)
        {
            if (words == null)
                return;

            int count = math.min(words.Length, WordCount);
            for (int i = 0; i < count; i++)
                words[i] = 0L;
        }

        /// <summary>
        /// Resolves a stable 0-1023 discovery bit from a uint content hash.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBitIndex(uint hash)
        {
            return (int)(hash & (MaxDiscoveryCount - 1u));
        }

        /// <summary>
        /// Checks one discovery bit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSet(long[] words, int bitIndex)
        {
            if (!HasExpectedCapacity(words) || (uint)bitIndex >= MaxDiscoveryCount)
                return false;

            int wordIndex = bitIndex >> 6;
            ulong bit = 1UL << (bitIndex & 63);
            return (((ulong)words[wordIndex]) & bit) != 0UL;
        }

        /// <summary>
        /// Sets one discovery bit and returns true only when the bit changed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySet(long[] words, int bitIndex)
        {
            if (!HasExpectedCapacity(words) || (uint)bitIndex >= MaxDiscoveryCount)
                return false;

            int wordIndex = bitIndex >> 6;
            ulong bit = 1UL << (bitIndex & 63);
            ulong word = (ulong)words[wordIndex];
            bool changed = (word & bit) == 0UL;
            words[wordIndex] = (long)(word | bit);
            return changed;
        }
    }

    /// <summary>
    /// Burst-compatible input packet for scanner frequency tuning.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DataArchaeologyFrequencyInput
    {
        [FieldOffset(0)] public uint ArtifactHash;
        [FieldOffset(4)] public float SignalPhase01;
        [FieldOffset(8)] public float NoisePhase01;
        [FieldOffset(12)] public float Threshold;
        [FieldOffset(16)] public float Interference01;
        [FieldOffset(20)] public float Battery01;
        [FieldOffset(24)] public float DeltaTime;
        [FieldOffset(28)] private uint _pad0;
    }

    /// <summary>
    /// Burst-compatible scanner tuning result.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DataArchaeologyFrequencyResult
    {
        [FieldOffset(0)] public float Signal;
        [FieldOffset(4)] public float Noise;
        [FieldOffset(8)] public float Difference;
        [FieldOffset(12)] public float Match01;
        [FieldOffset(16)] public float ProgressDeltaSeconds;
        [FieldOffset(20)] public float FeedbackPitchScale;
        [FieldOffset(24)] public float FeedbackFrequency01;
        [FieldOffset(28)] public byte Matched;
        [FieldOffset(29)] public byte Reserved0;
        [FieldOffset(30)] public ushort Reserved1;
    }

    /// <summary>
    /// Zero-allocation notification payload for HUD/PDA consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DataArchaeologyNotification
    {
        [FieldOffset(0)] public uint EntryHash;
        [FieldOffset(4)] public ushort ProgressPermille;
        [FieldOffset(6)] public byte Kind;
        [FieldOffset(7)] public byte Flags;
        [FieldOffset(8)] private ulong _pad0;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    internal struct DataArchaeologyTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint Frame;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public uint Hash;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public float3 Position;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public float Match01;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public uint Reserved1;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public ushort ProgressPermille;
        [System.Runtime.InteropServices.FieldOffset(30)]
        public byte Flags;
        [System.Runtime.InteropServices.FieldOffset(31)]
        public byte Reserved0;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad31;
    }

    /// <summary>
    /// Burst math for scanner signal/noise matching.
    /// </summary>
    public static class DataArchaeologyFrequencyKernel
    {
        private const uint LcgA = 1664525u;
        private const uint LcgC = 1013904223u;
        private const float UIntToUnit = 2.3283064e-10f;

        /// <summary>
        /// Evaluates a deterministic frequency match. The authored sine pair is rendered with a parabolic proxy.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DataArchaeologyFrequencyResult Evaluate(in DataArchaeologyFrequencyInput input)
        {
            uint seed = NextLcg(input.ArtifactHash ^ 0xA7C15EEDu);
            float stableOffset = (seed & 1023u) * 0.0009765625f;
            float signal = FastParabolicSineSigned(input.SignalPhase01 + stableOffset);
            float noise = FastParabolicSineSigned(input.NoisePhase01 + (input.Interference01 * 0.25f));
            float threshold = math.max(0.0001f, input.Threshold);
            float difference = math.abs(signal - noise);
            byte matched = (byte)math.select(0, 1, difference < threshold);
            float match01 = math.saturate(1f - (difference * math.rcp(threshold)));
            float progressScale = math.select(0f, 1f + (match01 * 0.5f), matched != 0);
            float batteryScale = 0.65f + (math.saturate(input.Battery01) * 0.35f);

            return new DataArchaeologyFrequencyResult
            {
                Signal = signal,
                Noise = noise,
                Difference = difference,
                Match01 = match01,
                ProgressDeltaSeconds = math.max(0f, input.DeltaTime) * progressScale * batteryScale,
                FeedbackPitchScale = 0.9f + (match01 * 0.35f),
                FeedbackFrequency01 = match01,
                Matched = matched
            };
        }

        /// <summary>
        /// One Jacobi relaxation step used by corrupted data recovery widgets.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 RecoverCorruptedDataJacobi4(float4 current, float4 left, float4 right, float4 source, float relaxation01)
        {
            float4 average = (left + right + source) * 0.33333334f;
            return math.lerp(current, average, math.saturate(relaxation01));
        }

        /// <summary>
        /// Branch-cheap UI color ramp for match/progress indication.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveProgressColorRgb(float match01)
        {
            return math.select(new float3(1f, 0.24f, 0.08f), new float3(0.08f, 0.86f, 1f), match01 >= 0.5f);
        }

        /// <summary>
        /// Deterministic LCG used for artifact interference seeding.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint NextLcg(uint state)
        {
            return (state * LcgA) + LcgC;
        }

        /// <summary>
        /// Converts deterministic LCG state to [0,1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LcgToUnit(uint state)
        {
            return state * UIntToUnit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastParabolicSineSigned(float phase01)
        {
            float wrapped = phase01 - math.floor(phase01);
            float triangle = 1f - math.abs((wrapped * 2f) - 1f);
            float signed = (triangle * 2f) - 1f;
            return signed * (1.5f - (0.5f * math.abs(signed)));
        }
    }

    /// <summary>
    /// Burst wrapper for scanner frequency tuning batch tests.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DataArchaeologyFrequencyTuningJob : IJob
    {
        public DataArchaeologyFrequencyInput Input;
        [NoAlias] public NativeSlice<DataArchaeologyFrequencyResult> Output;

        /// <inheritdoc />
        public void Execute()
        {
            if (Output.Length == 0)
                return;

            Output[0] = DataArchaeologyFrequencyKernel.Evaluate(in Input);
        }
    }

    /// <summary>
        /// Scanner-owned archaeology runtime: tuning state, discovery bits, fragment positions, persisted text reads, and hologram draw batches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DataArchaeologyRuntime : MonoBehaviour, ISaveable, IRenderable, IColdTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IDisposable
    {
        private int _signalPushDropCount;
        public const int MaxDiscoveryCount = DataArchaeologyDiscoveryBitMask.MaxDiscoveryCount;
        public const int DiscoveryWordCount = DataArchaeologyDiscoveryBitMask.WordCount;
        public const int DiscoveryByteCount = DataArchaeologyDiscoveryBitMask.ByteCount;
        public const int MaxPartialScanCount = 256;
        public const int NotificationCapacity = 32;
        public const int HologramInstanceCapacity = 64;
        public const int TelemetryCapacity = 300;

        private const int MmfHeaderBytes = 16;
        private const int MmfFragmentRecordBytes = 16;
        private const int MmfPartialRecordBytes = 8;
        private const float MmfPartialFlushCadenceSeconds = 4f;
        private const float MmfUrgentFlushDelaySeconds = 0.25f;
        private const float MmfFailureRetrySeconds = 8f;
        private const int MmfFileStreamBufferBytes = 4096;
        private const uint MmfMagic = 0x41443848u; // H8DA
        private const uint MmfVersion = 1u;
        private const byte NotificationKindDiscovery = 1;
        private const byte NotificationKindProgress = 2;
        private const byte TelemetryFlagMatched = 1 << 0;
        private const byte TelemetryFlagCompleted = 1 << 1;
        private const byte ScanStateUnscanned = 0;
        private const byte ScanStateScanning = 1;
        private const byte ScanStateScanned = 2;
        private const byte ToolAcousticStateScanning = 1;
        private const string DiscoveryUnlockedFallbackMessage = "PDA ARCHIVE ENTRY UNLOCKED";
        private const string DiscoveryUnlockedTitlePrefix = "PDA ARCHIVE // ";
        private const int DiscoveryNotificationCharCapacity = 160;
        private const int ScannerShaderPointCapacity = 4;
        private static readonly int _HectonScannerPointsId = Shader.PropertyToID("_HectonScannerPoints");
        private static readonly int _HectonScannerPointCountId = Shader.PropertyToID("_HectonScannerPointCount");
        private static readonly uint _scannerToolHash = Hecton.Localization.LocHash.ComputeAscii("tool.scanner");

        [Header("Data Archaeology")]
        [Tooltip("Signal/noise difference allowed for a successful archaeology scan tick.")]
        [SerializeField, Range(0.02f, 0.5f)] private float tuningThreshold = 0.14f;

        [Tooltip("Environmental electromagnetic interference scalar added to the noise wave.")]
        [SerializeField, Range(0f, 1f)] private float signalInterference01;

        [Tooltip("Fallback wireframe mesh used when a completed fragment has no MeshFilter.")]
        [SerializeField] private Mesh reconstructionMesh;

        [Tooltip("Required authored instanced wireframe material. Runtime material generation is forbidden.")]
        [SerializeField] private Material reconstructionMaterial;

        [Tooltip("PDA encyclopedia MMF index path. Text loads only when TryLoadLoreTextOnRead is called.")]
        [SerializeField] private string loreIndexPath;

        [Tooltip("PDA encyclopedia MMF payload path. Text loads only when TryLoadLoreTextOnRead is called.")]
        [SerializeField] private string lorePayloadPath;

        [Tooltip("When enabled, discovered fragment positions and partial scans are mirrored to a fixed binary sidecar.")]
        [SerializeField] private bool enableMmfPersistence = true;

        [Tooltip("Fixed binary sidecar filename under Application.persistentDataPath.")]
        [SerializeField] private string mmfFileName = "data_archaeology.mmf";

        private readonly long[] _discoveryWords = new long[DiscoveryWordCount]; // COLD ALLOC: long[16] - active 128-byte discovery bit mask - owner: DataArchaeologyRuntime
        private readonly uint[] _partialHashes = new uint[MaxPartialScanCount]; // COLD ALLOC: uint[256] - partial scan hash slots - owner: DataArchaeologyRuntime
        private readonly ushort[] _partialProgressPermille = new ushort[MaxPartialScanCount]; // COLD ALLOC: ushort[256] - partial scan progress slots - owner: DataArchaeologyRuntime
        private readonly uint[] _fragmentHashes = new uint[MaxDiscoveryCount]; // COLD ALLOC: uint[1024] - persisted fragment hash mirror - owner: DataArchaeologyRuntime
        private readonly Vector3[] _fragmentPositionsMirror = new Vector3[MaxDiscoveryCount]; // COLD ALLOC: Vector3[1024] - persisted fragment position mirror - owner: DataArchaeologyRuntime
        private readonly Matrix4x4[] _hologramMatrices = new Matrix4x4[HologramInstanceCapacity]; // COLD ALLOC: Matrix4x4[64] - instanced hologram draw buffer - owner: DataArchaeologyRuntime
        private readonly Vector4[] _scannerShaderPoints = new Vector4[ScannerShaderPointCapacity]; // COLD ALLOC: Vector4[4] - scanner emissive mask shader point payload - owner: DataArchaeologyRuntime

        private readonly int[] _scanStateKeys = new int[MaxDiscoveryCount]; // COLD ALLOC: int[1024] - fixed archaeology scan-state keys - owner: DataArchaeologyRuntime
        private readonly byte[] _scanStateValues = new byte[MaxDiscoveryCount]; // COLD ALLOC: byte[1024] - fixed archaeology scan-state values - owner: DataArchaeologyRuntime
        private VaultGenerationHandle<ulong> _unlockedLoreWordsHandle;
        private VaultGenerationHandle<DataArchaeologyNotification> _notificationsHandle;
        private VaultGenerationHandle<DataArchaeologyTelemetryEntry> _telemetryRingHandle;
        private LoreMmfEncyclopedia _loreMmf;
        private LoreMmfLoadStatus _loreMmfLastOpenStatus = LoreMmfLoadStatus.NotOpen;
        private Mesh _resolvedReconstructionMesh;
        private IDataVault _dataVault;
        private int _partialCount;
        private int _fragmentCount;
        private int _scanStateCount;
        private int _hologramCount;
        private int _notificationRead;
        private int _notificationWrite;
        private int _notificationCount;
        private int _telemetryCursor;
        private float _tuningPhase01;
        private float _manualTune01 = 0.5f;
        private float _nextSensoryFeedbackTime;
        private float3 _lastScannerShaderPoint = new float3(float.NaN);
        private float _lastScannerShaderProgress = -1f;
        private bool _registeredSave;
        private bool _registeredRenderable;
        private bool _registeredColdTick;
        private bool _registeredOriginShift;

        /// <summary>
        /// Latched once the authored reconstruction MATERIAL gap is reported, so the IRenderable lane can
        /// never be re-entered and the assert can never fire a second time.
        /// </summary>
        /// <remarks>
        /// Keyed on the material, not the mesh. A missing instanced material is unrecoverable - this project
        /// forbids runtime material synthesis - while <c>reconstructionMesh</c> is a documented fallback that
        /// <see cref="RegisterHologram"/> can still satisfy from <c>ScannableFragment.CachedSharedMesh</c>.
        /// </remarks>
        private bool _reconstructionSetupPermanentlyFailed;

        /// <summary>
        /// Set once the missing authored fallback mesh has been announced, so the report is one line per
        /// session instead of one per Awake/OnEnable of every pooled scanner instance.
        /// </summary>
        private bool _missingReconstructionMeshAnnounced;
        private bool _mmfDirty;
        private float _nextMmfFlushTime = float.PositiveInfinity;
        private bool _disposed;
        private bool _loreMmfOpenAttempted;
        private bool _hotSwapListenerRegistered;
        private ILoreUnlockSink _cachedLoreDatabase;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;

        /// <inheritdoc />
        public int SavePriority => 206;

        /// <inheritdoc />
        public int LoadPriority => 206;

        /// <summary>
        /// Resolves scanner range from base range and battery level.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveScannerRange(float baseRange, float battery01)
        {
            return math.max(0f, baseRange) * (1f + math.saturate(battery01));
        }

        /// <summary>
        /// Sets manual tune phase supplied by future scanner/PDA controls.
        /// </summary>
        public void SetManualTune01(float tune01)
        {
            _manualTune01 = math.saturate(tune01);
        }

        /// <summary>
        /// Evaluates one focused archaeology scan tick for a fragment.
        /// </summary>
        public bool TryEvaluateFocusedScan(
            ScannableFragment fragment,
            float3 fragmentPosition,
            float heldDeltaTime,
            float battery01,
            out DataArchaeologyFrequencyResult result)
        {
            result = default;
            if (fragment == null || heldDeltaTime <= 0f)
                return false;

            uint hash = fragment.DiscoveryHash;
            if (hash == 0u)
                return false;

            if (!math.all(math.isfinite(new float4(fragmentPosition, heldDeltaTime))))
            {
                RecordTelemetry(hash, 0, float3.zero, 0f, 0);
                DumpTelemetryCold();
                return false;
            }

            SetScanState(hash, ScanStateScanning);
            _tuningPhase01 = Wrap01(_tuningPhase01 + (heldDeltaTime * (0.18f + (math.saturate(battery01) * 0.08f))));
            uint seed = BuildAupArtifactSeed(hash, fragmentPosition);
            float signalPhase = DataArchaeologyFrequencyKernel.LcgToUnit(seed);
            float noisePhase = Wrap01(_manualTune01 + _tuningPhase01 + DataArchaeologyFrequencyKernel.LcgToUnit(DataArchaeologyFrequencyKernel.NextLcg(seed)) * 0.125f);

            DataArchaeologyFrequencyInput input = new DataArchaeologyFrequencyInput
            {
                ArtifactHash = hash,
                SignalPhase01 = signalPhase,
                NoisePhase01 = noisePhase,
                Threshold = tuningThreshold,
                Interference01 = signalInterference01,
                Battery01 = battery01,
                DeltaTime = heldDeltaTime
            };

            result = DataArchaeologyFrequencyKernel.Evaluate(in input);
            EmitSensoryFeedback(in result);
            PublishToolAcoustic(hash, fragment.ProgressNormalized, result.FeedbackPitchScale, math.saturate(result.Match01));
            PublishScannerShaderPoint(fragmentPosition, fragment.ProgressNormalized);
            byte flags = result.Matched != 0 ? TelemetryFlagMatched : (byte)0;
            RecordTelemetry(hash, flags, fragmentPosition, result.Match01, ToPermille(fragment.ProgressNormalized));
            return true;
        }

        /// <summary>
        /// Registers a probe-scanned target hash and seeds its zero-GC scan state.
        /// </summary>
        public bool RegisterProbeTarget(uint entityHash, float3 hitPosition)
        {
            if (entityHash == 0u)
                return false;

            if (TryGetScanState(entityHash, out byte state) && state == ScanStateScanned)
                return false;

            if (state == ScanStateUnscanned)
                SetScanState(entityHash, ScanStateScanning);

            RegisterFragmentPosition(entityHash, hitPosition);
            PublishScannerShaderPoint(hitPosition, 0f);
            return true;
        }

        /// <summary>
        /// Updates a held probe target scan using caller-owned local progress seconds.
        /// </summary>
        public bool UpdateProbeTargetProgress(uint entityHash, float3 hitPosition, float progressSeconds, out bool completed)
        {
            completed = false;
            if (entityHash == 0u)
                return false;

            float progress01 = math.saturate(progressSeconds);
            if (!math.all(math.isfinite(new float4(hitPosition, progress01))))
            {
                RecordTelemetry(entityHash, 0, float3.zero, 0f, ToPermille(progress01));
                DumpTelemetryCold();
                return false;
            }

            if (TryGetScanState(entityHash, out byte previousState) && previousState == ScanStateScanned)
                return false;

            if (progressSeconds > 1f)
            {
                RemovePartial(entityHash);
                SetScanState(entityHash, ScanStateScanned);
                SetNativeLoreBit(DataArchaeologyDiscoveryBitMask.ResolveBitIndex(entityHash));
                RegisterFragmentPosition(entityHash, hitPosition);
                ILoreUnlockSink loreDatabase = _cachedLoreDatabase;
                if (loreDatabase != null)
                    loreDatabase.TryUnlockByHash(entityHash);

                PublishCompletionSignals(entityHash, hitPosition);
                EnqueueNotification(entityHash, 1000, NotificationKindDiscovery, 0);
                RecordTelemetry(entityHash, TelemetryFlagCompleted, hitPosition, 1f, 1000);
                MarkMmfDirty(true);
                completed = true;
                return true;
            }

            SetScanState(entityHash, ScanStateScanning);
            UpsertPartial(entityHash, ToPermille(progress01));
            PublishScannerShaderPoint(hitPosition, progress01);
            PublishToolAcoustic(entityHash, progress01, 0.8f + (progress01 * 0.42f), 0.2f + (progress01 * 0.8f));
            RecordTelemetry(entityHash, TelemetryFlagMatched, hitPosition, progress01, ToPermille(progress01));
            return true;
        }

        /// <summary>
        /// Restores persisted target progress for a hash-only scan target.
        /// </summary>
        public bool TryGetTargetProgress01(uint entityHash, out float progress01)
        {
            progress01 = 0f;
            if (entityHash == 0u || !TryFindPartial(entityHash, out int index))
                return false;

            progress01 = _partialProgressPermille[index] * 0.001f;
            return true;
        }

        /// <summary>
        /// Applies persisted partial progress to a fragment when scanner focus reacquires it.
        /// </summary>
        public void TryApplyPersistedProgress(ScannableFragment fragment)
        {
            if (fragment == null || fragment.IsCompleted)
                return;

            uint hash = fragment.DiscoveryHash;
            if (hash == 0u || !TryFindPartial(hash, out int index))
                return;

            fragment.RestoreProgressNormalized(_partialProgressPermille[index] * 0.001f);
        }

        /// <summary>
        /// Records fragment scan progress in fixed arrays for binary/MMF persistence.
        /// </summary>
        public void RecordPartialProgress(ScannableFragment fragment)
        {
            if (fragment == null)
                return;

            uint hash = fragment.DiscoveryHash;
            if (hash == 0u)
                return;

            ushort progress = ToPermille(fragment.ProgressNormalized);
            if (progress >= 1000)
            {
                SetScanState(hash, ScanStateScanned);
                RemovePartial(hash);
                return;
            }

            SetScanState(hash, ScanStateScanning);
            if (UpsertPartial(hash, progress))
                EnqueueNotification(hash, progress, NotificationKindProgress, 0);
        }

        /// <summary>
        /// Records completed archaeology fragment discovery and queues hologram reconstruction.
        /// </summary>
        public void NotifyFragmentCompleted(ScannableFragment fragment, float3 fragmentPosition)
        {
            if (fragment == null)
                return;

            uint hash = fragment.DiscoveryHash;
            if (hash == 0u)
                return;

            RemovePartial(hash);
            int bitIndex = DataArchaeologyDiscoveryBitMask.ResolveBitIndex(hash);
            bool changed = DataArchaeologyDiscoveryBitMask.TrySet(_discoveryWords, bitIndex);
            SetNativeLoreBit(bitIndex);
            SetScanState(hash, ScanStateScanned);
            bool hasKnownPosition = FindFragmentMirror(hash) >= 0;
            if (!changed && hasKnownPosition)
                return;

            if (changed && _cachedLoreDatabase != null)
                _cachedLoreDatabase.TryUnlockByHash(hash);

            RegisterFragmentPosition(hash, fragmentPosition);
            RegisterHologram(fragment, fragmentPosition);
            ScanEvents.TryRaiseEntryDiscovered(hash, 0u, 0u, 0u, ScanEntryKind.Scannable);
            PublishCompletionSignals(hash, fragmentPosition);
            EnqueueNotification(hash, 1000, NotificationKindDiscovery, 0);
            RecordTelemetry(hash, TelemetryFlagCompleted, fragmentPosition, 1f, 1000);
            MarkMmfDirty(true);
        }

        /// <summary>
        /// Resets partial scan progress to the previous 25% milestone after an interruption.
        /// </summary>
        public void InterruptScan(uint hash)
        {
            if (hash == 0u || !TryFindPartial(hash, out int index))
                return;

            ushort current = _partialProgressPermille[index];
            ushort milestone = (ushort)((current / 250) * 250);
            _partialProgressPermille[index] = milestone;
            MarkMmfDirty(true);
        }

        /// <summary>
        /// Attempts to dequeue one fixed-ring HUD notification.
        /// </summary>
        public bool TryDequeueNotification(out DataArchaeologyNotification notification)
        {
            notification = default;
            if (!TryOpenNotifications(out NativeArray<DataArchaeologyNotification> notifications) || _notificationCount <= 0)
                return false;

            notification = notifications[_notificationRead];
            _notificationRead = (_notificationRead + 1) & (NotificationCapacity - 1);
            _notificationCount--;
            return true;
        }

        /// <summary>
        /// Loads PDA text from the MMF only when the caller explicitly requests a read.
        /// </summary>
        public LoreMmfLoadStatus TryLoadLoreTextOnRead(uint hash, char[] destination, out int charsWritten)
        {
            charsWritten = 0;
            if (hash == 0u)
                return LoreMmfLoadStatus.MissingEntry;

            if (destination == null || destination.Length == 0)
                return LoreMmfLoadStatus.DestinationTooSmall;

            if (_loreMmfOpenAttempted && _loreMmfLastOpenStatus != LoreMmfLoadStatus.Ok)
                return _loreMmfLastOpenStatus;

            if (_loreMmf == null)
                _loreMmf = new LoreMmfEncyclopedia(); // COLD ALLOC: LoreMmfEncyclopedia[1] - read-on-demand PDA MMF view - owner: DataArchaeologyRuntime

            if (!_loreMmf.IsOpen)
            {
                _loreMmfOpenAttempted = true;
                _loreMmfLastOpenStatus = _loreMmf.TryOpen(loreIndexPath, lorePayloadPath);
                if (_loreMmfLastOpenStatus != LoreMmfLoadStatus.Ok)
                {
                    _loreMmf.Dispose();
                    _loreMmf = null;
                    return _loreMmfLastOpenStatus;
                }
            }

            return _loreMmf.TryLoadEntryUtf16(hash, destination, out charsWritten);
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            EnsureNativeState();
            SyncNativeLoreToManaged();
            DataArchaeologyDiscoveryBitMask.EnsureCapacity(ref data.dataArchaeologyDiscoveryBitWords);
            for (int i = 0; i < DiscoveryWordCount; i++)
                data.dataArchaeologyDiscoveryBitWords[i] = _discoveryWords[i];

            EnsurePartialSaveArrays(data);
            int safeCount = math.min(_partialCount, MaxPartialScanCount);
            data.dataArchaeologyPartialScanCount = safeCount;
            for (int i = 0; i < safeCount; i++)
            {
                data.dataArchaeologyPartialScanHashes[i] = _partialHashes[i];
                data.dataArchaeologyPartialScanProgressPermille[i] = _partialProgressPermille[i];
            }

            for (int i = safeCount; i < MaxPartialScanCount; i++)
            {
                data.dataArchaeologyPartialScanHashes[i] = 0u;
                data.dataArchaeologyPartialScanProgressPermille[i] = 0;
            }

            PopulateScanStateSaveData(data);
            PersistMmfCold();
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            EnsureNativeState();
            DataArchaeologyDiscoveryBitMask.Clear(_discoveryWords);
            _partialCount = 0;
            ClearNativeLoreWords();
            ClearScanStates();

            if (data != null)
            {
                DataArchaeologyDiscoveryBitMask.EnsureCapacity(ref data.dataArchaeologyDiscoveryBitWords);
                for (int i = 0; i < DiscoveryWordCount; i++)
                    _discoveryWords[i] = data.dataArchaeologyDiscoveryBitWords[i];
                SyncManagedLoreToNative();

                EnsurePartialSaveArrays(data);
                int safeCount = math.clamp(
                    data.dataArchaeologyPartialScanCount,
                    0,
                    math.min(MaxPartialScanCount, math.min(data.dataArchaeologyPartialScanHashes.Length, data.dataArchaeologyPartialScanProgressPermille.Length)));

                for (int i = 0; i < safeCount; i++)
                {
                    uint hash = data.dataArchaeologyPartialScanHashes[i];
                    ushort progress = data.dataArchaeologyPartialScanProgressPermille[i];
                    if (hash == 0u || progress >= 1000)
                        continue;

                    InsertOrUpgradePartialCold(hash, progress);
                    SetScanState(hash, ScanStateScanning);
                }

                LoadScanStateSaveData(data);
                RemoveNonScanningPartials(markDirty: false);
            }

            TryLoadMmfCold(data != null);
        }

        /// <inheritdoc />
        public void Render(float deltaTime)
        {
            if (_reconstructionSetupPermanentlyFailed || _hologramCount <= 0)
                return;

            if (!AreReconstructionResourcesReady())
                return;

            Mesh mesh = _resolvedReconstructionMesh != null ? _resolvedReconstructionMesh : reconstructionMesh;
            Material material = reconstructionMaterial;
            if (mesh == null || material == null)
                return;

            UnityEngine.Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                _hologramMatrices,
                _hologramCount,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                null,
                LightProbeUsage.Off);
        }

        /// <inheritdoc />
        public void ColdTick()
        {
            if (_mmfDirty && (float)SystemDispatcher.CurrentUnscaledTimeSeconds >= _nextMmfFlushTime)
                PersistMmfCold();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            PersistMmfCold();
            UnregisterRuntime();
            _disposed = true;

            _loreMmf?.Dispose();
            _loreMmf = null;
            _loreMmfLastOpenStatus = LoreMmfLoadStatus.NotOpen;
            _loreMmfOpenAttempted = false;

            ClearFragmentPositions();
            ClearScanStates();

            ReleaseVaultHandles(_dataVault);
            _dataVault = null;

        }

        private void Awake()
        {
            CacheRegistryServicesCold();
            EnsureNativeState();
            EnsureReconstructionResources();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            EnsureNativeState();

            // Registration runs BEFORE the authored-asset check on purpose. EnsureReconstructionResources can
            // still throw once through its asserts, and this tail is what the throw used to destroy: the
            // component silently stopped being an ISaveable, IColdTickable and IOriginShiftListener on every
            // pooled activation. Nothing below depends on the reconstruction mesh or material - Render
            // null-guards both - so the reorder costs nothing and removes the orphaning entirely.
            TryRegisterHotSwapListener();
            RegisterOriginShiftListener();
            TryRegisterRuntime();
            TryLoadMmfCold(requireExistingSaveState: false);

            EnsureReconstructionResources();
        }

        private void Start()
        {
            TryRegisterRuntime();
        }

        private void OnDisable()
        {
            PersistMmfCold();
            TryUnregisterHotSwapListener();
            UnregisterRuntime();
        }

        private void UnregisterRuntime()
        {
            UnregisterOriginShiftListener();
            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.UI);
                _registeredColdTick = false;
            }

            if (_registeredSave || _registeredSaveService != null)
            {
                ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
                if (saveService != null)
                    saveService.Unregister(this);

                _registeredSaveService = null;
                _registeredSave = false;
            }
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            Dispose();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.LoreDatabaseRuntime)
            {
                _cachedLoreDatabase = currentService as ILoreUnlockSink;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseVaultHandles(previousService as IDataVault ?? _dataVault);
                _dataVault = currentService as IDataVault;
                EnsureNativeState();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                if (_registeredSave || _registeredSaveService != null)
                {
                    ISaveService previousSave = _registeredSaveService != null ? _registeredSaveService : previousService as ISaveService ?? _saveService;
                    if (previousSave != null)
                        previousSave.Unregister(this);

                    _registeredSaveService = null;
                    _registeredSave = false;
                }

                _saveService = currentService as ISaveService;
                TryRegisterRuntime();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedLoreDatabase = GlobalRegistry.LoreUnlockSink;
            _dataVault = GlobalRegistry.DataVault;
            _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterRuntime()
        {
            if (!Application.isPlaying)
                return;

            // The latch must be honoured HERE, not only at the failure site. This method is the re-arm path:
            // clearing _registeredRenderable without refusing here would let OnEnable/Start/hot-swap push the
            // component back into the render lane it was just removed from.
            if (!_registeredRenderable && !_reconstructionSetupPermanentlyFailed)
                _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);

            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.UI);

            if (!_registeredSave)
            {
                ISaveService saveService = _saveService;
                if (!IsSaveServiceUsable(saveService))
                {
                    saveService = GlobalRegistry.Save;
                    _saveService = saveService;
                }

                if (!IsSaveServiceUsable(saveService))
                    return;

                saveService.Register(this);
                _registeredSaveService = saveService;
                _saveService = saveService;
                _registeredSave = true;
            }
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            float3 runtimeDelta = -(float3)shiftOffset;
            RebaseRuntimePositions(runtimeDelta);
        }

        private void RegisterOriginShiftListener()
        {
            if (_registeredOriginShift)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = true;
        }

        private void UnregisterOriginShiftListener()
        {
            if (!_registeredOriginShift)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShift = false;
        }

        private void EnsureNativeState()
        {
            bool loreHandleWasCreated = IsHandleCreated(in _unlockedLoreWordsHandle);
            if (TryOpenOrAcquireUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords) && !loreHandleWasCreated)
                SyncManagedLoreToNative(unlockedLoreWords);

            TryOpenOrAcquireNotifications(out _);
            TryOpenOrAcquireTelemetryRing(out _);
        }

        private bool TryOpenOrAcquireUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords)
        {
            return TryOpenOrAcquireVaultView(
                ref _unlockedLoreWordsHandle,
                BufferID.DataArchaeologyUnlockedLoreWords,
                DiscoveryWordCount,
                NativeArrayOptions.ClearMemory,
                out unlockedLoreWords);
        }

        private bool TryOpenOrAcquireNotifications(out NativeArray<DataArchaeologyNotification> notifications)
        {
            return TryOpenOrAcquireVaultView(
                ref _notificationsHandle,
                BufferID.DataArchaeologyNotifications,
                NotificationCapacity,
                NativeArrayOptions.ClearMemory,
                out notifications);
        }

        private bool TryOpenOrAcquireTelemetryRing(out NativeArray<DataArchaeologyTelemetryEntry> telemetryRing)
        {
            return TryOpenOrAcquireVaultView(
                ref _telemetryRingHandle,
                BufferID.DataArchaeologyTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory,
                out telemetryRing);
        }

        private bool TryOpenUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords)
        {
            return TryOpenVaultView(_dataVault, in _unlockedLoreWordsHandle, DiscoveryWordCount, out unlockedLoreWords);
        }

        private bool TryOpenNotifications(out NativeArray<DataArchaeologyNotification> notifications)
        {
            return TryOpenVaultView(_dataVault, in _notificationsHandle, NotificationCapacity, out notifications);
        }

        private bool TryOpenTelemetryRing(out NativeArray<DataArchaeologyTelemetryEntry> telemetryRing)
        {
            return TryOpenVaultView(_dataVault, in _telemetryRingHandle, TelemetryCapacity, out telemetryRing);
        }

        private bool TryOpenOrAcquireVaultView<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (TryOpenVaultView(vault, in handle, requiredLength, out buffer))
                return true;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                TryOpenVaultView(vault, in existing, requiredLength, out buffer))
            {
                handle = existing;
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                handle = default;
                buffer = default;
                return false;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GameplayTools,
                options);

            if (!TryOpenVaultView(vault, in acquired, requiredLength, out buffer))
            {
                handle = default;
                buffer = default;
                return false;
            }

            handle = acquired;
            return true;
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
            {
                _unlockedLoreWordsHandle = default;
                _notificationsHandle = default;
                _telemetryRingHandle = default;
                return;
            }

            ReleaseVaultHandle(vault, ref _unlockedLoreWordsHandle);
            ReleaseVaultHandle(vault, ref _notificationsHandle);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (IsHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        /// <summary>
        /// Resolves the authored hologram mesh/material pair, or latches reconstruction off permanently.
        /// </summary>
        /// <remarks>
        /// Statement ORDER is the whole fix. <c>UnityEngine.Assertions.Assert</c> THROWS in this project -
        /// nothing under Assets ever sets <c>Assert.raiseExceptions = false</c> - so every statement after an
        /// assert that fires is unreachable. Two earlier repairs of this same defect class in
        /// HectonMarineSnowRenderer were wasted by placing the cleanup BELOW the assert.
        ///
        /// The measured damage was not in this method, it was in its CALLERS. Awake and OnEnable both
        /// call this; omega_route20.log:15824-15831 caught the throw escaping Awake during
        /// PlayerToolManager pool warmup. When OnEnable took the same throw, TryRegisterHotSwapListener,
        /// RegisterOriginShiftListener, TryRegisterRuntime and TryLoadMmfCold never ran, so the component
        /// silently stopped being an ISaveable, IColdTickable and IOriginShiftListener - archaeology
        /// discoveries stopped persisting and fragment positions stopped being rebased on floating-origin
        /// shifts. The scanner prefab is POOLED, so that loss repeated on every activation. OnEnable now
        /// calls this last, and the latch keeps the throw out of any caller tail on every later call.
        ///
        /// The three original asserts also hid each other: the mesh assert threw first, so the material and
        /// GPU-instancing asserts could never report. That is why no log has ever named the material state.
        /// </remarks>
        private void EnsureReconstructionResources()
        {
            if (_reconstructionSetupPermanentlyFailed)
                return;

            _resolvedReconstructionMesh = _resolvedReconstructionMesh != null ? _resolvedReconstructionMesh : reconstructionMesh;

            bool materialAuthored = reconstructionMaterial != null;
            bool instancingAuthored = materialAuthored && reconstructionMaterial.enableInstancing;
            if (materialAuthored && instancingAuthored)
            {
                // A missing fallback mesh is SURVIVABLE and must not latch the lane off. The serialized field
                // is documented as a fallback "used when a completed fragment has no MeshFilter";
                // RegisterHologram fills _resolvedReconstructionMesh from ScannableFragment.CachedSharedMesh
                // when it is null, and Render null-guards mesh and material before DrawMeshInstanced. Latching
                // here would kill that live fragment-supplied path to punish an unassigned fallback, and the
                // old fatal assert took the whole component's registration down with it.
                if (_resolvedReconstructionMesh == null && !_missingReconstructionMeshAnnounced)
                {
                    _missingReconstructionMeshAnnounced = true;
                    LogMissingReconstructionMeshFallback();
                }

                return;
            }

            // LEAVE THE RENDER LANE, LATCH, ANNOUNCE, AND RETURN. No assert.
            //
            // The comment this replaces claimed "the material gap is unrecoverable". Render disproves that
            // four hundred lines up: it reads `Material material = reconstructionMaterial;` then
            // `if (mesh == null || material == null) return;`, so a null material is survivable by
            // construction - the hologram simply does not draw. The latch above already leaves the render
            // lane and the log above already names the gap, so the asserts added exactly one thing:
            // destroying the caller.
            //
            // UnityEngine.Assertions.Assert THROWS in this project, and this component is instantiated
            // during a POOL WARMUP inside another component's OnEnable - PlayerToolManager.OnEnable ->
            // WarmRuntimePoolsIfNeeded -> WarmAssignedToolPoolsIfNeeded -> EnsurePoolWarmup ->
            // ObjectPoolManager.Warmup -> InstantiatePooled. The throw aborted PlayerToolManager.OnEnable,
            // which is precisely why the route probe's Tool row reported slotCount=4 with
            // IsToolAvailableInSlot false for EVERY slot. An unassigned cosmetic hologram material was
            // costing the player all four tools.
            //
            // Same shape as HectonVoxelEngine.EnsureVoxelBakeGhostMaterial, fixed in 585401145: an assert
            // guarding optional state, taking an unrelated system's entire initialisation with it. The
            // authoring gap is real - reconstructionMaterial is unassigned and wants an authored asset with
            // GPU instancing enabled - and LogMissingReconstructionMaterial says so without unwinding
            // anyone's OnEnable.
            DisableReconstructionAfterUnrecoverableSetupFailure();
            LogMissingReconstructionMaterial(materialAuthored);
        }

        /// <summary>
        /// Gives up on hologram reconstruction permanently and leaves the IRenderable lane.
        /// </summary>
        /// <remarks>
        /// Latch FIRST; the unregister alone is worse than doing nothing. Clearing
        /// <c>_registeredRenderable</c> re-arms <see cref="TryRegisterRuntime"/>, whose three live callers -
        /// OnEnable, Start and OnGlobalRegistryServiceReplaced - would push the component straight back into
        /// the lane it just left. That is the churn cycle that made the HectonMarineSnowRenderer assertion
        /// count RISE from 48 to 69 per headless run when its unregister landed without a latch.
        ///
        /// Scope is deliberately narrower than the marine-snow fix: <c>enabled = false</c> is NOT set, and the
        /// cold-tick, save and origin-shift registrations are left untouched. Those lanes own archaeology
        /// persistence; disabling them to silence a cosmetic hologram gap would trade a missing hologram for
        /// lost save data.
        /// </remarks>
        private void DisableReconstructionAfterUnrecoverableSetupFailure()
        {
            _reconstructionSetupPermanentlyFailed = true;
            _hologramCount = 0;

            if (_registeredRenderable)
            {
                GlobalRegistry.Renderables.Unregister(this);
                _registeredRenderable = false;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingReconstructionMeshFallback()
        {
            Hecton8.Core.H8Debug.LogError("DataArchaeologyRuntime: serialized field 'reconstructionMesh' is unassigned on the scanner tool prefab. Completed fragments without their own CachedSharedMesh will draw no hologram. Scan, discovery, save and origin-shift duties stay live. Fix by authoring a low-poly wireframe reconstruction mesh and assigning it in the inspector; runtime mesh synthesis is forbidden.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingReconstructionMaterial(bool materialAuthored)
        {
            if (!materialAuthored)
            {
                Hecton8.Core.H8Debug.LogError("DataArchaeologyRuntime: reconstructionMaterial is unassigned. Hologram reconstruction is latched off for this session. Runtime material synthesis is forbidden - assign an authored GPU-instanced wireframe material.");
                return;
            }

            Hecton8.Core.H8Debug.LogError("DataArchaeologyRuntime: reconstructionMaterial has Enable GPU Instancing off. Hologram reconstruction is latched off for this session. Enable instancing on the authored material asset.");
        }

        private bool AreReconstructionResourcesReady()
        {
            return (_resolvedReconstructionMesh != null || reconstructionMesh != null) &&
                   reconstructionMaterial != null &&
                   reconstructionMaterial.enableInstancing;
        }

        private bool TryGetScanState(uint hash, out byte state)
        {
            state = ScanStateUnscanned;
            if (hash == 0u)
                return false;

            int index = FindScanStateIndex(unchecked((int)hash));
            if (index < 0)
                return false;

            state = _scanStateValues[index];
            return true;
        }

        private void SetScanState(uint hash, byte state)
        {
            if (hash == 0u)
                return;

            int key = unchecked((int)hash);
            int index = FindScanStateIndex(key);
            if (state == ScanStateUnscanned)
            {
                if (index >= 0)
                    RemoveScanStateAt(index);
                return;
            }

            if (index >= 0)
            {
                _scanStateValues[index] = state;
                return;
            }

            if (_scanStateCount >= MaxDiscoveryCount)
                return;

            _scanStateKeys[_scanStateCount] = key;
            _scanStateValues[_scanStateCount] = state;
            _scanStateCount++;
        }

        private void SetNativeLoreBit(int bitIndex)
        {
            if (!TryOpenUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords) || (uint)bitIndex >= MaxDiscoveryCount)
                return;

            int word = bitIndex >> 6;
            int bit = bitIndex & 63;
            unlockedLoreWords[word] = unlockedLoreWords[word] | (1UL << bit);
            _discoveryWords[word] = (long)unlockedLoreWords[word];
        }

        private void SyncManagedLoreToNative()
        {
            if (!TryOpenOrAcquireUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords))
                return;

            SyncManagedLoreToNative(unlockedLoreWords);
        }

        private void SyncManagedLoreToNative(NativeArray<ulong> unlockedLoreWords)
        {
            for (int i = 0; i < DiscoveryWordCount; i++)
                unlockedLoreWords[i] = unchecked((ulong)_discoveryWords[i]);
        }

        private void SyncNativeLoreToManaged()
        {
            if (!TryOpenOrAcquireUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords))
                return;

            for (int i = 0; i < DiscoveryWordCount; i++)
                _discoveryWords[i] = unchecked((long)unlockedLoreWords[i]);
        }

        private void ClearNativeLoreWords()
        {
            if (!TryOpenOrAcquireUnlockedLoreWords(out NativeArray<ulong> unlockedLoreWords))
                return;

            for (int i = 0; i < DiscoveryWordCount; i++)
                unlockedLoreWords[i] = 0UL;
        }

        private void PublishCompletionSignals(uint hash, float3 position)
        {
            if (hash == 0u || !math.all(math.isfinite(new float4(position, 1f))))
                return;

            if (!TryResolveRuntimeAup(position, out AbsoluteUniversePosition aup))
                return;

            uint frame = SystemDispatcher.CurrentFrameId;
            H8AppliedLoreRuntime.TryRaisePacketUnlockedAt(
                hash,
                in aup,
                _scannerToolHash,
                0,
                (byte)ScanEntryKind.Scannable);
            SignalBus<ProgressionEventSignal>.TryPushTracked(new ProgressionEventSignal
            {
                PositionAup = aup,
                PoiHash = hash,
                QuestHash = hash,
                Frame = frame,
                Source = 2,
                Flags = 0
            }, ref _signalPushDropCount);
            SignalBus<BlueprintUnlockedSignal>.TryPushTracked(new BlueprintUnlockedSignal
            {
                EntityHash = hash,
                BlueprintHash = hash,
                SourceId = _scannerToolHash,
                Frame = frame,
                Category = 0,
                Flags = 0
            }, ref _signalPushDropCount);
            PublishDiscoveryHudNotification(hash);
        }

        private static void PublishDiscoveryHudNotification(uint hash)
        {
            Span<char> message = stackalloc char[DiscoveryNotificationCharCapacity];
            ReadOnlySpan<char> prefix = DiscoveryUnlockedTitlePrefix.AsSpan();
            if (prefix.TryCopyTo(message) &&
                H8AppliedLoreRuntime.TryWriteTitleUtf16(
                    hash,
                    H8AppliedLoreRuntime.DefaultLocaleHash,
                    message.Slice(prefix.Length),
                    out int titleLength) &&
                titleLength > 0)
            {
                NotificationEvents.TryPushInfo(message.Slice(0, prefix.Length + titleLength));
                return;
            }

            NotificationEvents.TryPushInfo(DiscoveryUnlockedFallbackMessage.AsSpan());
        }

        private static float ResolvePresentationQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
        }

        private void PublishScannerShaderPoint(float3 runtimePosition, float progress01)
        {
            if (!math.all(math.isfinite(new float4(runtimePosition, progress01))))
                return;

            float clampedProgress = math.saturate(progress01);
            float qualityCurve01 = math.smoothstep(0.08f, 1f, ResolvePresentationQualityWeight01());
            float shaderProgress = clampedProgress * qualityCurve01;
            if (math.all(math.isfinite(new float4(_lastScannerShaderPoint, _lastScannerShaderProgress))) &&
                math.lengthsq(runtimePosition - _lastScannerShaderPoint) <= 0.0001f &&
                math.abs(shaderProgress - _lastScannerShaderProgress) < 0.01f)
                return;

            if (!TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition shaderPointAup))
                return;

            double3 absolutePosition = shaderPointAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(absolutePosition)))
                return;

            _scannerShaderPoints[0] = new Vector4((float)absolutePosition.x, (float)absolutePosition.y, (float)absolutePosition.z, shaderProgress);
            _lastScannerShaderPoint = runtimePosition;
            _lastScannerShaderProgress = shaderProgress;
            Shader.SetGlobalInt(_HectonScannerPointCountId, 1);
            Shader.SetGlobalVectorArray(_HectonScannerPointsId, _scannerShaderPoints);
        }

        private static bool TryResolveRuntimeAup(float3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void PublishToolAcoustic(uint hash, float progress01, float pitchScale, float intensity01)
        {
            if (hash == 0u)
                return;

            SignalBus<ToolAcousticSignal>.TryPushTracked(new ToolAcousticSignal
            {
                ToolHash = _scannerToolHash,
                TargetHash = hash,
                Progress01 = math.saturate(progress01),
                PitchScale = math.max(0.1f, pitchScale),
                Intensity01 = math.saturate(intensity01),
                Frame = SystemDispatcher.CurrentFrameId,
                State = ToolAcousticStateScanning,
                Flags = 0
            }, ref _signalPushDropCount);
        }

        private void RebaseRuntimePositions(float3 runtimeDelta)
        {
            if (!math.all(math.isfinite(new float4(runtimeDelta, 1f))))
                return;

            bool persistedPositionsChanged = false;
            for (int i = 0; i < _fragmentCount; i++)
            {
                uint hash = _fragmentHashes[i];
                if (hash == 0u)
                    continue;

                Vector3 position = _fragmentPositionsMirror[i];
                position += new Vector3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z);
                _fragmentPositionsMirror[i] = position;
                persistedPositionsChanged = true;
            }

            for (int i = 0; i < _hologramCount; i++)
            {
                Matrix4x4 matrix = _hologramMatrices[i];
                matrix.m03 += runtimeDelta.x;
                matrix.m13 += runtimeDelta.y;
                matrix.m23 += runtimeDelta.z;
                _hologramMatrices[i] = matrix;
            }

            if (math.all(math.isfinite(new float4(_lastScannerShaderPoint, _lastScannerShaderProgress))))
                _lastScannerShaderPoint += runtimeDelta;

            if (persistedPositionsChanged)
                MarkMmfDirty(false);
        }

        private void RegisterFragmentPosition(uint hash, float3 position)
        {
            if (hash == 0u || !math.all(math.isfinite(new float4(position, 1f))))
                return;

            int mirrorIndex = FindFragmentMirror(hash);
            if (mirrorIndex >= 0)
            {
                _fragmentPositionsMirror[mirrorIndex] = new Vector3(position.x, position.y, position.z);
                return;
            }

            if (_fragmentCount < MaxDiscoveryCount)
            {
                _fragmentHashes[_fragmentCount] = hash;
                _fragmentPositionsMirror[_fragmentCount] = new Vector3(position.x, position.y, position.z);
                _fragmentCount++;
            }
        }

        private void RegisterHologram(ScannableFragment fragment, float3 position)
        {
            // Latched means Render can never draw these, so stop accumulating matrices RebaseRuntimePositions
            // would then walk on every floating-origin shift.
            if (_reconstructionSetupPermanentlyFailed ||
                _hologramCount >= HologramInstanceCapacity ||
                !math.all(math.isfinite(new float4(position, 1f))))
                return;

            if (_resolvedReconstructionMesh == null)
                _resolvedReconstructionMesh = TryResolveFragmentMesh(fragment);

            _hologramMatrices[_hologramCount] = Matrix4x4.TRS(
                new Vector3(position.x, position.y, position.z),
                Quaternion.identity,
                Vector3.one);
            _hologramCount++;
        }

        private static Mesh TryResolveFragmentMesh(ScannableFragment fragment)
        {
            if (fragment == null)
                return null;

            return fragment.CachedSharedMesh;
        }

        private void EnqueueNotification(uint hash, ushort progressPermille, byte kind, byte flags)
        {
            if (!TryOpenNotifications(out NativeArray<DataArchaeologyNotification> notifications))
                return;

            DataArchaeologyNotification notification = new DataArchaeologyNotification
            {
                EntryHash = hash,
                ProgressPermille = progressPermille,
                Kind = kind,
                Flags = flags
            };

            notifications[_notificationWrite] = notification;
            _notificationWrite = (_notificationWrite + 1) & (NotificationCapacity - 1);
            if (_notificationCount < NotificationCapacity)
            {
                _notificationCount++;
                return;
            }

            _notificationRead = (_notificationRead + 1) & (NotificationCapacity - 1);
        }

        private bool UpsertPartial(uint hash, ushort progressPermille)
        {
            if (TryFindPartial(hash, out int index))
            {
                if (progressPermille > _partialProgressPermille[index])
                {
                    _partialProgressPermille[index] = progressPermille;
                    MarkMmfDirty(false);
                    return true;
                }
                return false;
            }

            if (_partialCount >= MaxPartialScanCount)
                return false;

            _partialHashes[_partialCount] = hash;
            _partialProgressPermille[_partialCount] = progressPermille;
            _partialCount++;
            MarkMmfDirty(false);
            return true;
        }

        private void InsertOrUpgradePartialCold(uint hash, ushort progressPermille)
        {
            if (hash == 0u || progressPermille >= 1000)
                return;

            if (TryFindPartial(hash, out int index))
            {
                if (progressPermille > _partialProgressPermille[index])
                    _partialProgressPermille[index] = progressPermille;
                return;
            }

            if (_partialCount >= MaxPartialScanCount)
                return;

            _partialHashes[_partialCount] = hash;
            _partialProgressPermille[_partialCount] = progressPermille;
            _partialCount++;
        }

        private bool TryFindPartial(uint hash, out int index)
        {
            for (int i = 0; i < _partialCount; i++)
            {
                if (_partialHashes[i] == hash)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private void RemovePartial(uint hash)
        {
            if (!TryFindPartial(hash, out int index))
                return;

            RemovePartialAt(index);
            MarkMmfDirty(true);
        }

        private void RemoveNonScanningPartials(bool markDirty)
        {
            bool changed = false;
            for (int i = _partialCount - 1; i >= 0; i--)
            {
                uint hash = _partialHashes[i];
                if (hash == 0u || (TryGetScanState(hash, out byte state) && state != ScanStateScanning))
                {
                    RemovePartialAt(i);
                    changed = true;
                }
            }

            if (changed && markDirty)
                MarkMmfDirty(true);
        }

        private void RemovePartialAt(int index)
        {
            if ((uint)index >= (uint)_partialCount)
                return;

            int last = _partialCount - 1;
            _partialHashes[index] = _partialHashes[last];
            _partialProgressPermille[index] = _partialProgressPermille[last];
            _partialHashes[last] = 0u;
            _partialProgressPermille[last] = 0;
            _partialCount = last;
        }

        private void MarkMmfDirty(bool urgent)
        {
            _mmfDirty = true;
            float now = Application.isPlaying ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
            float delay = urgent ? MmfUrgentFlushDelaySeconds : MmfPartialFlushCadenceSeconds;
            float target = now + delay;
            if (!math.isfinite(_nextMmfFlushTime) || target < _nextMmfFlushTime)
                _nextMmfFlushTime = target;
        }

        private int FindFragmentMirror(uint hash)
        {
            for (int i = 0; i < _fragmentCount; i++)
            {
                if (_fragmentHashes[i] == hash)
                    return i;
            }

            return -1;
        }

        private void ClearFragmentPositions()
        {
            for (int i = 0; i < _fragmentCount; i++)
            {
                _fragmentHashes[i] = 0u;
                _fragmentPositionsMirror[i] = Vector3.zero;
            }

            _fragmentCount = 0;
        }

        private int FindScanStateIndex(int key)
        {
            for (int i = 0; i < _scanStateCount; i++)
            {
                if (_scanStateKeys[i] == key)
                    return i;
            }

            return -1;
        }

        private void RemoveScanStateAt(int index)
        {
            if ((uint)index >= (uint)_scanStateCount)
                return;

            int last = _scanStateCount - 1;
            _scanStateKeys[index] = _scanStateKeys[last];
            _scanStateValues[index] = _scanStateValues[last];
            _scanStateKeys[last] = 0;
            _scanStateValues[last] = ScanStateUnscanned;
            _scanStateCount = last;
        }

        private void ClearScanStates()
        {
            for (int i = 0; i < _scanStateCount; i++)
            {
                _scanStateKeys[i] = 0;
                _scanStateValues[i] = ScanStateUnscanned;
            }

            _scanStateCount = 0;
        }

        private void RecordTelemetry(uint hash, byte flags, float3 position, float match01, ushort progressPermille)
        {
            if (!TryOpenTelemetryRing(out NativeArray<DataArchaeologyTelemetryEntry> telemetryRing))
                return;

            if (!math.all(math.isfinite(new float4(position, match01))))
            {
                DumpTelemetryCold();
                return;
            }

            telemetryRing[_telemetryCursor] = new DataArchaeologyTelemetryEntry
            {
                Frame = SystemDispatcher.CurrentFrameId,
                Hash = hash,
                Position = position,
                Match01 = match01,
                Flags = flags,
                Reserved0 = 0,
                ProgressPermille = progressPermille
            };
            _telemetryCursor++;
            if (_telemetryCursor >= TelemetryCapacity)
                _telemetryCursor = 0;
        }

        private void EmitSensoryFeedback(in DataArchaeologyFrequencyResult result)
        {
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (result.Match01 <= 0.01f || now < _nextSensoryFeedbackTime)
                return;

            float intensity = math.saturate(result.Match01);
            PlayerSignalEvents.TryRaiseInteractionSignal(new PlayerInteractionStressSignal(
                0f,
                intensity,
                result.FeedbackPitchScale,
                result.FeedbackFrequency01));
            ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                intensity * 0.18f,
                intensity * 0.32f,
                0.07f,
                28f + (result.Match01 * 42f),
                2,
                0x03);
            _nextSensoryFeedbackTime = now + 0.1f;
        }

        private static ushort ToPermille(float progress01)
        {
            return (ushort)math.clamp((int)math.round(math.saturate(progress01) * 1000f), 0, 1000);
        }

        private static uint BuildAupArtifactSeed(uint hash, float3 position)
        {
            int3 sector = (int3)math.floor(position * 0.02f);
            uint seed = hash ^ (uint)sector.x * 73856093u ^ (uint)sector.y * 19349663u ^ (uint)sector.z * 83492791u;
            return DataArchaeologyFrequencyKernel.NextLcg(seed != 0u ? seed : 1u);
        }

        private static float Wrap01(float value)
        {
            return value - math.floor(value);
        }

        private static void EnsurePartialSaveArrays(SaveData data)
        {
            if (data.dataArchaeologyPartialScanHashes == null ||
                data.dataArchaeologyPartialScanHashes.Length < MaxPartialScanCount)
            {
                data.dataArchaeologyPartialScanHashes = new uint[MaxPartialScanCount]; // COLD ALLOC: uint[256] - archaeology partial scan save hashes - owner: SaveData
                data.dataArchaeologyPartialScanCount = 0;
            }

            if (data.dataArchaeologyPartialScanProgressPermille == null ||
                data.dataArchaeologyPartialScanProgressPermille.Length < MaxPartialScanCount)
            {
                data.dataArchaeologyPartialScanProgressPermille = new ushort[MaxPartialScanCount]; // COLD ALLOC: ushort[256] - archaeology partial scan save progress - owner: SaveData
                data.dataArchaeologyPartialScanCount = 0;
            }
        }

        private static void EnsureScanStateSaveArrays(SaveData data)
        {
            if (data.dataArchaeologyScanStateKeys == null ||
                data.dataArchaeologyScanStateKeys.Length < MaxDiscoveryCount)
            {
                data.dataArchaeologyScanStateKeys = new int[MaxDiscoveryCount]; // COLD ALLOC: int[1024] - explicit data archaeology scan state save keys - owner: SaveData
                data.dataArchaeologyScanStateCount = 0;
            }

            if (data.dataArchaeologyScanStateValues == null ||
                data.dataArchaeologyScanStateValues.Length < MaxDiscoveryCount)
            {
                data.dataArchaeologyScanStateValues = new byte[MaxDiscoveryCount]; // COLD ALLOC: byte[1024] - explicit data archaeology scan state save values - owner: SaveData
                data.dataArchaeologyScanStateCount = 0;
            }
        }

        private void PopulateScanStateSaveData(SaveData data)
        {
            EnsureScanStateSaveArrays(data);
            data.dataArchaeologyScanStateCount = 0;
            if (_scanStateCount == 0)
                return;

            int safeCount = math.min(_scanStateCount, SaveData.MaxDataArchaeologyScanStates);
            for (int i = 0; i < safeCount; i++)
            {
                data.dataArchaeologyScanStateKeys[i] = _scanStateKeys[i];
                data.dataArchaeologyScanStateValues[i] = _scanStateValues[i];
            }

            for (int i = safeCount; i < SaveData.MaxDataArchaeologyScanStates; i++)
            {
                data.dataArchaeologyScanStateKeys[i] = 0;
                data.dataArchaeologyScanStateValues[i] = ScanStateUnscanned;
            }

            data.dataArchaeologyScanStateCount = safeCount;
        }

        private void LoadScanStateSaveData(SaveData data)
        {
            EnsureScanStateSaveArrays(data);
            int safeCount = math.clamp(
                data.dataArchaeologyScanStateCount,
                0,
                math.min(MaxDiscoveryCount, math.min(data.dataArchaeologyScanStateKeys.Length, data.dataArchaeologyScanStateValues.Length)));
            for (int i = 0; i < safeCount; i++)
            {
                int key = data.dataArchaeologyScanStateKeys[i];
                if (key == 0)
                    continue;

                byte state = data.dataArchaeologyScanStateValues[i];
                if (state > ScanStateScanned)
                    state = ScanStateUnscanned;

                SetScanState(unchecked((uint)key), state);
            }
        }

        private void TryLoadMmfCold(bool requireExistingSaveState)
        {
#if !UNITY_WEBGL
            if (!enableMmfPersistence)
                return;

            string path = ResolveMmfPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                bool shouldRewriteMmf = false;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    long expectedByteCount = ResolveExpectedMmfByteCount();
                    if (stream.Length < expectedByteCount)
                        return;

                    uint magic = reader.ReadUInt32();
                    uint version = reader.ReadUInt32();
                    if (magic != MmfMagic || version != MmfVersion)
                        return;

                    int fragmentCount = reader.ReadInt32();
                    int partialCount = reader.ReadInt32();
                    if ((uint)fragmentCount > MaxDiscoveryCount || (uint)partialCount > MaxPartialScanCount)
                        return;

                    int safeFragmentCount = fragmentCount;
                    int safePartialCount = partialCount;

                    for (int i = 0; i < MaxDiscoveryCount; i++)
                    {
                        if (stream.Position + MmfFragmentRecordBytes > stream.Length)
                            return;

                        uint hash = reader.ReadUInt32();
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        float z = reader.ReadSingle();

                        if (i >= safeFragmentCount)
                            continue;

                        if (hash == 0u)
                        {
                            shouldRewriteMmf = true;
                            continue;
                        }

                        float3 position = new float3(x, y, z);
                        if (!math.all(math.isfinite(new float4(position, 1f))))
                        {
                            shouldRewriteMmf = true;
                            continue;
                        }

                        if (!CanApplyMmfFragment(hash, requireExistingSaveState))
                        {
                            shouldRewriteMmf = true;
                            continue;
                        }

                        RegisterFragmentPosition(hash, position);
                        SetNativeLoreBit(DataArchaeologyDiscoveryBitMask.ResolveBitIndex(hash));
                        SetScanState(hash, ScanStateScanned);
                    }

                    for (int i = 0; i < MaxPartialScanCount; i++)
                    {
                        if (stream.Position + MmfPartialRecordBytes > stream.Length)
                            return;

                        uint hash = reader.ReadUInt32();
                        ushort progress = reader.ReadUInt16();
                        reader.ReadUInt16();

                        if (i >= safePartialCount)
                            continue;

                        if (hash == 0u || progress >= 1000)
                        {
                            shouldRewriteMmf = true;
                            continue;
                        }

                        if (!CanApplyMmfPartial(hash, requireExistingSaveState))
                        {
                            shouldRewriteMmf = true;
                            continue;
                        }

                        if (TryGetScanState(hash, out byte state) && state == ScanStateScanned)
                        {
                            shouldRewriteMmf = true;
                            continue;
                        }

                        if (TryFindPartial(hash, out int partialIndex) && progress < _partialProgressPermille[partialIndex])
                            shouldRewriteMmf = true;

                        InsertOrUpgradePartialCold(hash, progress);
                        SetScanState(hash, ScanStateScanning);
                    }
                }

                if (shouldRewriteMmf)
                    MarkMmfDirty(false);
                else
                    ClearMmfDirty();
            }
            catch (IOException)
            {
                if (_mmfDirty)
                    ScheduleMmfPersistenceRetry();
            }
            catch (UnauthorizedAccessException)
            {
                if (_mmfDirty)
                    ScheduleMmfPersistenceRetry();
            }
            catch (NotSupportedException)
            {
                if (_mmfDirty)
                    ScheduleMmfPersistenceRetry();
            }
            catch (ArgumentException)
            {
                if (_mmfDirty)
                    ScheduleMmfPersistenceRetry();
            }
            catch (ObjectDisposedException)
            {
                if (_mmfDirty)
                    ScheduleMmfPersistenceRetry();
            }
#endif
        }

        private bool CanApplyMmfFragment(uint hash, bool requireExistingSaveState)
        {
            if (!requireExistingSaveState)
                return true;

            return TryGetScanState(hash, out byte state) && state == ScanStateScanned;
        }

        private bool CanApplyMmfPartial(uint hash, bool requireExistingSaveState)
        {
            if (!requireExistingSaveState)
                return true;

            return TryGetScanState(hash, out byte state) && state == ScanStateScanning;
        }

        private void PersistMmfCold()
        {
            if (!_mmfDirty)
                return;

#if !UNITY_WEBGL
            if (!enableMmfPersistence)
            {
                ClearMmfDirty();
                return;
            }

            string path = ResolveMmfPath();
            if (string.IsNullOrEmpty(path))
            {
                ClearMmfDirty();
                return;
            }

            string tempPath = null;
            try
            {
                long byteCount = ResolveExpectedMmfByteCount();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                tempPath = path + ".tmp";
                TryDeleteMmfTempFileNoThrow(tempPath);
                long writtenLength;
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, MmfFileStreamBufferBytes, FileOptions.WriteThrough))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(MmfMagic);
                    writer.Write(MmfVersion);
                    writer.Write(_fragmentCount);
                    writer.Write(_partialCount);

                    for (int i = 0; i < MaxDiscoveryCount; i++)
                    {
                        uint hash = i < _fragmentCount ? _fragmentHashes[i] : 0u;
                        Vector3 position = i < _fragmentCount ? _fragmentPositionsMirror[i] : Vector3.zero;
                        writer.Write(hash);
                        writer.Write(position.x);
                        writer.Write(position.y);
                        writer.Write(position.z);
                    }

                    for (int i = 0; i < MaxPartialScanCount; i++)
                    {
                        uint hash = i < _partialCount ? _partialHashes[i] : 0u;
                        ushort progress = i < _partialCount ? _partialProgressPermille[i] : (ushort)0;
                        writer.Write(hash);
                        writer.Write(progress);
                        writer.Write((ushort)0);
                    }

                    stream.SetLength(byteCount);
                    stream.Flush(true);
                    writtenLength = stream.Length;
                }

                if (writtenLength != byteCount)
                {
                    TryDeleteMmfTempFileNoThrow(tempPath);
                    ScheduleMmfPersistenceRetry();
                    return;
                }

                PromoteMmfTempFileCold(tempPath, path);
                ClearMmfDirty();
            }
            catch (IOException)
            {
                TryDeleteMmfTempFileNoThrow(tempPath);
                ScheduleMmfPersistenceRetry();
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteMmfTempFileNoThrow(tempPath);
                ScheduleMmfPersistenceRetry();
            }
            catch (NotSupportedException)
            {
                TryDeleteMmfTempFileNoThrow(tempPath);
                ScheduleMmfPersistenceRetry();
            }
            catch (ArgumentException)
            {
                TryDeleteMmfTempFileNoThrow(tempPath);
                ScheduleMmfPersistenceRetry();
            }
            catch (ObjectDisposedException)
            {
                TryDeleteMmfTempFileNoThrow(tempPath);
                ScheduleMmfPersistenceRetry();
            }
#else
            ClearMmfDirty();
#endif
        }

        private static long ResolveExpectedMmfByteCount()
        {
            return MmfHeaderBytes +
                   ((long)MaxDiscoveryCount * MmfFragmentRecordBytes) +
                   ((long)MaxPartialScanCount * MmfPartialRecordBytes);
        }

        private static void PromoteMmfTempFileCold(string tempPath, string path)
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, null, true);
            else
                File.Move(tempPath, path);
        }

        private static void TryDeleteMmfTempFileNoThrow(string tempPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        private void ClearMmfDirty()
        {
            _mmfDirty = false;
            _nextMmfFlushTime = float.PositiveInfinity;
        }

        private void ScheduleMmfPersistenceRetry()
        {
            _mmfDirty = true;
            float now = Application.isPlaying ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
            _nextMmfFlushTime = now + MmfFailureRetrySeconds;
        }

        private string ResolveMmfPath()
        {
            if (string.IsNullOrEmpty(mmfFileName))
                return string.Empty;

            return HectonPersistentPathPolicy.CombineFile(mmfFileName);
        }

        private void DumpTelemetryCold()
        {
#if UNITY_EDITOR
            if (!TryOpenTelemetryRing(out NativeArray<DataArchaeologyTelemetryEntry> telemetryRing))
                return;

            NativeArray<byte> payload = default;
            try
            {
                const string path = "Docs/AgentLogs/Dump_DATA_ARCHAEOLOGY.bin";
                const int rowBytes = 28;
                int byteCount = TelemetryCapacity * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(DataArchaeologyRuntime),
                    "DataArchaeologyTelemetryDumpPayload");

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    int cursor = 0;
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        DataArchaeologyTelemetryEntry entry = telemetryRing[i];
                        WriteUInt(bytes, cursor, entry.Frame);
                        WriteUInt(bytes, cursor + 4, entry.Hash);
                        WriteFloat(bytes, cursor + 8, entry.Position.x);
                        WriteFloat(bytes, cursor + 12, entry.Position.y);
                        WriteFloat(bytes, cursor + 16, entry.Position.z);
                        WriteFloat(bytes, cursor + 20, entry.Match01);
                        bytes[cursor + 24] = entry.Flags;
                        bytes[cursor + 25] = entry.Reserved0;
                        WriteUShort(bytes, cursor + 26, entry.ProgressPermille);
                        cursor += rowBytes;
                    }
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (Exception)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(DataArchaeologyRuntime),
                    "DataArchaeologyTelemetryDumpPayload");
            }
#endif
        }

        private static unsafe void WriteUInt(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteUShort(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteFloat(byte* data, int offset, float value)
        {
            UnsafeUtility.MemCpy(data + offset, &value, sizeof(float));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tuningThreshold < 0.0001f)
                tuningThreshold = 0.0001f;

            signalInterference01 = math.saturate(signalInterference01);
        }
#endif
    }
}
