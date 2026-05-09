using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Stateless deterministic resolver for rare PDA sonar ghost pings.
    /// </summary>
    internal static class GhostSignalUtility
    {
        internal const float MinimumDepthMeters = 450f;
        internal const float CycleSeconds = 137f;
        internal const float WindowSeconds = 7f;
        private const uint GhostSignalSalt = 0x47535431u;

        internal static bool TryResolvePing(
            int worldSeed,
            float unscaledTimeSeconds,
            float depthMeters,
            float weakestIntensity,
            out Vector4 ping)
        {
            if (!TryResolveCandidate(worldSeed, unscaledTimeSeconds, depthMeters, out ping))
                return false;

            return ping.w > weakestIntensity;
        }

        internal static bool TryResolveCandidate(
            int worldSeed,
            float unscaledTimeSeconds,
            float depthMeters,
            out Vector4 ping)
        {
            ping = default;
            if (depthMeters < MinimumDepthMeters)
                return false;

            float cyclePosition = unscaledTimeSeconds % CycleSeconds;
            if (cyclePosition > WindowSeconds)
                return false;

            int cycleIndex = ResolveCycleIndex(unscaledTimeSeconds);
            uint hash = HashGhostSignal(
                unchecked((uint)worldSeed),
                unchecked((uint)cycleIndex),
                unchecked((uint)Mathf.FloorToInt(depthMeters)));
            if ((hash & 0xFFu) > 8u)
                return false;

            float intensity = 0.52f + (((hash >> 8) & 0xFFu) / 255f) * 0.24f;
            float radius = 0.18f + (((hash >> 4) & 0x0Fu) / 15f) * 0.24f;
            float vertical = -0.18f + (((hash >> 12) & 0x0Fu) / 15f) * 0.36f;
            ResolveDiamondOffset01((hash >> 16) & 0xFFFFu, radius, out float x, out float z);
            ping = new Vector4(
                x,
                vertical,
                z,
                intensity);
            return true;
        }

        private static void ResolveDiamondOffset01(uint phase16, float radius, out float x, out float z)
        {
            float phase4 = (phase16 * (1f / 65535f)) * 4f;
            int quadrant = (int)phase4;
            if (quadrant > 3)
                quadrant = 3;

            float t = phase4 - quadrant;
            if (quadrant == 0)
            {
                x = t;
                z = 1f - t;
            }
            else if (quadrant == 1)
            {
                x = 1f - t;
                z = -t;
            }
            else if (quadrant == 2)
            {
                x = -t;
                z = -(1f - t);
            }
            else
            {
                x = -(1f - t);
                z = t;
            }

            x *= radius;
            z *= radius;
        }

        internal static int ResolveCycleIndex(float unscaledTimeSeconds)
        {
            return Mathf.FloorToInt(unscaledTimeSeconds / CycleSeconds);
        }

        internal static uint HashGhostSignal(uint seed, uint cycleIndex, uint depthMeters)
        {
            unchecked
            {
                uint hash = 2166136261u ^ GhostSignalSalt;
                hash = (hash ^ seed) * 16777619u;
                hash = (hash ^ cycleIndex) * 16777619u;
                hash = (hash ^ depthMeters) * 16777619u;
                hash ^= hash >> 13;
                hash *= 1274126177u;
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}
