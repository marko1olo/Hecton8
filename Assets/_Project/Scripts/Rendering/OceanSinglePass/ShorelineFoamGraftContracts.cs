using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Habitat.Deformation;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Rendering.OceanSinglePass
{
    public static class ShorelineFoamConstants
    {
        public const int ParamsStrideBytes = 32;
        public const int RuntimeStateStrideBytes = 64;
        public const int TelemetryEntryStrideBytes = 64;
        public const int ProfileStrideBytes = 32;
        public const int MaxCapacity = 64;
        public const int ShaderLoopMax = 16;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 16;
        public const int CsvScratchBytes = 8192;
        public const float DefaultLifetimeSeconds = 2.4f;
        public const float DefaultDepthFalloffMeters = 4.75f;
        public const float BudgetSpikeDumpThresholdMicroseconds = 1000f;
        public const uint LayoutHash = 0x53323737u;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_277.bin";
        public const string CsvRelativePath = "Assets/_Project/Data/shoreline_foam_profiles.csv";

        public const BufferID ParamsBuffer = (BufferID)71940;
        public const BufferID RuntimeStateBuffer = (BufferID)71941;
        public const BufferID TelemetryRingBuffer = (BufferID)71942;
        public const BufferID TelemetryCursorBuffer = (BufferID)71943;
        public const BufferID ProfileBuffer = (BufferID)71944;
        public const BufferID CsvScratchBuffer = (BufferID)71945;
        public const BufferID SelfAuditBuffer = (BufferID)71946;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShorelineFoamParamsDTO
    {
        [FieldOffset(0)] public float4 FoamIntensityAndFalloff;
        [FieldOffset(16)] public float4 QualityAndLimits;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShorelineFoamProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public float Intensity;
        [FieldOffset(12)] public float FalloffMeters;
        [FieldOffset(16)] public float DecayRate;
        [FieldOffset(20)] public float NormalPerturbation;
        [FieldOffset(24)] public float DepthBiasMeters;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShorelineFoamRuntimeStateDTO
    {
        [FieldOffset(0)] public uint CurrentWriteIndex;
        [FieldOffset(4)] public uint ActiveCount;
        [FieldOffset(8)] public uint TotalWritten;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float WaterSurfaceLocalY;
        [FieldOffset(24)] public float CameraLocalY;
        [FieldOffset(28)] public float DeltaSeconds;
        [FieldOffset(32)] public float DecayRate;
        [FieldOffset(36)] public float ShaderLoopLimit;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public float4 DebugLane0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShorelineFoamTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveCount;
        [FieldOffset(8)] public uint CurrentWriteIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float WaterSurfaceLocalY;
        [FieldOffset(24)] public float CameraLocalY;
        [FieldOffset(28)] public float UploadMicroseconds;
        [FieldOffset(32)] public float DepthPassMicroseconds;
        [FieldOffset(36)] public float DecayRate;
        [FieldOffset(40)] public float ShaderLoopLimit;
        [FieldOffset(44)] public float EstimatedGpuMicroseconds;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint LayoutHash;
        [FieldOffset(56)] public float NormalPerturbation;
        [FieldOffset(60)] public uint Pad0;
    }

    public static unsafe class ShorelineFoamMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQuality(float quality)
        {
            return math.isfinite(quality) ? math.saturate(quality) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothQuality(float quality)
        {
            float q = SanitizeQuality(quality);
            return q * q * (3f - 2f * q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveActiveLimit(float quality, int capacity)
        {
            int safeCapacity = math.clamp(capacity, 1, ShorelineFoamConstants.MaxCapacity);
            float raw = math.lerp(1f, safeCapacity, SmoothQuality(quality));
            return math.clamp((int)math.ceil(raw), 1, safeCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveShaderLoopLimit(float quality, int activeLimit)
        {
            int cap = math.clamp(activeLimit, 1, ShorelineFoamConstants.ShaderLoopMax);
            float raw = math.lerp(1f, cap, SmoothQuality(quality));
            return math.clamp((int)math.ceil(raw), 1, cap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDecayRate(float quality, float profileDecayRate)
        {
            float q = SanitizeQuality(quality);
            float baseRate = math.isfinite(profileDecayRate) && profileDecayRate > 0f
                ? profileDecayRate
                : math.rcp(ShorelineFoamConstants.DefaultLifetimeSeconds);
            return baseRate * math.lerp(1.45f, 0.52f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveNormalPerturbation(float quality, float profileNormal)
        {
            float q = SanitizeQuality(quality);
            float normal = math.isfinite(profileNormal) ? math.max(0f, profileNormal) : 0.07f;
            return normal * math.lerp(0.2f, 1.85f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LocalizeWaterSurfaceY(double waterSurfaceAupY, double cameraAupY)
        {
            double local = waterSurfaceAupY - cameraAupY;
            local = math.clamp(local, -4096.0, 4096.0);
            return (float)local;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ShorelineFoamProfileDTO CreateDefaultProfile()
        {
            ShorelineFoamProfileDTO profile = default;
            profile.NameHash = 0xD277F04Du;
            profile.Version = 1u;
            profile.Intensity = 1.2f;
            profile.FalloffMeters = ShorelineFoamConstants.DefaultDepthFalloffMeters;
            profile.DecayRate = math.rcp(ShorelineFoamConstants.DefaultLifetimeSeconds);
            profile.NormalPerturbation = 0.075f;
            profile.DepthBiasMeters = 0.05f;
            profile.Flags = 1u;
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ShorelineFoamParamsDTO BuildParams(
            in ShorelineFoamProfileDTO profile,
            float quality,
            float waterSurfaceLocalY,
            uint frame,
            uint lane)
        {
            float q = SanitizeQuality(quality);
            float phase = ((frame & 1023u) + lane * 37u) * 0.0174532924f;
            float triangle = math.abs(math.frac(phase) * 2f - 1f);
            float intensity = math.max(0f, profile.Intensity) * math.lerp(0.35f, 1.35f, SmoothQuality(q));
            float falloff = math.max(0.1f, profile.FalloffMeters) * math.lerp(0.62f, 1.35f, q);
            float depthBias = math.clamp(profile.DepthBiasMeters + triangle * 0.12f * q, -2f, 2f);
            float normal = ResolveNormalPerturbation(q, profile.NormalPerturbation);

            ShorelineFoamParamsDTO dto = default;
            dto.FoamIntensityAndFalloff = new float4(intensity, falloff, waterSurfaceLocalY + depthBias, 1f);
            dto.QualityAndLimits = new float4(q, depthBias, normal, 1f);
            return dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashState(
            uint frame,
            uint activeCount,
            float quality,
            float waterSurfaceLocalY,
            float cameraLocalY)
        {
            uint hash = 2166136261u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ activeCount) * 16777619u;
            hash = (hash ^ math.asuint(quality)) * 16777619u;
            hash = (hash ^ math.asuint(waterSurfaceLocalY)) * 16777619u;
            hash = (hash ^ math.asuint(cameraLocalY)) * 16777619u;
            return hash == 0u ? ShorelineFoamConstants.LayoutHash : hash;
        }

        public static bool ValidateRuntimeLayouts()
        {
            return UnsafeUtility.SizeOf<ShorelineFoamParamsDTO>() == ShorelineFoamConstants.ParamsStrideBytes &&
                   UnsafeUtility.SizeOf<ShorelineFoamProfileDTO>() == ShorelineFoamConstants.ProfileStrideBytes &&
                   UnsafeUtility.SizeOf<ShorelineFoamRuntimeStateDTO>() == ShorelineFoamConstants.RuntimeStateStrideBytes &&
                   UnsafeUtility.SizeOf<ShorelineFoamTelemetryEntry>() == ShorelineFoamConstants.TelemetryEntryStrideBytes &&
                   GetOffset<ShorelineFoamParamsDTO>(nameof(ShorelineFoamParamsDTO.FoamIntensityAndFalloff)) == 0 &&
                   GetOffset<ShorelineFoamParamsDTO>(nameof(ShorelineFoamParamsDTO.QualityAndLimits)) == 16 &&
                   typeof(ShorelineFoamParamsDTO).GetProperties().Length == 0;
        }

        private static int GetOffset<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ProcessFoamParametersJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<IntegrityStateDTO> IntegrityStates;
        [NoAlias] public NativeArray<ShorelineFoamParamsDTO> FoamParams;
        public ShorelineFoamProfileDTO Profile;
        public float ModuleDepthMeters;
        public float WaterSurfaceLocalY;
        public float GlobalQualityWeight;
        public int ActiveLimit;
        public uint Frame;

        public void Execute(int index)
        {
            if (!IntegrityStates.IsCreated || !FoamParams.IsCreated || index >= IntegrityStates.Length || index >= FoamParams.Length || index >= ActiveLimit)
                return;

            IntegrityStateDTO state = IntegrityStates[index];
            float baseStrength = math.max(0.001f, math.abs(state.BaseStrength));
            float stress01 = math.saturate(math.abs(state.CurrentStress) / baseStrength);
            float pressure01 = math.saturate(math.abs(state.AppliedPressure) * 0.0025f);
            float buckle01 = math.saturate(state.BucklingScalar);
            float damage01 = math.saturate(math.max(stress01, math.max(pressure01, buckle01)));
            float quality = ShorelineFoamMath.SanitizeQuality(GlobalQualityWeight);
            float depthGain = math.saturate(math.abs(ModuleDepthMeters) * 0.05f);

            ShorelineFoamProfileDTO localProfile = Profile;
            localProfile.Intensity *= math.lerp(0.25f, 1.75f, damage01) * math.lerp(0.6f, 1.35f, depthGain);
            localProfile.FalloffMeters *= math.lerp(0.8f, 1.35f, pressure01);
            localProfile.DepthBiasMeters += math.lerp(-0.08f, 0.22f, buckle01);

            ShorelineFoamParamsDTO dto = ShorelineFoamMath.BuildParams(
                in localProfile,
                quality,
                WaterSurfaceLocalY,
                Frame,
                (uint)index);
            dto.FoamIntensityAndFalloff.w = damage01;
            dto.QualityAndLimits.w = math.saturate(damage01 * quality);
            FoamParams[index] = dto;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DecayShorelineFoamOpacityJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ShorelineFoamParamsDTO> FoamParams;
        public float DecayRate;
        public float DeltaSeconds;

        public void Execute(int index)
        {
            ShorelineFoamParamsDTO dto = FoamParams[index];
            float opacity = math.max(0f, dto.FoamIntensityAndFalloff.w - math.max(0f, DecayRate) * math.max(0f, DeltaSeconds));
            dto.FoamIntensityAndFalloff.w = opacity;
            dto.QualityAndLimits.w = opacity > 0.0001f ? dto.QualityAndLimits.w : 0f;
            FoamParams[index] = dto;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockShorelineFoamDataJob : IJob
    {
        [NoAlias] public NativeArray<ShorelineFoamParamsDTO> FoamParams;
        [NoAlias] public NativeArray<ShorelineFoamRuntimeStateDTO> State;
        public ShorelineFoamProfileDTO Profile;
        public uint Frame;
        public float GlobalQualityWeight;
        public float WaterSurfaceLocalY;
        public float CameraLocalY;
        public float DeltaSeconds;

        public void Execute()
        {
            if (!FoamParams.IsCreated || FoamParams.Length <= 0 || !State.IsCreated || State.Length <= 0)
                return;

            int capacity = math.min(FoamParams.Length, ShorelineFoamConstants.MaxCapacity);
            int activeLimit = ShorelineFoamMath.ResolveActiveLimit(GlobalQualityWeight, capacity);
            int writeIndex = (int)(State[0].TotalWritten % (uint)capacity);
            ShorelineFoamParamsDTO dto = ShorelineFoamMath.BuildParams(
                in Profile,
                GlobalQualityWeight,
                WaterSurfaceLocalY,
                Frame,
                (uint)writeIndex);
            FoamParams[writeIndex] = dto;

            ShorelineFoamRuntimeStateDTO runtime = State[0];
            runtime.CurrentWriteIndex = (uint)((writeIndex + 1) % capacity);
            runtime.TotalWritten = runtime.TotalWritten == uint.MaxValue ? 1u : runtime.TotalWritten + 1u;
            runtime.ActiveCount = (uint)math.min(activeLimit, math.min(capacity, (int)runtime.TotalWritten));
            runtime.Frame = Frame;
            runtime.GlobalQualityWeight = ShorelineFoamMath.SanitizeQuality(GlobalQualityWeight);
            runtime.WaterSurfaceLocalY = WaterSurfaceLocalY;
            runtime.CameraLocalY = CameraLocalY;
            runtime.DeltaSeconds = math.clamp(DeltaSeconds, 0f, 0.1f);
            runtime.DecayRate = ShorelineFoamMath.ResolveDecayRate(GlobalQualityWeight, Profile.DecayRate);
            runtime.ShaderLoopLimit = ShorelineFoamMath.ResolveShaderLoopLimit(GlobalQualityWeight, (int)runtime.ActiveCount);
            runtime.Flags = 1u;
            runtime.StateHash = ShorelineFoamMath.HashState(Frame, runtime.ActiveCount, runtime.GlobalQualityWeight, WaterSurfaceLocalY, CameraLocalY);
            runtime.DebugLane0 = new float4(Profile.Intensity, Profile.FalloffMeters, Profile.DepthBiasMeters, Profile.NormalPerturbation);
            State[0] = runtime;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CopyShorelineFoamParamsToMappedBufferJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<ShorelineFoamParamsDTO> Source;
        [NoAlias] public NativeArray<ShorelineFoamParamsDTO> Destination;
        public int Count;

        public void Execute()
        {
            if (!Source.IsCreated || !Destination.IsCreated || Count <= 0)
                return;

            int count = math.min(Count, math.min(Source.Length, Destination.Length));
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Destination);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, count * ShorelineFoamConstants.ParamsStrideBytes);
        }
    }

    #if UNITY_EDITOR
    public static class ShorelineFoamProfileCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int ParseProfiles(ReadOnlySpan<byte> bytes, NativeArray<ShorelineFoamProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0 || bytes.Length <= 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            bool headerSkipped = false;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = Trim(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!headerSkipped && StartsWithNameHeader(line))
                {
                    headerSkipped = true;
                    continue;
                }

                if (TryParseProfile(line, out ShorelineFoamProfileDTO profile))
                {
                    profiles[count] = profile;
                    count++;
                    if (count >= profiles.Length)
                        break;
                }
            }

            return count;
        }

        private static bool TryParseProfile(ReadOnlySpan<byte> line, out ShorelineFoamProfileDTO profile)
        {
            profile = ShorelineFoamMath.CreateDefaultProfile();
            Span<FieldSlice> fields = stackalloc FieldSlice[6];
            if (SliceCsvLine(line, fields) < 6)
                return false;

            profile.NameHash = HashLowerAscii(fields[0].Slice(line));
            profile.Version = 1u;
            if (!TryParseFloat(fields[1].Slice(line), out profile.Intensity) ||
                !TryParseFloat(fields[2].Slice(line), out profile.FalloffMeters) ||
                !TryParseFloat(fields[3].Slice(line), out profile.DecayRate) ||
                !TryParseFloat(fields[4].Slice(line), out profile.NormalPerturbation) ||
                !TryParseFloat(fields[5].Slice(line), out profile.DepthBiasMeters))
            {
                return false;
            }

            profile.Intensity = math.clamp(profile.Intensity, 0f, 8f);
            profile.FalloffMeters = math.clamp(profile.FalloffMeters, 0.1f, 128f);
            profile.DecayRate = math.clamp(profile.DecayRate, 0.01f, 16f);
            profile.NormalPerturbation = math.clamp(profile.NormalPerturbation, 0f, 2f);
            profile.DepthBiasMeters = math.clamp(profile.DepthBiasMeters, -2f, 2f);
            profile.Flags = 1u;
            return profile.NameHash != 0u;
        }

        private static int SliceCsvLine(ReadOnlySpan<byte> line, Span<FieldSlice> fields)
        {
            int count = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i < line.Length && line[i] != (byte)',')
                    continue;

                if (count < fields.Length)
                    fields[count] = new FieldSlice(start, i);
                count++;
                start = i + 1;
            }

            return count;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start < bytes.Length && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(bytes[end]))
                end--;
            return end < start ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static bool StartsWithNameHeader(ReadOnlySpan<byte> line)
        {
            line = Trim(line);
            return line.Length >= 4 &&
                   ToLower(line[0]) == (byte)'n' &&
                   ToLower(line[1]) == (byte)'a' &&
                   ToLower(line[2]) == (byte)'m' &&
                   ToLower(line[3]) == (byte)'e';
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            bytes = Trim(bytes);
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ ToLower(bytes[i])) * FnvPrime;
            return hash == 0u ? FnvOffset : hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            bytes = Trim(bytes);
            if (bytes.Length <= 0)
                return false;

            int index = 0;
            bool negative = false;
            if (bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < bytes.Length)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = integer * 10f + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < bytes.Length)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction = fraction * 10f + (b - (byte)'0');
                    divisor *= 10f;
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit || index != bytes.Length)
                return false;

            value = integer + fraction / divisor;
            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private readonly struct FieldSlice
        {
            private readonly int _start;
            private readonly int _end;

            public FieldSlice(int start, int end)
            {
                _start = start;
                _end = end;
            }

            public ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> source)
            {
                return source.Slice(_start, math.max(0, _end - _start));
            }
        }
    }
    #endif

    public static unsafe class ShorelineFoamGraftRuntime
    {
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;

        private static VaultGenerationHandle<ShorelineFoamParamsDTO> s_paramsHandle;
        private static VaultGenerationHandle<ShorelineFoamRuntimeStateDTO> s_stateHandle;
        private static VaultGenerationHandle<ShorelineFoamTelemetryEntry> s_telemetryHandle;
        private static VaultGenerationHandle<int> s_telemetryCursorHandle;
        private static VaultGenerationHandle<ShorelineFoamProfileDTO> s_profileHandle;
#if UNITY_EDITOR
        private static VaultGenerationHandle<byte> s_csvScratchHandle;
#endif
        private static GraphicsBuffer s_bufferA;
        private static GraphicsBuffer s_bufferB;
        private static GraphicsBuffer s_activeBuffer;
        private static int s_bufferWriteIndex;
        private static int s_publishedCount;
        private static Vector4 s_publishedRuntimeParams;
        private static bool s_seeded;
        private static bool s_csvLoaded;
        private static bool s_layoutValid;
        private static float s_editorIntensity = -1f;
        private static float s_editorFalloff = -1f;
        private static float s_editorDecay = -1f;
        private static float s_editorNormal = -1f;

        public static void VisualSyncTick(
            IDataVault vault,
            string projectRootPath,
            double3 cameraAup,
            double waterSurfaceAupY,
            float qualityWeight,
            uint frame,
            float frameDeltaSeconds,
            float depthPassMicroseconds)
        {
            if (vault == null)
                return;

            if (!s_layoutValid || !EnsureVaultState(vault, projectRootPath, allowAcquire: false))
                return;

            if (!GpuBuffersReady())
                return;

            if (!TryResolve(vault, in s_paramsHandle, ShorelineFoamConstants.ParamsBuffer, ShorelineFoamConstants.MaxCapacity, out NativeArray<ShorelineFoamParamsDTO> foamParams) ||
                !TryResolve(vault, in s_stateHandle, ShorelineFoamConstants.RuntimeStateBuffer, 1, out NativeArray<ShorelineFoamRuntimeStateDTO> stateArray) ||
                !TryResolve(vault, in s_profileHandle, ShorelineFoamConstants.ProfileBuffer, ShorelineFoamConstants.ProfileCapacity, out NativeArray<ShorelineFoamProfileDTO> profiles))
            {
                return;
            }

            ShorelineFoamProfileDTO profile = ResolveProfile(profiles);
            float quality = ShorelineFoamMath.SanitizeQuality(qualityWeight);
            float waterLocalY = ShorelineFoamMath.LocalizeWaterSurfaceY(waterSurfaceAupY, cameraAup.y);
            float cameraLocalY = (float)math.clamp(cameraAup.y, -4096.0, 4096.0);
            float deltaSeconds = math.clamp(math.isfinite(frameDeltaSeconds) ? frameDeltaSeconds : 0f, 0f, 0.1f);
            float decayRate = ShorelineFoamMath.ResolveDecayRate(quality, profile.DecayRate);

            DecayShorelineFoamOpacityJob decayJob = new DecayShorelineFoamOpacityJob
            {
                FoamParams = foamParams,
                DecayRate = decayRate,
                DeltaSeconds = deltaSeconds
            };
            decayJob.Run(foamParams.Length);

            GenerateMockShorelineFoamDataJob mockJob = new GenerateMockShorelineFoamDataJob
            {
                FoamParams = foamParams,
                State = stateArray,
                Profile = profile,
                Frame = frame,
                GlobalQualityWeight = quality,
                WaterSurfaceLocalY = waterLocalY,
                CameraLocalY = cameraLocalY,
                DeltaSeconds = deltaSeconds
            };
            mockJob.Run();

            ShorelineFoamRuntimeStateDTO state = stateArray[0];
            int count = math.clamp((int)state.ActiveCount, 1, ShorelineFoamConstants.MaxCapacity);
            float uploadMicros = UploadToGpu(foamParams, count);
            s_publishedCount = math.clamp((int)state.ShaderLoopLimit, 1, math.min(count, ShorelineFoamConstants.ShaderLoopMax));
            s_publishedRuntimeParams = new Vector4(cameraLocalY, waterLocalY, quality, s_publishedCount);
            RecordTelemetry(vault, projectRootPath, in state, quality, cameraLocalY, waterLocalY, uploadMicros, depthPassMicroseconds, profile.NormalPerturbation);
        }

        public static bool EnsureColdState(IDataVault vault, string projectRootPath)
        {
            if (vault == null)
                return false;

            s_layoutValid = ShorelineFoamMath.ValidateRuntimeLayouts();
            if (!s_layoutValid || !EnsureVaultState(vault, projectRootPath, allowAcquire: true))
                return false;

            EnsureGpuBuffersCold();
            return GpuBuffersReady();
        }

        public static bool TryGetActiveBuffer(out GraphicsBuffer buffer, out int count, out Vector4 runtimeParams)
        {
            buffer = s_activeBuffer;
            count = s_publishedCount;
            runtimeParams = s_publishedRuntimeParams;
            return buffer != null && buffer.IsValid() && count > 0;
        }

        public static bool TryReadDebugFoam(out NativeArray<ShorelineFoamParamsDTO>.ReadOnly foamParams, out int count)
        {
            foamParams = default;
            count = s_publishedCount;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                count <= 0 ||
                !TryResolve(vault, in s_paramsHandle, ShorelineFoamConstants.ParamsBuffer, math.min(count, ShorelineFoamConstants.MaxCapacity), out NativeArray<ShorelineFoamParamsDTO> mutableFoamParams))
            {
                return false;
            }

            foamParams = mutableFoamParams.AsReadOnly();
            return true;
        }

        public static bool TrySetEditorProfile(float intensity, float falloffMeters, float decayRate, float normalPerturbation)
        {
            s_editorIntensity = math.clamp(intensity, 0f, 8f);
            s_editorFalloff = math.clamp(falloffMeters, 0.1f, 128f);
            s_editorDecay = math.clamp(decayRate, 0.01f, 16f);
            s_editorNormal = math.clamp(normalPerturbation, 0f, 2f);
            return true;
        }

        public static bool TryReadTelemetry(out NativeArray<ShorelineFoamTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !TryResolve(vault, in s_telemetryHandle, ShorelineFoamConstants.TelemetryRingBuffer, ShorelineFoamConstants.TelemetryCapacity, out NativeArray<ShorelineFoamTelemetryEntry> mutableTelemetry))
            {
                return false;
            }

            telemetry = mutableTelemetry.AsReadOnly();
            if (TryResolve(vault, in s_telemetryCursorHandle, ShorelineFoamConstants.TelemetryCursorBuffer, 1, out NativeArray<int> cursorArray))
                cursor = cursorArray[0];
            return true;
        }

        public static void Shutdown(IDataVault vault)
        {
            s_bufferA?.Release();
            s_bufferB?.Release();
            s_bufferA = null;
            s_bufferB = null;
            s_activeBuffer = null;
            s_publishedCount = 0;
            s_publishedRuntimeParams = default;
            s_seeded = false;
            s_csvLoaded = false;
            ReleaseVaultHandles(vault);
        }

        private static bool EnsureVaultState(IDataVault vault, string projectRootPath, bool allowAcquire)
        {
            if (!Acquire(vault, ShorelineFoamConstants.ParamsBuffer, ShorelineFoamConstants.MaxCapacity, NativeArrayOptions.ClearMemory, ref s_paramsHandle, out NativeArray<ShorelineFoamParamsDTO> _, allowAcquire))
                return false;
            if (!Acquire(vault, ShorelineFoamConstants.RuntimeStateBuffer, 1, NativeArrayOptions.ClearMemory, ref s_stateHandle, out NativeArray<ShorelineFoamRuntimeStateDTO> state, allowAcquire))
                return false;
            if (!Acquire(vault, ShorelineFoamConstants.TelemetryRingBuffer, ShorelineFoamConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory, ref s_telemetryHandle, out NativeArray<ShorelineFoamTelemetryEntry> _, allowAcquire))
                return false;
            if (!Acquire(vault, ShorelineFoamConstants.TelemetryCursorBuffer, 1, NativeArrayOptions.ClearMemory, ref s_telemetryCursorHandle, out NativeArray<int> _, allowAcquire))
                return false;
            if (!Acquire(vault, ShorelineFoamConstants.ProfileBuffer, ShorelineFoamConstants.ProfileCapacity, NativeArrayOptions.ClearMemory, ref s_profileHandle, out NativeArray<ShorelineFoamProfileDTO> profiles, allowAcquire))
                return false;

            if (!s_seeded)
            {
                if (!allowAcquire)
                    return false;

                if (state.IsCreated && state.Length > 0)
                    state[0] = default;
                SeedProfiles(profiles);
                s_seeded = true;
            }

            LoadProfilesCsvIfNeeded(vault, projectRootPath, profiles, allowAcquire);
            return true;
        }

        private static void SeedProfiles(NativeArray<ShorelineFoamProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return;

            profiles[0] = ShorelineFoamMath.CreateDefaultProfile();
            for (int i = 1; i < profiles.Length; i++)
                profiles[i] = default;
        }

        private static void LoadProfilesCsvIfNeeded(IDataVault vault, string projectRootPath, NativeArray<ShorelineFoamProfileDTO> profiles, bool allowAcquire)
        {
#if !UNITY_EDITOR
            s_csvLoaded = true;
            return;
#else
            if (!allowAcquire)
                return;

            if (s_csvLoaded || string.IsNullOrEmpty(projectRootPath))
                return;

            s_csvLoaded = true;
            string path = Path.Combine(projectRootPath, ShorelineFoamConstants.CsvRelativePath);
            if (!File.Exists(path))
                return;

            if (!Acquire(vault, ShorelineFoamConstants.CsvScratchBuffer, ShorelineFoamConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, ref s_csvScratchHandle, out NativeArray<byte> scratch, allowAcquire: true))
                return;

            int byteCount = LoadFileBytes(path, scratch);
            if (byteCount <= 0)
                return;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            ShorelineFoamProfileCsvParser.ParseProfiles(new ReadOnlySpan<byte>(ptr, byteCount), profiles);
#endif
        }

#if UNITY_EDITOR
        private static int LoadFileBytes(string absolutePath, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return 0;

            using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            int length = (int)math.min(stream.Length, scratch.Length);
            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
            Span<byte> target = new Span<byte>(destination, length);
            int total = 0;
            while (total < length)
            {
                int read = stream.Read(target.Slice(total));
                if (read <= 0)
                    break;
                total += read;
            }

            return total;
        }
#endif

        private static ShorelineFoamProfileDTO ResolveProfile(NativeArray<ShorelineFoamProfileDTO> profiles)
        {
            ShorelineFoamProfileDTO profile = profiles.IsCreated && profiles.Length > 0 && profiles[0].Version != 0u
                ? profiles[0]
                : ShorelineFoamMath.CreateDefaultProfile();

            if (s_editorIntensity >= 0f)
                profile.Intensity = s_editorIntensity;
            if (s_editorFalloff >= 0f)
                profile.FalloffMeters = s_editorFalloff;
            if (s_editorDecay >= 0f)
                profile.DecayRate = s_editorDecay;
            if (s_editorNormal >= 0f)
                profile.NormalPerturbation = s_editorNormal;
            return profile;
        }

        private static void EnsureGpuBuffersCold()
        {
            if (s_bufferA == null || !s_bufferA.IsValid())
            {
                s_bufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    ShorelineFoamConstants.MaxCapacity,
                    ShorelineFoamConstants.ParamsStrideBytes); // COLD ALLOC: shoreline foam double-buffer A - owner SHINOBU_277
            }

            if (s_bufferB == null || !s_bufferB.IsValid())
            {
                s_bufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    ShorelineFoamConstants.MaxCapacity,
                    ShorelineFoamConstants.ParamsStrideBytes); // COLD ALLOC: shoreline foam double-buffer B - owner SHINOBU_277
            }
        }

        private static bool GpuBuffersReady()
        {
            return s_bufferA != null &&
                   s_bufferA.IsValid() &&
                   s_bufferB != null &&
                   s_bufferB.IsValid();
        }

        private static float UploadToGpu(NativeArray<ShorelineFoamParamsDTO> source, int count)
        {
            if (!source.IsCreated || count <= 0)
                return 0f;

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            GraphicsBuffer target = s_bufferWriteIndex == 0 ? s_bufferA : s_bufferB;
            s_bufferWriteIndex ^= 1;
            int safeCount = math.clamp(count, 1, math.min(source.Length, ShorelineFoamConstants.MaxCapacity));
            NativeArray<ShorelineFoamParamsDTO> mapped = target.LockBufferForWrite<ShorelineFoamParamsDTO>(0, safeCount);
            try
            {
                CopyShorelineFoamParamsToMappedBufferJob copyJob = new CopyShorelineFoamParamsToMappedBufferJob
                {
                    Source = source,
                    Destination = mapped,
                    Count = safeCount
                };
                copyJob.Run();
            }
            finally
            {
                target.UnlockBufferAfterWrite<ShorelineFoamParamsDTO>(safeCount);
            }

            s_activeBuffer = target;
            long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            return (float)(ticks * (1000000.0 / System.Diagnostics.Stopwatch.Frequency));
        }

        private static void RecordTelemetry(
            IDataVault vault,
            string projectRootPath,
            in ShorelineFoamRuntimeStateDTO state,
            float quality,
            float cameraLocalY,
            float waterLocalY,
            float uploadMicros,
            float depthPassMicroseconds,
            float normalPerturbation)
        {
            if (!TryResolve(vault, in s_telemetryHandle, ShorelineFoamConstants.TelemetryRingBuffer, ShorelineFoamConstants.TelemetryCapacity, out NativeArray<ShorelineFoamTelemetryEntry> telemetry))
                return;

            int cursor = 0;
            if (TryResolve(vault, in s_telemetryCursorHandle, ShorelineFoamConstants.TelemetryCursorBuffer, 1, out NativeArray<int> cursorArray))
            {
                cursor = cursorArray[0];
                cursorArray[0] = Wrap(cursor + 1, ShorelineFoamConstants.TelemetryCapacity);
            }

            int slot = Wrap(cursor, telemetry.Length);
            ShorelineFoamTelemetryEntry entry = default;
            entry.Frame = state.Frame;
            entry.ActiveCount = state.ActiveCount;
            entry.CurrentWriteIndex = state.CurrentWriteIndex;
            entry.Flags = state.Flags;
            entry.GlobalQualityWeight = quality;
            entry.WaterSurfaceLocalY = waterLocalY;
            entry.CameraLocalY = cameraLocalY;
            entry.UploadMicroseconds = uploadMicros;
            entry.DepthPassMicroseconds = math.max(0f, depthPassMicroseconds);
            entry.DecayRate = state.DecayRate;
            entry.ShaderLoopLimit = state.ShaderLoopLimit;
            entry.EstimatedGpuMicroseconds = EstimateGpuMicroseconds((int)state.ActiveCount, quality);
            entry.StateHash = state.StateHash;
            entry.LayoutHash = ShorelineFoamConstants.LayoutHash;
            entry.NormalPerturbation = normalPerturbation;
            telemetry[slot] = entry;

            if (uploadMicros > ShorelineFoamConstants.BudgetSpikeDumpThresholdMicroseconds)
                ShorelineFoamTelemetryDump.TryWrite(projectRootPath, telemetry, slot, ShorelineFoamConstants.TelemetryCapacity);
        }

        private static float EstimateGpuMicroseconds(int count, float quality)
        {
            float rows = math.max(1f, count);
            return rows * math.lerp(0.035f, 0.19f, ShorelineFoamMath.SmoothQuality(quality));
        }

        private static bool Acquire<T>(
            IDataVault vault,
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer,
            bool allowAcquire) where T : struct
        {
            buffer = default;
            if (vault == null || length <= 0)
            {
                handle = default;
                return false;
            }

            if (IsOwnedHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= length)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                IsOwnedHandle(in existing, bufferId) &&
                vault.TryResolveHandle(in existing, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= length)
            {
                handle = existing;
                return true;
            }

            if (!allowAcquire || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (IsOwnedHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<T>(bufferId, length, OwnerSystemId, options);
            return IsOwnedHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= length;
        }

        private static bool TryResolve<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsOwnedHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOwnedHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref s_paramsHandle, ShorelineFoamConstants.ParamsBuffer);
            ReleaseVaultHandle(vault, ref s_stateHandle, ShorelineFoamConstants.RuntimeStateBuffer);
            ReleaseVaultHandle(vault, ref s_telemetryHandle, ShorelineFoamConstants.TelemetryRingBuffer);
            ReleaseVaultHandle(vault, ref s_telemetryCursorHandle, ShorelineFoamConstants.TelemetryCursorBuffer);
            ReleaseVaultHandle(vault, ref s_profileHandle, ShorelineFoamConstants.ProfileBuffer);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref s_csvScratchHandle, ShorelineFoamConstants.CsvScratchBuffer);
#endif
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsOwnedHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Wrap(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }

    public static unsafe class ShorelineFoamTelemetryDump
    {
        public static bool TryWrite(string projectRootPath, NativeArray<ShorelineFoamTelemetryEntry> telemetryRing, int writeIndex, int writtenCount)
        {
            if (string.IsNullOrEmpty(projectRootPath) || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int count = math.clamp(writtenCount, 0, math.min(telemetryRing.Length, ShorelineFoamConstants.TelemetryCapacity));
            if (count <= 0)
                return false;

            try
            {
                string path = Path.Combine(projectRootPath, ShorelineFoamConstants.DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                int start = Wrap(writeIndex, telemetryRing.Length);
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                int stride = ShorelineFoamConstants.TelemetryEntryStrideBytes;
                int firstCount = math.min(count, telemetryRing.Length - start);
                stream.Write(new ReadOnlySpan<byte>(basePtr + start * stride, firstCount * stride));
                int secondCount = count - firstCount;
                if (secondCount > 0)
                    stream.Write(new ReadOnlySpan<byte>(basePtr, secondCount * stride));
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static int Wrap(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }
}
