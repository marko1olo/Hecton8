using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.Equipment.Auxiliary
{
    public static class AuxiliaryEquipmentConstants
    {
        public const int MaxDeployedAuxiliaries = 1024;
        public const int MockDeploymentCount = 500;
        public const int TelemetryFrameCount = 300;
        public const int ProfileCapacity = 64;
        public const int CsvScratchBytes = 16384;
        public const float MinimumCadenceHz = 15f;
        public const float MaximumCadenceHz = 60f;
        public const float FaultDumpThresholdMicroseconds = 500f;
        public const uint OwnerHash = 0xE2290001u;
        public const uint FlarePrefabHash = 0xF1A9E229u;
        public const uint SensorPingPrefabHash = 0x51A7E229u;
        public const uint GravityTetherPrefabHash = 0x7E77E229u;
        public const uint FlareLightLaneHash = 0x464C5231u; // FLR1
        public const uint SensorPingLaneHash = 0x50494E47u; // PING
        public const uint TetherLaneHash = 0x54485452u; // THTR
    }

    public static class AuxiliaryEquipmentFlags
    {
        public const uint Active = 1u << 0;
        public const uint Flare = 1u << 1;
        public const uint SensorPing = 1u << 2;
        public const uint GravityTether = 1u << 3;
        public const uint Mock = 1u << 4;
        public const uint RoutedThisFrame = 1u << 5;
        public const uint NonFiniteRecovered = 1u << 29;
        public const uint UnknownPrefab = 1u << 30;
        public const uint Faulted = 1u << 31;
    }

    public static class AuxiliaryTuningFlags
    {
        public const uint OverrideGlobalQualityWeight = 1u << 0;
    }

    public static class AuxiliaryEquipmentVaultIds
    {
        public const BufferID Deployments = BufferID.ShinobuAuxiliaryDeployments;
        public const BufferID States = BufferID.ShinobuAuxiliaryStates;
        public const BufferID ActiveCount = BufferID.ShinobuAuxiliaryActiveCount;
        public const BufferID Tuning = BufferID.ShinobuAuxiliaryTuning;
        public const BufferID RouteCounters = BufferID.ShinobuAuxiliaryRouteCounters;
        public const BufferID VfxMatrices = BufferID.ShinobuAuxiliaryVfxMatrices;
        public const BufferID TelemetryRing = BufferID.ShinobuAuxiliaryTelemetryRing;
        public const BufferID TelemetryCursor = BufferID.ShinobuAuxiliaryTelemetryCursor;
        public const BufferID Profiles = BufferID.ShinobuAuxiliaryProfiles;
        public const BufferID CsvScratch = BufferID.ShinobuAuxiliaryCsvScratch;
        public const BufferID ActiveEquipmentState = BufferID.ShinobuAuxiliaryActiveEquipmentState;
        public const BufferID TetherAnchors = BufferID.ShinobuAuxiliaryTetherAnchors;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DeployedAuxiliaryDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public uint PrefabHashID;
        [FieldOffset(28)] public float RemainingLifetime;
        [FieldOffset(32)] private uint _pad0;
        [FieldOffset(36)] private uint _pad1;
        [FieldOffset(40)] private uint _pad2;
        [FieldOffset(44)] private uint _pad3;
        [FieldOffset(48)] private uint _pad4;
        [FieldOffset(52)] private uint _pad5;
        [FieldOffset(56)] private uint _pad6;
        [FieldOffset(60)] private uint _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AuxiliaryStateDTO
    {
        [FieldOffset(0)] public float BaseLifetime;
        [FieldOffset(4)] public float Scalar0;
        [FieldOffset(8)] public float AccumulatedDelta;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AuxiliaryTetherAnchorDTO
    {
        [FieldOffset(0)] public double3 AnchorAup;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AuxiliaryActiveEquipmentDTO
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public float CurrentBattery;
        [FieldOffset(8)] public float ThermalLoad;
        [FieldOffset(12)] public uint StateFlags;
        [FieldOffset(16)] public float PowerDrawRate;
        [FieldOffset(20)] public float HeatGenerationRate;
        [FieldOffset(24)] public ulong Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AuxiliaryProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public uint PrefabHashID;
        [FieldOffset(8)] public float Lifetime;
        [FieldOffset(12)] public float Scalar0;
        [FieldOffset(16)] public float Scalar1;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliaryTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float FlareBaseLifetime;
        [FieldOffset(8)] public float FlareIntensity;
        [FieldOffset(12)] public float FlareRange;
        [FieldOffset(16)] public float PingBaseLifetime;
        [FieldOffset(20)] public float PingExpansionRate;
        [FieldOffset(24)] public float PingMaxRadius;
        [FieldOffset(28)] public float TetherBaseLifetime;
        [FieldOffset(32)] public float TetherMaxDistance;
        [FieldOffset(36)] public float VfxScale;
        [FieldOffset(40)] public float SignalIntensityScale;
        [FieldOffset(44)] public float MinimumCadenceHz;
        [FieldOffset(48)] public float MaximumCadenceHz;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;

        public static AuxiliaryTuningDTO CreateDefault(float globalQualityWeight)
        {
            AuxiliaryTuningDTO tuning = default;
            tuning.GlobalQualityWeight = AuxiliaryEquipmentMath.Sanitize01(globalQualityWeight, 1f);
            tuning.FlareBaseLifetime = 60f;
            tuning.FlareIntensity = 3f;
            tuning.FlareRange = 15f;
            tuning.PingBaseLifetime = 8f;
            tuning.PingExpansionRate = 24f;
            tuning.PingMaxRadius = 96f;
            tuning.TetherBaseLifetime = 12f;
            tuning.TetherMaxDistance = 60f;
            tuning.VfxScale = 1f;
            tuning.SignalIntensityScale = 1f;
            tuning.MinimumCadenceHz = AuxiliaryEquipmentConstants.MinimumCadenceHz;
            tuning.MaximumCadenceHz = AuxiliaryEquipmentConstants.MaximumCadenceHz;
            tuning.Flags = 0u;
            return tuning;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AuxiliaryRouteCounterDTO
    {
        [FieldOffset(0)] public uint FlareSignals;
        [FieldOffset(4)] public uint PingSignals;
        [FieldOffset(8)] public uint TetherSignals;
        [FieldOffset(12)] public uint FaultFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliaryVfxMatrixDTO
    {
        [FieldOffset(0)] public float4 Row0;
        [FieldOffset(16)] public float4 Row1;
        [FieldOffset(32)] public float4 Row2;
        [FieldOffset(48)] public float4 Row3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliaryTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveCount;
        [FieldOffset(8)] public uint FlareSignals;
        [FieldOffset(12)] public uint PingSignals;
        [FieldOffset(16)] public uint TetherSignals;
        [FieldOffset(20)] public float EffectiveCadenceHz;
        [FieldOffset(24)] public float CpuMicroseconds;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint FaultFlags;
        [FieldOffset(36)] public uint SnapshotHash;
        [FieldOffset(40)] public uint DroppedSlots;
        [FieldOffset(44)] public uint DroppedSignals;
        [FieldOffset(48)] public uint CorruptedSignals;
        [FieldOffset(52)] public uint PeakQueuedSignals;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AuxiliaryCsvParseResult
    {
        [FieldOffset(0)] public int ParsedRows;
        [FieldOffset(4)] public int SkippedRows;
        [FieldOffset(8)] public uint LastProfileHash;
        [FieldOffset(12)] public uint FaultFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliaryFlareLightSignal : ISignal
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float Intensity;
        [FieldOffset(28)] public float RangeMeters;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public float3 ColorRgb;
        [FieldOffset(52)] public float QualityWeight;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliarySonarRequestSignal : ISignal
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float CurrentRadius;
        [FieldOffset(28)] public float Intensity;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public float ExpansionRate;
        [FieldOffset(44)] public float MaxRadius;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliaryTetherConnectionSignal : ISignal
    {
        [FieldOffset(0)] public double3 ProjectileAup;
        [FieldOffset(24)] public double3 AnchorAup;
        [FieldOffset(48)] public float RestLength;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AuxiliaryLayoutAuditDTO
    {
        [FieldOffset(0)] public int DeploymentSize;
        [FieldOffset(4)] public int DeploymentAlign;
        [FieldOffset(8)] public int AupOffset;
        [FieldOffset(12)] public int PrefabHashOffset;
        [FieldOffset(16)] public int LifetimeOffset;
        [FieldOffset(20)] public int StateSize;
        [FieldOffset(24)] public int SignalFlareSize;
        [FieldOffset(28)] public int SignalPingSize;
        [FieldOffset(32)] public int SignalTetherSize;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    public static class AuxiliaryEquipmentMath
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback)
        {
            float safeFallback = math.select(0f, fallback, math.isfinite(fallback));
            return math.saturate(math.select(safeFallback, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeNonNegative(float value, float fallback)
        {
            float safeFallback = math.select(0f, fallback, math.isfinite(fallback));
            float safe = math.select(safeFallback, value, math.isfinite(value));
            return math.max(0f, safe);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizePositive(float value, float fallback)
        {
            float safeFallback = math.select(0.01f, fallback, math.isfinite(fallback));
            float safe = math.select(safeFallback, value, math.isfinite(value));
            return math.max(0.01f, safe);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCadenceHz(float globalQualityWeight, in AuxiliaryTuningDTO tuning)
        {
            float q = Sanitize01(globalQualityWeight, tuning.GlobalQualityWeight);
            float minHz = math.max(1f, math.select(AuxiliaryEquipmentConstants.MinimumCadenceHz, tuning.MinimumCadenceHz, math.isfinite(tuning.MinimumCadenceHz)));
            float maxHz = math.max(minHz, math.select(AuxiliaryEquipmentConstants.MaximumCadenceHz, tuning.MaximumCadenceHz, math.isfinite(tuning.MaximumCadenceHz)));
            float curved = q * q * (3f - (2f * q));
            return math.lerp(minHz, maxHz, curved);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveBaseLifetime(uint prefabHash, in AuxiliaryTuningDTO tuning)
        {
            if (prefabHash == AuxiliaryEquipmentConstants.FlarePrefabHash)
                return SanitizePositive(tuning.FlareBaseLifetime, 60f);
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return SanitizePositive(tuning.PingBaseLifetime, 8f);
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                return SanitizePositive(tuning.TetherBaseLifetime, 12f);
            return 0.01f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveKindFlags(uint prefabHash)
        {
            if (prefabHash == AuxiliaryEquipmentConstants.FlarePrefabHash)
                return AuxiliaryEquipmentFlags.Active | AuxiliaryEquipmentFlags.Flare;
            if (prefabHash == AuxiliaryEquipmentConstants.SensorPingPrefabHash)
                return AuxiliaryEquipmentFlags.Active | AuxiliaryEquipmentFlags.SensorPing;
            if (prefabHash == AuxiliaryEquipmentConstants.GravityTetherPrefabHash)
                return AuxiliaryEquipmentFlags.Active | AuxiliaryEquipmentFlags.GravityTether;
            return AuxiliaryEquipmentFlags.UnknownPrefab | AuxiliaryEquipmentFlags.Faulted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashAupFrame(double3 aup, uint frame, uint salt)
        {
            uint hash = FnvOffset;
            long x = (long)math.round(aup.x * 1000.0);
            long y = (long)math.round(aup.y * 1000.0);
            long z = (long)math.round(aup.z * 1000.0);
            hash = MixLong(hash, x);
            hash = MixLong(hash, y);
            hash = MixLong(hash, z);
            hash = Mix(hash, frame);
            hash = Mix(hash, salt);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DeterministicNoise01(double3 aup, uint frame, uint salt)
        {
            uint hash = HashAupFrame(aup, frame, salt);
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            hash *= 3266489917u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldSnapshot(uint hash, in DeployedAuxiliaryDTO deployment)
        {
            hash = Mix(hash, deployment.PrefabHashID);
            hash = Mix(hash, math.asuint(deployment.RemainingLifetime));
            hash = Mix(hash, HashAupFrame(deployment.AUP_Position, 0u, deployment.PrefabHashID));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FnvaByte(uint hash, byte value)
        {
            return (hash ^ value) * FnvPrime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint hash, uint value)
        {
            hash = FnvaByte(hash, (byte)value);
            hash = FnvaByte(hash, (byte)(value >> 8));
            hash = FnvaByte(hash, (byte)(value >> 16));
            return FnvaByte(hash, (byte)(value >> 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixLong(uint hash, long value)
        {
            ulong u = unchecked((ulong)value);
            hash = Mix(hash, (uint)u);
            return Mix(hash, (uint)(u >> 32));
        }
    }
}
