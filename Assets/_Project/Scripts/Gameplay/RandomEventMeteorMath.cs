using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Stateless deterministic meteor-shower math extracted from <see cref="RandomEventSystem"/>.
    /// </summary>
    public static class RandomEventMeteorMath
    {
        private const float HashToUnit = 1f / 16777215f;
        private const float MinimumFlashRate = 0.01f;
        private const float MeteorFlashDecay = 11.5f;
        private const float ExpApproxQuadratic = 0.48f;
        private const float ExpApproxCubic = 0.235f;

        /// <summary>
        /// Evaluates one deterministic meteor flash sample from event age and seed.
        /// </summary>
        /// <param name="eventAgeSeconds">Elapsed meteor-shower event age in seconds.</param>
        /// <param name="seed">Stable meteor event seed.</param>
        /// <param name="flashRate">Flash cadence in flashes per second.</param>
        /// <returns>Normalized flash intensity in [0,1].</returns>
        public static float EvaluateMeteorFlash(float eventAgeSeconds, float seed, float flashRate)
        {
            float safeRate = math.max(MinimumFlashRate, flashRate);
            float phase = math.max(0f, eventAgeSeconds) * safeRate + seed * 0.017f;
            int flashIndex = (int)math.floor(phase);
            float local = phase - flashIndex;
            float gate = Hash01(unchecked((uint)flashIndex), unchecked((uint)(int)math.round(seed)));
            if (gate < 0.56f)
                return 0f;

            float envelope = FastExpNegPositive(local * MeteorFlashDecay);
            return math.saturate(envelope * math.lerp(0.45f, 1f, gate));
        }

        private static float FastExpNegPositive(float x)
        {
            float clampedX = math.max(0f, x);
            float x2 = clampedX * clampedX;
            float denominator = 1f + clampedX + ExpApproxQuadratic * x2 + ExpApproxCubic * x2 * clampedX;
            return math.rcp(math.max(0.0001f, denominator));
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
