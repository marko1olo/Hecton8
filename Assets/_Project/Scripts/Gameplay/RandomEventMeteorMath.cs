using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Stateless deterministic meteor-shower math extracted from <see cref="RandomEventSystem"/>.
    /// </summary>
    public static class RandomEventMeteorMath
    {
        private const float HashToUnit = 1f / 16777215f;

        /// <summary>
        /// Evaluates one deterministic meteor flash sample from event age and seed.
        /// </summary>
        /// <param name="eventAgeSeconds">Elapsed meteor-shower event age in seconds.</param>
        /// <param name="seed">Stable meteor event seed.</param>
        /// <param name="flashRate">Flash cadence in flashes per second.</param>
        /// <returns>Normalized flash intensity in [0,1].</returns>
        public static float EvaluateMeteorFlash(float eventAgeSeconds, float seed, float flashRate)
        {
            float safeRate = Mathf.Max(0.01f, flashRate);
            float phase = Mathf.Max(0f, eventAgeSeconds) * safeRate + seed * 0.017f;
            int flashIndex = Mathf.FloorToInt(phase);
            float local = phase - flashIndex;
            float gate = Hash01(unchecked((uint)flashIndex), unchecked((uint)Mathf.RoundToInt(seed)));
            if (gate < 0.56f)
                return 0f;

            float envelope = Mathf.Exp(-local * 11.5f);
            return Mathf.Clamp01(envelope * Mathf.Lerp(0.45f, 1f, gate));
        }

        /// <summary>
        /// Returns a deterministic 24-bit hash mapped to [0,1].
        /// </summary>
        /// <param name="a">First hash lane.</param>
        /// <param name="b">Second hash lane.</param>
        /// <returns>Stable normalized hash value.</returns>
        public static float Hash01(uint a, uint b)
        {
            uint state = a * 747796405u + b * 2891336453u + 0x9E3779B9u;
            state ^= state >> 16;
            state *= 2246822519u;
            state ^= state >> 13;
            state *= 3266489917u;
            state ^= state >> 16;
            return (state & 0x00FFFFFFu) * HashToUnit;
        }
    }
}
