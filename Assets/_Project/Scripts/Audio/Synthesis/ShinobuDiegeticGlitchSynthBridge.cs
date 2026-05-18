using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Synthesis
{
    /// <summary>
    /// SHINOBU_75 bridge for bending the real 16-byte synth parameter ABI without adding a UI assembly dependency.
    /// </summary>
    public static class ShinobuDiegeticGlitchSynthBridge
    {
        /// <summary>Mutates one synth parameter DTO in place using continuous glitch quality.</summary>
        public static void ApplyPitchBend(ref SynthParametersDTO synth, float intensity01, float globalQualityWeight, uint frame, int index)
        {
            float intensity = Sanitize01(intensity01);
            float quality = Sanitize01(globalQualityWeight);
            float bend = intensity * math.lerp(0.22f, 1f, Smooth01(quality));
            uint h = Hash(frame ^ ((uint)index * 374761393u) ^ math.asuint(synth.PressureScalar));
            float signed = ((h & 2047u) * (1f / 1023.5f)) - 1f;
            float pitch = math.lerp(1f, math.clamp(0.72f + signed * 0.22f, 0.45f, 1.18f), bend);
            synth.BaseFrequency = math.max(20f, synth.BaseFrequency * pitch);
            synth.ModulationIndex = math.saturate(synth.ModulationIndex + bend * 0.35f);
            synth.GrainSize = math.max(0.0025f, synth.GrainSize * math.lerp(1f, 1f + math.abs(signed) * 1.45f, bend));
            synth.PressureScalar = math.saturate(math.max(synth.PressureScalar, intensity));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct SynthParametersPitchBendJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<SynthParametersDTO> SynthParameters;
            public float Intensity01;
            public float GlobalQualityWeight;
            public uint Frame;

            public void Execute(int index)
            {
                if (!SynthParameters.IsCreated || (uint)index >= (uint)SynthParameters.Length)
                    return;

                SynthParametersDTO synth = SynthParameters[index];
                ApplyPitchBend(ref synth, Intensity01, GlobalQualityWeight, Frame, index);
                SynthParameters[index] = synth;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
