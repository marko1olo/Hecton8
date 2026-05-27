using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;
using Debug = UnityEngine.Debug;

namespace Hecton8.UI
{
    public static class PdaProjectionVaultIds
    {
        public const int State = 348730;
        public const int Input = 348731;
        public const int TelemetryRing = 348732;
        public const int TelemetryCursor = 348733;
        public const int Tuning = 348734;
        public const int InterfaceProfiles = 348735;
#if UNITY_EDITOR
        public const int CsvScratch = 348736;
#endif
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct PdaStateDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
        [FieldOffset(64)] public uint ActiveTabHashID;
        [FieldOffset(68)] public float BootSequenceProgress01;
        [FieldOffset(72)] public uint PdaFlags;
        [FieldOffset(76)] private byte _pad76;
        [FieldOffset(77)] private byte _pad77;
        [FieldOffset(78)] private byte _pad78;
        [FieldOffset(79)] private byte _pad79;
    }

    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct PdaProjectionInputDTO
    {
        [FieldOffset(0)] public double3 WristAup;
        [FieldOffset(24)] public double3 CameraAup;
        [FieldOffset(48)] public float4 WristRotation;
        [FieldOffset(64)] public float3 LocalScreenOffset;
        [FieldOffset(76)] public float ScreenWidthMeters;
        [FieldOffset(80)] public float ScreenHeightMeters;
        [FieldOffset(84)] public float BootSequenceProgress01;
        [FieldOffset(88)] public uint ActiveTabHashID;
        [FieldOffset(92)] public uint PdaFlags;
        [FieldOffset(96)] public float GlassRefractionIndex;
        [FieldOffset(100)] public float ScreenCurvatureScalar;
        [FieldOffset(104)] public float GlobalQualityWeight01;
        [FieldOffset(108)] private byte _pad108;
        [FieldOffset(109)] private byte _pad109;
        [FieldOffset(110)] private byte _pad110;
        [FieldOffset(111)] private byte _pad111;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaProjectionTuningDTO
    {
        [FieldOffset(0)] public float4 Params0;
        [FieldOffset(16)] public float4 Params1;
        [FieldOffset(32)] public float4 AtlasFallbackRect;
        [FieldOffset(48)] public float4 VisualParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PdaInterfaceProfileDTO
    {
        [FieldOffset(0)] public float4 UvRect;
        [FieldOffset(16)] public uint TabHashID;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private byte _pad24;
        [FieldOffset(25)] private byte _pad25;
        [FieldOffset(26)] private byte _pad26;
        [FieldOffset(27)] private byte _pad27;
        [FieldOffset(28)] private byte _pad28;
        [FieldOffset(29)] private byte _pad29;
        [FieldOffset(30)] private byte _pad30;
        [FieldOffset(31)] private byte _pad31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaProjectionTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint ActiveTabHashID;
        [FieldOffset(12)] public uint JobMicrosecondsQ16;
        [FieldOffset(16)] public float LocalizedDistanceMeters;
        [FieldOffset(20)] public float BootSequenceProgress01;
        [FieldOffset(24)] public float QualityWeight01;
        [FieldOffset(28)] public uint TelemetryCursor;
        [FieldOffset(32)] public uint MatrixHash;
        [FieldOffset(36)] public uint ProfileHash;
        [FieldOffset(40)] public float ScreenWidthMeters;
        [FieldOffset(44)] public float ScreenHeightMeters;
        [FieldOffset(48)] public float GlassRefractionIndex;
        [FieldOffset(52)] public float ScreenCurvatureScalar;
        [FieldOffset(56)] public float GlobalQualityWeight01;
        [FieldOffset(60)] public uint PdaFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaProjectionGlobalsDTO
    {
        [FieldOffset(0)] public float4 ScreenParams;
        [FieldOffset(16)] public float4 RefractionParams;
        [FieldOffset(32)] public float4 AtlasRect;
        [FieldOffset(48)] public float4 VisualParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PdaProjectionBlackBoxDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint FrameIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int TelemetryCapacity;
        [FieldOffset(20)] public int TelemetryCursor;
        [FieldOffset(24)] public int TelemetryEntrySizeBytes;
        [FieldOffset(28)] public int PayloadBytes;
        [FieldOffset(32)] public int TelemetryValidCount;
        [FieldOffset(36)] public int TelemetryStartIndex;
        [FieldOffset(40)] private byte _pad40;
        [FieldOffset(41)] private byte _pad41;
        [FieldOffset(42)] private byte _pad42;
        [FieldOffset(43)] private byte _pad43;
        [FieldOffset(44)] private byte _pad44;
        [FieldOffset(45)] private byte _pad45;
        [FieldOffset(46)] private byte _pad46;
        [FieldOffset(47)] private byte _pad47;
        [FieldOffset(48)] private byte _pad48;
        [FieldOffset(49)] private byte _pad49;
        [FieldOffset(50)] private byte _pad50;
        [FieldOffset(51)] private byte _pad51;
        [FieldOffset(52)] private byte _pad52;
        [FieldOffset(53)] private byte _pad53;
        [FieldOffset(54)] private byte _pad54;
        [FieldOffset(55)] private byte _pad55;
        [FieldOffset(56)] private byte _pad56;
        [FieldOffset(57)] private byte _pad57;
        [FieldOffset(58)] private byte _pad58;
        [FieldOffset(59)] private byte _pad59;
        [FieldOffset(60)] private byte _pad60;
        [FieldOffset(61)] private byte _pad61;
        [FieldOffset(62)] private byte _pad62;
        [FieldOffset(63)] private byte _pad63;
    }

    public sealed unsafe partial class WristHologramHudRuntime : IPDAEventListener, IPDAIntrusionEventListener
    {
        private const int PdaProjectionStateCapacity = 1;
        private const int PdaProjectionInputCapacity = 1;
        private const int PdaProjectionTelemetryCapacity = 300;
        private const int PdaProjectionInterfaceProfileCapacity = 64;
#if UNITY_EDITOR
        private const int PdaProjectionCsvScratchBytes = 16384;
#endif
        private const int PdaProjectionGlobalsStrideBytes = 64;
        private const int PdaProjectionMinimumShaderLevel = 45;
        private const float PdaProjectionBudgetMicroseconds = 100f;
        private const float PdaProjectionDefaultWidthMeters = 0.18f;
        private const float PdaProjectionDefaultHeightMeters = 0.112f;
        private const uint PdaProjectionBlackBoxMagic = 0x50333438u; // P348
        private const uint PdaProjectionBlackBoxVersion = 2u;
        private const uint PdaProjectionFlagActive = 1u << 0;
        private const uint PdaProjectionFlagMockSource = 1u << 1;
        private const uint PdaProjectionFlagNonFinite = 1u << 2;
        private const uint PdaProjectionFlagOverBudget = 1u << 3;
        private const uint PdaProjectionFlagIntrusion = 1u << 4;
        private const uint PdaProjectionFlagQualityOverride = 1u << 5;
        private const uint PdaProjectionFlagGpuUploadFault = 1u << 6;
        private const uint PdaProjectionWriteMaskState = 1u << 0;
        private const uint PdaProjectionWriteMaskInput = 1u << 1;
        private const uint PdaProjectionWriteMaskTelemetry = 1u << 2;
        private const uint PdaProjectionWriteMaskTelemetryCursor = 1u << 3;
        private const uint PdaProjectionWriteMaskTuning = 1u << 4;
        private const uint PdaProjectionWriteMaskProfiles = 1u << 5;
#if UNITY_EDITOR
        private const string PdaProjectionProfilesCsvFileName = "pda_interface_profiles.csv";
#endif

        private static readonly BufferID PdaProjectionStateBufferId = (BufferID)PdaProjectionVaultIds.State;
        private static readonly BufferID PdaProjectionInputBufferId = (BufferID)PdaProjectionVaultIds.Input;
        private static readonly BufferID PdaProjectionTelemetryBufferId = (BufferID)PdaProjectionVaultIds.TelemetryRing;
        private static readonly BufferID PdaProjectionTelemetryCursorBufferId = (BufferID)PdaProjectionVaultIds.TelemetryCursor;
        private static readonly BufferID PdaProjectionTuningBufferId = (BufferID)PdaProjectionVaultIds.Tuning;
        private static readonly BufferID PdaProjectionProfilesBufferId = (BufferID)PdaProjectionVaultIds.InterfaceProfiles;
#if UNITY_EDITOR
        private static readonly BufferID PdaProjectionCsvScratchBufferId = (BufferID)PdaProjectionVaultIds.CsvScratch;
#endif
        private static WristHologramHudRuntime s_activePdaProjectorRuntime;

        [Header("Screen-Space PDA Projector")]
        [SerializeField] private bool enableScreenSpacePdaProjection = true;
        [SerializeField] private bool enableMockWristProjection;
        [SerializeField] private bool forcePdaProjectionVisible;
        [SerializeField] private Texture2D pdaInterfaceAtlas;
        [SerializeField, Range(0.08f, 0.34f)] private float pdaProjectionWidthMeters = PdaProjectionDefaultWidthMeters;
        [SerializeField, Range(0.05f, 0.22f)] private float pdaProjectionHeightMeters = PdaProjectionDefaultHeightMeters;
        [SerializeField, Range(-0.08f, 0.08f)] private float pdaProjectionLocalXOffsetMeters = 0f;
        [SerializeField, Range(-0.08f, 0.12f)] private float pdaProjectionLocalYOffsetMeters = 0.018f;
        [SerializeField, Range(-0.12f, 0.18f)] private float pdaProjectionLocalZOffsetMeters = -0.045f;
        [SerializeField, Range(1.0f, 1.8f)] private float pdaProjectionGlassRefractionIndex = 1.46f;
        [SerializeField, Range(0f, 1f)] private float pdaProjectionScreenCurvatureScalar = 0.28f;
        [SerializeField, Range(-1f, 1f)] private float pdaProjectionQualityOverride01 = -1f;
        [SerializeField, Range(0.1f, 16f)] private float pdaProjectionBootRate = 4.25f;

        private VaultGenerationHandle<PdaStateDTO> _pdaProjectionStateHandle;
        private VaultGenerationHandle<PdaProjectionInputDTO> _pdaProjectionInputHandle;
        private VaultGenerationHandle<PdaProjectionTelemetryEntry> _pdaProjectionTelemetryHandle;
        private VaultGenerationHandle<int> _pdaProjectionTelemetryCursorHandle;
        private VaultGenerationHandle<PdaProjectionTuningDTO> _pdaProjectionTuningHandle;
        private VaultGenerationHandle<PdaInterfaceProfileDTO> _pdaProjectionProfileHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _pdaProjectionCsvScratchHandle;
#endif
        private GraphicsBuffer _pdaProjectionStateBufferA;
        private GraphicsBuffer _pdaProjectionStateBufferB;
        private GraphicsBuffer _pdaProjectionActiveStateBuffer;
        private GraphicsBuffer _pdaProjectionGlobalsBufferA;
        private GraphicsBuffer _pdaProjectionGlobalsBufferB;
        private GraphicsBuffer _pdaProjectionActiveGlobalsBuffer;
        private float4x4 _lastPdaProjectionMatrix;
        private uint _pdaProjectionActiveTabHash = 0x50444100u;
        private uint _pdaProjectionFlags;
        private bool _pdaProjectionTuningSeeded;
        private bool _pdaProjectionDefaultProfilesSeeded;
        private bool _pdaProjectionProfilesLoaded;
        private bool _pdaProjectionNativeBuffersReady;
        private bool _pdaProjectionGraphicsPathSupported;
        private bool _pdaProjectionGraphicsBuffersReady;
        private bool _pdaProjectionGpuPayloadValid;
        private bool _pdaProjectionBlackBoxDumped;
        private int _pdaProjectionWriteBufferIndex;
        private int _pdaProjectionGlobalsWriteBufferIndex;
        private float _pdaProjectionBoot01;
        private float _pdaProjectionCorruption01;

        private void PdaProjectorOnEnable()
        {
            if (!enableScreenSpacePdaProjection)
                return;

            s_activePdaProjectorRuntime = this;
            PDAEvents.Register(this);
            PDAIntrusionEvents.Register(this);
#if UNITY_EDITOR
            if (EnsurePdaProjectionNativeBuffers())
                TryLoadPdaInterfaceProfilesCold();
#endif

            EnsurePdaProjectionGraphicsBuffers();
        }

        private void PdaProjectorOnDisable()
        {
            PDAEvents.Unregister(this);
            PDAIntrusionEvents.Unregister(this);
            if (ReferenceEquals(s_activePdaProjectorRuntime, this))
                s_activePdaProjectorRuntime = null;

            ReleasePdaProjectionGraphicsBuffers();
        }

        private void PdaProjectorOnDestroy()
        {
            PdaProjectorOnDisable();
            PdaProjectorReleaseNativeStateHandles();
        }

        private void PdaProjectorTick(float deltaTime)
        {
            if (!enableScreenSpacePdaProjection)
                return;

            if (!_pdaProjectionNativeBuffersReady || !_pdaProjectionGraphicsBuffersReady)
                return;

            float activeTarget = ResolvePdaProjectionActive01();
            float bootBlend = math.saturate(math.max(0f, deltaTime) * math.max(0.1f, pdaProjectionBootRate));
            _pdaProjectionBoot01 = math.saturate(math.lerp(_pdaProjectionBoot01, activeTarget, bootBlend));
            _pdaProjectionCorruption01 = math.saturate(math.lerp(_pdaProjectionCorruption01, math.saturate(_globalSystemPressure01), bootBlend * 0.25f));
        }

        private void PdaProjectorOnDataVaultServiceReplaced()
        {
            _pdaProjectionGpuPayloadValid = false;
            if (!enableScreenSpacePdaProjection || !isActiveAndEnabled)
                return;

#if UNITY_EDITOR
            if (EnsurePdaProjectionNativeBuffers())
                TryLoadPdaInterfaceProfilesCold();
#endif
        }

        private void PdaProjectorLateFrameTick()
        {
            uint acquiredMask = 0u;
            PdaStateDTO stateSnapshot = default;
            PdaProjectionGlobalsDTO globalsSnapshot = default;
            bool shouldDump = false;
            uint dumpFlags = 0u;

            if (!enableScreenSpacePdaProjection ||
                !_pdaProjectionNativeBuffersReady ||
                !_pdaProjectionGraphicsBuffersReady ||
                !TryAcquirePdaProjectionWriteBuffers(
                    out NativeArray<PdaStateDTO> states,
                    out NativeArray<PdaProjectionInputDTO> inputs,
                    out NativeArray<PdaProjectionTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor,
                    out NativeArray<PdaProjectionTuningDTO> tuning,
                    out NativeArray<PdaInterfaceProfileDTO> profiles,
                    out acquiredMask))
            {
                _pdaProjectionGpuPayloadValid = false;
                return;
            }

            try
            {
                if (!BuildPdaProjectionInput(inputs, tuning))
                {
                    _pdaProjectionGpuPayloadValid = false;
                    return;
                }

                long startTicks = Stopwatch.GetTimestamp();
                CompilePdaProjectionMatrices(inputs, states, telemetry, telemetryCursor, Hecton8.Core.SystemDispatcher.CurrentFrameId);
                long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                uint elapsedQ16 = (uint)math.max(0, (int)math.round((float)elapsedTicks * 1000000f * 16f / Stopwatch.Frequency));
                PatchPdaProjectionTelemetryJobCost(telemetry, telemetryCursor, elapsedQ16);
                stateSnapshot = states[0];
                _lastPdaProjectionMatrix = stateSnapshot.LocalToWorld;
                globalsSnapshot = BuildPdaProjectionGlobals(tuning, profiles, in stateSnapshot);

                float elapsedMicros = elapsedQ16 * (1f / 16f);
                if ((stateSnapshot.PdaFlags & PdaProjectionFlagNonFinite) != 0u || elapsedMicros > PdaProjectionBudgetMicroseconds)
                {
                    shouldDump = true;
                    dumpFlags = stateSnapshot.PdaFlags;
                }
            }
            finally
            {
                ReleasePdaProjectionAcquiredBuffers(acquiredMask);
            }

            UploadPdaProjectionGpu(in stateSnapshot, in globalsSnapshot);
            if (shouldDump)
                DumpPdaProjectionBlackBoxOnce(dumpFlags);
        }

        private bool EnsurePdaProjectionNativeBuffers()
        {
            if (_vault == null && _cachedDataVault != null)
                _vault = _cachedDataVault;

            IDataVault vault = _vault;
            if (vault == null)
            {
                PdaProjectorReleaseNativeStateHandles();
                return false;
            }

            bool valid =
                IsExactVaultHandle(in _pdaProjectionStateHandle, PdaProjectionStateBufferId) &&
                IsExactVaultHandle(in _pdaProjectionInputHandle, PdaProjectionInputBufferId) &&
                IsExactVaultHandle(in _pdaProjectionTelemetryHandle, PdaProjectionTelemetryBufferId) &&
                IsExactVaultHandle(in _pdaProjectionTelemetryCursorHandle, PdaProjectionTelemetryCursorBufferId) &&
                IsExactVaultHandle(in _pdaProjectionTuningHandle, PdaProjectionTuningBufferId) &&
                IsExactVaultHandle(in _pdaProjectionProfileHandle, PdaProjectionProfilesBufferId)
#if UNITY_EDITOR
                && IsExactVaultHandle(in _pdaProjectionCsvScratchHandle, PdaProjectionCsvScratchBufferId)
#endif
                ;

            if (!valid)
            {
                _pdaProjectionStateHandle = vault.EnsureGenerationHandle<PdaStateDTO>(PdaProjectionStateBufferId, PdaProjectionStateCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
                _pdaProjectionInputHandle = vault.EnsureGenerationHandle<PdaProjectionInputDTO>(PdaProjectionInputBufferId, PdaProjectionInputCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
                _pdaProjectionTelemetryHandle = vault.EnsureGenerationHandle<PdaProjectionTelemetryEntry>(PdaProjectionTelemetryBufferId, PdaProjectionTelemetryCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
                _pdaProjectionTelemetryCursorHandle = vault.EnsureGenerationHandle<int>(PdaProjectionTelemetryCursorBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
                _pdaProjectionTuningHandle = vault.EnsureGenerationHandle<PdaProjectionTuningDTO>(PdaProjectionTuningBufferId, 1, SystemID.UI, NativeArrayOptions.UninitializedMemory);
                _pdaProjectionProfileHandle = vault.EnsureGenerationHandle<PdaInterfaceProfileDTO>(PdaProjectionProfilesBufferId, PdaProjectionInterfaceProfileCapacity, SystemID.UI, NativeArrayOptions.UninitializedMemory);
#if UNITY_EDITOR
                _pdaProjectionCsvScratchHandle = vault.EnsureGenerationHandle<byte>(PdaProjectionCsvScratchBufferId, PdaProjectionCsvScratchBytes, SystemID.UI, NativeArrayOptions.UninitializedMemory);
#endif
                _pdaProjectionTuningSeeded = false;
                _pdaProjectionDefaultProfilesSeeded = false;
                _pdaProjectionProfilesLoaded = false;
                _pdaProjectionNativeBuffersReady = false;
            }

            uint acquiredMask = 0u;
            if (!TryAcquirePdaProjectionWriteBuffers(
                    out NativeArray<PdaStateDTO> states,
                    out NativeArray<PdaProjectionInputDTO> inputs,
                    out NativeArray<PdaProjectionTelemetryEntry> telemetry,
                    out NativeArray<int> cursor,
                    out NativeArray<PdaProjectionTuningDTO> tuning,
                    out NativeArray<PdaInterfaceProfileDTO> profiles,
                    out acquiredMask))
            {
                return false;
            }

            try
            {
                _ = states;
                _ = inputs;
                if (!_pdaProjectionTuningSeeded)
                {
                    SeedPdaProjectionTuning(tuning);
                    ClearPdaProjectionTelemetry(telemetry, cursor);
                    _pdaProjectionTuningSeeded = true;
                }

                if (!_pdaProjectionDefaultProfilesSeeded)
                {
                    SeedDefaultPdaInterfaceProfiles(profiles);
                    _pdaProjectionDefaultProfilesSeeded = true;
                }
            }
            finally
            {
                ReleasePdaProjectionAcquiredBuffers(acquiredMask);
            }

            _pdaProjectionNativeBuffersReady = true;
            return true;
        }

        private void PdaProjectorReleaseNativeStateHandles()
        {
            _pdaProjectionStateHandle = default;
            _pdaProjectionInputHandle = default;
            _pdaProjectionTelemetryHandle = default;
            _pdaProjectionTelemetryCursorHandle = default;
            _pdaProjectionTuningHandle = default;
            _pdaProjectionProfileHandle = default;
#if UNITY_EDITOR
            _pdaProjectionCsvScratchHandle = default;
#endif
            _pdaProjectionTuningSeeded = false;
            _pdaProjectionDefaultProfilesSeeded = false;
            _pdaProjectionProfilesLoaded = false;
            _pdaProjectionNativeBuffersReady = false;
            _pdaProjectionGpuPayloadValid = false;
        }

        private bool TryAcquirePdaProjectionWriteBuffers(
            out NativeArray<PdaStateDTO> states,
            out NativeArray<PdaProjectionInputDTO> inputs,
            out NativeArray<PdaProjectionTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<PdaProjectionTuningDTO> tuning,
            out NativeArray<PdaInterfaceProfileDTO> profiles,
            out uint acquiredMask)
        {
            states = default;
            inputs = default;
            telemetry = default;
            telemetryCursor = default;
            tuning = default;
            profiles = default;
            acquiredMask = 0u;
            bool success = false;
            try
            {
                if (_vault == null)
                    return false;
                if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionStateHandle, PdaProjectionStateBufferId, PdaProjectionStateCapacity, out states))
                    return false;
                acquiredMask |= PdaProjectionWriteMaskState;
                if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionInputHandle, PdaProjectionInputBufferId, PdaProjectionInputCapacity, out inputs))
                    return false;
                acquiredMask |= PdaProjectionWriteMaskInput;
                if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionTelemetryHandle, PdaProjectionTelemetryBufferId, PdaProjectionTelemetryCapacity, out telemetry))
                    return false;
                acquiredMask |= PdaProjectionWriteMaskTelemetry;
                if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionTelemetryCursorHandle, PdaProjectionTelemetryCursorBufferId, 1, out telemetryCursor))
                    return false;
                acquiredMask |= PdaProjectionWriteMaskTelemetryCursor;
                if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionTuningHandle, PdaProjectionTuningBufferId, 1, out tuning))
                    return false;
                acquiredMask |= PdaProjectionWriteMaskTuning;
                if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionProfileHandle, PdaProjectionProfilesBufferId, PdaProjectionInterfaceProfileCapacity, out profiles))
                    return false;
                acquiredMask |= PdaProjectionWriteMaskProfiles;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    ReleasePdaProjectionAcquiredBuffers(acquiredMask);
            }
        }

        private bool TryAcquirePdaProjectionVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.UI, out buffer))
            {
                buffer = default;
                return false;
            }

            if (!vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            vault.ReleaseWriteLock(in handle, SystemID.UI);
            buffer = default;
            return false;
        }

        private void ReleasePdaProjectionAcquiredBuffers(uint acquiredMask)
        {
            IDataVault vault = _vault;
            if (vault == null || acquiredMask == 0u)
                return;

            if ((acquiredMask & PdaProjectionWriteMaskProfiles) != 0u)
                vault.ReleaseWriteLock(in _pdaProjectionProfileHandle, SystemID.UI);
            if ((acquiredMask & PdaProjectionWriteMaskTuning) != 0u)
                vault.ReleaseWriteLock(in _pdaProjectionTuningHandle, SystemID.UI);
            if ((acquiredMask & PdaProjectionWriteMaskTelemetryCursor) != 0u)
                vault.ReleaseWriteLock(in _pdaProjectionTelemetryCursorHandle, SystemID.UI);
            if ((acquiredMask & PdaProjectionWriteMaskTelemetry) != 0u)
                vault.ReleaseWriteLock(in _pdaProjectionTelemetryHandle, SystemID.UI);
            if ((acquiredMask & PdaProjectionWriteMaskInput) != 0u)
                vault.ReleaseWriteLock(in _pdaProjectionInputHandle, SystemID.UI);
            if ((acquiredMask & PdaProjectionWriteMaskState) != 0u)
                vault.ReleaseWriteLock(in _pdaProjectionStateHandle, SystemID.UI);
        }

        private bool TryReadOnlyPdaProjectionVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            if (_vault == null ||
                _vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !_vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength ||
                _vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void SeedPdaProjectionTuning(NativeArray<PdaProjectionTuningDTO> tuning)
        {
            ref PdaProjectionTuningDTO row = ref ResolvePdaProjectionElementRef(tuning, 0);
            float width = math.max(0.01f, pdaProjectionWidthMeters);
            float height = math.max(0.01f, pdaProjectionHeightMeters);
            row.Params0 = MakeFloat4(width, height, math.rcp(width), math.rcp(height));
            row.Params1 = MakeFloat4(
                pdaProjectionQualityOverride01,
                math.clamp(pdaProjectionGlassRefractionIndex, 1f, 1.8f),
                math.saturate(pdaProjectionScreenCurvatureScalar),
                0f);
            row.AtlasFallbackRect = MakeFloat4(0f, 0f, 1f, 1f);
            row.VisualParams = MakeFloat4(baseIntensity, 0f, 0f, 0f);
        }

        private void SeedDefaultPdaInterfaceProfiles(NativeArray<PdaInterfaceProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            ref PdaInterfaceProfileDTO profile = ref ResolvePdaProjectionElementRef(profiles, 0);
            profile.UvRect = MakeFloat4(0f, 0f, 1f, 1f);
            profile.TabHashID = 0u;
            profile.Flags = 1u;
        }

        private static void ClearPdaProjectionTelemetry(
            NativeArray<PdaProjectionTelemetryEntry> telemetry,
            NativeArray<int> cursor)
        {
            if (cursor.IsCreated && cursor.Length > 0)
                cursor[0] = 0;
            if (!telemetry.IsCreated)
                return;

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
        }

        private bool EnsurePdaProjectionGraphicsBuffers()
        {
            if (!SupportsPdaProjectionGraphicsPath())
            {
                ReleasePdaProjectionGraphicsBuffers();
                _pdaProjectionGraphicsPathSupported = false;
                return false;
            }

            _pdaProjectionGraphicsPathSupported = true;
            EnsurePdaProjectionBuffer(ref _pdaProjectionStateBufferA, PdaProjectionStateCapacity, UnsafeUtility.SizeOf<PdaStateDTO>(), GraphicsBuffer.Target.Structured);
            EnsurePdaProjectionBuffer(ref _pdaProjectionStateBufferB, PdaProjectionStateCapacity, UnsafeUtility.SizeOf<PdaStateDTO>(), GraphicsBuffer.Target.Structured);
            EnsurePdaProjectionBuffer(ref _pdaProjectionGlobalsBufferA, 1, PdaProjectionGlobalsStrideBytes, GraphicsBuffer.Target.Constant);
            EnsurePdaProjectionBuffer(ref _pdaProjectionGlobalsBufferB, 1, PdaProjectionGlobalsStrideBytes, GraphicsBuffer.Target.Constant);
            _pdaProjectionGraphicsBuffersReady =
                IsValidPdaProjectionBuffer(_pdaProjectionStateBufferA, 1) &&
                IsValidPdaProjectionBuffer(_pdaProjectionStateBufferB, 1) &&
                IsValidPdaProjectionBuffer(_pdaProjectionGlobalsBufferA, 1) &&
                IsValidPdaProjectionBuffer(_pdaProjectionGlobalsBufferB, 1);
            return _pdaProjectionGraphicsBuffersReady;
        }

        private static bool SupportsPdaProjectionGraphicsPath()
        {
            return SystemInfo.supportsSetConstantBuffer &&
                   SystemInfo.graphicsShaderLevel >= PdaProjectionMinimumShaderLevel;
        }

        private static void EnsurePdaProjectionBuffer(ref GraphicsBuffer buffer, int count, int stride, GraphicsBuffer.Target target)
        {
            if (buffer != null && buffer.IsValid() && buffer.count == count && buffer.stride == stride)
                return;

            ReleaseGraphicsBuffer(ref buffer);
            buffer = new GraphicsBuffer(
                target,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride); // COLD ALLOC: GraphicsBuffer - double-buffered screen-space PDA projection payload - owner: WristHologramHudRuntime
        }

        private static bool IsValidPdaProjectionBuffer(GraphicsBuffer buffer, int requiredCount)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= requiredCount;
        }

        private void ReleasePdaProjectionGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _pdaProjectionStateBufferA);
            ReleaseGraphicsBuffer(ref _pdaProjectionStateBufferB);
            ReleaseGraphicsBuffer(ref _pdaProjectionGlobalsBufferA);
            ReleaseGraphicsBuffer(ref _pdaProjectionGlobalsBufferB);
            _pdaProjectionActiveStateBuffer = null;
            _pdaProjectionActiveGlobalsBuffer = null;
            _pdaProjectionGraphicsPathSupported = false;
            _pdaProjectionGraphicsBuffersReady = false;
            _pdaProjectionGpuPayloadValid = false;
        }

        private bool BuildPdaProjectionInput(NativeArray<PdaProjectionInputDTO> inputs, NativeArray<PdaProjectionTuningDTO> tuning)
        {
            if (!inputs.IsCreated || inputs.Length <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            PdaProjectionTuningDTO tuningRow = tuning[0];
            uint activeFlag = ResolvePdaProjectionActive01() > 0.001f ? PdaProjectionFlagActive : 0u;
            uint qualityOverrideFlag = pdaProjectionQualityOverride01 >= 0f ? PdaProjectionFlagQualityOverride : 0u;
            float quality = ResolvePdaProjectionQuality01(in tuningRow);
            uint flags = activeFlag | qualityOverrideFlag | _pdaProjectionFlags;

            if (enableMockWristProjection && AllowPdaProjectionMockSource())
            {
                double3 cameraAup = ResolveCameraAupAbsoluteDouble3();
                inputs[0] = BuildMockPdaProjectionInput(
                    cameraAup,
                    (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                    _pdaProjectionActiveTabHash,
                    _pdaProjectionBoot01,
                    math.max(0.01f, tuningRow.Params0.x),
                    math.max(0.01f, tuningRow.Params0.y),
                    MakeFloat3(pdaProjectionLocalXOffsetMeters, pdaProjectionLocalYOffsetMeters, pdaProjectionLocalZOffsetMeters),
                    tuningRow.Params1.y,
                    tuningRow.Params1.z,
                    quality,
                    flags | PdaProjectionFlagMockSource);
                return true;
            }

            if (!TryBuildRealPdaProjectionInput(out PdaProjectionInputDTO input))
                return false;

            input.ActiveTabHashID = _pdaProjectionActiveTabHash;
            input.BootSequenceProgress01 = _pdaProjectionBoot01;
            input.ScreenWidthMeters = math.max(0.01f, tuningRow.Params0.x);
            input.ScreenHeightMeters = math.max(0.01f, tuningRow.Params0.y);
            input.LocalScreenOffset = MakeFloat3(pdaProjectionLocalXOffsetMeters, pdaProjectionLocalYOffsetMeters, pdaProjectionLocalZOffsetMeters);
            input.GlassRefractionIndex = tuningRow.Params1.y;
            input.ScreenCurvatureScalar = tuningRow.Params1.z;
            input.GlobalQualityWeight01 = quality;
            input.PdaFlags = flags;
            inputs[0] = input;
            return true;
        }

        private static PdaProjectionInputDTO BuildMockPdaProjectionInput(
            double3 cameraAup,
            float timeSeconds,
            uint activeTabHashID,
            float bootSequenceProgress01,
            float screenWidthMeters,
            float screenHeightMeters,
            float3 localScreenOffset,
            float glassRefractionIndex,
            float screenCurvatureScalar,
            float globalQualityWeight01,
            uint flags)
        {
            float t = timeSeconds;
            float orbit = TriangleWaveSigned(t * 0.1162f) * 0.035f;
            float3 local = MakeFloat3(
                orbit,
                -0.08f + TriangleWaveSigned(t * 0.1862f + 0.19f) * 0.018f,
                0.54f + TriangleWaveSigned(t * 0.1448f + 0.25f) * 0.025f);
            quaternion rotation = quaternion.EulerXYZ(
                math.radians(-18f + TriangleWaveSigned(t * 0.1003f) * 8f),
                math.radians(4f + TriangleWaveSigned(t * 0.0653f + 0.31f) * 12f),
                math.radians(TriangleWaveSigned(t * 0.1385f + 0.07f) * 7f));
            PdaProjectionInputDTO input = default;
            input.WristAup = cameraAup + MakeDouble3(local.x, local.y, local.z);
            input.CameraAup = cameraAup;
            input.WristRotation = rotation.value;
            input.LocalScreenOffset = localScreenOffset;
            input.ScreenWidthMeters = screenWidthMeters;
            input.ScreenHeightMeters = screenHeightMeters;
            input.BootSequenceProgress01 = bootSequenceProgress01;
            input.ActiveTabHashID = activeTabHashID;
            input.PdaFlags = flags;
            input.GlassRefractionIndex = glassRefractionIndex;
            input.ScreenCurvatureScalar = screenCurvatureScalar;
            input.GlobalQualityWeight01 = globalQualityWeight01;
            return input;
        }

        private bool TryBuildRealPdaProjectionInput(out PdaProjectionInputDTO input)
        {
            input = default;
            Camera camera = ResolveRenderCamera();
            if (camera == null || camera.transform == null)
                return false;

            Transform wrist = leftWristAnchor != null ? leftWristAnchor : transform;
            if (wrist == null)
                return false;

            if (!TryResolveRuntimeAup(camera.transform.position, out AbsoluteUniversePosition cameraAup) ||
                !TryResolveRuntimeAup(wrist.position, out AbsoluteUniversePosition wristAup))
            {
                return false;
            }

            Quaternion rotation = wrist.rotation;
            input.CameraAup = cameraAup.ToAbsoluteDouble3();
            input.WristAup = wristAup.ToAbsoluteDouble3();
            input.WristRotation = MakeFloat4(rotation.x, rotation.y, rotation.z, rotation.w);
            return true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            return RuntimeOriginRoute.TryRuntimePositionToAup(runtimePosition, ref aup) && aup.IsFinite();
        }

        private double3 ResolveCameraAupAbsoluteDouble3()
        {
            Camera camera = ResolveRenderCamera();
            if (camera != null && camera.transform != null && TryResolveRuntimeAup(camera.transform.position, out AbsoluteUniversePosition cameraAup))
                return cameraAup.ToAbsoluteDouble3();

            AbsoluteUniversePosition origin = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return origin.IsFinite() ? origin.ToAbsoluteDouble3() : double3.zero;
        }

        private float ResolvePdaProjectionActive01()
        {
            bool visible = forcePdaProjectionVisible || (enableMockWristProjection && AllowPdaProjectionMockSource()) || _latestPdaSignal.IsOpen != 0u || _pdaProjectionBoot01 > 0.001f;
            return visible ? 1f : 0f;
        }

        private static bool AllowPdaProjectionMockSource()
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private float ResolvePdaProjectionQuality01(in PdaProjectionTuningDTO tuning)
        {
            float override01 = tuning.Params1.x;
            float baseQuality = math.saturate(_cachedQualityWeight01);
            return math.saturate(math.select(baseQuality, override01, math.isfinite(override01) && override01 >= 0f));
        }

        private void UploadPdaProjectionGpu(
            in PdaStateDTO state,
            in PdaProjectionGlobalsDTO globals)
        {
            GraphicsBuffer stateWrite = ReferenceEquals(_pdaProjectionActiveStateBuffer, _pdaProjectionStateBufferA)
                ? _pdaProjectionStateBufferB
                : _pdaProjectionStateBufferA;
            GraphicsBuffer globalsWrite = ReferenceEquals(_pdaProjectionActiveGlobalsBuffer, _pdaProjectionGlobalsBufferA)
                ? _pdaProjectionGlobalsBufferB
                : _pdaProjectionGlobalsBufferA;

            if (!IsValidPdaProjectionBuffer(stateWrite, 1) || !IsValidPdaProjectionBuffer(globalsWrite, 1))
            {
                _pdaProjectionGpuPayloadValid = false;
                return;
            }

            try
            {
                PdaStateDTO stateCopy = state;
                NativeArray<PdaStateDTO> mappedState = stateWrite.LockBufferForWrite<PdaStateDTO>(0, 1);
                try
                {
                    UnsafeUtility.MemCpy(
                        NativeArrayUnsafeUtility.GetUnsafePtr(mappedState),
                        UnsafeUtility.AddressOf(ref stateCopy),
                        UnsafeUtility.SizeOf<PdaStateDTO>());
                }
                finally
                {
                    stateWrite.UnlockBufferAfterWrite<PdaStateDTO>(1);
                }

                PdaProjectionGlobalsDTO globalsCopy = globals;
                NativeArray<PdaProjectionGlobalsDTO> mappedGlobals = globalsWrite.LockBufferForWrite<PdaProjectionGlobalsDTO>(0, 1);
                try
                {
                    UnsafeUtility.MemCpy(
                        NativeArrayUnsafeUtility.GetUnsafePtr(mappedGlobals),
                        UnsafeUtility.AddressOf(ref globalsCopy),
                        UnsafeUtility.SizeOf<PdaProjectionGlobalsDTO>());
                }
                finally
                {
                    globalsWrite.UnlockBufferAfterWrite<PdaProjectionGlobalsDTO>(1);
                }
            }
            catch (ObjectDisposedException)
            {
                ReportPdaProjectionGpuUploadFaultClosed();
                return;
            }
            catch (InvalidOperationException)
            {
                ReportPdaProjectionGpuUploadFaultClosed();
                return;
            }
            catch (ArgumentException)
            {
                ReportPdaProjectionGpuUploadFaultClosed();
                return;
            }
            catch (NotSupportedException)
            {
                ReportPdaProjectionGpuUploadFaultClosed();
                return;
            }
            catch (UnityException)
            {
                ReportPdaProjectionGpuUploadFaultClosed();
                return;
            }

            _pdaProjectionActiveStateBuffer = stateWrite;
            _pdaProjectionActiveGlobalsBuffer = globalsWrite;
            _pdaProjectionGpuPayloadValid = true;
            _pdaProjectionFlags &= ~PdaProjectionFlagGpuUploadFault;
            _pdaProjectionWriteBufferIndex ^= 1;
            _pdaProjectionGlobalsWriteBufferIndex ^= 1;
        }

        private void ReportPdaProjectionGpuUploadFaultClosed()
        {
            _pdaProjectionGpuPayloadValid = false;
            _pdaProjectionFlags |= PdaProjectionFlagGpuUploadFault;
        }

        private PdaProjectionGlobalsDTO BuildPdaProjectionGlobals(
            NativeArray<PdaProjectionTuningDTO> tuning,
            NativeArray<PdaInterfaceProfileDTO> profiles,
            in PdaStateDTO state)
        {
            PdaProjectionTuningDTO tuningRow = tuning[0];
            float width = math.max(0.01f, tuningRow.Params0.x);
            float height = math.max(0.01f, tuningRow.Params0.y);
            float quality = ResolvePdaProjectionQuality01(in tuningRow);
            float4 atlasRect = ResolvePdaProfileRect(profiles, state.ActiveTabHashID, tuningRow.AtlasFallbackRect);
            PdaProjectionGlobalsDTO globals = default;
            globals.ScreenParams = MakeFloat4(width, height, math.rcp(width), math.rcp(height));
            globals.RefractionParams = MakeFloat4(quality, math.max(1f, tuningRow.Params1.y), math.saturate(tuningRow.Params1.z), state.BootSequenceProgress01);
            globals.AtlasRect = atlasRect;
            globals.VisualParams = MakeFloat4(math.max(0f, tuningRow.VisualParams.x), (float)SystemDispatcher.CurrentUnscaledTimeSeconds, _pdaProjectionCorruption01, 0f);
            return globals;
        }

        private static float4 MakeFloat4(float x, float y, float z, float w)
        {
            float4 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            value.w = w;
            return value;
        }

        private static float4 MakeFloat4(float3 xyz, float w)
        {
            float4 value = default;
            value.x = xyz.x;
            value.y = xyz.y;
            value.z = xyz.z;
            value.w = w;
            return value;
        }

        private static float3 MakeFloat3(float x, float y, float z)
        {
            float3 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        private static double3 MakeDouble3(double x, double y, double z)
        {
            double3 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        private static float4 ResolvePdaProfileRect(NativeArray<PdaInterfaceProfileDTO> profiles, uint tabHash, float4 fallback)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return fallback;

            bool hasDefault = false;
            float4 defaultRect = fallback;
            for (int i = 0; i < profiles.Length; i++)
            {
                PdaInterfaceProfileDTO profile = profiles[i];
                if ((profile.Flags & 1u) == 0u)
                    break;

                if (profile.TabHashID == tabHash)
                    return profile.UvRect;
                if (profile.TabHashID == 0u && !hasDefault)
                {
                    defaultRect = profile.UvRect;
                    hasDefault = true;
                }
            }

            return defaultRect;
        }

        private static void PatchPdaProjectionTelemetryJobCost(
            NativeArray<PdaProjectionTelemetryEntry> telemetry,
            NativeArray<int> cursor,
            uint elapsedQ16)
        {
            if (!telemetry.IsCreated ||
                telemetry.Length <= 0 ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
                return;

            int index = cursor[0] - 1;
            if (index < 0)
                index += telemetry.Length;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            PdaProjectionTelemetryEntry entry = telemetry[index];
            entry.JobMicrosecondsQ16 = elapsedQ16;
            if (elapsedQ16 > (uint)(PdaProjectionBudgetMicroseconds * 16f))
                entry.Flags |= PdaProjectionFlagOverBudget;
            telemetry[index] = entry;
        }

#if UNITY_EDITOR
        private bool TryLoadPdaInterfaceProfilesCold()
        {
            if (_pdaProjectionProfilesLoaded)
            {
                return false;
            }

            string path = ResolvePdaProfileCsvPath();
            if (string.IsNullOrEmpty(path))
                return false;
            if (!File.Exists(path))
                return false;

            Span<byte> csvScratch = stackalloc byte[PdaProjectionCsvScratchBytes];
            int byteCount = TryReadPdaProfileCsvBytes(path, csvScratch);
            if (byteCount <= 0)
                return false;

            ReadOnlySpan<byte> span = csvScratch.Slice(0, byteCount);
            Span<PdaInterfaceProfileDTO> parsed = stackalloc PdaInterfaceProfileDTO[PdaProjectionInterfaceProfileCapacity];
            int cursor = 0;
            int written = 0;
            while (cursor < span.Length && written < parsed.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(span, ref cursor);
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if (!TryParsePdaProfileCsvLine(line, out PdaInterfaceProfileDTO profile))
                    continue;

                parsed[written++] = profile;
            }

            if (written <= 0)
                return false;

            if (!TryAcquirePdaProjectionVaultBuffer(in _pdaProjectionProfileHandle, PdaProjectionProfilesBufferId, PdaProjectionInterfaceProfileCapacity, out NativeArray<PdaInterfaceProfileDTO> profiles))
                return false;

            try
            {
                int copyCount = math.min(written, profiles.Length);
                for (int i = 0; i < copyCount; i++)
                    profiles[i] = parsed[i];
                for (int i = copyCount; i < profiles.Length; i++)
                    profiles[i] = default;

                _pdaProjectionProfilesLoaded = copyCount > 0;
                return _pdaProjectionProfilesLoaded;
            }
            finally
            {
                _vault?.ReleaseWriteLock(in _pdaProjectionProfileHandle, SystemID.UI);
            }
        }

        private string ResolvePdaProfileCsvPath()
        {
            string projectPath = Path.Combine(GetProjectRoot(), "Assets", "_Project", "Data", "UI", PdaProjectionProfilesCsvFileName);
            if (File.Exists(projectPath))
                return projectPath;

            return string.Empty;
        }

        private static int TryReadPdaProfileCsvBytes(string path, Span<byte> destination)
        {
            if (destination.Length <= 0)
                return 0;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamBytes = stream.Length;
                    if (streamBytes < 0L)
                        streamBytes = 0L;

                    long boundedBytes = streamBytes < destination.Length ? streamBytes : destination.Length;
                    int targetBytes = (int)boundedBytes;
                    Span<byte> target = destination.Slice(0, targetBytes);
                    int total = 0;
                    while (total < targetBytes)
                    {
                        int read = stream.Read(target.Slice(total));
                        if (read <= 0)
                            break;
                        total += read;
                    }

                    return total;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (NotSupportedException)
            {
                return 0;
            }
        }

        private static bool TryParsePdaProfileCsvLine(ReadOnlySpan<byte> line, out PdaInterfaceProfileDTO profile)
        {
            profile = default;
            if (line.Length >= 3 && line[0] == 0xEF && line[1] == 0xBB && line[2] == 0xBF)
                line = line.Slice(3);

            int cursor = 0;
            ReadOnlySpan<byte> name = ReadCsvToken(line, ref cursor);
            if (name.Length == 0 ||
                !TryReadCsvFloat(line, ref cursor, out float u) ||
                !TryReadCsvFloat(line, ref cursor, out float v) ||
                !TryReadCsvFloat(line, ref cursor, out float w) ||
                !TryReadCsvFloat(line, ref cursor, out float h))
            {
                return false;
            }

            profile.TabHashID = ResolvePdaProfileTabHash(name);
            profile.UvRect = MakeFloat4(math.saturate(u), math.saturate(v), math.saturate(w), math.saturate(h));
            profile.Flags = 1u;
            return true;
        }

        private static uint ResolvePdaProfileTabHash(ReadOnlySpan<byte> token)
        {
            token = TrimAscii(token);
            if (token.Length == 0 ||
                EqualsAsciiLower(token, "default") ||
                EqualsAsciiLower(token, "*"))
            {
                return 0u;
            }

            if (TryParsePdaTabIndexToken(token, out int tabIndex))
                return ResolvePdaTabHash(tabIndex);

            if (EqualsAsciiLower(token, "inventory") || EqualsAsciiLower(token, "inventory_tab") || EqualsAsciiLower(token, "tab_inventory"))
                return ResolvePdaTabHash(0);
            if (EqualsAsciiLower(token, "loadout") || EqualsAsciiLower(token, "loadout_tab") || EqualsAsciiLower(token, "tab_loadout"))
                return ResolvePdaTabHash(1);
            if (EqualsAsciiLower(token, "construction") || EqualsAsciiLower(token, "construction_tab") || EqualsAsciiLower(token, "tab_construction"))
                return ResolvePdaTabHash(2);
            if (EqualsAsciiLower(token, "barter") || EqualsAsciiLower(token, "barter_tab") || EqualsAsciiLower(token, "tab_barter"))
                return ResolvePdaTabHash(3);
            if (EqualsAsciiLower(token, "data_log") || EqualsAsciiLower(token, "datalog") || EqualsAsciiLower(token, "data_log_tab") ||
                EqualsAsciiLower(token, "logbook") || EqualsAsciiLower(token, "logbook_tab") || EqualsAsciiLower(token, "tab_datalog"))
            {
                return ResolvePdaTabHash(4);
            }

            if (EqualsAsciiLower(token, "spectrum") || EqualsAsciiLower(token, "spectrum_tab") || EqualsAsciiLower(token, "tab_spectrum"))
                return ResolvePdaTabHash(5);
            if (EqualsAsciiLower(token, "atlas_signal") || EqualsAsciiLower(token, "atlassignal") || EqualsAsciiLower(token, "atlas_signal_tab") ||
                EqualsAsciiLower(token, "tab_atlassignal"))
            {
                return ResolvePdaTabHash(6);
            }

            if (EqualsAsciiLower(token, "diagnostics") || EqualsAsciiLower(token, "diagnostics_tab") || EqualsAsciiLower(token, "tab_diagnostics"))
                return ResolvePdaTabHash(7);
            if (EqualsAsciiLower(token, "map") || EqualsAsciiLower(token, "map_tab") || EqualsAsciiLower(token, "tab_map"))
                return ResolvePdaTabHash(5);
            if (EqualsAsciiLower(token, "controls") || EqualsAsciiLower(token, "controls_tab") || EqualsAsciiLower(token, "tab_controls"))
                return ResolvePdaTabHash(2);
            if (EqualsAsciiLower(token, "encyclopedia") || EqualsAsciiLower(token, "encyclopedia_tab") || EqualsAsciiLower(token, "tab_encyclopedia"))
                return ResolvePdaTabHash(3);

            return HashFnv1a32(token);
        }

        private static bool TryParsePdaTabIndexToken(ReadOnlySpan<byte> token, out int tabIndex)
        {
            tabIndex = 0;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            if (TryParsePositiveAsciiInt(token, 0, out tabIndex))
                return true;
            if (StartsWithAsciiLower(token, "tab_") && TryParsePositiveAsciiInt(token, 4, out tabIndex))
                return true;
            if (StartsWithAsciiLower(token, "tab") && TryParsePositiveAsciiInt(token, 3, out tabIndex))
                return true;
            if (StartsWithAsciiLower(token, "pda_tab_") && TryParsePositiveAsciiInt(token, 8, out tabIndex))
                return true;
            if (StartsWithAsciiLower(token, "pda_tab") && TryParsePositiveAsciiInt(token, 7, out tabIndex))
                return true;

            return false;
        }

        private static bool TryParsePositiveAsciiInt(ReadOnlySpan<byte> token, int start, out int value)
        {
            value = 0;
            if ((uint)start >= (uint)token.Length)
                return false;

            int result = 0;
            for (int i = start; i < token.Length; i++)
            {
                byte c = token[i];
                if (c < '0' || c > '9')
                    return false;

                result = result * 10 + (c - '0');
                if (result > 1024)
                    return false;
            }

            value = result;
            return true;
        }

        private static bool StartsWithAsciiLower(ReadOnlySpan<byte> value, string prefix)
        {
            if (value.Length < prefix.Length)
                return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                byte c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (byte)(c + 32);
                if (c != (byte)prefix[i])
                    return false;
            }

            return true;
        }

        private static bool EqualsAsciiLower(ReadOnlySpan<byte> value, string expected)
        {
            if (value.Length != expected.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (byte)(c + 32);
                if (c != (byte)expected[i])
                    return false;
            }

            return true;
        }

        private static uint HashFnv1a32(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 2166136261u : hash;
        }
#endif

        private void DumpPdaProjectionBlackBoxOnce(uint flags)
        {
            if (_pdaProjectionBlackBoxDumped ||
                !TryReadPdaProjectionVaultBuffer(in _pdaProjectionTelemetryHandle, PdaProjectionTelemetryBufferId, PdaProjectionTelemetryCapacity, out NativeArray<PdaProjectionTelemetryEntry> telemetry) ||
                !TryReadPdaProjectionVaultBuffer(in _pdaProjectionTelemetryCursorHandle, PdaProjectionTelemetryCursorBufferId, 1, out NativeArray<int> cursor))
            {
                return;
            }

            int entrySize = UnsafeUtility.SizeOf<PdaProjectionTelemetryEntry>();
            int validCount = CountValidPdaProjectionTelemetryRows(telemetry);
            int startIndex = ResolvePdaProjectionTelemetryStartIndex(cursor[0], telemetry.Length, validCount);
            int payloadBytes = validCount * entrySize;
            int telemetryCapacity = telemetry.Length;
            int telemetryCursor = cursor[0];
            PdaProjectionBlackBoxDumpHeader header = default;
            header.Magic = PdaProjectionBlackBoxMagic;
            header.Version = PdaProjectionBlackBoxVersion;
            header.FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            header.Flags = flags;
            header.TelemetryCapacity = telemetryCapacity;
            header.TelemetryCursor = telemetryCursor;
            header.TelemetryEntrySizeBytes = entrySize;
            header.PayloadBytes = payloadBytes;
            header.TelemetryValidCount = validCount;
            header.TelemetryStartIndex = startIndex;
            telemetry = default;
            cursor = default;

            _pdaProjectionBlackBoxDumped = true;
            try
            {
                string directory = ResolvePdaProjectionDumpDirectory();
                if (string.IsNullOrEmpty(directory))
                    return;

                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_1309_UIPresentation_PdaProjection.bin");
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(MemoryMarshal.CreateReadOnlySpan(ref UnsafeUtility.AsRef<byte>(&header), UnsafeUtility.SizeOf<PdaProjectionBlackBoxDumpHeader>()));
                    WritePdaProjectionTelemetryDump(stream, startIndex, validCount, entrySize);
                }
            }
            catch (IOException)
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_348 projection dump failed.");
            }
            catch (UnauthorizedAccessException)
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_348 projection dump failed.");
            }
            catch (ObjectDisposedException)
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_348 projection dump failed.");
            }
            catch (InvalidOperationException)
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_348 projection dump failed.");
            }
            catch (ArgumentException)
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_348 projection dump failed.");
            }
            catch (NotSupportedException)
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_348 projection dump failed.");
            }
        }

        private static string ResolvePdaProjectionDumpDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(ResolveProjectRoot(), "Docs", "AgentLogs");
#else
            string persistentRoot = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentRoot))
                return string.Empty;

            return Path.Combine(persistentRoot, "Hecton8", "AgentLogs");
#endif
        }

        private static int CountValidPdaProjectionTelemetryRows(NativeArray<PdaProjectionTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < telemetry.Length; i++)
            {
                PdaProjectionTelemetryEntry entry = telemetry[i];
                if (entry.ActiveTabHashID != 0u || entry.FrameIndex != 0u || entry.Flags != 0u)
                    count++;
            }

            return count;
        }

        private static int ResolvePdaProjectionTelemetryStartIndex(int cursor, int capacity, int validCount)
        {
            if (capacity <= 0 || validCount <= 0 || validCount < capacity)
                return 0;
            if (cursor <= 0)
                return 0;
            return cursor >= capacity ? 0 : cursor;
        }

        private void WritePdaProjectionTelemetryDump(
            FileStream stream,
            int startIndex,
            int validCount,
            int entrySize)
        {
            if (stream == null || validCount <= 0 || entrySize <= 0)
                return;

            for (int i = 0; i < validCount; i++)
            {
                int sourceIndex = startIndex + i;
                if (sourceIndex >= PdaProjectionTelemetryCapacity)
                    sourceIndex -= PdaProjectionTelemetryCapacity;

                if (!TryReadPdaProjectionTelemetryRow(sourceIndex, out PdaProjectionTelemetryEntry row))
                    return;

                stream.Write(MemoryMarshal.CreateReadOnlySpan(ref UnsafeUtility.AsRef<byte>(&row), entrySize));
            }
        }

        private bool TryReadPdaProjectionTelemetryRow(int index, out PdaProjectionTelemetryEntry row)
        {
            row = default;
            if ((uint)index >= PdaProjectionTelemetryCapacity ||
                !TryReadOnlyPdaProjectionVaultBuffer(in _pdaProjectionTelemetryHandle, PdaProjectionTelemetryBufferId, PdaProjectionTelemetryCapacity, out NativeArray<PdaProjectionTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            row = telemetry[index];
            return true;
        }

        void IPDAEventListener.OnPDAEvent(in PDAEventPayload payload)
        {
            PDAEventType eventType = (PDAEventType)payload.EventType;
            if (eventType == PDAEventType.Opened)
            {
                _latestPdaSignal.IsOpen = 1u;
                _latestPdaSignal.ActiveTab = payload.CurrentTab;
                _pdaProjectionActiveTabHash = ResolvePdaTabHash(payload.CurrentTab);
                return;
            }

            if (eventType == PDAEventType.Closed)
            {
                _latestPdaSignal.IsOpen = 0u;
                return;
            }

            if (eventType == PDAEventType.TabChanged)
            {
                _latestPdaSignal.ActiveTab = payload.CurrentTab;
                _pdaProjectionActiveTabHash = ResolvePdaTabHash(payload.CurrentTab);
            }
        }

        void IPDAIntrusionEventListener.OnPDAIntrusionEvent(in PDAIntrusionEventPayload payload)
        {
            if ((PDAIntrusionEventType)payload.EventType == PDAIntrusionEventType.RebootCompleted)
            {
                _pdaProjectionFlags &= ~PdaProjectionFlagIntrusion;
                _pdaProjectionCorruption01 = 0f;
            }
        }

        private static uint ResolvePdaTabHash(int tab)
        {
            uint value = unchecked((uint)math.max(0, tab));
            return unchecked(0x50444100u ^ (value + 1u) * 16777619u);
        }

        public static bool TryGetActivePdaProjectionResources(
            out GraphicsBuffer stateBuffer,
            out GraphicsBuffer globalsBuffer,
            out Texture atlasTexture)
        {
            stateBuffer = null;
            globalsBuffer = null;
            atlasTexture = null;
            WristHologramHudRuntime runtime = s_activePdaProjectorRuntime;
            if (runtime == null ||
                !runtime._pdaProjectionGraphicsPathSupported ||
                !runtime._pdaProjectionGraphicsBuffersReady ||
                !runtime._pdaProjectionGpuPayloadValid ||
                runtime._pdaProjectionActiveStateBuffer == null ||
                runtime._pdaProjectionActiveGlobalsBuffer == null ||
                !runtime._pdaProjectionActiveStateBuffer.IsValid() ||
                !runtime._pdaProjectionActiveGlobalsBuffer.IsValid())
            {
                return false;
            }

            stateBuffer = runtime._pdaProjectionActiveStateBuffer;
            globalsBuffer = runtime._pdaProjectionActiveGlobalsBuffer;
            atlasTexture = runtime.pdaInterfaceAtlas != null ? runtime.pdaInterfaceAtlas : runtime.fontAtlasTexture != null ? runtime.fontAtlasTexture : Texture2D.whiteTexture;
            return true;
        }

        public static bool TryGetActivePdaProjectionTuning(out PdaProjectionTuningDTO tuning)
        {
            tuning = default;
            WristHologramHudRuntime runtime = s_activePdaProjectorRuntime;
            if (runtime == null ||
                !runtime.TryReadOnlyPdaProjectionVaultBuffer(in runtime._pdaProjectionTuningHandle, PdaProjectionTuningBufferId, 1, out NativeArray<PdaProjectionTuningDTO>.ReadOnly buffer))
            {
                return false;
            }

            tuning = buffer[0];
            return true;
        }

#if UNITY_EDITOR
        public static bool TrySetActivePdaProjectionTuning(float glassRefractionIndex, float screenCurvatureScalar, float qualityOverride01)
        {
            WristHologramHudRuntime runtime = s_activePdaProjectorRuntime;
            if (runtime == null ||
                !runtime.TryResolvePdaProjectionVaultBuffer(in runtime._pdaProjectionTuningHandle, PdaProjectionTuningBufferId, 1, out NativeArray<PdaProjectionTuningDTO> buffer))
            {
                return false;
            }

            ref PdaProjectionTuningDTO tuning = ref ResolvePdaProjectionElementRef(buffer, 0);
            tuning.Params1.y = math.clamp(glassRefractionIndex, 1f, 1.8f);
            tuning.Params1.z = math.saturate(screenCurvatureScalar);
            tuning.Params1.x = math.clamp(qualityOverride01, -1f, 1f);
            runtime.pdaProjectionGlassRefractionIndex = tuning.Params1.y;
            runtime.pdaProjectionScreenCurvatureScalar = tuning.Params1.z;
            runtime.pdaProjectionQualityOverride01 = tuning.Params1.x;
            return true;
        }
#endif

        public static bool TryGetActivePdaProjectionTelemetry(
            out NativeArray<PdaProjectionTelemetryEntry>.ReadOnly telemetry,
            out int cursor)
        {
            telemetry = default;
            cursor = 0;
            WristHologramHudRuntime runtime = s_activePdaProjectorRuntime;
            if (runtime == null ||
                !runtime.TryReadOnlyPdaProjectionVaultBuffer(in runtime._pdaProjectionTelemetryHandle, PdaProjectionTelemetryBufferId, PdaProjectionTelemetryCapacity, out telemetry) ||
                !runtime.TryReadOnlyPdaProjectionVaultBuffer(in runtime._pdaProjectionTelemetryCursorHandle, PdaProjectionTelemetryCursorBufferId, 1, out NativeArray<int>.ReadOnly cursorBuffer))
            {
                return false;
            }

            cursor = cursorBuffer[0];
            return true;
        }

        private static ref T ResolvePdaProjectionElementRef<T>(NativeArray<T> buffer, int index) where T : unmanaged
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            return ref UnsafeUtility.AsRef<T>((byte*)basePtr + index * UnsafeUtility.SizeOf<T>());
        }

#if UNITY_EDITOR
        private void PdaProjectorOnDrawGizmosSelected()
        {
            if (!TryReadOnlyPdaProjectionVaultBuffer(in _pdaProjectionStateHandle, PdaProjectionStateBufferId, 1, out NativeArray<PdaStateDTO>.ReadOnly states))
                return;

            Camera camera = ResolveRenderCamera();
            if (camera == null)
                return;

            PdaStateDTO state = states[0];
            Matrix4x4 matrix = ToMatrix4x4(state.LocalToWorld);
            Vector3 cameraPosition = camera.transform.position;
            Vector4 cameraRelativeCenter = matrix.GetColumn(3);
            Vector3 worldCenter = cameraPosition + new Vector3(cameraRelativeCenter.x, cameraRelativeCenter.y, cameraRelativeCenter.z);
            matrix.SetColumn(3, new Vector4(worldCenter.x, worldCenter.y, worldCenter.z, 1f));
            Gizmos.color = new Color(0.1f, 1f, 0.42f, 0.9f);
            Gizmos.matrix = matrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(math.max(0.01f, pdaProjectionWidthMeters), math.max(0.01f, pdaProjectionHeightMeters), 0.012f));
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = new Color(1f, 0.82f, 0.1f, 0.9f);
            Gizmos.DrawLine(cameraPosition, worldCenter);
        }

        private void PdaProjectorOnValidate()
        {
            pdaProjectionWidthMeters = math.clamp(pdaProjectionWidthMeters, 0.08f, 0.34f);
            pdaProjectionHeightMeters = math.clamp(pdaProjectionHeightMeters, 0.05f, 0.22f);
            pdaProjectionGlassRefractionIndex = math.clamp(pdaProjectionGlassRefractionIndex, 1f, 1.8f);
            pdaProjectionScreenCurvatureScalar = math.saturate(pdaProjectionScreenCurvatureScalar);
            pdaProjectionQualityOverride01 = math.clamp(pdaProjectionQualityOverride01, -1f, 1f);
            pdaProjectionBootRate = math.clamp(pdaProjectionBootRate, 0.1f, 16f);
        }
#else
        private void PdaProjectorOnValidate()
        {
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockWristMatricesJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<PdaProjectionInputDTO> Inputs;
            public double3 CameraAup;
            public float TimeSeconds;
            public uint FrameIndex;
            public uint ActiveTabHashID;
            public float BootSequenceProgress01;
            public float ScreenWidthMeters;
            public float ScreenHeightMeters;
            public float3 LocalScreenOffset;
            public float GlassRefractionIndex;
            public float ScreenCurvatureScalar;
            public float GlobalQualityWeight01;
            public uint Flags;

            public void Execute(int index)
            {
                float t = TimeSeconds + index * 0.17320508f;
                float orbit = TriangleWaveSigned(t * 0.1162f) * 0.035f;
                float3 local = MakeFloat3(
                    orbit,
                    -0.08f + TriangleWaveSigned(t * 0.1862f + 0.19f) * 0.018f,
                    0.54f + TriangleWaveSigned(t * 0.1448f + 0.25f) * 0.025f);
                quaternion rotation = quaternion.EulerXYZ(
                    math.radians(-18f + TriangleWaveSigned(t * 0.1003f) * 8f),
                    math.radians(4f + TriangleWaveSigned(t * 0.0653f + 0.31f) * 12f),
                    math.radians(TriangleWaveSigned(t * 0.1385f + 0.07f) * 7f));
                PdaProjectionInputDTO input = default;
                input.WristAup = CameraAup + MakeDouble3(local.x, local.y, local.z);
                input.CameraAup = CameraAup;
                input.WristRotation = rotation.value;
                input.LocalScreenOffset = LocalScreenOffset;
                input.ScreenWidthMeters = ScreenWidthMeters;
                input.ScreenHeightMeters = ScreenHeightMeters;
                input.BootSequenceProgress01 = BootSequenceProgress01;
                input.ActiveTabHashID = ActiveTabHashID;
                input.PdaFlags = Flags;
                input.GlassRefractionIndex = GlassRefractionIndex;
                input.ScreenCurvatureScalar = ScreenCurvatureScalar;
                input.GlobalQualityWeight01 = GlobalQualityWeight01;
                Inputs[index] = input;
            }

            private static float TriangleWaveSigned(float phase)
            {
                return math.abs(math.frac(phase) * 2f - 1f) * 2f - 1f;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CompilePdaMatricesJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<PdaProjectionInputDTO> Inputs;
            [NoAlias] public NativeArray<PdaStateDTO> States;
            [NoAlias] public NativeArray<PdaProjectionTelemetryEntry> Telemetry;
            [NoAlias] public NativeArray<int> TelemetryCursor;
            public uint FrameIndex;

            public void Execute()
            {
                if (!Inputs.IsCreated || Inputs.Length <= 0 || !States.IsCreated || States.Length <= 0)
                    return;

                ref readonly PdaProjectionInputDTO input = ref ReadOnlyElementRef(Inputs, 0);
                uint flags = input.PdaFlags;
                quaternion rotation = NormalizeQuaternion(input.WristRotation, out uint rotationFlags);
                flags |= rotationFlags;

                float3 localDelta = AupPrecisionMath.LocalDeltaFloat3(
                    input.WristAup,
                    input.CameraAup,
                    MakeFloat3(0f, -0.08f, 0.54f));
                if (!math.all(math.isfinite(localDelta)))
                {
                    localDelta = MakeFloat3(0f, -0.08f, 0.54f);
                    flags |= PdaProjectionFlagNonFinite;
                }

                float3 rightFallback = MakeFloat3(1f, 0f, 0f);
                float3 upFallback = MakeFloat3(0f, 1f, 0f);
                float3 forwardFallback = MakeFloat3(0f, 0f, 1f);
                float3 right = AupPrecisionMath.SafeNormalize(math.mul(rotation, rightFallback), rightFallback);
                float3 up = AupPrecisionMath.SafeNormalize(math.mul(rotation, upFallback), upFallback);
                float3 forward = AupPrecisionMath.SafeNormalize(math.mul(rotation, forwardFallback), forwardFallback);
                float3 center = localDelta + right * input.LocalScreenOffset.x + up * input.LocalScreenOffset.y + forward * input.LocalScreenOffset.z;
                if (!math.all(math.isfinite(center)))
                {
                    center = MakeFloat3(0f, -0.08f, 0.54f);
                    flags |= PdaProjectionFlagNonFinite;
                }

                float4x4 matrix = default;
                matrix.c0 = MakeFloat4(right, 0f);
                matrix.c1 = MakeFloat4(up, 0f);
                matrix.c2 = MakeFloat4(forward, 0f);
                matrix.c3 = MakeFloat4(center, 1f);

                ref PdaStateDTO state = ref ElementRef(States, 0);
                state.LocalToWorld = matrix;
                state.ActiveTabHashID = input.ActiveTabHashID;
                state.BootSequenceProgress01 = math.saturate(input.BootSequenceProgress01);
                state.PdaFlags = flags;

                if (Telemetry.IsCreated && Telemetry.Length > 0 && TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                {
                    ref int cursor = ref ElementRef(TelemetryCursor, 0);
                    int index = cursor;
                    if ((uint)index >= (uint)Telemetry.Length)
                        index = 0;
                    cursor = index + 1;

                    ref PdaProjectionTelemetryEntry entry = ref ElementRef(Telemetry, index);
                    entry.FrameIndex = FrameIndex;
                    entry.Flags = flags;
                    entry.ActiveTabHashID = input.ActiveTabHashID;
                    entry.JobMicrosecondsQ16 = 0u;
                    entry.LocalizedDistanceMeters = math.length(center);
                    entry.BootSequenceProgress01 = math.saturate(input.BootSequenceProgress01);
                    entry.QualityWeight01 = math.saturate(input.GlobalQualityWeight01);
                    entry.TelemetryCursor = (uint)cursor;
                    entry.MatrixHash = HashMatrix(matrix);
                    entry.ProfileHash = input.ActiveTabHashID;
                    entry.ScreenWidthMeters = math.max(0.01f, input.ScreenWidthMeters);
                    entry.ScreenHeightMeters = math.max(0.01f, input.ScreenHeightMeters);
                    entry.GlassRefractionIndex = math.max(1f, input.GlassRefractionIndex);
                    entry.ScreenCurvatureScalar = math.saturate(input.ScreenCurvatureScalar);
                    entry.GlobalQualityWeight01 = math.saturate(input.GlobalQualityWeight01);
                    entry.PdaFlags = flags;
                }
            }

            private static quaternion NormalizeQuaternion(float4 value, out uint flags)
            {
                flags = 0u;
                float lengthSq = math.lengthsq(value);
                if (!math.isfinite(lengthSq) || lengthSq < 0.000001f)
                {
                    flags |= PdaProjectionFlagNonFinite;
                    return quaternion.identity;
                }

                quaternion normalized = default;
                normalized.value = value * math.rsqrt(lengthSq);
                return normalized;
            }

            private static uint HashMatrix(float4x4 matrix)
            {
                uint hash = 2166136261u;
                hash = Mix(hash, math.asuint(matrix.c0.x));
                hash = Mix(hash, math.asuint(matrix.c0.y));
                hash = Mix(hash, math.asuint(matrix.c0.z));
                hash = Mix(hash, math.asuint(matrix.c1.x));
                hash = Mix(hash, math.asuint(matrix.c1.y));
                hash = Mix(hash, math.asuint(matrix.c1.z));
                hash = Mix(hash, math.asuint(matrix.c2.x));
                hash = Mix(hash, math.asuint(matrix.c2.y));
                hash = Mix(hash, math.asuint(matrix.c2.z));
                hash = Mix(hash, math.asuint(matrix.c3.x));
                hash = Mix(hash, math.asuint(matrix.c3.y));
                hash = Mix(hash, math.asuint(matrix.c3.z));
                return hash;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint Mix(uint hash, uint value)
            {
                hash ^= value;
                return hash * 16777619u;
            }

            private static ref readonly T ReadOnlyElementRef<T>(NativeArray<T> buffer, int index) where T : unmanaged
            {
                void* basePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
                return ref UnsafeUtility.AsRef<T>((byte*)basePtr + index * UnsafeUtility.SizeOf<T>());
            }

            private static ref T ElementRef<T>(NativeArray<T> buffer, int index) where T : unmanaged
            {
                void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
                return ref UnsafeUtility.AsRef<T>((byte*)basePtr + index * UnsafeUtility.SizeOf<T>());
            }
        }

    }
}
