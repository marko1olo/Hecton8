using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;

namespace Hecton8.World.SeedShipAnomaly
{
    public static class SeedShipAnomalyConstants
    {
        public const int SingletonLength = 1;
        public const int TelemetryFrameCount = 300;
        public const int DefaultMockLeviathanCapacity = 50000;
        public const int CsvOverrideCapacity = 32;
        public const float DefaultRadiusMeters = 3000f;
        public const float DefaultSeedShipDepthMeters = -5000f;
        public const uint SourceHash = 0x53485341u; // SHSA
        public const uint GlitchHash = 0x53474C54u; // SGLT
        public const uint CoreHackAcceptedHash = 0x53484B30u; // SHK0
        public const uint RadarJamLaneHash = 0x524A414Du; // RJAM
        public const uint CoreHackLaneHash = 0x4348414Bu; // CHAK
        public const uint MockHudLaneHash = 0x4D485544u; // MHDD
        public const uint MockAupRebaseLaneHash = 0x4D415550u; // MAUP
        public const int DefaultGlitchGlyphCount = 16;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct AnomalyFieldDTO
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Radius;
        [FieldOffset(28)] public float CorruptionLevel;
        [FieldOffset(32)] public uint GlitchHash;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnomalyTuningDTO
    {
        [FieldOffset(0)] public float MaxCorruptionRadius;
        [FieldOffset(4)] public float GravityInversionStrength;
        [FieldOffset(8)] public float PulseFrequency;
        [FieldOffset(12)] public float GlitchIntensity;
        [FieldOffset(16)] public float HeatEmission;
        [FieldOffset(20)] public float RadiationEmission;
        [FieldOffset(24)] public float RadarJamIntensity;
        [FieldOffset(28)] public float BabelScrambleStrength;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public int MaxEntityBudget;
        [FieldOffset(40)] public int MinEntityBudget;
        [FieldOffset(44)] public float ShaderNoiseStrength;
        [FieldOffset(48)] public float HealingRateScalar;
        [FieldOffset(52)] public float MockRebaseChance01;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnomalyGlobalScalarsDTO
    {
        [FieldOffset(0)] public float Corruption01;
        [FieldOffset(4)] public float GravityY;
        [FieldOffset(8)] public float ShaderCorruption01;
        [FieldOffset(12)] public float UniverseOffsetNoise01;
        [FieldOffset(16)] public float HeatSource01;
        [FieldOffset(20)] public float Radiation01;
        [FieldOffset(24)] public float RadarJam01;
        [FieldOffset(28)] public float BabelScramble01;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public int EntityBudget;
        [FieldOffset(40)] public int EntitiesAffected;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public float AnomalyComputeTimeMs;
        [FieldOffset(56)] public float RadiusMeters;
        [FieldOffset(60)] public uint LastRebaseFrame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockLeviathanState
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float AggressionWeight;
        [FieldOffset(28)] public float LightAversion;
        [FieldOffset(32)] public float Frenzy01;
        [FieldOffset(36)] public float Corruption01;
        [FieldOffset(40)] public uint EntityId;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float LastDistanceMeters;
        [FieldOffset(52)] public uint LastFrame;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct AnomalyThermoSourceDTO
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Heat01;
        [FieldOffset(28)] public float Radiation01;
        [FieldOffset(32)] public float RadiusMeters;
        [FieldOffset(36)] public float Pulse01;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnomalyTelemetryEntry
    {
        [FieldOffset(0)] public float CurrentCorruptionLevel;
        [FieldOffset(4)] public int EntitiesAffected;
        [FieldOffset(8)] public float AnomalyComputeTimeMs;
        [FieldOffset(12)] public float GravityY;
        [FieldOffset(16)] public float RadarJam01;
        [FieldOffset(20)] public float HeatSource01;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint StateHash;
        [FieldOffset(40)] public double3 EpicenterAUP;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AnomalyCsvOverrideDTO
    {
        [FieldOffset(0)] public uint KeyHash;
        [FieldOffset(4)] public float Value;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    public static class SeedShipAnomalyMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveCorruption01(double3 actorAup, double3 epicenterAup, float radiusMeters)
        {
            float safeRadius = math.max(1f, math.isfinite(radiusMeters) ? radiusMeters : SeedShipAnomalyConstants.DefaultRadiusMeters);
            double3 delta64 = actorAup - epicenterAup;
            float3 delta = (float3)delta64;
            if (!math.all(math.isfinite(delta)))
                return 0f;

            float radiusSq = safeRadius * safeRadius;
            float distSq = math.dot(delta, delta);
            float falloff = math.saturate(1f - distSq * math.rcp(math.max(1f, radiusSq)));
            float smooth = falloff * falloff * (3f - 2f * falloff);
            float inverse = math.rsqrt(1f + distSq * math.rcp(math.max(1f, radiusSq)));
            return math.saturate(smooth * inverse);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveGravityY(float corruption01, float pulseSeconds, float inversionStrength)
        {
            float corruption = math.saturate(corruption01);
            float gate = SmoothStep(0.45f, 0.65f, corruption);
            float pulse01 = 0.5f + 0.5f * math.sin(pulseSeconds);
            float inverted = math.lerp(9.80665f, -2f, math.saturate(pulse01 * math.saturate(inversionStrength)));
            return math.lerp(9.80665f, inverted, gate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveEntityBudget(int entityCapacity, float globalQualityWeight, float corruption01, int minBudget, int maxBudget)
        {
            int capacity = math.max(0, entityCapacity);
            if (capacity == 0)
                return 0;

            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float corruptionGate = SmoothStep(0.05f, 0.25f, math.saturate(corruption01));
            float qualitySq = quality * quality;
            float curvedQuality = qualitySq * qualitySq;
            float activeGate = math.step(0.000001f, curvedQuality * corruptionGate);
            float minFloor = math.lerp(0f, math.max(0, minBudget), SmoothStep(0.35f, 0.75f, quality));
            float maxTarget = math.max(minFloor, math.max(minBudget, maxBudget));
            float requested = math.lerp(minFloor, maxTarget, curvedQuality) * corruptionGate * activeGate;
            return math.clamp((int)math.ceil(requested), 0, capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnomalyTuningDTO SanitizeTuning(AnomalyTuningDTO tuning)
        {
            tuning.MaxCorruptionRadius = math.clamp(FiniteOr(tuning.MaxCorruptionRadius, SeedShipAnomalyConstants.DefaultRadiusMeters), 1f, 12000f);
            tuning.GravityInversionStrength = math.saturate(FiniteOr(tuning.GravityInversionStrength, 1f));
            tuning.PulseFrequency = math.clamp(FiniteOr(tuning.PulseFrequency, 1.7f), 0.01f, 32f);
            tuning.GlitchIntensity = math.saturate(FiniteOr(tuning.GlitchIntensity, 0.85f));
            tuning.HeatEmission = math.saturate(FiniteOr(tuning.HeatEmission, 0.9f));
            tuning.RadiationEmission = math.saturate(FiniteOr(tuning.RadiationEmission, 0.7f));
            tuning.RadarJamIntensity = math.saturate(FiniteOr(tuning.RadarJamIntensity, 0.8f));
            tuning.BabelScrambleStrength = math.saturate(FiniteOr(tuning.BabelScrambleStrength, 0.65f));
            tuning.GlobalQualityWeight = math.saturate(FiniteOr(tuning.GlobalQualityWeight, 1f));
            tuning.MinEntityBudget = math.clamp(tuning.MinEntityBudget, 0, SeedShipAnomalyConstants.DefaultMockLeviathanCapacity);
            tuning.MaxEntityBudget = math.clamp(tuning.MaxEntityBudget <= 0 ? SeedShipAnomalyConstants.DefaultMockLeviathanCapacity : tuning.MaxEntityBudget, tuning.MinEntityBudget, SeedShipAnomalyConstants.DefaultMockLeviathanCapacity);
            tuning.ShaderNoiseStrength = math.saturate(FiniteOr(tuning.ShaderNoiseStrength, 0.75f));
            tuning.HealingRateScalar = math.clamp(FiniteOr(tuning.HealingRateScalar, 1f), 0.01f, 10f);
            tuning.MockRebaseChance01 = math.saturate(FiniteOr(tuning.MockRebaseChance01, 0.015f));
            return tuning;
        }

        public static int ScrambleUtf8Bytes(Span<byte> utf8Bytes, float corruption01, uint seed)
        {
            if (utf8Bytes.Length == 0)
                return 0;

            uint rngSeed = seed != 0u ? seed : SeedShipAnomalyConstants.GlitchHash;
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(rngSeed);
            uint threshold = (uint)math.round(math.saturate(corruption01) * 65535f);
            int changed = 0;
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                byte value = utf8Bytes[i];
                if (value < 0x20 || value > 0x7Eu)
                    continue;

                if ((random.NextUInt() & 0xFFFFu) > threshold)
                    continue;

                utf8Bytes[i] = ResolveDefaultGlitchGlyph((int)(random.NextUInt() & 15u));
                changed++;
            }

            return changed;
        }

        public static int ScrambleUtf8Bytes(Span<byte> utf8Bytes, ReadOnlySpan<byte> glitchTable, float corruption01, uint seed)
        {
            if (utf8Bytes.Length == 0 || glitchTable.Length == 0)
                return 0;

            uint rngSeed = seed != 0u ? seed : SeedShipAnomalyConstants.GlitchHash;
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(rngSeed);
            uint threshold = (uint)math.round(math.saturate(corruption01) * 65535f);
            int changed = 0;
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                byte value = utf8Bytes[i];
                if (value < 0x20 || value > 0x7Eu)
                    continue;

                if ((random.NextUInt() & 0xFFFFu) > threshold)
                    continue;

                int glyphIndex = (int)(random.NextUInt() % (uint)glitchTable.Length);
                utf8Bytes[i] = glitchTable[glyphIndex];
                changed++;
            }

            return changed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = math.saturate((value - edge0) * math.rcp(math.max(0.0001f, edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashFrameState(float corruption01, int entitiesAffected, uint frame)
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(corruption01)) * 16777619u;
            hash = (hash ^ (uint)entitiesAffected) * 16777619u;
            hash = (hash ^ frame) * 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashAupSector(double3 aup)
        {
            const double invSectorMeters = 1.0 / 1024.0;
            long sx = (long)math.floor(aup.x * invSectorMeters);
            long sy = (long)math.floor(aup.y * invSectorMeters);
            long sz = (long)math.floor(aup.z * invSectorMeters);
            uint hash = 2166136261u;
            hash = MixLong(hash, sx);
            hash = MixLong(hash, sy);
            hash = MixLong(hash, sz);
            return hash != 0u ? hash : SeedShipAnomalyConstants.SourceHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixLong(uint hash, long value)
        {
            ulong raw = unchecked((ulong)value);
            hash = (hash ^ (uint)raw) * 16777619u;
            hash = (hash ^ (uint)(raw >> 32)) * 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveDefaultGlitchGlyph(int index)
        {
            switch (index & 15)
            {
                case 0: return (byte)'#';
                case 1: return (byte)'%';
                case 2: return (byte)'@';
                case 3: return (byte)'?';
                case 4: return (byte)'0';
                case 5: return (byte)'1';
                case 6: return (byte)'X';
                case 7: return (byte)'_';
                case 8: return (byte)'/';
                case 9: return (byte)'\\';
                case 10: return (byte)'|';
                case 11: return (byte)'~';
                case 12: return (byte)'^';
                case 13: return (byte)'*';
                case 14: return (byte)'!';
                default: return (byte)'=';
            }
        }
    }
}
