using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Hecton8.VFX.Wakes;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PropwashEventDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 ThrustVector;
        [FieldOffset(24)] public float Intensity;
        [FieldOffset(28)] public float Radius;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct KinematicWakeSourceDTO
    {
        [FieldOffset(0)] public double3 EngineAup;
        [FieldOffset(24)] public float3 Forward;
        [FieldOffset(36)] public float EnginePower;
        [FieldOffset(40)] public float3 LinearVelocity;
        [FieldOffset(52)] public float TailSweepPower;
        [FieldOffset(56)] public float Radius;
        [FieldOffset(60)] public float QualityWeight;
        [FieldOffset(64)] public uint EntityId;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public float AgeSeconds;
        [FieldOffset(76)] public float Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PropwashRingCursorDTO
    {
        [FieldOffset(0)] public int WriteCursor;
        [FieldOffset(4)] public int EventCount;
        [FieldOffset(8)] public int DroppedCount;
        [FieldOffset(12)] public int LastFrame;
        [FieldOffset(16)] public uint StateHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PropwashTelemetryEntry
    {
        [FieldOffset(0)] public int Frame;
        [FieldOffset(4)] public int EventCount;
        [FieldOffset(8)] public int ParticleBudgetLimit;
        [FieldOffset(12)] public int OverflowCount;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float MaxIntensity;
        [FieldOffset(24)] public float EstimatedGpuMicroseconds;
        [FieldOffset(28)] public float SdfProximityMeters;
        [FieldOffset(32)] public float3 StrongestLocalPosition;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Cursor;
        [FieldOffset(56)] public uint ProfileHash;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PropwashGpuTuningDTO
    {
        [FieldOffset(0)] public float SiltProximityMeters;
        [FieldOffset(4)] public float CurlNoiseFrequency;
        [FieldOffset(8)] public float GlobalQualityWeightOverride;
        [FieldOffset(12)] public float MaxEventRadius;
        [FieldOffset(16)] public float BiomeTintR;
        [FieldOffset(20)] public float BiomeTintG;
        [FieldOffset(24)] public float BiomeTintB;
        [FieldOffset(28)] public uint Version;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PropwashWakeProfileDTO
    {
        [FieldOffset(0)] public uint EngineHash;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public float EmissionRate;
        [FieldOffset(12)] public float ParticleLifetime;
        [FieldOffset(16)] public float TurbulenceMultiplier;
        [FieldOffset(20)] public float RadiusMultiplier;
        [FieldOffset(24)] public float IntensityMultiplier;
        [FieldOffset(28)] public float SiltLift;
        [FieldOffset(32)] public float BiomeTintR;
        [FieldOffset(36)] public float BiomeTintG;
        [FieldOffset(40)] public float BiomeTintB;
        [FieldOffset(44)] public float CurlFrequency;
        [FieldOffset(48)] public float SpawnJitter;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    public static class PropwashGpuContracts
    {
        public const int EventStrideBytes = 32;
        public const int KinematicSourceStrideBytes = 80;
        public const int WakeSourceStrideBytes = 128;
        public const int RingCursorStrideBytes = 32;
        public const int TelemetryEntryStrideBytes = 64;
        public const int TuningStrideBytes = 32;
        public const int WakeProfileStrideBytes = 64;
        public const int MockEventCount = 500;
        public const int EventRingCapacity = 512;
        public const int TelemetryCapacity = 300;
        public const int WakeProfileCapacity = 64;
        public const uint LayoutHash = 0x53483237u;
        public const uint DefaultWakeProfileHash = 0x933B5BDEu;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_237.bin";

        public static bool ValidateRuntimeLayouts()
        {
            return UnsafeUtility.SizeOf<PropwashEventDTO>() == EventStrideBytes &&
                UnsafeUtility.SizeOf<KinematicWakeSourceDTO>() == KinematicSourceStrideBytes &&
                UnsafeUtility.SizeOf<WakeSource>() == WakeSourceStrideBytes &&
                UnsafeUtility.SizeOf<PropwashRingCursorDTO>() == RingCursorStrideBytes &&
                UnsafeUtility.SizeOf<PropwashTelemetryEntry>() == TelemetryEntryStrideBytes &&
                UnsafeUtility.SizeOf<PropwashGpuTuningDTO>() == TuningStrideBytes &&
                UnsafeUtility.SizeOf<PropwashWakeProfileDTO>() == WakeProfileStrideBytes;
        }

        public static PropwashGpuTuningDTO CreateDefaultTuning()
        {
            return new PropwashGpuTuningDTO
            {
                SiltProximityMeters = 1.8f,
                CurlNoiseFrequency = 0.215f,
                GlobalQualityWeightOverride = -1f,
                MaxEventRadius = 12f,
                BiomeTintR = 0.46f,
                BiomeTintG = 0.42f,
                BiomeTintB = 0.35f,
                Version = 1u
            };
        }

        public static PropwashWakeProfileDTO CreateDefaultWakeProfile()
        {
            return new PropwashWakeProfileDTO
            {
                EngineHash = DefaultWakeProfileHash,
                Version = 1u,
                EmissionRate = 1f,
                ParticleLifetime = 0.85f,
                TurbulenceMultiplier = 1f,
                RadiusMultiplier = 1f,
                IntensityMultiplier = 1f,
                SiltLift = 0.08f,
                BiomeTintR = 0.46f,
                BiomeTintG = 0.42f,
                BiomeTintB = 0.35f,
                CurlFrequency = 0.215f,
                SpawnJitter = 0.18f,
                Reserved0 = 0u,
                Reserved1 = 0u,
                Reserved2 = 0u
            };
        }

        public static int ResolveParticleBudget(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            float curved = q * q * (3f - 2f * q);
            return math.clamp((int)math.round(math.lerp(
                VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount,
                VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount,
                curved)), 64, VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount);
        }

        public static uint HashState(int frame, int eventCount, float qualityWeight, uint profileHash)
        {
            uint hash = 2166136261u;
            hash = (hash ^ unchecked((uint)frame)) * 16777619u;
            hash = (hash ^ unchecked((uint)eventCount)) * 16777619u;
            hash = (hash ^ math.asuint(qualityWeight)) * 16777619u;
            hash = (hash ^ profileHash) * 16777619u;
            return hash == 0u ? LayoutHash : hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockPropwashEventsJob : IJob
    {
        [NoAlias] public NativeArray<PropwashEventDTO> Events;
        [NoAlias] public NativeArray<PropwashRingCursorDTO> Cursor;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public int RequestedCount;
        public int Frame;

        public void Execute()
        {
            int capacity = Events.IsCreated ? Events.Length : 0;
            if (capacity <= 0 || !Cursor.IsCreated || Cursor.Length <= 0)
                return;

            int eventCount = math.clamp(RequestedCount, 0, math.min(capacity, PropwashGpuContracts.MockEventCount));
            PropwashRingCursorDTO cursor = Cursor[0];
            int baseCursor = WrapIndex(cursor.WriteCursor, capacity);
            float quality = math.saturate(GlobalQualityWeight);
            float radiusScale = math.lerp(0.62f, 1.35f, quality);
            float forceScale = math.lerp(0.45f, 1.85f, quality);

            for (int i = 0; i < eventCount; i++)
            {
                float lane = i + 1f;
                float lane01 = lane * math.rcp(math.max(1f, eventCount));
                float phase = TimeSeconds * (0.23f + lane01 * 0.41f) + lane * 0.013671875f;
                float side = TriangleSigned(phase) * (0.35f + 7.5f * lane01);
                float lift = TriangleSigned(phase * 0.37f + 0.25f) * (0.18f + 0.75f * lane01);
                float range = 1.5f + lane01 * 18f;
                float swirl = TriangleSigned(phase * 0.71f + 0.5f);
                float intensity = math.saturate(0.18f + lane01 * 0.82f) * forceScale;
                float radius = (1.15f + 5.25f * lane01) * radiusScale;
                int slot = WrapIndex(baseCursor + i, capacity);

                Events[slot] = new PropwashEventDTO
                {
                    LocalPosition = new float3(side, lift - 0.35f, -range),
                    ThrustVector = new float3(swirl * 0.28f, math.max(0.02f, intensity * 0.11f), -intensity),
                    Intensity = intensity,
                    Radius = radius
                };
            }

            cursor.WriteCursor = WrapIndex(baseCursor + eventCount, capacity);
            cursor.EventCount = eventCount;
            cursor.DroppedCount = math.max(0, RequestedCount - eventCount);
            cursor.LastFrame = Frame;
            cursor.GlobalQualityWeight = quality;
            cursor.StateHash = PropwashGpuContracts.HashState(Frame, eventCount, quality, 0u);
            cursor.Flags = eventCount > 0 ? 1u : 0u;
            Cursor[0] = cursor;
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }

        private static float TriangleSigned(float phase)
        {
            float t = math.frac(phase);
            return (math.abs(t * 2f - 1f) * 2f) - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct HarvestWakeSourcePropwashJob : IJob
    {
        private const byte WakeSourceVehicle = 2;
        private const byte WakeSourceApexPredator = 3;
        private const uint WakeSourceBridgeFlag = 4u;

        [ReadOnly, NoAlias] public NativeArray<WakeSource> WakeSources;
        [NoAlias] public NativeArray<PropwashEventDTO> Events;
        [NoAlias] public NativeArray<PropwashRingCursorDTO> Cursor;
        public double3 CameraAup;
        public int SourceScanLimit;
        public int WriteLimit;
        public int Frame;
        public float GlobalQualityWeight;
        public uint ProfileHash;

        public void Execute()
        {
            int capacity = Events.IsCreated ? Events.Length : 0;
            if (capacity <= 0 ||
                !Cursor.IsCreated ||
                Cursor.Length <= 0 ||
                !WakeSources.IsCreated ||
                WakeSources.Length <= 0)
                return;

            int scanLimit = math.clamp(SourceScanLimit, 0, WakeSources.Length);
            int writeLimit = math.clamp(WriteLimit, 0, math.min(capacity, scanLimit));
            if (scanLimit <= 0 || writeLimit <= 0)
                return;

            PropwashRingCursorDTO cursor = Cursor[0];
            int previousCount = math.clamp(cursor.EventCount, 0, capacity);
            int writeCursor = WrapIndex(cursor.WriteCursor, capacity);
            int dropped = math.max(0, cursor.DroppedCount);
            int written = 0;
            float quality = math.saturate(GlobalQualityWeight);
            float forceScale = math.lerp(0.55f, 1.45f, quality);
            float radiusScale = math.lerp(0.75f, 1.65f, quality);
            void* sourceBase = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(WakeSources);

            for (int i = 0; i < scanLimit && written < writeLimit; i++)
            {
                ref readonly WakeSource source = ref UnsafeUtility.AsRef<WakeSource>(
                    (byte*)sourceBase + i * PropwashGpuContracts.WakeSourceStrideBytes);
                byte sourceKind = source.SourceKind != 0
                    ? source.SourceKind
                    : (byte)(source.SourceFlags & 0xFFu);
                if (source.Active == 0 ||
                    (sourceKind != WakeSourceVehicle && sourceKind != WakeSourceApexPredator))
                    continue;

                float3 velocity = source.VelocityWS;
                float intensity = math.max(0f, source.Intensity);
                float radius = math.max(0.05f, source.Radius);
                double3 localDouble = ToAbsoluteDouble3(in source.PositionAup) - CameraAup;
                float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
                float speedSq = math.lengthsq(velocity);
                bool valid =
                    intensity > 0.0001f &&
                    radius > 0.05f &&
                    speedSq > 0.0001f &&
                    math.all(math.isfinite(local)) &&
                    math.all(math.isfinite(velocity));
                if (!valid)
                    continue;

                float invSpeed = math.rsqrt(math.max(speedSq, 0.0001f));
                float3 direction = velocity * invSpeed;
                float faunaWeight = sourceKind == WakeSourceApexPredator ? 0.72f : 1f;
                int slot = WrapIndex(writeCursor + written, capacity);
                Events[slot] = new PropwashEventDTO
                {
                    LocalPosition = local,
                    ThrustVector = direction * (intensity * forceScale * faunaWeight),
                    Intensity = math.saturate(intensity * faunaWeight),
                    Radius = math.clamp(radius * radiusScale, 0.25f, 32f)
                };

                if (previousCount >= capacity && dropped < int.MaxValue)
                    dropped++;
                else
                    previousCount = math.min(capacity, previousCount + 1);
                written++;
            }

            if (written <= 0)
                return;

            cursor.WriteCursor = WrapIndex(writeCursor + written, capacity);
            cursor.EventCount = previousCount;
            cursor.DroppedCount = dropped;
            cursor.LastFrame = Frame;
            cursor.GlobalQualityWeight = quality;
            cursor.StateHash = PropwashGpuContracts.HashState(Frame, previousCount, quality, ProfileHash);
            cursor.Flags = cursor.EventCount > 0 ? (cursor.Flags | WakeSourceBridgeFlag) : cursor.Flags;
            Cursor[0] = cursor;
        }

        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * cellSize) + position.LocalX,
                (position.GridY * cellSize) + position.LocalY,
                (position.GridZ * cellSize) + position.LocalZ);
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct HarvestKinematicWakeJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicWakeSourceDTO> Sources;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<PropwashEventDTO> Events;
        public double3 CameraAup;
        public int RingWriteCursor;
        public int SourceCount;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int capacity = Events.IsCreated ? Events.Length : 0;
            int count = math.min(math.min(SourceCount, Sources.IsCreated ? Sources.Length : 0), capacity);
            if (index < 0 || index >= count || capacity <= 0)
                return;

            void* sourceBase = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Sources);
            ref readonly KinematicWakeSourceDTO source = ref UnsafeUtility.AsRef<KinematicWakeSourceDTO>(
                (byte*)sourceBase + index * PropwashGpuContracts.KinematicSourceStrideBytes);

            int writeIndex = WrapIndex(RingWriteCursor + index, capacity);
            double3 delta = source.EngineAup - CameraAup;
            float3 localPosition = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            float3 forward = math.normalizesafe(source.Forward, new float3(0f, 0f, 1f));
            float3 velocity = source.LinearVelocity;
            float3 slip = velocity - forward * math.dot(velocity, forward);
            float slipMagnitude = math.length(slip);
            float quality = math.saturate(source.QualityWeight >= 0f ? source.QualityWeight : GlobalQualityWeight);
            float engine = math.max(0f, source.EnginePower);
            float tailSweep = math.max(0f, source.TailSweepPower);
            float intensity = math.saturate(engine + tailSweep * 0.65f + slipMagnitude * 0.035f);
            bool valid = intensity > 0.0001f &&
                math.all(math.isfinite(localPosition)) &&
                math.all(math.isfinite(forward)) &&
                math.all(math.isfinite(velocity));

            Events[writeIndex] = valid
                ? new PropwashEventDTO
                {
                    LocalPosition = localPosition,
                    ThrustVector = -forward * (intensity * math.lerp(0.45f, 1.5f, quality)) + math.normalizesafe(slip) * (slipMagnitude * 0.025f),
                    Intensity = intensity,
                    Radius = math.clamp(source.Radius * math.lerp(0.75f, 1.75f, quality), 0.25f, 24f)
                }
                : default;
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CommitVehicleWakePropwashEventJob : IJob
    {
        [NoAlias] public NativeArray<PropwashEventDTO> Events;
        [NoAlias] public NativeArray<PropwashRingCursorDTO> Cursor;
        public float3 LocalPosition;
        public float3 ThrustVector;
        public float Intensity;
        public float Radius;
        public int Frame;
        public float GlobalQualityWeight;
        public uint ProfileHash;

        public void Execute()
        {
            int capacity = Events.IsCreated ? Events.Length : 0;
            if (capacity <= 0 || !Cursor.IsCreated || Cursor.Length <= 0)
                return;

            float intensity = math.max(0f, Intensity);
            float radius = math.clamp(Radius, 0.25f, 32f);
            bool valid =
                intensity > 0.0001f &&
                math.all(math.isfinite(LocalPosition)) &&
                math.all(math.isfinite(ThrustVector)) &&
                math.isfinite(radius);

            PropwashRingCursorDTO cursor = Cursor[0];
            int previousCount = math.clamp(cursor.EventCount, 0, capacity);
            int write = WrapIndex(cursor.WriteCursor, capacity);
            if (valid)
            {
                Events[write] = new PropwashEventDTO
                {
                    LocalPosition = LocalPosition,
                    ThrustVector = ThrustVector,
                    Intensity = intensity,
                    Radius = radius
                };
            }

            int written = valid ? 1 : 0;
            int nextCount = math.min(capacity, previousCount + written);
            cursor.WriteCursor = WrapIndex(write + written, capacity);
            cursor.EventCount = nextCount;
            cursor.DroppedCount += previousCount >= capacity && written > 0 ? 1 : 0;
            cursor.LastFrame = Frame;
            cursor.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            cursor.StateHash = PropwashGpuContracts.HashState(Frame, nextCount, cursor.GlobalQualityWeight, ProfileHash);
            cursor.Flags = nextCount > 0 ? (cursor.Flags | 2u) : 0u;
            Cursor[0] = cursor;
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CommitPropwashRingWriteJob : IJob
    {
        [NoAlias] public NativeArray<PropwashRingCursorDTO> Cursor;
        public int PreviousWriteCursor;
        public int WrittenCount;
        public int Capacity;
        public int Frame;
        public float GlobalQualityWeight;
        public uint ProfileHash;

        public void Execute()
        {
            if (!Cursor.IsCreated || Cursor.Length <= 0)
                return;

            int safeCapacity = math.max(1, Capacity);
            int count = math.clamp(WrittenCount, 0, safeCapacity);
            PropwashRingCursorDTO cursor = Cursor[0];
            cursor.WriteCursor = WrapIndex(PreviousWriteCursor + count, safeCapacity);
            cursor.EventCount = count;
            cursor.DroppedCount = math.max(0, WrittenCount - count);
            cursor.LastFrame = Frame;
            cursor.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            cursor.StateHash = PropwashGpuContracts.HashState(Frame, count, cursor.GlobalQualityWeight, ProfileHash);
            cursor.Flags = count > 0 ? 1u : 0u;
            Cursor[0] = cursor;
        }

        private static int WrapIndex(int value, int capacity)
        {
            int wrapped = value % capacity;
            return wrapped < 0 ? wrapped + capacity : wrapped;
        }
    }

    #if UNITY_EDITOR
    public static class PropwashGpuProfileCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint SiltProximityHash = 0xDBBF2DF6u;
        private const uint CurlNoiseFrequencyHash = 0xA0AE9CFBu;
        private const uint GlobalQualityWeightHash = 0xB00FB719u;
        private const uint QualityOverrideHash = 0xBD9CD603u;
        private const uint MaxEventRadiusHash = 0x4034C951u;
        private const uint BiomeTintRHash = 0x0ADB3688u;
        private const uint BiomeTintGHash = 0xFFDB2537u;
        private const uint BiomeTintBHash = 0xFADB1D58u;

        public static bool TryParse(ReadOnlySpan<byte> bytes, ref PropwashGpuTuningDTO tuning, out uint fileHash)
        {
            fileHash = HashBytes(bytes);
            bool changed = false;
            int cursor = 0;
            while (cursor < bytes.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(bytes, ref cursor);
                changed |= TryApplyLine(line, ref tuning);
            }

            if (changed)
            {
                tuning.Version = tuning.Version == uint.MaxValue ? 1u : tuning.Version + 1u;
            }

            return changed;
        }

        public static bool TryParseWakeProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<PropwashWakeProfileDTO> profiles,
            out int profileCount,
            out uint fileHash)
        {
            fileHash = HashBytes(bytes);
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int cursor = 0;
            while (cursor < bytes.Length && profileCount < profiles.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(bytes, ref cursor);
                TryApplyWakeProfileLine(line, profiles, ref profileCount);
            }

            return profileCount > 0;
        }

        private static bool TryApplyLine(ReadOnlySpan<byte> line, ref PropwashGpuTuningDTO tuning)
        {
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int separator = IndexOfSeparator(line);
            if (separator <= 0)
                return false;

            ReadOnlySpan<byte> key = Trim(line.Slice(0, separator));
            ReadOnlySpan<byte> valueSpan = Trim(line.Slice(separator + 1));
            if (!TryParseFloat(valueSpan, out float value))
                return false;

            switch (HashLowerAscii(key))
            {
                case SiltProximityHash:
                    tuning.SiltProximityMeters = math.clamp(value, 0.05f, 8f);
                    return true;
                case CurlNoiseFrequencyHash:
                    tuning.CurlNoiseFrequency = math.clamp(value, 0.01f, 2f);
                    return true;
                case GlobalQualityWeightHash:
                case QualityOverrideHash:
                    tuning.GlobalQualityWeightOverride = math.clamp(value, -1f, 1f);
                    return true;
                case MaxEventRadiusHash:
                    tuning.MaxEventRadius = math.clamp(value, 0.25f, 32f);
                    return true;
                case BiomeTintRHash:
                    tuning.BiomeTintR = math.saturate(value);
                    return true;
                case BiomeTintGHash:
                    tuning.BiomeTintG = math.saturate(value);
                    return true;
                case BiomeTintBHash:
                    tuning.BiomeTintB = math.saturate(value);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryApplyWakeProfileLine(
            ReadOnlySpan<byte> line,
            NativeArray<PropwashWakeProfileDTO> profiles,
            ref int profileCount)
        {
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#' || profileCount >= profiles.Length)
                return false;

            int cursor = 0;
            ReadOnlySpan<byte> engineName = Trim(ReadCsvToken(line, ref cursor));
            if (engineName.Length <= 0)
                return false;

            PropwashWakeProfileDTO profile = PropwashGpuContracts.CreateDefaultWakeProfile();
            profile.EngineHash = HashLowerAscii(engineName);
            profile.Version = 1u;

            if (!TryParseNextFloat(line, ref cursor, out profile.EmissionRate))
                return false;

            if (!TryParseOptionalFloat(line, ref cursor, ref profile.ParticleLifetime) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.TurbulenceMultiplier) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.RadiusMultiplier) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.IntensityMultiplier) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.SiltLift) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.BiomeTintR) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.BiomeTintG) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.BiomeTintB) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.CurlFrequency) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.SpawnJitter))
                return false;

            profile.EmissionRate = math.clamp(profile.EmissionRate, 0f, 1000000f);
            profile.ParticleLifetime = math.clamp(profile.ParticleLifetime, 0.05f, 12f);
            profile.TurbulenceMultiplier = math.clamp(profile.TurbulenceMultiplier, 0f, 8f);
            profile.RadiusMultiplier = math.clamp(profile.RadiusMultiplier, 0.05f, 8f);
            profile.IntensityMultiplier = math.clamp(profile.IntensityMultiplier, 0f, 8f);
            profile.SiltLift = math.clamp(profile.SiltLift, 0f, 4f);
            profile.BiomeTintR = math.saturate(profile.BiomeTintR);
            profile.BiomeTintG = math.saturate(profile.BiomeTintG);
            profile.BiomeTintB = math.saturate(profile.BiomeTintB);
            profile.CurlFrequency = math.clamp(profile.CurlFrequency, 0.01f, 4f);
            profile.SpawnJitter = math.clamp(profile.SpawnJitter, 0f, 2f);

            profiles[profileCount] = profile;
            profileCount++;
            return true;
        }

        private static ReadOnlySpan<byte> ReadCsvToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return line.Slice(start, end - start);
        }

        private static bool TryParseNextFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            if (cursor >= line.Length)
                return false;

            ReadOnlySpan<byte> token = Trim(ReadCsvToken(line, ref cursor));
            return TryParseFloat(token, out value);
        }

        private static bool TryParseOptionalFloat(ReadOnlySpan<byte> line, ref int cursor, ref float value)
        {
            if (cursor >= line.Length)
                return true;

            ReadOnlySpan<byte> token = Trim(ReadCsvToken(line, ref cursor));
            if (token.Length <= 0)
                return true;

            if (!TryParseFloat(token, out float parsed))
                return false;

            value = parsed;
            return true;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'\n')
                cursor++;

            return bytes.Slice(start, end - start);
        }

        private static int IndexOfSeparator(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsAsciiWhitespace(span[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * FnvPrime;
            return hash == 0u ? FnvOffset : hash;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * FnvPrime;
            }

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

            if (!hasDigit)
                return false;

            if (index != bytes.Length)
                return false;

            value = integer + fraction / divisor;
            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                value == (byte)'\t' ||
                value == (byte)'\r' ||
                value == (byte)'\n';
        }
    }
    #endif

    public static unsafe class PropwashTelemetryDump
    {
        public static bool TryWrite(string projectRoot, NativeArray<PropwashTelemetryEntry> telemetryRing, int writeIndex, int writtenCount)
        {
            if (string.IsNullOrEmpty(projectRoot) || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int count = math.clamp(writtenCount, 0, math.min(telemetryRing.Length, PropwashGpuContracts.TelemetryCapacity));
            if (count <= 0)
                return false;

            try
            {
                string path = Path.Combine(projectRoot, PropwashGpuContracts.DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                int start = count >= telemetryRing.Length ? WrapIndex(writeIndex, telemetryRing.Length) : 0;
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                int stride = PropwashGpuContracts.TelemetryEntryStrideBytes;
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

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }
}
