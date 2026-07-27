using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    /// <summary>
    /// Stateless cold-path metallic grain-bank builder for player-critical DSP synthesis.
    /// </summary>
    internal static class PlayerCriticalMetallicGrainBank
    {
        public static void Generate(NativeArray<float> grainBank)
        {
            if (!grainBank.IsCreated)
                return;

            int length = math.max(grainBank.Length, 1);
            float invLengthMinusOne = length > 1 ? math.rcp(length - 1f) : 0f;
            for (int i = 0; i < grainBank.Length; i++)
                grainBank[i] = GenerateGranularStressEmission(i, invLengthMinusOne);
        }

        private static float GenerateGranularStressEmission(int index, float invLengthMinusOne)
        {
            float t = index * invLengthMinusOne;
            float strike = HashSigned((uint)index ^ 0xA91C52B1u);
            float friction =
                HeldNoise((uint)index, 2, 0x2D1A44C7u) * 0.62f +
                HeldNoise((uint)index, 4, 0x6B9342D1u) * 0.38f;
            float sweep = math.lerp(0.18f, 1f, t);
            float phaseA = t * math.lerp(122f, 640f, sweep) * 0.071f;
            float phaseB = t * math.lerp(244f, 1180f, sweep) * 0.047f;
            float phaseC = t * math.lerp(508f, 2330f, sweep) * 0.029f;
            float modPhase = t * math.lerp(31f, 187f, sweep) * 0.113f;
            float envelope = math.saturate(math.abs(strike) * math.abs(strike) * 0.72f);
            float modulator = LFO_TriangleOscillator(modPhase);
            float sample =
                LFO_TriangleOscillator(phaseA) * 0.48f +
                LFO_TriangleOscillator(phaseB) * 0.31f +
                LFO_TriangleOscillator(phaseC) * 0.21f;
            sample = (sample + modulator * friction * 0.45f) * (0.42f + envelope * 0.58f);
            return SoftClipSaturation(sample * 2.6f);
        }

        private static float LFO_TriangleOscillator(float phase)
        {
            float x = math.frac(phase);
            return (math.abs(x - 0.5f) * 4f) - 1f;
        }

        private static float SoftClipSaturation(float value)
        {
            return value * math.rcp(1f + math.abs(value));
        }

        private static float HeldNoise(uint sampleIndex, int shift, uint seed)
        {
            return HashSigned((sampleIndex >> shift) ^ seed);
        }

        private static float HashSigned(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return ((value & 0x00FFFFFFu) / 8388607.5f) - 1f;
        }
    }
}
