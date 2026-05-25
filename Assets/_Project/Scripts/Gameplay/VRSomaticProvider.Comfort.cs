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
        [FieldOffset(60)] public uint _pad0;
    }

    /// <summary>Open-addressed Vault profile lookup slot. Size: 16 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VrComfortProfileLookupSlotDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public int ProfileIndex;
        [FieldOffset(8)] public uint Occupied;
        [FieldOffset(12)] public uint _pad0;
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

    /// <summary>Fixed 300-frame comfort telemetry row. Size: 80 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
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
        [FieldOffset(44)] public float VramPressure01;
        [FieldOffset(48)] public float ThermalPressure01;
        [FieldOffset(52)] public float SystemPressure01;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Sequence;
        [FieldOffset(64)] public uint AupHash;
        [FieldOffset(68)] public uint _pad0;
        [FieldOffset(72)] public ulong _pad1;
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
        private const int SomaticCsvScratchBytes = 4096;
        private const int SomaticComfortStateBytes = 32;
        private const int ComfortTelemetryEntryBytes = 80;
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
        private VaultBufferView<byte> _somaticCsvScratch;
        private JobHandle _somaticComfortHandle;
        private bool _somaticComfortBuffersSeeded;
        private bool _somaticComfortJobScheduled;
        private bool _somaticComfortTelemetryDumped;
        private uint _somaticTelemetrySequence;
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

            NativeArray<SomaticMockSicknessSampleDTO> samples = _somaticMockSicknessSamples.AsNativeArray();
            NativeArray<SomaticComfortStateDTO> write = _somaticComfortWrite.AsNativeArray();
            NativeArray<SomaticDerivativeDTO> derivatives = _somaticDerivatives.AsNativeArray();
            NativeArray<VrComfortProfileDTO> profiles = _somaticProfiles.AsNativeArray();
            if (samples.Length == 0 || write.Length == 0 || derivatives.Length == 0 || profiles.Length == 0)
                return;

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            float quality = ResolveGlobalQualityWeight01();
            GenerateMockSicknessDataJob job = new GenerateMockSicknessDataJob
            {
                Samples = samples,
                GlobalQualityWeight01 = quality,
                Frame = frame
            };
            JobHandle sampleHandle = job.Schedule(samples.Length, 32);
            JobHandle jitterHandle = ScheduleMockKinematicJitter(frame, quality, sampleHandle);
            InjectMockSicknessDerivativeJob injectJob = new InjectMockSicknessDerivativeJob
            {
                Samples = samples,
                Frame = frame,
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives)
            };
            JobHandle injectHandle = injectJob.Schedule(jitterHandle);

            EvaluateFovTunnelingJob fovJob = new EvaluateFovTunnelingJob
            {
                DeltaTime = HectonXRRuntimeState.FrameIntervalSeconds,
                GlobalQualityWeight01 = quality,
                RuntimeComfortBlend01 = math.max(_somaticComfortPresence01, 1f),
                ImpactShock01 = 1f,
                VramPressure01 = _somaticVramPressure01,
                ThermalPressure01 = _somaticThermalPressure01,
                SystemPressure01 = _somaticSystemPressure01,
                KccAngularVelocityRadS = _kccAngularVelocityRadiansPerSecond,
                KccAngularAccelerationRadS2 = _kccAngularAccelerationRadiansPerSecondSq,
                State = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives),
                Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles)
            };
            JobHandle fovHandle = fovJob.Schedule(injectHandle);

            float mockPhase = (frame & 127u) * math.lerp(0.2f, 0.055f, SmoothJob01(quality));
            CalculateHorizonLockJob horizonJob = new CalculateHorizonLockJob
            {
                CurrentRotation = math.mul(
                    quaternion.RotateY(MathLodApproximation.ApproxSinBhaskara(mockPhase * 1.73f) * 1.35f),
                    quaternion.RotateZ(MathLodApproximation.ApproxSinBhaskara(mockPhase * 4.7f) * 0.72f)),
                DeltaTime = HectonXRRuntimeState.FrameIntervalSeconds,
                GlobalQualityWeight01 = quality,
                State = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives),
                Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles)
            };
            JobHandle legacyHorizonHandle = horizonJob.Schedule(fovHandle);
            _somaticComfortHandle = SchedulePreparedHorizonLockEvaluation(HectonXRRuntimeState.FrameIntervalSeconds, quality, legacyHorizonHandle);
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
            NativeArray<VrComfortProfileLookupSlotDTO> lookup)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return 0;

            if (lookup.IsCreated)
            {
                for (int i = 0; i < lookup.Length; i++)
                    lookup[i] = default;
            }

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
                _somaticCsvScratch = VaultBufferView<byte>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticCsvScratch,
                    SomaticCsvScratchBytes,
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
                !_somaticMockSicknessSamples.IsCreated ||
                !_somaticCsvScratch.IsCreated)
            {
                return;
            }

            NativeArray<SomaticComfortStateDTO> write = _somaticComfortWrite.AsNativeArray();
            NativeArray<SomaticComfortStateDTO> read = _somaticComfortRead.AsNativeArray();
            NativeArray<SomaticDerivativeDTO> derivatives = _somaticDerivatives.AsNativeArray();
            NativeArray<SomaticKinematicHistoryDTO> history = _somaticHistory.AsNativeArray();
            NativeArray<VrComfortProfileDTO> profiles = _somaticProfiles.AsNativeArray();
            NativeArray<VrComfortProfileLookupSlotDTO> lookup = _somaticProfileLookup.AsNativeArray();
            NativeArray<VRSomaticKinematicStateMirrorDTO> kccMirror = _somaticKccStateMirror.AsNativeArray();
            NativeArray<quaternion> rawRotations = _somaticRawRotation.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> horizonWrite = _somaticHorizonWrite.AsNativeArray();
            NativeArray<VRSomaticComfortDTO> horizonRead = _somaticHorizonRead.AsNativeArray();
            NativeArray<SomaticTelemetryEntry> horizonTelemetry = _somaticHorizonTelemetry.AsNativeArray();
            NativeArray<ComfortTelemetryEntry> telemetry = _somaticComfortTelemetry.AsNativeArray();
            NativeArray<SomaticMockSicknessSampleDTO> mock = _somaticMockSicknessSamples.AsNativeArray();

            SeedSomaticComfortBuffersJob seedJob = new SeedSomaticComfortBuffersJob
            {
                WriteState = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                ReadState = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(read),
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives),
                History = (SomaticKinematicHistoryDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(history),
                Profiles = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles),
                ProfileLookup = (VrComfortProfileLookupSlotDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(lookup),
                KinematicStates = (VRSomaticKinematicStateMirrorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(kccMirror),
                RawRotations = (quaternion*)NativeArrayUnsafeUtility.GetUnsafePtr(rawRotations),
                HorizonWrite = (VRSomaticComfortDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(horizonWrite),
                HorizonRead = (VRSomaticComfortDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(horizonRead),
                ProfileCount = profiles.Length,
                LookupCount = lookup.Length
            };
            JobHandle seedHandle = seedJob.Schedule();

            ClearHorizonTelemetryJob clearHorizonTelemetryJob = new ClearHorizonTelemetryJob
            {
                Telemetry = horizonTelemetry
            };
            JobHandle horizonTelemetryHandle = clearHorizonTelemetryJob.Schedule(horizonTelemetry.Length, 32, seedHandle);

            ClearComfortTelemetryJob clearTelemetryJob = new ClearComfortTelemetryJob
            {
                Telemetry = telemetry
            };
            JobHandle telemetryHandle = clearTelemetryJob.Schedule(telemetry.Length, 32, horizonTelemetryHandle);

            GenerateMockSicknessDataJob mockJob = new GenerateMockSicknessDataJob
            {
                Samples = mock,
                GlobalQualityWeight01 = ResolveGlobalQualityWeight01(),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            };
            _somaticComfortHandle = mockJob.Schedule(mock.Length, 32, telemetryHandle);
            _somaticComfortBuffersSeeded = true;
            _somaticComfortJobScheduled = true;
            TryRegisterLateFrame();
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
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.VramPressure01)) != 44 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.ThermalPressure01)) != 48 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.SystemPressure01)) != 52 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.StateHash)) != 56 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Sequence)) != 60 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.AupHash)) != 64 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry._pad0)) != 68 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry._pad1)) != 72)
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
            _somaticCsvScratch.Release();
            ResetHorizonLockBuffers();
            _somaticComfortWrite = default;
            _somaticComfortRead = default;
            _somaticDerivatives = default;
            _somaticHistory = default;
            _somaticProfiles = default;
            _somaticProfileLookup = default;
            _somaticComfortTelemetry = default;
            _somaticMockSicknessSamples = default;
            _somaticCsvScratch = default;
            _somaticComfortBuffersSeeded = false;
            _somaticComfortJobScheduled = false;
            _somaticComfortTelemetryDumped = false;
            _somaticComfortHandle = default;
            _somaticTelemetryCursor = 0;
            _somaticTelemetrySequence = 0u;
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
                return;

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

            NativeArray<SomaticComfortStateDTO> write = _somaticComfortWrite.AsNativeArray();
            NativeArray<SomaticDerivativeDTO> derivatives = _somaticDerivatives.AsNativeArray();
            NativeArray<SomaticKinematicHistoryDTO> history = _somaticHistory.AsNativeArray();
            NativeArray<VrComfortProfileDTO> profiles = _somaticProfiles.AsNativeArray();
            if (write.Length == 0 || derivatives.Length == 0 || history.Length == 0 || profiles.Length == 0)
                return;

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
            uint historyFlags = history[0].Flags;
            bool derivativeSampleDue =
                (historyFlags & SomaticHistoryValidFlag) == 0u ||
                _somaticImpactShock01 > 0.001f ||
                frameIndex % derivativeSampleStride == 0;

            JobHandle derivativeHandle = default;
            if (derivativeSampleDue)
            {
                ComputeSomaticDerivativesJob derivativeJob = new ComputeSomaticDerivativesJob
                {
                    CurrentAup = sourceAup,
                    CurrentRotation = sourceRotation,
                    DeltaTime = safeDeltaTime,
                    HistoryDepth = historyDepth,
                    Frame = unchecked((uint)frameIndex),
                    History = (SomaticKinematicHistoryDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(history),
                    Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives)
                };
                derivativeHandle = derivativeJob.Schedule();
            }

            EvaluateFovTunnelingJob fovJob = new EvaluateFovTunnelingJob
            {
                DeltaTime = safeDeltaTime,
                GlobalQualityWeight01 = quality,
                RuntimeComfortBlend01 = _somaticComfortPresence01,
                ImpactShock01 = _somaticImpactShock01,
                VramPressure01 = _somaticVramPressure01,
                ThermalPressure01 = _somaticThermalPressure01,
                SystemPressure01 = _somaticSystemPressure01,
                KccAngularVelocityRadS = _kccAngularVelocityRadiansPerSecond,
                KccAngularAccelerationRadS2 = _kccAngularAccelerationRadiansPerSecondSq,
                State = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives),
                Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles)
            };
            JobHandle fovHandle = fovJob.Schedule(derivativeHandle);

            CalculateHorizonLockJob horizonJob = new CalculateHorizonLockJob
            {
                CurrentRotation = sourceRotation,
                DeltaTime = safeDeltaTime,
                GlobalQualityWeight01 = quality,
                State = (SomaticComfortStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write),
                Derivatives = (SomaticDerivativeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(derivatives),
                Profile = (VrComfortProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profiles)
            };
            JobHandle legacyHorizonHandle = horizonJob.Schedule(fovHandle);
            _somaticComfortHandle = ScheduleHorizonLockKernel(in sourceAup, sourceRotation, safeDeltaTime, quality, legacyHorizonHandle);
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

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _somaticComfortHandle))
                return;

            PublishSomaticComfortStateFromWrite();
        }

        private void CompleteSomaticComfortIfReady()
        {
            if (!_somaticComfortJobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _somaticComfortHandle, false))
                return;

            PublishSomaticComfortStateFromWrite();
        }

        private void CompleteSomaticComfortForBarrier()
        {
            if (!_somaticComfortJobScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _somaticComfortHandle, true);
            PublishSomaticComfortStateFromWrite();
        }

        private unsafe void PublishSomaticComfortStateFromWrite()
        {
            _somaticComfortJobScheduled = false;
            if (!_somaticComfortWrite.IsCreated || !_somaticComfortRead.IsCreated)
                return;

            NativeArray<SomaticComfortStateDTO> write = _somaticComfortWrite.AsNativeArray();
            NativeArray<SomaticComfortStateDTO> read = _somaticComfortRead.AsNativeArray();
            if (write.Length == 0 || read.Length == 0)
                return;

            void* writePtr = NativeArrayUnsafeUtility.GetUnsafePtr(write);
            void* readPtr = NativeArrayUnsafeUtility.GetUnsafePtr(read);
            UnsafeUtility.MemCpy(readPtr, writePtr, UnsafeUtility.SizeOf<SomaticComfortStateDTO>());

            SomaticComfortStateDTO state = read[0];
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

            NativeArray<ComfortTelemetryEntry> telemetry = _somaticComfortTelemetry.AsNativeArray();
            NativeArray<SomaticDerivativeDTO> derivatives = _somaticDerivatives.AsNativeArray();
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
                NativeArray<SomaticKinematicHistoryDTO> history = _somaticHistory.AsNativeArray();
                if (history.IsCreated && history.Length > 0)
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
                VramPressure01 = Sanitize01(_somaticVramPressure01, 0f),
                ThermalPressure01 = Sanitize01(_somaticThermalPressure01, 0f),
                SystemPressure01 = Sanitize01(_somaticSystemPressure01, 0f),
                StateHash = ResolveSomaticComfortStateHash(in state, in derivative, flags),
                Sequence = _somaticTelemetrySequence++,
                AupHash = aupHash
            };
            _somaticTelemetryCursor = _somaticTelemetryCursor == int.MaxValue
                ? telemetry.Length
                : _somaticTelemetryCursor + 1;

            if (nonFinite)
            {
                DumpComfortTelemetryOnce();
                DumpBlackBoxOnce();
            }
        }

        private unsafe void DumpComfortTelemetryOnce()
        {
            if (_somaticComfortTelemetryDumped)
                return;

            try
            {
                NativeArray<ComfortTelemetryEntry> telemetry = _somaticComfortTelemetry.IsCreated
                    ? _somaticComfortTelemetry.AsNativeArray()
                    : default;
                NativeArray<SomaticTelemetryEntry> horizonTelemetry = _somaticHorizonTelemetry.IsCreated
                    ? _somaticHorizonTelemetry.AsNativeArray()
                    : default;
                bool hasComfort = telemetry.IsCreated && telemetry.Length > 0;
                bool hasHorizon = horizonTelemetry.IsCreated && horizonTelemetry.Length > 0;
                if (!hasComfort && !hasHorizon)
                    return;

                _somaticComfortTelemetryDumped = true;
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    "Dump_SHINOBU_326.bin"));
                using (System.IO.FileStream stream = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[40];
                    WriteUInt32LittleEndian(header, 0, SomaticHorizonTelemetryHash);
                    WriteUInt32LittleEndian(header, 4, 3u);
                    WriteUInt32LittleEndian(header, 8, hasComfort ? (uint)telemetry.Length : 0u);
                    WriteUInt32LittleEndian(header, 12, (uint)UnsafeUtility.SizeOf<ComfortTelemetryEntry>());
                    WriteUInt32LittleEndian(header, 16, hasHorizon ? (uint)horizonTelemetry.Length : 0u);
                    WriteUInt32LittleEndian(header, 20, (uint)UnsafeUtility.SizeOf<SomaticTelemetryEntry>());
                    WriteUInt32LittleEndian(header, 24, unchecked((uint)_somaticTelemetryCursor));
                    WriteUInt32LittleEndian(header, 28, unchecked((uint)_somaticHorizonTelemetryCursor));
                    WriteUInt32LittleEndian(header, 32, SomaticComfortTelemetryHash);
                    WriteUInt32LittleEndian(header, 36, SomaticHorizonTelemetryHash);
                    stream.Write(header);

                    if (hasComfort)
                    {
                        void* comfortPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                        stream.Write(new ReadOnlySpan<byte>(comfortPtr, telemetry.Length * UnsafeUtility.SizeOf<ComfortTelemetryEntry>()));
                    }

                    if (hasHorizon)
                    {
                        void* horizonPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(horizonTelemetry);
                        stream.Write(new ReadOnlySpan<byte>(horizonPtr, horizonTelemetry.Length * UnsafeUtility.SizeOf<SomaticTelemetryEntry>()));
                    }
                }
            }
            catch (Exception exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(ComfortDumpFaultHash, SomaticComfortTelemetryHash, exception.HResult);
            }
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

            NativeArray<ComfortTelemetryEntry> telemetry = _somaticComfortTelemetry.AsNativeArray();
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SeedSomaticComfortBuffersJob : IJob
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticComfortStateDTO* WriteState;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticComfortStateDTO* ReadState;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticDerivativeDTO* Derivatives;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticKinematicHistoryDTO* History;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VrComfortProfileDTO* Profiles;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VrComfortProfileLookupSlotDTO* ProfileLookup;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VRSomaticKinematicStateMirrorDTO* KinematicStates;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe quaternion* RawRotations;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VRSomaticComfortDTO* HorizonWrite;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VRSomaticComfortDTO* HorizonRead;
            public int ProfileCount;
            public int LookupCount;

            public unsafe void Execute()
            {
                SomaticComfortStateDTO clear = new SomaticComfortStateDTO
                {
                    FovTunnelingIntensity = 0f,
                    HorizonLockBlend = 0f,
                    FoveatedScaleMultiplier = 1f,
                    ActiveComfortFlags = 0u,
                    ReservedParameters = float4.zero
                };
                UnsafeUtility.AsRef<SomaticComfortStateDTO>(WriteState) = clear;
                UnsafeUtility.AsRef<SomaticComfortStateDTO>(ReadState) = clear;
                UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives) = default;
                UnsafeUtility.AsRef<SomaticKinematicHistoryDTO>(History) = default;
                UnsafeUtility.AsRef<VRSomaticKinematicStateMirrorDTO>(KinematicStates) = default;
                UnsafeUtility.AsRef<quaternion>(RawRotations) = quaternion.identity;
                VRSomaticComfortDTO horizonClear = new VRSomaticComfortDTO
                {
                    StabilizedRotation = quaternion.identity,
                    FovTunnelScalar = 0f,
                    PitchDampening = 0f,
                    ComfortFlags = SomaticComfortFlagHorizonInitialized
                };
                UnsafeUtility.AsRef<VRSomaticComfortDTO>(HorizonWrite) = horizonClear;
                UnsafeUtility.AsRef<VRSomaticComfortDTO>(HorizonRead) = horizonClear;
                for (int i = 0; i < LookupCount; i++)
                    ProfileLookup[i] = default;

                if (ProfileCount <= 0)
                    return;

                Profiles[0] = DefaultJobNoviceProfile();
                InsertProfileLookup(ProfileLookup, LookupCount, Profiles[0].ProfileHash, 0);
                if (ProfileCount > 1)
                {
                    Profiles[1] = DefaultJobVeteranProfile();
                    InsertProfileLookup(ProfileLookup, LookupCount, Profiles[1].ProfileHash, 1);
                }
                if (ProfileCount > 2)
                {
                    Profiles[2] = DefaultJobDisabledProfile();
                    InsertProfileLookup(ProfileLookup, LookupCount, Profiles[2].ProfileHash, 2);
                }
                if (ProfileCount > 3)
                {
                    Profiles[3] = DefaultJobQuest3Profile();
                    InsertProfileLookup(ProfileLookup, LookupCount, Profiles[3].ProfileHash, 3);
                }
            }

            private static unsafe void InsertProfileLookup(
                VrComfortProfileLookupSlotDTO* lookup,
                int lookupCount,
                uint profileHash,
                int profileIndex)
            {
                if (lookupCount <= 0 || profileHash == 0u)
                    return;

                int start = (int)(profileHash % (uint)lookupCount);
                for (int i = 0; i < lookupCount; i++)
                {
                    int slotIndex = (start + i) % lookupCount;
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

            private static VrComfortProfileDTO DefaultJobNoviceProfile()
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
                    LinearAccelerationSoftMps2 = 6f,
                    EwmaSharpness = 22f,
                    ImpactShockWeight = 1f,
                    FlatScreenBaselineFovTunnel = 0.05f,
                    VrBaselineFovTunnel = 0.82f,
                    ReleaseSharpness = 9f,
                    MockAmplitude = 1f
                };
            }

            private static VrComfortProfileDTO DefaultJobVeteranProfile()
            {
                VrComfortProfileDTO profile = DefaultJobNoviceProfile();
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

            private static VrComfortProfileDTO DefaultJobDisabledProfile()
            {
                VrComfortProfileDTO profile = DefaultJobNoviceProfile();
                profile.ProfileHash = SomaticProfileDisabledHash;
                profile.UserComfortWeight01 = 0f;
                profile.FovAggressiveness = 0f;
                profile.HorizonLockSpeed = 0f;
                profile.FoveatedBaseline = 0f;
                profile.FlatScreenBaselineFovTunnel = 0f;
                profile.VrBaselineFovTunnel = 0f;
                return profile;
            }

            private static VrComfortProfileDTO DefaultJobQuest3Profile()
            {
                VrComfortProfileDTO profile = DefaultJobVeteranProfile();
                profile.ProfileHash = SomaticProfileQuest3Hash;
                profile.FoveatedBaseline = 0.1f;
                profile.AngularAccelerationSoftRadS2 = 36f;
                profile.VrBaselineFovTunnel = 0.7f;
                return profile;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearComfortTelemetryJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<ComfortTelemetryEntry> Telemetry;

            public void Execute(int index)
            {
                Telemetry[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct InjectMockSicknessDerivativeJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<SomaticMockSicknessSampleDTO> Samples;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticDerivativeDTO* Derivatives;
            public uint Frame;

            public unsafe void Execute()
            {
                if (Samples.Length == 0)
                    return;

                int sampleIndex = (int)(Frame % (uint)Samples.Length);
                SomaticMockSicknessSampleDTO sample = Samples[sampleIndex];
                float3 angularVelocity = ClampLength(SanitizeJobFloat3(sample.AngularVelocity), 48f);
                float3 angularAcceleration = ClampLength(SanitizeJobFloat3(sample.AngularAcceleration), 480f);
                float3 linearAcceleration = ClampLength(SanitizeJobFloat3(sample.LinearAcceleration), 240f);
                UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives) = new SomaticDerivativeDTO
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
        private struct CalculateHorizonLockJob : IJob
        {
            public quaternion CurrentRotation;
            public float DeltaTime;
            public float GlobalQualityWeight01;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticComfortStateDTO* State;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe SomaticDerivativeDTO* Derivatives;
            [NativeDisableUnsafePtrRestriction, NoAlias] public unsafe VrComfortProfileDTO* Profile;

            public unsafe void Execute()
            {
                ref SomaticComfortStateDTO state = ref UnsafeUtility.AsRef<SomaticComfortStateDTO>(State);
                ref SomaticDerivativeDTO derivatives = ref UnsafeUtility.AsRef<SomaticDerivativeDTO>(Derivatives);
                VrComfortProfileDTO profile = SanitizeJobProfile(UnsafeUtility.AsRef<VrComfortProfileDTO>(Profile));
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
                float derivativeAngularAcceleration = SanitizeJobNonNegative(derivatives.PeakAngularAccelerationRadS2);
                float accelerationAssist = SmoothJob01(derivativeAngularAcceleration * math.rcp(math.max(profile.AngularAccelerationSoftRadS2 * 2f, 0.01f)));
                float quality = SanitizeJob01(GlobalQualityWeight01, 1f);
                float target = math.saturate(math.max(SmoothJob01(upError), accelerationAssist) * SanitizeJob01(profile.UserComfortWeight01, 1f) * math.lerp(1.12f, 0.88f, quality));
                float currentBlend = SanitizeJob01(state.HorizonLockBlend, 0f);
                float springOmega = math.max(0.01f, profile.HorizonLockSpeed) * math.lerp(4.75f, 2.35f, quality);
                float blend = ResolveCriticalDampedSpringBlend(springOmega, DeltaTime, quality);
                state.HorizonLockBlend = SanitizeJob01(math.lerp(currentBlend, target, SanitizeJob01(blend, 0f)), currentBlend);
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
            double3 delta = new double3(
                (((double)current.GridX - previous.GridX) * CellSize) + ((double)current.LocalX - previous.LocalX),
                (((double)current.GridY - previous.GridY) * CellSize) + ((double)current.LocalY - previous.LocalY),
                (((double)current.GridZ - previous.GridZ) * CellSize) + ((double)current.LocalZ - previous.LocalZ));
            return math.all(math.isfinite(delta)) ? SanitizeJobFloat3((float3)delta) : float3.zero;
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
