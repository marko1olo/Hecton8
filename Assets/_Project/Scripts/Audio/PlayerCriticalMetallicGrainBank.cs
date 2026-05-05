using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    /// <summary>
    /// Stateless cold-path metallic grain-bank builder for player-critical DSP synthesis.
    /// </summary>
    internal static class PlayerCriticalMetallicGrainBank
    {
        private const float TwoPi = 6.28318530718f;

        public static void Generate(NativeArray<float> grainBank)
        {
            if (!grainBank.IsCreated)
                return;

            BuildJob job = new BuildJob
            {
                GrainBank = grainBank
            };
            // COLD SYNC JOB: init-only grain-bank bake; no audio block or Tick caller waits on this handle.
            job.Schedule(grainBank.Length, 64).Complete();
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildJob : IJobParallelFor
        {
            public NativeArray<float> GrainBank;

            public void Execute(int index)
            {
                int length = math.max(GrainBank.Length, 1);
                float t = length > 1 ? index / (float)(length - 1) : 0f;
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
                float modulator = math.sin(modPhase * TwoPi);
                float sample =
                    math.sin(phaseA * TwoPi) * 0.48f +
                    math.sin(phaseB * TwoPi) * 0.31f +
                    math.sin(phaseC * TwoPi) * 0.21f;
                sample = (sample + modulator * friction * 0.45f) * (0.42f + envelope * 0.58f);
                GrainBank[index] = math.tanh(sample * 2.6f);
            }
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
