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
            ping = default;
            if (depthMeters < MinimumDepthMeters)
                return false;

            float cyclePosition = unscaledTimeSeconds % CycleSeconds;
            if (cyclePosition > WindowSeconds)
                return false;

            int cycleIndex = Mathf.FloorToInt(unscaledTimeSeconds / CycleSeconds);
            uint hash = HashGhostSignal(
                unchecked((uint)worldSeed),
                unchecked((uint)cycleIndex),
                unchecked((uint)Mathf.FloorToInt(depthMeters)));
            if ((hash & 0xFFu) > 8u)
                return false;

            float intensity = 0.52f + (((hash >> 8) & 0xFFu) / 255f) * 0.24f;
            if (intensity <= weakestIntensity)
                return false;

            float angleRadians = (((hash >> 16) & 0xFFFFu) / 65535f) * Mathf.PI * 2f;
            float radius = 0.18f + (((hash >> 4) & 0x0Fu) / 15f) * 0.24f;
            float vertical = -0.18f + (((hash >> 12) & 0x0Fu) / 15f) * 0.36f;
            ping = new Vector4(
                Mathf.Sin(angleRadians) * radius,
                vertical,
                Mathf.Cos(angleRadians) * radius,
                intensity);
            return true;
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
