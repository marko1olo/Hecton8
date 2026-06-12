using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Lighting
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EnvironmentLightingDTO
    {
        [FieldOffset(0)] public float4 AmbientColor;
        [FieldOffset(16)] public float4 FogColor;
        [FieldOffset(32)] public float4 DirectionalLightColor;
        [FieldOffset(48)] public float SunIntensity;
        [FieldOffset(52)] public float MoonIntensity;
        [FieldOffset(56)] public float SHCoefficientCount;
        [FieldOffset(60)] public float SHQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LightingRelayTuningDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public float WaterExtinctionConstant;
        [FieldOffset(8)] public float EclipseDarkeningMultiplier;
        [FieldOffset(12)] public float GlobalQualityOverride;
        [FieldOffset(16)] public float DeepGloomStartMeters;
        [FieldOffset(20)] public float DeepGloomFullMeters;
        [FieldOffset(24)] public float LowTierSHOrder;
        [FieldOffset(28)] public float UltraTierSHOrder;
        [FieldOffset(32)] public float DebugColorBlocks;
        [FieldOffset(36)] public float CsvProfilesLoaded;
        [FieldOffset(40)] public float Reserved0;
        [FieldOffset(44)] public float Reserved1;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LightingGradientProfileDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint BiomeIndex;
        [FieldOffset(8)] public float4 AmbientColor;
        [FieldOffset(24)] public float4 FogColor;
        [FieldOffset(40)] public float4 DirectionalTint;
        [FieldOffset(56)] public float Weight;
        [FieldOffset(60)] public float Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LightingRelayMockSampleDTO
    {
        [FieldOffset(0)] public float TimeOfDay01;
        [FieldOffset(4)] public float DepthMeters;
        [FieldOffset(8)] public float Eclipse01;
        [FieldOffset(12)] public float MoonPhase01;
        [FieldOffset(16)] public float4 BiomeBlend;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LightingRelayTelemetryEntry
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public uint Sequence;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint StateHash;
        [FieldOffset(16)] public float BurstCpuMicroseconds;
        [FieldOffset(20)] public float TimeOfDay01;
        [FieldOffset(24)] public float DepthMeters;
        [FieldOffset(28)] public float GloomScalar;
        [FieldOffset(32)] public float QualityWeight;
        [FieldOffset(36)] public float BiomeWeight01;
        [FieldOffset(40)] public float AmbientLuma;
        [FieldOffset(44)] public float SunIntensity;
        [FieldOffset(48)] public float MoonIntensity;
        [FieldOffset(52)] public uint EnvironmentHash;
        [FieldOffset(56)] private ulong _pad0;
    }

    public sealed partial class HectonGIRelaySystem
    {
        private const int DayNightGradientProfileCapacity = 32;
        private const int DayNightMockSampleCapacity = 128;
        private const int EnvironmentLightingStrideBytes = 64;
        private const float DayNightTelemetryWarnMicroseconds = 200f;
        private const uint DayNightTuningMagic = 0x53484749u;
        private const uint DayNightRelayHash = 0x53483334u;
        private const BufferID DayNightEnvironmentLightingBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightEnvironmentLightingBuffer;
        private const BufferID DayNightTelemetryRingBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightTelemetryRingBuffer;
        private const BufferID DayNightTelemetryCursorBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightTelemetryCursorBuffer;
        private const BufferID DayNightTuningBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightTuningBuffer;
        private const BufferID DayNightGradientProfilesBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightGradientProfilesBuffer;
        private const BufferID DayNightGradientProfileCountBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightGradientProfileCountBuffer;
        private const BufferID DayNightMockSamplesBuffer = BufferID.HectonLightingRuntime_DayNightRelay_DayNightMockSamplesBuffer;
        private const string DayNightBlackBoxDumpPath = "Docs/AgentLogs/Dump_13KRA.bin";
        private const string LightingGradientProfilesRelativePath = "Docs/Data/lighting_gradient_profiles.csv";

        private static readonly int _HectonEnvironmentLightingCBufferId = Shader.PropertyToID("HectonEnvironmentLighting");
        private static readonly int _H8EnvironmentDebugBlocksId = Shader.PropertyToID("_H8EnvironmentDebugBlocks");

        private VaultGenerationHandle<EnvironmentLightingDTO> _environmentLighting;
        private VaultGenerationHandle<LightingRelayTelemetryEntry> _dayNightTelemetryRing;
        private VaultGenerationHandle<int> _dayNightTelemetryCursor;
        private VaultGenerationHandle<LightingRelayTuningDTO> _dayNightTuning;
        private VaultGenerationHandle<LightingGradientProfileDTO> _lightingGradientProfiles;
        private VaultGenerationHandle<int> _lightingGradientProfileCount;
        private VaultGenerationHandle<LightingRelayMockSampleDTO> _lightingMockSamples;
        private GraphicsBuffer _environmentLightingCBufferA;
        private GraphicsBuffer _environmentLightingCBufferB;
        private GraphicsBuffer _activeEnvironmentLightingCBuffer;
        private int _environmentLightingCBufferWriteIndex;
        private int _dayNightTelemetryCursorCached;
        private int _dayNightTelemetryCount;
        private int _lightingGradientProfileCountCached;
        private long _pendingDayNightScheduleTicks;
        private EnvironmentLightingDTO _lastEnvironmentLighting;
        private bool _lastEnvironmentLightingValid;
        private float _editorWaterExtinctionConstant = -1f;
        private float _editorEclipseDarkeningMultiplier = -1f;
        private float _editorQualityOverride = -1f;
        private float _debugColorBlocksEnabled;

        private bool EnsureDayNightRelayNativeStorage()
        {
            _environmentLighting = AcquireBuffer<EnvironmentLightingDTO>(
                DayNightEnvironmentLightingBuffer,
                1,
                NativeArrayOptions.UninitializedMemory);
            _dayNightTelemetryRing = AcquireBuffer<LightingRelayTelemetryEntry>(
                DayNightTelemetryRingBuffer,
                TelemetryCapacity);
            _dayNightTelemetryCursor = AcquireBuffer<int>(
                DayNightTelemetryCursorBuffer,
                1);
            _dayNightTuning = AcquireBuffer<LightingRelayTuningDTO>(
                DayNightTuningBuffer,
                1,
                NativeArrayOptions.UninitializedMemory);
            _lightingGradientProfiles = AcquireBuffer<LightingGradientProfileDTO>(
                DayNightGradientProfilesBuffer,
                DayNightGradientProfileCapacity,
                NativeArrayOptions.UninitializedMemory);
            _lightingGradientProfileCount = AcquireBuffer<int>(
                DayNightGradientProfileCountBuffer,
                1);
            _lightingMockSamples = AcquireBuffer<LightingRelayMockSampleDTO>(
                DayNightMockSamplesBuffer,
                DayNightMockSampleCapacity,
                NativeArrayOptions.UninitializedMemory);

            if (!HasRequiredDayNightRelayStorage())
                return false;

            InitializeDayNightTuning();
            BuildDefaultLightingGradientProfiles();
            EnsureEnvironmentLightingCBuffer();
            return true;
        }

        private bool HasRequiredDayNightRelayStorage()
        {
            return TryOpenGIRelayBuffer(in _environmentLighting, DayNightEnvironmentLightingBuffer, 1, out NativeArray<EnvironmentLightingDTO> environment) &&
                   environment.IsCreated &&
                   TryOpenGIRelayBuffer(in _dayNightTelemetryRing, DayNightTelemetryRingBuffer, TelemetryCapacity, out NativeArray<LightingRelayTelemetryEntry> telemetry) &&
                   telemetry.IsCreated &&
                   TryOpenGIRelayBuffer(in _dayNightTelemetryCursor, DayNightTelemetryCursorBuffer, 1, out NativeArray<int> cursor) &&
                   cursor.IsCreated &&
                   TryOpenGIRelayBuffer(in _dayNightTuning, DayNightTuningBuffer, 1, out NativeArray<LightingRelayTuningDTO> tuning) &&
                   tuning.IsCreated &&
                   TryOpenGIRelayBuffer(in _lightingGradientProfiles, DayNightGradientProfilesBuffer, DayNightGradientProfileCapacity, out NativeArray<LightingGradientProfileDTO> profiles) &&
                   profiles.IsCreated &&
                   TryOpenGIRelayBuffer(in _lightingGradientProfileCount, DayNightGradientProfileCountBuffer, 1, out NativeArray<int> profileCount) &&
                   profileCount.IsCreated &&
                   TryOpenGIRelayBuffer(in _lightingMockSamples, DayNightMockSamplesBuffer, DayNightMockSampleCapacity, out NativeArray<LightingRelayMockSampleDTO> samples) &&
                   samples.IsCreated;
        }

        private void ReleaseDayNightRelayNativeStorage()
        {
            ReleaseGIRelayDescriptor(in _environmentLighting, DayNightEnvironmentLightingBuffer);
            ReleaseGIRelayDescriptor(in _dayNightTelemetryRing, DayNightTelemetryRingBuffer);
            ReleaseGIRelayDescriptor(in _dayNightTelemetryCursor, DayNightTelemetryCursorBuffer);
            ReleaseGIRelayDescriptor(in _dayNightTuning, DayNightTuningBuffer);
            ReleaseGIRelayDescriptor(in _lightingGradientProfiles, DayNightGradientProfilesBuffer);
            ReleaseGIRelayDescriptor(in _lightingGradientProfileCount, DayNightGradientProfileCountBuffer);
            ReleaseGIRelayDescriptor(in _lightingMockSamples, DayNightMockSamplesBuffer);
            _environmentLighting = default;
            _dayNightTelemetryRing = default;
            _dayNightTelemetryCursor = default;
            _dayNightTuning = default;
            _lightingGradientProfiles = default;
            _lightingGradientProfileCount = default;
            _lightingMockSamples = default;
            _dayNightTelemetryCursorCached = 0;
            _dayNightTelemetryCount = 0;
            _lightingGradientProfileCountCached = 0;
            _pendingDayNightScheduleTicks = 0;
            _lastEnvironmentLightingValid = false;
            ReleaseEnvironmentLightingCBuffer();
        }

        private NativeArray<T> OpenDayNightRelayArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return TryOpenGIRelayBuffer(in handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private bool TryReadDayNightRelayArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (_vault == null ||
                requiredLength <= 0 ||
                !IsGIRelayVaultHandle(in handle, bufferId) ||
                !_vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void InitializeDayNightTuning()
        {
            NativeArray<LightingRelayTuningDTO> tuningArray = OpenDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1);
            if (!tuningArray.IsCreated)
                return;

            LightingRelayTuningDTO tuning = tuningArray[0];
            if (tuning.Magic != DayNightTuningMagic ||
                !math.isfinite(tuning.WaterExtinctionConstant) ||
                !math.isfinite(tuning.EclipseDarkeningMultiplier))
            {
                tuning = CreateDefaultLightingTuning();
            }

            tuning.GlobalQualityOverride = math.select(-1f, math.saturate(_editorQualityOverride), _editorQualityOverride >= 0f);
            tuning.WaterExtinctionConstant = math.select(tuning.WaterExtinctionConstant, _editorWaterExtinctionConstant, _editorWaterExtinctionConstant >= 0f);
            tuning.EclipseDarkeningMultiplier = math.select(tuning.EclipseDarkeningMultiplier, _editorEclipseDarkeningMultiplier, _editorEclipseDarkeningMultiplier >= 0f);
            tuning.DebugColorBlocks = _debugColorBlocksEnabled;
            tuningArray[0] = tuning;
        }

        private static LightingRelayTuningDTO CreateDefaultLightingTuning()
        {
            return new LightingRelayTuningDTO
            {
                Magic = DayNightTuningMagic,
                WaterExtinctionConstant = 0.0017f,
                EclipseDarkeningMultiplier = 0.72f,
                GlobalQualityOverride = -1f,
                DeepGloomStartMeters = 180f,
                DeepGloomFullMeters = 2200f,
                LowTierSHOrder = 0.18f,
                UltraTierSHOrder = 1f,
                DebugColorBlocks = 0f,
                CsvProfilesLoaded = 0f
            };
        }

        private void BuildDefaultLightingGradientProfiles()
        {
            NativeArray<LightingGradientProfileDTO> profiles =
                OpenDayNightRelayArray(in _lightingGradientProfiles, DayNightGradientProfilesBuffer, DayNightGradientProfileCapacity);
            NativeArray<int> countArray =
                OpenDayNightRelayArray(in _lightingGradientProfileCount, DayNightGradientProfileCountBuffer, 1);
            if (!profiles.IsCreated || !countArray.IsCreated)
                return;

            profiles[0] = CreateProfile(0x00000000u, 0u, new float3(0.028f, 0.075f, 0.090f), new float3(0.006f, 0.020f, 0.032f), new float3(0.42f, 0.68f, 0.72f), 1f);
            profiles[1] = CreateProfile(0x2AF0B711u, 1u, new float3(0.018f, 0.090f, 0.060f), new float3(0.004f, 0.028f, 0.020f), new float3(0.34f, 0.74f, 0.48f), 1f);
            profiles[2] = CreateProfile(0x7C92D531u, 2u, new float3(0.060f, 0.034f, 0.022f), new float3(0.026f, 0.014f, 0.010f), new float3(0.70f, 0.42f, 0.26f), 1f);
            profiles[3] = CreateProfile(0xB14A0D61u, 3u, new float3(0.012f, 0.035f, 0.082f), new float3(0.002f, 0.012f, 0.038f), new float3(0.25f, 0.40f, 0.88f), 1f);
            countArray[0] = 4;
            _lightingGradientProfileCountCached = 4;
        }

        private static LightingGradientProfileDTO CreateProfile(
            uint hash,
            uint index,
            float3 ambient,
            float3 fog,
            float3 directional,
            float weight)
        {
            return new LightingGradientProfileDTO
            {
                BiomeHash = hash,
                BiomeIndex = index,
                AmbientColor = new float4(ambient, 1f),
                FogColor = new float4(fog, 1f),
                DirectionalTint = new float4(directional, 1f),
                Weight = math.saturate(weight)
            };
        }

        private double3 ResolvePlayerAupDouble()
        {
            IPlayerRuntimeContext player = _cachedPlayerContext;
            if (player != null)
            {
                if (player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    snapshot.Aup.IsFinite())
                {
                    return snapshot.Aup.ToAbsoluteDouble3();
                }

                if (player.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    movementState.PredictedAup.IsFinite())
                {
                    return movementState.PredictedAup.ToAbsoluteDouble3();
                }
            }

            return double3.zero;
        }

        private static double3 ResolveBiomeCenterAup(in BiomeGradientSignal biomeGradient)
        {
            AbsoluteUniversePosition center = biomeGradient.PositionAup;
            return center.IsFinite() ? center.ToAbsoluteDouble3() : double3.zero;
        }

        private float ResolveDayNightQualityWeight()
        {
            if (TryReadDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1, out NativeArray<LightingRelayTuningDTO>.ReadOnly tuningArray))
            {
                LightingRelayTuningDTO tuning = tuningArray[0];
                if (tuning.GlobalQualityOverride >= 0f && math.isfinite(tuning.GlobalQualityOverride))
                    return math.saturate(tuning.GlobalQualityOverride);
            }

            if (_editorQualityOverride >= 0f && math.isfinite(_editorQualityOverride))
                return math.saturate(_editorQualityOverride);

            return ResolveGlobalQualityWeight();
        }

        private float ResolveWaterExtinctionConstant()
        {
            if (_editorWaterExtinctionConstant >= 0f && math.isfinite(_editorWaterExtinctionConstant))
                return math.max(0f, _editorWaterExtinctionConstant);

            if (!TryReadDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1, out NativeArray<LightingRelayTuningDTO>.ReadOnly tuningArray))
                return 0.0017f;

            float value = tuningArray[0].WaterExtinctionConstant;
            return math.isfinite(value) ? math.max(0f, value) : 0.0017f;
        }

        private float ResolveEclipseDarkeningMultiplier()
        {
            if (_editorEclipseDarkeningMultiplier >= 0f && math.isfinite(_editorEclipseDarkeningMultiplier))
                return math.saturate(_editorEclipseDarkeningMultiplier);

            if (!TryReadDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1, out NativeArray<LightingRelayTuningDTO>.ReadOnly tuningArray))
                return 0.72f;

            float value = tuningArray[0].EclipseDarkeningMultiplier;
            return math.isfinite(value) ? math.saturate(value) : 0.72f;
        }

        private unsafe bool TryUploadDayNightLightingCBuffer()
        {
            NativeArray<EnvironmentLightingDTO> environment =
                OpenDayNightRelayArray(in _environmentLighting, DayNightEnvironmentLightingBuffer, 1);
            if (!environment.IsCreated)
                return false;

            EnvironmentLightingDTO dto = environment[0];
            bool finite = IsEnvironmentLightingFinite(in dto);
            if (!finite)
            {
                RecordDayNightLightingTelemetry(in dto, DayNightTelemetryFlags.NonFinite);
                DumpDayNightBlackBox();
                return false;
            }

            if (!IsEnvironmentLightingCBufferReady())
            {
                RecordDayNightLightingTelemetry(in dto, DayNightTelemetryFlags.CBufferUnavailable);
                return false;
            }

            GraphicsBuffer writeBuffer = AcquireNextEnvironmentLightingCBufferForWrite();
            NativeArray<EnvironmentLightingDTO> mapped = writeBuffer.LockBufferForWrite<EnvironmentLightingDTO>(0, 1);
            try
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                void* src = UnsafeUtility.AddressOf(ref dto);
                UnsafeUtility.MemCpy(dst, src, EnvironmentLightingStrideBytes);
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<EnvironmentLightingDTO>(1);
            }

            _activeEnvironmentLightingCBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(
                _HectonEnvironmentLightingCBufferId,
                _activeEnvironmentLightingCBuffer,
                0,
                EnvironmentLightingStrideBytes);

            _lastEnvironmentLighting = dto;
            _lastEnvironmentLightingValid = true;
            RecordDayNightLightingTelemetry(in dto, DayNightTelemetryFlags.PushedCBuffer);
            return true;
        }

        private bool EnsureEnvironmentLightingCBuffer()
        {
            if (!SystemInfo.supportsSetConstantBuffer)
                return false;

            if (IsEnvironmentLightingCBufferReady())
                return true;

            ReleaseEnvironmentLightingCBuffer();
            _environmentLightingCBufferWriteIndex = 0;
            _environmentLightingCBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Constant,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                EnvironmentLightingStrideBytes);
            _environmentLightingCBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Constant,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                EnvironmentLightingStrideBytes);
            bool valid = _environmentLightingCBufferA.IsValid() && _environmentLightingCBufferB.IsValid();
            if (!valid)
                ReleaseEnvironmentLightingCBuffer();
            return valid;
        }

        private bool IsEnvironmentLightingCBufferReady()
        {
            return SystemInfo.supportsSetConstantBuffer &&
                   _environmentLightingCBufferA != null &&
                   _environmentLightingCBufferB != null &&
                   _environmentLightingCBufferA.IsValid() &&
                   _environmentLightingCBufferB.IsValid() &&
                   _environmentLightingCBufferA.count >= 1 &&
                   _environmentLightingCBufferB.count >= 1 &&
                   _environmentLightingCBufferA.stride == EnvironmentLightingStrideBytes &&
                   _environmentLightingCBufferB.stride == EnvironmentLightingStrideBytes;
        }

        private GraphicsBuffer AcquireNextEnvironmentLightingCBufferForWrite()
        {
            _environmentLightingCBufferWriteIndex ^= 1;
            return _environmentLightingCBufferWriteIndex == 0
                ? _environmentLightingCBufferA
                : _environmentLightingCBufferB;
        }

        private void ReleaseEnvironmentLightingCBuffer()
        {
            _environmentLightingCBufferA?.Release();
            _environmentLightingCBufferB?.Release();
            _environmentLightingCBufferA = null;
            _environmentLightingCBufferB = null;
            _activeEnvironmentLightingCBuffer = null;
            _environmentLightingCBufferWriteIndex = 0;
        }

        private static bool IsEnvironmentLightingFinite(in EnvironmentLightingDTO dto)
        {
            return math.all(math.isfinite(dto.AmbientColor)) &&
                   math.all(math.isfinite(dto.FogColor)) &&
                   math.all(math.isfinite(dto.DirectionalLightColor)) &&
                   math.isfinite(dto.SunIntensity) &&
                   math.isfinite(dto.MoonIntensity) &&
                   math.isfinite(dto.SHCoefficientCount) &&
                   math.isfinite(dto.SHQualityWeight);
        }

        private void RecordDayNightLightingTelemetry(in EnvironmentLightingDTO dto, DayNightTelemetryFlags flags)
        {
            NativeArray<LightingRelayTelemetryEntry> telemetryRing =
                OpenDayNightRelayArray(in _dayNightTelemetryRing, DayNightTelemetryRingBuffer, TelemetryCapacity);
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            float elapsedUs = ConsumePendingDayNightJobMicroseconds();
            float ambientLuma = math.dot(dto.AmbientColor.xyz, new float3(0.2126f, 0.7152f, 0.0722f));
            uint hash = HashEnvironmentLighting(in dto);
            if (elapsedUs > DayNightTelemetryWarnMicroseconds)
                flags |= DayNightTelemetryFlags.OverBudget;

            int index = _dayNightTelemetryCursorCached;
            telemetryRing[index] = new LightingRelayTelemetryEntry
            {
                FrameIndex = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                Sequence = _snapshot.Sequence,
                Flags = (uint)flags,
                StateHash = hash,
                BurstCpuMicroseconds = elapsedUs,
                TimeOfDay01 = _snapshot.TimeOfDay01,
                DepthMeters = _snapshot.DepthMeters,
                GloomScalar = dto.FogColor.w,
                QualityWeight = ResolveDayNightQualityWeight(),
                BiomeWeight01 = dto.DirectionalLightColor.w,
                AmbientLuma = ambientLuma,
                SunIntensity = dto.SunIntensity,
                MoonIntensity = dto.MoonIntensity,
                EnvironmentHash = hash
            };

            index++;
            _dayNightTelemetryCursorCached = index >= telemetryRing.Length ? 0 : index;
            if (_dayNightTelemetryCount < telemetryRing.Length)
                _dayNightTelemetryCount++;

            NativeArray<int> cursorArray = OpenDayNightRelayArray(in _dayNightTelemetryCursor, DayNightTelemetryCursorBuffer, 1);
            if (cursorArray.IsCreated)
                cursorArray[0] = _dayNightTelemetryCursorCached;

            if ((flags & (DayNightTelemetryFlags.NonFinite | DayNightTelemetryFlags.OverBudget)) != 0)
                DumpDayNightBlackBox();
        }

        private float ConsumePendingDayNightJobMicroseconds()
        {
            long start = _pendingDayNightScheduleTicks;
            _pendingDayNightScheduleTicks = 0;
            if (start <= 0)
                return 0f;

            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            double seconds = (double)delta / System.Diagnostics.Stopwatch.Frequency;
            return (float)(seconds * 1000000.0);
        }

        private unsafe void DumpDayNightBlackBox()
        {
            NativeArray<LightingRelayTelemetryEntry> telemetryRing =
                OpenDayNightRelayArray(in _dayNightTelemetryRing, DayNightTelemetryRingBuffer, TelemetryCapacity);
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            try
            {
                DayNightLightingDumpHeader header = new DayNightLightingDumpHeader
                {
                    Magic = 0x53483334u,
                    EntryStrideBytes = UnsafeUtility.SizeOf<LightingRelayTelemetryEntry>(),
                    EntryCount = telemetryRing.Length,
                    Cursor = _dayNightTelemetryCursorCached,
                    RecordedCount = _dayNightTelemetryCount,
                    Sequence = _snapshot.Sequence,
                    LastDepthMeters = _snapshot.DepthMeters,
                    Reserved0 = 0f
                };

                int count = telemetryRing.Length;
                int headerBytes = UnsafeUtility.SizeOf<DayNightLightingDumpHeader>();
                int entryBytes = UnsafeUtility.SizeOf<LightingRelayTelemetryEntry>();
                int byteCount = headerBytes + count * entryBytes;
                int startIndex = _dayNightTelemetryCount >= count ? _dayNightTelemetryCursorCached : 0;
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(HectonGIRelaySystem),
                        "DayNightLightingDumpPayload");
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    UnsafeUtility.MemCpy(target, UnsafeUtility.AddressOf(ref header), headerBytes);

                    byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                    int firstCount = math.min(count, count - startIndex);
                    UnsafeUtility.MemCpy(target + headerBytes, source + startIndex * entryBytes, firstCount * entryBytes);
                    int secondCount = count - firstCount;
                    if (secondCount > 0)
                        UnsafeUtility.MemCpy(target + headerBytes + firstCount * entryBytes, source, secondCount * entryBytes);

                    NativeFaultDumpWriter.TryWriteAll(DayNightBlackBoxDumpPath, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(HectonGIRelaySystem),
                        "DayNightLightingDumpPayload");
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogException(exception, this);
#endif
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct DayNightLightingDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public int EntryStrideBytes;
            [FieldOffset(8)] public int EntryCount;
            [FieldOffset(12)] public int Cursor;
            [FieldOffset(16)] public int RecordedCount;
            [FieldOffset(20)] public uint Sequence;
            [FieldOffset(24)] public float LastDepthMeters;
            [FieldOffset(28)] public float Reserved0;
        }

        private static uint HashEnvironmentLighting(in EnvironmentLightingDTO dto)
        {
            uint hash = 2166136261u;
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.AmbientColor.x, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.AmbientColor.y, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.AmbientColor.z, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.FogColor.x, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.FogColor.y, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.FogColor.z, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.SunIntensity, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.MoonIntensity, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.FogColor.w, 10000f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.SHCoefficientCount, 100f));
            hash = HashInt(hash, QuantizeTelemetryFloat(dto.SHQualityWeight, 10000f));
            return HashUInt(hash, DayNightRelayHash);
        }

        public bool TryGetEnvironmentLightingCopy(out EnvironmentLightingDTO lighting)
        {
            lighting = default;
            if (!TryReadDayNightRelayArray(in _environmentLighting, DayNightEnvironmentLightingBuffer, 1, out NativeArray<EnvironmentLightingDTO>.ReadOnly environment))
                return false;

            lighting = environment[0];
            return IsEnvironmentLightingFinite(in lighting);
        }

        public bool TryGetDayNightTelemetryReadback(
            out NativeArray<LightingRelayTelemetryEntry>.ReadOnly telemetry,
            out int cursor)
        {
            telemetry = default;
            cursor = _dayNightTelemetryCursorCached;
            if (!TryReadDayNightRelayArray(in _dayNightTelemetryRing, DayNightTelemetryRingBuffer, TelemetryCapacity, out NativeArray<LightingRelayTelemetryEntry>.ReadOnly telemetryRing))
                return false;

            telemetry = telemetryRing;
            return true;
        }

        public bool TryGetLightingRelayTuningCopy(out LightingRelayTuningDTO tuning)
        {
            tuning = default;
            if (!TryReadDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1, out NativeArray<LightingRelayTuningDTO>.ReadOnly tuningArray))
                return false;

            tuning = tuningArray[0];
            return tuning.Magic == DayNightTuningMagic;
        }

        public int LightingGradientProfileCount => _lightingGradientProfileCountCached;

        public bool LastEnvironmentLightingValid => _lastEnvironmentLightingValid;

        public void SetEditorWaterExtinctionConstant(float value)
        {
#if UNITY_EDITOR
            _editorWaterExtinctionConstant = value < 0f ? -1f : math.max(0f, value);
            WriteTuningOverride();
#endif
        }

        public void SetEditorEclipseDarkeningMultiplier(float value)
        {
#if UNITY_EDITOR
            _editorEclipseDarkeningMultiplier = value < 0f ? -1f : math.saturate(value);
            WriteTuningOverride();
#endif
        }

        public void SetEditorQualityOverride(float value)
        {
#if UNITY_EDITOR
            _editorQualityOverride = value < 0f ? -1f : math.saturate(value);
            WriteTuningOverride();
#endif
        }

        public void SetEditorDebugColorBlocks(float value)
        {
#if UNITY_EDITOR
            _debugColorBlocksEnabled = value > 0.5f ? 1f : 0f;
            Shader.SetGlobalFloat(_H8EnvironmentDebugBlocksId, _debugColorBlocksEnabled);
            WriteTuningOverride();
#endif
        }

        private void WriteTuningOverride()
        {
            NativeArray<LightingRelayTuningDTO> tuningArray = OpenDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1);
            if (!tuningArray.IsCreated)
                return;

            LightingRelayTuningDTO tuning = tuningArray[0];
            if (tuning.Magic != DayNightTuningMagic)
                tuning = CreateDefaultLightingTuning();

            tuning.WaterExtinctionConstant = _editorWaterExtinctionConstant >= 0f
                ? _editorWaterExtinctionConstant
                : tuning.WaterExtinctionConstant;
            tuning.EclipseDarkeningMultiplier = _editorEclipseDarkeningMultiplier >= 0f
                ? _editorEclipseDarkeningMultiplier
                : tuning.EclipseDarkeningMultiplier;
            tuning.GlobalQualityOverride = _editorQualityOverride >= 0f ? _editorQualityOverride : -1f;
            tuning.DebugColorBlocks = _debugColorBlocksEnabled;
            tuningArray[0] = tuning;
        }

        public void GenerateMockLightingEnvironment()
        {
#if !UNITY_EDITOR
            return;
#else
            NativeArray<LightingRelayMockSampleDTO> samples =
                OpenDayNightRelayArray(in _lightingMockSamples, DayNightMockSamplesBuffer, DayNightMockSampleCapacity);
            if (!samples.IsCreated)
                return;

            GenerateMockLightingRelayJob job = new GenerateMockLightingRelayJob
            {
                Samples = samples,
                PhaseSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                QualityWeight = ResolveDayNightQualityWeight()
            };
            for (int index = 0; index < samples.Length; index++)
                job.Execute(index);
#endif
        }

        public void RequestLightingGradientProfilesReload()
        {
#if !UNITY_EDITOR
            return;
#else
            NativeArray<LightingGradientProfileDTO> profiles =
                OpenDayNightRelayArray(in _lightingGradientProfiles, DayNightGradientProfilesBuffer, DayNightGradientProfileCapacity);
            NativeArray<int> countArray =
                OpenDayNightRelayArray(in _lightingGradientProfileCount, DayNightGradientProfileCountBuffer, 1);
            if (!profiles.IsCreated || !countArray.IsCreated)
                return;

            string fullPath = Path.Combine(Application.dataPath, "..", LightingGradientProfilesRelativePath);
            if (!File.Exists(fullPath))
            {
                BuildDefaultLightingGradientProfiles();
                return;
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            int count = ParseLightingGradientProfiles(bytes, profiles);
            if (count <= 0)
            {
                BuildDefaultLightingGradientProfiles();
                return;
            }

            countArray[0] = count;
            _lightingGradientProfileCountCached = count;
            NativeArray<LightingRelayTuningDTO> tuningArray = OpenDayNightRelayArray(in _dayNightTuning, DayNightTuningBuffer, 1);
            if (tuningArray.IsCreated)
            {
                LightingRelayTuningDTO tuning = tuningArray[0];
                if (tuning.Magic != DayNightTuningMagic)
                    tuning = CreateDefaultLightingTuning();
                tuning.CsvProfilesLoaded = 1f;
                tuningArray[0] = tuning;
            }
#endif
        }

        public void DumpDayNightBlackBoxNow()
        {
            DumpDayNightBlackBox();
        }

        private static int ParseLightingGradientProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<LightingGradientProfileDTO> profiles)
        {
            int cursor = 0;
            int count = 0;
            while (cursor < bytes.Length && count < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                    cursor++;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, cursor - lineStart);
                if (cursor < bytes.Length && bytes[cursor] == (byte)'\n')
                    cursor++;

                line = TrimLine(line);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int token = 0;
                if (!TryReadProfileKey(line, ref token, out uint hash))
                    continue;
                if (!TryReadUInt(line, ref token, out uint index))
                    continue;
                if (!TryReadColorValue(line, ref token, out float3 ambient))
                    continue;
                if (!TryReadColorValue(line, ref token, out float3 fog))
                    continue;
                if (!TryReadColorValue(line, ref token, out float3 directional))
                    continue;

                float weight = 1f;
                TryReadFloat(line, ref token, out weight);
                profiles[count] = CreateProfile(hash, index, ambient, fog, directional, weight);
                count++;
            }

            return count;
        }

        private static ReadOnlySpan<byte> TrimLine(ReadOnlySpan<byte> line)
        {
            int start = 0;
            int end = line.Length;
            while (start < end && IsWhitespace(line[start]))
                start++;
            while (end > start && IsWhitespace(line[end - 1]))
                end--;
            return line.Slice(start, end - start);
        }

        private static bool TryReadFloat3(ReadOnlySpan<byte> line, ref int cursor, out float3 value)
        {
            value = default;
            if (!TryReadFloat(line, ref cursor, out float x) ||
                !TryReadFloat(line, ref cursor, out float y) ||
                !TryReadFloat(line, ref cursor, out float z))
            {
                return false;
            }

            value = new float3(x, y, z);
            return math.all(math.isfinite(value));
        }

        private static bool TryReadProfileKey(ReadOnlySpan<byte> line, ref int cursor, out uint hash)
        {
            hash = 0u;
            SkipSeparators(line, ref cursor);
            if (cursor >= line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            ReadOnlySpan<byte> token = TrimLine(line.Slice(start, cursor - start));
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;
            if (token.Length == 0)
                return false;

            int tokenCursor = 0;
            if (TryReadUInt(token, ref tokenCursor, out hash))
                return true;

            hash = HashProfileName(token);
            return hash != 0u;
        }

        private static bool TryReadColorValue(ReadOnlySpan<byte> line, ref int cursor, out float3 value)
        {
            value = default;
            SkipSeparators(line, ref cursor);
            if (cursor >= line.Length)
                return false;

            byte c = line[cursor];
            if (c == (byte)'#' || (c == (byte)'0' && cursor + 1 < line.Length && (line[cursor + 1] == (byte)'x' || line[cursor + 1] == (byte)'X')))
                return TryReadHexColor(line, ref cursor, out value);

            return TryReadFloat3(line, ref cursor, out value);
        }

        private static bool TryReadHexColor(ReadOnlySpan<byte> line, ref int cursor, out float3 value)
        {
            value = default;
            SkipSeparators(line, ref cursor);
            if (cursor >= line.Length)
                return false;

            if (line[cursor] == (byte)'#')
            {
                cursor++;
            }
            else if (cursor + 1 < line.Length &&
                     line[cursor] == (byte)'0' &&
                     (line[cursor + 1] == (byte)'x' || line[cursor + 1] == (byte)'X'))
            {
                cursor += 2;
            }

            uint packed = 0u;
            int digits = 0;
            while (cursor < line.Length && digits < 8)
            {
                int digit = HexDigit(line[cursor]);
                if (digit < 0)
                    break;

                packed = (packed << 4) | (uint)digit;
                cursor++;
                digits++;
            }

            if (digits != 6 && digits != 8)
            {
                SkipTokenRemainder(line, ref cursor);
                return false;
            }

            if (digits == 6)
                packed <<= 8;

            float inv255 = 1f / 255f;
            value = new float3(
                ((packed >> 24) & 255u) * inv255,
                ((packed >> 16) & 255u) * inv255,
                ((packed >> 8) & 255u) * inv255);
            SkipTokenRemainder(line, ref cursor);
            return math.all(math.isfinite(value));
        }

        private static uint HashProfileName(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }

        private static bool TryReadUInt(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            value = 0u;
            SkipSeparators(line, ref cursor);
            if (cursor >= line.Length)
                return false;

            bool hex = cursor + 1 < line.Length &&
                line[cursor] == (byte)'0' &&
                (line[cursor + 1] == (byte)'x' || line[cursor + 1] == (byte)'X');
            if (hex)
                cursor += 2;

            uint result = 0u;
            bool any = false;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                int digit = hex ? HexDigit(c) : DecimalDigit(c);
                if (digit < 0)
                    break;

                result = hex ? (result << 4) + (uint)digit : result * 10u + (uint)digit;
                cursor++;
                any = true;
            }

            value = result;
            SkipTokenRemainder(line, ref cursor);
            return any;
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            SkipSeparators(line, ref cursor);
            if (cursor >= line.Length)
                return false;

            float sign = 1f;
            if (line[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }
            else if (line[cursor] == (byte)'+')
            {
                cursor++;
            }

            float result = 0f;
            bool any = false;
            while (cursor < line.Length)
            {
                int digit = DecimalDigit(line[cursor]);
                if (digit < 0)
                    break;

                result = result * 10f + digit;
                cursor++;
                any = true;
            }

            if (cursor < line.Length && line[cursor] == (byte)'.')
            {
                cursor++;
                float place = 0.1f;
                while (cursor < line.Length)
                {
                    int digit = DecimalDigit(line[cursor]);
                    if (digit < 0)
                        break;

                    result += digit * place;
                    place *= 0.1f;
                    cursor++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = result * sign;
            SkipTokenRemainder(line, ref cursor);
            return math.isfinite(value);
        }

        private static void SkipSeparators(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && (line[cursor] == (byte)',' || IsWhitespace(line[cursor])))
                cursor++;
        }

        private static void SkipTokenRemainder(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        private static int DecimalDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9' ? value - (byte)'0' : -1;
        }

        private static int HexDigit(byte value)
        {
            int decimalDigit = DecimalDigit(value);
            if (decimalDigit >= 0)
                return decimalDigit;
            if (value >= (byte)'a' && value <= (byte)'f')
                return value - (byte)'a' + 10;
            if (value >= (byte)'A' && value <= (byte)'F')
                return value - (byte)'A' + 10;
            return -1;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockLightingRelayJob : IJobParallelFor
        {
            [WriteOnly, NoAlias] public NativeArray<LightingRelayMockSampleDTO> Samples;
            public float PhaseSeconds;
            public float QualityWeight;

            public void Execute(int index)
            {
                float t = math.frac(((float)index / math.max(1f, Samples.Length - 1f)) + PhaseSeconds * 0.013f);
                float depth = math.lerp(12f, 2600f, H8Smooth01(math.frac(t * 0.71f + QualityWeight * 0.17f)));
                float eclipse = math.saturate(1f - math.abs(t - 0.47f) * 18f);
                float moon = H8Smooth01(1f - math.abs(t - 0.05f) * 2f);
                Samples[index] = new LightingRelayMockSampleDTO
                {
                    TimeOfDay01 = t,
                    DepthMeters = depth,
                    Eclipse01 = eclipse,
                    MoonPhase01 = moon,
                    BiomeBlend = new float4((index & 3), ((index + 1) & 3), H8Smooth01(t), QualityWeight)
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateGlobalIlluminationJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<float> SHDay;
            [ReadOnly, NoAlias] public NativeArray<float> SHNight;
            [ReadOnly, NoAlias] public NativeArray<float> SHDiscreteStates;
            [ReadOnly, NoAlias] public NativeArray<LightingGradientProfileDTO> GradientProfiles;
            [ReadOnly, NoAlias] public NativeArray<int> GradientProfileCount;
            [WriteOnly, NoAlias] public NativeArray<float> SHOutput;
            [WriteOnly, NoAlias] public NativeArray<EnvironmentLightingDTO> EnvironmentLighting;
            public BiomeGradientSignal BiomeGradient;
            public double3 PlayerAup;
            public double3 BiomeCenterAup;
            public float TimeOfDay01;
            public float DepthMeters;
            public float Depth01;
            public float Eclipse01;
            public float MoonPhase01;
            public float DepthPaletteStrength;
            public float QualityWeight;
            public float WaterExtinctionConstant;
            public float EclipseDarkeningMultiplier;

            public void Execute()
            {
                float quality = H8SaturateFinite(QualityWeight);
                float daylight01 = ResolveDaylight(TimeOfDay01, Eclipse01, EclipseDarkeningMultiplier);
                float moonlight01 = ResolveMoonlight(TimeOfDay01, MoonPhase01, Eclipse01);
                float gloom = ResolveDeepGloom(DepthMeters, WaterExtinctionConstant, quality);
                float biomeWeight = ResolveBiomeWeight(in BiomeGradient, PlayerAup, BiomeCenterAup, quality);
                LightingGradientProfileDTO biomeProfile = ResolveBiomeProfile(in BiomeGradient, GradientProfiles, GradientProfileCount);
                float3 biomeAmbient = math.max(float3.zero, biomeProfile.AmbientColor.xyz);
                float3 biomeFog = math.max(float3.zero, biomeProfile.FogColor.xyz);
                float3 biomeDirectional = math.max(new float3(0.05f, 0.08f, 0.10f), biomeProfile.DirectionalTint.xyz);
                float3 depthTint = ResolveDepthTint(Depth01, DepthPaletteStrength, MoonPhase01, biomeWeight, biomeAmbient);
                int state = ResolveDiscreteState(TimeOfDay01, daylight01);
                int offset = state * SHCoefficientCount;
                float discreteWeight = 1f - H8Smooth01((quality - 0.18f) * 5.0f);

                for (int i = 0; i < SHCoefficientCount; i++)
                {
                    float continuous = math.lerp(SHNight[i], SHDay[i], daylight01);
                    float snapped = SHDiscreteStates[offset + i];
                    float value = math.lerp(continuous, snapped, discreteWeight);
                    value *= ResolveChannelTint(i, depthTint);
                    value *= ResolveSHOrderWeight(i, quality);
                    SHOutput[i] = value * gloom;
                }

                float3 shallowAmbient = new float3(0.035f, 0.085f, 0.105f);
                float3 deepAmbient = new float3(0.002f, 0.004f, 0.008f);
                float3 dayAmbient = math.lerp(shallowAmbient, new float3(0.14f, 0.24f, 0.30f), daylight01);
                float3 nightAmbient = math.lerp(deepAmbient, new float3(0.030f, 0.044f, 0.080f), moonlight01);
                float3 baseAmbient = math.lerp(nightAmbient, dayAmbient, daylight01);
                baseAmbient = math.lerp(baseAmbient, biomeAmbient, biomeWeight * 0.62f);
                baseAmbient *= gloom;

                float3 baseFog = math.lerp(new float3(0.007f, 0.022f, 0.034f), new float3(0.020f, 0.090f, 0.110f), 1f - Depth01);
                baseFog = math.lerp(baseFog, biomeFog, biomeWeight * 0.74f);
                baseFog *= math.lerp(0.42f, 1f, gloom);

                float3 directional = math.lerp(new float3(0.022f, 0.034f, 0.070f), new float3(0.60f, 0.78f, 0.92f), daylight01);
                directional = math.lerp(directional, biomeDirectional, biomeWeight * 0.44f) * gloom;

                EnvironmentLighting[0] = new EnvironmentLightingDTO
                {
                    AmbientColor = new float4(math.max(float3.zero, baseAmbient), quality),
                    FogColor = new float4(math.max(float3.zero, baseFog), gloom),
                    DirectionalLightColor = new float4(math.max(float3.zero, directional), biomeWeight),
                    SunIntensity = daylight01 * gloom,
                    MoonIntensity = moonlight01 * gloom,
                    SHCoefficientCount = HectonGIRelaySystem.SHCoefficientCount,
                    SHQualityWeight = quality
                };
            }

            private static float ResolveDaylight(float timeOfDay01, float eclipse01, float eclipseMultiplier)
            {
                float daylight = math.saturate(1f - math.abs(timeOfDay01 - 0.5f) * 2f);
                return daylight * (1f - math.saturate(eclipse01) * math.saturate(eclipseMultiplier));
            }

            private static float ResolveMoonlight(float timeOfDay01, float moonPhase01, float eclipse01)
            {
                float night = 1f - math.saturate(1f - math.abs(timeOfDay01 - 0.5f) * 2f);
                return night * math.lerp(0.18f, 1f, H8SaturateFinite(moonPhase01)) * (1f - math.saturate(eclipse01) * 0.28f);
            }

            private static int ResolveDiscreteState(float timeOfDay01, float daylight01)
            {
                if (daylight01 < 0.18f)
                    return 0;
                if (timeOfDay01 < 0.5f)
                    return 1;
                if (daylight01 > 0.66f)
                    return 2;
                return 3;
            }

            private static float ResolveDeepGloom(float depthMeters, float extinction, float quality)
            {
                float safeDepth = math.max(0f, math.select(0f, depthMeters, math.isfinite(depthMeters)));
                float safeExtinction = math.max(0.0001f, math.select(0.0017f, extinction, math.isfinite(extinction)));
                float cheapRamp = 1f - H8SmoothRange01(180f, 2200f, safeDepth);
                float extinctionDistance = safeDepth * safeExtinction * math.lerp(0.68f, 1.36f, quality);
                float extinctionSq = extinctionDistance * extinctionDistance;
                float extinctionCube = extinctionSq * extinctionDistance;
                float exponential = math.rcp(1f + extinctionDistance + extinctionSq * 0.48f + extinctionCube * 0.235f);
                float floor = math.lerp(0.080f, 0.024f, quality);
                return math.max(floor, math.lerp(cheapRamp, exponential, H8SmoothRange01(0.26f, 0.78f, quality)));
            }

            private static float ResolveBiomeWeight(
                in BiomeGradientSignal signal,
                double3 playerAup,
                double3 biomeCenterAup,
                float quality)
            {
                float blend = H8SaturateFinite(signal.BlendFactor01);
                double3 delta64 = biomeCenterAup - playerAup;
                double distSq64 = math.min(math.max(0.0, math.dot(delta64, delta64)), 1000000000000.0);
                float distanceSqMeters = (float)distSq64;
                float cellSize = math.max(1f, math.select(64f, signal.CellSizeMeters, math.isfinite(signal.CellSizeMeters)));
                float boundary = math.abs(math.select(cellSize, signal.BoundaryDistanceMeters, math.isfinite(signal.BoundaryDistanceMeters)));
                float boundaryWeight = 1f - math.saturate(boundary / cellSize);
                float radius = cellSize * 2f;
                float localityWeight = 1f - math.saturate(distanceSqMeters / math.max(0.0001f, radius * radius));
                float qualityGate = H8SmoothRange01(0.05f, 0.42f, quality);
                float hasBiome = math.select(0f, 1f, blend > 0f || signal.BiomeAHash != 0u || signal.BiomeBHash != 0u);
                return math.saturate(blend * math.max(boundaryWeight, localityWeight) * qualityGate) * hasBiome;
            }

            private static LightingGradientProfileDTO ResolveBiomeProfile(
                in BiomeGradientSignal signal,
                NativeArray<LightingGradientProfileDTO> profiles,
                NativeArray<int> profileCount)
            {
                LightingGradientProfileDTO profileA = ResolveOneProfile(signal.BiomeAHash, signal.BiomeA, profiles, profileCount);
                LightingGradientProfileDTO profileB = ResolveOneProfile(signal.BiomeBHash, signal.BiomeB, profiles, profileCount);
                float t = H8SaturateFinite(signal.BlendFactor01);
                return new LightingGradientProfileDTO
                {
                    BiomeHash = t < 0.5f ? profileA.BiomeHash : profileB.BiomeHash,
                    BiomeIndex = t < 0.5f ? profileA.BiomeIndex : profileB.BiomeIndex,
                    AmbientColor = math.lerp(profileA.AmbientColor, profileB.AmbientColor, t),
                    FogColor = math.lerp(profileA.FogColor, profileB.FogColor, t),
                    DirectionalTint = math.lerp(profileA.DirectionalTint, profileB.DirectionalTint, t),
                    Weight = math.lerp(profileA.Weight, profileB.Weight, t)
                };
            }

            private static LightingGradientProfileDTO ResolveOneProfile(
                uint hash,
                byte index,
                NativeArray<LightingGradientProfileDTO> profiles,
                NativeArray<int> profileCount)
            {
                if (profiles.IsCreated && profileCount.IsCreated && profileCount.Length > 0)
                {
                    int count = math.clamp(profileCount[0], 0, profiles.Length);
                    for (int i = 0; i < count; i++)
                    {
                        LightingGradientProfileDTO profile = profiles[i];
                        if ((hash != 0u && profile.BiomeHash == hash) ||
                            (hash == 0u && profile.BiomeIndex == index))
                        {
                            return profile;
                        }
                    }

                    if (count > 0)
                        return profiles[math.clamp((int)index, 0, count - 1)];
                }

                float3 ambient = HashToColor(hash, index, new float3(0.018f, 0.058f, 0.072f));
                float3 fog = ambient * new float3(0.38f, 0.52f, 0.64f);
                float3 directional = ambient + new float3(0.18f, 0.22f, 0.26f);
                return new LightingGradientProfileDTO
                {
                    BiomeHash = hash,
                    BiomeIndex = index,
                    AmbientColor = new float4(ambient, 1f),
                    FogColor = new float4(fog, 1f),
                    DirectionalTint = new float4(directional, 1f),
                    Weight = 1f
                };
            }

            private static float3 HashToColor(uint hash, byte index, float3 fallback)
            {
                uint seed = hash == 0u ? (uint)(index + 1u) * 747796405u : hash;
                seed ^= seed >> 16;
                seed *= 2246822519u;
                float r = ((seed >> 0) & 255u) * (1f / 255f);
                float g = ((seed >> 8) & 255u) * (1f / 255f);
                float b = ((seed >> 16) & 255u) * (1f / 255f);
                return math.lerp(fallback, new float3(r, g, b) * 0.11f, 0.72f);
            }

            private static float3 ResolveDepthTint(
                float depth01,
                float strength,
                float moonPhase01,
                float biomeWeight,
                float3 biomeAmbient)
            {
                float3 shallow = new float3(0.34f, 0.94f, 1f);
                float3 deep = new float3(0.006f, 0.008f, 0.014f);
                float3 palette = math.lerp(shallow, deep, math.saturate(depth01));
                palette += new float3(0.015f, 0.025f, 0.04f) * H8SaturateFinite(moonPhase01) * (1f - math.saturate(depth01));
                palette = math.lerp(palette, math.max(biomeAmbient * 3f, new float3(0.08f, 0.20f, 0.16f)), math.saturate(biomeWeight) * 0.22f);
                return math.lerp(new float3(1f, 1f, 1f), palette, math.saturate(strength));
            }

            private static float ResolveChannelTint(int coefficientIndex, float3 tint)
            {
                if (coefficientIndex < SHChannelCoefficientCount)
                    return tint.x;
                if (coefficientIndex < SHChannelCoefficientCount * 2)
                    return tint.y;
                return tint.z;
            }

            private static float ResolveSHOrderWeight(int coefficientIndex, float quality)
            {
                int orderIndex = coefficientIndex % SHChannelCoefficientCount;
                if (orderIndex == 0)
                    return 1f;
                if (orderIndex <= 3)
                    return H8SmoothRange01(0.16f, 0.48f, quality);
                return H8SmoothRange01(0.44f, 0.88f, quality);
            }
        }

        [Flags]
        private enum DayNightTelemetryFlags : uint
        {
            None = 0u,
            PushedCBuffer = 1u << 0,
            CBufferUnavailable = 1u << 1,
            NonFinite = 1u << 2,
            OverBudget = 1u << 3
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float H8SaturateFinite(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float H8Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float H8SmoothRange01(float min, float max, float value)
        {
            return H8Smooth01((value - min) / math.max(0.0001f, max - min));
        }
    }
}
