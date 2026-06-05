using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>Vault-published VR somatic comfort state. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SomaticComfortStateDTO
    {
        [FieldOffset(0)] public float FovTunnelingIntensity;
        [FieldOffset(4)] public float HorizonLockBlend;
        [FieldOffset(8)] public float FoveatedScaleMultiplier;
        [FieldOffset(12)] public uint ActiveComfortFlags;
        [FieldOffset(16)] public float4 ReservedParameters;
    }

    /// <summary>Designer-authored comfort profile copied into DataVault before gameplay. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VrComfortProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float UserComfortWeight01;
        [FieldOffset(8)] public float FovAggressiveness;
        [FieldOffset(12)] public float HorizonLockSpeed;
        [FieldOffset(16)] public float FoveatedBaseline;
        [FieldOffset(20)] public float AngularVelocitySoftRadS;
        [FieldOffset(24)] public float AngularAccelerationSoftRadS2;
        [FieldOffset(28)] public float LinearAccelerationSoftMps2;
        [FieldOffset(32)] public float EwmaSharpness;
        [FieldOffset(36)] public float ImpactShockWeight;
        [FieldOffset(40)] public float FlatScreenBaselineFovTunnel;
        [FieldOffset(44)] public float VrBaselineFovTunnel;
        [FieldOffset(48)] public float ReleaseSharpness;
        [FieldOffset(52)] public float MockAmplitude;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    /// <summary>Open-addressed Vault profile lookup slot. Size: 16 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VrComfortProfileLookupSlotDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public int ProfileIndex;
        [FieldOffset(8)] public uint Occupied;
        [FieldOffset(12)] private byte _pad0;
        [FieldOffset(13)] private byte _pad1;
        [FieldOffset(14)] private byte _pad2;
        [FieldOffset(15)] private byte _pad3;
    }

    /// <summary>Last-frame AUP/rotation history for derivative calculation. Size: 96 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct SomaticKinematicHistoryDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PreviousAup;
        [FieldOffset(48)] public quaternion PreviousRotation;
        [FieldOffset(64)] public float3 PreviousVelocity;
        [FieldOffset(76)] public float3 PreviousAngularVelocity;
        [FieldOffset(88)] public uint PreviousFrame;
        [FieldOffset(92)] public uint Flags;
    }

    /// <summary>Finite KCC/player derivatives consumed by comfort evaluators. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SomaticDerivativeDTO
    {
        [FieldOffset(0)] public float3 LinearVelocity;
        [FieldOffset(12)] public float3 LinearAcceleration;
        [FieldOffset(24)] public float3 AngularVelocity;
        [FieldOffset(36)] public float3 AngularAcceleration;
        [FieldOffset(48)] public float PeakAngularVelocityRadS;
        [FieldOffset(52)] public float PeakAngularAccelerationRadS2;
        [FieldOffset(56)] public float PeakLinearAccelerationMps2;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>Fixed 300-frame comfort telemetry row. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ComfortTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float PeakAngularVelocityRadS;
        [FieldOffset(12)] public float PeakAngularAccelerationRadS2;
        [FieldOffset(16)] public float PeakLinearAccelerationMps2;
        [FieldOffset(20)] public float FovTunnelingIntensity;
        [FieldOffset(24)] public float HorizonLockBlend;
        [FieldOffset(28)] public float FoveatedScaleMultiplier;
        [FieldOffset(32)] public float BurstExecutionMicroseconds;
        [FieldOffset(36)] public float ImpactShock01;
        [FieldOffset(40)] public float GlobalQualityWeight01;
        [FieldOffset(44)] public float Pressure01;
        [FieldOffset(48)] public uint LockContentionCount;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint Sequence;
        [FieldOffset(60)] public uint AupHash;
    }

    /// <summary>Profiler-safe mock sickness sample injected into Vault buffers. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SomaticMockSicknessSampleDTO
    {
        [FieldOffset(0)] public float3 LinearAcceleration;
        [FieldOffset(12)] public float3 AngularVelocity;
        [FieldOffset(24)] public float3 AngularAcceleration;
        [FieldOffset(36)] public quaternion Rotation;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public float TimeSeconds;
    }

    public sealed partial class VRSomaticProvider
    {
        private const int SomaticComfortProfileCapacity = 4;
        private const int SomaticComfortProfileLookupCapacity = 8;
        private const int SomaticMockSicknessSampleCapacity = 128;
        private const int SomaticComfortStateBytes = 32;
        private const int ComfortTelemetryEntryBytes = 64;
        private const string SomaticComfortTelemetryDumpFileName = "Dump_1335_SomaticComfortTelemetry.bin";
        private const uint SomaticHistoryValidFlag = 1u << 0;
        private const uint SomaticDerivativeNonFiniteFlag = 1u << 1;
        private const uint SomaticComfortFlagFovTunnel = 1u << 0;
        private const uint SomaticComfortFlagHorizonLock = 1u << 1;
        private const uint SomaticComfortFlagFoveatedPressure = 1u << 2;
        private const uint SomaticComfortFlagImpactShock = 1u << 3;
        private const uint SomaticComfortFlagMockData = 1u << 4;
        private const uint SomaticProfileNoviceHash = 0xBC45CD7Bu;
        private const uint SomaticProfileVeteranHash = 0xE27847B4u;
        private const uint SomaticProfileDisabledHash = 0xBFCE9925u;
        private const uint SomaticProfileQuest3Hash = 0x47CA36AAu;
        private const uint SomaticComfortTelemetryHash = 0x56525343u; // VRSC

        private VaultBufferView<SomaticComfortStateDTO> _somaticComfortWrite;
        private VaultBufferView<SomaticComfortStateDTO> _somaticComfortRead;
        private VaultBufferView<SomaticDerivativeDTO> _somaticDerivatives;
        private VaultBufferView<SomaticKinematicHistoryDTO> _somaticHistory;
        private VaultBufferView<VrComfortProfileDTO> _somaticProfiles;
        private VaultBufferView<VrComfortProfileLookupSlotDTO> _somaticProfileLookup;
        private VaultBufferView<ComfortTelemetryEntry> _somaticComfortTelemetry;
        private VaultBufferView<SomaticMockSicknessSampleDTO> _somaticMockSicknessSamples;
        private JobHandle _somaticComfortHandle;
        private bool _somaticComfortBuffersSeeded;
        private bool _somaticComfortJobScheduled;
        private bool _somaticComfortTelemetryDumped;
        private uint _somaticTelemetrySequence;
        private uint _somaticComfortLockContentionCount;
        private int _somaticTelemetryCursor;
        private long _somaticScheduleTimestamp;
        private float _somaticFovTunnelingIntensity01;
        private float _somaticHorizonLockBlend01;
        private float _somaticFoveatedScaleMultiplier = 1f;
        private float _somaticVramPressure01;
        private float _somaticThermalPressure01;
        private float _somaticSystemPressure01;
        private float _somaticImpactShock01;
        private float _somaticComfortPresence01;
        private Vector4 _lastPublishedSomaticComfortState = Vector4.positiveInfinity;
        private Vector4 _pendingSomaticComfortState;
        private bool _somaticComfortShaderStateDirty;
#if UNITY_EDITOR
        private static bool s_somaticComfortLayoutsValidated;
#endif

        /// <summary>Cold test hook that refills the Vault mock sickness buffer with deterministic violent motion samples.</summary>
        public unsafe void GenerateMockSicknessData()
        {
            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return;

            EnsureSomaticComfortBuffers(vault);
            TryPublishCompletedSomaticComfortNoBlock();
            if (!_somaticMockSicknessSamples.IsCreated ||
                !_somaticComfortWrite.IsCreated ||
                !_somaticDerivatives.IsCreated ||
                !_somaticProfiles.IsCreated ||
                _somaticComfortJobScheduled)
            {
                return;
            }

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            float quality = ResolveGlobalQualityWeight01();
            if (!TryPopulateMockSicknessSamples(frame, quality, out SomaticMockSicknessSampleDTO mockSample) ||
                !TryReadFirst(in _somaticComfortWrite, out SomaticComfortStateDTO comfortState) ||
                !TryReadPrimaryComfortProfile(out VrComfortProfileDTO profile))
            {
                return;
            }

            SomaticDerivativeDTO derivatives = ResolveMockSicknessDerivative(in mockSample);
            float mockPhase = (frame & 127u) * math.lerp(0.2f, 0.055f, SmoothJob01(quality));
            quaternion mockRotation = math.mul(
                quaternion.RotateY(MathLodApproximation.ApproxSinBhaskara(mockPhase * 1.73f) * 1.35f),
                quaternion.RotateZ(MathLodApproximation.ApproxSinBhaskara(mockPhase * 4.7f) * 0.72f));
            ExecuteComfortAndHorizonImmediate(
                ref comfortState,
                ref derivatives,
                in profile,
                mockRotation,
                HectonXRRuntimeState.FrameIntervalSeconds,
                quality,
                math.max(_somaticComfortPresence01, 1f),
                1f);

            ResolveMockKinematicJitter(frame, quality, out VRSomaticKinematicStateMirrorDTO kccState, out quaternion rawRotation);
            if (!TryPublishFirst(ref _somaticDerivatives, in derivatives) ||
                !TryPublishFirst(ref _somaticComfortWrite, in comfortState))
            {
                return;
            }

            if (TryPublishFirst(ref _somaticKccStateMirror, in kccState) &&
                TryPublishFirst(ref _somaticRawRotation, in rawRotation))
            {
                EvaluatePreparedHorizonLockImmediate(
                    HectonXRRuntimeState.FrameIntervalSeconds,
                    quality,
                    in kccState,
                    rawRotation,
                    in derivatives,
                    in profile);
            }

            _somaticComfortHandle = default;
            _somaticScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _somaticComfortJobScheduled = true;
            TryRegisterLateFrame();
        }

#if UNITY_EDITOR
        public static int ParseComfortProfilesCsv(ReadOnlySpan<byte> csv, NativeArray<VrComfortProfileDTO> profiles)
        {
            return ParseComfortProfilesCsv(csv, profiles, default(NativeArray<VrComfortProfileLookupSlotDTO>));
        }

        public static int ParseComfortProfilesCsv(
            ReadOnlySpan<byte> csv,
            NativeArray<VrComfortProfileDTO> profiles,
            Span<uint> profileHashes)
        {
            return ParseComfortProfilesCsv(csv, profiles, default(NativeArray<VrComfortProfileLookupSlotDTO>), profileHashes);
        }

        public static int ParseComfortProfilesCsv(
            ReadOnlySpan<byte> csv,
            NativeArray<VrComfortProfileDTO> profiles,
            NativeArray<VrComfortProfileLookupSlotDTO> lookup)
        {
            return ParseComfortProfilesCsv(csv, profiles, lookup, default);
        }

        private static int ParseComfortProfilesCsv(
            ReadOnlySpan<byte> csv,
            NativeArray<VrComfortProfileDTO> profiles,
            NativeArray<VrComfortProfileLookupSlotDTO> lookup,
            Span<uint> profileHashes)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return 0;

            if (lookup.IsCreated && lookup.Length > 0)
                ClearComfortProfileLookup(lookup);

            int count = 0;
            int rowStart = 0;
            while (rowStart < csv.Length && count < profiles.Length)
            {
                int rowEnd = rowStart;
                while (rowEnd < csv.Length && csv[rowEnd] != (byte)'\n' && csv[rowEnd] != (byte)'\r')
                    rowEnd++;

                ReadOnlySpan<byte> row = csv.Slice(rowStart, rowEnd - rowStart);
                if (TryParseComfortProfileRow(row, out VrComfortProfileDTO profile))
                {
                    profile = SanitizeProfile(profile);
                    profiles[count] = profile;
                    if (count < profileHashes.Length)
                        profileHashes[count] = profile.ProfileHash;
                    if (lookup.IsCreated && lookup.Length > 0)
                        InsertProfileLookup(lookup, profile.ProfileHash, count);
                    count++;
                }

                rowStart = rowEnd + 1;
                while (rowStart < csv.Length && (csv[rowStart] == (byte)'\n' || csv[rowStart] == (byte)'\r'))
                    rowStart++;
            }

            return count;
        }

        private static unsafe void ClearComfortProfileLookup(NativeArray<VrComfortProfileLookupSlotDTO> lookup)
        {
            void* lookupPtr = NativeArrayUnsafeUtility.GetUnsafePtr(lookup);
            UnsafeUtility.MemClear(lookupPtr, (long)lookup.Length * UnsafeUtility.SizeOf<VrComfortProfileLookupSlotDTO>());
        }
#endif

        private unsafe void EnsureSomaticComfortBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

#if UNITY_EDITOR
            ValidateSomaticComfortLayouts();
#endif

            if (!_somaticComfortWrite.IsCreated)
            {
                _somaticComfortWrite = VaultBufferView<SomaticComfortStateDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticComfortWrite,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticComfortRead = VaultBufferView<SomaticComfortStateDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticComfortRead,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticDerivatives = VaultBufferView<SomaticDerivativeDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticDerivatives,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticHistory = VaultBufferView<SomaticKinematicHistoryDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHistory,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticProfiles = VaultBufferView<VrComfortProfileDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticProfile,
                    SomaticComfortProfileCapacity,
                    NativeArrayOptions.UninitializedMemory);
                _somaticProfileLookup = VaultBufferView<VrComfortProfileLookupSlotDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticProfileLookup,
                    SomaticComfortProfileLookupCapacity,
                    NativeArrayOptions.UninitializedMemory);
                _somaticKccStateMirror = VaultBufferView<VRSomaticKinematicStateMirrorDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticKccStateMirror,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticRawRotation = VaultBufferView<quaternion>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticRawRotation,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticHorizonWrite = VaultBufferView<VRSomaticComfortDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHorizonWrite,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticHorizonRead = VaultBufferView<VRSomaticComfortDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHorizonRead,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                _somaticHorizonTelemetry = VaultBufferView<SomaticTelemetryEntry>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHorizonTelemetry,
                    BlackBoxFrameCapacity,
                    NativeArrayOptions.UninitializedMemory);
                _somaticComfortTelemetry = VaultBufferView<ComfortTelemetryEntry>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticComfortTelemetry,
                    BlackBoxFrameCapacity,
                    NativeArrayOptions.UninitializedMemory);
                _somaticMockSicknessSamples = VaultBufferView<SomaticMockSicknessSampleDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticMockSickness,
                    SomaticMockSicknessSampleCapacity,
                    NativeArrayOptions.UninitializedMemory);
            }
            if (!_somaticKccStateMirror.IsCreated)
            {
                _somaticKccStateMirror = VaultBufferView<VRSomaticKinematicStateMirrorDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticKccStateMirror,
                    1,
                    NativeArrayOptions.UninitializedMemory);
            }
            if (!_somaticRawRotation.IsCreated)
            {
                _somaticRawRotation = VaultBufferView<quaternion>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticRawRotation,
                    1,
                    NativeArrayOptions.UninitializedMemory);
            }
            if (!_somaticHorizonWrite.IsCreated)
            {
                _somaticHorizonWrite = VaultBufferView<VRSomaticComfortDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHorizonWrite,
                    1,
                    NativeArrayOptions.UninitializedMemory);
            }
            if (!_somaticHorizonRead.IsCreated)
            {
                _somaticHorizonRead = VaultBufferView<VRSomaticComfortDTO>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHorizonRead,
                    1,
                    NativeArrayOptions.UninitializedMemory);
            }
            if (!_somaticHorizonTelemetry.IsCreated)
            {
                _somaticHorizonTelemetry = VaultBufferView<SomaticTelemetryEntry>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHorizonTelemetry,
                    BlackBoxFrameCapacity,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (_somaticComfortBuffersSeeded ||
                !_somaticComfortWrite.IsCreated ||
                !_somaticComfortRead.IsCreated ||
                !_somaticDerivatives.IsCreated ||
                !_somaticHistory.IsCreated ||
                !_somaticProfiles.IsCreated ||
                !_somaticProfileLookup.IsCreated ||
                !_somaticKccStateMirror.IsCreated ||
                !_somaticRawRotation.IsCreated ||
                !_somaticHorizonWrite.IsCreated ||
                !_somaticHorizonRead.IsCreated ||
                !_somaticHorizonTelemetry.IsCreated ||
                !_somaticComfortTelemetry.IsCreated ||
                !_somaticMockSicknessSamples.IsCreated)
            {
                return;
            }

            if (!TrySeedSomaticComfortBuffersImmediate())
                return;

            _somaticComfortHandle = default;
            _somaticComfortBuffersSeeded = true;
        }

        private bool TrySeedSomaticComfortBuffersImmediate()
        {
            SomaticComfortStateDTO comfortClear = new SomaticComfortStateDTO
            {
                FovTunnelingIntensity = 0f,
                HorizonLockBlend = 0f,
                FoveatedScaleMultiplier = 1f,
                ActiveComfortFlags = 0u,
                ReservedParameters = float4.zero
            };
            VRSomaticComfortDTO horizonClear = new VRSomaticComfortDTO
            {
                StabilizedRotation = quaternion.identity,
                FovTunnelScalar = 0f,
                PitchDampening = 0f,
                ComfortFlags = SomaticComfortFlagHorizonInitialized
            };

            return
                TryPublishFirst(ref _somaticComfortWrite, in comfortClear) &&
                TryPublishFirst(ref _somaticComfortRead, in comfortClear) &&
                TryPublishFirst(ref _somaticDerivatives, default(SomaticDerivativeDTO)) &&
                TryPublishFirst(ref _somaticHistory, default(SomaticKinematicHistoryDTO)) &&
                TrySeedComfortProfiles() &&
                TrySeedComfortProfileLookup() &&
                TryPublishFirst(ref _somaticKccStateMirror, default(VRSomaticKinematicStateMirrorDTO)) &&
                TryPublishFirst(ref _somaticRawRotation, quaternion.identity) &&
                TryPublishFirst(ref _somaticHorizonWrite, in horizonClear) &&
                TryPublishFirst(ref _somaticHorizonRead, in horizonClear) &&
                TryClearBuffer(ref _somaticHorizonTelemetry) &&
                TryClearBuffer(ref _somaticComfortTelemetry) &&
                TrySeedMockSicknessSamples(Hecton8.Core.SystemDispatcher.CurrentFrameId, ResolveGlobalQualityWeight01());
        }

        private bool TrySeedComfortProfiles()
        {
            bool locked = false;
            try
            {
                if (!_somaticProfiles.TryAcquireWriteNativeArray(out NativeArray<VrComfortProfileDTO> profiles))
                    return false;

                locked = true;
                if (profiles.Length <= 0)
                    return false;

                profiles[0] = DefaultNoviceProfile();
                if (profiles.Length > 1)
                    profiles[1] = DefaultVeteranProfile();
                if (profiles.Length > 2)
                    profiles[2] = DefaultDisabledProfile();
                if (profiles.Length > 3)
                    profiles[3] = DefaultQuest3Profile();
                for (int i = 4; i < profiles.Length; i++)
                    profiles[i] = default;
                return true;
            }
            finally
            {
                if (locked)
                    _somaticProfiles.ReleaseWriteNativeArray();
            }
        }

        private bool TrySeedComfortProfileLookup()
        {
            bool locked = false;
            try
            {
                if (!_somaticProfileLookup.TryAcquireWriteNativeArray(out NativeArray<VrComfortProfileLookupSlotDTO> lookup))
                    return false;

                locked = true;
                if (lookup.Length <= 0)
                    return false;

                for (int i = 0; i < lookup.Length; i++)
                    lookup[i] = default;
                InsertProfileLookup(lookup, SomaticProfileNoviceHash, 0);
                InsertProfileLookup(lookup, SomaticProfileVeteranHash, 1);
                InsertProfileLookup(lookup, SomaticProfileDisabledHash, 2);
                InsertProfileLookup(lookup, SomaticProfileQuest3Hash, 3);
                return true;
            }
            finally
            {
                if (locked)
                    _somaticProfileLookup.ReleaseWriteNativeArray();
            }
        }

        private bool TrySeedMockSicknessSamples(uint frame, float globalQualityWeight01)
        {
            return TryPopulateMockSicknessSamples(frame, globalQualityWeight01, out _);
        }

        private bool TryPopulateMockSicknessSamples(uint frame, float globalQualityWeight01, out SomaticMockSicknessSampleDTO selectedSample)
        {
            selectedSample = default;
            bool locked = false;
            try
            {
                if (!_somaticMockSicknessSamples.TryAcquireWriteNativeArray(out NativeArray<SomaticMockSicknessSampleDTO> samples))
                    return false;

                locked = true;
                if (samples.Length <= 0)
                    return false;

                GenerateMockSicknessDataJob job = new GenerateMockSicknessDataJob
                {
                    Samples = samples,
                    GlobalQualityWeight01 = globalQualityWeight01,
                    Frame = frame
                };
                for (int i = 0; i < samples.Length; i++)
                    job.Execute(i);

                selectedSample = samples[(int)(frame % (uint)samples.Length)];
                return true;
            }
            finally
            {
                if (locked)
                    _somaticMockSicknessSamples.ReleaseWriteNativeArray();
            }
        }

        private static bool TryReadFirst<T>(in VaultBufferView<T> source, out T value) where T : struct
        {
            value = default;
            VaultBufferView<T> local = source;
            if (!local.TryReadOnlyNativeArray(out NativeArray<T>.ReadOnly array) ||
                !array.IsCreated ||
                array.Length <= 0)
            {
                return false;
            }

            value = array[0];
            return true;
        }

        private static bool TryPublishFirst<T>(ref VaultBufferView<T> target, in T value) where T : struct
        {
            bool locked = false;
            try
            {
                if (!target.TryAcquireWriteNativeArray(out NativeArray<T> array))
                    return false;

                locked = true;
                if (!array.IsCreated || array.Length <= 0)
                    return false;

                array[0] = value;
                return true;
            }
            finally
            {
                if (locked)
                    target.ReleaseWriteNativeArray();
            }
        }

        private static bool TryClearBuffer<T>(ref VaultBufferView<T> target) where T : struct
        {
            bool locked = false;
            try
            {
                if (!target.TryAcquireWriteNativeArray(out NativeArray<T> array))
                    return false;

                locked = true;
                for (int i = 0; i < array.Length; i++)
                    array[i] = default;
                return true;
            }
            finally
            {
                if (locked)
                    target.ReleaseWriteNativeArray();
            }
        }

        private bool TryReadPrimaryComfortProfile(out VrComfortProfileDTO profile)
        {
            if (!TryReadFirst(in _somaticProfiles, out profile))
            {
                profile = DefaultNoviceProfile();
                return false;
            }

            profile = SanitizeJobProfile(profile);
            return true;
        }

        private static SomaticDerivativeDTO ResolveMockSicknessDerivative(in SomaticMockSicknessSampleDTO sample)
        {
            float3 angularVelocity = ClampLength(SanitizeJobFloat3(sample.AngularVelocity), 48f);
            float3 angularAcceleration = ClampLength(SanitizeJobFloat3(sample.AngularAcceleration), 480f);
            float3 linearAcceleration = ClampLength(SanitizeJobFloat3(sample.LinearAcceleration), 240f);
            return new SomaticDerivativeDTO
            {
                LinearVelocity = float3.zero,
                LinearAcceleration = linearAcceleration,
                AngularVelocity = angularVelocity,
                AngularAcceleration = angularAcceleration,
                PeakAngularVelocityRadS = math.length(angularVelocity),
                PeakAngularAccelerationRadS2 = math.length(angularAcceleration),
                PeakLinearAccelerationMps2 = math.length(linearAcceleration),
                Flags = SomaticComfortFlagMockData
            };
        }

        private static void ResolveMockKinematicJitter(
            uint frame,
            float globalQualityWeight01,
            out VRSomaticKinematicStateMirrorDTO kccState,
            out quaternion rawRotation)
        {
            float quality = SmoothJob01(SanitizeJob01(globalQualityWeight01, 1f));
            float t = frame * math.lerp(0.37f, 0.083f, quality);
            float triangle = math.abs(frac(t * 2.37f) * 2f - 1f);
            float pulse = (triangle * 2f) - 1f;
            float amplitude = math.lerp(1.4f, 3.8f, quality);
            float pitch = MathLodApproximation.ApproxSinBhaskara(t * 3.1f) * 0.28f * amplitude;
            float yaw = pulse * 0.72f * amplitude;
            float roll = MathLodApproximation.ApproxSinBhaskara(t * 5.3f) * 0.42f * amplitude;
            rawRotation = SanitizeJobQuaternion(math.mul(math.mul(quaternion.RotateY(yaw), quaternion.RotateX(pitch)), quaternion.RotateZ(roll)), quaternion.identity);
            kccState = new VRSomaticKinematicStateMirrorDTO
            {
                AUP_Position = double3.zero,
                Velocity = float3.zero,
                AngularVelocity = ClampLength(new float3(pitch, yaw, roll) * math.rcp(math.max(HectonXRRuntimeState.FrameIntervalSeconds, MinimumDeltaTime)), 48f),
                Mass = 1f,
                Flags = SomaticComfortFlagMockData,
                DragCoefficient = 0f,
                RestingFrameCount = 0,
                DeepSleepTickCount = 0,
                SleepMaterialIndex = 0
            };
        }

        private static unsafe void ExecuteSomaticDerivativesImmediate(
            ref SomaticKinematicHistoryDTO history,
            ref SomaticDerivativeDTO derivatives,
            in AbsoluteUniversePosition currentAup,
            quaternion currentRotation,
            float deltaTime,
            int historyDepth,
            uint frame)
        {
            ComputeSomaticDerivativesJob derivativeJob = new ComputeSomaticDerivativesJob
            {
                CurrentAup = currentAup,
                CurrentRotation = currentRotation,
                DeltaTime = deltaTime,
                HistoryDepth = historyDepth,
                Frame = frame,
                History = (SomaticKinematicHistoryDTO*)UnsafeUtility.AddressOf(ref history),
                Derivatives = (SomaticDerivativeDTO*)UnsafeUtility.AddressOf(ref derivatives)
            };
            derivativeJob.Execute();
        }

        private unsafe void ExecuteComfortAndHorizonImmediate(
            ref SomaticComfortStateDTO state,
            ref SomaticDerivativeDTO derivatives,
            in VrComfortProfileDTO profile,
            quaternion currentRotation,
            float deltaTime,
            float globalQualityWeight01,
            float runtimeComfortBlend01,
            float impactShock01)
        {
            VrComfortProfileDTO sanitizedProfile = SanitizeJobProfile(profile);
            EvaluateComfortAndHorizonJob comfortJob = new EvaluateComfortAndHorizonJob
            {
                CurrentRotation = currentRotation,
                DeltaTime = deltaTime,
                GlobalQualityWeight01 = globalQualityWeight01,
                RuntimeComfortBlend01 = runtimeComfortBlend01,
                ImpactShock01 = impactShock01,
                VramPressure01 = _somaticVramPressure01,
                ThermalPressure01 = _somaticThermalPressure01,
                SystemPressure01 = _somaticSystemPressure01,
                KccAngularVelocityRadS = _kccAngularVelocityRadiansPerSecond,
                KccAngularAccelerationRadS2 = _kccAngularAccelerationRadiansPerSecondSq,
                State = (SomaticComfortStateDTO*)UnsafeUtility.AddressOf(ref state),
                Derivatives = (SomaticDerivativeDTO*)UnsafeUtility.AddressOf(ref derivatives),
                Profile = (VrComfortProfileDTO*)UnsafeUtility.AddressOf(ref sanitizedProfile)
            };
            comfortJob.Execute();
        }

#if UNITY_EDITOR
        private static void ValidateSomaticComfortLayouts()
        {
            if (s_somaticComfortLayoutsValidated)
                return;

            if (UnsafeUtility.SizeOf<SomaticComfortStateDTO>() != SomaticComfortStateBytes ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.FovTunnelingIntensity)) != 0 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.HorizonLockBlend)) != 4 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.FoveatedScaleMultiplier)) != 8 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.ActiveComfortFlags)) != 12 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.ReservedParameters)) != 16)
            {
                throw new InvalidOperationException("SomaticComfortStateDTO ABI drift.");
            }

            if (UnsafeUtility.SizeOf<ComfortTelemetryEntry>() != ComfortTelemetryEntryBytes ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Frame)) != 0 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Flags)) != 4 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.PeakAngularVelocityRadS)) != 8 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.PeakAngularAccelerationRadS2)) != 12 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.PeakLinearAccelerationMps2)) != 16 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.FovTunnelingIntensity)) != 20 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.HorizonLockBlend)) != 24 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.FoveatedScaleMultiplier)) != 28 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.BurstExecutionMicroseconds)) != 32 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.ImpactShock01)) != 36 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.GlobalQualityWeight01)) != 40 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Pressure01)) != 44 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.LockContentionCount)) != 48 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.StateHash)) != 52 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Sequence)) != 56 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.AupHash)) != 60)
            {
                throw new InvalidOperationException("ComfortTelemetryEntry ABI drift.");
            }

            ValidateHorizonLockLayouts();
            s_somaticComfortLayoutsValidated = true;
        }

#endif

        private void ResetSomaticComfortBuffers()
        {
            CompleteSomaticComfortForBarrier();
            _somaticComfortWrite.Release();
            _somaticComfortRead.Release();
            _somaticDerivatives.Release();
            _somaticHistory.Release();
            _somaticProfiles.Release();
            _somaticProfileLookup.Release();
            _somaticComfortTelemetry.Release();
            _somaticMockSicknessSamples.Release();
            ResetHorizonLockBuffers();
            _somaticComfortWrite = default;
            _somaticComfortRead = default;
            _somaticDerivatives = default;
            _somaticHistory = default;
            _somaticProfiles = default;
            _somaticProfileLookup = default;
            _somaticComfortTelemetry = default;
            _somaticMockSicknessSamples = default;
            _somaticComfortBuffersSeeded = false;
            _somaticComfortJobScheduled = false;
            _somaticComfortTelemetryDumped = false;
            _somaticComfortHandle = default;
            _somaticTelemetryCursor = 0;
            _somaticTelemetrySequence = 0u;
            _somaticComfortLockContentionCount = 0u;
            _somaticScheduleTimestamp = 0L;
            ResetSomaticComfortStateForShift();
        }

        private void ResetSomaticComfortStateForShift()
        {
            _somaticFovTunnelingIntensity01 = 0f;
            _somaticHorizonLockBlend01 = 0f;
            _somaticFoveatedScaleMultiplier = 1f;
            _somaticVramPressure01 = 0f;
            _somaticThermalPressure01 = 0f;
            _somaticSystemPressure01 = 0f;
            _somaticImpactShock01 = 0f;
            _somaticComfortPresence01 = 0f;
            _lastPublishedSomaticComfortState = Vector4.positiveInfinity;
        }

        private unsafe void ScheduleSomaticComfortKernel(in AbsoluteUniversePosition headAup, Quaternion headRotation, float deltaTime)
        {
            TryPublishCompletedSomaticComfortNoBlock();

            if (!_somaticComfortBuffersSeeded ||
                !_somaticComfortWrite.IsCreated ||
                !_somaticDerivatives.IsCreated ||
                !_somaticHistory.IsCreated ||
                !_somaticProfiles.IsCreated ||
                !_somaticKccStateMirror.IsCreated ||
                !_somaticRawRotation.IsCreated ||
                !_somaticHorizonWrite.IsCreated ||
                !_somaticHorizonRead.IsCreated ||
                !_somaticHorizonTelemetry.IsCreated)
            {
                return;
            }

            if (_somaticComfortJobScheduled)
            {
                if (_somaticComfortLockContentionCount != uint.MaxValue)
                    _somaticComfortLockContentionCount++;
                return;
            }

            RefreshSomaticPressureState(deltaTime);

            AbsoluteUniversePosition sourceAup = headAup;
            quaternion sourceRotation = (quaternion)headRotation;
            if (TryResolveLatestKccVelocitySignal(out KccVelocitySignal kccSignal) &&
                kccSignal.Sequence != 0u &&
                !IsKccVelocitySignalStale(kccSignal.Frame) &&
                TryResolveKccPlanarDirection(in kccSignal, out float2 kccDirection))
            {
                sourceAup = kccSignal.BodyAup;
                sourceRotation = quaternion.RotateY(MathLodApproximation.ApproxAtan2Fast(kccDirection.x, kccDirection.y));
            }

            if (!TryReadFirst(in _somaticComfortWrite, out SomaticComfortStateDTO comfortState) ||
                !TryReadFirst(in _somaticDerivatives, out SomaticDerivativeDTO derivatives) ||
                !TryReadFirst(in _somaticHistory, out SomaticKinematicHistoryDTO history) ||
                !TryReadPrimaryComfortProfile(out VrComfortProfileDTO profile))
            {
                return;
            }

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, MinimumDeltaTime) : MinimumDeltaTime;
            float quality = ResolveGlobalQualityWeight01();
            float runtimeComfortTarget01 = ResolveRuntimeComfortBlendTarget01(quality, _comfortPressureFallbackWeight01);
            _somaticComfortPresence01 = Sanitize01(math.lerp(
                Sanitize01(_somaticComfortPresence01, 0f),
                runtimeComfortTarget01,
                ResolveCinematicBlendApprox(12f, safeDeltaTime)), runtimeComfortTarget01);
            int historyDepth = (int)math.lerp(2f, 8f, quality);
            int derivativeSampleStride = (int)math.max(1f, math.round(math.lerp(12f, 1f, quality)));
            int frameIndex = SystemDispatcher.CurrentFrameIndex;
            bool derivativeSampleDue =
                (history.Flags & SomaticHistoryValidFlag) == 0u ||
                _somaticImpactShock01 > 0.001f ||
                frameIndex % derivativeSampleStride == 0;

            if (derivativeSampleDue)
            {
                ExecuteSomaticDerivativesImmediate(
                    ref history,
                    ref derivatives,
                    in sourceAup,
                    sourceRotation,
                    safeDeltaTime,
                    historyDepth,
                    unchecked((uint)frameIndex));
            }

            ExecuteComfortAndHorizonImmediate(
                ref comfortState,
                ref derivatives,
                in profile,
                sourceRotation,
                safeDeltaTime,
                quality,
                _somaticComfortPresence01,
                _somaticImpactShock01);

            if (!TryPublishFirst(ref _somaticDerivatives, in derivatives) ||
                !TryPublishFirst(ref _somaticHistory, in history) ||
                !TryPublishFirst(ref _somaticComfortWrite, in comfortState))
            {
                return;
            }

            EvaluateHorizonLockImmediate(
                in sourceAup,
                sourceRotation,
                safeDeltaTime,
                quality,
                in derivatives,
                in profile);

            _somaticComfortHandle = default;
            _somaticScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _somaticComfortJobScheduled = true;
            TryRegisterLateFrame();
        }

        private void RefreshSomaticPressureState(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
            float releaseBlend = ResolveCinematicBlendApprox(4f, math.max(safeDeltaTime, MinimumDeltaTime));
            _somaticVramPressure01 = math.lerp(Sanitize01(_somaticVramPressure01, 0f), 0f, releaseBlend);
            _somaticThermalPressure01 = math.lerp(Sanitize01(_somaticThermalPressure01, 0f), 0f, releaseBlend);
            _somaticSystemPressure01 = math.lerp(Sanitize01(_somaticSystemPressure01, 0f), 0f, releaseBlend);
            _somaticImpactShock01 = math.lerp(Sanitize01(_somaticImpactShock01, 0f), 0f, ResolveCinematicBlendApprox(10f, math.max(safeDeltaTime, MinimumDeltaTime)));

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthSignal signal = healthSignals[i];
                float pressureFromLevel = math.saturate(signal.PressureLevel * math.rcp(3f));
                float foveatedPressure = math.saturate(signal.FoveatedPressureTier * math.rcp(3f));
                _somaticSystemPressure01 = math.max(_somaticSystemPressure01, math.max(Sanitize01(signal.SystemHealthIndex01, 0f), pressureFromLevel));
                _somaticVramPressure01 = math.max(_somaticVramPressure01, math.max(Sanitize01(signal.GpuUtil01, 0f), foveatedPressure));
            }

            ReadOnlySpan<SystemHealthIndexSignal> indexSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < indexSignals.Length; i++)
            {
                SystemHealthIndexSignal signal = indexSignals[i];
                _somaticSystemPressure01 = math.max(_somaticSystemPressure01, Sanitize01(signal.Pressure01, 0f));
            }

            ReadOnlySpan<ThermalStateChangedSignal> thermalSignals = SignalBus<ThermalStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < thermalSignals.Length; i++)
            {
                ThermalStateChangedSignal signal = thermalSignals[i];
                _somaticThermalPressure01 = math.max(_somaticThermalPressure01, math.saturate(signal.Severity * math.rcp(3f)));
            }

            ReadOnlySpan<HighSpeedImpactSignal> impactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot();
            for (int i = 0; i < impactSignals.Length; i++)
            {
                HighSpeedImpactSignal signal = impactSignals[i];
                if (signal.SourceKind != HighSpeedImpactSignal.SourcePlayer && signal.SourceKind != HighSpeedImpactSignal.SourceVehicle)
                    continue;

                float speed01 = math.saturate(SanitizeNonNegative(signal.ImpactSpeed) * math.rcp(math.max(impactSpeedThresholdMetersPerSecond, 0.01f) * 2f));
                float safeMass = math.max(SanitizeNonNegative(signal.EffectiveMass), 1f);
                float energy01 = math.saturate(SanitizeNonNegative(signal.KineticEnergy) * math.rcp(safeMass * 120f));
                _somaticImpactShock01 = math.max(_somaticImpactShock01, math.max(speed01, energy01));
            }
        }

        private static float ResolveRuntimeComfortBlendTarget01(float globalQualityWeight01, float pressureFallbackWeight01)
        {
            float quality = Sanitize01(globalQualityWeight01, 1f);
            float fallback = Sanitize01(pressureFallbackWeight01, 0f);
            float protectiveBias01 = Smoothstep01(math.max(1f - quality, fallback));
            return math.saturate(math.lerp(0.92f, 1f, protectiveBias01));
        }

        private void TryPublishCompletedSomaticComfortNoBlock()
        {
            if (!_somaticComfortJobScheduled || !_somaticComfortHandle.IsCompleted)
                return;

            _somaticComfortHandle = default;
            PublishSomaticComfortStateFromWrite();
        }

        private void CompleteSomaticComfortIfReady()
        {
            if (!_somaticComfortJobScheduled)
                return;

            if (!_somaticComfortHandle.IsCompleted)
                return;

            _somaticComfortHandle = default;
            PublishSomaticComfortStateFromWrite();
        }

        private void CompleteSomaticComfortForBarrier()
        {
            if (!_somaticComfortJobScheduled)
                return;

            if (!_somaticComfortHandle.IsCompleted)
                return;

            _somaticComfortHandle = default;
            PublishSomaticComfortStateFromWrite();
        }

        private unsafe void PublishSomaticComfortStateFromWrite()
        {
            _somaticComfortJobScheduled = false;
            if (!_somaticComfortWrite.IsCreated || !_somaticComfortRead.IsCreated)
                return;

            SomaticComfortStateDTO state;
            bool readLocked = false;
            try
            {
                if (!_somaticComfortWrite.TryReadOnlyNativeArray(out NativeArray<SomaticComfortStateDTO>.ReadOnly write) ||
                    !_somaticComfortRead.TryAcquireWriteNativeArray(out NativeArray<SomaticComfortStateDTO> read))
                {
                    return;
                }

                readLocked = true;
                if (write.Length == 0 || read.Length == 0)
                    return;

                state = write[0];
                read[0] = state;
            }
            finally
            {
                if (readLocked)
                    _somaticComfortRead.ReleaseWriteNativeArray();
            }

            _somaticFovTunnelingIntensity01 = Sanitize01(state.FovTunnelingIntensity, 0f);
            _somaticHorizonLockBlend01 = Sanitize01(state.HorizonLockBlend, 0f);
            if (TryPublishHorizonLockStateFromWrite(out VRSomaticComfortDTO horizonState))
            {
                _somaticFovTunnelingIntensity01 = math.max(_somaticFovTunnelingIntensity01, Sanitize01(horizonState.FovTunnelScalar, 0f));
                _somaticHorizonLockBlend01 = math.max(_somaticHorizonLockBlend01, Sanitize01(horizonState.PitchDampening, 0f));
            }
            _somaticFoveatedScaleMultiplier = math.max(1f, math.isfinite(state.FoveatedScaleMultiplier) ? state.FoveatedScaleMultiplier : 1f);
            RecordSomaticComfortTelemetry(in state);
            PublishComfortVignette(_somaticFovTunnelingIntensity01);
        }

        private void RecordSomaticComfortTelemetry(in SomaticComfortStateDTO state)
        {
            if (!_somaticComfortTelemetry.IsCreated || !_somaticDerivatives.IsCreated)
                return;

            bool telemetryLocked = false;
            bool shouldDump = false;
            try
            {
                if (!_somaticComfortTelemetry.TryAcquireWriteNativeArray(out NativeArray<ComfortTelemetryEntry> telemetry))
                    return;
                telemetryLocked = true;

                if (!_somaticDerivatives.TryReadOnlyNativeArray(out NativeArray<SomaticDerivativeDTO>.ReadOnly derivatives))
                    return;

                if (telemetry.Length == 0 || derivatives.Length == 0)
                    return;

                SomaticDerivativeDTO derivative = derivatives[0];
                bool nonFinite =
                    !math.isfinite(state.FovTunnelingIntensity) ||
                    !math.isfinite(state.HorizonLockBlend) ||
                    !math.isfinite(state.FoveatedScaleMultiplier) ||
                    !math.isfinite(derivative.PeakAngularVelocityRadS) ||
                    !math.isfinite(derivative.PeakAngularAccelerationRadS2) ||
                    !math.isfinite(derivative.PeakLinearAccelerationMps2);
                uint flags = state.ActiveComfortFlags;
                if (nonFinite)
                    flags |= SomaticDerivativeNonFiniteFlag;

                float burstMicroseconds = 0f;
                if (_somaticScheduleTimestamp > 0L)
                {
                    long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - _somaticScheduleTimestamp;
                    burstMicroseconds = (float)(ticks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);
                }

                uint aupHash = 0u;
                if (_somaticHistory.IsCreated)
                {
                    if (_somaticHistory.TryReadOnlyNativeArray(out NativeArray<SomaticKinematicHistoryDTO>.ReadOnly history) &&
                        history.IsCreated &&
                        history.Length > 0)
                    {
                        SomaticKinematicHistoryDTO historyRow = history[0];
                        aupHash = ResolveAupHash(in historyRow.PreviousAup);
                    }
                }

                int index = PositiveModuloSomatic(_somaticTelemetryCursor, telemetry.Length);
                telemetry[index] = new ComfortTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Flags = flags,
                    PeakAngularVelocityRadS = SanitizeNonNegative(derivative.PeakAngularVelocityRadS),
                    PeakAngularAccelerationRadS2 = SanitizeNonNegative(derivative.PeakAngularAccelerationRadS2),
                    PeakLinearAccelerationMps2 = SanitizeNonNegative(derivative.PeakLinearAccelerationMps2),
                    FovTunnelingIntensity = Sanitize01(state.FovTunnelingIntensity, 0f),
                    HorizonLockBlend = Sanitize01(state.HorizonLockBlend, 0f),
                    FoveatedScaleMultiplier = math.max(1f, math.isfinite(state.FoveatedScaleMultiplier) ? state.FoveatedScaleMultiplier : 1f),
                    BurstExecutionMicroseconds = SanitizeNonNegative(burstMicroseconds),
                    ImpactShock01 = Sanitize01(_somaticImpactShock01, 0f),
                    GlobalQualityWeight01 = ResolveGlobalQualityWeight01(),
                    Pressure01 = math.max(
                        Sanitize01(_somaticVramPressure01, 0f),
                        math.max(Sanitize01(_somaticThermalPressure01, 0f), Sanitize01(_somaticSystemPressure01, 0f))),
                    LockContentionCount = _somaticComfortLockContentionCount,
                    StateHash = ResolveSomaticComfortStateHash(in state, in derivative, flags),
                    Sequence = _somaticTelemetrySequence++,
                    AupHash = aupHash
                };
                _somaticTelemetryCursor = _somaticTelemetryCursor == int.MaxValue
                    ? telemetry.Length
                    : _somaticTelemetryCursor + 1;

                shouldDump = nonFinite;
            }
            finally
            {
                if (telemetryLocked)
                    _somaticComfortTelemetry.ReleaseWriteNativeArray();
            }

            if (shouldDump)
            {
                DumpComfortTelemetryOnce();
                DumpBlackBoxOnce();
            }
        }

        private unsafe void DumpComfortTelemetryOnce()
        {
            if (_somaticComfortTelemetryDumped)
                return;

            const string dumpPayloadLabel = "VRSomaticProvider.ComfortTelemetryDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                bool hasComfort = TryGetComfortTelemetryLength(out int comfortLength);
                bool hasHorizon = TryGetHorizonTelemetryLength(out int horizonLength);
                if (!hasComfort && !hasHorizon)
                    return;

                _somaticComfortTelemetryDumped = true;
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    SomaticComfortTelemetryDumpFileName));
                int byteCount = 40 +
                    (hasComfort ? comfortLength * ComfortTelemetryEntryBytes : 0) +
                    (hasHorizon ? horizonLength * SomaticTelemetryEntryBytes : 0);
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(VRSomaticProvider),
                    dumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);

                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> bytes = new Span<byte>(target, byteCount);
                WriteUInt32LittleEndian(bytes, 0, SomaticHorizonTelemetryHash);
                WriteUInt32LittleEndian(bytes, 4, 3u);
                WriteUInt32LittleEndian(bytes, 8, hasComfort ? (uint)comfortLength : 0u);
                WriteUInt32LittleEndian(bytes, 12, (uint)ComfortTelemetryEntryBytes);
                WriteUInt32LittleEndian(bytes, 16, hasHorizon ? (uint)horizonLength : 0u);
                WriteUInt32LittleEndian(bytes, 20, (uint)SomaticTelemetryEntryBytes);
                WriteUInt32LittleEndian(bytes, 24, unchecked((uint)_somaticTelemetryCursor));
                WriteUInt32LittleEndian(bytes, 28, unchecked((uint)_somaticHorizonTelemetryCursor));
                WriteUInt32LittleEndian(bytes, 32, SomaticComfortTelemetryHash);
                WriteUInt32LittleEndian(bytes, 36, SomaticHorizonTelemetryHash);

                int offset = 40;
                if (hasComfort)
                {
                    for (int i = 0; i < comfortLength; i++)
                    {
                        Span<byte> row = bytes.Slice(offset, ComfortTelemetryEntryBytes);
                        if (TryReadComfortTelemetryEntry(i, out ComfortTelemetryEntry entry))
                            WriteComfortTelemetryEntry(row, in entry);
                        offset += ComfortTelemetryEntryBytes;
                    }
                }

                if (hasHorizon)
                {
                    for (int i = 0; i < horizonLength; i++)
                    {
                        Span<byte> row = bytes.Slice(offset, SomaticTelemetryEntryBytes);
                        if (TryReadHorizonTelemetryEntry(i, out SomaticTelemetryEntry entry))
                            WriteHorizonTelemetryEntry(row, in entry);
                        offset += SomaticTelemetryEntryBytes;
                    }
                }

                if (!NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount))
                    PublishComfortDumpFault(unchecked((int)0x80004005));
            }
            catch (System.IO.IOException exception)
            {
                PublishComfortDumpFault(exception.HResult);
            }
            catch (UnauthorizedAccessException exception)
            {
                PublishComfortDumpFault(exception.HResult);
            }
            catch (ArgumentException exception)
            {
                PublishComfortDumpFault(exception.HResult);
            }
            catch (NotSupportedException exception)
            {
                PublishComfortDumpFault(exception.HResult);
            }
            catch (ObjectDisposedException exception)
            {
                PublishComfortDumpFault(exception.HResult);
            }
            catch (InvalidOperationException exception)
            {
                PublishComfortDumpFault(exception.HResult);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(VRSomaticProvider),
                    dumpPayloadLabel);
            }
        }

        private bool TryGetComfortTelemetryLength(out int length)
        {
            length = 0;
            if (!_somaticComfortTelemetry.TryReadOnlyNativeArray(out NativeArray<ComfortTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return false;
            }

            length = telemetry.Length;
            return true;
        }

        private bool TryGetHorizonTelemetryLength(out int length)
        {
            length = 0;
            if (!_somaticHorizonTelemetry.TryReadOnlyNativeArray(out NativeArray<SomaticTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return false;
            }

            length = telemetry.Length;
            return true;
        }

        private bool TryReadComfortTelemetryEntry(int index, out ComfortTelemetryEntry entry)
        {
            entry = default;
            if (!_somaticComfortTelemetry.TryReadOnlyNativeArray(out NativeArray<ComfortTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated ||
                (uint)index >= (uint)telemetry.Length)
            {
                return false;
            }

            entry = telemetry[index];
            return true;
        }

        private bool TryReadHorizonTelemetryEntry(int index, out SomaticTelemetryEntry entry)
        {
            entry = default;
            if (!_somaticHorizonTelemetry.TryReadOnlyNativeArray(out NativeArray<SomaticTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated ||
                (uint)index >= (uint)telemetry.Length)
            {
                return false;
            }

            entry = telemetry[index];
            return true;
        }

        private static void PublishComfortDumpFault(int hResult)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(ComfortDumpFaultHash, SomaticComfortTelemetryHash, hResult);
        }

        private static void WriteComfortTelemetryEntry(Span<byte> target, in ComfortTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(target, 0, entry.Frame);
            WriteUInt32LittleEndian(target, 4, entry.Flags);
            WriteFloatLittleEndian(target, 8, entry.PeakAngularVelocityRadS);
            WriteFloatLittleEndian(target, 12, entry.PeakAngularAccelerationRadS2);
            WriteFloatLittleEndian(target, 16, entry.PeakLinearAccelerationMps2);
            WriteFloatLittleEndian(target, 20, entry.FovTunnelingIntensity);
            WriteFloatLittleEndian(target, 24, entry.HorizonLockBlend);
            WriteFloatLittleEndian(target, 28, entry.FoveatedScaleMultiplier);
            WriteFloatLittleEndian(target, 32, entry.BurstExecutionMicroseconds);
            WriteFloatLittleEndian(target, 36, entry.ImpactShock01);
            WriteFloatLittleEndian(target, 40, entry.GlobalQualityWeight01);
            WriteFloatLittleEndian(target, 44, entry.Pressure01);
            WriteUInt32LittleEndian(target, 48, entry.LockContentionCount);
            WriteUInt32LittleEndian(target, 52, entry.StateHash);
            WriteUInt32LittleEndian(target, 56, entry.Sequence);
            WriteUInt32LittleEndian(target, 60, entry.AupHash);
        }

        private static void WriteHorizonTelemetryEntry(Span<byte> target, in SomaticTelemetryEntry entry)
        {
            WriteFloat4LittleEndian(target, 0, entry.StabilizedRotation.value);
            WriteFloat4LittleEndian(target, 16, entry.QuaternionDelta);
            WriteFloat3LittleEndian(target, 32, entry.RawAngularVelocity);
            WriteFloatLittleEndian(target, 44, entry.FovTunnelScalar);
            WriteFloatLittleEndian(target, 48, entry.PitchDampening);
            WriteFloatLittleEndian(target, 52, entry.BurstExecutionMicroseconds);
            WriteUInt32LittleEndian(target, 56, entry.Frame);
            WriteUInt32LittleEndian(target, 60, entry.Flags);
            WriteUInt32LittleEndian(target, 64, entry.StateHash);
            WriteUInt32LittleEndian(target, 68, entry.AupHash);
        }

        private static void WriteFloat4LittleEndian(Span<byte> target, int offset, float4 value)
        {
            WriteFloatLittleEndian(target, offset, value.x);
            WriteFloatLittleEndian(target, offset + 4, value.y);
            WriteFloatLittleEndian(target, offset + 8, value.z);
            WriteFloatLittleEndian(target, offset + 12, value.w);
        }

        private static void WriteFloat3LittleEndian(Span<byte> target, int offset, float3 value)
        {
            WriteFloatLittleEndian(target, offset, value.x);
            WriteFloatLittleEndian(target, offset + 4, value.y);
            WriteFloatLittleEndian(target, offset + 8, value.z);
        }

        private static void WriteFloatLittleEndian(Span<byte> target, int offset, float value)
        {
            WriteUInt32LittleEndian(target, offset, math.asuint(value));
        }

        private static void WriteUInt32LittleEndian(Span<byte> target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static uint ResolveSomaticComfortStateHash(in SomaticComfortStateDTO state, in SomaticDerivativeDTO derivative, uint flags)
        {
            uint hash = SomaticComfortTelemetryHash;
            hash = MixHash(hash, math.asuint(state.FovTunnelingIntensity));
            hash = MixHash(hash, math.asuint(state.HorizonLockBlend));
            hash = MixHash(hash, math.asuint(state.FoveatedScaleMultiplier));
            hash = MixHash(hash, math.asuint(derivative.PeakAngularVelocityRadS));
            hash = MixHash(hash, math.asuint(derivative.PeakAngularAccelerationRadS2));
            hash = MixHash(hash, math.asuint(derivative.PeakLinearAccelerationMps2));
            return MixHash(hash, flags);
        }

        private static uint ResolveAupHash(in AbsoluteUniversePosition aup)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, (uint)aup.GridX);
            hash = MixHash(hash, (uint)(aup.GridX >> 32));
            hash = MixHash(hash, (uint)aup.GridY);
            hash = MixHash(hash, (uint)(aup.GridY >> 32));
            hash = MixHash(hash, (uint)aup.GridZ);
            hash = MixHash(hash, (uint)(aup.GridZ >> 32));
            hash = MixHash(hash, math.asuint(aup.LocalX));
            hash = MixHash(hash, math.asuint(aup.LocalY));
            return MixHash(hash, math.asuint(aup.LocalZ));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawHorizonLockVectorsGizmo();
            if (!_somaticComfortTelemetry.IsCreated)
                return;

            if (!_somaticComfortTelemetry.TryReadOnlyNativeArray(out NativeArray<ComfortTelemetryEntry>.ReadOnly telemetry))
                return;
            if (!telemetry.IsCreated || telemetry.Length < 2)
                return;

            const int MaxGraphFrames = 60;
            int count = math.min(MaxGraphFrames, telemetry.Length);
            int start = _somaticTelemetryCursor - count;
            Vector3 origin = transform.position + (transform.forward * 1.1f) + (Vector3.up * 0.35f);

            for (int i = 1; i < count; i++)
            {
                ComfortTelemetryEntry previous = telemetry[PositiveModuloSomatic(start + i - 1, telemetry.Length)];
                ComfortTelemetryEntry current = telemetry[PositiveModuloSomatic(start + i, telemetry.Length)];
                float x0 = (i - 1) * 0.035f;
                float x1 = i * 0.035f;
                float raw0 = math.saturate(previous.PeakAngularVelocityRadS * math.rcp(16f));
                float raw1 = math.saturate(current.PeakAngularVelocityRadS * math.rcp(16f));
                float smoothed0 = math.saturate(previous.FovTunnelingIntensity);
                float smoothed1 = math.saturate(current.FovTunnelingIntensity);

                Vector3 rawPrevious = origin + (transform.right * x0) + (Vector3.up * raw0 * 0.35f);
                Vector3 rawCurrent = origin + (transform.right * x1) + (Vector3.up * raw1 * 0.35f);
                Vector3 smoothedPrevious = origin + (transform.right * x0) - (Vector3.up * 0.12f) + (Vector3.up * smoothed0 * 0.35f);
                Vector3 smoothedCurrent = origin + (transform.right * x1) - (Vector3.up * 0.12f) + (Vector3.up * smoothed1 * 0.35f);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(rawPrevious, rawCurrent);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(smoothedPrevious, smoothedCurrent);
            }
        }
#endif

        private void InvalidateSomaticComfortPublishCache()
        {
            _lastPublishedSomaticComfortState = Vector4.positiveInfinity;
        }

        private void PublishSomaticComfortShaderState()
        {
            float foveatedScale = math.max(1f, math.isfinite(_somaticFoveatedScaleMultiplier) ? _somaticFoveatedScaleMultiplier : 1f);
            float pressure01 = math.max(
                math.max(Sanitize01(_somaticVramPressure01, 0f), Sanitize01(_somaticThermalPressure01, 0f)),
                Sanitize01(_somaticSystemPressure01, 0f));
            Vector4 state = new Vector4(
                Sanitize01(_somaticFovTunnelingIntensity01, 0f),
                Sanitize01(_somaticHorizonLockBlend01, 0f),
                foveatedScale,
                pressure01);
            if (Approximately(in state, in _lastPublishedSomaticComfortState))
                return;

            _pendingSomaticComfortState = state;
            _somaticComfortShaderStateDirty = true;
            TryRegisterLateFrame();
        }

        private void FlushQueuedSomaticComfortShaderState()
        {
            if (!_somaticComfortShaderStateDirty)
                return;

            _somaticComfortShaderStateDirty = false;
            if (Approximately(in _pendingSomaticComfortState, in _lastPublishedSomaticComfortState))
                return;

            Shader.SetGlobalVector(VrSomaticComfortStateId, _pendingSomaticComfortState);
            _lastPublishedSomaticComfortState = _pendingSomaticComfortState;
        }

        private static bool TryParseComfortProfileRow(ReadOnlySpan<byte> row, out VrComfortProfileDTO profile)
        {
            profile = default;
            row = TrimAscii(row);
            if (row.Length == 0 || row[0] == (byte)'#' || IsHeaderRow(row))
                return false;

            profile = DefaultNoviceProfile();
            int field = 0;
            int start = 0;
            while (start <= row.Length)
            {
                int end = start;
                while (end < row.Length && row[end] != (byte)',')
                    end++;

                ReadOnlySpan<byte> token = TrimAscii(row.Slice(start, end - start));
                if (field == 0)
                {
                    profile.ProfileHash = Fnv1aAscii(token);
                }
                else if (TryParseFloatAscii(token, out float value))
                {
                    ApplyComfortProfileField(ref profile, field, value);
                }

                field++;
                if (end >= row.Length)
                    break;
                start = end + 1;
            }

            return profile.ProfileHash != 0u;
        }

        private static void ApplyComfortProfileField(ref VrComfortProfileDTO profile, int field, float value)
        {
            switch (field)
            {
                case 1: profile.UserComfortWeight01 = value; break;
                case 2: profile.FovAggressiveness = value; break;
                case 3: profile.HorizonLockSpeed = value; break;
                case 4: profile.FoveatedBaseline = value; break;
                case 5: profile.AngularVelocitySoftRadS = value; break;
                case 6: profile.AngularAccelerationSoftRadS2 = value; break;
                case 7: profile.LinearAccelerationSoftMps2 = value; break;
                case 8: profile.EwmaSharpness = value; break;
                case 9: profile.ImpactShockWeight = value; break;
                case 10: profile.FlatScreenBaselineFovTunnel = value; break;
                case 11: profile.VrBaselineFovTunnel = value; break;
                case 12: profile.ReleaseSharpness = value; break;
                case 13: profile.MockAmplitude = value; break;
            }
        }

        private static VrComfortProfileDTO SanitizeProfile(VrComfortProfileDTO profile)
        {
            profile.UserComfortWeight01 = Sanitize01(profile.UserComfortWeight01, 1f);
            profile.FovAggressiveness = math.max(0f, math.isfinite(profile.FovAggressiveness) ? profile.FovAggressiveness : 1f);
            profile.HorizonLockSpeed = math.max(0f, math.isfinite(profile.HorizonLockSpeed) ? profile.HorizonLockSpeed : 12f);
            profile.FoveatedBaseline = math.max(0f, math.isfinite(profile.FoveatedBaseline) ? profile.FoveatedBaseline : 0.08f);
            profile.AngularVelocitySoftRadS = math.max(0.01f, math.isfinite(profile.AngularVelocitySoftRadS) ? profile.AngularVelocitySoftRadS : 1.2f);
            profile.AngularAccelerationSoftRadS2 = math.max(0.01f, math.isfinite(profile.AngularAccelerationSoftRadS2) ? profile.AngularAccelerationSoftRadS2 : 34f);
            profile.LinearAccelerationSoftMps2 = math.max(0.01f, math.isfinite(profile.LinearAccelerationSoftMps2) ? profile.LinearAccelerationSoftMps2 : 7f);
            profile.EwmaSharpness = math.max(0.01f, math.isfinite(profile.EwmaSharpness) ? profile.EwmaSharpness : 18f);
            profile.ImpactShockWeight = math.max(0f, math.isfinite(profile.ImpactShockWeight) ? profile.ImpactShockWeight : 0.9f);
            profile.FlatScreenBaselineFovTunnel = Sanitize01(profile.FlatScreenBaselineFovTunnel, 0.05f);
            profile.VrBaselineFovTunnel = Sanitize01(profile.VrBaselineFovTunnel, 0.8f);
            profile.ReleaseSharpness = math.max(0.01f, math.isfinite(profile.ReleaseSharpness) ? profile.ReleaseSharpness : 10f);
            profile.MockAmplitude = math.max(0f, math.isfinite(profile.MockAmplitude) ? profile.MockAmplitude : 1f);
            return profile;
        }

        private static VrComfortProfileDTO DefaultNoviceProfile()
        {
            return new VrComfortProfileDTO
            {
                ProfileHash = SomaticProfileNoviceHash,
                UserComfortWeight01 = 1f,
                FovAggressiveness = 1.1f,
                HorizonLockSpeed = 18f,
                FoveatedBaseline = 0.12f,
                AngularVelocitySoftRadS = 1.0f,
                AngularAccelerationSoftRadS2 = 28f,
                LinearAccelerationSoftMps2 = 6.0f,
                EwmaSharpness = 22f,
                ImpactShockWeight = 1.0f,
                FlatScreenBaselineFovTunnel = 0.05f,
                VrBaselineFovTunnel = 0.82f,
                ReleaseSharpness = 9f,
                MockAmplitude = 1f
            };
        }

        private static VrComfortProfileDTO DefaultVeteranProfile()
        {
            VrComfortProfileDTO profile = DefaultNoviceProfile();
            profile.ProfileHash = SomaticProfileVeteranHash;
            profile.FovAggressiveness = 0.72f;
            profile.HorizonLockSpeed = 12f;
            profile.FoveatedBaseline = 0.08f;
            profile.AngularVelocitySoftRadS = 1.4f;
            profile.AngularAccelerationSoftRadS2 = 42f;
            profile.LinearAccelerationSoftMps2 = 9f;
            profile.VrBaselineFovTunnel = 0.62f;
            return profile;
        }

        private static VrComfortProfileDTO DefaultDisabledProfile()
        {
            VrComfortProfileDTO profile = DefaultNoviceProfile();
            profile.ProfileHash = SomaticProfileDisabledHash;
            profile.UserComfortWeight01 = 0f;
            profile.FovAggressiveness = 0f;
            profile.HorizonLockSpeed = 0f;
            profile.FoveatedBaseline = 0f;
            profile.FlatScreenBaselineFovTunnel = 0f;
            profile.VrBaselineFovTunnel = 0f;
            return profile;
        }

        private static VrComfortProfileDTO DefaultQuest3Profile()
        {
            VrComfortProfileDTO profile = DefaultVeteranProfile();
            profile.ProfileHash = SomaticProfileQuest3Hash;
            profile.FoveatedBaseline = 0.1f;
            profile.AngularAccelerationSoftRadS2 = 36f;
            profile.VrBaselineFovTunnel = 0.7f;
            return profile;
        }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        public static unsafe bool RunSomaticComfortFuzzerForTests(out float peakIntensity01, out float finalIntensity01, out uint finalFlags)
        {
            peakIntensity01 = 0f;
            finalIntensity01 = 0f;
            finalFlags = 0u;

            NativeArray<SomaticComfortStateDTO> stateBuffer = new NativeArray<SomaticComfortStateDTO>(
                1,
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            NativeArray<SomaticDerivativeDTO> derivativeBuffer = new NativeArray<SomaticDerivativeDTO>(
                1,
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);
            NativeArray<VrComfortProfileDTO> profileBuffer = new NativeArray<VrComfortProfileDTO>(
                1,
                Allocator.TempJob,
                NativeArrayOptions.ClearMemory);

            try
            {
                profileBuffer[0] = DefaultNoviceProfile();
                float previous = 0f;
                for (int i = 0; i < 512; i++)
                {
                    bool burstSpike = (i & 7) == 0;
                    derivativeBuffer[0] = new SomaticDerivativeDTO
                    {
                        PeakAngularVelocityRadS = burstSpike ? 174.53293f : 0.15f,
                        PeakAngularAccelerationRadS2 = burstSpike ? 10471.976f : 0.25f,
                        PeakLinearAccelerationMps2 = burstSpike ? 120f : 0.1f,
                        Flags = SomaticComfortFlagMockData
                    };

                    EvaluateFovTunnelingJob job = new EvaluateFovTunnelingJob
                    {
                        DeltaTime = 1f / 90f,
                        GlobalQualityWeight01 = (i & 1) == 0 ? 0f : 1f,
                        RuntimeComfortBlend01 = 1f,
                        ImpactShock01 = burstSpike ? 1f : 0f,
                        VramPressure01 = burstSpike ? 0.8f : 0f,
                        ThermalPressure01 = 0f,
                        SystemPressure01 = burstSpike ? 0.5f : 0f,
                        KccAngularVelocityRadS = burstSpike ? 174.53293f : 0f,
                        KccAngularAccelerationRadS2 = burstSpike ? 10471.976f : 0f,
                        State = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateBuffer),
                        Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivativeBuffer),
                        Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileBuffer)
                    };
                    job.Execute();

                    SomaticComfortStateDTO state = stateBuffer[0];
                    float current = state.FovTunnelingIntensity;
                    if (!math.isfinite(current) ||
                        !math.isfinite(state.FoveatedScaleMultiplier) ||
                        current < 0f ||
                        current > 1f ||
                        state.FoveatedScaleMultiplier < 1f ||
                        math.abs(current - previous) > 0.35f)
                    {
                        peakIntensity01 = math.max(peakIntensity01, math.saturate(current));
                        finalIntensity01 = current;
                        finalFlags = state.ActiveComfortFlags;
                        return false;
                    }

                    peakIntensity01 = math.max(peakIntensity01, current);
                    previous = current;
                }

                SomaticComfortStateDTO finalState = stateBuffer[0];
                finalIntensity01 = finalState.FovTunnelingIntensity;
                finalFlags = finalState.ActiveComfortFlags;
                return peakIntensity01 > 0.001f && math.isfinite(finalIntensity01);
            }
            finally
            {
                if (profileBuffer.IsCreated)
                    profileBuffer.Dispose();
                if (derivativeBuffer.IsCreated)
                    derivativeBuffer.Dispose();
                if (stateBuffer.IsCreated)
                    stateBuffer.Dispose();
            }
        }
#endif

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsAsciiWhitespaceOrQuote(value[start]))
                start++;
            while (end >= start && IsAsciiWhitespaceOrQuote(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool IsAsciiWhitespaceOrQuote(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'"';
        }

        private static bool IsHeaderRow(ReadOnlySpan<byte> row)
        {
            if (row.Length < 4)
                return false;

            return (byte)(row[0] | 0x20) == (byte)'n' &&
                   (byte)(row[1] | 0x20) == (byte)'a' &&
                   (byte)(row[2] | 0x20) == (byte)'m' &&
                   (byte)(row[3] | 0x20) == (byte)'e';
        }

        private static uint Fnv1aAscii(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value == (byte)'"' || value == (byte)' ' || value == (byte)'\t')
                    continue;
                hash = (hash ^ value) * 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }

        private static void InsertProfileLookup(
            NativeArray<VrComfortProfileLookupSlotDTO> lookup,
            uint profileHash,
            int profileIndex)
        {
            if (!lookup.IsCreated || lookup.Length == 0 || profileHash == 0u)
                return;

            int start = (int)(profileHash % (uint)lookup.Length);
            for (int i = 0; i < lookup.Length; i++)
            {
                int slotIndex = (start + i) % lookup.Length;
                VrComfortProfileLookupSlotDTO slot = lookup[slotIndex];
                if (slot.Occupied == 0u || slot.ProfileHash == profileHash)
                {
                    lookup[slotIndex] = new VrComfortProfileLookupSlotDTO
                    {
                        ProfileHash = profileHash,
                        ProfileIndex = profileIndex,
                        Occupied = 1u
                    };
                    return;
                }
            }
        }

        private static bool TryParseFloatAscii(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = TrimAscii(token);
            if (token.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (token[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigits = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                integer = (integer * 10f) + (token[index] - (byte)'0');
                index++;
                hasDigits = true;
            }

            float fraction = 0f;
            float denominator = 1f;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (token[index] - (byte)'0');
                    denominator *= 10f;
                    index++;
                    hasDigits = true;
                }
            }

            if (!hasDigits)
                return false;

            value = sign * (integer + (fraction * math.rcp(math.max(denominator, 1f))));
            return math.isfinite(value);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ComputeSomaticDerivativesJob : IJob
        {
            public AbsoluteUniversePosition CurrentAup;
            public quaternion CurrentRotation;
            public float DeltaTime;
            public int HistoryDepth;
            public uint Frame;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticKinematicHistoryDTO* History;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticDerivativeDTO* Derivatives;

            public unsafe void Execute()
            {
                ref SomaticKinematicHistoryDTO history = ref UnsafeUtility.AsRef<SomaticKinematicHistoryDTO>(History);
                ref SomaticDerivativeDTO derivatives = ref UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives);
                float dt = math.max(DeltaTime, MinimumDeltaTime);
                quaternion currentRotation = SanitizeJobQuaternion(CurrentRotation, quaternion.identity);
                if ((history.Flags & SomaticHistoryValidFlag) == 0u)
                {
                    history.PreviousAup = CurrentAup;
                    history.PreviousRotation = currentRotation;
                    history.PreviousVelocity = float3.zero;
                    history.PreviousAngularVelocity = float3.zero;
                    history.PreviousFrame = Frame;
                    history.Flags = SomaticHistoryValidFlag;
                    derivatives = default;
                    return;
                }

                float3 localDelta = ResolveLocalAupDeltaMeters(in CurrentAup, in history.PreviousAup);
                uint frameDelta = Frame >= history.PreviousFrame ? Frame - history.PreviousFrame : 1u;
                if (frameDelta == 0u)
                    frameDelta = 1u;
                if (frameDelta > 120u)
                    frameDelta = 120u;
                float sampleDt = math.max(dt * frameDelta, MinimumDeltaTime);
                float invDt = math.rcp(sampleDt);
                float historyBlend = math.rcp(math.max(1f, HistoryDepth));
                float3 rawLinearVelocity = localDelta * invDt;
                float3 linearVelocity = math.lerp(SanitizeJobFloat3(history.PreviousVelocity), rawLinearVelocity, historyBlend);
                linearVelocity = ClampLength(linearVelocity, 80f);
                float3 linearAcceleration = (linearVelocity - SanitizeJobFloat3(history.PreviousVelocity)) * invDt;
                linearAcceleration = ClampLength(linearAcceleration, 240f);

                quaternion previousRotation = SanitizeJobQuaternion(history.PreviousRotation, currentRotation);
                quaternion deltaRotation = SanitizeJobQuaternion(math.mul(currentRotation, math.inverse(previousRotation)), quaternion.identity);
                float4 delta = deltaRotation.value;
                if (delta.w < 0f)
                    delta = -delta;

                float vectorSq = math.lengthsq(delta.xyz);
                float angle = 0f;
                float3 axis = new float3(0f, 1f, 0f);
                if (math.isfinite(vectorSq) && vectorSq > 0.0000001f)
                {
                    float vectorLength = math.sqrt(vectorSq);
                    axis = delta.xyz * math.rcp(math.max(vectorLength, 0.0001f));
                    angle = 2f * MathLodApproximation.ApproxAtan2Fast(vectorLength, math.max(math.abs(delta.w), 0.0001f));
                }

                if (angle > math.PI)
                    angle -= 2f * math.PI;

                float3 rawAngularVelocity = ClampLength(axis * angle * invDt, 48f);
                float3 angularVelocity = ClampLength(math.lerp(SanitizeJobFloat3(history.PreviousAngularVelocity), rawAngularVelocity, historyBlend), 48f);
                float3 angularAcceleration = ClampLength((angularVelocity - SanitizeJobFloat3(history.PreviousAngularVelocity)) * invDt, 480f);
                uint flags = 0u;
                if (!math.all(math.isfinite(linearVelocity)) ||
                    !math.all(math.isfinite(linearAcceleration)) ||
                    !math.all(math.isfinite(angularVelocity)) ||
                    !math.all(math.isfinite(angularAcceleration)))
                {
                    flags |= SomaticDerivativeNonFiniteFlag;
                    linearVelocity = float3.zero;
                    linearAcceleration = float3.zero;
                    angularVelocity = float3.zero;
                    angularAcceleration = float3.zero;
                }

                history.PreviousAup = CurrentAup;
                history.PreviousRotation = currentRotation;
                history.PreviousVelocity = linearVelocity;
                history.PreviousAngularVelocity = angularVelocity;
                history.PreviousFrame = Frame;
                history.Flags = SomaticHistoryValidFlag;
                derivatives = new SomaticDerivativeDTO
                {
                    LinearVelocity = linearVelocity,
                    LinearAcceleration = linearAcceleration,
                    AngularVelocity = angularVelocity,
                    AngularAcceleration = angularAcceleration,
                    PeakAngularVelocityRadS = math.length(angularVelocity),
                    PeakAngularAccelerationRadS2 = math.length(angularAcceleration),
                    PeakLinearAccelerationMps2 = math.length(linearAcceleration),
                    Flags = flags
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateFovTunnelingJob : IJob
        {
            public float DeltaTime;
            public float GlobalQualityWeight01;
            public float RuntimeComfortBlend01;
            public float ImpactShock01;
            public float VramPressure01;
            public float ThermalPressure01;
            public float SystemPressure01;
            public float KccAngularVelocityRadS;
            public float KccAngularAccelerationRadS2;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticComfortStateDTO* State;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticDerivativeDTO* Derivatives;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VrComfortProfileDTO* Profile;

            public unsafe void Execute()
            {
                ref SomaticComfortStateDTO state = ref UnsafeUtility.AsRef<SomaticComfortStateDTO>(State);
                ref SomaticDerivativeDTO derivatives = ref UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives);
                VrComfortProfileDTO profile = SanitizeJobProfile(UnsafeUtility.AsRef<VrComfortProfileDTO>(Profile));
                float quality = SanitizeJob01(GlobalQualityWeight01, 1f);
                float qualityCurve = SmoothJob01(quality);
                float comfortWeight = SanitizeJob01(profile.UserComfortWeight01, 1f);
                float derivativeAngularSpeed = SanitizeJobNonNegative(derivatives.PeakAngularVelocityRadS);
                float derivativeAngularAcceleration = SanitizeJobNonNegative(derivatives.PeakAngularAccelerationRadS2);
                float derivativeLinearAcceleration = SanitizeJobNonNegative(derivatives.PeakLinearAccelerationMps2);
                float angularSpeed = math.max(derivativeAngularSpeed, math.abs(math.select(KccAngularVelocityRadS, 0f, !math.isfinite(KccAngularVelocityRadS))));
                float angularAcceleration = math.max(derivativeAngularAcceleration, math.max(0f, math.select(KccAngularAccelerationRadS2, 0f, !math.isfinite(KccAngularAccelerationRadS2))));
                float linearAcceleration = derivativeLinearAcceleration;

                float velocityThreshold = math.max(0.01f, profile.AngularVelocitySoftRadS * math.lerp(0.78f, 1.22f, qualityCurve));
                float angularAccThreshold = math.max(0.01f, profile.AngularAccelerationSoftRadS2 * math.lerp(0.72f, 1.18f, qualityCurve));
                float linearAccThreshold = math.max(0.01f, profile.LinearAccelerationSoftMps2 * math.lerp(0.78f, 1.12f, qualityCurve));
                float speed01 = math.saturate((angularSpeed - velocityThreshold) * math.rcp(math.max(velocityThreshold, 0.01f)));
                float angularAcc01 = math.saturate((angularAcceleration - angularAccThreshold) * math.rcp(math.max(angularAccThreshold, 0.01f)));
                float linearAcc01 = math.saturate((linearAcceleration - linearAccThreshold) * math.rcp(math.max(linearAccThreshold, 0.01f)));
                float shock01 = SanitizeJob01(ImpactShock01, 0f) * math.max(0f, profile.ImpactShockWeight);
                float motion01 = math.max(math.max(SmoothJob01(speed01), SmoothJob01(angularAcc01)), math.max(SmoothJob01(linearAcc01), shock01));
                float interventionStrength = math.lerp(profile.FlatScreenBaselineFovTunnel, profile.VrBaselineFovTunnel, SanitizeJob01(RuntimeComfortBlend01, 0f));
                float responseGain = math.max(0f, profile.FovAggressiveness) * math.lerp(1.18f, 0.84f, qualityCurve);
                float target = math.saturate(motion01 * interventionStrength * responseGain * comfortWeight);

                float currentTunnel = SanitizeJob01(state.FovTunnelingIntensity, 0f);
                float sharpness = target > currentTunnel ? profile.EwmaSharpness : profile.ReleaseSharpness;
                float blend = 1f - MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0.01f, sharpness) * math.max(DeltaTime, MinimumDeltaTime));
                state.FovTunnelingIntensity = SanitizeJob01(math.lerp(currentTunnel, target, SanitizeJob01(blend, 0f)), currentTunnel);

                float pressure = math.max(math.max(SanitizeJob01(VramPressure01, 0f), SanitizeJob01(ThermalPressure01, 0f)), SanitizeJob01(SystemPressure01, 0f));
                float lowQualityCurve = SmoothJob01((0.3f - quality) * 3.3333333f);
                float pressureGain = math.lerp(0.9f, 1.45f, 1f - qualityCurve) + (lowQualityCurve * 0.25f);
                state.FoveatedScaleMultiplier = math.max(1f, 1f + profile.FoveatedBaseline + (pressure * pressureGain));
                uint flags = state.ActiveComfortFlags;
                flags = state.FovTunnelingIntensity > 0.001f ? (flags | SomaticComfortFlagFovTunnel) : (flags & ~SomaticComfortFlagFovTunnel);
                flags = pressure > 0.001f ? (flags | SomaticComfortFlagFoveatedPressure) : (flags & ~SomaticComfortFlagFoveatedPressure);
                flags = shock01 > 0.001f ? (flags | SomaticComfortFlagImpactShock) : (flags & ~SomaticComfortFlagImpactShock);
                flags = (derivatives.Flags & SomaticComfortFlagMockData) != 0u ? (flags | SomaticComfortFlagMockData) : (flags & ~SomaticComfortFlagMockData);
                if ((derivatives.Flags & SomaticDerivativeNonFiniteFlag) != 0u)
                    flags |= SomaticDerivativeNonFiniteFlag;
                state.ActiveComfortFlags = flags;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateComfortAndHorizonJob : IJob
        {
            public quaternion CurrentRotation;
            public float DeltaTime;
            public float GlobalQualityWeight01;
            public float RuntimeComfortBlend01;
            public float ImpactShock01;
            public float VramPressure01;
            public float ThermalPressure01;
            public float SystemPressure01;
            public float KccAngularVelocityRadS;
            public float KccAngularAccelerationRadS2;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticComfortStateDTO* State;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticDerivativeDTO* Derivatives;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VrComfortProfileDTO* Profile;

            public unsafe void Execute()
            {
                ref SomaticComfortStateDTO state = ref UnsafeUtility.AsRef<SomaticComfortStateDTO>(State);
                ref SomaticDerivativeDTO derivatives = ref UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives);
                VrComfortProfileDTO profile = SanitizeJobProfile(UnsafeUtility.AsRef<VrComfortProfileDTO>(Profile));
                float quality = SanitizeJob01(GlobalQualityWeight01, 1f);
                float qualityCurve = SmoothJob01(quality);
                float comfortWeight = SanitizeJob01(profile.UserComfortWeight01, 1f);
                float derivativeAngularSpeed = SanitizeJobNonNegative(derivatives.PeakAngularVelocityRadS);
                float derivativeAngularAcceleration = SanitizeJobNonNegative(derivatives.PeakAngularAccelerationRadS2);
                float derivativeLinearAcceleration = SanitizeJobNonNegative(derivatives.PeakLinearAccelerationMps2);
                float angularSpeed = math.max(derivativeAngularSpeed, math.abs(math.select(KccAngularVelocityRadS, 0f, !math.isfinite(KccAngularVelocityRadS))));
                float angularAcceleration = math.max(derivativeAngularAcceleration, math.max(0f, math.select(KccAngularAccelerationRadS2, 0f, !math.isfinite(KccAngularAccelerationRadS2))));
                float linearAcceleration = derivativeLinearAcceleration;

                float velocityThreshold = math.max(0.01f, profile.AngularVelocitySoftRadS * math.lerp(0.78f, 1.22f, qualityCurve));
                float angularAccThreshold = math.max(0.01f, profile.AngularAccelerationSoftRadS2 * math.lerp(0.72f, 1.18f, qualityCurve));
                float linearAccThreshold = math.max(0.01f, profile.LinearAccelerationSoftMps2 * math.lerp(0.78f, 1.12f, qualityCurve));
                float speed01 = math.saturate((angularSpeed - velocityThreshold) * math.rcp(math.max(velocityThreshold, 0.01f)));
                float angularAcc01 = math.saturate((angularAcceleration - angularAccThreshold) * math.rcp(math.max(angularAccThreshold, 0.01f)));
                float linearAcc01 = math.saturate((linearAcceleration - linearAccThreshold) * math.rcp(math.max(linearAccThreshold, 0.01f)));
                float shock01 = SanitizeJob01(ImpactShock01, 0f) * math.max(0f, profile.ImpactShockWeight);
                float motion01 = math.max(math.max(SmoothJob01(speed01), SmoothJob01(angularAcc01)), math.max(SmoothJob01(linearAcc01), shock01));
                float interventionStrength = math.lerp(profile.FlatScreenBaselineFovTunnel, profile.VrBaselineFovTunnel, SanitizeJob01(RuntimeComfortBlend01, 0f));
                float responseGain = math.max(0f, profile.FovAggressiveness) * math.lerp(1.18f, 0.84f, qualityCurve);
                float target = math.saturate(motion01 * interventionStrength * responseGain * comfortWeight);

                float currentTunnel = SanitizeJob01(state.FovTunnelingIntensity, 0f);
                float tunnelSharpness = target > currentTunnel ? profile.EwmaSharpness : profile.ReleaseSharpness;
                float tunnelBlend = 1f - MathLodApproximation.ApproxExpNegPade33Wide40(math.max(0.01f, tunnelSharpness) * math.max(DeltaTime, MinimumDeltaTime));
                state.FovTunnelingIntensity = SanitizeJob01(math.lerp(currentTunnel, target, SanitizeJob01(tunnelBlend, 0f)), currentTunnel);

                float pressure = math.max(math.max(SanitizeJob01(VramPressure01, 0f), SanitizeJob01(ThermalPressure01, 0f)), SanitizeJob01(SystemPressure01, 0f));
                float lowQualityCurve = SmoothJob01((0.3f - quality) * 3.3333333f);
                float pressureGain = math.lerp(0.9f, 1.45f, 1f - qualityCurve) + (lowQualityCurve * 0.25f);
                state.FoveatedScaleMultiplier = math.max(1f, 1f + profile.FoveatedBaseline + (pressure * pressureGain));
                uint flags = state.ActiveComfortFlags;
                flags = state.FovTunnelingIntensity > 0.001f ? (flags | SomaticComfortFlagFovTunnel) : (flags & ~SomaticComfortFlagFovTunnel);
                flags = pressure > 0.001f ? (flags | SomaticComfortFlagFoveatedPressure) : (flags & ~SomaticComfortFlagFoveatedPressure);
                flags = shock01 > 0.001f ? (flags | SomaticComfortFlagImpactShock) : (flags & ~SomaticComfortFlagImpactShock);
                flags = (derivatives.Flags & SomaticComfortFlagMockData) != 0u ? (flags | SomaticComfortFlagMockData) : (flags & ~SomaticComfortFlagMockData);
                if ((derivatives.Flags & SomaticDerivativeNonFiniteFlag) != 0u)
                    flags |= SomaticDerivativeNonFiniteFlag;
                state.ActiveComfortFlags = flags;

                quaternion current = SanitizeJobQuaternion(CurrentRotation, quaternion.identity);
                float3 up = math.rotate(current, new float3(0f, 1f, 0f));
                float3 forward = math.rotate(current, new float3(0f, 0f, 1f));
                float3 levelForward = new float3(forward.x, 0f, forward.z);
                float lenSq = math.lengthsq(levelForward);
                if (!math.isfinite(lenSq) || lenSq < 0.000001f)
                    levelForward = new float3(0f, 0f, 1f);
                else
                    levelForward *= math.rsqrt(lenSq);

                quaternion levelRotation = quaternion.LookRotationSafe(levelForward, new float3(0f, 1f, 0f));
                quaternion correction = SanitizeJobQuaternion(math.mul(levelRotation, math.inverse(current)), quaternion.identity);
                float upError = math.saturate(math.lengthsq(math.cross(up, new float3(0f, 1f, 0f))));
                float accelerationAssist = SmoothJob01(derivativeAngularAcceleration * math.rcp(math.max(profile.AngularAccelerationSoftRadS2 * 2f, 0.01f)));
                float horizonTarget = math.saturate(math.max(SmoothJob01(upError), accelerationAssist) * comfortWeight * math.lerp(1.12f, 0.88f, quality));
                float currentBlend = SanitizeJob01(state.HorizonLockBlend, 0f);
                float springOmega = math.max(0.01f, profile.HorizonLockSpeed) * math.lerp(4.75f, 2.35f, quality);
                float horizonBlend = ResolveCriticalDampedSpringBlend(springOmega, DeltaTime, quality);
                state.HorizonLockBlend = SanitizeJob01(math.lerp(currentBlend, horizonTarget, SanitizeJob01(horizonBlend, 0f)), currentBlend);
                state.ReservedParameters = correction.value;
                if (state.HorizonLockBlend > 0.001f)
                    state.ActiveComfortFlags |= SomaticComfortFlagHorizonLock;
                else
                    state.ActiveComfortFlags &= ~SomaticComfortFlagHorizonLock;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockSicknessDataJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<SomaticMockSicknessSampleDTO> Samples;
            public float GlobalQualityWeight01;
            public uint Frame;

            public void Execute(int index)
            {
                float q = SanitizeJob01(GlobalQualityWeight01, 1f);
                float t = index * math.lerp(0.2f, 0.055f, SmoothJob01(q));
                float triangle = math.abs(frac(t * 1.73f) * 2f - 1f);
                float signedPulse = (triangle * 2f) - 1f;
                float amplitude = math.lerp(0.65f, 1.35f, SmoothJob01(q));
                float yaw = signedPulse * 1.35f * amplitude;
                float roll = MathLodApproximation.ApproxSinBhaskara(t * 4.7f) * 0.72f * amplitude;
                Samples[index] = new SomaticMockSicknessSampleDTO
                {
                    LinearAcceleration = new float3(MathLodApproximation.ApproxSinBhaskara(t * 3.1f), signedPulse * 1.8f, MathLodApproximation.ApproxCosBhaskara(t * 2.3f)) * 9.5f * amplitude,
                    AngularVelocity = new float3(roll, yaw, MathLodApproximation.ApproxSinBhaskara(t * 5.2f) * 0.42f) * 6f,
                    AngularAcceleration = new float3(MathLodApproximation.ApproxCosBhaskara(t * 3.4f), signedPulse, MathLodApproximation.ApproxSinBhaskara(t * 6.1f)) * 85f * amplitude,
                    Rotation = math.mul(quaternion.RotateY(yaw), quaternion.RotateZ(roll)),
                    Frame = Frame + (uint)index,
                    Flags = SomaticComfortFlagMockData,
                    TimeSeconds = t
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothJob01(float value)
        {
            float x = SanitizeJob01(value, 0f);
            return x * x * (3f - (2f * x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeJob01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeJobNonNegative(float value)
        {
            return math.max(0f, math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float frac(float value)
        {
            return value - math.floor(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeJobFloat3(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ClampLength(float3 value, float maxLength)
        {
            float lenSq = math.lengthsq(value);
            if (!math.isfinite(lenSq))
                return float3.zero;
            float maxSq = maxLength * maxLength;
            if (lenSq <= maxSq)
                return value;
            return value * math.rsqrt(math.max(lenSq, 0.000001f)) * maxLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveModuloSomatic(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion SanitizeJobQuaternion(quaternion value, quaternion fallback)
        {
            float4 q = value.value;
            float lenSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lenSq) || lenSq < 0.000001f)
                return fallback;
            return new quaternion(math.normalizesafe(q, fallback.value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveLocalAupDeltaMeters(in AbsoluteUniversePosition current, in AbsoluteUniversePosition previous)
        {
            const double CellSize = AbsoluteUniversePosition.CellSizeMeters;
            const double MaxLocalDeltaMeters = 1000000.0;
            double3 delta = new double3(
                (((double)current.GridX - previous.GridX) * CellSize) + ((double)current.LocalX - previous.LocalX),
                (((double)current.GridY - previous.GridY) * CellSize) + ((double)current.LocalY - previous.LocalY),
                (((double)current.GridZ - previous.GridZ) * CellSize) + ((double)current.LocalZ - previous.LocalZ));
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double3 clampedDelta = math.clamp(
                delta,
                new double3(-MaxLocalDeltaMeters),
                new double3(MaxLocalDeltaMeters));
            return SanitizeJobFloat3(new float3((float)clampedDelta.x, (float)clampedDelta.y, (float)clampedDelta.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VrComfortProfileDTO SanitizeJobProfile(VrComfortProfileDTO profile)
        {
            profile.UserComfortWeight01 = math.saturate(math.isfinite(profile.UserComfortWeight01) ? profile.UserComfortWeight01 : 1f);
            profile.FovAggressiveness = math.max(0f, math.isfinite(profile.FovAggressiveness) ? profile.FovAggressiveness : 1f);
            profile.HorizonLockSpeed = math.max(0.01f, math.isfinite(profile.HorizonLockSpeed) ? profile.HorizonLockSpeed : 12f);
            profile.FoveatedBaseline = math.max(0f, math.isfinite(profile.FoveatedBaseline) ? profile.FoveatedBaseline : 0.1f);
            profile.AngularVelocitySoftRadS = math.max(0.01f, math.isfinite(profile.AngularVelocitySoftRadS) ? profile.AngularVelocitySoftRadS : 1.2f);
            profile.AngularAccelerationSoftRadS2 = math.max(0.01f, math.isfinite(profile.AngularAccelerationSoftRadS2) ? profile.AngularAccelerationSoftRadS2 : 34f);
            profile.LinearAccelerationSoftMps2 = math.max(0.01f, math.isfinite(profile.LinearAccelerationSoftMps2) ? profile.LinearAccelerationSoftMps2 : 7f);
            profile.EwmaSharpness = math.max(0.01f, math.isfinite(profile.EwmaSharpness) ? profile.EwmaSharpness : 18f);
            profile.ImpactShockWeight = math.max(0f, math.isfinite(profile.ImpactShockWeight) ? profile.ImpactShockWeight : 0.9f);
            profile.FlatScreenBaselineFovTunnel = math.saturate(math.isfinite(profile.FlatScreenBaselineFovTunnel) ? profile.FlatScreenBaselineFovTunnel : 0.05f);
            profile.VrBaselineFovTunnel = math.saturate(math.isfinite(profile.VrBaselineFovTunnel) ? profile.VrBaselineFovTunnel : 0.8f);
            profile.ReleaseSharpness = math.max(0.01f, math.isfinite(profile.ReleaseSharpness) ? profile.ReleaseSharpness : 10f);
            return profile;
        }
    }
}
