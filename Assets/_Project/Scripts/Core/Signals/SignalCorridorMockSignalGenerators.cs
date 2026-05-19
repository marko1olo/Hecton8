using System.Runtime.CompilerServices;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Cold-path deterministic signal injectors for isolated SignalBus validation.
    /// </summary>
    [Preserve]
    public static class MockSignalGenerators
    {
        private const uint LcgA = 1664525u;
        private const uint LcgC = 1013904223u;

        public static int InjectAcousticBurst(
            in AbsoluteUniversePosition originAup,
            int count,
            uint sourceId,
            float radiusMeters,
            float intensity01,
            uint seed,
            byte channel = AcousticPingSignal.ChannelActiveSonar)
        {
            int safeCount = math.clamp(count, 0, 4096);
            float safeRadius = math.max(0f, math.isfinite(radiusMeters) ? radiusMeters : 0f);
            float safeIntensity = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            uint state = seed != 0u ? seed : Mix(sourceId ^ 0xA60057C1u);
            int pushed = 0;

            SignalBus<AcousticPingSignal>.EnsureInitialized();
            for (int i = 0; i < safeCount; i++)
            {
                float3 offset = ResolveDeterministicOffset(ref state, 1.0f);
                AbsoluteUniversePosition pingAup = AbsoluteUniversePosition.OffsetMeters(
                    in originAup,
                    new double3(offset.x, offset.y, offset.z));
                AcousticPingSignal signal = default;
                signal.PositionAup = pingAup;
                signal.RadiusMeters = safeRadius;
                signal.Intensity01 = safeIntensity;
                signal.SourceId = sourceId;
                signal.Channel = channel;
                signal.Flags = channel == AcousticPingSignal.ChannelActiveSonar
                    ? AcousticPingSignal.FlagActiveSonar
                    : (byte)0;

                if (SignalBus<AcousticPingSignal>.TryPush(in signal))
                    pushed++;
            }

            return pushed;
        }

        public static int InjectCombatDamageBurst(
            double3 originAup,
            int count,
            uint targetHash,
            uint sourceHash,
            float magnitude,
            uint damageType,
            uint seed,
            byte channel = 0)
        {
            int safeCount = math.clamp(count, 0, 4096);
            float safeMagnitude = math.max(0f, math.isfinite(magnitude) ? magnitude : 0f);
            uint state = seed != 0u ? seed : Mix(targetHash ^ sourceHash ^ 0xC0DA6E11u);
            int pushed = 0;

            SignalBus<CombatDamageSignal>.EnsureInitialized();
            for (int i = 0; i < safeCount; i++)
            {
                float3 offset = ResolveDeterministicOffset(ref state, 0.5f);
                CombatDamageSignal signal = default;
                signal.ImpactAup = originAup + new double3(offset.x, offset.y, offset.z);
                signal.Direction = math.normalizesafe(offset, new float3(0f, 1f, 0f));
                signal.Magnitude = safeMagnitude;
                signal.DamageType = damageType;
                signal.TargetHash = targetHash;
                signal.SourceHash = sourceHash;
                signal.Frame = seed;
                signal.SourceId = unchecked((ushort)(sourceHash & 0xFFFFu));
                signal.TargetId = unchecked((ushort)(targetHash & 0xFFFFu));
                signal.Channel = channel;
                signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
                signal.IntegrityDelta = (byte)math.clamp((int)math.round(safeMagnitude * 255f), 0, 255);

                if (SignalBus<CombatDamageSignal>.TryPush(in signal))
                    pushed++;
            }

            return pushed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveDeterministicOffset(ref uint state, float radiusMeters)
        {
            float x = NextSigned01(ref state);
            float y = NextSigned01(ref state);
            float z = NextSigned01(ref state);
            return new float3(x, y, z) * math.max(0f, radiusMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float NextSigned01(ref uint state)
        {
            state = (state * LcgA) + LcgC;
            uint mantissa = (state >> 9) | 0x3F800000u;
            return (math.asfloat(mantissa) - 1f) * 2f - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }
}
